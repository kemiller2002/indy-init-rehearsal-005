/// The write path's request book: the only route from a local change to a published commit.
/// Pure. The shell performs the returned effects and feeds the provider's answers back as events.
///
/// Write invariants (curation defect classes, R4-01; tests in tests/App.Engine.Tests/WriteInvariantTests.fs):
///  - DC-001  a publication is parented by the exact commit its change was decided at (expectedHeadOid);
///            reconciliation classifies by ancestry, never by ref equality.
///  - DC-002  an answer applies only to the request instance that asked it, while it is still in flight;
///            every user event is legal only in its source states; refused steps change nothing.
///  - DC-003  once the publication was sent, any failure is Unknown (may have applied): no Retry,
///            reconciliation re-reads the head first. Pending, sending, conflicted and unknown stay distinct.
module IndyInitRootCauseInvestigator.Engine.Sync

/// A Git commit object id. Provider metadata: never written into domain files.
type Oid = Oid of string

/// One request instance. Allocated by the book itself, strictly increasing, never reused, so an answer
/// to an earlier instance (a replaced write, an older connection) can never match the current one.
type RequestId = RequestId of int64

type FileWrite = { Path: string; Bytes: string }

/// A local change: the files to publish, decided by the engine against the history read at `DecidedAt`.
/// `Marker` is a fresh per-change token carried in the commit so reconciliation can recognise it.
type Change =
    { DecidedAt: Oid
      Marker: string
      Files: FileWrite list }

/// Where a write is. Classification of a failure depends on it (DC-003).
type Stage =
    | Preparing
    | Sent

type Attempt =
    { Request: RequestId
      Change: Change
      Stage: Stage }

/// A definitive answer from the provider: the publication was certainly not applied.
type Refusal =
    /// The branch moved (expected head mismatch, STALE_DATA, non-fast-forward).
    | StaleHead
    | Unauthorized
    | Forbidden
    | NotFound
    | RateLimited
    | Invalid of reason: string

/// A failure that carries no answer from the provider (network error, timeout, cancellation).
type Transport =
    | NetworkError
    | TimedOut
    | Cancelled

type ConnectFailure =
    | ConnectUnauthorized
    | ConnectForbidden
    | ConnectNotFound
    | ConnectOffline
    | ConnectIncompatible of reason: string

/// Why a change is held locally instead of being in flight. The change itself is always kept.
type HeldReason =
    /// Another writer advanced the branch; `remoteHead` is the head observed (None when only the refusal is known).
    | Conflict of remoteHead: Oid option
    /// The provider refused, or the request never left: nothing was written; Retry is honest.
    | NotWritten of NotWrittenCause

and NotWrittenCause =
    | Refused of Refusal
    | NeverSent of Transport

/// One commit after the base on the way to the observed head, oldest first.
type CommitInfo =
    { Oid: Oid
      Parent: Oid
      Marker: string option }

/// How the observed head relates to the base the change was decided at.
type Ancestry =
    /// The head descends from the base through these commits (empty: the head is the base).
    | Descends of CommitInfo list
    /// The head does not descend from the base (history rewritten or unrelated).
    | Unrelated

type Phase =
    | Disconnected
    | Connecting of RequestId
    | ConnectRefused of ConnectFailure
    /// No write in flight. `pending` is a local change not yet published.
    | Idle of head: Oid * pending: Change option
    | Writing of head: Oid * Attempt
    /// The provider acknowledged `commit`; success is claimed only after reading it back.
    | Confirming of head: Oid * Attempt * commit: Oid * read: RequestId
    /// Sent and unanswered (or answered ambiguously): the change may have been applied.
    | Unknown of head: Oid * Attempt
    | Reconciling of head: Oid * Attempt * read: RequestId
    | Held of head: Oid * Change * HeldReason

type State = { Phase: Phase; Issued: int64 }

type Event =
    // user events
    | Connect
    | Edit of Change
    | Write
    | Reapply of Change
    | Discard
    | Reconcile
    // provider answers, each naming the request instance it answers
    | Connected of RequestId * head: Oid
    | ConnectFailed of RequestId * ConnectFailure
    | PublicationSent of RequestId
    | PublicationAcknowledged of RequestId * commit: Oid
    | PublicationRefused of RequestId * Refusal
    | PublicationFailed of RequestId * Transport
    | ReadBack of RequestId * sentBytesFound: bool
    | ReadBackFailed of RequestId
    | HeadObserved of RequestId * head: Oid * Ancestry
    | HeadReadFailed of RequestId

