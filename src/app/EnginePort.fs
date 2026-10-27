/// The Port the shell drives in production: the core kernel (Boundaries A, B, E) over a persistence program.
///
/// Persistence (stream persist's outer Sync program: connection, loading, catalog, saves) is reached only through
/// the thin `Persistence` record below until it lands on main; the shell never drives core's Sync directly.
/// Commands: the shell names a capability the engine offered; this module builds the typed command, decides it with
/// `Kernel.submit` at the version the view was rendered from, and hands accepted events to persistence. A rejection
/// changes nothing and is reported with the engine's own reasons (UI-004-O4).
module Causa.App.EnginePort

open System
open Limen.Contract.Core
open IndyInitRootCauseInvestigator.Engine
module A = Causa.App.Adapter

/// One investigation persistence holds: its authoritative session and how it is saved.
type Document = { Session: Session; Writable: bool; Integrity: DataIntegrity option; Sync: A.SyncStatus }

type PersistenceInput =
    | Connect of A.ConnectRequest
    | Disconnect
    | Open of id: string
    /// Accepted by the kernel: the new session and exactly the events it produced, decided at `expected`.
    | Accepted of id: string * expected: int64 option * session: Session * events: DomainEvent list
    | Answer of correlation: string * EffectResult
    /// Re-read the branch to settle a save whose outcome is unknown (the shell's "check now").
    | CheckRemote

/// The thin persistence interface (until stream persist's program lands).
type Persistence =
    { Name: string
      Connection: A.Connection
      /// The workspace's investigations, in catalog order.
      Catalog: A.Loadable<string list>
      Documents: Map<string, A.Loadable<Document>>
      Message: (string * string) option
      Administration: A.AdminFact list
      Handle: A.Entropy -> PersistenceInput -> Persistence * EffectRequest list }

let human: Actor = { ActorId = "causa-user"; Kind = Human }

module Persistence =
    /// Production stand-in until the persistence program is composed in: refuses Connect, claims nothing.
    let rec unavailable: Persistence =
        { Name = "unavailable"
          Connection = A.NotConnected
          Catalog = A.NotRequested
          Documents = Map.empty
          Message = None
          Administration =
            [ { Id = "provider"; Section = "persistence"; Label = "Data provider"; Value = "Not composed into this build"; State = "Unavailable" } ]
          Handle =
            fun _ input ->
                match input with
                | Connect _ ->
                    { unavailable with
                        Connection = A.ConnectionFailed "Connecting to a data repository is not available in this build yet. Nothing was sent."
                        Message = Some("warning", "Connecting is not available in this build yet. Nothing was sent and nothing was stored.") },
                    []
                | _ -> unavailable, [] }

    /// Test build only: an in-memory workspace in this tab. Nothing is saved anywhere, and it says so.
    let rec memory (docs: Map<string, A.Loadable<Document>>) (order: string list) : Persistence =
        { Name = "memory"
          Connection = A.ConnectedTo("this tab only (test build)", "none", "not saved")
          Catalog = A.Loaded order
          Documents = docs
          Message = None
          Administration =
            [ { Id = "provider"; Section = "persistence"; Label = "Data provider"; Value = "In-memory test workspace; nothing is saved"; State = "Unavailable" } ]
          Handle =
            fun _ input ->
                match input with
                | Accepted(id, _, session, events) ->
                    let unsaved =
                        match docs.TryFind id with
                        | Some(A.Loaded d) -> (match d.Sync with A.Unsaved n -> n | _ -> 0)
                        | _ -> 0

                    let doc = { Session = session; Writable = true; Integrity = None; Sync = A.Unsaved(unsaved + events.Length) }
                    memory (docs.Add(id, A.Loaded doc)) (if List.contains id order then order else order @ [ id ]), []
                | _ -> memory docs order, [] }

let private truncated (t: DateTime) =
    let o = DateTimeOffset(t, TimeSpan.Zero)
    o.AddTicks(-(o.Ticks % TimeSpan.TicksPerMillisecond))

let private text (fields: Map<string, string>) key = fields |> Map.tryFind key |> Option.defaultValue ""

