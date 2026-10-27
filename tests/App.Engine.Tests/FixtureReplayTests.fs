/// T15: frozen fixture replay against the Causa storage contract v1.0.0 (kemiller2002/causa-data at
/// causa-storage-v1.0.0), through THIS engine (Boundary C). The data is read at test time from a causa-data checkout
/// (CAUSA_DATA_DIR, or a sibling `causa-data` directory); nothing is vendored into this repository (EXT-DATA-001).
/// A missing checkout FAILS these tests; they are never skipped.
module App.Engine.Tests.FixtureReplayTests

open System
open System.IO
open System.Text
open Xunit
open IndyInitRootCauseInvestigator.Engine

let private causaData =
    lazy
        (match Environment.GetEnvironmentVariable "CAUSA_DATA_DIR" with
         | null
         | "" ->
             let rec up (dir: DirectoryInfo | null) =
                 match dir with
                 | null -> failwith "causa-data checkout not found: set CAUSA_DATA_DIR to a checkout of kemiller2002/causa-data at causa-storage-v1.0.0"
                 | d ->
                     let candidate = Path.Combine(d.FullName, "causa-data")
                     if Directory.Exists(Path.Combine(candidate, "contract", "v1.0.0")) then candidate else up d.Parent
             up (DirectoryInfo AppContext.BaseDirectory)
         | fromEnv -> fromEnv)

let private path (parts: string list) = Path.Combine(Array.ofList (causaData.Value :: parts))
let private bytes parts = File.ReadAllBytes(path parts)
let private json parts =
    match Json.parse (Encoding.UTF8.GetString(bytes parts)) with
    | Ok j -> j
    | Error e -> failwith e

let private strField name j =
    match Json.field name j with
    | Some(JString s) -> s
    | other -> failwithf "%s: %A" name other

let private intField name j =
    match Json.field name j with
    | Some(JInt i) -> i
    | other -> failwithf "%s: %A" name other

let private arrField name j =
    match Json.field name j with
    | Some(JArray xs) -> xs
    | other -> failwithf "%s: %A" name other

let private readDirectory (dir: string) =
    Directory.GetFileSystemEntries dir
    |> Array.map (fun f -> (match Path.GetFileName f with null -> "" | n -> n), File.ReadAllBytes f)
    |> Map.ofArray

let private rootManifest () = json [ "fixtures"; "v1.0.0"; "manifest.json" ]

let private loadFixture (investigationId: string) =
    Storage.read investigationId (readDirectory (path [ "fixtures"; "v1.0.0"; "investigations"; investigationId ]))

let private canonicalDigest (b: byte[]) =
    let text = Encoding.UTF8.GetString b
    Hashing.sha256OfText (text.Substring(0, text.Length - 1))

[<Fact>]
[<Trait("obligation", "VPO-5933d986a6ba6dc3")>]
[<Trait("obligation", "VPO-2d84a4c6df612b0d")>]
[<Trait("task", "T15")>]
let ``T15 STORAGE-007 VPO-5933d986a6ba6dc3 VPO-2d84a4c6df612b0d the three release identities match the contract this engine implements`` () =
    Assert.Equal(Some Storage.schemaIdentity, Storage.embeddedSchemaIdentity ())
    Assert.Equal(Storage.schemaIdentity, Hashing.sha256Hex (bytes [ "contract"; "v1.0.0"; "schemas"; "bundle.json" ]))
    Assert.Equal(Storage.datasetIdentity, Hashing.sha256Hex (bytes [ "fixtures"; "v1.0.0"; "manifest.json" ]))
    Assert.Equal(Storage.releaseIdentity, Hashing.sha256Hex (bytes [ "compatibility"; "v1.0.0"; "lock.json" ]))
    // The embedded schema files are byte-identical to the contract's.
    for KeyValue(name, embedded) in Schema.files.Value do
        Assert.Equal<byte[]>(bytes [ "contract"; "v1.0.0"; "schemas"; yield! name.Split('/') |> List.ofArray ], embedded)

