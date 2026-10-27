/// Historical catalog loading after Connect (T25), application level, on a synthetic catalog served by the fake.
/// The real catalog is loaded live by `causa-persistence catalog` in CI; no catalog record is bundled here.
module Persistence.Tests.CatalogAppTests

open Xunit
open Causa.Persistence
open Persistence.Tests.Synthetic
open Persistence.Tests.FakeGitHub
open Persistence.Tests.Harness

let private catalogWs = "synthetic-catalog"

type private Tamper =
    | NoTamper
    | WrongStateDigest
    | ExtraInvestigation
    | MissingCase
    | MalformedPointer
    | MalformedManifest

let private build (tamper: Tamper) =
    let fake = newFake ()
    let t = target catalogWs
    let cases = [ investigation "catalog-a" 1; investigation "catalog-b" 2 ]
    let present = if tamper = MissingCase then [ cases.Head ] else cases
    let extra = if tamper = ExtraInvestigation then [ investigation "not-in-catalog" 0 ] else []
    let files =
        (present @ extra)
        |> List.collect (fun v -> Store.files v |> List.map (fun (n, b) -> Target.investigationDir t v.InvestigationId + "/" + n, b))
    let case (i: int) (v: Verified) =
        JObj
            [ "caseKey", JStr(sprintf "case-%d" i)
              "investigationId", JStr v.InvestigationId
              "title", JStr v.Title
              "path", JStr(Target.investigationDir t v.InvestigationId)
              "expectedVersion", JInt v.Version
              "expectedLifecycle", JStr v.Lifecycle
              "expectedStateSha256", JStr(if tamper = WrongStateDigest && i = 0 then String.replicate 64 "0" else v.StateSha256)
              "eventsSha256", JStr v.EventsSha256
              "manifestSha256", JStr v.ManifestSha256
              "snapshotSha256", JStr v.SnapshotSha256 ]
    let manifest =
        JObj
            [ "catalogId", JStr "synthetic"
              "catalogVersion", JStr "1.0.0"
              "formatVersion", JStr "1.0.0"
              "workspace", JStr(Target.workspaceRoot t)
              "cases", JArr(cases |> List.mapi case) ]
    let pointer =
        JObj
            [ "activeCatalog", JStr "synthetic-v1"
              "catalogId", JStr "synthetic"
              "catalogVersion", JStr "1.0.0"
              "manifest", JStr "catalog/synthetic-v1/manifest.json"
              "workspace", JStr(Target.workspaceRoot t) ]
    fake.CommitFiles(
        branch,
        files
        @ [ "catalog/active.json", utf8 (if tamper = MalformedPointer then "{\"activeCatalog\":" else Json.canonicalText pointer + "\n")
            "catalog/synthetic-v1/manifest.json", utf8 (if tamper = MalformedManifest then "{\"cases\":[{\"caseKey\":1}]}\n" else Json.canonicalText manifest + "\n") ]
    )
    |> ignore
    fake, cases

[<Fact>]
[<Trait("obligation", "VPO-c81484cbcf203d4f")>]
[<Trait("obligation", "VPO-d60e3a3a25cb106d")>]
[<Trait("obligation", "VPO-dc48d066cfa2309d")>]
[<Trait("obligation", "VPO-7ced47ef45099aab")>]
[<Trait("obligation", "VPO-557743cb4997c484")>]
let ``VPO-c81484cbcf203d4f VPO-d60e3a3a25cb106d VPO-dc48d066cfa2309d VPO-557743cb4997c484 after Connect every catalog case loads, replays to its manifest digest and is read-only`` () =
    let fake, cases = build NoTamper
    let app = App(fake)
    match app.Connect catalogWs with
    | Ok() -> ()
    | Error e -> failwith e
    Assert.Equal("connected", app.Connection)
    match app.Model.Catalog with
    | CatalogVerified(man, results) ->
        Assert.Equal(cases.Length, results.Length)
        Assert.True(results |> List.forall (fun r -> Result.isOk r.Outcome))
        Assert.Equal("synthetic", man.CatalogId)
    | other -> failwithf "%A" other
    for v in cases do
        let d = app.Doc v.InvestigationId
        Assert.True(d.ReadOnly)
        Assert.Equal(v.StateSha256, d.Local.StateSha256)
        Assert.True(Result.isError (app.Step(Submit(hypothesis v.InvestigationId v.Version "x"))))
        Assert.True(Result.isError (app.Step(Save v.InvestigationId)))
    Assert.True(Result.isError (app.Step(Create(start (id "new-in-catalog") "x"))))
    Assert.True(fake.Requests |> List.forall (fun r -> r.Method = "GET"))
    // Catalog records are never written to the browser store.
    Assert.False(app.Store |> Map.exists (fun _ v -> cases |> List.exists (fun c -> v.Contains c.InvestigationId)))

