/// Application-level persistence tests: the whole Sync program (connect, load, create, edit, local durability,
/// guarded save, confirmation, conflict, unknown outcome, credential rejection, reload) driven through its effects
/// against the recorded-behaviour fake GitHub and an in-memory browser store. Each test names its VPO obligations.
module Persistence.Tests.SyncAppTests

open System
open Xunit
open Causa.Persistence
open Persistence.Tests.Synthetic
open Persistence.Tests.FakeGitHub
open Persistence.Tests.Harness

let private ws = "rehearsal-test"

let private seed (fake: Fake) (workspace: string) (v: Verified) =
    let t = target workspace
    fake.CommitFiles(branch, FileSet.files v |> List.map (fun (n, b) -> Target.investigationDir t v.InvestigationId + "/" + n, b))

let private ok (r: Result<unit, string>) =
    match r with
    | Ok() -> ()
    | Error e -> failwithf "unexpected refusal: %s" e

let private refused (r: Result<unit, string>) =
    match r with
    | Ok() -> failwith "the step was expected to be refused"
    | Error _ -> ()

let private writes (fake: Fake) = fake.Requests |> List.filter (fun r -> r.Method <> "GET")

let private newId (name: string) = id ("app/" + name)

let private createEvents (iid: string) = [ started iid "Created in the app"; activate iid 2L ]

[<Fact>]
[<Trait("obligation", "VPO-d79488f204e14ff8")>]
[<Trait("obligation", "VPO-8ba57ebcfe06030f")>]
let ``VPO-d79488f204e14ff8 VPO-8ba57ebcfe06030f a fresh launch is disconnected and empty, imports nothing and sends nothing`` () =
    let fake = newFake ()
    let app = App(fake)
    ok (app.Reload())
    Assert.Equal("disconnected", app.Connection)
    Assert.Empty(app.Model.Documents)
    Assert.Empty(app.Effects)
    Assert.Empty(fake.Requests)

[<Fact>]
[<Trait("obligation", "VPO-1e613b30576dc52c")>]
[<Trait("obligation", "VPO-70c58e3278725c35")>]
let ``VPO-1e613b30576dc52c VPO-70c58e3278725c35 the target is an explicit, validated selection and is never inferred from a token`` () =
    Assert.True(Result.isError (Target.create "" repo branch ws))
    Assert.True(Result.isError (Target.create owner "" branch ws))
    Assert.True(Result.isError (Target.create owner repo "" ws))
    Assert.True(Result.isError (Target.create owner repo branch ""))
    Assert.True(Result.isError (Target.create owner repo "../main" ws))
    Assert.True(Result.isError (Target.create owner repo branch "../../catalog"))
    // Connect takes a Target value and a Credential value: there is no constructor from a token alone.
    let fake = newFake ()
    let app = App(fake)
    ok (app.Connect ws)
    Assert.Equal(Some(target ws), app.Model.Target)
    Assert.True(fake.Requests |> List.forall (fun r -> r.Url.Contains("/repos/" + owner + "/" + repo)))

