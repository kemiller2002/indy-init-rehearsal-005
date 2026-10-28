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
[<Trait("obligation", "VPO-34731ac9a977b818")>]
let ``DCT-001-A VPO-34731ac9a977b818 the publication is parented by the decided-at commit and a moved branch is a held conflict without retry`` () =
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
let ``DCT-001-B reconciliation classifies a head reached through another writer as concurrent, by ancestry and marker`` () =
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
let ``DCT-001-B an applied write followed by another writer is recognised as ours and a reload follows`` () =
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
[<Trait("obligation", "VPO-cd9dd8ec730c3d3e")>]
let ``DCT-002-A VPO-2d4b7557ce013e50 VPO-cd9dd8ec730c3d3e a late answer for a replaced write is discarded`` () =
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
[<Trait("obligation", "VPO-2d4b7557ce013e50")>]
let ``REQUEST-IDENTITY VPO-2d4b7557ce013e50 an answer to an earlier connection never lands on a newer one`` () =
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
[<Trait("obligation", "VPO-89ed6170f531961e")>]
let ``DCT-003-A VPO-89ed6170f531961e a failure after send is unknown with no retry and reconcile reads the head first`` () =
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

/// The marker of the change a document's book holds as unknown (the change whose outcome is being reconciled).
let private unknownMarker (app: App) (iid: string) =
    match app.Doc(iid).Book.Phase with
    | IndyInitRootCauseInvestigator.Engine.Sync.Unknown(_, attempt) -> attempt.Change.Marker
    | other -> failwithf "expected an unknown outcome, found %A" other

let private eventsAtHead (fake: Fake) (iid: string) =
    let files = fake.FilesAt(fake.Head branch)
    text files.[Target.investigationDir (target ws) iid + "/events.ndjson"]

// Review F-1 (R5-independent-review.md), failing first at fbe4d87: two devices decided changes at the same head; A
// saved, B's answer was lost before GitHub applied it. B's reconcile must not take A's commit for its own.
[<Fact>]
[<Trait("obligation", "VPO-b3efad6eb85360be")>]
let ``DCT-001-B DCT-003-A VPO-b3efad6eb85360be two devices: a lost write is never reconciled as applied because another device's commit followed the same head`` () =
    let fake = newFake ()
    let v = investigation "rev-two-devices" 0
    seed fake v |> ignore
    let iid = v.InvestigationId
    let a = App(fake)
    let b = App(fake)
    ok (a.Connect ws)
    ok (b.Connect ws)
    ok (a.Step(Submit(hypothesis iid v.Version "from device A")))
    ok (b.Step(Submit(hypothesis iid v.Version "from device B")))
    ok (a.Step(Save iid))
    Assert.Equal("remotelySynced", a.Status iid)
    b.Faults <- (fun r -> if r.Method = "POST" then LoseWithoutApplying else Deliver)
    ok (b.Step(Save iid))
    Assert.Equal("unknown", b.Status iid)
    b.Faults <- (fun _ -> Deliver)
    ok (b.Step(Reconcile iid))
    Assert.Equal("conflict", b.Status iid)
    Assert.NotEqual<string>("remotelySynced", b.Status iid)
    // B cannot save over A, and A's accepted event stays on the branch.
    refused (b.Step(Save iid))
    Assert.Contains("Hypothesis from device A", eventsAtHead fake iid)
    Assert.DoesNotContain("Hypothesis from device B", eventsAtHead fake iid)
    // B's own change is kept on the device.
    Assert.Contains("Hypothesis from device B", text (b.Doc(iid).Local.EventsFile))

[<Fact>]
let ``DCT-001-B two tabs or devices never write the same marker, and a reloaded page never reuses one`` () =
    let fake = newFake ()
    let v = investigation "rev-markers" 0
    seed fake v |> ignore
    let iid = v.InvestigationId
    let marker (app: App) =
        app.Faults <- (fun r -> if r.Method = "POST" then LoseWithoutApplying else Deliver)
        ok (app.Step(Save iid))
        let m = unknownMarker app iid
        app.Faults <- (fun _ -> Deliver)
        m
    let a = App(fake)
    let b = App(fake)
    ok (a.Connect ws)
    ok (b.Connect ws)
    ok (a.Step(Submit(hypothesis iid v.Version "a")))
    ok (b.Step(Submit(hypothesis iid v.Version "b")))
    let ma, mb = marker a, marker b
    Assert.NotEqual<string>(ma, mb)
    // The same tab after a reload: settle the lost write, then a new change on a new page load.
    ok (a.Reload())
    ok (a.Connect ws)
    // Connect settles the unknown write by reading (it was never applied); then a new change on the new page load.
    Assert.NotEqual<string>("unknown", a.Status iid)
    ok (a.Step(Submit(hypothesis iid (a.Doc(iid).Local.Version) "a again")))
    let ma2 = marker a
    Assert.NotEqual<string>(ma, ma2)
    Assert.NotEqual<string>(mb, ma2)

