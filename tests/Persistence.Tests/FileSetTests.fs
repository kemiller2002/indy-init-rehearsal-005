/// Storage-contract reader/writer tests (causa-data CONTRACT v1.0.0 §2–§11) on synthetic histories.
module Persistence.Tests.FileSetTests

open Xunit
open Causa.Persistence
open Persistence.Tests.Synthetic

let private codeOf r =
    match r with
    | Ok _ -> "OK"
    | Error(e: ContractError) -> e.Code

[<Fact>]
[<Trait("obligation", "VPO-9c6055c47e4eb07d")>]
[<Trait("obligation", "VPO-5933d986a6ba6dc3")>]
let ``VPO-9c6055c47e4eb07d VPO-5933d986a6ba6dc3 a created investigation is written as a v1 file set that reads back byte-identically`` () =
    let v = investigation "roundtrip" 2
    let again = FileSet.read engine (raw v)
    match again with
    | Ok r ->
        Assert.Equal(4L, r.Version)
        Assert.Equal("active", r.Lifecycle)
        Assert.Equal<byte[]>(v.EventsFile, r.EventsFile)
        Assert.Equal<byte[]>(v.Snapshot, r.Snapshot)
        Assert.Equal(v.StateSha256, r.StateSha256)
    | Error e -> failwithf "%s %s" e.Code e.Detail

[<Fact>]
[<Trait("obligation", "VPO-c90593e00b2dae72")>]
let ``VPO-c90593e00b2dae72 the reader never decides admissibility itself; without the engine it says not evaluated`` () =
    let v = investigation "no-engine" 0
    match v.Admissibility with
    | NotEvaluated _ -> ()
    | other -> failwithf "expected NotEvaluated, got %A" other

[<Fact>]
[<Trait("obligation", "VPO-acd13bfa08dba51a")>]
[<Trait("obligation", "VPO-c135d16c59f5baf8")>]
let ``VPO-acd13bfa08dba51a VPO-c135d16c59f5baf8 an inadmissible verdict from the engine fails the load closed with the domain code`` () =
    let v = investigation "inadmissible" 1
    let refusing = { Name = "refusing"; Evaluate = fun _ -> Inadmissible("HISTORY.INADMISSIBLE_EVENT", "refused by the engine") }
    match FileSet.read refusing (raw v) with
    | Error e ->
        Assert.Equal(Codes.historyInadmissible, e.Code)
        Assert.Equal(Some "HISTORY.INADMISSIBLE_EVENT", e.DomainCode)
    | Ok _ -> failwith "an inadmissible history loaded"

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
          "CR", replaceEntry FileSet.manifestName (utf8 ((text v.Manifest).Replace("\n", "\r\n"))) r, Codes.encoding
          "BOM", replaceEntry FileSet.snapshotName (Array.append [| 0xEFuy; 0xBBuy; 0xBFuy |] v.Snapshot) r, Codes.encoding
          "blank line", replaceEntry FileSet.eventsName (utf8 (events + "\n")) r, Codes.encoding
          "not JSON", replaceEntry FileSet.manifestName (utf8 "{oops}\n") r, Codes.jsonMalformed
          "duplicate key", replaceEntry FileSet.manifestName (utf8 ("{\"a\":1,\"a\":2}\n")) r, Codes.jsonMalformed
          "float", replaceEntry FileSet.manifestName (utf8 ("{\"a\":1.5}\n")) r, Codes.jsonMalformed
          "non-canonical", replaceEntry FileSet.manifestName (utf8 ((text v.Manifest).Replace(":", ": "))) r, Codes.nonCanonical
          "future version", replaceEntry FileSet.manifestName (utf8 ((text v.Manifest).Replace("\"formatVersion\":\"1.0.0\"", "\"formatVersion\":\"2.0.0\""))) r, Codes.unsupportedVersion
          "unknown event", replaceEntry FileSet.eventsName (utf8 (events.Replace("\"eventType\":\"HypothesisRecorded\"", "\"eventType\":\"HypothesisDeleted\""))) r, Codes.unknownEventType
          "unknown field", replaceEntry FileSet.manifestName (utf8 ((text v.Manifest).Replace("{\"aggregateVersion\"", "{\"aaa\":1,\"aggregateVersion\""))) r, Codes.schemaViolation
          "altered event", replaceEntry FileSet.eventsName (utf8 (events.Replace("Hypothesis 1", "Hypothesis X"))) r, Codes.eventsHashMismatch ]
    for (name, input, expected) in cases do
        let got = codeOf (FileSet.read engine input)
        Assert.True((got = expected), sprintf "%s: expected %s, got %s" name expected got)