type Effect =
    | OpenConnection of RequestId
    /// Guarded publication: expectedHeadOid = parent = the commit the change was decided at.
    | Publish of RequestId * parent: Oid * marker: string * files: FileWrite list
    | ReadCommit of RequestId * commit: Oid * files: FileWrite list
    | ReadHead of RequestId * since: Oid
    /// Our change is in, but others wrote after it: reload before deciding anything else.
    | ReloadRequired of head: Oid
    /// A held change the person discarded, handed back so it can be reapplied after a reload.
    | Released of Change

type FaultCode =
    | IllegalInState
    | StaleAnswer
    | NotDecidedOnHead
    | NothingToWrite
    | OutcomeUnknown

/// A refused step. The state is unchanged.
type Fault =
    { Code: FaultCode
      Phase: string
      Event: string
      Reason: string }

let initial = { Phase = Disconnected; Issued = 0L }

let phaseName =
    function
    | Disconnected -> "Disconnected"
    | Connecting _ -> "Connecting"
    | ConnectRefused _ -> "ConnectRefused"
    | Idle _ -> "Idle"
    | Writing _ -> "Writing"
    | Confirming _ -> "Confirming"
    | Unknown _ -> "Unknown"
    | Reconciling _ -> "Reconciling"
    | Held _ -> "Held"

let eventName =
    function
    | Connect -> "Connect"
    | Edit _ -> "Edit"
    | Write -> "Write"
    | Reapply _ -> "Reapply"
    | Discard -> "Discard"
    | Reconcile -> "Reconcile"
    | Connected _ -> "Connected"
    | ConnectFailed _ -> "ConnectFailed"
    | PublicationSent _ -> "PublicationSent"
    | PublicationAcknowledged _ -> "PublicationAcknowledged"
    | PublicationRefused _ -> "PublicationRefused"
    | PublicationFailed _ -> "PublicationFailed"
    | ReadBack _ -> "ReadBack"
    | ReadBackFailed _ -> "ReadBackFailed"
    | HeadObserved _ -> "HeadObserved"
    | HeadReadFailed _ -> "HeadReadFailed"

/// How a reconciliation read relates to an attempt (DCT-001-B): by ancestry and marker, never by ref equality.
type Classification =
    | NotApplied
    | Applied of ours: Oid * advancedTo: Oid option
    | Concurrent

let classify (attempt: Attempt) (head: Oid) (ancestry: Ancestry) : Classification =
    match ancestry with
    | Unrelated -> Concurrent
    | Descends [] -> NotApplied
    | Descends(first :: rest) ->
        if first.Parent = attempt.Change.DecidedAt && first.Marker = Some attempt.Change.Marker then
            Applied(first.Oid, (if List.isEmpty rest then None else Some head))
        else
            Concurrent

/// The request instance an answer names, if it is an answer.
let answerOf =
    function
    | Connected(r, _)
    | ConnectFailed(r, _)
    | PublicationSent r
    | PublicationAcknowledged(r, _)
    | PublicationRefused(r, _)
    | PublicationFailed(r, _)
    | ReadBack(r, _)
    | ReadBackFailed r
    | HeadObserved(r, _, _)
    | HeadReadFailed r -> Some r
    | Connect
    | Edit _
    | Write
    | Reapply _
    | Discard
    | Reconcile -> None

/// The request instance the current phase is waiting on.
let inFlight =
    function
    | Connecting r -> Some r
    | Writing(_, a) -> Some a.Request
    | Confirming(_, _, _, r) -> Some r
    | Reconciling(_, _, r) -> Some r
    | Disconnected
    | ConnectRefused _
    | Idle _
    | Unknown _
    | Held _ -> None

