/// Percepta state injection (test build only): an explicit domain state `{ name, predicates, capabilities }` becomes a
/// fixed WorkspaceProjection, which the normal View projects. Nothing here is reachable in the production build:
/// Dispatch accepts the event only after the test build's main.js enabled the hooks.
module Causa.App.TestState

open System.Text.Json
open Causa.App.Adapter

/// The fixture investigation's id: a test-only GUID, not any real investigation.
let fixtureId = "00000000-0000-4000-8000-0000000000f1"

/// The predicates this application can represent. Any other predicate is refused (Percepta then reports the
/// state-dependent checks as Unavailable rather than checking a default state).
let supportedPredicates = set [ "material-blocker-exists" ]

/// `Conflict`: the fixture's optional "sync": "conflict" member, so the persistence-conflict screen (S23) can be shown
/// without a provider (Percepta passes the fixture JSON through unchanged).
type Injected = { Name: string; Predicates: string list; Capabilities: (string * bool) list; Conflict: bool }

let parse (json: string) : Result<Injected, string> =
    try
        use doc = JsonDocument.Parse json
        let root = doc.RootElement

        let name =
            match root.TryGetProperty "name" with
            | true, v when v.ValueKind = JsonValueKind.String -> v.GetString()
            | _ -> "unnamed"

        let predicates =
            match root.TryGetProperty "predicates" with
            | true, v when v.ValueKind = JsonValueKind.Array ->
                [ for p in v.EnumerateArray() do
                      if p.ValueKind = JsonValueKind.String then yield p.GetString() ]
            | _ -> []

        let capabilities =
            match root.TryGetProperty "capabilities" with
            | true, v when v.ValueKind = JsonValueKind.Object ->
                [ for c in v.EnumerateObject() do
                      if c.Value.ValueKind = JsonValueKind.String then yield c.Name, (c.Value.GetString() = "legal") ]
            | _ -> []

        let conflict =
            match root.TryGetProperty "sync" with
            | true, v when v.ValueKind = JsonValueKind.String -> v.GetString() = "conflict"
            | _ -> false

        match predicates |> List.filter (fun p -> not (supportedPredicates.Contains p)) with
        | [] -> Ok { Name = name; Predicates = predicates; Capabilities = capabilities; Conflict = conflict }
        | unsupported -> Error("Unsupported predicate(s): " + String.concat ", " unsupported)
    with ex ->
        Error("The injected state is not valid JSON: " + ex.Message)

let private capabilityLabel (id: string) =
    match id with
    | "RecordObservation" -> "Record evidence"
    | "RecordHypothesis" -> "Record a hypothesis"
    | "ConfirmCause" -> "Confirm the cause"
    | "StartInvestigation" -> "Start the investigation"
    | "FalsifyHypothesis" -> "Falsify the hypothesis"
    | "ReopenHypothesis" -> "Reopen the hypothesis"
    | "RelateEvidence" -> "Link evidence to a hypothesis"
    | "PinPolicyVersion" -> "Pin the policy version"
    | "RecordPrediction" -> "Record a prediction"
    | "RecordInvestigationAction" -> "Plan an investigation action"
    | "RecordCorrectiveAction" -> "Record a corrective action"
    | "MarkCorrectiveActionImplemented" -> "Mark implemented"
    | "RecordVerification" -> "Record a verification result"
    | "ResolveUnknown" -> "Mark the question answered"
    | "RecordFinding" -> "Record the result"
    | "ResolveContradiction" -> "Mark the contradiction resolved"
    | other -> other