[<Fact>]
[<Trait("obligation", "VPO-c135d16c59f5baf8")>]
let ``VPO-c135d16c59f5baf8 a self-consistent but forged snapshot is refused as not the projection of the history`` () =
    let v = investigation "forged-snapshot" 1
    let forged = (text v.Snapshot).Replace("\"lifecycle\":\"active\"", "\"lifecycle\":\"resolved\"")
    let manifest =
        (text v.Manifest).Replace(v.SnapshotSha256, Json.sha256OfText (forged.TrimEnd('\n')))
    let input = raw v |> replaceEntry FileSet.snapshotName (utf8 forged) |> replaceEntry FileSet.manifestName (utf8 manifest)
    Assert.Equal(Codes.snapshotMismatch, codeOf (FileSet.read engine input))

[<Fact>]
[<Trait("obligation", "VPO-d7fbd84e40ded145")>]
let ``VPO-d7fbd84e40ded145 an append decided at an older version is refused as stale and builds nothing`` () =
    let v = investigation "stale" 1
    let next = hypothesis v.InvestigationId 4L "late"
    Assert.Equal(Codes.versionStale, codeOf (FileSet.append engine v.InvestigationId (Some v) 2L [ next ]))
    Assert.Equal("OK", codeOf (FileSet.append engine v.InvestigationId (Some v) 3L [ next ]))

[<Fact>]
[<Trait("obligation", "VPO-d7fbd84e40ded145")>]
let ``VPO-d7fbd84e40ded145 a successor that rewrites accepted history is refused`` () =
    let a = investigation "rewrite-a" 1
    let b = investigation "rewrite-a" 2
    Assert.Equal("OK", codeOf (FileSet.successor a b))
    let other = investigation "rewrite-b" 3
    Assert.Equal(Codes.historyRewritten, codeOf (FileSet.successor b other))

[<Fact>]
[<Trait("obligation", "VPO-2d84a4c6df612b0d")>]
let ``VPO-2d84a4c6df612b0d data of an unsupported contract version fails closed`` () =
    let v = investigation "versioned" 0
    let events = (text v.EventsFile).Replace("\"semanticVersion\":\"1.0.0\"", "\"semanticVersion\":\"1.1.0\"")
    Assert.Equal(Codes.unsupportedVersion, codeOf (FileSet.read engine (raw v |> replaceEntry FileSet.eventsName (utf8 events))))

[<Fact>]
[<Trait("obligation", "VPO-0304a8342e7147dd")>]
let ``VPO-0304a8342e7147dd event ids are derived per the contract and a wrong id is refused`` () =
    let iid = id "inv/ids"
    let e = started iid "ids"
    // Known derivation shape: v5 UUID, lowercase canonical.
    let eid = match e with JObj o -> Json.str "eventId" o |> Option.defaultValue "" | _ -> ""
    Assert.True(Uuid.isValid eid)
    Assert.Equal('5', eid[14])
    let v = investigation "ids" 0
    let bad = (text v.EventsFile).Replace(eid, Uuid.v5 ns "other")
    let bytes = utf8 bad
    let manifest = (text v.Manifest).Replace(v.EventsSha256, Json.sha256Hex bytes)
    let input = raw v |> replaceEntry FileSet.eventsName bytes |> replaceEntry FileSet.manifestName (utf8 manifest)
    Assert.Equal(Codes.eventIdMismatch, codeOf (FileSet.read engine input))