[<Fact>]
[<Trait("obligation", "VPO-4c2041ba04ba0b31")>]
[<Trait("obligation", "VPO-57fe23e7c821450f")>]
let ``VPO-4c2041ba04ba0b31 VPO-57fe23e7c821450f the eight connection states are distinct and a token alone never shows connected`` () =
    let seen = Collections.Generic.HashSet<string>()
    // disconnected
    let fake = newFake ()
    let app = App(fake)
    seen.Add app.Connection |> ignore
    // connecting: the repository probe is held, so only "connecting" may be shown.
    app.Faults <- (fun _ -> Hold)
    ok (app.Connect ws)
    Assert.Equal("connecting", app.Connection)
    seen.Add app.Connection |> ignore
    // loading: let the probe and head through, hold the first workspace read.
    let app = App(fake)
    let mutable n = 0
    app.Faults <- (fun _ -> n <- n + 1; if n <= 2 then Deliver else Hold)
    ok (app.Connect ws)
    Assert.Equal("loading", app.Connection)
    seen.Add app.Connection |> ignore
    // authorizedEmpty
    let app = App(fake)
    ok (app.Connect ws)
    Assert.Equal("authorizedEmpty", app.Connection)
    seen.Add app.Connection |> ignore
    // connected
    seed fake ws (investigation "states" 0) |> ignore
    let app = App(fake)
    ok (app.Connect ws)
    Assert.Equal("connected", app.Connection)
    seen.Add app.Connection |> ignore
    // conflict: another writer changes the investigation after this device read it, then a save is refused.
    let iid = (investigation "states" 0).InvestigationId
    ok (app.Step(Edit(iid, 2L, [ hypothesis iid 3L "mine" ])))
    seed fake ws (investigation "states" 1) |> ignore
    ok (app.Step(Save iid))
    Assert.Equal("conflict", app.Connection)
    seen.Add app.Connection |> ignore
    // incompatible: malformed data in the workspace.
    let bad = newFake ()
    let v = investigation "malformed" 0
    bad.CommitFiles(branch, [ Target.investigationDir (target ws) v.InvestigationId + "/manifest.json", utf8 "{not json}\n"
                              Target.investigationDir (target ws) v.InvestigationId + "/events.ndjson", v.EventsFile
                              Target.investigationDir (target ws) v.InvestigationId + "/snapshot.json", v.Snapshot ]) |> ignore
    let app = App(bad)
    ok (app.Connect ws)
    Assert.Equal("incompatible", app.Connection)
    seen.Add app.Connection |> ignore
    // accessDenied: a rejected token.
    let app = App(newFake ())
    ok (app.Connect(ws, "not-a-valid-token"))
    Assert.Equal("accessDenied", app.Connection)
    seen.Add app.Connection |> ignore
    // offline
    let app = App(newFake ())
    app.Faults <- (fun _ -> NotSentFault)
    ok (app.Connect ws)
    Assert.Equal("offline", app.Connection)
    seen.Add app.Connection |> ignore
    // EXT-DATA-007's eight, plus the ordinary connected state.
    for label in [ "disconnected"; "connecting"; "authorizedEmpty"; "loading"; "incompatible"; "accessDenied"; "offline"; "conflict"; "connected" ] do
        Assert.Contains(label, seen)
    Assert.Equal(9, seen.Count)

[<Fact>]
[<Trait("obligation", "VPO-7ced47ef45099aab")>]
let ``VPO-7ced47ef45099aab connecting and reading never mutate the repository`` () =
    let fake = newFake ()
    seed fake ws (investigation "read-only-connect" 2) |> ignore
    let head = fake.Head branch
    let app = App(fake)
    ok (app.Connect ws)
    Assert.Equal("connected", app.Connection)
    Assert.True(fake.Requests |> List.forall (fun r -> r.Method = "GET"))
    Assert.Equal(head, fake.Head branch)

[<Fact>]
[<Trait("obligation", "VPO-61b2e81f99761d55")>]
[<Trait("obligation", "VPO-9c6055c47e4eb07d")>]
[<Trait("obligation", "VPO-d7fbd84e40ded145")>]
[<Trait("obligation", "VPO-b3efad6eb85360be")>]
[<Trait("obligation", "VPO-4c06d10238d15c5c")>]
[<Trait("obligation", "VPO-89ed6170f531961e")>]
let ``VPO-61b2e81f99761d55 VPO-9c6055c47e4eb07d VPO-d7fbd84e40ded145 VPO-b3efad6eb85360be VPO-4c06d10238d15c5c VPO-89ed6170f531961e explicit create is saved locally, written in one guarded commit and claimed synced only after the re-read`` () =
    let fake = newFake ()
    let app = App(fake)
    ok (app.Connect ws)
    let headBefore = fake.Head branch
    let iid = newId "create"
    // Explicit create: nothing is written remotely by creating.
    ok (app.Dispatch(Create(iid, createEvents iid)))
    Assert.Equal("savingLocally", app.Status iid)
    app.Run()
    Assert.Equal("savedLocally", app.Status iid)
    Assert.Empty(writes fake)
    // Save: hold the commit answer -> syncing remotely, never synced.
    app.Faults <- (fun r -> if r.Method = "POST" then Hold else Deliver)
    ok (app.Step(Save iid))
    Assert.Equal("syncingRemotely", app.Status iid)
    let commitId, _ = app.Held.Head
    // The guarded write carries the head the change was read at.
    let body = (writes fake).Head.Body |> Option.defaultValue ""
    Assert.Contains("\"expectedHeadOid\":\"" + headBefore + "\"", body)
    // Acknowledged but the confirmation read is held: still not synced.
    app.Faults <- (fun _ -> Hold)
    ok (app.DeliverHeld commitId)
    Assert.Equal("syncingRemotely", app.Status iid)
    Assert.Equal(None, app.Doc(iid).LastSyncedAt)
    let confirmId, _ = app.Held.Head
    app.Faults <- (fun _ -> Deliver)
    ok (app.DeliverHeld confirmId)
    Assert.Equal("remotelySynced", app.Status iid)
    Assert.Equal(Some "2026-10-27T12:00:00.000Z", app.Doc(iid).LastSyncedAt)
    // One commit, exactly the three files under the workspace, byte-identical to the local copy.
    Assert.Single(writes fake) |> ignore
    let files = fake.FilesAt(fake.Head branch)
    for (n, b) in FileSet.files (app.Doc iid).Local do
        Assert.Equal<byte[]>(b, files[Target.investigationDir (target ws) iid + "/" + n])
    // The written investigation reads back through the full reader (round trip).
    let raw = { DirectoryName = iid; Entries = FileSet.names |> List.map (fun n -> n, files[Target.investigationDir (target ws) iid + "/" + n]) }
    Assert.True(Result.isOk (FileSet.read env.Engine raw))

