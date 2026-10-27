/// The projection: the shell model and the port's WorkspaceProjection as the flat key -> value view the Limen kernel
/// binds into index.html. Total: every key is present in every projection. Pure.
module Causa.App.View

open Limen.Contract.Core
open Causa.App.Routes
open Causa.App.Adapter
open Causa.App.Presentation
open Causa.App.Shell

let private text (s: string) = ViewValue.Text s
let private flag (b: bool) = ViewValue.Flag b
let private items (xs: ViewItem list) = ViewValue.Items xs
let private item (fields: (string * string) list) : ViewItem = fields |> List.map (fun (k, v) -> k, ViewPrimitive.Text v) |> Map.ofList

let private itemF (fields: (string * string) list) (flags: (string * bool) list) : ViewItem =
    (fields |> List.map (fun (k, v) -> k, ViewPrimitive.Text v)) @ (flags |> List.map (fun (k, v) -> k, ViewPrimitive.Flag v)) |> Map.ofList

let private current b = if b then "page" else "false"

/// Which screen the route shows, given what the port knows.
type Screen =
    | ConnectScreen
    | ListScreen
    | NewScreen
    | OpeningScreen
    | InvestigationScreen of InvestigationProjection * Section
    | FailedScreen of string
    | MissingScreen
    | AdministrationScreen
    | SettingsScreen
    | NotFoundScreen

let screenOf (model: Model) =
    let ws = model.Port.Projection
    let connected = match ws.Connection with ConnectedTo _ -> true | _ -> false

    match model.Route with
    | Administration -> AdministrationScreen
    | Settings -> SettingsScreen
    | NotFound _ -> NotFoundScreen
    | _ when not connected -> ConnectScreen
    | Home
    | Investigations -> ListScreen
    | NewInvestigation -> NewScreen
    | Investigation(id, section) ->
        match ws.Opened.TryFind id with
        | Some(Loaded inv) -> InvestigationScreen(inv, section)
        | Some(Failed reason) -> FailedScreen reason
        | Some Loading -> OpeningScreen
        | Some NotRequested
        | None ->
            match ws.Investigations with
            | Loaded cards when not (cards |> List.exists (fun c -> c.Id = id)) -> MissingScreen
            | _ -> OpeningScreen

let private screenName =
    function
    | ConnectScreen -> "connect"
    | ListScreen -> "investigations"
    | NewScreen -> "investigation-start"
    | OpeningScreen -> "investigation-opening"
    | InvestigationScreen(_, Overview) -> "investigation-overview"
    | InvestigationScreen(_, CausalAnalysis) -> "investigation-causal-analysis"
    | InvestigationScreen(_, Evidence) -> "investigation-evidence"
    | InvestigationScreen(_, Hypotheses) -> "investigation-hypotheses"
    | InvestigationScreen(_, Timeline) -> "investigation-timeline"
    | InvestigationScreen(_, CorrectiveActions) -> "investigation-corrective-actions"
    | InvestigationScreen(_, Report) -> "investigation-report"
    | InvestigationScreen(_, AiProposals) -> "investigation-proposals"
    | FailedScreen _ -> "investigation-failed"
    | MissingScreen -> "investigation-missing"
    | AdministrationScreen -> "administration"
    | SettingsScreen -> "settings"
    | NotFoundScreen -> "not-found"

