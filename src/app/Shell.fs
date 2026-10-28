/// The application shell: route, navigation, the Connect form, notices, session settings and the build identity.
/// Investigation meaning is delegated to the Port (Adapter.fs). `update` is pure given its Entropy.
module Causa.App.Shell

open System
open Limen.Contract
open Limen.Contract.Core
open Causa.App.Routes
open Causa.App.Adapter

type Notice = { Tone: string; Text: string }

type Drafts =
    { Token: string
      Repository: string
      Branch: string
      Workspace: string
      Title: string
      Statement: string
      /// The provider key while it is typed; cleared the moment it is handed to the port.
      AiKey: string
      AiTask: string }

/// Session-scope preferences (UI-019-O5): kept in this page's memory only, never in browser storage.
type Settings =
    { LargeText: bool
      ReducedMotion: bool
      /// The AI model id to call: configuration the person enters; there is no default (T27).
      AiModel: string }

type Model =
    { Route: Route
      Selected: string
      Drafts: Drafts
      Notice: Notice option
      Settings: Settings
      /// build-info.json as served next to the page (VER-007-O2: what is actually deployed).
      Build: Loadable<(string * string) list>
      /// Evidence published next to the site (evidence/proof.json, evidence/factory.json), as text, by name.
      Evidence: Map<string, Loadable<string>>
      Port: Port
      Counter: int
      /// Correlation ids of requests the shell itself made; every other answer belongs to the port.
      ShellPending: Set<string>
      PageBase: string
      /// The version the history screen shows (from `?version=` or the picker); None is the latest (T28).
      HistoryVersion: int64 option
      /// Timeline category filter ("all" or a category key, UI-003-O2).
      TimelineFilter: string
      /// The investigation catalog's order (stream ux): "activity", "title" or "status".
      CatalogSort: string
      /// A destructive or irreversible action waiting for the person's confirmation (UI-004-O3), with its words.
      PendingConfirm: (AdapterInput * string) option
      /// Bumped each time a token or key is taken, so the page can empty its password fields.
      SecretEpoch: int
      /// Text typed for a capability that needs one input, by capability id.
      CommandInputs: Map<string, string>
      /// Only the test build enables this (Percepta state injection).
      TestHooks: bool
      /// The Percepta fixture state name currently shown, if any.
      InjectedState: string option }

let initialDrafts = { Token = ""; Repository = ""; Branch = "main"; Workspace = ""; Title = ""; Statement = ""; AiKey = ""; AiTask = "" }

let initial =
    { Route = Home
      Selected = ""
      Drafts = initialDrafts
      Notice = None
      Settings = { LargeText = false; ReducedMotion = false; AiModel = "" }
      Build = NotRequested
      Evidence = Map.empty
      Port = EnginePort.create PersistencePort.persistence
      Counter = 0
      ShellPending = Set.empty
      PageBase = ""
      CommandInputs = Map.empty
      SecretEpoch = 0
      PendingConfirm = None
      HistoryVersion = None
      TimelineFilter = "all"
      CatalogSort = "activity"
      TestHooks = false
      InjectedState = None }

let requestTimeoutMs = 15_000L

let private readBuildInfo (model: Model) =
    let corr = $"shell-{model.Counter + 1}"

    { model with Counter = model.Counter + 1; ShellPending = model.ShellPending.Add corr; Build = Loading },
    [ EffectRequest.Http
          { CorrelationId = CorrelationId corr
            Method = HttpMethod.Get
            Url = model.PageBase + "build-info.json?fresh=" + corr
            Headers = None
            Body = None
            TimeoutMs = requestTimeoutMs
            Response = Some HttpResponseKind.Text
            ResponseHeaders = None
            Credentials = Some HttpCredentials.Omit
            Xsrf = None } ]

