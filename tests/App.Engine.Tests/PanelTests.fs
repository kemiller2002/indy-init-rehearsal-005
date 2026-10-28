/// S17 multi-agent panel and S18 research tournament, engine side. Fakes only: the key is a fake literal and every
/// provider answer is a canned response.
module App.Engine.Tests.PanelTests

open System
open Xunit
open IndyInitRootCauseInvestigator.Engine

let private key = ProviderKey.Create "fake-key-for-tests-0000"
let private profile = AiLoop.anthropic "configured-model-id"
let private ns = Guid "8e0b7c52-55a1-4a43-9a0e-3c3f2f0d6a11"
let private gid (name: string) = Hashing.uuidV5 ns name
let private invId: InvestigationId = Id(gid "investigation")
let private t0 = DateTimeOffset(2026, 10, 27, 12, 0, 0, TimeSpan.Zero)
let private human = { ActorId = "investigator-01"; Kind = Human }

let private investigation () =
    [ StartInvestigation("Checkout failures", Id(gid "obs"), "Payments fail since 09:40.")
      ActivateInvestigation
      RecordHypothesis(Id(gid "h"), "Timeout too short", None) ]
    |> List.indexed
    |> List.fold
        (fun s (i, c) ->
            match Kernel.submit s { CommandId = gid $"c/{i}"; Investigation = invId; ExpectedVersion = Some(Kernel.version s); Actor = human; RequestedAt = t0.AddMinutes(float i); Command = c } with
            | Ok a -> a.Session
            | Error r -> failwithf "%A" r)
        Kernel.empty
    |> fun s -> s.State |> Option.get

let private answer (proposals: (string * string) list) =
    let items = proposals |> List.map (fun (k, t) -> JObject [ "kind", JString k; "text", JString t; "rationale", JString "because" ])
    let text = Json.canonical (JObject [ "proposals", JArray items ])
    Json.canonical (
        JObject
            [ "content", JArray [ JObject [ "type", JString "text"; "text", JString text ] ]
              "stop_reason", JString "end_turn"
              "usage", JObject [ "input_tokens", JInt 900L; "output_tokens", JInt 60L ] ]
    )

let private refusal = Json.canonical (JObject [ "content", JArray []; "stop_reason", JString "refusal" ])

let private startPanel () =
    match Panel.startPanel key profile (investigation ()) "Propose next steps." Panel.roles with
    | Ok x -> x
    | Error e -> failwithf "%A" e

let private feed (state: BatchState) (answers: (int64 * string) list) =
    answers |> List.fold (fun s (r, body) -> match Panel.receive s (Responded(r, 200, [], body)) (Some 1500L) with Ok s -> s | Error e -> failwithf "%A" e) state

[<Fact>]
let ``S17 the panel sends one request per role, all against the same version, each with its own versioned role prompt`` () =
    let inv = investigation ()
    let state, effects = startPanel ()
    Assert.Equal(Panel.roles.Length, effects.Length)
    Assert.Equal(inv.Version, state.SourceVersion)
    Assert.All(state.Pending |> Map.toList, fun (_, (p, prov)) -> Assert.Equal(inv.Version, p.ContextVersion); Assert.Equal(inv.Version, prov.SourceVersion))
    for (r, e) in List.zip Panel.roles effects do
        Assert.Contains(r.Name, e.Body)
        for other in Panel.roles do
            if other.Id <> r.Id then Assert.DoesNotContain(other.Name, e.Body)
    // The key appears only in the authentication header.
    Assert.All(effects, fun e -> Assert.DoesNotContain("fake-key-for-tests-0000", e.Body))
    Assert.Equal(Error(Choice1Of2 KeyMissing), Panel.startPanel None profile inv "x" Panel.roles |> Result.map ignore)

[<Fact>]
let ``S17 roles are blind: every prompt is built before any answer, so no role's output can enter another's request`` () =
    let state, effects = startPanel ()
    let state = feed state [ 1L, answer [ "hypothesis", "A certificate expired at 09:40" ] ]
    // The remaining requests were fixed at start; the answer text appears in none of them.
    Assert.All(effects, fun e -> Assert.DoesNotContain("certificate expired", e.Body))
    Assert.Equal(2, state.Pending.Count)
    // An answer for an unknown or already answered request is refused.
    Assert.Equal(Error(StaleAnswer 1L), Panel.receive state (Responded(1L, 200, [], answer [])) None |> Result.map ignore)