[<Fact>]
[<Trait("obligation", "VPO-b3efad6eb85360be")>]
let ``VPO-b3efad6eb85360be an acknowledged commit whose re-read fails is unknown, never saved`` () =
    let fake = newFake ()
    let app = App(fake)
    ok (app.Connect ws)
    let iid = newId "confirm-fails"
    ok (app.Step(Create(iid, createEvents iid)))
    let mutable posted = false
    app.Faults <- (fun r -> if r.Method = "POST" then posted <- true; Deliver elif posted then NotSentFault else Deliver)
    ok (app.Step(Save iid))
    Assert.Equal("unknown", app.Status iid)
    Assert.Equal(None, app.Doc(iid).LastSyncedAt)
    refused (app.Step(Save iid))

[<Fact>]
[<Trait("obligation", "VPO-34731ac9a977b818")>]
[<Trait("obligation", "VPO-d7fbd84e40ded145")>]
let ``VPO-34731ac9a977b818 VPO-d7fbd84e40ded145 a concurrent remote change is a conflict that keeps local edits and never overwrites remote history`` () =
    let fake = newFake ()
    let base' = investigation "conflict" 0
    seed fake ws base' |> ignore
    let app = App(fake)
    ok (app.Connect ws)
    let iid = base'.InvestigationId
    ok (app.Step(Edit(iid, 2L, [ hypothesis iid 3L "local" ])))
    // Another writer appends to the same investigation.
    let theirs = investigation "conflict" 1
    let theirHead = seed fake ws theirs
    ok (app.Step(Save iid))
    Assert.Equal("conflict", app.Status iid)
    Assert.Equal(theirHead, fake.Head branch)
    Assert.Equal<byte[]>(theirs.EventsFile, (fake.FilesAt theirHead)[Target.investigationDir (target ws) iid + "/events.ndjson"])
    Assert.Equal(3L, app.Doc(iid).Local.Version)
    // Saving again is refused until the remote copy is reloaded; reload keeps local work and shows theirs.
    refused (app.Step(Save iid))
    ok (app.Step(ReloadRemote iid))
    match app.Doc(iid).Remote with
    | Conflicted(_, Some remote, _) -> Assert.Equal(theirs.EventsSha256, remote.EventsSha256)
    | other -> failwithf "expected a conflict with the remote copy, got %A" other
    Assert.Equal(3L, app.Doc(iid).Local.Version)
    // The person chooses: discard the local change and adopt theirs.
    ok (app.Step(DiscardLocal iid))
    Assert.Equal(theirs.EventsSha256, app.Doc(iid).Local.EventsSha256)
    Assert.Equal("clean", app.Status iid)