/// The typed command for a capability name the engine offered, with fresh ids from entropy.
let commandFor (entropy: A.Entropy) (name: string) (fields: Map<string, string>) : Result<Command, string> =
    let fresh i = entropy.Ids[i]
    let t = text fields "text"

    match name.Split(':') with
    | [| "StartInvestigation" |] -> Ok(StartInvestigation(text fields "title", Id.ofGuid (fresh 2), text fields "problemStatement"))
    | [| "ActivateInvestigation" |] -> Ok ActivateInvestigation
    | [| "MarkAwaitingEvidence" |] -> Ok(MarkAwaitingEvidence t)
    | [| "MarkAwaitingVerification" |] -> Ok MarkAwaitingVerification
    | [| "ResolveInvestigation" |] -> Ok ResolveInvestigation
    | [| "CloseInvestigation" |] -> Ok CloseInvestigation
    | [| "ReopenInvestigation" |] -> Ok(ReopenInvestigation t)
    | [| "RecordObservation" |] -> Ok(RecordObservation(Id.ofGuid (fresh 2), t, None))
    | [| "RecordHypothesis" |] -> Ok(RecordHypothesis(Id.ofGuid (fresh 2), t, None))
    | [| "IdentifyUnknown" |] -> Ok(IdentifyUnknown(Id.ofGuid (fresh 2), t, Material))
    | [| "AcceptProposal"; id |] ->
        match Guid.TryParse id with
        | true, g -> Ok(AcceptProposal(Id.ofGuid g, fresh 2))
        | _ -> Error "That proposal is not in this investigation."
    | [| "RejectProposal"; id |] ->
        match Guid.TryParse id with
        | true, g -> Ok(RejectProposal(Id.ofGuid g, t))
        | _ -> Error "That proposal is not in this investigation."
    | [| "ConfirmCause"; id |] ->
        match Guid.TryParse id with
        | true, g -> Ok(ConfirmCause(Id.ofGuid g))
        | _ -> Error "That cause is not in this investigation."
    | _ -> Error "That action is not available here."

/// The AI proposal loop's state (T27). The key is held here, in memory, and only AiLoop puts it in a header.
type Ai =
    { Loop: AiLoopState
      Key: ProviderKey option
      Investigation: string option
      Result: AiResult option
      Task: string
      Model: string
      LastRequest: string option }

let noAi = { Loop = AiLoop.initial; Key = None; Investigation = None; Result = None; Task = ""; Model = ""; LastRequest = None }

let private kindText =
    function
    | ProposedHypothesis -> "Hypothesis"
    | ProposedUnknown -> "Open question"
    | ProposedAction -> "Action"

let private resultTone (r: AiResult) =
    match r with
    | Drafted _ -> "info"
    | KeyMissing
    | KeyRejected
    | NotPermitted _
    | ModelUnavailable _
    | SpendCapReached _
    | RequestRejected _ -> "danger"
    | _ -> "warning"

let private currentVersion (p: Persistence) (id: string) =
    match p.Documents.TryFind id with
    | Some(A.Loaded d) -> d.Session.State |> Option.map (fun i -> i.Version)
    | _ -> None

let private aiView (p: Persistence) (ai: Ai) : A.AiView =
    let drafts, stale =
        match ai.Result, ai.Investigation with
        | Some(Drafted(ds, contextVersion, _) as r), Some id ->
            let rows = ds |> List.map (fun d -> ({ Kind = kindText d.Kind; Text = d.Text; Rationale = d.Rationale }: A.DraftRow))
            let stale =
                match currentVersion p id with
                | Some v when AiLoop.isStale v r -> Some $"Stale: drafted from version {contextVersion}; the investigation is now at version {v}. Review them against the current state."
                | _ -> None
            rows, stale
        | _ -> [], None

    { KeyHeld = ai.Key.IsSome
      InFlight = ai.Loop.InFlight.IsSome
      Investigation = ai.Investigation
      Result = ai.Result |> Option.map (fun r -> AiLoop.describe r, resultTone r, AiLoop.retryable r)
      Drafts = drafts
      Stale = stale
      LastRequest = ai.LastRequest }

/// The provider call as a Limen Http effect, exactly as AiLoop built it.
let private httpOf (e: AiHttpEffect) : EffectRequest =
    EffectRequest.Http
        { CorrelationId = CorrelationId($"ai-{e.Request}")
          Method = HttpMethod.Post
          Url = e.Url
          Headers = Some(Map.ofList e.Headers)
          Body = Some e.Body
          TimeoutMs = int64 e.TimeoutMs
          Response = Some HttpResponseKind.Text
          ResponseHeaders = Some [ "retry-after" ]
          Credentials = Some HttpCredentials.Omit
          Xsrf = None }

/// Limen's outcome for request `n` as AiLoop's outcome. A text response arrives as a JSON string literal.
let private outcomeOf (n: int64) (result: EffectResult) : AiHttpOutcome option =
    match result with
    | EffectResult.HttpResult(_, outcome) ->
        match outcome with
        | EffectOutcome.Success(status, Limen.Contract.RawJson raw, headers) ->
            let body =
                try
                    Text.Json.JsonSerializer.Deserialize<string>(raw: string)
                with _ ->
                    raw
            let hs = headers |> Option.map Map.toList |> Option.defaultValue []
            Some(Responded(n, int status, hs, body))
        | EffectOutcome.Failure(HttpFailureReason.Aborted, _) -> Some(TimedOut n)
        | EffectOutcome.Failure(_, _) -> Some(NetworkFailed n)
        | EffectOutcome.Cancelled -> Some(Cancelled n)
        | EffectOutcome.OutcomeUnknown _ -> Some(OutcomeUnknown n)
    | _ -> None

