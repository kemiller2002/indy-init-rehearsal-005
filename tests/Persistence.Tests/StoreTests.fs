/// The persistence adapter over the engine's storage contract (Boundary C): one validator, the engine's.
module Persistence.Tests.StoreTests

open System
open Xunit
open Causa.Persistence
open Persistence.Tests.Synthetic

module W = IndyInitRootCauseInvestigator.Engine.Wire
module Ev = IndyInitRootCauseInvestigator.Engine.Evolve
module H = IndyInitRootCauseInvestigator.Engine.Hashing
module S = IndyInitRootCauseInvestigator.Engine.Storage
module CJ = IndyInitRootCauseInvestigator.Engine.Json

let private codeOf r =
    match r with
    | Ok _ -> "OK"
    | Error(e: ContractError) -> e.Code

/// Files for an arbitrary event list, written exactly as the engine writes them but WITHOUT its admissibility check,
/// so that a forged (inadmissible) history can be presented to the reader.
let private forge (events: IndyInitRootCauseInvestigator.Engine.DomainEvent list) =
    let eventsBytes =
        match S.appendBytes events with
        | Ok b -> b
        | Error e -> failwith e.Detail
    let state = Ev.replay events |> Option.get
    let lastAt = (List.last events).At
    let snapshot = CJ.canonical (W.snapshot state lastAt)
    let manifest = CJ.canonical (W.manifest state (int64 events.Length) (H.sha256Hex eventsBytes) (H.sha256OfText snapshot) (List.head events).At lastAt)
    [ Store.manifestName, utf8 (manifest + "\n"); Store.eventsName, eventsBytes; Store.snapshotName, utf8 (snapshot + "\n") ]

[<Fact>]
[<Trait("obligation", "VPO-9c6055c47e4eb07d")>]
[<Trait("obligation", "VPO-5933d986a6ba6dc3")>]
let ``VPO-9c6055c47e4eb07d VPO-5933d986a6ba6dc3 a command-created investigation is a v1 file set that reads back byte-identically through the engine`` () =
    let v = investigation "roundtrip" 2
    match Store.read (raw v) with
    | Ok r ->
        Assert.Equal(4L, r.Version)
        Assert.Equal("active", r.Lifecycle)
        Assert.Equal<byte[]>(v.EventsFile, r.EventsFile)
        Assert.Equal<byte[]>(v.Snapshot, r.Snapshot)
        Assert.Equal(v.StateSha256, r.StateSha256)
    | Error e -> failwithf "%s %s" e.Code e.Detail

[<Fact>]
[<Trait("obligation", "VPO-acd13bfa08dba51a")>]
[<Trait("obligation", "VPO-c90593e00b2dae72")>]
[<Trait("obligation", "VPO-c135d16c59f5baf8")>]
let ``VPO-acd13bfa08dba51a VPO-c90593e00b2dae72 VPO-c135d16c59f5baf8 an inadmissible history is refused through the persistence path by the engine's History.validate`` () =
    let v = investigation "inadmissible" 1
    let events = v.Loaded.Events
    let duplicate = { List.last events with Version = int64 events.Length + 1L; CommandId = Guid(id "forged-command") }
    let files = forge (events @ [ duplicate ])
    match Store.read { DirectoryName = v.InvestigationId; Entries = files } with
    | Error e ->
        Assert.Equal(Codes.historyInadmissible, e.Code)
        Assert.True(e.DomainCode.IsSome)
    | Ok _ -> failwith "an inadmissible history loaded"
    // The same forged directory fails the same way when it arrives through the application (Sync load).
    let fake = Persistence.Tests.Harness.newFake ()
    let t = Persistence.Tests.Harness.target "inadmissible"
    fake.CommitFiles(Persistence.Tests.Harness.branch, files |> List.map (fun (n, b) -> Target.investigationDir t v.InvestigationId + "/" + n, b)) |> ignore
    let app = Persistence.Tests.Harness.App(fake)
    app.Connect "inadmissible" |> ignore
    Assert.Equal("incompatible", app.Connection)
    Assert.False(app.Model.Documents.ContainsKey v.InvestigationId)
    Assert.Equal(Codes.historyInadmissible, (app.Model.LoadErrors |> List.head |> snd).Code)

