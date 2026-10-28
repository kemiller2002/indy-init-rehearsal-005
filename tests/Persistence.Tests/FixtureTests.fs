/// Local fixture mode (PF-003) and the one data port (PF-001): the synthetic repository is answered by the same
/// recorded-behaviour GitHub and driven by the same Sync program as real GitHub.
module Persistence.Tests.FixtureTests

open Xunit
open Causa.Persistence


let private ok (r: Result<unit, string>) =
    match r with
    | Ok() -> ()
    | Error e -> failwithf "unexpected refusal: %s" e

let private fixtureTarget = { Owner = Fixture.owner; Repo = Fixture.repo; Branch = Fixture.branch; Workspace = Fixture.workspace }

[<Fact>]
[<Trait("obligation", "VPO-ad958e7a60d7fd9b")>]
[<Trait("obligation", "VPO-f00be0cb5a2bb0f8")>]
let ``VPO-ad958e7a60d7fd9b VPO-f00be0cb5a2bb0f8 the fixture is one compact, complete synthetic investigation decided by the engine, with stable ids`` () =
    let v =
        match Fixture.investigation () with
        | Ok v -> v
        | Error e -> failwith e
    let s = v.Loaded.State
    Assert.True(s.Observations.Count >= 2)
    Assert.True(s.Hypotheses.Count >= 3)
    let states = s.Hypotheses |> Map.toList |> List.map (fun (_, h) -> h.State) |> List.distinct
    Assert.True(states.Length >= 3, sprintf "hypothesis states: %A" states)
    let kinds = s.Relationships |> Map.toList |> List.map (fun (_, r) -> r.Kind) |> Set.ofList
    Assert.True(kinds.Contains IndyInitRootCauseInvestigator.Engine.Supports && kinds.Contains IndyInitRootCauseInvestigator.Engine.Contradicts)
    Assert.True(s.Evidence.Count >= 3)
    Assert.True(s.Evidence |> Map.forall (fun _ e -> e.Provenance.Actor.ActorId = "fixture-generator"))
    Assert.NotEmpty(s.Unknowns)
    Assert.NotEmpty(s.Assumptions)
    Assert.NotEmpty(s.Contradictions)
    Assert.NotEmpty(s.Predictions)
    Assert.NotEmpty(s.Actions)
    Assert.NotEmpty(s.NegativeKnowledge)
    Assert.NotEmpty(s.Causes)
    Assert.NotEmpty(s.CorrectiveActions)
    Assert.NotEmpty(s.Criteria)
    // Deterministic: generated twice, byte-identical (stable ids, timestamps and ordering).
    match Fixture.investigation () with
    | Ok again -> Assert.Equal<byte[]>(v.EventsFile, again.EventsFile)
    | Error e -> failwith e

/// Runs one script through the Sync program against a provider and returns what the program showed and asked.
let private script (provider: RecordedGitHub) (target: Target) (credential: Credential) (iid: string) =
    let env = { Now = (fun () -> "2026-10-27T12:00:00.000Z"); Nonce = "page" }
    let mutable m = Sync.initial
    let labels = ResizeArray<string>()
    let routes = ResizeArray<string>()
    let rec run (queue: Msg list) =
        match queue with
        | [] -> ()
        | msg :: rest ->
            match Sync.update env msg m with
            | Error _ when (match msg with LocalStored _ -> true | _ -> false) -> run rest
            | Error r -> failwith r.Reason
            | Ok(m2, effects) ->
                m <- m2
                labels.Add(Sync.connectionLabel m)
                let answers =
                    effects
                    |> List.map (fun e ->
                        match e with
                        | Send(id, r) ->
                            routes.Add(r.Method + " " + r.Url.Replace(target.Owner + "/" + target.Repo, "{repo}"))
                            HttpAnswered(id, provider.Handle r)
                        | StoreLocal(w, _, _) -> LocalStored(w, true))
                run (rest @ answers)
    run [ Connect(target, credential) ]
    let d = Map.find iid m.Documents
    let before = d.Local
    run [ Submit(Persistence.Tests.Synthetic.hypothesis iid before.Version "added in the script") ]
    run [ Save iid ]
    m, labels |> List.ofSeq, routes |> List.ofSeq