/// The workspace projection, re-derived from persistence's sessions at `at` (one version per investigation).
let private project (at: DateTimeOffset) (p: Persistence) (message: (string * string) option) (ai: A.AiView) : A.WorkspaceProjection =
    let projectionOf (d: Document) =
        d.Session.State |> Option.map (fun inv -> Projection.ofInvestigation human at d.Integrity inv)

    let opened =
        p.Documents
        |> Map.map (fun _ l ->
            match l with
            | A.Loaded d ->
                match projectionOf d with
                | Some proj -> A.Loaded(EngineView.ofProjection d.Writable d.Session.History proj)
                | None -> A.Failed "This investigation has no recorded history."
            | A.Loading -> A.Loading
            | A.NotRequested -> A.NotRequested
            | A.Failed r -> A.Failed r)

    let cards =
        match p.Catalog with
        | A.Loaded ids ->
            A.Loaded(
                ids
                |> List.choose (fun id ->
                    match p.Documents.TryFind id with
                    | Some(A.Loaded d) -> projectionOf d |> Option.map (fun proj -> EngineView.card d.Writable proj d.Session.History)
                    | _ -> None)
            )
        | A.Loading -> A.Loading
        | A.NotRequested -> A.NotRequested
        | A.Failed r -> A.Failed r

    let sync =
        p.Documents |> Map.toList |> List.tryPick (fun (_, l) -> match l with A.Loaded d when d.Sync <> A.NoChanges -> Some d.Sync | _ -> None)

    let connected = match p.Connection with A.ConnectedTo _ -> true | _ -> false

    { Connection = p.Connection
      Investigations = cards
      Opened = opened
      Sync = sync |> Option.defaultValue A.NoChanges
      // Starting needs no engine state; it is offered only where persistence can hold the result.
      Capabilities =
        [ { Id = "StartInvestigation"; Name = "StartInvestigation"; Input = None; Label = "Start the investigation"
            Legal = connected
            Reason = (if connected then None else Some "Connect a data repository first.")
            BlockerId = None } ]
      Blockers = []
      Message = (match message with Some m -> Some m | None -> p.Message)
      Ai = ai
      Administration =
        p.Administration
        @ [ { Id = "engine"; Section = "persistence"; Label = "Investigation engine"; Value = "Causa kernel (F#), running in this page"; State = "Reported by the running application" } ] }

let rec private portOf (at: DateTimeOffset) (p: Persistence) (message: (string * string) option) (ai: Ai) : A.Port =
    { Name = "engine/" + p.Name
      Projection = project at p message (aiView p ai)
      History =
        fun id version ->
            match p.Documents.TryFind id with
            | Some(A.Loaded d) -> EngineView.history d.Session.History version
            | _ -> None
      Handle = fun entropy input -> handle entropy input p ai }

