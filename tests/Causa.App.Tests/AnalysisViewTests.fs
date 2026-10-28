/// The analysis screens bound to Percepta contracts (S03, S04, S11, S12, S13, S15, S23, S24): commands cross the real
/// engine port, legality is only ever the engine's (or the injected fixture's), and every screen key is present.
module Causa.App.Tests.AnalysisViewTests

open System
open Xunit
open Limen.Contract.Core
open Causa.App
open Causa.App.Adapter

let private entropy () = { Now = DateTime(2026, 10, 27, 12, 0, 0, DateTimeKind.Utc); Ids = List.init 4 (fun _ -> Guid.NewGuid()); Nonce = "n" }

let private opened (port: Port) id =
    match port.Projection.Opened.TryFind id with
    | Some(Loaded inv) -> inv
    | other -> failwithf "not loaded: %A" other

let private send (port: Port) id name fields =
    let version = (opened port id).Version
    let port, _ = port.Handle (entropy ()) (Command(Some id, name, Map.ofList fields, version))
    Assert.True((opened port id).Version = version + 1L, sprintf "%s was not recorded: %A" name port.Projection.Message)
    port

/// An active investigation with one hypothesis, from the real kernel over an in-memory workspace.
let private active () =
    let port = EnginePort.create (EnginePort.Persistence.memory Map.empty [])
    let port, _ = port.Handle (entropy ()) (Command(None, "StartInvestigation", Map.ofList [ "title", "Valve leak"; "problemStatement", "Coolant found under valve 3." ], 0L))
    let id = match port.Projection.Investigations with Loaded [ c ] -> c.Id | other -> failwithf "%A" other
    let port = send port id "ActivateInvestigation" []
    let port = send port id "RecordHypothesis" [ "text", "The valve seal is worn." ]
    port, id, (opened port id).Hypotheses.Head.Id

let private view (port: Port) route selected =
    View.project { Shell.initial with Route = route; Port = port; Selected = selected }
    |> AnalysisView.extend { Shell.initial with Route = route; Port = port; Selected = selected }

let private textOf (v: ViewState) k = match v |> Map.tryFind k with Some(ViewValue.Text t) -> t | other -> failwithf "%s: %A" k other
let private flagOf (v: ViewState) k = match v |> Map.tryFind k with Some(ViewValue.Flag b) -> b | other -> failwithf "%s: %A" k other

[<Fact>]
let ``S03 a prediction recorded through the form reaches the engine and is shown as what would falsify the hypothesis`` () =
    let port, id, h = active ()
    let before = view port (Routes.Investigation(id, Routes.Hypotheses)) ""
    Assert.False(flagOf before "an.pred.disabled")
    let port = send port id "RecordPrediction" [ "hypothesis", h; "statement", "The seal shows wear marks."; "strength", "necessary" ]
    let inv = opened port id
    Assert.Equal("The seal shows wear marks.", inv.Detail.Predictions.Head.Text)
    Assert.Equal<string list>([ h ], inv.Detail.Predictions.Head.Related)
    let after = view port (Routes.Investigation(id, Routes.Hypotheses)) ""
    Assert.True(flagOf after "an.hyp.battlefield")
    Assert.Contains("The seal shows wear marks.", sprintf "%A" (after |> Map.find "an.falsifiers"))

[<Fact>]
let ``S03 an investigation action is planned through the engine against a hypothesis`` () =
    let port, id, h = active ()
    let port = send port id "RecordInvestigationAction" [ "purpose", "Inspect the seal."; "kind", "inspection"; "hypothesis", h ]
    let a = (opened port id).Detail.Actions.Head
    Assert.Equal("Inspect the seal.", a.Text)
    Assert.StartsWith("Inspection", a.State)

