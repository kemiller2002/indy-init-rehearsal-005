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
    | [| "ConfirmCause"; id |] ->
        match Guid.TryParse id with
        | true, g -> Ok(ConfirmCause(Id.ofGuid g))
        | _ -> Error "That cause is not in this investigation."
    | _ -> Error "That action is not available here."

/// The workspace projection, re-derived from persistence's sessions at `at` (one version per investigation).
let private project (at: DateTimeOffset) (p: Persistence) (message: (string * string) option) : A.WorkspaceProjection =
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
      Administration =
        p.Administration
        @ [ { Id = "engine"; Section = "persistence"; Label = "Investigation engine"; Value = "Causa kernel (F#), running in this page"; State = "Reported by the running application" } ] }

let rec private portOf (at: DateTimeOffset) (p: Persistence) (message: (string * string) option) : A.Port =
    { Name = "engine/" + p.Name
      Projection = project at p message
      Handle = fun entropy input -> handle entropy input p }

and private handle (entropy: A.Entropy) (input: A.AdapterInput) (p: Persistence) : A.Port * EffectRequest list =
    let at = truncated entropy.Now
    let pass i = let p, fx = p.Handle entropy i in portOf at p None, fx

    match input with
    | A.Connect r -> pass (Connect r)
    | A.Disconnect -> pass Disconnect
    | A.Open id -> pass (Open id)
    | A.Answer(corr, result) -> pass (Answer(corr, result))
    | A.CheckRemote -> pass CheckRemote
    | A.Command(investigation, name, fields, expected) ->
        let session, expectedVersion, id =
            match investigation with
            | Some id ->
                match p.Documents.TryFind id with
                | Some(A.Loaded d) -> Some d.Session, Some expected, id
                | _ -> None, Some expected, id
            | None -> Some Kernel.empty, None, (entropy.Ids[1]).ToString("D")

        match session, commandFor entropy name fields with
        | None, _ -> portOf at p (Some("warning", "That investigation is not open.")), []
        | _, Error reason -> portOf at p (Some("warning", reason)), []
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
                portOf at p (Some("info", $"Recorded ({n} change(s), version {Kernel.version accepted.Session}). Not saved yet.")), fx @ go
            | Error rejection ->
                // Nothing changes on a rejection; the engine's reasons are shown as they are (safe messages).
                portOf at p (Some("warning", "Not recorded: " + String.concat " " (Kernel.reasons rejection))), []

let create (p: Persistence) : A.Port = portOf DateTimeOffset.UnixEpoch p None
