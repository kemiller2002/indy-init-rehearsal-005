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

type Injected = { Name: string; Predicates: string list; Capabilities: (string * bool) list }

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

        match predicates |> List.filter (fun p -> not (supportedPredicates.Contains p)) with
        | [] -> Ok { Name = name; Predicates = predicates; Capabilities = capabilities }
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
          Capabilities = capabilities }

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
                Contradictions = Unknown } ]
      Opened = Map.ofList [ fixtureId, Loaded investigation ]
      Sync = NoChanges
      Capabilities = capabilities
      Blockers = blockers
      Message = None
      Ai = noAi
      Administration = [ { Id = "fixture"; Section = "persistence"; Label = "Data provider"; Value = "Percepta fixture state " + s.Name; State = "Unknown" } ] }