[<Fact>]
[<Trait("obligation", "VPO-acd13bfa08dba51a")>]
[<Trait("obligation", "VPO-c90593e00b2dae72")>]
[<Trait("obligation", "VPO-557743cb4997c484")>]
[<Trait("obligation", "VPO-3d67d1f2b8287e39")>]
[<Trait("obligation", "VPO-f00be0cb5a2bb0f8")>]
[<Trait("task", "T15")>]
let ``T15 VPO-acd13bfa08dba51a VPO-c90593e00b2dae72 VPO-557743cb4997c484 VPO-3d67d1f2b8287e39 five golden scenarios replay through the engine to their pinned digests and golden states`` () =
    let entries = arrField "investigations" (rootManifest ())
    Assert.Equal(5, entries.Length)
    for entry in entries do
        let id = strField "investigationId" entry
        let key = strField "scenarioKey" entry
        match loadFixture id with
        | Error e -> failwithf "%s refused: %A" key e
        | Ok loaded ->
            Assert.Equal(strField "eventsSha256" entry, loaded.EventsSha256)
            Assert.Equal(strField "snapshotSha256" entry, loaded.SnapshotSha256)
            Assert.Equal(strField "manifestSha256" entry, canonicalDigest loaded.ManifestBytes)
            Assert.Equal(strField "expectedStateSha256" entry, loaded.StateSha256)
            Assert.Equal(intField "expectedVersion" entry, loaded.State.Version)
            Assert.Equal(strField "expectedLifecycle" entry, Wire.lifecycle loaded.State.Lifecycle)
            Assert.Equal(strField "title" entry, loaded.State.Title.Value)
            // The golden projection, byte for byte.
            let golden = bytes [ "contract"; "v1.0.0"; "golden"; key + ".state.json" ]
            Assert.Equal(Encoding.UTF8.GetString golden, Json.canonical (Wire.state loaded.State) + "\n")

[<Fact>]
[<Trait("obligation", "VPO-9c6055c47e4eb07d")>]
[<Trait("obligation", "VPO-61b2e81f99761d55")>]
[<Trait("task", "T15")>]
let ``T15 VPO-9c6055c47e4eb07d VPO-61b2e81f99761d55 the writer reproduces every frozen file set byte for byte from the decoded events`` () =
    for entry in arrField "investigations" (rootManifest ()) do
        let id = strField "investigationId" entry
        match loadFixture id with
        | Error e -> failwithf "%s refused: %A" id e
        | Ok loaded ->
            match Storage.write loaded.Events with
            | Error e -> failwithf "%s not writable: %A" id e
            | Ok set ->
                Assert.Equal<byte[]>(loaded.EventsBytes, set.Events)
                Assert.Equal<byte[]>(loaded.ManifestBytes, set.Manifest)
                Assert.Equal<byte[]>(loaded.SnapshotBytes, set.Snapshot)

let private decode (j: Json) =
    match j with
    | JObject [ "base64", JString b ] -> Convert.FromBase64String b
    | other -> failwithf "not a base64 file: %A" other

let private scenarioId (key: string) =
    arrField "investigations" (rootManifest ())
    |> List.find (fun e -> strField "scenarioKey" e = key)
    |> strField "investigationId"