/// Reads one published evidence file (S20/S21) once; the answer is kept as text and parsed by the screen module.
let private readEvidence (name: string) (model: Model) =
    if model.Evidence.ContainsKey name then model, []
    else
        let corr = $"shell-ev-{name}-{model.Counter + 1}"
        { model with Counter = model.Counter + 1; ShellPending = model.ShellPending.Add corr; Evidence = model.Evidence.Add(name, Loading) },
        [ EffectRequest.Http
              { CorrelationId = CorrelationId corr
                Method = HttpMethod.Get
                Url = model.PageBase + $"evidence/{name}.json?fresh=" + corr
                Headers = None
                Body = None
                TimeoutMs = requestTimeoutMs
                Response = Some HttpResponseKind.Text
                ResponseHeaders = None
                Credentials = Some HttpCredentials.Omit
                Xsrf = None } ]

/// The evidence the route needs, requested when it is first shown.
let private evidenceFor (model: Model) =
    match model.Route with
    | RequirementsProof -> readEvidence "proof" model
    | FactoryView ->
        let model, fx = readEvidence "factory" model
        let model, more = readEvidence "proof" model
        model, fx @ more
    | _ -> model, []

/// build-info.json is a flat object of strings plus a "components" object of strings; anything else is refused.
let parseBuildInfo (text: string) : Result<(string * string) list, string> =
    try
        use doc = Text.Json.JsonDocument.Parse text
        let root = doc.RootElement

        if root.ValueKind <> Text.Json.JsonValueKind.Object then Error "build-info.json is not an object"
        else
            let pairs =
                [ for p in root.EnumerateObject() do
                      match p.Value.ValueKind with
                      | Text.Json.JsonValueKind.String -> yield p.Name, p.Value.GetString()
                      | Text.Json.JsonValueKind.Object ->
                          for c in p.Value.EnumerateObject() do
                              if c.Value.ValueKind = Text.Json.JsonValueKind.String then yield $"{p.Name}.{c.Name}", c.Value.GetString()
                      | _ -> () ]

            Ok pairs
    with ex ->
        Error("build-info.json could not be read: " + ex.GetType().Name)

let private toPort (entropy: Entropy) (input: AdapterInput) (model: Model) =
    let port, effects = model.Port.Handle entropy input
    // Limen does not answer the engine's own navigation with LocationChanged: the route follows the push here.
    let route =
        effects
        |> List.tryPick (fun e ->
            match e with
            | EffectRequest.Navigation(NavigationEffectRequest.Push(_, url))
            | EffectRequest.Navigation(NavigationEffectRequest.Replace(_, url)) -> Some(Routes.parse url)
            | _ -> None)
    { model with Port = port; Route = route |> Option.defaultValue model.Route }, effects

/// Actions that destroy local work or cannot be undone in the record: each needs an explicit confirmation.
let confirmationFor (input: AdapterInput) : string option =
    match input with
    | DiscardLocal _ -> Some "Discard your local change to this investigation? It has not been saved to GitHub and cannot be recovered."
    | Command(_, "CloseInvestigation", _, _) -> Some "Close this investigation? A closed investigation accepts no further changes unless it is reopened."
    | Command(_, name, _, _) when name.StartsWith("TransitionHypothesis:", StringComparison.Ordinal) && name.EndsWith(":falsify", StringComparison.Ordinal) ->
        Some "Falsify this hypothesis? The falsification is recorded permanently in the investigation history."
    | Command(_, name, _, _) when name.StartsWith("RejectProposal:", StringComparison.Ordinal) ->
        Some "Reject this proposal? The rejection and your reason are recorded permanently."
    | _ -> None

/// Sends an input to the port, or holds it for confirmation when it is destructive.
let private guarded (entropy: Entropy) (input: AdapterInput) (model: Model) =
    match confirmationFor input with
    | Some text -> { model with PendingConfirm = Some(input, text); Notice = None }, []
    | None -> toPort entropy input model

