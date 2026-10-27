/// T12: write invariants first (R4-01). One failing-first test per write defect class DC-001..DC-003 and the
/// request-identity class (R1 M4 / R3 M1), each with a negative control: the same invariant check is run against a
/// deliberately defective step function and must FAIL there (AR-009, AR-022-O3).
/// DC-004/DC-005 (provider/browser level, T18) and DC-006 (browser boot, T16) belong to other streams.
/// Each test names the VPO obligation ids it proves and carries them as `obligation` traits.
module App.Engine.Tests.WriteInvariantTests

open Xunit
open IndyInitRootCauseInvestigator.Engine
open IndyInitRootCauseInvestigator.Engine.Sync

type Stepper = State -> Event -> Result<State * Effect list, Fault>

let private real: Stepper = Sync.step

let private oid s = Oid s
let private A = oid "a000000000000000000000000000000000000000"
let private B = oid "b000000000000000000000000000000000000000"
let private C = oid "c000000000000000000000000000000000000000"

let private change at marker =
    { DecidedAt = at
      Marker = marker
      Files = [ { Path = "workspaces/demo/run-1/investigations/x/events.ndjson"; Bytes = "{}\n" } ] }

/// Drives a stepper through events from a state; every step must be accepted.
let private run' (stepper: Stepper) (state: State) (events: Event list) =
    events
    |> List.fold
        (fun (state, effects) event ->
            match stepper state event with
            | Ok(s, e) -> s, effects @ e
            | Error f -> failwithf "setup step refused: %A" f)
        (state, [])

let private run (stepper: Stepper) (events: Event list) = run' stepper Sync.initial events

/// The last request id the book issued (the shell learns it from the effect it must perform).
let private lastRequest (effects: Effect list) =
    effects
    |> List.choose (function
        | OpenConnection r -> Some r
        | Publish(r, _, _, _) -> Some r
        | ReadCommit(r, _, _) -> Some r
        | ReadHead(r, _) -> Some r
        | _ -> None)
    |> List.last

let private connectedAt (stepper: Stepper) head =
    let s, fx = run stepper [ Connect ]
    let s, _ = run' stepper s [ Connected(lastRequest fx, head) ]
    s

// ---------------------------------------------------------------------------------------------------------------
// DCT-001-A: a publication is parented by the exact commit its decision was read at.

/// Invariant: (1) a change decided at another head is refused; (2) the publication's parent is the decided-at head;
/// (3) a stale-head refusal keeps the change held (conflict), never published over unread history.
let private parentedByDecidedHead (stepper: Stepper) =
    let s = connectedAt stepper A
    let staleRefused =
        match stepper s (Edit(change B "m1")) with
        | Error f -> f.Code = NotDecidedOnHead && true
        | Ok _ -> false
    let s1, _ = run' stepper s [ Edit(change A "m1") ]
    match stepper s1 Write with
    | Ok(s2, [ Publish(r, parent, _, _) ]) ->
        let parentOk = parent = A
        match stepper s2 (PublicationRefused(r, StaleHead)) with
        | Ok(s3, _) ->
            let kept =
                match s3.Phase with
                | Held(_, c, HeldReason.Conflict _) -> c.Marker = "m1"
                | _ -> false
            staleRefused && parentOk && kept && not (canRetry s3.Phase)
        | Error _ -> false
    | _ -> false

/// Defect: the book re-targets a change to whatever head it currently holds (fast-forward over unread history).
let private retargetingDefect: Stepper =
    fun state event ->
        match state.Phase, event with
        | Idle(head, _), Edit c -> Sync.step state (Edit { c with DecidedAt = head })
        | _ -> Sync.step state event

[<Fact>]
[<Trait("obligation", "VPO-d7fbd84e40ded145")>]
[<Trait("obligation", "VPO-34731ac9a977b818")>]
[<Trait("defectClass", "DC-001")>]
let ``DCT-001-A VPO-d7fbd84e40ded145 VPO-34731ac9a977b818 publication is parented by the decided-at head and a moved branch is a held conflict`` () =
    Assert.True(parentedByDecidedHead real)