[<Fact>]
let ``S03 an illegal prediction is disabled with its reason and a link to the blocking question`` () =
    let s = match TestState.parse """{ "name": "b", "predicates": ["material-blocker-exists"], "capabilities": { "RecordPrediction": "illegal", "RecordInvestigationAction": "legal" } }""" with Ok s -> s | Error e -> failwith e
    let v = view (TestState.port s) (Routes.Investigation(TestState.fixtureId, Routes.Hypotheses)) ""
    Assert.True(flagOf v "an.pred.disabled")
    Assert.Equal("An open material question must be answered first.", textOf v "an.pred.reason")
    Assert.True(flagOf v "an.pred.hasBlocker")
    Assert.EndsWith("?item=blocker-1", textOf v "an.pred.blockerHref")
    Assert.False(flagOf v "an.act.disabled")
    Assert.Equal("", textOf v "an.act.reason")

[<Fact>]
let ``S04 a selected hypothesis shows its detail screen with the engine's falsify and reopen legality`` () =
    let port, id, h = active ()
    let v = view port (Routes.Investigation(id, Routes.Hypotheses)) h
    Assert.True(flagOf v "an.hyp.detail")
    Assert.False(flagOf v "an.hyp.battlefield")
    Assert.Equal("The valve seal is worn.", textOf v "an.h.statement")
    let caps = (opened port id).Capabilities
    let legal cmd = caps |> List.exists (fun c -> c.Id = $"TransitionHypothesis:{h}:{cmd}" && c.Legal)
    Assert.Equal(not (legal "falsify"), flagOf v "an.falsify.disabled")
    // A proposed hypothesis is not falsified, so reopening it is not available, and the reason says why.
    Assert.True(flagOf v "an.reopen.disabled")
    Assert.NotEqual<string>("", textOf v "an.reopen.reason")

[<Fact>]
let ``S04 falsifying from the detail screen is the selected hypothesis's own transition and waits for confirmation`` () =
    let port, id, h = active ()
    let ev name value: SemanticEvent = { Name = name; Key = None; Value = Some value; Checked = None; Values = None; Submitter = None }
    let model = { Shell.initial with Route = Routes.Investigation(id, Routes.Hypotheses); Port = port; Selected = h }
    let model, _ = Shell.update (entropy ()) (BrowserToEngineMessage.Event(ev "field-FalsifyHypothesis.reason" "Wear marks absent.")) model
    let model, fx = Shell.update (entropy ()) (BrowserToEngineMessage.Event(ev "submit-FalsifyHypothesis" "")) model
    Assert.Empty(fx)
    match model.PendingConfirm with
    | Some(Command(Some i, name, fields, _), _) ->
        Assert.Equal(id, i)
        Assert.Equal($"TransitionHypothesis:{h}:falsify", name)
        Assert.Equal(Some "Wear marks absent.", fields.TryFind "reason")
    | other -> failwithf "%A" other

[<Fact>]
let ``S11 S12 S13 cause review and corrective action show the engine's records and gates`` () =
    let port, id, h = active ()
    let port = send port id "RecordCause" [ "outcome", "Coolant leaked."; "hypothesis", h; "role", "rootCause" ]
    let inv = opened port id
    let cause = inv.Causes.Head
    let v = view port (Routes.Investigation(id, Routes.CausalAnalysis)) ""
    Assert.Equal("Coolant leaked.", textOf v "an.cause.outcome")
    let confirm = inv.Capabilities |> List.find (fun c -> c.Id = "ConfirmCause:" + cause.Id)
    Assert.Contains((if confirm.Legal then "\"legal\", Flag true" else "\"illegal\", Flag true"), sprintf "%A" (v |> Map.find "an.confirm"))
    let gate = inv.Detail.CauseGates |> List.find (fun (c, _) -> c = cause.Id) |> snd
    Assert.Equal(confirm.Legal, gate.IsEmpty)
    let port = send port id "RecordCorrectiveAction" [ "description", "Replace the seal."; "kind", "correction"; "cause", cause.Id ]
    let ca = (opened port id).Detail.Corrections.Head
    Assert.Equal<string list>([ cause.Id ], ca.Targets)
    let v = view port (Routes.Investigation(id, Routes.CorrectiveActions)) ""
    Assert.True(flagOf v "an.corr.action")
    Assert.Equal("Root-cause correction", textOf v "an.ca.kind")
    let mark = (opened port id).Capabilities |> List.find (fun c -> c.Id = "MarkCorrectiveActionImplemented:" + ca.Id)
    Assert.Contains((if mark.Legal then "\"legal\", Flag true" else "\"illegal\", Flag true"), sprintf "%A" (v |> Map.find "an.markImplemented"))
    // No criterion is recorded: the verification screen is not shown and nothing is called verified.
    Assert.False(flagOf v "an.corr.verification")
    Assert.True(flagOf v "an.ca.criteria.empty")