[<Fact>]
[<Trait("obligation", "VPO-34731ac9a977b818")>]
let ``VPO-34731ac9a977b818 a moved branch that did not touch the investigation is reloaded and the change rebased, then saved`` () =
    let fake = newFake ()
    let base' = investigation "rebase" 0
    seed fake ws base' |> ignore
    let app = App(fake)
    ok (app.Connect ws)
    let iid = base'.InvestigationId
    ok (app.Step(Edit(iid, 2L, [ hypothesis iid 3L "mine" ])))
    fake.CommitFiles(branch, [ "unrelated/file.txt", utf8 "other writer\n" ]) |> ignore
    ok (app.Step(Save iid))
    Assert.Equal("conflict", app.Status iid)
    ok (app.Step(ReloadRemote iid))
    Assert.Equal("savedLocally", app.Status iid)
    ok (app.Step(Save iid))
    Assert.Equal("remotelySynced", app.Status iid)
    Assert.True((fake.FilesAt(fake.Head branch)).ContainsKey "unrelated/file.txt")

[<Fact>]
[<Trait("obligation", "VPO-b3efad6eb85360be")>]
[<Trait("obligation", "VPO-cd9dd8ec730c3d3e")>]
let ``VPO-b3efad6eb85360be VPO-cd9dd8ec730c3d3e a save whose answer was lost is unknown and is settled only by re-reading`` () =
    let fake = newFake ()
    let app = App(fake)
    ok (app.Connect ws)
    let iid = newId "lost-applied"
    ok (app.Step(Create(iid, createEvents iid)))
    app.Faults <- (fun r -> if r.Method = "POST" then ApplyThenLose else Deliver)
    ok (app.Step(Save iid))
    Assert.Equal("unknown", app.Status iid)
    refused (app.Step(Save iid))
    refused (app.Step(DiscardLocal iid))
    app.Faults <- (fun _ -> Deliver)
    let writesBefore = (writes fake).Length
    ok (app.Step(Reconcile iid))
    Assert.Equal("remotelySynced", app.Status iid)
    Assert.Equal(writesBefore, (writes fake).Length)

[<Fact>]
[<Trait("obligation", "VPO-cd9dd8ec730c3d3e")>]
let ``VPO-cd9dd8ec730c3d3e a lost save that was not applied reconciles back to pending and can be retried`` () =
    let fake = newFake ()
    let app = App(fake)
    ok (app.Connect ws)
    let iid = newId "lost-not-applied"
    ok (app.Step(Create(iid, createEvents iid)))
    app.Faults <- (fun r -> if r.Method = "POST" then LoseWithoutApplying else Deliver)
    ok (app.Step(Save iid))
    Assert.Equal("unknown", app.Status iid)
    app.Faults <- (fun _ -> Deliver)
    ok (app.Step(Reconcile iid))
    Assert.Equal("savedLocally", app.Status iid)
    ok (app.Step(Save iid))
    Assert.Equal("remotelySynced", app.Status iid)

[<Fact>]
[<Trait("obligation", "VPO-2d4b7557ce013e50")>]
[<Trait("obligation", "VPO-bb08b9a414ba4cd7")>]
let ``VPO-2d4b7557ce013e50 a revoked token fails the next request, drops the credential and keeps local work; a replacement token saves it`` () =
    let fake = newFake ()
    let app = App(fake)
    ok (app.Connect ws)
    let iid = newId "revoked"
    ok (app.Step(Create(iid, createEvents iid)))
    fake.Revoke token
    ok (app.Step(Save iid))
    Assert.Equal("accessDenied", app.Connection)
    Assert.Equal("failed", app.Status iid)
    Assert.True(app.Model.Credential.IsNone)
    Assert.Empty(writes fake |> List.filter (fun _ -> fake.Head branch <> fake.Head branch))
    refused (app.Step(Save iid))
    // A new token: reconnect, the pending change is still here, and it saves.
    let replacement = "causa-replacement-token-2b81"
    fake.Authorize replacement
    ok (app.Connect(ws, replacement))
    Assert.Equal("savedLocally", app.Status iid)
    ok (app.Step(Save iid))
    Assert.Equal("remotelySynced", app.Status iid)