/// Run one negative fixture through the reader/writer; the (code, domain code) it is refused with, or None if accepted.
let private outcome (fixture: Json) : (string * string option) option =
    let refused (r: Result<'T, StorageError>) =
        match r with
        | Ok _ -> None
        | Error e -> Some(e.Code, e.DomainCode)
    match strField "kind" fixture with
    | "investigation" ->
        let files =
            match Json.field "files" fixture with
            | Some(JObject members) -> members |> List.map (fun (name, f) -> name, decode f) |> Map.ofList
            | other -> failwithf "files: %A" other
        refused (Storage.read (strField "directoryId" fixture) files)
    | "append" ->
        let baseId = scenarioId (strField "baseScenarioKey" fixture)
        match loadFixture baseId with
        | Error e -> failwithf "base refused: %A" e
        | Ok current ->
            let appended = decode (Json.field "appendedEvents" fixture |> Option.get)
            refused (Storage.append current (intField "expectedAggregateVersion" fixture) appended)
    | "successor" ->
        let baseId = scenarioId (strField "baseScenarioKey" fixture)
        match loadFixture baseId with
        | Error e -> failwithf "base refused: %A" e
        | Ok accepted ->
            let files =
                match Json.field "files" fixture with
                | Some(JObject members) -> members |> List.map (fun (name, f) -> name, decode f) |> Map.ofList
                | other -> failwithf "files: %A" other
            refused (Storage.successor accepted baseId files)
    | other -> failwithf "unknown fixture kind %s" other

[<Fact>]
[<Trait("obligation", "VPO-f52d9071426e82b3")>]
[<Trait("obligation", "VPO-c135d16c59f5baf8")>]
[<Trait("obligation", "VPO-2d84a4c6df612b0d")>]
[<Trait("obligation", "VPO-9d59cc57fd44b927")>]
[<Trait("obligation", "VPO-821ce84e76d497b9")>]
[<Trait("task", "T15")>]
let ``T15 VPO-f52d9071426e82b3 VPO-c135d16c59f5baf8 VPO-9d59cc57fd44b927 all 90 negative fixtures are refused with their expected code and domain code`` () =
    let listed = arrField "negativeFixtures" (rootManifest ())
    Assert.Equal(90, listed.Length)
    let mismatches =
        listed
        |> List.choose (fun entry ->
            let file = strField "path" entry
            let fixture = json [ "fixtures"; "v1.0.0"; yield! file.Split('/') |> List.ofArray ]
            let expected =
                strField "expectedErrorCode" fixture,
                (match Json.field "expectedDomainCode" fixture with
                 | Some(JString d) -> Some d
                 | _ -> None)
            Assert.Equal(strField "expectedErrorCode" entry, fst expected)
            let actual = (try outcome fixture with ex -> Some("EXCEPTION " + ex.Message, None))
            if actual = Some expected then None else Some $"{file}: expected {expected}, got {actual}")
    Assert.True(mismatches.IsEmpty, String.Join("\n", mismatches))

[<Fact>]
[<Trait("obligation", "VPO-dc48d066cfa2309d")>]
[<Trait("obligation", "VPO-d60e3a3a25cb106d")>]
[<Trait("task", "T15")>]
let ``T15 VPO-dc48d066cfa2309d VPO-d60e3a3a25cb106d every frozen file still has its locked digest after the replay run`` () =
    // Replay everything first (reads only), then check every frozen file against the release lock.
    for entry in arrField "investigations" (rootManifest ()) do
        loadFixture (strField "investigationId" entry) |> ignore
    let frozen = arrField "frozenFiles" (json [ "compatibility"; "v1.0.0"; "lock.json" ])
    Assert.True(frozen.Length > 100)
    for f in frozen do
        let p = strField "path" f
        Assert.True((strField "sha256" f = Hashing.sha256Hex (bytes (p.Split('/') |> List.ofArray))), $"{p} changed")

[<Fact>]
[<Trait("obligation", "VPO-c81484cbcf203d4f")>]
[<Trait("obligation", "VPO-4b4747eaff96e385")>]
[<Trait("obligation", "VPO-77f6e22e08d5bba0")>]
[<Trait("task", "T15")>]
let ``T15 EXT-DATA-013 VPO-c81484cbcf203d4f VPO-4b4747eaff96e385 the active historical catalog loads all three cases through the engine to their catalog states`` () =
    let active = json [ "catalog"; "active.json" ]
    let manifestPath = strField "manifest" active
    let catalog = json (manifestPath.Split('/') |> List.ofArray)
    let cases = arrField "cases" catalog
    Assert.Equal(3, cases.Length)
    for case in cases do
        let id = strField "investigationId" case
        let dir = path ((strField "path" case).Split('/') |> List.ofArray)
        match Storage.read id (readDirectory dir) with
        | Error e -> failwithf "%s refused: %A" (strField "caseKey" case) e
        | Ok loaded ->
            Assert.Equal(strField "eventsSha256" case, loaded.EventsSha256)
            Assert.Equal(strField "snapshotSha256" case, loaded.SnapshotSha256)
            Assert.Equal(strField "manifestSha256" case, canonicalDigest loaded.ManifestBytes)
            Assert.Equal(strField "expectedStateSha256" case, loaded.StateSha256)
            Assert.Equal(intField "expectedVersion" case, loaded.State.Version)
            Assert.Equal(strField "expectedLifecycle" case, Wire.lifecycle loaded.State.Lifecycle)
            Assert.Equal(strField "title" case, loaded.State.Title.Value)

[<Fact>]
[<Trait("obligation", "VPO-3d67d1f2b8287e39")>]
[<Trait("obligation", "VPO-557743cb4997c484")>]
[<Trait("task", "T15")>]
let ``T15 VPO-3d67d1f2b8287e39 golden scenarios.json agrees with the replayed histories (no CAUSA.GOLDEN_MISMATCH)`` () =
    let scenarios = arrField "scenarios" (json [ "contract"; "v1.0.0"; "golden"; "scenarios.json" ])
    Assert.Equal(5, scenarios.Length)
    let allTypes = Collections.Generic.HashSet<string>()
    for s in scenarios do
        match loadFixture (strField "investigationId" s) with
        | Error e -> failwithf "%A" e
        | Ok loaded ->
            Assert.Equal(intField "events" s, int64 loaded.Events.Length)
            let types = loaded.Events |> List.map (fun e -> Wire.eventType e.Body) |> List.distinct |> List.sortWith (fun a b -> String.CompareOrdinal(a, b))
            Assert.Equal<string list>(arrField "eventTypes" s |> List.map (function JString t -> t | other -> failwithf "%A" other), types)
            Assert.Equal(intField "events" s, int64 (History.groups loaded.Events |> List.collect id).Length)
            for t in types do allTypes.Add t |> ignore
    // together the valid fixtures exercise all 31 registered event types (contract §12.1)
    Assert.Equal(31, allTypes.Count)

[<Fact>]
[<Trait("obligation", "VPO-c135d16c59f5baf8")>]
[<Trait("task", "T15")>]
let ``T15 VPO-c135d16c59f5baf8 concurrent readers all verify (regression: the schema regex cache raced under parallel reads)`` () =
    let ids = arrField "investigations" (rootManifest ()) |> List.map (strField "investigationId")
    let results =
        [| for _ in 1..8 do yield! ids |]
        |> Array.Parallel.map (fun id -> loadFixture id |> Result.map (fun l -> l.StateSha256))
    Assert.All(results, fun r -> Assert.True(Result.isOk r, sprintf "%A" r))