[<Fact>]
[<Trait("obligation", "VPO-821ce84e76d497b9")>]
[<Trait("obligation", "VPO-7b54ff75e3784cae")>]
[<Trait("defectClass", "DC-001")>]
let ``DCT-001-A negative control VPO-821ce84e76d497b9 VPO-7b54ff75e3784cae the check fails on a book that re-targets stale decisions`` () =
    Assert.False(parentedByDecidedHead retargetingDefect)

// ---------------------------------------------------------------------------------------------------------------
// DCT-001-B: reconciliation compares commits by ancestry, never by ref equality.

let private unknownAfterSend (stepper: Stepper) marker =
    let s = connectedAt stepper A
    let s, fx = run' stepper s [ Edit(change A marker); Write ]
    let r = lastRequest fx
    let s, _ = run' stepper s [ PublicationSent r; PublicationFailed(r, NetworkError); Reconcile ]
    s

let private classifiesByAncestry (stepper: Stepper) =
    // The remote head descends from the base through ANOTHER writer's commit.
    let s = unknownAfterSend stepper "ours"
    let read = inFlight s.Phase |> Option.get
    let other = { Oid = B; Parent = A; Marker = Some "theirs" }
    let concurrent =
        match stepper s (HeadObserved(read, B, Descends [ other ])) with
        | Ok(s', _) ->
            match s'.Phase with
            | Held(_, c, HeldReason.Conflict(Some head)) -> head = B && c.Marker = "ours"
            | _ -> false
        | Error _ -> false
    // Our own commit, directly on the base: applied.
    let s2 = unknownAfterSend stepper "ours"
    let read2 = inFlight s2.Phase |> Option.get
    let applied =
        match stepper s2 (HeadObserved(read2, C, Descends [ { Oid = C; Parent = A; Marker = Some "ours" } ])) with
        | Ok(s', _) -> s'.Phase = Idle(C, None)
        | Error _ -> false
    // The head is still the base: not applied, change kept pending.
    let s3 = unknownAfterSend stepper "ours"
    let read3 = inFlight s3.Phase |> Option.get
    let notApplied =
        match stepper s3 (HeadObserved(read3, A, Descends [])) with
        | Ok(s', _) ->
            match s'.Phase with
            | Idle(h, Some c) -> h = A && c.Marker = "ours"
            | _ -> false
        | Error _ -> false
    concurrent && applied && notApplied

/// Defect: "the head moved past the base, so our write must be there" (ref comparison).
let private refEqualityDefect: Stepper =
    fun state event ->
        match state.Phase, event with
        | Reconciling(_, a, _), HeadObserved(r, head, _) when head <> a.Change.DecidedAt ->
            Sync.step state (HeadObserved(r, head, Descends [ { Oid = head; Parent = a.Change.DecidedAt; Marker = Some a.Change.Marker } ]))
        | _ -> Sync.step state event

[<Fact>]
[<Trait("obligation", "VPO-d7fbd84e40ded145")>]
[<Trait("defectClass", "DC-001")>]
let ``DCT-001-B VPO-d7fbd84e40ded145 reconciliation classifies a head reached through another writer as concurrent`` () =
    Assert.True(classifiesByAncestry real)

[<Fact>]
[<Trait("obligation", "VPO-821ce84e76d497b9")>]
[<Trait("obligation", "VPO-7b54ff75e3784cae")>]
[<Trait("defectClass", "DC-001")>]
let ``DCT-001-B negative control VPO-821ce84e76d497b9 VPO-7b54ff75e3784cae the check fails on ref-equality reconciliation`` () =
    Assert.False(classifiesByAncestry refEqualityDefect)

// ---------------------------------------------------------------------------------------------------------------
// DCT-002-A: an answer applies only to the request instance that asked it, while it is in flight.

let private lateAnswersDiscarded (stepper: Stepper) =
    let s = connectedAt stepper A
    // W1 is refused before it is sent, so the change is held and written again as W2.
    let s, fx1 = run' stepper s [ Edit(change A "m"); Write ]
    let w1 = lastRequest fx1
    let s, _ = run' stepper s [ PublicationFailed(w1, TimedOut) ]
    let s, fx2 = run' stepper s [ Write ]
    let w2 = lastRequest fx2
    let distinct = w1 <> w2
    // Late answers for W1 must be refused as stale and change nothing.
    let late =
        [ PublicationAcknowledged(w1, C); PublicationRefused(w1, StaleHead); PublicationFailed(w1, Cancelled) ]
        |> List.forall (fun e ->
            match stepper s e with
            | Error f -> f.Code = StaleAnswer
            | Ok _ -> false)
    // W2's own answer still applies.
    let own =
        match stepper s (PublicationAcknowledged(w2, C)) with
        | Ok(s', _) ->
            match s'.Phase with
            | Confirming(_, a, commit, _) -> a.Request = w2 && commit = C
            | _ -> false
        | Error _ -> false
    distinct && late && own

/// Defect: answers are matched by kind, not by request instance.
let private ignoresRequestIdentity: Stepper =
    fun state event ->
        let retag r =
            match inFlight state.Phase with
            | Some current -> current
            | None -> r
        let e =
            match event with
            | PublicationAcknowledged(r, c) -> PublicationAcknowledged(retag r, c)
            | PublicationRefused(r, x) -> PublicationRefused(retag r, x)
            | PublicationFailed(r, x) -> PublicationFailed(retag r, x)
            | Connected(r, h) -> Connected(retag r, h)
            | other -> other
        Sync.step state e

[<Fact>]
[<Trait("obligation", "VPO-2d4b7557ce013e50")>]
[<Trait("obligation", "VPO-8c435e1ebdf43e7c")>]
[<Trait("obligation", "VPO-cd9dd8ec730c3d3e")>]
[<Trait("defectClass", "DC-002")>]
let ``DCT-002-A VPO-2d4b7557ce013e50 VPO-8c435e1ebdf43e7c VPO-cd9dd8ec730c3d3e a late answer for a replaced write is discarded`` () =
    Assert.True(lateAnswersDiscarded real)

[<Fact>]
[<Trait("obligation", "VPO-821ce84e76d497b9")>]
[<Trait("obligation", "VPO-7b54ff75e3784cae")>]
[<Trait("defectClass", "DC-002")>]
let ``DCT-002-A negative control VPO-821ce84e76d497b9 VPO-7b54ff75e3784cae the check fails when answers are matched by kind`` () =
    Assert.False(lateAnswersDiscarded ignoresRequestIdentity)

/// Request identity across connections (R1 M4 / R3 M1): an answer to an earlier connection never lands.
let private reconnectIdentity (stepper: Stepper) =
    let s, fx1 = run stepper [ Connect ]
    let c1 = lastRequest fx1
    let s, _ = run' stepper s [ ConnectFailed(c1, ConnectOffline) ]
    let s, fx2 = run' stepper s [ Connect ]
    let c2 = lastRequest fx2
    let stale =
        match stepper s (Connected(c1, A)) with
        | Error f -> f.Code = StaleAnswer
        | Ok _ -> false
    let own =
        match stepper s (Connected(c2, B)) with
        | Ok(s', _) -> s'.Phase = Idle(B, None)
        | Error _ -> false
    stale && own

[<Fact>]
[<Trait("obligation", "VPO-8c435e1ebdf43e7c")>]
[<Trait("obligation", "VPO-2d4b7557ce013e50")>]
[<Trait("defectClass", "REQUEST-IDENTITY")>]
let ``REQUEST-IDENTITY VPO-8c435e1ebdf43e7c VPO-2d4b7557ce013e50 an answer to an earlier connection never lands on a newer one`` () =
    Assert.True(reconnectIdentity real)

[<Fact>]
[<Trait("obligation", "VPO-821ce84e76d497b9")>]
[<Trait("defectClass", "REQUEST-IDENTITY")>]
let ``REQUEST-IDENTITY negative control VPO-821ce84e76d497b9 the check fails when connection answers are matched by kind`` () =
    Assert.False(reconnectIdentity ignoresRequestIdentity)

// ---------------------------------------------------------------------------------------------------------------
// DCT-002-B: every user event is legal only in its source states; refused events change nothing.

let private sample = change A "m"

/// One representative state per phase.
let private everyPhase (stepper: Stepper) =
    let s0 = Sync.initial
    let connecting, _ = run stepper [ Connect ]
    let refused, _ = run' stepper connecting [ ConnectFailed(inFlight connecting.Phase |> Option.get, ConnectForbidden) ]
    let idle = connectedAt stepper A
    let idlePending, _ = run' stepper idle [ Edit sample ]
    let writing, fx = run' stepper idlePending [ Write ]
    let w = lastRequest fx
    let sent, _ = run' stepper writing [ PublicationSent w ]
    let confirming, _ = run' stepper sent [ PublicationAcknowledged(w, C) ]
    let unknown, _ = run' stepper sent [ PublicationFailed(w, NetworkError) ]
    let reconciling, _ = run' stepper unknown [ Reconcile ]
    let heldConflict, _ = run' stepper writing [ PublicationRefused(w, StaleHead) ]
    let heldNotWritten, _ = run' stepper writing [ PublicationRefused(w, Forbidden) ]
    [ s0; connecting; refused; idle; idlePending; writing; confirming; unknown; reconciling; heldConflict; heldNotWritten ]

/// The legal source states of each user event (DCT-002-B).
let private legal (event: Event) (phase: Phase) =
    match event, phase with
    | Connect, (Disconnected | ConnectRefused _) -> true
    | Write, (Idle(_, Some _) | Held(_, _, NotWritten _)) -> true
    | (Edit _ | Reapply _), Idle _ -> true
    | Discard, Held _ -> true
    | Reconcile, Unknown _ -> true
    | _ -> false

let private userEventsGuarded (stepper: Stepper) =
    let events = [ Connect; Write; Edit sample; Reapply sample; Discard; Reconcile ]
    everyPhase stepper
    |> List.forall (fun state ->
        events
        |> List.forall (fun event ->
            match stepper state event with
            | Ok _ -> legal event state.Phase
            | Error _ -> not (legal event state.Phase)))

/// Defect: a write is accepted while the previous outcome is unknown (R2: "event handlers did not check the state").
let private unguardedWrite: Stepper =
    fun state event ->
        match state.Phase, event with
        | Unknown(head, a), Write -> Sync.step { state with Phase = Idle(head, Some a.Change) } Write
        | _ -> Sync.step state event

[<Fact>]
[<Trait("obligation", "VPO-199103f9166730e7")>]
[<Trait("obligation", "VPO-6886a2a6e612f704")>]
[<Trait("defectClass", "DC-002")>]
let ``DCT-002-B VPO-199103f9166730e7 VPO-6886a2a6e612f704 every user event is accepted only in its legal source states`` () =
    Assert.True(userEventsGuarded real)

[<Fact>]
[<Trait("obligation", "VPO-199103f9166730e7")>]
[<Trait("defectClass", "DC-002")>]
let ``DCT-002-B VPO-199103f9166730e7 a refused event leaves the state unchanged and is a typed fault`` () =
    // Refusal is a value (Result.Error with a typed code); the caller keeps its state: nothing to roll back.
    for state in everyPhase real do
        for event in [ Connect; Write; Discard; Reconcile ] do
            match real state event with
            | Error f -> Assert.Contains(f.Code, [ IllegalInState; NothingToWrite; OutcomeUnknown; NotDecidedOnHead ])
            | Ok _ -> ()

[<Fact>]
[<Trait("obligation", "VPO-821ce84e76d497b9")>]
[<Trait("obligation", "VPO-787e24f3c23195e9")>]
[<Trait("defectClass", "DC-002")>]
let ``DCT-002-B negative control VPO-821ce84e76d497b9 VPO-787e24f3c23195e9 the check fails on a book that writes during an unknown outcome`` () =
    Assert.False(userEventsGuarded unguardedWrite)

[<Fact>]
[<Trait("obligation", "VPO-199103f9166730e7")>]
[<Trait("defectClass", "DC-002")>]
let ``DCT-002-B R3 D-04 VPO-199103f9166730e7 resubmitting an unchanged held change keeps its target parent`` () =
    let s = connectedAt real A
    let s, fx = run' real s [ Edit sample; Write ]
    let w = lastRequest fx
    let s, _ = run' real s [ PublicationFailed(w, TimedOut) ]
    match real s Write with
    | Ok(_, [ Publish(_, parent, marker, _) ]) ->
        Assert.Equal(A, parent)
        Assert.Equal("m", marker)
    | other -> failwithf "unexpected %A" other

// ---------------------------------------------------------------------------------------------------------------
// DCT-003-A: once the publication was sent, any failure is unknown, never "not written".

let private sentFailuresAreUnknown (stepper: Stepper) =
    [ NetworkError; TimedOut; Cancelled ]
    |> List.forall (fun transport ->
        let s = connectedAt stepper A
        let s, fx = run' stepper s [ Edit sample; Write ]
        let w = lastRequest fx
        let s, _ = run' stepper s [ PublicationSent w ]
        match stepper s (PublicationFailed(w, transport)) with
        | Ok(s', _) ->
            let unknown =
                match s'.Phase with
                | Unknown _ -> true
                | _ -> false
            let noRetry = not (canRetry s'.Phase)
            let writeRefused =
                match stepper s' Write with
                | Error f -> f.Code = OutcomeUnknown
                | Ok _ -> false
            let reconcileReadsHeadFirst =
                match stepper s' Reconcile with
                | Ok(_, [ ReadHead(_, since) ]) -> since = A
                | _ -> false
            unknown && noRetry && writeRefused && reconcileReadsHeadFirst && Sync.status s'.Phase = MayHaveSaved
        | Error _ -> false)

let private readBackMismatchIsUnknown (stepper: Stepper) =
    let s = connectedAt stepper A
    let s, fx = run' stepper s [ Edit sample; Write ]
    let w = lastRequest fx
    let s, fx2 = run' stepper s [ PublicationSent w; PublicationAcknowledged(w, C) ]
    let read = lastRequest fx2
    let notYetSaved = Sync.status s.Phase <> Clean
    match stepper s (ReadBack(read, false)) with
    | Ok(s', _) ->
        notYetSaved
        && (match s'.Phase with
            | Unknown _ -> true
            | _ -> false)
    | Error _ -> false

/// Defect: failure classification ignores the write stage (R2 DC-003).
let private stageBlindClassification: Stepper =
    fun state event ->
        match state.Phase, event with
        | Writing(h, a), PublicationFailed _ -> Sync.step { state with Phase = Writing(h, { a with Stage = Preparing }) } event
        | _ -> Sync.step state event

[<Fact>]
[<Trait("obligation", "VPO-b3efad6eb85360be")>]
[<Trait("obligation", "VPO-89ed6170f531961e")>]
[<Trait("defectClass", "DC-003")>]
let ``DCT-003-A VPO-b3efad6eb85360be VPO-89ed6170f531961e a failure after send is unknown with no retry and reconcile reads the head first`` () =
    Assert.True(sentFailuresAreUnknown real)

[<Fact>]
[<Trait("obligation", "VPO-b3efad6eb85360be")>]
[<Trait("defectClass", "DC-003")>]
let ``DCT-003-A VPO-b3efad6eb85360be saved is claimed only after the read-back matches`` () =
    Assert.True(readBackMismatchIsUnknown real)

[<Fact>]
[<Trait("obligation", "VPO-821ce84e76d497b9")>]
[<Trait("obligation", "VPO-7b54ff75e3784cae")>]
[<Trait("defectClass", "DC-003")>]
let ``DCT-003-A negative control VPO-821ce84e76d497b9 VPO-7b54ff75e3784cae the check fails on stage-blind failure classification`` () =
    Assert.False(sentFailuresAreUnknown stageBlindClassification)

[<Fact>]
[<Trait("obligation", "VPO-89ed6170f531961e")>]
[<Trait("defectClass", "DC-003")>]
let ``UI-006 VPO-89ed6170f531961e pending sending conflicted unknown and failed are distinct statuses`` () =
    let statuses = everyPhase real |> List.map (fun s -> Sync.status s.Phase) |> List.distinct
    for expected in [ Unsaved; Saving; Verifying; MayHaveSaved; Checking; Conflicted; Failed; Clean; NotConnected ] do
        Assert.Contains(expected, statuses)
