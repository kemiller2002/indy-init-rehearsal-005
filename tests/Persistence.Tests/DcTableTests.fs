/// Core's write-invariant scenario table (DCT-001..DCT-003 and request identity, tests/App.Engine.Tests/
/// WriteInvariantTests.fs) run through the outer persistence program the shell drives, end to end against the
/// recorded-behaviour fake: the invariants must hold for the application, not only for the book in isolation.
module Persistence.Tests.DcTableTests

open Xunit
open Causa.Persistence
open Persistence.Tests.Synthetic
open Persistence.Tests.FakeGitHub
open Persistence.Tests.Harness

let private ws = "dc-table"

let private seed (fake: Fake) (v: Verified) =
    let t = target ws
    fake.CommitFiles(branch, Store.files v |> List.map (fun (n, b) -> Target.investigationDir t v.InvestigationId + "/" + n, b))

let private ok (r: Result<unit, string>) =
    match r with
    | Ok() -> ()
    | Error e -> failwithf "unexpected refusal: %s" e

let private refused (r: Result<unit, string>) = Assert.True(Result.isError r, "the step was expected to be refused")

let private posts (fake: Fake) = fake.Requests |> List.filter (fun r -> r.Method = "POST")

/// A connected app with one seeded investigation and one local change decided at the head it was read at.
let private withChange (name: string) =
    let fake = newFake ()
    let v = investigation name 0
    let readAt = seed fake v
    let app = App(fake)
    ok (app.Connect ws)
    ok (app.Step(Submit(hypothesis v.InvestigationId v.Version "local")))
    fake, app, v.InvestigationId, readAt

[<Fact>]
[<Trait("obligation", "VPO-d7fbd84e40ded145")>]
[<Trait("obligation", "VPO-34731ac9a977b818")>]
let ``DCT-001-A VPO-d7fbd84e40ded145 VPO-34731ac9a977b818 the publication is parented by the decided-at commit and a moved branch is a held conflict without retry`` () =
    let fake, app, iid, readAt = withChange "dct-001-a"
    let moved = fake.CommitFiles(branch, [ "elsewhere/x.txt", utf8 "another writer\n" ])
    ok (app.Step(Save iid))
    let body = (posts fake).Head.Body |> Option.defaultValue ""
    Assert.Contains("\"expectedHeadOid\":\"" + readAt + "\"", body)
    Assert.Equal(moved, fake.Head branch)
    Assert.Equal("conflict", app.Status iid)
    Assert.False(Sync.canSave app.Model (app.Doc iid))
    refused (app.Step(Save iid))
    Assert.Single(posts fake) |> ignore

[<Fact>]
[<Trait("obligation", "VPO-d7fbd84e40ded145")>]
let ``DCT-001-B VPO-d7fbd84e40ded145 reconciliation classifies a head reached through another writer as concurrent, by ancestry and marker`` () =
    let fake, app, iid, _ = withChange "dct-001-b"
    // The write is lost before GitHub applies it; then another writer advances the branch.
    app.Faults <- (fun r -> if r.Method = "POST" then LoseWithoutApplying else Deliver)
    ok (app.Step(Save iid))
    Assert.Equal("unknown", app.Status iid)
    fake.CommitFiles(branch, [ "elsewhere/y.txt", utf8 "another writer\n" ]) |> ignore
    app.Faults <- (fun _ -> Deliver)
    ok (app.Step(Reconcile iid))
    // The head descends from the base, but not through our marked commit: concurrent, never "applied".
    Assert.Equal("conflict", app.Status iid)
    Assert.True(fake.Requests |> List.exists (fun r -> r.Url.Contains "/compare/"))

[<Fact>]
[<Trait("obligation", "VPO-d7fbd84e40ded145")>]
let ``DCT-001-B VPO-d7fbd84e40ded145 an applied write followed by another writer is recognised as ours and a reload follows`` () =
    let fake, app, iid, _ = withChange "dct-001-b-ours"
    app.Faults <- (fun r -> if r.Method = "POST" then ApplyThenLose else Deliver)
    ok (app.Step(Save iid))
    Assert.Equal("unknown", app.Status iid)
    let after = fake.CommitFiles(branch, [ "elsewhere/z.txt", utf8 "another writer after us\n" ])
    app.Faults <- (fun _ -> Deliver)
    ok (app.Step(Reconcile iid))
    Assert.Equal("remotelySynced", app.Status iid)
    // ReloadRequired: the program re-read the branch, so the document now observes the newer head.
    Assert.Equal(Some after, app.Doc(iid).ConfirmedHead)