/// The fixed projection for an injected state.
let toProjection (s: Injected) : WorkspaceProjection =
    let blocked = s.Predicates |> List.contains "material-blocker-exists"
    let blockerId = "blocker-1"

    let blockers =
        if blocked then [ { Id = blockerId; Question = "Was the material change in the event sequence observed directly?"; Material = true } ] else []

    let capabilities =
        s.Capabilities
        |> List.map (fun (id, legal) ->
            { Id = id
              Name = id
              Input = None
              Label = capabilityLabel id
              Legal = legal
              Reason = (if legal then None elif blocked then Some "An open material question must be answered first." else Some "Not allowed in the current status.")
              BlockerId = (if not legal && blocked then Some blockerId else None) })

    let investigation =
        { Id = fixtureId
          Title = "Fixture investigation"
          Version = 7L
          Lifecycle = (if blocked then "awaitingEvidence" else "active")
          Writable = true
          Description = Some "A fixture state injected by the Percepta verification run."
          Hypotheses = [ { Id = "h-1"; Statement = "A seal lost resilience in the cold."; State = "plausible"; Supporting = Known 1; Contradicting = Known 0; Evidence = [ "e-1" ] } ]
          Evidence = [ { Id = "e-1"; Content = "Launch-time temperature was recorded below the qualified range."; Source = Some "Fixture source"; Category = "measurement"; RecordedAt = Some "2026-01-01T00:00:00Z" } ]
          Blockers = blockers
          Causes = [ { Id = "c-1"; Outcome = "The joint failed during ascent."; State = "identified"; Members = [ "h-1", RootCause ] } ]
          Proposals = []
          Findings = None
          Integrity = None
          Report = None
          Timeline = [ { Sequence = 1L; EventType = "InvestigationOpened"; OccurredAt = "2026-01-01T00:00:00Z"; RecordedAt = Some "2026-01-01T00:00:00Z"; Actor = Some "Fixture"; Category = "lifecycle"; Affected = []; ClockBeforePrevious = false } ]
          Capabilities = capabilities
          Typed = UxFixture.source
          Detail =
            { Contradictions = [ { Id = "x-1"; Text = "The recorded temperature and the operator's log disagree."; State = "Unresolved"; Related = [ "e-1" ] } ]
              Assumptions = [ { Id = "a-1"; Text = "The sensor was calibrated before the event."; State = "Not verified · Material"; Related = [] } ]
              Predictions = [ { Id = "p-1"; Text = "A seal tested at the same temperature loses resilience."; State = "Necessary: if it fails, the hypothesis is falsified · Prospective: recorded before any finding"; Related = [ "h-1" ] } ]
              Actions = [ { Id = "act-1"; Text = "Compress a seal sample at the recorded temperature."; State = "Test · Proposed"; Related = [ "h-1" ] } ]
              NegativeKnowledge = [ { Id = "nk-1"; Text = "The joint was not damaged in transport."; State = "Scope: transport to the site"; Related = [ "e-1" ] } ]
              Unknowns = [ { Id = blockerId; Text = "Was the material change in the event sequence observed directly?"; State = (if blocked then "Open · Material" else "Open · Not material"); Related = [] } ]
              CauseGates = [ "c-1", (if blocked then [ "An open material question must be answered first." ] else []) ]
              Corrections = [ { Id = "ca-1"; Description = "Qualify the seal for the lowest launch temperature."; Kind = "Root-cause correction"; State = "Approved"; Targets = [ "c-1" ] } ]
              Findings = [ { Id = "f-1"; Text = "The sample lost 40% of its rebound at the recorded temperature."; State = "No interpretation recorded."; Related = [ "act-1" ] } ]
              Criteria = [ { Id = "cr-1"; Correction = "ca-1"; Expected = "No seal loses resilience in qualification at the lowest temperature."; Method = Some "Qualification test"; Satisfied = false; Results = [] } ] } }

    { Connection = ConnectedTo("fixture/percepta", "main", "test")
      ConnectionStatus = None
      Investigations =
        Loaded
            [ { Id = fixtureId
                Title = investigation.Title
                Lifecycle = investigation.Lifecycle
                Writable = true
                LastActivity = Some "2026-01-01"
                Hypotheses = Known 1
                Evidence = Known 1
                Contradictions = Unknown
                OpenQuestionCount = Unknown
                NextAction = None } ]
      Opened = Map.ofList [ fixtureId, Loaded investigation ]
      Sync = (if s.Conflict then Conflict else NoChanges)
      Capabilities = capabilities
      Blockers = blockers
      Message = None
      Ai =
        { noAi with
            Investigation = Some fixtureId
            Panel =
              Some
                  { SourceVersion = 7L
                    Running = false
                    Roles = EngineView.panelRoles
                    Answers =
                      [ { Key = "evidence-auditor"; Label = "Evidence auditor"; Model = "fixture-model"; PromptVersion = "causa.panel-role/evidence-auditor/1"; SourceVersion = 7L; Outcome = "1 proposal(s) to review."
                          Drafts = [ { Kind = "Open question"; Text = "Was the seal temperature measured at the joint?"; Rationale = None } ] }
                        { Key = "alternative-cause-seeker"; Label = "Alternative-cause seeker"; Model = "fixture-model"; PromptVersion = "causa.panel-role/alternative-cause-seeker/1"; SourceVersion = 7L; Outcome = "1 proposal(s) to review."
                          Drafts = [ { Kind = "Hypothesis"; Text = "Putty blow-by opened a leak path."; Rationale = None } ] }
                        { Key = "timeline-checker"; Label = "Timeline checker"; Model = "fixture-model"; PromptVersion = "causa.panel-role/timeline-checker/1"; SourceVersion = 7L; Outcome = "The model declined to answer."; Drafts = [] } ]
                    Disagreements =
                      [ "Open question “Was the seal temperature measured at the joint?”: proposed by Evidence auditor; not proposed by Alternative-cause seeker."
                        "Hypothesis “Putty blow-by opened a leak path.”: proposed by Alternative-cause seeker; not proposed by Evidence auditor." ]
                    Agreements = [] }
            Tournament =
              Some
                  { SourceVersion = 7L
                    Running = false
                    Task = "Propose next hypotheses."
                    Treatments =
                      [ { Id = "Treatment A"; Model = "fixture-model-a"; PromptVersion = "causa.proposal-prompt/1"; Measurements = [ "Valid output: yes"; "Drafts: 2"; "Refused: no"; "Latency: 1200 ms (measured in this browser)"; "Provider tokens: 900 in, 60 out (as reported)" ] }
                        { Id = "Treatment B"; Model = "fixture-model-b"; PromptVersion = "causa.panel-role/evidence-auditor/1"; Measurements = [ "Valid output: no"; "Drafts: 0"; "Refused: no"; "Latency: 400 ms (measured in this browser)"; "Provider tokens: not reported" ] } ]
                    Limitations = [ "One investigation at one frozen version (7); results do not generalise to other cases."; "No treatment is ranked or named the winner; compare the measurements for the question you have." ] } }
      Administration = [ { Id = "fixture"; Section = "persistence"; Label = "Data provider"; Value = "Percepta fixture state " + s.Name; State = "Unknown" } ] }