// Review F-1, second half: even a commit that carries our exact marker on the decided-at parent is not ours unless its
// tree holds exactly the bytes we sent. Reconcile reads the commit back before it claims saved.
[<Fact>]
[<Trait("obligation", "VPO-b3efad6eb85360be")>]
let ``DCT-001-B DCT-003-A VPO-b3efad6eb85360be a commit carrying our marker but other bytes is a conflict, never saved`` () =
    let fake, app, iid, _ = withChange "dct-forged-marker"
    app.Faults <- (fun r -> if r.Method = "POST" then LoseWithoutApplying else Deliver)
    ok (app.Step(Save iid))
    let marker = unknownMarker app iid
    // Another writer's commit on the same parent whose message carries the identical marker.
    let other = Store.submit (app.Doc(iid).Confirmed.Value) (hypothesis iid (app.Doc(iid).Confirmed.Value.Version) "other writer")
    match other with
    | Error r -> failwithf "%A" r
    | Ok theirs ->
        let dir = Target.investigationDir (target ws) iid
        fake.CommitFiles(branch, Store.files theirs |> List.map (fun (n, b) -> dir + "/" + n, b), "causa: other\n\n" + GitHub.markerTrailer + marker) |> ignore
    app.Faults <- (fun _ -> Deliver)
    ok (app.Step(Reconcile iid))
    Assert.Equal("conflict", app.Status iid)
    Assert.Contains("Hypothesis other writer", eventsAtHead fake iid)
    refused (app.Step(Save iid))

[<Fact>]
[<Trait("obligation", "VPO-b3efad6eb85360be")>]
let ``DCT-001-B DCT-003-A VPO-b3efad6eb85360be a write applied before its answer was lost is confirmed only after its bytes are read back`` () =
    let fake, app, iid, _ = withChange "dct-applied-readback"
    app.Faults <- (fun r -> if r.Method = "POST" then ApplyThenLose else Deliver)
    ok (app.Step(Save iid))
    Assert.Equal("unknown", app.Status iid)
    let before = fake.Requests.Length
    app.Faults <- (fun _ -> Deliver)
    ok (app.Step(Reconcile iid))
    Assert.Equal("remotelySynced", app.Status iid)
    // The reconcile read the head, then listed the investigation's files at our commit (the read-back).
    let reads = fake.Requests |> List.skip before |> List.map (fun r -> r.Url)
    let compareAt = reads |> List.findIndex (fun u -> u.Contains "/compare/")
    Assert.True(reads |> List.skip compareAt |> List.exists (fun u -> u.Contains "/contents/" && u.Contains "?ref="), sprintf "%A" reads)

// Review F-1: when the read-back itself fails, the outcome stays unknown (never saved, never retried).
[<Fact>]
[<Trait("obligation", "VPO-b3efad6eb85360be")>]
let ``DCT-003-A VPO-b3efad6eb85360be a reconcile whose read-back fails stays unknown`` () =
    let fake, app, iid, _ = withChange "dct-readback-fails"
    app.Faults <- (fun r -> if r.Method = "POST" then ApplyThenLose else Deliver)
    ok (app.Step(Save iid))
    let mutable compared = false
    app.Faults <-
        (fun r ->
            if r.Url.Contains "/compare/" then
                compared <- true
                Deliver
            elif compared && r.Method = "GET" then
                LoseWithoutApplying
            else
                Deliver)
    ok (app.Step(Reconcile iid))
    Assert.True(compared)
    Assert.Equal("unknown", app.Status iid)
    Assert.False(Sync.canSave app.Model (app.Doc iid))

// Review F-2: an error type GitHub did not document as "not applied", answered after the write was sent, may have saved.
[<Fact>]
[<Trait("obligation", "VPO-b3efad6eb85360be")>]
let ``DCT-003-A VPO-b3efad6eb85360be a GraphQL error of an unrecognised type after send is unknown, never not-written with retry`` () =
    let _, app, iid, _ = withChange "dct-f2"
    let answer = { Status = 200; Headers = [ "content-type", "application/json; charset=utf-8" ]; Body = """{"data":{"createCommitOnBranch":null},"errors":[{"type":"INTERNAL","message":"x"}]}""" }
    app.Faults <- (fun r -> if r.Method = "POST" then Answer answer else Deliver)
    ok (app.Step(Save iid))
    Assert.Equal("unknown", app.Status iid)
    Assert.False(Sync.canSave app.Model (app.Doc iid))
    Assert.True(Sync.canReconcile app.Model (app.Doc iid))
    match GitHub.parseCommit answer with
    | Error(ProviderFailure.Unknown _) -> ()
    | other -> failwithf "%A" other