[<Fact>]
[<Trait("obligation", "VPO-2d4b7557ce013e50")>]
[<Trait("obligation", "VPO-8c435e1ebdf43e7c")>]
[<Trait("obligation", "VPO-cd9dd8ec730c3d3e")>]
let ``DCT-002-A VPO-2d4b7557ce013e50 VPO-8c435e1ebdf43e7c VPO-cd9dd8ec730c3d3e a late answer for a replaced write is discarded`` () =
    let fake, app, iid, _ = withChange "dct-002-a"
    // First attempt never leaves the device (held, not written); the retry is held in flight.
    app.Faults <- (fun r -> if r.Method = "POST" then NotSentFault else Deliver)
    ok (app.Step(Save iid))
    let firstId =
        app.Effects
        |> List.choose (fun e -> match e with Send(id, r) when r.Method = "POST" -> Some id | _ -> None)
        |> List.last
    Assert.Equal("queuedOffline", app.Status iid)
    // Back online: reconnect (the held change is kept and re-offered), then retry; the retry is held in flight.
    app.Faults <- (fun r -> if r.Method = "POST" then Hold else Deliver)
    ok (app.Connect ws)
    ok (app.Step(Save iid))
    Assert.Equal("syncingRemotely", app.Status iid)
    let before = app.Model
    // A late answer naming the first (replaced) request changes nothing.
    refused (app.Step(HttpAnswered(firstId, respond "write.create" (Some(JObj [ "data", JObj [ "createCommitOnBranch", JObj [ "commit", JObj [ "oid", JStr(String.replicate 40 "a") ] ] ] ])))))
    Assert.Equal(before, app.Model)
    // The real answer settles it.
    app.Faults <- (fun _ -> Deliver)
    let heldId, _ = app.Held.Head
    ok (app.DeliverHeld heldId)
    Assert.Equal("remotelySynced", app.Status iid)

[<Fact>]
[<Trait("obligation", "VPO-8c435e1ebdf43e7c")>]
[<Trait("obligation", "VPO-2d4b7557ce013e50")>]
let ``REQUEST-IDENTITY VPO-8c435e1ebdf43e7c VPO-2d4b7557ce013e50 an answer to an earlier connection never lands on a newer one`` () =
    let fake = newFake ()
    let app = App(fake)
    app.Faults <- (fun _ -> Hold)
    ok (app.Connect ws)
    let firstId, _ = app.Held.Head
    app.Faults <- (fun _ -> Deliver)
    ok (app.Connect ws)
    let before = app.Model
    refused (app.DeliverHeld firstId)
    Assert.Equal(before, app.Model)

[<Fact>]
[<Trait("obligation", "VPO-199103f9166730e7")>]
[<Trait("obligation", "VPO-6886a2a6e612f704")>]
let ``DCT-002-B VPO-199103f9166730e7 VPO-6886a2a6e612f704 every user event is accepted only in its legal source states and a refusal changes nothing`` () =
    let fake, app, iid, _ = withChange "dct-002-b"
    let check (msg: Msg) =
        let before = app.Model
        let effects = app.Effects.Length
        refused (app.Step msg)
        Assert.Equal(before, app.Model)
        Assert.Equal(effects, app.Effects.Length)
    // Pending: nothing to reconcile.
    check (Reconcile iid)
    // Sending: no second save, no edit, no discard, no reload, no reconnect.
    app.Faults <- (fun r -> if r.Method = "POST" then Hold else Deliver)
    ok (app.Step(Save iid))
    check (Save iid)
    check (Submit(hypothesis iid 3L "during send"))
    check (DiscardLocal iid)
    check (ReloadRemote iid)
    check (Connect(target ws, credential token))
    check Disconnect
    // Unknown: no save (no retry), no edit, no discard until reconciled.
    let heldId, _ = app.Held.Head
    app.Dispatch(HttpFailed(heldId, TransportFailure.OutcomeUnknown "lost")) |> ignore
    app.Run()
    Assert.Equal("unknown", app.Status iid)
    check (Save iid)
    check (Submit(hypothesis iid 3L "during unknown"))
    check (DiscardLocal iid)
    ignore fake

[<Fact>]
[<Trait("obligation", "VPO-b3efad6eb85360be")>]
[<Trait("obligation", "VPO-89ed6170f531961e")>]
let ``DCT-003-A VPO-b3efad6eb85360be VPO-89ed6170f531961e a failure after send is unknown with no retry and reconcile reads the head first`` () =
    let fake, app, iid, _ = withChange "dct-003-a"
    app.Faults <- (fun r -> if r.Method = "POST" then ApplyThenLose else Deliver)
    ok (app.Step(Save iid))
    Assert.Equal("unknown", app.Status iid)
    Assert.False(Sync.canSave app.Model (app.Doc iid))
    refused (app.Step(Save iid))
    app.Faults <- (fun _ -> Deliver)
    let before = fake.Requests.Length
    ok (app.Step(Reconcile iid))
    let first = fake.Requests |> List.skip before |> List.head
    Assert.Contains("/git/ref/heads/", first.Url)
    Assert.True(first.Fresh)
    Assert.Equal("remotelySynced", app.Status iid)
    Assert.Single(posts fake) |> ignore

[<Fact>]
[<Trait("obligation", "VPO-b3efad6eb85360be")>]
let ``DCT-003-A VPO-b3efad6eb85360be saved is claimed only after the read-back matches the sent bytes`` () =
    let fake, app, iid, _ = withChange "dct-003-a-readback"
    // Let the commit through, hold the read-back.
    app.Faults <- (fun r -> if r.Method = "POST" then Deliver else Hold)
    ok (app.Step(Save iid))
    Assert.Equal("syncingRemotely", app.Status iid)
    let readId, held = app.Held.Head
    // The read-back finds other bytes than were sent (one blob id differs): not saved, unknown.
    let tampered =
        match held with
        | Ok r ->
            let firstSha = r.Body.Substring(r.Body.IndexOf("\"sha\":\"") + 7, 40)
            Ok { r with Body = r.Body.Replace(firstSha, String.replicate 40 "e") }
        | other -> other
    match tampered with
    | Ok r -> ok (app.Step(HttpAnswered(readId, r)))
    | Error _ -> failwith "expected a held answer"
    Assert.Equal("unknown", app.Status iid)
    Assert.Equal(None, app.Doc(iid).LastSyncedAt)
    ignore fake