[<Fact>]
[<Trait("obligation", "VPO-c81484cbcf203d4f")>]
[<Trait("obligation", "VPO-2d84a4c6df612b0d")>]
let ``VPO-c81484cbcf203d4f a case that does not replay to its manifest digest fails the catalog closed and presents no case`` () =
    let fake, _ = build WrongStateDigest
    let app = App(fake)
    app.Connect catalogWs |> ignore
    Assert.Equal("incompatible", app.Connection)
    Assert.True(app.Model.Documents.IsEmpty)
    match app.Model.Catalog with
    | CatalogFailed reason -> Assert.Contains(Codes.catalogDrift, reason)
    | other -> failwithf "%A" other

[<Fact>]
[<Trait("obligation", "VPO-c81484cbcf203d4f")>]
let ``VPO-c81484cbcf203d4f an investigation the manifest does not list is never presented as a catalog case`` () =
    let fake, _ = build ExtraInvestigation
    let app = App(fake)
    app.Connect catalogWs |> ignore
    Assert.Equal("incompatible", app.Connection)
    Assert.True(app.Model.Documents.IsEmpty)

[<Fact>]
[<Trait("obligation", "VPO-9d59cc57fd44b927")>]
[<Trait("obligation", "VPO-f52d9071426e82b3")>]
let ``VPO-9d59cc57fd44b927 VPO-f52d9071426e82b3 a malformed catalog pointer or manifest fails closed and presents no case`` () =
    for tamper in [ MalformedPointer; MalformedManifest ] do
        let fake, _ = build tamper
        let app = App(fake)
        app.Connect catalogWs |> ignore
        Assert.True((app.Connection = "incompatible"), sprintf "%A: %s %A" tamper app.Connection app.Model.Catalog)
        Assert.True(app.Model.Documents.IsEmpty)
        match app.Model.Catalog with
        | CatalogFailed _ -> ()
        | other -> failwithf "%A: %A" tamper other

[<Fact>]
[<Trait("obligation", "VPO-c81484cbcf203d4f")>]
[<Trait("obligation", "VPO-9d59cc57fd44b927")>]
let ``VPO-c81484cbcf203d4f VPO-9d59cc57fd44b927 a missing catalog case fails the catalog closed`` () =
    let fake, _ = build MissingCase
    let app = App(fake)
    app.Connect catalogWs |> ignore
    Assert.Equal("incompatible", app.Connection)
    Assert.True(app.Model.Documents.IsEmpty)

[<Fact>]
[<Trait("obligation", "VPO-8ba57ebcfe06030f")>]
let ``VPO-8ba57ebcfe06030f another workspace of the same repository does not engage the catalog`` () =
    let fake, _ = build NoTamper
    let app = App(fake)
    app.Connect "my-work" |> ignore
    Assert.Equal("authorizedEmpty", app.Connection)
    Assert.Equal(NotCatalog, app.Model.Catalog)
    Assert.True(app.Model.Documents.IsEmpty)
