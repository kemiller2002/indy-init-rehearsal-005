namespace IndyInitRootCauseInvestigator.Engine

open System
open Aegis

/// One loaded aggregate: its authoritative history and the state it folds to.
type Session =
    { History: DomainEvent list
      State: Investigation option }

/// Boundary A success: the semantic events the command produced and the new session (new version).
type Accepted =
    { Events: DomainEvent list
      Session: Session }

/// Boundary A typed rejection. Nothing changes on a rejection (AR-006-O2: rejected before side effects).
type Rejection =
    /// The caller decided against a version that is no longer current (UI-004-O4).
    | StaleVersion of expected: int64 * current: int64
    /// The command id was already applied: a duplicate submission is not applied twice.
    | DuplicateCommand of commandId: Guid
    /// The actor may not issue this command (D-004: agents only propose; only humans accept proposals).
    | Unauthorized of DomainError
    /// Structurally illegal or blocked by policy, with every reason (R8.2).
    | Refused of DomainError list
    /// A value with no v1 wire form (contract §2.9: sub-millisecond instant; §2.3: U+FEFF).
    | NotRepresentable of reason: string

/// The authoritative kernel facade (Boundary A). Pure: the caller supplies ids, time and actor in the envelope.
[<RequireQualifiedAccess>]
module Kernel =
    let empty: Session = { History = []; State = None }

    /// A session from a verified load (Boundary C output).
    let ofLoaded (loaded: LoadedInvestigation) : Session =
        { History = loaded.Events
          State = Some loaded.State }

    /// Replay a history through the engine: a session, or the typed admissibility violations.
    let replay (history: DomainEvent list) : Result<Session, DomainError list> =
        match History.validate history with
        | [] -> Ok { History = history; State = Evolve.replay history }
        | errors -> Error errors

    let version (session: Session) =
        session.State |> Option.map (fun s -> s.Version) |> Option.defaultValue 0L

    /// The command boundary de-duplicates cited ids before deciding (contract §2.7: no v1 wire form otherwise).
    let normalize (command: Command) : Command =
        match command with
        | TransitionHypothesis(h, c, r, ev) -> TransitionHypothesis(h, c, r, List.distinct ev)
        | RecordContradiction(c, s, ev, m) -> RecordContradiction(c, s, List.distinct ev, m)
        | RecordAction(a, k, p, t) -> RecordAction(a, k, p, List.distinct t)
        | EstablishNegativeKnowledge(n, s, sc, b) -> EstablishNegativeKnowledge(n, s, sc, List.distinct b)
        | RecordCause(c, o, members, m) -> RecordCause(c, o, List.distinct members, m)
        | RecordCorrectiveAction(c, k, d, t) -> RecordCorrectiveAction(c, k, d, List.distinct t)
        | RecordVerification(v, c, o, ev) -> RecordVerification(v, c, o, List.distinct ev)
        | RecordConclusion(c, s, st, causes, r) -> RecordConclusion(c, s, st, List.distinct causes, r)
        | other -> other

    let private isAuthority (e: DomainError) =
        e.Code = "AGENT.PROPOSAL_ONLY" || e.Code = "PROPOSAL.AGENT_CANNOT_ACCEPT"

    /// Submit one command: idempotency, version, authority, structure, policy, then wire representability.
    let submit (session: Session) (env: CommandEnvelope) : Result<Accepted, Rejection> =
        let current = version session
        if session.History |> List.exists (fun e -> e.CommandId = env.CommandId) then
            Error(DuplicateCommand env.CommandId)
        else
            match env.ExpectedVersion with
            | Some expected when expected <> current -> Error(StaleVersion(expected, current))
            | _ ->
                if not (Wire.isMillisecond env.RequestedAt) then
                    Error(NotRepresentable "the command time has sub-millisecond precision; supply millisecond-truncated UTC")
                else
                    let env = { env with Command = normalize env.Command }
                    match Decide.execute session.State env with
                    | Error errors ->
                        match errors |> List.tryFind isAuthority with
                        | Some authority -> Error(Unauthorized authority)
                        | None -> Error(Refused errors)
                    | Ok events ->
                        match events |> List.tryPick Wire.unrepresentable with
                        | Some reason -> Error(NotRepresentable reason)
                        | None ->
                            let history = session.History @ events
                            Ok
                                { Events = events
                                  Session =
                                    { History = history
                                      State = events |> List.fold Evolve.apply session.State } }

    /// A stable code per rejection, for the projection and for faults.
    let code (rejection: Rejection) =
        match rejection with
        | StaleVersion _ -> "VERSION.STALE"
        | DuplicateCommand _ -> "COMMAND.DUPLICATE"
        | Unauthorized e -> e.Code
        | Refused(e :: _) -> e.Code
        | Refused [] -> "COMMAND.REFUSED"
        | NotRepresentable _ -> "CAUSA.UNREPRESENTABLE"

    /// Safe, human-readable explanations (no secrets ever enter the domain).
    let reasons (rejection: Rejection) : string list =
        match rejection with
        | StaleVersion(expected, current) -> [ $"The investigation changed (you saw version {expected}, it is now {current}); reload and decide again." ]
        | DuplicateCommand _ -> [ "This command was already applied; it is not applied twice." ]
        | Unauthorized e -> [ e.Message ]
        | Refused errors -> errors |> List.map (fun e -> e.Message)
        | NotRepresentable reason -> [ reason ]

/// Aegis-compatible faults at the engine boundary (DEC-026, R8.3): a rejection becomes a typed fault, never a hidden
/// exception or generic text. Domain refusals are expected outcomes (Warning, operation-only, no recovery).
[<RequireQualifiedAccess>]
module Faults =
    let ofRejection (operation: string) (rejection: Rejection) : Fault =
        let config = Operational.aegis
        let code = Kernel.code rejection
        let message = String.Join(" ", Kernel.reasons rejection)
        let scope = Aegis.scope config operation (Map.ofList [ "code", ContextValue.Public code ])
        Aegis.faultOf
            config
            scope
            (FaultCode code)
            FailureCategory.DomainFailure
            FaultSeverity.Warning
            FaultImpact.OperationOnly
            Persistence.Transient
            RecoveryPolicy.NoRecovery
            message
            (InvalidOperationException($"{code}: {message}"))

    let ofStorageError (operation: string) (error: StorageError) : Fault =
        let config = Operational.aegis
        let code = error.Code
        let scope =
            Aegis.scope
                config
                operation
                (Map.ofList [ "code", ContextValue.Public code; "domainCode", ContextValue.Public(defaultArg error.DomainCode "") ])
        Aegis.faultOf
            config
            scope
            (FaultCode code)
            FailureCategory.DomainFailure
            FaultSeverity.Error
            FaultImpact.OperationOnly
            Persistence.Transient
            RecoveryPolicy.NoRecovery
            error.Detail
            (IO.InvalidDataException($"{code}: {error.Detail}"))