let rec private onEvent (entropy: Entropy) (e: SemanticEvent) (model: Model) =
    let value = e.Value |> Option.defaultValue ""
    let d = model.Drafts

    match e.Name with
    | "token" -> { model with Drafts = { d with Token = value } }, []
    | "repository" -> { model with Drafts = { d with Repository = value.Trim() } }, []
    | "branch" -> { model with Drafts = { d with Branch = value.Trim() } }, []
    | "workspace" -> { model with Drafts = { d with Workspace = value.Trim() } }, []
    | "title" -> { model with Drafts = { d with Title = value } }, []
    | "statement" -> { model with Drafts = { d with Statement = value } }, []
    | "start" ->
        // The engine decides whether it may start and validates the fields; the shell only forwards them (UI-004-O6).
        let fields = Map.ofList [ "title", d.Title.Trim(); "problemStatement", d.Statement.Trim() ]
        toPort entropy (Command(None, "StartInvestigation", fields, 0L)) model
    | "connect" ->
        let request: ConnectRequest = { Token = d.Token; Repository = d.Repository; Branch = d.Branch; Workspace = d.Workspace }
        // The token leaves the shell's model as soon as it is handed to the port (memory only, OD-INDY-10).
        toPort entropy (Connect request) { model with Drafts = { d with Token = "" }; Notice = None; SecretEpoch = model.SecretEpoch + 1 }
    | "disconnect" -> toPort entropy Disconnect { model with Notice = Some { Tone = "info"; Text = "Disconnected. The token was forgotten." } }
    | "dismiss" -> { model with Notice = None }, []
    | "confirm-yes" ->
        match model.PendingConfirm with
        | Some(input, _) -> toPort entropy input { model with PendingConfirm = None; Notice = None }
        | None -> model, []
    | "confirm-no" -> { model with PendingConfirm = None; Notice = Some { Tone = "info"; Text = "Cancelled. Nothing was changed." } }, []
    | "check-remote" -> toPort entropy CheckRemote model
    | "discard-local"
    | "reload-remote" ->
        match model.Route with
        | Investigation(id, _) -> guarded entropy (if e.Name = "discard-local" then DiscardLocal id else ReloadRemote id) model
        | _ -> model, []
    // The graph frame reports the node the person selected; the shell only remembers which (UI-026-O3).
    | "graph-select" when value = "" || Routes.selection ("?item=" + value) = value -> { model with Selected = value }, []
    | "text-size" -> { model with Settings = { model.Settings with LargeText = (value = "large") } }, []
    | "motion" -> { model with Settings = { model.Settings with ReducedMotion = (value = "reduced") } }, []
    | "history-version" ->
        match Int64.TryParse value with
        | true, n when n >= 1L -> { model with HistoryVersion = Some n }, []
        | _ -> { model with HistoryVersion = None }, []
    | "catalog-sort" -> { model with CatalogSort = (if List.exists (fun (v, _) -> v = value) [ "activity", (); "title", (); "status", () ] then value else "activity") }, []
    | "proof-pick" ->
        let corr = $"shell-nav-{model.Counter + 1}"
        let url = Routes.formatItem RequirementsProof value
        { model with Counter = model.Counter + 1; Selected = value }, [ EffectRequest.Navigation(NavigationEffectRequest.Replace(CorrelationId corr, url)) ]
    | "timeline-filter" -> { model with TimelineFilter = (if value = "" then "all" else value) }, []
    | "ai-model" -> { model with Settings = { model.Settings with AiModel = value.Trim() } }, []
    | "ai-key" -> { model with Drafts = { d with AiKey = value } }, []
    | "ai-key-set" -> toPort entropy (SetAiKey d.AiKey) { model with Drafts = { d with AiKey = "" }; SecretEpoch = model.SecretEpoch + 1 }
    | "ai-key-clear" -> toPort entropy ClearAiKey model
    | "ai-task" -> { model with Drafts = { d with AiTask = value } }, []
    | "ai-request"
    | "ai-retry" ->
        match model.Route with
        | Investigation(id, _) -> toPort entropy (RequestProposals(id, model.Settings.AiModel, d.AiTask)) model
        | _ -> model, []
    // S17 / S18: the panel and the tournament use the same key, model setting and task as single requests.
    | "panel-request" ->
        match model.Route with
        | Investigation(id, _) -> toPort entropy (RequestPanel(id, model.Settings.AiModel, d.AiTask)) model
        | _ -> model, []
    | "tournament-request" ->
        match model.Route with
        | Investigation(id, _) ->
            let input k = model.CommandInputs |> Map.tryFind ("Tournament." + k) |> Option.defaultValue ""
            let treatments =
                [ input "modelA", input "promptA"; input "modelB", input "promptB" ]
                |> List.filter (fun (m, _) -> m.Trim() <> "")
                |> List.map (fun (m, v) -> m, (if v = "" then "causa.proposal-prompt/1" else v))
            toPort entropy (RequestTournament(id, treatments, d.AiTask)) model
        | _ -> model, []
    | "ai-cancel" -> toPort entropy CancelProposals model
    | "ai-discard" -> toPort entropy DiscardDrafts model
    | "ai-record" ->
        match model.Route with
        | Investigation(id, _) -> toPort entropy (RecordDrafts(id, model.Settings.AiModel)) model
        | _ -> model, []
    | "proposal-reason" ->
        match e.Key with
        | Some k -> { model with CommandInputs = model.CommandInputs.Add("RejectProposal:" + k, value) }, []
        | None -> model, []
    | "proposal-accept"
    | "proposal-reject" ->
        match e.Key with
        | Some k -> onEvent entropy { e with Name = "command"; Key = Some((if e.Name = "proposal-accept" then "AcceptProposal:" else "RejectProposal:") + k) } model
        | None -> model, []
    // Multi-field forms: "field-<Form>.<field>" keeps the typed value; "submit-<Form>" sends the command with them.
    | name when name.StartsWith("field-", StringComparison.Ordinal) ->
        { model with CommandInputs = model.CommandInputs.Add(name.Substring 6, value) }, []
    | name when name.StartsWith("submit-", StringComparison.Ordinal) ->
        let form = name.Substring 7
        let fields =
            model.CommandInputs
            |> Map.toList
            |> List.choose (fun (k, v) -> if k.StartsWith(form + ".", StringComparison.Ordinal) then Some(k.Substring(form.Length + 1), v) else None)
            |> Map.ofList
            // The record a detail screen shows (?item=) is the subject of its forms (S04, S13).
            |> fun f -> if model.Selected = "" || f.ContainsKey "item" then f else f.Add("item", model.Selected)
        let command =
            if form = "TransitionHypothesis" then
                $"""TransitionHypothesis:{fields |> Map.tryFind "hypothesis" |> Option.defaultValue ""}:{fields |> Map.tryFind "change" |> Option.defaultValue ""}"""
            elif form = "FalsifyHypothesis" then $"TransitionHypothesis:{model.Selected}:falsify"
            elif form = "ReopenHypothesis" then $"TransitionHypothesis:{model.Selected}:reopen"
            else form
        match model.Route, model.Route |> (function Investigation(id, _) -> model.Port.Projection.Opened.TryFind id | _ -> None) with
        | Investigation(id, _), Some(Loaded inv) ->
            let cleared = model.CommandInputs |> Map.filter (fun k _ -> not (k.StartsWith(form + ".", StringComparison.Ordinal)) || k.EndsWith(".hypothesis") || k.EndsWith(".evidence"))
            guarded entropy (Command(Some id, command, fields, inv.Version)) { model with CommandInputs = cleared }
        | _ -> { model with Notice = Some { Tone = "warning"; Text = "That action is not available here." } }, []
    | "command-input" ->
        match e.Key with
        | Some capability -> { model with CommandInputs = model.CommandInputs.Add(capability, value) }, []
        | None -> model, []
    | "command" ->
        // A capability the engine offered for the version this view was rendered from (UI-004-O4).
        let version =
            match model.Route with
            | Investigation(id, _) ->
                match model.Port.Projection.Opened.TryFind id with
                | Some(Loaded inv) -> Some inv.Version
                | _ -> None
            | _ -> None

        match e.Key, version with
        | Some capability, Some v ->
            let fields = model.CommandInputs |> Map.tryFind capability |> Option.map (fun t -> Map.ofList [ "text", t ]) |> Option.defaultValue Map.empty
            let investigation = match model.Route with Investigation(id, _) -> Some id | _ -> None
            guarded entropy (Command(investigation, capability, fields, v)) { model with CommandInputs = model.CommandInputs.Remove capability }
        | _ -> { model with Notice = Some { Tone = "warning"; Text = "That action is not available here." } }, []
    | _ -> model, []