let project (model: Model) : ViewState =
    let ws = model.Port.Projection
    let screen = screenOf model
    let connected = match ws.Connection with ConnectedTo _ -> true | _ -> false

    let inv, section =
        match screen with
        | InvestigationScreen(inv, section) -> Some inv, section
        | _ -> None, Overview

    let invId = inv |> Option.map (fun i -> i.Id) |> Option.defaultValue ""
    let on s = (match screen with InvestigationScreen(_, x) -> x = s | _ -> false)

    let primaryNav =
        let area = Routes.primaryOf model.Route

        [ "investigations", "Investigations", "#/investigations", area = InvestigationsArea
          "administration", "Administration", "#/administration", area = AdministrationArea
          "settings", "Settings", "#/settings", area = SettingsArea ]
        |> List.map (fun (id, l, href, cur) -> item [ "id", id; "label", l; "href", href; "current", current cur ])

    let sectionNav =
        Routes.sections
        |> List.map (fun (sec, slug, name) ->
            item [ "id", slug; "label", name; "href", Routes.format (Investigation(invId, sec)); "current", current (inv.IsSome && sec = section) ])

    let cards =
        match ws.Investigations with
        | Loaded cs ->
            cs
            |> List.map (fun c ->
                item
                    [ "id", c.Id
                      "title", c.Title
                      "status", label c.Lifecycle
                      "tone", tone c.Lifecycle
                      "kind", (if c.Writable then "Your working copy" else "Read-only demonstration")
                      "activity", (c.LastActivity |> Option.map (fun a -> "Latest activity " + a) |> Option.defaultValue "Latest activity not recorded")
                      "hypotheses", count c.Hypotheses "hypotheses"
                      "evidence", count c.Evidence "evidence items"
                      "contradictions", count c.Contradictions "unresolved contradictions"
                      "href", Routes.format (Investigation(c.Id, Overview)) ])
        | _ -> []

    let listLoading = match ws.Investigations with Loading | NotRequested -> true | _ -> false
    let listFailed = match ws.Investigations with Failed _ -> true | _ -> false
    let listFailure = match ws.Investigations with Failed r -> r | _ -> ""

    let hypotheses =
        inv
        |> Option.map (fun i ->
            i.Hypotheses
            |> List.map (fun h ->
                item
                    [ "id", h.Id
                      "statement", h.Statement
                      "state", label h.State
                      "tone", tone h.State
                      "supporting", count h.Supporting "supporting"
                      "contradicting", count h.Contradicting "contradicting"
                      "href", Routes.formatItem (Investigation(i.Id, Hypotheses)) h.Id ]))
        |> Option.defaultValue []

    let evidence =
        inv
        |> Option.map (fun i ->
            i.Evidence
            |> List.map (fun e ->
                item
                    [ "id", e.Id
                      "content", e.Content
                      "source", (e.Source |> Option.defaultValue "Source not recorded")
                      "category", label e.Category
                      "recorded", (e.RecordedAt |> Option.map (fun t -> "Recorded " + t) |> Option.defaultValue "Recording time not recorded")
                      "href", Routes.formatItem (Investigation(i.Id, Evidence)) e.Id ]))
        |> Option.defaultValue []

    let blockers =
        inv
        |> Option.map (fun i ->
            i.Blockers |> List.map (fun b -> item [ "id", b.Id; "anchor", "blocker-" + b.Id; "selected", (if model.Selected = b.Id then "true" else "false"); "question", b.Question; "materiality", (if b.Material then "Material question" else "Not material") ]))
        |> Option.defaultValue []

    let capabilities =
        inv
        |> Option.map (fun i ->
            i.Capabilities
            |> List.map (fun c ->
                itemF
                    [ "id", c.Id
                      "name", c.Name
                      "label", c.Label
                      "inputLabel", (c.Input |> Option.defaultValue "")
                      "inputId", "input-" + c.Id.Replace(":", "-")
                      "inputLabelId", "input-label-" + c.Id.Replace(":", "-")
                      "inputValue", (model.CommandInputs |> Map.tryFind c.Id |> Option.defaultValue "")
                      "reason", (c.Reason |> Option.defaultValue "")
                      "reasonId", "reason-" + c.Id.Replace(":", "-")
                      "blockerHref", (c.BlockerId |> Option.map (fun b -> Routes.formatItem (Investigation(i.Id, Overview)) b) |> Option.defaultValue "") ]
                    [ "legal", c.Legal && c.Input.IsNone; "legalInput", c.Legal && c.Input.IsSome; "illegal", not c.Legal; "hasBlocker", c.BlockerId.IsSome ]))
        |> Option.defaultValue []

    let timeline =
        inv
        |> Option.map (fun i ->
            i.Timeline
            |> List.sortBy (fun t -> t.Sequence)
            |> List.filter (fun t -> model.TimelineFilter = "all" || t.Category = model.TimelineFilter)
            |> List.map (fun t ->
                let first = t.Affected |> List.tryHead
                let sectionOf slug = Routes.sections |> List.tryFind (fun (_, s, _) -> s = slug) |> Option.map (fun (x, _, _) -> x)
                itemF
                    [ "id", string t.Sequence
                      "sequence", $"#{t.Sequence}"
                      "type", t.EventType
                      "occurred", "Event time " + t.OccurredAt
                      "recorded", (t.RecordedAt |> Option.map (fun r -> "Recorded " + r) |> Option.defaultValue "Recorded time not available")
                      "actor", (t.Actor |> Option.defaultValue "Actor not recorded")
                      "versionHref", Routes.format (Investigation(i.Id, Timeline)) + $"?version={t.Sequence}"
                      "affectedText", (first |> Option.map (fun (_, _, w) -> w) |> Option.defaultValue "")
                      "affectedHref",
                      (first
                       |> Option.bind (fun (slug, id, _) -> sectionOf slug |> Option.map (fun sec -> Routes.formatItem (Investigation(i.Id, sec)) id))
                       |> Option.defaultValue "")
                      "current", (if model.HistoryVersion = Some t.Sequence then "true" else "false") ]
                    [ "hasAffected", first.IsSome
                      "clockNote", t.ClockBeforePrevious ]))
        |> Option.defaultValue []

    // Time travel (T28): the state at the chosen version, derived by the engine and labelled historical.
    let history =
        match inv, model.HistoryVersion with
        | Some i, Some v -> model.Port.History i.Id v
        | _ -> None

    let versions =
        inv
        |> Option.map (fun i ->
            i.Timeline |> List.map (fun t -> item [ "id", string t.Sequence; "value", string t.Sequence; "label", $"Version {t.Sequence}: {t.EventType}" ]))
        |> Option.defaultValue []

    let categories =
        item [ "id", "all"; "value", "all"; "label", "All events" ]
        :: (EngineView.categoryLabels |> List.map (fun (k, l) -> item [ "id", k; "value", k; "label", l ]))

    let graphDoc = inv |> Option.map (fun i -> Graph.toDocument i.Title i.Id (Graph.ofInvestigation i)) |> Option.defaultValue ""
    let graphSelection = inv |> Option.map (fun i -> Graph.describeSelection i model.Selected) |> Option.defaultValue ""

    let hypothesisText =
        inv |> Option.map (fun i -> i.Hypotheses |> List.map (fun h -> h.Id, h.Statement) |> Map.ofList) |> Option.defaultValue Map.empty

    // The same causal structure as text (UI-011-O5, UI-026-O4): the graph is never the only way to read it.
    let causes =
        inv
        |> Option.map (fun i ->
            i.Causes
            |> List.sortBy (fun c -> c.Id)
            |> List.map (fun c ->
                let members =
                    c.Members
                    |> List.sortBy fst
                    |> List.map (fun (h, role) -> $"""{roleLabel role}: {hypothesisText |> Map.tryFind h |> Option.defaultValue "a hypothesis not in this view"}""")

                item
                    [ "id", c.Id
                      "outcome", c.Outcome
                      "state", label c.State
                      "tone", tone c.State
                      "members", (if members.IsEmpty then "No contributing hypotheses recorded." else String.concat "; " members) ]))
        |> Option.defaultValue []

    // Counts of what the projection says blocks progress; derived only from the engine's blockers and legality.
    let materialQuestions = inv |> Option.map (fun i -> i.Blockers |> List.filter (fun b -> b.Material) |> List.length) |> Option.defaultValue 0
    let blockedActions = inv |> Option.map (fun i -> i.Capabilities |> List.filter (fun c -> not c.Legal && c.BlockerId.IsSome) |> List.length) |> Option.defaultValue 0

    let attentionQuestions =
        match materialQuestions with
        | 0 -> "No material open questions are recorded."
        | 1 -> "1 material open question."
        | n -> $"{n} material open questions."

    let attentionBlocked =
        if blockedActions = 1 then "1 action is unavailable until it is answered; nothing it would establish is shown as settled."
        else $"{blockedActions} actions are unavailable until they are answered; nothing they would establish is shown as settled."

    // Administration (UI-018, S25): every fact in one of six sections, each with an explicit state. Only states that
    // report an observed fact count as verified; everything else is listed as not verified (never implied).
    let observedStates = set [ "Verified"; "Connected"; "Reported by the deployed build"; "Reported by the running application"; "Integrity check passed" ]

    let facts =
        let fact id sec l v st = { Id = id; Section = sec; Label = l; Value = v; State = st }

        let build =
            match model.Build with
            | NotRequested
            | Loading -> [ fact "build" "deployment" "Deployed build" "Reading build-info.json" "Unknown" ]
            | Failed e -> [ fact "build" "deployment" "Deployed build" e "Unavailable" ]
            | Loaded pairs ->
                pairs
                |> List.mapi (fun i (k, v) ->
                    let sec = if k.StartsWith "security." then "security" else "deployment"
                    fact $"build-{i}" sec (if k.StartsWith "security." then k.Substring 9 else k) v "Reported by the deployed build")

        let integrity =
            ws.Opened
            |> Map.toList
            |> List.choose (fun (id, l) ->
                match l with
                | Loaded i -> i.Integrity |> Option.map (fun t -> fact ("integrity-" + id) "persistence" ("Data integrity: " + i.Title) t "Integrity check passed")
                | _ -> None)

        [ fact "connection" "persistence" "Provider connection" (connectionText ws.Connection) (if connected then "Connected" else "Not connected") ]
        @ ws.Administration
        @ integrity
        @ build
        // VER-007-O2: the runtime actually executing this projection, read from itself.
        @ [ fact "runtime" "deployment" ".NET runtime (loaded)" (string System.Environment.Version) "Reported by the running application"
            fact "ai" "ai" "AI provider key" (if ws.Ai.KeyHeld then "Held in this page's memory (never shown or stored)" else "Not entered") (if ws.Ai.KeyHeld then "Unknown" else "Unavailable")
            fact "ai-model" "ai" "AI model id" (if model.Settings.AiModel = "" then "Not set (there is no default)" else model.Settings.AiModel) (if model.Settings.AiModel = "" then "Unavailable" else "Unknown")
            fact "ai-last" "ai" "Last AI request (key redacted)" (ws.Ai.LastRequest |> Option.defaultValue "None in this session") "Unknown"
            fact "report-print" "report" "Report preview and print" "Rendered from the open investigation; print output is not verified here" "Unknown"
            fact "token" "security" "Credential handling" "The token is held in this page's memory only and sent only in the Authorization header" "Design rule; not measured here"
            fact "smoke" "smoke-tests" "Smoke tests at this commit" "Results are recorded by CI, not by the running application" "Unknown" ]

    let adminRows sec =
        facts
        |> List.filter (fun f -> f.Section = sec)
        |> List.map (fun f -> itemF [ "id", f.Id; "label", f.Label; "value", f.Value; "state", f.State ] [ "observed", observedStates.Contains f.State ])

    let unverified = facts |> List.filter (fun f -> not (observedStates.Contains f.State))

    let readiness =
        match unverified.Length with
        | 0 -> "Every item below reports an observed state."
        | n -> $"Not confirmed ready to demonstrate: {n} item(s) are not verified."

    // Starting an investigation (S01): only the capability the F# policy returned; none means not available.
    let start =
        ws.Capabilities
        |> List.tryFind (fun c -> c.Id = "StartInvestigation")
        |> Option.defaultValue
            { Id = "StartInvestigation"; Name = "StartInvestigation"; Input = None; Label = "Start the investigation"; Legal = false; Reason = Some "Starting an investigation is not available in this build yet."; BlockerId = None }

    let wsBlockers =
        ws.Blockers |> List.map (fun b -> item [ "id", b.Id; "anchor", "ws-blocker-" + b.Id; "question", b.Question; "materiality", (if b.Material then "Material question" else "Not material") ])

    let destination =
        match ws.Connection with
        | ConnectedTo(repo, branch, workspace) -> $"GitHub repository {repo}, branch {branch}, workspace {workspace}. Nothing is written until you save."
        | _ -> "Not connected: there is nowhere to save an investigation yet."

    // Report preview (S22, R12.3): an investigation that is not resolved or closed is shown as incomplete.
    let report = inv |> Option.bind (fun i -> i.Report)
    let complete = inv |> Option.exists (fun i -> i.Lifecycle = "resolved" || i.Lifecycle = "closed")
    let confirmedCauses = inv |> Option.map (fun i -> i.Causes |> List.filter (fun c -> c.State = "confirmed")) |> Option.defaultValue []

    // AI proposals (T27): recorded proposals with the engine's accept/reject legality, and the loop's state.
    let capability id = inv |> Option.bind (fun i -> i.Capabilities |> List.tryFind (fun c -> c.Id = id))

    let proposals =
        inv
        |> Option.map (fun i ->
            i.Proposals
            |> List.map (fun p ->
                let accept = capability ("AcceptProposal:" + p.Id)
                let reject = capability ("RejectProposal:" + p.Id)
                let legal (c: Capability option) = c |> Option.exists (fun c -> c.Legal)
                let why (c: Capability option) = c |> Option.bind (fun c -> c.Reason) |> Option.defaultValue ""

                itemF
                    [ "id", p.Id
                      "kind", p.Kind
                      "text", p.Text
                      "rationale", (p.Rationale |> Option.defaultValue "No rationale given.")
                      "state", p.State
                      "by", p.By
                      "reasonInputId", "reject-reason-" + p.Id
                      "reasonLabelId", "reject-label-" + p.Id
                      "reasonValue", (model.CommandInputs |> Map.tryFind ("RejectProposal:" + p.Id) |> Option.defaultValue "")
                      "acceptWhy", why accept
                      "rejectWhy", why reject ]
                    [ "canAccept", legal accept
                      "canReject", legal reject
                      "decided", not (legal accept || legal reject)
                      "acceptBlocked", (accept.IsSome && not (legal accept))
                      "rejectBlocked", (reject.IsSome && not (legal reject)) ]))
        |> Option.defaultValue []

    let ai = ws.Ai
    let aiHere = inv.IsSome && ai.Investigation = (inv |> Option.map (fun i -> i.Id))

    let notice, noticeTone =
        match model.Notice, ws.Message with
        | Some n, _ -> Some n.Text, n.Tone
        | None, Some(t, m) -> Some m, t
        | None, None -> None, "info"

    let pageTitle =
        match screen with
        | AdministrationScreen -> "Administration · Causa"
        | SettingsScreen -> "Settings · Causa"
        | ConnectScreen -> "Connect · Causa"
        | ListScreen -> "Investigations · Causa"
        | NewScreen -> "New investigation · Causa"
        | InvestigationScreen(i, s) -> $"{i.Title} · {sectionLabel s} · Causa"
        | OpeningScreen -> "Opening investigation · Causa"
        | FailedScreen _ -> "Investigation unavailable · Causa"
        | MissingScreen -> "Investigation not found · Causa"
        | NotFoundScreen -> "Page not found · Causa"

    let connectFailure = match ws.Connection with ConnectionFailed r -> r | _ -> ""

    [ "page.title", text pageTitle
      "screen", text (screenName screen)
      "secrets.epoch", text (string model.SecretEpoch)
      "percepta.state", text (model.InjectedState |> Option.defaultValue "")
      "text.size", text (if model.Settings.LargeText then "large" else "standard")
      "motion", text (if model.Settings.ReducedMotion then "reduced" else "system")
      "nav", items primaryNav
      "connection.label", text (connectionText ws.Connection)
      "connected", flag connected
      "notice.show", flag notice.IsSome
      "notice.text", text (notice |> Option.defaultValue "")
      "notice.tone", text noticeTone
      "notice.alert", flag (notice.IsSome && (noticeTone = "danger" || noticeTone = "warning"))
      "notice.status", flag (notice.IsSome && not (noticeTone = "danger" || noticeTone = "warning"))
      // Connect (UI-001-O1: empty, disconnected workspace with the explicit Connect action).
      "screen.connect", flag (screen = ConnectScreen)
      "draft.repository", text model.Drafts.Repository
      "draft.branch", text model.Drafts.Branch
      "draft.workspace", text model.Drafts.Workspace
      "connect.busy", flag (ws.Connection = Connecting)
      "connect.failed", flag (connectFailure <> "")
      "connect.failure", text connectFailure
      "route.pending", flag (match model.Route with Investigation _ | NewInvestigation -> true | _ -> false)
      // Investigations (UI-001, UI-022).
      "screen.list", flag (screen = ListScreen)
      "list.loading", flag listLoading
      "list.failed", flag listFailed
      "list.failure", text listFailure
      "list.empty", flag (match ws.Investigations with Loaded [] -> true | _ -> false)
      "cards", items cards
      "screen.new", flag (screen = NewScreen)
      "screen.opening", flag (screen = OpeningScreen)
      "screen.failed", flag (match screen with FailedScreen _ -> true | _ -> false)
      "failed.text", text (match screen with FailedScreen r -> r | _ -> "")
      "screen.missing", flag (screen = MissingScreen)
      "screen.notfound", flag (screen = NotFoundScreen)
      // An investigation (UI-025, UI-029-O2).
      "screen.investigation", flag inv.IsSome
      "inv.id", text invId
      "inv.title", text (inv |> Option.map (fun i -> i.Title) |> Option.defaultValue "")
      "inv.status", text (inv |> Option.map (fun i -> label i.Lifecycle) |> Option.defaultValue "")
      "inv.tone", text (inv |> Option.map (fun i -> tone i.Lifecycle) |> Option.defaultValue "neutral")
      "inv.kind", text (inv |> Option.map (fun i -> if i.Writable then "Your working copy" else "Read-only demonstration") |> Option.defaultValue "")
      "inv.version", text (inv |> Option.map (fun i -> $"Version {i.Version}") |> Option.defaultValue "")
      "inv.description", text (inv |> Option.bind (fun i -> i.Description) |> Option.defaultValue "What happened has not been recorded yet.")
      "sections", items sectionNav
      "section.overview", flag (on Overview)
      "section.causal", flag (on CausalAnalysis)
      "section.evidence", flag (on Evidence)
      "section.hypotheses", flag (on Hypotheses)
      "section.timeline", flag (on Timeline)
      "section.corrective", flag (on CorrectiveActions)
      "section.report", flag (on Report)
      "section.proposals", flag (on AiProposals)
      "hypotheses", items hypotheses
      "hypotheses.empty", flag (inv.IsSome && hypotheses.IsEmpty)
      "evidence", items evidence
      "evidence.empty", flag (inv.IsSome && evidence.IsEmpty)
      "blockers", items blockers
      "blockers.empty", flag (inv.IsSome && blockers.IsEmpty)
      "capabilities", items capabilities
      "capabilities.empty", flag (inv.IsSome && capabilities.IsEmpty)
      "timeline", items timeline
      "timeline.empty", flag (inv.IsSome && timeline.IsEmpty)
      "graph.any", flag (graphDoc <> "")
      "graph.document", text graphDoc
      "graph.selected", flag (graphSelection <> "")
      "graph.selection", text graphSelection
      "causes", items causes
      "causes.empty", flag (inv.IsSome && causes.IsEmpty)
      "attention.questions", text attentionQuestions
      "attention.blocked", flag (blockedActions > 0)
      "attention.blockedText", text attentionBlocked
      "attention.material", flag (materialQuestions > 0)
      "attention.first", text (inv |> Option.bind (fun i -> i.Blockers |> List.tryFind (fun b -> b.Material)) |> Option.map (fun b -> b.Question) |> Option.defaultValue "")
      "attention.provisional", text "While a material question is open, nothing it affects is shown as settled."
      "ws.questions", text (match ws.Blockers |> List.filter (fun b -> b.Material) |> List.length with 0 -> "None recorded." | 1 -> "1 material open question." | n -> $"{n} material open questions.")
      "ws.material", flag (ws.Blockers |> List.exists (fun b -> b.Material))
      "ws.first", text (ws.Blockers |> List.tryFind (fun b -> b.Material) |> Option.map (fun b -> b.Question) |> Option.defaultValue "")
      "ws.provisional", text "Starting is blocked until it is answered; nothing has been created."
      "start.availability", text (if start.Legal then "You can start an investigation here." else "Starting is unavailable: " + (start.Reason |> Option.defaultValue "no reason was given."))
      "attention.tone", text (if materialQuestions > 0 then "attention" else "neutral")
      "attention.href", text (inv |> Option.map (fun i -> Routes.formatItem (Investigation(i.Id, Overview)) (i.Blockers |> List.tryHead |> Option.map (fun b -> b.Id) |> Option.defaultValue "")) |> Option.defaultValue "#/")
      "start.legal", flag start.Legal
      "start.illegal", flag (not start.Legal)
      "start.label", text start.Label
      "start.reason", text (start.Reason |> Option.defaultValue "")
      "start.hasBlocker", flag start.BlockerId.IsSome
      "start.blockerHref", text (start.BlockerId |> Option.map (fun b -> Routes.formatItem NewInvestigation b) |> Option.defaultValue "#/investigations/new")
      "draft.title", text model.Drafts.Title
      "draft.statement", text model.Drafts.Statement
      "destination", text destination
      "wsBlockers", items wsBlockers
      "wsBlockers.empty", flag wsBlockers.IsEmpty
      "timeline.version",
      text (
          match history, inv with
          | Some h, _ when h.IsHistorical -> $"Historical view: version {h.AtVersion} of {h.LatestVersion}. This is not the current state."
          | _, Some i -> $"Showing the investigation as of version {i.Version}, the latest recorded version."
          | _ -> ""
      )
      "history.show", flag history.IsSome
      "history.historical", flag (history |> Option.exists (fun h -> h.IsHistorical))
      "history.version", text (match model.HistoryVersion with Some v -> string v | None -> "")
      "history.versions", items versions
      "history.title", text (history |> Option.map (fun h -> h.Title) |> Option.defaultValue "")
      "history.status", text (history |> Option.map (fun h -> label h.Lifecycle) |> Option.defaultValue "")
      "history.counts", items (history |> Option.map (fun h -> h.Counts |> List.map (fun (l, n) -> item [ "id", l; "text", $"{l}: {n}" ])) |> Option.defaultValue [])
      "history.here", items (history |> Option.map (fun h -> h.ChangesHere |> List.mapi (fun k c -> item [ "id", string k; "text", c ])) |> Option.defaultValue [])
      "history.hereEmpty", flag (history |> Option.exists (fun h -> h.ChangesHere.IsEmpty))
      "history.since", items (history |> Option.map (fun h -> h.ChangesSince |> List.mapi (fun k c -> item [ "id", string k; "text", c ])) |> Option.defaultValue [])
      "history.sinceShow", flag (history |> Option.exists (fun h -> h.IsHistorical))
      "history.latestHref", text (inv |> Option.map (fun i -> Routes.format (Investigation(i.Id, Timeline))) |> Option.defaultValue "#/")
      "timeline.filter", text model.TimelineFilter
      "timeline.categories", items categories
      "timeline.replay", text (inv |> Option.map (fun i -> $"{i.Timeline.Length} recorded events, in their recorded order.") |> Option.defaultValue "")
      "report.incomplete", flag (inv.IsSome && not complete)
      "report.status", text (if complete then "Complete" else "Incomplete: this investigation is not resolved, so the report is a working draft.")
      "report.source", text (inv |> Option.map (fun i -> $"{i.Title}, version {i.Version}") |> Option.defaultValue "")
      "report.conclusion",
      text (
          match inv |> Option.bind (fun i -> i.Report) |> Option.bind (fun r -> r.Conclusion), confirmedCauses with
          | Some c, _ -> c
          | None, [] -> "No cause has been confirmed and no conclusion is recorded. The report states no conclusion."
          | None, cs -> "No conclusion is recorded. Confirmed causes: " + (cs |> List.map (fun c -> c.Outcome) |> String.concat "; ")
      )
      "report.causes", items (report |> Option.map (fun r -> r.Causes |> List.mapi (fun k (o, confirmed, members) -> item [ "id", string k; "outcome", o; "state", (if confirmed then "Confirmed" else "Not confirmed"); "members", members ])) |> Option.defaultValue [])
      "report.hasCauses", flag (report |> Option.exists (fun r -> not r.Causes.IsEmpty))
      "report.open", items (report |> Option.map (fun r -> (r.OpenQuestions |> List.map (fun q -> "Open question: " + q)) @ (r.OpenContradictions |> List.map (fun c -> "Open contradiction: " + c)) @ (r.UnverifiedAssumptions |> List.map (fun a -> "Unverified assumption: " + a)) @ (r.AcceptedResiduals |> List.map (fun a -> "Accepted as residual: " + a)) |> List.mapi (fun k t -> item [ "id", string k; "text", t ])) |> Option.defaultValue [])
      "report.engine", flag report.IsSome
      "report.noOpen", flag (report |> Option.forall (fun r -> r.OpenQuestions.IsEmpty && r.OpenContradictions.IsEmpty && r.UnverifiedAssumptions.IsEmpty && r.AcceptedResiduals.IsEmpty))
      "report.corrections", items (report |> Option.map (fun r -> r.Corrections |> List.mapi (fun k (d, st) -> item [ "id", string k; "text", d; "state", st ])) |> Option.defaultValue [])
      "report.noCorrections", flag (report |> Option.forall (fun r -> r.Corrections.IsEmpty))
      "findings.show", flag (inv |> Option.exists (fun i -> i.Findings.IsSome))
      "findings.text",
      text (
          match inv |> Option.bind (fun i -> i.Findings) with
          | Some f ->
              let resolved = if f.Resolved then "Resolved" else "Not resolved"
              $"Confirmed causes: {f.ConfirmedCauses} · Corrective actions verified effective: {f.VerifiedCorrections} · {resolved}"
          | None -> ""
      )
      "report.verification", text (match report with Some r when not r.Corrections.IsEmpty -> "Corrective actions and whether each is verified effective:" | _ -> "No corrective action is recorded; nothing is reported as verified.")
      "proposals", items proposals
      "proposals.empty", flag (inv.IsSome && proposals.IsEmpty)
      "ai.keyHeld", flag ai.KeyHeld
      "ai.noKey", flag (not ai.KeyHeld)
      "ai.modelSet", flag (model.Settings.AiModel <> "")
      "ai.noModel", flag (model.Settings.AiModel = "")
      "ai.model", text model.Settings.AiModel
      "ai.canRequest", flag (ai.KeyHeld && model.Settings.AiModel <> "" && not ai.InFlight)
      "ai.requestDisabled", flag (not (ai.KeyHeld && model.Settings.AiModel <> "") || ai.InFlight)
      "ai.inFlight", flag (aiHere && ai.InFlight)
      "ai.result.show", flag (aiHere && ai.Result.IsSome)
      "ai.result.text", text (ai.Result |> Option.map (fun (t, _, _) -> t) |> Option.defaultValue "")
      "ai.result.tone", text (ai.Result |> Option.map (fun (_, tone, _) -> tone) |> Option.defaultValue "info")
      "ai.retryable", flag (aiHere && not ai.InFlight && ai.Result |> Option.exists (fun (_, _, r) -> r))
      "ai.drafts", items (if aiHere then ai.Drafts |> List.mapi (fun i d -> item [ "id", string i; "kind", d.Kind; "text", d.Text; "rationale", (d.Rationale |> Option.defaultValue "No rationale given.") ]) else [])
      "ai.hasDrafts", flag (aiHere && not ai.Drafts.IsEmpty)
      "ai.stale.show", flag (aiHere && ai.Stale.IsSome)
      "ai.stale.text", text (ai.Stale |> Option.defaultValue "")
      "draft.aiTask", text model.Drafts.AiTask
      "settings.aiModel", text model.Settings.AiModel
      "settings.aiKey", text (if ai.KeyHeld then "A key is held in this page's memory for this session. It is never shown, stored or sent anywhere except the provider's authentication header." else "No key entered. Proposals cannot be requested without one.")
      "sync.text", text (syncText ws.Sync)
      "sync.tone", text (syncTone ws.Sync)
      "sync.mayHaveSaved", flag (match ws.Sync with MayHaveSaved _ -> true | _ -> false)
      // Administration (UI-018) and Settings (UI-019).
      "screen.admin", flag (screen = AdministrationScreen)
      "admin.readiness", text readiness
      "admin.unverified", items (unverified |> List.map (fun f -> item [ "id", f.Id; "text", $"{f.Label}: {f.State}" ]))
      "admin.deployment", items (adminRows "deployment")
      "admin.persistence", items (adminRows "persistence")
      "admin.ai", items (adminRows "ai")
      "admin.report", items (adminRows "report")
      "admin.security", items (adminRows "security")
      "admin.smoke", items (adminRows "smoke-tests")
      "screen.settings", flag (screen = SettingsScreen)
      "settings.textSize", text (if model.Settings.LargeText then "large" else "standard")
      "settings.motion", text (if model.Settings.ReducedMotion then "reduced" else "system")
      "settings.connection",
      text (
          match ws.Connection with
          | ConnectedTo(repo, branch, workspace) -> $"Connected to {repo}, branch {branch}, workspace {workspace}, for this session."
          | _ -> "Not connected. The repository you enter on Connect is used for this session only and is not remembered."
      ) ]
    |> Map.ofList