and private handle (entropy: A.Entropy) (input: A.AdapterInput) (p: Persistence) (ai: Ai) : A.Port * EffectRequest list =
    let at = truncated entropy.Now
    let pass i = let p, fx = p.Handle entropy i in portOf at p None ai, fx
    let say tone text = portOf at p (Some(tone, text)) ai, []

    let requestProposals (id: string) (model: string) (task: string) =
        match p.Documents.TryFind id with
        | Some(A.Loaded { Session = { State = Some inv } }) when not (String.IsNullOrWhiteSpace model) && not (String.IsNullOrWhiteSpace task) ->
            match AiLoop.start ai.Loop ai.Key (AiLoop.anthropic (model.Trim())) inv task with
            | Ok(loop, effect) ->
                let logged = AiLoop.redacted effect
                let line = $"""{logged.Method} {logged.Url} · headers: {logged.Headers |> List.map (fun (h, v) -> h + "=" + v) |> String.concat ", "}"""
                portOf at p None { ai with Loop = loop; Investigation = Some id; Result = None; Task = task; Model = model.Trim(); LastRequest = Some line }, [ httpOf effect ]
            | Error result -> portOf at p None { ai with Investigation = Some id; Result = Some result }, []
        | Some(A.Loaded _) when String.IsNullOrWhiteSpace model -> say "warning" "Set the AI model id in Settings first; there is no default model."
        | Some(A.Loaded _) -> say "warning" "Describe what the assistant should propose."
        | _ -> say "warning" "That investigation is not open."

    match input with
    | A.Connect r -> pass (Connect r)
    | A.Disconnect -> pass Disconnect
    | A.Open id -> pass (Open id)
    | A.CheckRemote -> pass CheckRemote
    | A.SetAiKey key ->
        match ProviderKey.Create key with
        | Some k -> portOf at p (Some("info", "The key is held in this page's memory only. Reloading forgets it.")) { ai with Key = Some k; Result = None }, []
        | None -> say "warning" "That key is empty or contains a line break; it was not kept."
    | A.ClearAiKey -> portOf at p (Some("info", "The key was forgotten.")) { ai with Key = None }, []
    | A.RequestProposals(id, model, task) -> requestProposals id model task
    | A.CancelProposals -> portOf at p None { ai with Loop = AiLoop.cancel ai.Loop; Result = Some CancelledResult }, []
    | A.DiscardDrafts -> portOf at p None { ai with Result = None }, []
    | A.RecordDrafts(id, model) ->
        match ai.Result, p.Documents.TryFind id with
        | Some(Drafted(drafts, _, _)), Some(A.Loaded d) when ai.Investigation = Some id ->
            let version = d.Session.State |> Option.map (fun i -> i.Version) |> Option.defaultValue 0L
            let ids = entropy.Ids |> List.chunkBySize 2 |> List.filter (fun c -> c.Length = 2) |> List.map (fun c -> c[0], c[1])
            if ids.Length < drafts.Length then say "warning" "Too many proposals to record at once."
            else
                let agent: AgentIdentity = { AgentId = "causa-ai"; Provider = "anthropic"; Model = (if model = "" then ai.Model else model) }
                let envelopes = Proposals.commands agent (Id.ofGuid (Guid.Parse id)) version at (ids |> List.truncate drafts.Length) drafts
                // Each envelope goes through Kernel.submit; recording stops at the first refusal and nothing after it is sent.
                let rec submit (session: Session) (events: DomainEvent list) (envs: CommandEnvelope list) =
                    match envs with
                    | [] -> Ok(session, events)
                    | e :: rest ->
                        match Kernel.submit session e with
                        | Ok a -> submit a.Session (events @ a.Events) rest
                        | Error r -> Error r
                match submit d.Session [] envelopes with
                | Ok(session, events) ->
                    let p, fx = p.Handle entropy (Accepted(id, Some version, session, events))
                    portOf at p (Some("info", $"{drafts.Length} proposal(s) recorded for your review. None is applied until you accept it. Not saved yet.")) { ai with Result = None }, fx
                | Error rejection -> say "warning" ("The proposals were not recorded: " + String.concat " " (Kernel.reasons rejection))
        | _ -> say "warning" "There are no drafted proposals to record."
    | A.Answer(corr, result) when corr.StartsWith("ai-", StringComparison.Ordinal) ->
        match Int64.TryParse(corr.Substring 3) with
        | true, n ->
            match outcomeOf n result with
            | Some outcome ->
                match AiLoop.receive ai.Loop outcome with
                | Ok(loop, r) -> portOf at p None { ai with Loop = loop; Result = Some r }, []
                // A late answer for a cancelled or replaced request changes nothing.
                | Error _ -> portOf at p None ai, []
            | None -> portOf at p None ai, []
        | _ -> portOf at p None ai, []
    | A.Answer(corr, result) -> pass (Answer(corr, result))
    | A.Command(investigation, name, fields, expected) ->
        let session, expectedVersion, id =
            match investigation with
            | Some id ->
                match p.Documents.TryFind id with
                | Some(A.Loaded d) -> Some d.Session, Some expected, id
                | _ -> None, Some expected, id
            | None -> Some Kernel.empty, None, (entropy.Ids[1]).ToString("D")

        match session, commandFor entropy name fields with
        | None, _ -> say "warning" "That investigation is not open."
        | _, Error reason -> say "warning" reason
        | Some s, Ok command ->
            let envelope =
                { CommandId = entropy.Ids[0]
                  Investigation = Id.ofGuid (Guid.Parse id)
                  ExpectedVersion = expectedVersion
                  Actor = human
                  RequestedAt = at
                  Command = command }

            match Kernel.submit s envelope with
            | Ok accepted ->
                let p, fx = p.Handle entropy (Accepted(id, expectedVersion, accepted.Session, accepted.Events))
                let n = accepted.Events.Length
                // A started investigation is opened at once (its identity is the route, UI-001-O6).
                let go =
                    if investigation.IsNone then
                        [ EffectRequest.Navigation(NavigationEffectRequest.Push(CorrelationId("start-" + id), Causa.App.Routes.format (Causa.App.Routes.Investigation(id, Causa.App.Routes.Overview)))) ]
                    else []
                portOf at p (Some("info", $"Recorded ({n} change(s), version {Kernel.version accepted.Session}). Not saved yet.")) ai, fx @ go
            | Error rejection ->
                // Nothing changes on a rejection; the engine's reasons are shown as they are (safe messages).
                say "warning" ("Not recorded: " + String.concat " " (Kernel.reasons rejection))

let create (p: Persistence) : A.Port = portOf DateTimeOffset.UnixEpoch p None noAi