[<Fact>]
[<Trait("obligation", "VPO-b090ff3e3c16c13d")>]
[<Trait("obligation", "VPO-ff3dc45c64bfa0cb")>]
[<Trait("obligation", "VPO-da07c2ac97baa9f1")>]
[<Trait("obligation", "VPO-d4b0d50550578aeb")>]
[<Trait("obligation", "VPO-557743cb4997c484")>]
let ``VPO-b090ff3e3c16c13d VPO-ff3dc45c64bfa0cb VPO-da07c2ac97baa9f1 VPO-d4b0d50550578aeb fixture and GitHub providers share one port and one program with the same semantics`` () =
    let v =
        match Fixture.investigation () with
        | Ok v -> v
        | Error e -> failwith e
    // The fixture provider: no credential is checked and nothing leaves the process.
    let fixture =
        match Fixture.provider () with
        | Ok p -> p
        | Error e -> failwith e
    let fm, flabels, froutes = script fixture fixtureTarget (Persistence.Tests.Harness.credential "placeholder") v.InvestigationId
    // The GitHub-mode provider (recorded behaviour with authorization), holding the same investigation.
    let github = Persistence.Tests.FakeGitHub.Fake(Persistence.Tests.Harness.owner, Persistence.Tests.Harness.repo, Fixture.branch, true)
    github.Authorize Persistence.Tests.Harness.token
    let gt = { fixtureTarget with Owner = Persistence.Tests.Harness.owner; Repo = Persistence.Tests.Harness.repo }
    github.CommitFiles(Fixture.branch, Store.files v |> List.map (fun (n, b) -> Target.investigationDir gt v.InvestigationId + "/" + n, b)) |> ignore
    let gm, glabels, groutes = script github gt (Persistence.Tests.Harness.credential Persistence.Tests.Harness.token) v.InvestigationId
    // Same states shown, same requests asked (by route), same domain meaning loaded and saved.
    Assert.Equal<string list>(glabels, flabels)
    let route (r: string) = System.Text.RegularExpressions.Regex.Replace(r, "[0-9a-f]{40}|causa_fresh=[^&]+", "*")
    Assert.Equal<string list>(groutes |> List.map route, froutes |> List.map route)
    let fd, gd = Map.find v.InvestigationId fm.Documents, Map.find v.InvestigationId gm.Documents
    Assert.Equal(gd.Local.StateSha256, fd.Local.StateSha256)
    Assert.Equal(Sync.documentStatus gm gd, Sync.documentStatus fm fd)
    Assert.Equal("remotelySynced", Sync.documentStatus fm fd)

[<Fact>]
[<Trait("obligation", "VPO-34731ac9a977b818")>]
[<Trait("obligation", "VPO-cd9dd8ec730c3d3e")>]
let ``VPO-34731ac9a977b818 VPO-cd9dd8ec730c3d3e the fixture conflict scenario: a deterministic other writer, then Reload keeps the change and Discard adopts theirs`` () =
    let provider =
        match Fixture.providerFor Fixture.conflictWorkspace with
        | Ok p -> p
        | Error e -> failwith e
    let t = { fixtureTarget with Workspace = Fixture.conflictWorkspace }
    let env = { Now = (fun () -> "2026-10-27T12:00:00.000Z"); Nonce = "page" }
    let mutable m = Sync.initial
    let rec run (queue: Msg list) =
        match queue with
        | [] -> ()
        | msg :: rest ->
            match Sync.update env msg m with
            | Error _ when (match msg with LocalStored _ -> true | _ -> false) -> run rest
            | Error r -> failwith r.Reason
            | Ok(m2, effects) ->
                m <- m2
                run (rest @ (effects |> List.map (fun e -> match e with Send(id, r) -> HttpAnswered(id, provider.Handle r) | StoreLocal(w, _, _) -> LocalStored(w, true))))
    run [ Connect(t, Persistence.Tests.Harness.credential "placeholder") ]
    let iid = Fixture.investigationId
    let v = (Map.find iid m.Documents).Local
    run [ Submit(Persistence.Tests.Synthetic.hypothesis iid v.Version "mine") ]
    run [ Save iid ]
    Assert.Equal("conflict", Sync.documentStatus m (Map.find iid m.Documents))
    run [ ReloadRemote iid ]
    let d = Map.find iid m.Documents
    Assert.Equal("conflict", Sync.documentStatus m d)
    Assert.Equal(v.Version + 1L, d.Local.Version)
    let theirs = d.RemoteConflict |> Option.bind (fun c -> c.Remote) |> Option.get
    Assert.Contains("Another writer's hypothesis", System.Text.Encoding.UTF8.GetString theirs.EventsFile)
    run [ DiscardLocal iid ]
    let d = Map.find iid m.Documents
    Assert.Equal(theirs.EventsSha256, d.Local.EventsSha256)
    Assert.True(d.RemoteConflict.IsNone)
