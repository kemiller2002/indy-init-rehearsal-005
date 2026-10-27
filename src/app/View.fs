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
                      "label", c.Label
                      "reason", (c.Reason |> Option.defaultValue "")
                      "reasonId", "reason-" + c.Id
                      "blockerHref", (c.BlockerId |> Option.map (fun b -> Routes.formatItem (Investigation(i.Id, Overview)) b) |> Option.defaultValue "") ]
                    [ "legal", c.Legal; "illegal", not c.Legal; "hasBlocker", c.BlockerId.IsSome ]))
        |> Option.defaultValue []

    let timeline =
        inv
        |> Option.map (fun i ->
            i.Timeline
            |> List.sortBy (fun t -> t.Sequence)
            |> List.map (fun t ->
                item
                    [ "id", string t.Sequence
                      "sequence", $"#{t.Sequence}"
                      "type", t.EventType
                      "occurred", "Event time " + t.OccurredAt
                      "recorded", (t.RecordedAt |> Option.map (fun r -> "Recorded " + r) |> Option.defaultValue "Recorded time not available")
                      "actor", (t.Actor |> Option.defaultValue "Actor not recorded") ]))
        |> Option.defaultValue []

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

    let adminRows =
        let row id l v st = item [ "id", id; "label", l; "value", v; "state", st ]

        let build =
            match model.Build with
            | NotRequested
            | Loading -> [ row "build" "Deployed build" "Reading build-info.json" "Unknown" ]
            | Failed e -> [ row "build" "Deployed build" e "Unavailable" ]
            | Loaded pairs -> pairs |> List.mapi (fun i (k, v) -> row $"build-{i}" k v "Reported by the deployed build")

        [ row "connection" "Provider connection" (connectionText ws.Connection) (if connected then "Connected" else "Not connected") ]
        @ (ws.Administration |> List.map (fun f -> row f.Id f.Label f.Value f.State))
        @ build
        // VER-007-O2: the runtime actually executing this projection, read from itself.
        @ [ row "runtime" ".NET runtime (loaded)" (string System.Environment.Version) "Reported by the running application" ]

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
      "sync.text", text (syncText ws.Sync)
      "sync.tone", text (syncTone ws.Sync)
      // Administration (UI-018) and Settings (UI-019).
      "screen.admin", flag (screen = AdministrationScreen)
      "admin", items adminRows
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