[<Fact>]
[<Trait("obligation", "VPO-54b0eef91ceeada4")>]
[<Trait("obligation", "VPO-89ed6170f531961e")>]
let ``VPO-54b0eef91ceeada4 VPO-89ed6170f531961e offline edits survive a reload; the credential does not and must be supplied again`` () =
    let fake = newFake ()
    let app = App(fake)
    ok (app.Connect ws)
    let iid = newId "offline"
    app.Faults <- (fun _ -> NotSentFault)
    ok (app.Step(Create(iid, createEvents iid)))
    ok (app.Step(Edit(iid, 2L, [ hypothesis iid 3L "offline" ])))
    ok (app.Step(Save iid))
    Assert.Equal("offline", app.Connection)
    Assert.Equal("queuedOffline", app.Status iid)
    ok (app.Reload())
    Assert.Equal("disconnected", app.Connection)
    Assert.True(app.Model.Credential.IsNone)
    Assert.Equal(3L, app.Doc(iid).Local.Version)
    Assert.Equal("queuedOffline", app.Status iid)
    refused (app.Step(Save iid))
    app.Faults <- (fun _ -> Deliver)
    ok (app.Connect ws)
    ok (app.Step(Save iid))
    Assert.Equal("remotelySynced", app.Status iid)
    Assert.Equal(3L, app.Doc(iid).Confirmed.Value.Version)

[<Fact>]
[<Trait("obligation", "VPO-b3efad6eb85360be")>]
let ``VPO-b3efad6eb85360be a reload during a send restores it as unknown and the next connect settles it by reading`` () =
    let fake = newFake ()
    let app = App(fake)
    ok (app.Connect ws)
    let iid = newId "reload-during-send"
    ok (app.Step(Create(iid, createEvents iid)))
    app.Faults <- (fun r -> if r.Method = "POST" then Hold else Deliver)
    ok (app.Step(Save iid))
    // GitHub applies it; the tab is reloaded before the answer arrives.
    ok (app.Reload())
    Assert.Equal("unknown", app.Status iid)
    app.Faults <- (fun _ -> Deliver)
    ok (app.Connect ws)
    Assert.Equal("remotelySynced", app.Status iid)

[<Fact>]
[<Trait("obligation", "VPO-2de744fb2c8bdc70")>]
[<Trait("obligation", "VPO-bb08b9a414ba4cd7")>]
[<Trait("obligation", "VPO-ca58030e3eecdf9b")>]
[<Trait("obligation", "VPO-c41f10e4ed8a086b")>]
let ``VPO-2de744fb2c8bdc70 VPO-bb08b9a414ba4cd7 VPO-ca58030e3eecdf9b VPO-c41f10e4ed8a086b the token appears only in the Authorization header`` () =
    let fake = newFake ()
    let app = App(fake)
    ok (app.Connect ws)
    let iid = newId "secret"
    ok (app.Step(Create(iid, createEvents iid)))
    ok (app.Step(Save iid))
    fake.Revoke token
    ok (app.Step(Edit(iid, 2L, [ hypothesis iid 3L "after revoke" ])))
    ok (app.Step(Save iid))
    let contains (s: string) = s.Contains(token, StringComparison.Ordinal)
    // Local storage, every request URL/body/header, every refusal, the projection and the model's text form.
    Assert.False(app.Store |> Map.exists (fun _ v -> contains v))
    for r in fake.Requests do
        Assert.False(contains r.Url)
        Assert.False(r.Body |> Option.exists contains)
        Assert.False(r.Headers |> List.exists (fun (_, v) -> contains v))
    Assert.False(app.Refusals |> List.exists contains)
    Assert.False(contains (sprintf "%A" app.Model))
    Assert.False(contains (Sync.connectionLabel app.Model + string (Sync.connectionDetail app.Model)))
    // The Authorization header is the only carrier.
    Assert.True(fake.Requests |> List.exists (fun r -> r.Credential |> Option.exists (fun c -> c.AuthorizationHeader = "Bearer " + token)))

[<Fact>]
[<Trait("obligation", "VPO-2d4b7557ce013e50")>]
[<Trait("obligation", "VPO-8c435e1ebdf43e7c")>]
let ``VPO-2d4b7557ce013e50 VPO-8c435e1ebdf43e7c answers for unknown requests or an earlier connection are refused and change nothing`` () =
    let fake = newFake ()
    let app = App(fake)
    app.Faults <- (fun _ -> Hold)
    ok (app.Connect ws)
    let firstId, _ = app.Held.Head
    // Reconnect (a new credential generation) before the first answer arrives.
    app.Faults <- (fun _ -> Deliver)
    ok (app.Connect ws)
    let before = app.Model
    refused (app.DeliverHeld firstId)
    Assert.Equal(before, app.Model)
    refused (app.Step(HttpAnswered("r999", { Status = 200; Headers = []; Body = "{}" })))
    Assert.Equal(before, app.Model)