/// Opening an investigation route asks the port for it (UI-001-O6: identity comes from the route).
let private openRoute (entropy: Entropy) (model: Model) =
    match model.Route with
    | Investigation(id, _) when not (model.Port.Projection.Opened.ContainsKey id) -> toPort entropy (Open id) model
    | _ -> model, []

let update (entropy: Entropy) (message: BrowserToEngineMessage) (model: Model) : Model * EffectRequest list =
    match message with
    | BrowserToEngineMessage.Initialize(_, location, _) ->
        let dir =
            match location.Path.LastIndexOf '/' with
            | -1 -> "/"
            | i -> location.Path.Substring(0, i + 1)

        let model, fx = readBuildInfo { model with Route = Routes.parse location.Hash; Selected = Routes.selection location.Hash; HistoryVersion = Routes.versionOf location.Hash; PageBase = location.Origin + dir }
        // Restore this device's work first, so it shows before Connect (persistence's Boot).
        let model, boot = toPort entropy Boot model
        let model, more = openRoute entropy model
        let model, ev = evidenceFor model
        model, fx @ boot @ more @ ev
    | BrowserToEngineMessage.LocationChanged location ->
        let model, fx = openRoute entropy { model with Route = Routes.parse location.Hash; Selected = Routes.selection location.Hash; HistoryVersion = Routes.versionOf location.Hash; Notice = None; PendingConfirm = None }
        let model, ev = evidenceFor model
        model, fx @ ev
    | BrowserToEngineMessage.Event e -> onEvent entropy e model
    | BrowserToEngineMessage.EffectResult(EffectResult.HttpResult(CorrelationId corr, outcome)) when model.ShellPending.Contains corr && corr.StartsWith("shell-ev-", StringComparison.Ordinal) ->
        let name = corr.Split('-')[2]
        let loaded =
            match outcome with
            | EffectOutcome.Success(200L, RawJson body, _) ->
                Loaded(try Text.Json.JsonSerializer.Deserialize<string>(body: string) with _ -> body)
            | EffectOutcome.Success(status, _, _) -> Failed $"evidence/{name}.json answered HTTP {status}: it was not published with this build"
            | _ -> Failed $"evidence/{name}.json could not be reached"
        { model with ShellPending = model.ShellPending.Remove corr; Evidence = model.Evidence.Add(name, loaded) }, []
    | BrowserToEngineMessage.EffectResult(EffectResult.HttpResult(CorrelationId corr, outcome)) when model.ShellPending.Contains corr ->
        let model = { model with ShellPending = model.ShellPending.Remove corr }

        let build =
            match outcome with
            | EffectOutcome.Success(200L, RawJson body, _) ->
                // The kernel delivers a text response as a JSON string literal.
                let text =
                    try
                        Text.Json.JsonSerializer.Deserialize<string>(body: string)
                    with _ ->
                        body

                match parseBuildInfo text with
                | Ok pairs -> Loaded pairs
                | Error e -> Failed e
            | EffectOutcome.Success(status, _, _) -> Failed $"build-info.json answered HTTP {status}"
            | _ -> Failed "build-info.json could not be reached"

        { model with Build = build }, []
    | BrowserToEngineMessage.EffectResult(EffectResult.HttpResult(CorrelationId corr, _) as result) -> toPort entropy (Answer(corr, result)) model
    | BrowserToEngineMessage.EffectResult(EffectResult.StorageResult(CorrelationId corr, _) as result) -> toPort entropy (Answer(corr, result)) model
    | BrowserToEngineMessage.EffectResult _ -> model, []
    | BrowserToEngineMessage.CapabilityFact _ -> model, []