[<Fact>]
let ``S15 a past version is compared with the latest: open questions and action availability that changed`` () =
    let port, id, _ = active ()
    let port = send port id "IdentifyUnknown" [ "text", "Was the valve replaced last month?" ]
    let model = { Shell.initial with Route = Routes.Investigation(id, Routes.Timeline); Port = port; HistoryVersion = Some 2L }
    let v = View.project model |> AnalysisView.extend model
    Assert.Equal("semantic-diff", textOf v "an.timeline.screen")
    Assert.Contains("Opened since: Was the valve replaced last month?", sprintf "%A" (v |> Map.find "an.diff.blockers"))
    let latest = { model with HistoryVersion = None }
    Assert.Equal("investigation-timeline", textOf (View.project latest |> AnalysisView.extend latest) "an.timeline.screen")

[<Fact>]
let ``S23 a held persistence conflict shows the recovery screen; S24 an absent policy pin is explained, never offered`` () =
    let s = match TestState.parse """{ "name": "c", "predicates": [], "capabilities": {}, "sync": "conflict" }""" with Ok s -> s | Error e -> failwith e
    let port = TestState.port s
    Assert.Equal(Conflict, port.Projection.Sync)
    let route = Routes.Investigation(TestState.fixtureId, Routes.Overview)
    let model = { Shell.initial with Route = route; Port = port }
    let v = View.project model |> AnalysisView.extend model
    Assert.Equal("persistence-conflict-recovery", textOf v "an.overview.screen")
    let settings = { model with Route = Routes.Settings }
    let sv = View.project settings |> AnalysisView.extend settings
    let pin = sprintf "%A" (sv |> Map.find "an.pin")
    Assert.Contains("\"illegal\", Flag true", pin)
    Assert.Contains("no other version to pin", pin)

[<Fact>]
let ``every analysis key is present on every screen`` () =
    let port, id, _ = active ()
    let keysOn route =
        let m = { Shell.initial with Route = route; Port = port }
        View.project m |> AnalysisView.extend m |> Map.keys |> Seq.filter (fun k -> k.StartsWith "an.") |> Set.ofSeq
    let reference = keysOn Routes.Settings
    for route in [ Routes.Investigations; Routes.Investigation(id, Routes.Overview); Routes.Investigation(id, Routes.Hypotheses); Routes.Investigation(id, Routes.Timeline) ] do
        Assert.Equal<Set<string>>(reference, keysOn route)

