/// The application shell: route, navigation, the Connect form, notices, session settings and the build identity.
/// Investigation meaning is delegated to the Port (Adapter.fs). `update` is pure given its Entropy.
module Causa.App.Shell

open System
open Limen.Contract
open Limen.Contract.Core
open Causa.App.Routes
open Causa.App.Adapter

type Notice = { Tone: string; Text: string }

type Drafts = { Token: string; Repository: string; Branch: string; Workspace: string; Title: string; Statement: string }

/// Session-scope preferences (UI-019-O5): kept in this page's memory only, never in browser storage.
type Settings = { LargeText: bool; ReducedMotion: bool }

type Model =
    { Route: Route
      Selected: string
      Drafts: Drafts
      Notice: Notice option
      Settings: Settings
      /// build-info.json as served next to the page (VER-007-O2: what is actually deployed).
      Build: Loadable<(string * string) list>
      Port: Port
      Counter: int
      /// Correlation ids of requests the shell itself made; every other answer belongs to the port.
      ShellPending: Set<string>
      PageBase: string
      /// Text typed for a capability that needs one input, by capability id.
      CommandInputs: Map<string, string>
      /// Only the test build enables this (Percepta state injection).
      TestHooks: bool
      /// The Percepta fixture state name currently shown, if any.
      InjectedState: string option }

let initialDrafts = { Token = ""; Repository = ""; Branch = "main"; Workspace = ""; Title = ""; Statement = "" }

let initial =
    { Route = Home
      Selected = ""
      Drafts = initialDrafts
      Notice = None
      Settings = { LargeText = false; ReducedMotion = false }
      Build = NotRequested
      Port = EnginePort.create PersistencePort.persistence
      Counter = 0
      ShellPending = Set.empty
      PageBase = ""
      CommandInputs = Map.empty
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

let private onEvent (entropy: Entropy) (e: SemanticEvent) (model: Model) =
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
        toPort entropy (Connect request) { model with Drafts = { d with Token = "" }; Notice = None }
    | "disconnect" -> toPort entropy Disconnect { model with Notice = Some { Tone = "info"; Text = "Disconnected. The token was forgotten." } }
    | "dismiss" -> { model with Notice = None }, []
    | "check-remote" -> toPort entropy CheckRemote model
    // The graph frame reports the node the person selected; the shell only remembers which (UI-026-O3).
    | "graph-select" when value = "" || Routes.selection ("?item=" + value) = value -> { model with Selected = value }, []
    | "text-size" -> { model with Settings = { model.Settings with LargeText = (value = "large") } }, []
    | "motion" -> { model with Settings = { model.Settings with ReducedMotion = (value = "reduced") } }, []
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
            toPort entropy (Command(investigation, capability, fields, v)) { model with CommandInputs = model.CommandInputs.Remove capability }
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

        let model, fx = readBuildInfo { model with Route = Routes.parse location.Hash; Selected = Routes.selection location.Hash; PageBase = location.Origin + dir }
        let model, more = openRoute entropy model
        model, fx @ more
    | BrowserToEngineMessage.LocationChanged location ->
        openRoute entropy { model with Route = Routes.parse location.Hash; Selected = Routes.selection location.Hash; Notice = None }
    | BrowserToEngineMessage.Event e -> onEvent entropy e model
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