/// The fixture's history (S15): version 1 against the fixture's latest version, as fixed words.
let history (s: Injected) (_: string) (version: int64) : HistoryView option =
    let latest = 7L
    if version < 1L || version > latest then None
    else
        let historical = version < latest
        Some
            { AtVersion = version
              LatestVersion = latest
              IsHistorical = historical
              Title = "Fixture investigation"
              Lifecycle = "active"
              Counts = [ "Hypotheses", 1; "Evidence items", (if historical then 0 else 1) ]
              ChangesHere = [ "Status set to Active" ]
              ChangesSince = (if historical then [ "Added evidence e-1"; "Hypothesis h-1: Proposed → Plausible" ] else [])
              BlockerChanges = (if historical && s.Predicates |> List.contains "material-blocker-exists" then [ "Opened since: Was the material change in the event sequence observed directly?" ] else [])
              CapabilityChanges = (if historical then [ "Link evidence to a hypothesis: not available then, available now" ] else [])
              Hypotheses = [ "A seal lost resilience in the cold.", (if historical then "Proposed" else "Plausible") ] }

/// The port for an injected state: the fixed projection, plus the fixture history for time travel.
let port (s: Injected) : Port =
    let projection = toProjection s
    let rec fixedPort () : Port =
        { Name = "fixture"
          History = history s
          Projection = projection
          Handle = fun _ _ -> fixedPort (), [] }
    fixedPort ()