[<Fact>]
let ``S08 a selected open question shows what waits on it, and answering it goes to the engine with its basis`` () =
    let port, id, _ = active ()
    let port = send port id "IdentifyUnknown" [ "text", "Was the valve replaced last month?" ]
    let u = (opened port id).Detail.Unknowns.Head
    let route = Routes.Investigation(id, Routes.Overview)
    let v = view port route u.Id
    Assert.Equal("epistemic-blocker-detail", textOf v "an.overview.screen")
    Assert.Equal("Open question", textOf v "an.blocker.kind")
    // Whatever the engine blocks on this question is listed; the contradiction control explains it does not apply.
    let blocked = (opened port id).Capabilities |> List.filter (fun c -> not c.Legal && c.BlockerId = Some u.Id)
    Assert.Equal(blocked.IsEmpty, flagOf v "an.blocker.blocks.empty")
    Assert.True(flagOf v "an.resolveContradiction.disabled")
    Assert.Equal("This is an open question, not a contradiction.", textOf v "an.resolveContradiction.reason")
    let legal = (opened port id).Capabilities |> List.exists (fun c -> c.Id = "ResolveUnknown:" + u.Id && c.Legal)
    Assert.Equal(not legal, flagOf v "an.resolveUnknown.disabled")
    let ev name value: SemanticEvent = { Name = name; Key = None; Value = Some value; Checked = None; Values = None; Submitter = None }
    let model = { Shell.initial with Route = route; Port = port; Selected = u.Id }
    let model, _ = Shell.update (entropy ()) (BrowserToEngineMessage.Event(ev "field-ResolveUnknown.basis" "The maintenance log says no.")) model
    let model, _ = Shell.update (entropy ()) (BrowserToEngineMessage.Event(ev "submit-ResolveUnknown" "")) model
    if legal then Assert.StartsWith("Answered", (opened model.Port id).Detail.Unknowns.Head.State)
    else Assert.Equal((opened port id).Version, (opened model.Port id).Version)
    // Without a selection the overview keeps its own identity.
    Assert.Equal("investigation-overview", textOf (view port route "") "an.overview.screen")

[<Fact>]
let ``S09 S10 the actions section designs an action and records its result through the engine; unsupported fields say not recorded`` () =
    let port, id, h = active ()
    let port = send port id "RecordPrediction" [ "hypothesis", h; "statement", "The seal shows wear marks."; "strength", "discriminating" ]
    let port = send port id "RecordInvestigationAction" [ "purpose", "Inspect the seal."; "kind", "inspection"; "hypothesis", h ]
    let designer = view port (Routes.Investigation(id, Routes.Actions)) ""
    Assert.True(flagOf designer "an.actions.designer")
    Assert.Equal("1 of 1 predictions were recorded before any finding (prospective); the rest were recorded after one and are marked retrospective.", textOf designer "an.actions.prereg")
    Assert.StartsWith("Cost, time and risk: not recorded.", textOf designer "an.actions.cost")
    let a = (opened port id).Detail.Actions.Head
    let route = Routes.Investigation(id, Routes.Actions)
    let capture = view port route a.Id
    Assert.True(flagOf capture "an.actions.capture")
    Assert.True(flagOf capture "an.capture.results.empty")
    let legal = (opened port id).Capabilities |> List.exists (fun c -> c.Id = "RecordFinding:" + a.Id && c.Legal)
    Assert.Equal(not legal, flagOf capture "an.finding.disabled")
    let ev name value: SemanticEvent = { Name = name; Key = None; Value = Some value; Checked = None; Values = None; Submitter = None }
    let model = { Shell.initial with Route = route; Port = port; Selected = a.Id }
    let model, _ = Shell.update (entropy ()) (BrowserToEngineMessage.Event(ev "field-RecordFinding.result" "Wear marks along one edge.")) model
    let model, _ = Shell.update (entropy ()) (BrowserToEngineMessage.Event(ev "submit-RecordFinding" "")) model
    if legal then
        let f = (opened model.Port id).Detail.Findings.Head
        Assert.Equal("Wear marks along one edge.", f.Text)
        Assert.Equal<string list>([ a.Id ], f.Related)
        Assert.Equal("No interpretation recorded.", f.State)

[<Fact>]
let ``S19 replay steps through recorded versions read-only, showing the hypotheses as they stood`` () =
    let port, id, _ = active ()
    let route = Routes.Investigation(id, Routes.Timeline)
    let at v = { Shell.initial with Route = route; Port = port; Selected = "replay"; HistoryVersion = v }
    let first = View.project (at None) |> AnalysisView.extend (at None)
    Assert.Equal("replay-spectator", textOf first "an.timeline.screen")
    Assert.StartsWith("Step 1 of 3: version 1 of 3.", textOf first "an.replay.version")
    Assert.True(flagOf first "an.replay.hypotheses.empty")
    Assert.False(flagOf first "an.replay.hasPrev")
    Assert.EndsWith("?item=replay&version=2", textOf first "an.replay.nextHref")
    let last = View.project (at (Some 3L)) |> AnalysisView.extend (at (Some 3L))
    Assert.Contains("The valve seal is worn.", sprintf "%A" (last |> Map.find "an.replay.hypotheses"))
    Assert.Equal("This is the last recorded event; nothing follows.", textOf last "an.replay.next")