[<Fact>]
[<Trait("obligation", "VPO-c135d16c59f5baf8")>]
[<Trait("obligation", "VPO-f52d9071426e82b3")>]
let ``VPO-c135d16c59f5baf8 VPO-f52d9071426e82b3 malformed and forged file sets fail closed at the first failing stage`` () =
    let v = investigation "forged" 1
    let r = raw v
    let events = text v.EventsFile
    let cases =
        [ "extra file", { r with Entries = r.Entries @ [ "notes.txt", utf8 "x" ] }, Codes.pathInvalid
          "bad directory", { r with DirectoryName = "../escape" }, Codes.pathInvalid
          "CR", replaceEntry Store.manifestName (utf8 ((text v.Manifest).Replace("\n", "\r\n"))) r, Codes.encoding
          "BOM", replaceEntry Store.snapshotName (Array.append [| 0xEFuy; 0xBBuy; 0xBFuy |] v.Snapshot) r, Codes.encoding
          "blank line", replaceEntry Store.eventsName (utf8 (events + "\n")) r, Codes.encoding
          "not JSON", replaceEntry Store.manifestName (utf8 "{oops}\n") r, Codes.jsonMalformed
          "float", replaceEntry Store.manifestName (utf8 "{\"a\":1.5}\n") r, Codes.jsonMalformed
          "non-canonical", replaceEntry Store.manifestName (utf8 ((text v.Manifest).Replace(":", ": "))) r, Codes.nonCanonical
          "future version", replaceEntry Store.manifestName (utf8 ((text v.Manifest).Replace("\"formatVersion\":\"1.0.0\"", "\"formatVersion\":\"2.0.0\""))) r, Codes.unsupportedVersion
          "unknown event", replaceEntry Store.eventsName (utf8 (events.Replace("\"eventType\":\"HypothesisRecorded\"", "\"eventType\":\"HypothesisDeleted\""))) r, Codes.unknownEventType
          "unknown field", replaceEntry Store.manifestName (utf8 ((text v.Manifest).Replace("{\"aggregateVersion\"", "{\"aaa\":1,\"aggregateVersion\""))) r, Codes.schemaViolation
          "altered event", replaceEntry Store.eventsName (utf8 (events.Replace("Hypothesis 1", "Hypothesis X"))) r, Codes.eventsHashMismatch ]
    for (name, input, expected) in cases do
        let got = codeOf (Store.read input)
        Assert.True((got = expected), sprintf "%s: expected %s, got %s" name expected got)

[<Fact>]
[<Trait("obligation", "VPO-c135d16c59f5baf8")>]
let ``VPO-c135d16c59f5baf8 a self-consistent but forged snapshot is refused as not the projection of the history`` () =
    let v = investigation "forged-snapshot" 1
    let forged = (text v.Snapshot).Replace("\"lifecycle\":\"active\"", "\"lifecycle\":\"resolved\"")
    let manifest = (text v.Manifest).Replace(v.SnapshotSha256, Json.sha256OfText (forged.TrimEnd('\n')))
    let input = raw v |> replaceEntry Store.snapshotName (utf8 forged) |> replaceEntry Store.manifestName (utf8 manifest)
    Assert.Equal(Codes.snapshotMismatch, codeOf (Store.read input))

[<Fact>]
let ``a change decided at an older version is refused by the engine and builds nothing`` () =
    let v = investigation "stale" 1
    match Store.submit v (hypothesis v.InvestigationId (v.Version - 1L) "late") with
    | Error r -> Assert.Equal("VERSION.STALE", r.Code)
    | Ok _ -> failwith "a stale decision was appended"
    Assert.True(Result.isOk (Store.submit v (hypothesis v.InvestigationId v.Version "late")))

[<Fact>]
let ``a successor that rewrites accepted history is refused`` () =
    let a = investigation "rewrite-a" 1
    let b = investigation "rewrite-a" 2
    Assert.Equal("OK", codeOf (Store.successor a b))
    let other = Store.submit a (hypothesis a.InvestigationId a.Version "different") |> Result.defaultWith (fun e -> failwith e.Code)
    Assert.Equal(Codes.historyRewritten, codeOf (Store.successor b other))

[<Fact>]
[<Trait("obligation", "VPO-2d84a4c6df612b0d")>]
let ``VPO-2d84a4c6df612b0d data of an unsupported contract version fails closed`` () =
    let v = investigation "versioned" 0
    let events = (text v.EventsFile).Replace("\"semanticVersion\":\"1.0.0\"", "\"semanticVersion\":\"1.1.0\"")
    Assert.Equal(Codes.unsupportedVersion, codeOf (Store.read (raw v |> replaceEntry Store.eventsName (utf8 events))))
