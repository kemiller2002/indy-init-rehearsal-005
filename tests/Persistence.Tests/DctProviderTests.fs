/// Provider defect classes DC-004 and DC-005 (acceptance/defect-classes.json), failing-first by construction: each
/// test fails on a program that gates saving on advisory permission fields, or that reuses a mutable-read URL.
module Persistence.Tests.DctProviderTests

open Xunit
open Causa.Persistence
open Persistence.Tests.Synthetic
open Persistence.Tests.FakeGitHub
open Persistence.Tests.Harness

let private ws = "dct-provider"

let private ok (r: Result<unit, string>) =
    match r with
    | Ok() -> ()
    | Error e -> failwithf "unexpected refusal: %s" e

let private saveOffered (app: App) (form: string) (push: bool) =
    let iid = id ("dct-004/" + form + "/" + string push)
    ok (app.Step(Create(start iid "Created where the repository answer is only advisory")))
    Assert.True(Sync.canSave app.Model (app.Doc iid), "saving must be offered whatever the advisory fields say")
    iid

[<Fact>]
let ``DCT-004-A an absent permissions field is not denial; only GitHub's answer to the write decides`` () =
    // The recorded repository form without a permissions field (a real answer, recordings/run-40585189886).
    let fake = newFake ()
    fake.RepositoryForm <- "repository.read.anonymous"
    let app = App(fake)
    ok (app.Connect ws)
    Assert.Equal("authorizedEmpty", app.Connection)
    Assert.Equal(None, app.Model.Repository |> Option.bind (fun r -> r.CanPush))
    let iid = saveOffered app "absent" true
    ok (app.Step(Save iid))
    Assert.Equal("remotelySynced", app.Status iid)

[<Fact>]
let ``DCT-004-A push false is advisory; a refused write is held as write-denied and a renewed credential saves it`` () =
    let fake = newFake ()
    fake.RepositoryForm <- "repository.read.installationToken"
    fake.Push <- false
    fake.ReadOnly <- Set.ofList [ "Bearer " + token ]
    let app = App(fake)
    ok (app.Connect ws)
    let iid = saveOffered app "push-false" false
    let head = fake.Head branch
    ok (app.Step(Save iid))
    // GitHub refused (recorded FORBIDDEN): nothing was written, the change is held, the person may renew the token.
    Assert.Equal("failed", app.Status iid)
    Assert.Equal(head, fake.Head branch)
    match (app.Doc iid).Book.Phase with
    | IndyInitRootCauseInvestigator.Engine.Sync.Held(_, _, IndyInitRootCauseInvestigator.Engine.Sync.NotWritten(IndyInitRootCauseInvestigator.Engine.Sync.Refused IndyInitRootCauseInvestigator.Engine.Sync.Forbidden)) -> ()
    | other -> failwithf "expected a write-denied hold, got %A" other
    let renewed = "causa-renewed-write-token"
    fake.Authorize renewed
    ok (app.Connect(ws, renewed))
    ok (app.Step(Save iid))
    Assert.Equal("remotelySynced", app.Status iid)

[<Fact>]
[<Trait("obligation", "VPO-2d4b7557ce013e50")>]
let ``DCT-005-A VPO-2d4b7557ce013e50 every mutable read has a URL unique across sessions and page loads, and the new head is observed`` () =
    let fake = newFake ()
    let app = App(fake)
    ok (app.Connect ws)
    let iid = id "dct-005/doc"
    ok (app.Step(Create(start iid "Freshness")))
    ok (app.Step(Save iid))
    let afterWrite = fake.Head branch
    Assert.Equal(Some afterWrite, app.Doc(iid).ConfirmedHead)
    // Another writer moves the branch; a reload (new page load) must observe the new head, not a cached one.
    let moved = fake.CommitFiles(branch, [ "elsewhere/fresh.txt", utf8 "moved\n" ])
    ok (app.Reload())
    ok (app.Connect ws)
    Assert.Equal(Some moved, app.Model.Head)
    // A second tab (another session) as well.
    let other = App(fake)
    ok (other.Connect ws)
    let mutableReads =
        fake.Requests
        |> List.filter (fun r -> r.Method = "GET" && (r.Url.Contains "/git/ref/heads/" || r.Url.EndsWith("/" + repo) || r.Url.Contains("/" + repo + "?")))
    Assert.True(mutableReads.Length >= 6)
    Assert.True(mutableReads |> List.forall (fun r -> r.Fresh && r.Url.Contains "causa_fresh="))
    Assert.Equal(mutableReads.Length, (mutableReads |> List.map (fun r -> r.Url) |> List.distinct).Length)
    // Content-addressed reads (listings at a commit id, blobs) may repeat: they are immutable.
    Assert.True(fake.Requests |> List.exists (fun r -> not r.Fresh))

[<Fact>]
[<Trait("obligation", "VPO-533a2328decf91cb")>]
let ``VPO-533a2328decf91cb an untyped GraphQL error (recorded live) is an unknown outcome, never a refusal`` () =
    match GitHub.parseCommit (respond "write.create.untypedError" (Some(JObj [ "errors", JArr [ JObj [ "message", JStr "Something went wrong" ] ] ]))) with
    | Error(ProviderFailure.Unknown _) -> ()
    | other -> failwithf "%A" other