let step (state: State) (event: Event) : Result<State * Effect list, Fault> =
    let fault code reason =
        Error
            { Code = code
              Phase = phaseName state.Phase
              Event = eventName event
              Reason = reason }

    let next = state.Issued + 1L
    let issue () = RequestId next
    let go phase effects = Ok({ state with Phase = phase }, effects)
    let goIssued phase effects = Ok({ Phase = phase; Issued = next }, effects)

    match answerOf event, inFlight state.Phase with
    | Some answered, current when current <> Some answered ->
        fault StaleAnswer "The answer is for a request instance that is not in flight."
    | _ ->
        match state.Phase, event with
        // connection
        | (Disconnected | ConnectRefused _), Connect ->
            let r = issue ()
            goIssued (Connecting r) [ OpenConnection r ]
        | Connecting _, Connected(_, head) -> go (Idle(head, None)) []
        | Connecting _, ConnectFailed(_, failure) -> go (ConnectRefused failure) []

        // local changes
        | Idle(head, _), (Edit change | Reapply change) ->
            if change.DecidedAt = head then go (Idle(head, Some change)) []
            else fault NotDecidedOnHead "The change was decided on a different head than the one read."
        | Idle(_, None), Write -> fault NothingToWrite "There is no local change to publish."
        | Idle(head, Some change), Write ->
            let r = issue ()
            let attempt = { Request = r; Change = change; Stage = Preparing }
            goIssued (Writing(head, attempt)) [ Publish(r, change.DecidedAt, change.Marker, change.Files) ]
        | Held(head, change, NotWritten _), Write ->
            let r = issue ()
            let attempt = { Request = r; Change = change; Stage = Preparing }
            goIssued (Writing(head, attempt)) [ Publish(r, change.DecidedAt, change.Marker, change.Files) ]
        | Held(_, _, Conflict _), Write ->
            fault IllegalInState "Another writer changed the repository; reload and reapply before publishing."
        | Held(head, change, Conflict remote), Discard ->
            let effects = [ Released change ] @ (remote |> Option.map ReloadRequired |> Option.toList)
            go (Idle(defaultArg remote head, None)) effects
        | Held(head, change, NotWritten _), Discard -> go (Idle(head, None)) [ Released change ]

        // the write in flight
        | Writing(head, attempt), PublicationSent _ ->
            go (Writing(head, { attempt with Stage = Sent })) []
        | Writing(head, attempt), PublicationAcknowledged(_, commit) ->
            let r = issue ()
            goIssued (Confirming(head, { attempt with Stage = Sent }, commit, r)) [ ReadCommit(r, commit, attempt.Change.Files) ]
        | Writing(head, attempt), PublicationRefused(_, StaleHead) ->
            go (Held(head, attempt.Change, Conflict None)) []
        | Writing(head, attempt), PublicationRefused(_, refusal) ->
            go (Held(head, attempt.Change, NotWritten(Refused refusal))) []
        | Writing(head, attempt), PublicationFailed(_, transport) ->
            match attempt.Stage with
            | Preparing -> go (Held(head, attempt.Change, NotWritten(NeverSent transport))) []
            | Sent -> go (Unknown(head, attempt)) []

        // confirmation: never claim saved before re-reading the commit
        | Confirming(_, _, commit, _), ReadBack(_, true) -> go (Idle(commit, None)) []
        | Confirming(head, attempt, _, _), ReadBack(_, false) -> go (Unknown(head, attempt)) []
        | Confirming(head, attempt, _, _), ReadBackFailed _ -> go (Unknown(head, attempt)) []

        // unknown outcome: reconcile by re-reading the head first
        | Unknown(head, attempt), Reconcile ->
            let r = issue ()
            goIssued (Reconciling(head, attempt, r)) [ ReadHead(r, attempt.Change.DecidedAt) ]
        | Unknown _, Write -> fault OutcomeUnknown "The last publication may have been applied; reconcile before writing again."
        | Reconciling(head, attempt, _), HeadObserved(_, observed, ancestry) ->
            match classify attempt observed ancestry with
            | NotApplied -> go (Idle(head, Some attempt.Change)) []
            | Applied(ours, None) -> go (Idle(ours, None)) []
            | Applied(ours, Some advanced) -> go (Idle(ours, None)) [ ReloadRequired advanced ]
            | Concurrent -> go (Held(head, attempt.Change, Conflict(Some observed))) []
        | Reconciling(head, attempt, _), HeadReadFailed _ -> go (Unknown(head, attempt)) []

        | _ -> fault IllegalInState "This event is not legal in this state."

/// Whether Retry may be offered: only when nothing was written (DC-003).
let canRetry =
    function
    | Held(_, _, NotWritten _) -> true
    | _ -> false

/// Distinct, honest save states for the projection (UI-006-O1). Synced only after a confirmed read-back.
type Status =
    | NotConnected
    | Opening
    | ConnectionRefused
    | Clean
    | Unsaved
    | Saving
    | Verifying
    | MayHaveSaved
    | Checking
    | Conflicted
    | Failed

let status =
    function
    | Disconnected -> NotConnected
    | Connecting _ -> Opening
    | ConnectRefused _ -> ConnectionRefused
    | Idle(_, None) -> Clean
    | Idle(_, Some _) -> Unsaved
    | Writing _ -> Saving
    | Confirming _ -> Verifying
    | Unknown _ -> MayHaveSaved
    | Reconciling _ -> Checking
    | Held(_, _, Conflict _) -> Conflicted
    | Held(_, _, NotWritten _) -> Failed