[<Fact>]
let ``S17 disagreement is structural (omission or a different kind), never a vote, and provenance stays on every answer`` () =
    let state, _ = startPanel ()
    let state =
        feed
            state
            [ 1L, answer [ "hypothesis", "A certificate expired at 09:40"; "unknown", "Was the gateway restarted?" ]
              2L, answer [ "hypothesis", "A certificate expired at 09:40."; "hypothesis", "The gateway was restarted" ]
              3L, answer [ "unknown", "A certificate expired at 09:40"; "unknown", "Was the gateway restarted?" ] ]
    Assert.True(Panel.complete state)
    let ds = Panel.disagreements state
    // Same text, different kinds: listed with who proposed which kind.
    let cert = ds |> List.find (fun d -> d.Kind = ProposedHypothesis && d.Text.StartsWith "A certificate")
    Assert.Equal<string list>([ "evidence-auditor"; "alternative-cause-seeker" ], cert.ProposedBy)
    Assert.Equal<(string * ProposalKind) list>([ "timeline-checker", ProposedUnknown ], cert.OtherKindBy)
    // Omitted by one role: listed, with no count and no verdict.
    let restart = ds |> List.find (fun d -> d.Text = "Was the gateway restarted?")
    Assert.Equal<string list>([ "alternative-cause-seeker" ], restart.OmittedBy)
    Assert.Empty(Panel.agreements state)
    Assert.All(state.Answers, fun a -> Assert.True(a.Provenance.Role.IsSome); Assert.Equal("configured-model-id", a.Provenance.Model); Assert.StartsWith("causa.panel-role/", a.Provenance.PromptVersion))
    // Drafts are still only drafts: recording them goes through Proposals as agent proposals (D-004).
    Assert.All(Panel.drafts state, fun (_, ds) -> Assert.NotEmpty(ds))

[<Fact>]
let ``S17 a role that fails is not counted as omitting anything`` () =
    let state, _ = startPanel ()
    let state = feed state [ 1L, answer [ "hypothesis", "X" ]; 2L, answer [ "hypothesis", "X" ]; 3L, refusal ]
    Assert.Empty(Panel.disagreements state)
    Assert.Equal<(ProposalKind * string) list>([ ProposedHypothesis, "X" ], Panel.agreements state)

[<Fact>]
let ``S18 treatments run on one frozen version; metrics are measured per treatment with no winner, and limitations are explicit`` () =
    let inv = investigation ()
    let treatments =
        [ { Id = "t-a"; Model = "model-a"; PromptVersion = AiLoop.promptPolicyVersion }
          { Id = "t-b"; Model = "model-b"; PromptVersion = "causa.panel-role/evidence-auditor/1" }
          { Id = "t-c"; Model = "model-c"; PromptVersion = AiLoop.promptPolicyVersion } ]
    let state, effects = match Panel.startTournament key profile inv "Propose." treatments with Ok x -> x | Error e -> failwithf "%A" e
    Assert.Equal(3, effects.Length)
    Assert.Contains("model-b", effects[1].Body)
    let state = match Panel.receive state (Responded(1L, 200, [], answer [ "hypothesis", "H1"; "unknown", "U1" ])) (Some 1200L) with Ok s -> s | Error e -> failwithf "%A" e
    let state = match Panel.receive state (Responded(2L, 200, [], refusal)) (Some 800L) with Ok s -> s | Error e -> failwithf "%A" e
    let state = match Panel.receive state (Responded(3L, 200, [], "<html>")) (Some 400L) with Ok s -> s | Error e -> failwithf "%A" e
    let m = Panel.metrics state
    let a = m |> List.find (fun x -> x.Treatment = "t-a")
    Assert.Equal((Some true, 2, false, Some 1200L, inv.Version), (a.ValidOutput, a.Drafts, a.Refused, a.LatencyMs, a.SourceVersion))
    Assert.Equal(Some { InputTokens = 900L; OutputTokens = 60L }, a.Usage)
    Assert.True((m |> List.find (fun x -> x.Treatment = "t-b")).Refused)
    Assert.Equal(Some false, (m |> List.find (fun x -> x.Treatment = "t-c")).ValidOutput)
    Assert.Contains(Panel.limitations state, fun l -> l.Contains "No treatment is ranked")
    Assert.Contains(Panel.limitations state, fun l -> l.StartsWith "One investigation at one frozen version")

[<Fact>]
let ``S18 an unknown prompt version or a duplicate treatment is refused before anything is sent`` () =
    let inv = investigation ()
    let t id v = { Id = id; Model = "m"; PromptVersion = v }
    Assert.Equal(Error(Choice2Of2(UnknownPromptVersion "made-up/9")), Panel.startTournament key profile inv "x" [ t "a" "made-up/9" ] |> Result.map ignore)
    Assert.Equal(Error(Choice2Of2(DuplicateTreatment "a")), Panel.startTournament key profile inv "x" [ t "a" AiLoop.promptPolicyVersion; t "a" AiLoop.promptPolicyVersion ] |> Result.map ignore)