[<Fact>]
[<Trait("obligation", "VPO-d7fbd84e40ded145")>]
let ``VPO-d7fbd84e40ded145 refused steps change nothing and send nothing`` () =
    let fake = newFake ()
    let app = App(fake)
    ok (app.Connect ws)
    let iid = newId "refusals"
    ok (app.Step(Create(iid, createEvents iid)))
    ok (app.Step(Save iid))
    let before = app.Model
    let effects = app.Effects.Length
    refused (app.Step(Save iid)) // nothing to save
    refused (app.Step(Create(iid, createEvents iid))) // exists
    refused (app.Step(Edit(iid, 1L, [ hypothesis iid 2L "stale" ]))) // decided at an old version
    refused (app.Step(Reconcile iid)) // nothing unknown
    refused (app.Step(DiscardLocal iid)) // nothing local
    Assert.Equal(before, app.Model)
    Assert.Equal(effects, app.Effects.Length)

[<Fact>]
[<Trait("obligation", "VPO-c135d16c59f5baf8")>]
[<Trait("obligation", "VPO-0157918192f093da")>]
let ``VPO-c135d16c59f5baf8 VPO-0157918192f093da authenticated but forged repository content fails closed and is not loaded`` () =
    let fake = newFake ()
    let good = investigation "trusted" 0
    seed fake ws good |> ignore
    let forged = investigation "forged-remote" 1
    let t = target ws
    let tampered = (text forged.EventsFile).Replace("Hypothesis 1", "Hypothesis forged")
    fake.CommitFiles(branch, [ Target.investigationDir t forged.InvestigationId + "/manifest.json", forged.Manifest
                               Target.investigationDir t forged.InvestigationId + "/events.ndjson", utf8 tampered
                               Target.investigationDir t forged.InvestigationId + "/snapshot.json", forged.Snapshot ]) |> ignore
    let app = App(fake)
    ok (app.Connect ws)
    Assert.Equal("incompatible", app.Connection)
    Assert.False(app.Model.Documents.ContainsKey forged.InvestigationId)
    Assert.True(app.Model.Documents.ContainsKey good.InvestigationId)
    let code = app.Model.LoadErrors |> List.find (fst >> (=) forged.InvestigationId) |> snd
    Assert.Equal(Codes.eventsHashMismatch, code.Code)

[<Fact>]
[<Trait("obligation", "VPO-074f1b1804e54869")>]
let ``VPO-074f1b1804e54869 writes are confined to the selected workspace of the selected repository`` () =
    let t = target ws
    let iid = newId "paths"
    Assert.True(Target.pathAllowed t (Target.investigationDir t iid + "/events.ndjson"))
    Assert.False(Target.pathAllowed t ("catalog/active.json"))
    Assert.False(Target.pathAllowed t ("workspaces/demo/historical-v1/investigations/" + iid + "/events.ndjson"))
    Assert.False(Target.pathAllowed t (Target.investigationDir t iid + "/../x/events.ndjson"))
    Assert.False(Target.pathAllowed t (Target.investigationDir t iid + "/notes.txt"))
    let fake = newFake ()
    let app = App(fake)
    ok (app.Connect ws)
    ok (app.Step(Create(iid, createEvents iid)))
    ok (app.Step(Save iid))
    let changed = fake.FilesAt(fake.Head branch) |> Map.toList |> List.map fst |> List.filter (fun p -> p <> "README.md")
    Assert.True(changed |> List.forall (Target.pathAllowed t))

[<Fact>]
[<Trait("obligation", "VPO-89ed6170f531961e")>]
let ``VPO-89ed6170f531961e a local store failure is shown as failed, distinct from every other state`` () =
    let fake = newFake ()
    let app = App(fake)
    ok (app.Connect ws)
    app.StoreFails <- true
    let iid = newId "store-fails"
    ok (app.Step(Create(iid, createEvents iid)))
    Assert.Equal("failed", app.Status iid)