let private providerAnswer (proposals: (string * string) list) =
    let items = proposals |> List.map (fun (k, t) -> sprintf """{"kind":"%s","text":"%s"}""" k t) |> String.concat ","
    let inner = sprintf """{"proposals":[%s]}""" items
    let body = sprintf """{"content":[{"type":"text","text":%s}],"stop_reason":"end_turn","usage":{"input_tokens":10,"output_tokens":5}}""" (Text.Json.JsonSerializer.Serialize inner)
    // Limen delivers a text response as a JSON string literal.
    EffectOutcome.Success(200L, Limen.Contract.RawJson(Text.Json.JsonSerializer.Serialize body), None)

[<Fact>]
let ``S17 the panel runs one blind request per role through the port and shows structural differences with provenance`` () =
    let port, id, _ = active ()
    let port, _ = port.Handle (entropy ()) (SetAiKey "fake-key-for-tests-0000")
    let port, fx = port.Handle (entropy ()) (RequestPanel(id, "fixture-model", "Propose next steps."))
    let corrs = fx |> List.choose (fun e -> match e with EffectRequest.Http h -> (let (CorrelationId c) = h.CorrelationId in Some c) | _ -> None)
    Assert.Equal<string list>([ "panel-1"; "panel-2"; "panel-3" ], corrs)
    let answers = [ [ "hypothesis", "A certificate expired" ]; [ "hypothesis", "A certificate expired" ]; [ "unknown", "Was the gateway restarted?" ] ]
    let port =
        List.zip corrs answers
        |> List.fold (fun (p: Port) (c, a) -> p.Handle (entropy ()) (Answer(c, EffectResult.HttpResult(CorrelationId c, providerAnswer a))) |> fst) port
    let route = Routes.Investigation(id, Routes.AiProposals)
    let v = view port route "panel"
    Assert.Equal("multi-agent-panel", textOf v "an.proposals.screen")
    let differences = sprintf "%A" (v |> Map.find "an.panel.disagreements")
    Assert.Contains("not proposed by Timeline checker", differences)
    Assert.DoesNotContain("majority", differences.ToLowerInvariant())
    Assert.Contains("prompt causa.panel-role/timeline-checker/1, source version 3", sprintf "%A" (v |> Map.find "an.panel.provenance"))

[<Fact>]
let ``S18 the tournament measures each treatment on one frozen version and names no winner`` () =
    let port, id, _ = active ()
    let port, _ = port.Handle (entropy ()) (SetAiKey "fake-key-for-tests-0000")
    let port, fx = port.Handle (entropy ()) (RequestTournament(id, [ "model-a", "causa.proposal-prompt/1"; "model-b", "causa.panel-role/evidence-auditor/1" ], "Propose."))
    Assert.Equal(2, fx.Length)
    let port = port.Handle (entropy ()) (Answer("tour-1", EffectResult.HttpResult(CorrelationId "tour-1", providerAnswer [ "hypothesis", "H" ]))) |> fst
    let v = view port (Routes.Investigation(id, Routes.AiProposals)) "tournament"
    Assert.True(flagOf v "an.tour.show")
    Assert.Equal("research-tournament", textOf v "an.proposals.screen")
    let metrics = sprintf "%A" (v |> Map.find "an.tour.metrics")
    Assert.Contains("Valid output: yes", metrics)
    Assert.Contains("Waiting for the answer.", metrics)
    Assert.DoesNotContain("winner", metrics.ToLowerInvariant())
    Assert.Contains("No treatment is ranked", sprintf "%A" (v |> Map.find "an.tour.limitations"))
