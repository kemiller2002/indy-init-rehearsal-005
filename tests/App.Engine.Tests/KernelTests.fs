/// T13: the authoritative F# semantic kernel. Transition tables, illegal-command and forged-history rejection (G3),
/// capabilities with reasons, Boundary A rejections, Boundary E report honesty, and an engine-created file set that
/// round-trips through the storage contract. Every test names the VPO obligation ids it proves.
module App.Engine.Tests.KernelTests

open System
open Xunit
open IndyInitRootCauseInvestigator.Engine

let private ns = Guid "3b0c1a52-6c1e-4f0e-9d8e-2a7f4b5c6d70"
let private gid (name: string) = Hashing.uuidV5 ns name
let private id<'T> name : Id<'T> = Id(gid name)
let private t0 = DateTimeOffset(2026, 10, 27, 12, 0, 0, TimeSpan.Zero)
let private human = { ActorId = "investigator-01"; Kind = Human }
let private agent = { ActorId = "assistant-01"; Kind = Agent("anthropic", "claude") }
let private inv: InvestigationId = id "investigation"

/// Drives commands through Kernel.submit, numbering command ids and minutes deterministically.
type private Script =
    { Session: Session
      Step: int }

let private envelope (s: Script) actor command =
    { CommandId = gid $"command/{s.Step}"
      Investigation = inv
      ExpectedVersion = Some(Kernel.version s.Session)
      Actor = actor
      RequestedAt = t0.AddMinutes(float s.Step)
      Command = command }

let private tryRun (s: Script) actor command = Kernel.submit s.Session (envelope s actor command)

let private run (s: Script) command =
    match tryRun s human command with
    | Ok a -> { Session = a.Session; Step = s.Step + 1 }
    | Error r -> failwithf "step %d refused: %A" s.Step r

let private start () =
    run { Session = Kernel.empty; Step = 0 } (StartInvestigation("Checkout failures", id "obs/1", "Payments fail since 09:40."))

let private state (s: Script) = s.Session.State |> Option.get

/// A full investigation from start to resolution, through the kernel only.
let private resolvedScript () =
    let s = start ()
    let steps =
        [ ActivateInvestigation
          RecordHypothesis(id "h/timeout", "Processor timeout too short", Some "Timeouts are mapped to declines")
          RecordHypothesis(id "h/fraud", "Fraud rule misfires", None)
          AdmitEvidence(id "e/1", TextInput "Decline rate rises with latency.", Measurement, "dashboard", None)
          RelateEvidence(id "r/1", id "e/1", id "h/timeout", Supports, None)
          TransitionHypothesis(id "h/timeout", MarkPlausible, "Fits the timing", [])
          TransitionHypothesis(id "h/timeout", MarkSupported, "Latency correlation", [ id "e/1" ])
          AdmitEvidence(id "e/2", TextInput "Fraud rejection rate unchanged.", Measurement, "fraud dashboard", None)
          TransitionHypothesis(id "h/fraud", Falsify, "Rate unchanged", [ id "e/2" ])
          RecordCause(id "c/1", "Card declines at checkout", [ { Hypothesis = id "h/timeout"; Role = RootCause } ], Some "Short timeout maps to decline")
          ConfirmCause(id "c/1")
          RecordCorrectiveAction(id "ca/1", Correction, "Raise the timeout to 6 s", [ id "c/1" ])
          RecordCriterion(id "cr/1", id "ca/1", "Decline rate back to baseline", None)
          TransitionCorrection(id "ca/1", ApproveCorrection)
          TransitionCorrection(id "ca/1", StartCorrection)
          TransitionCorrection(id "ca/1", MarkImplemented)
          TransitionCorrection(id "ca/1", StartVerificationOfEffect)
          AdmitEvidence(id "e/3", TextInput "Decline rate 0.3% for 24 h.", Measurement, "dashboard", None)
          RecordVerification(id "v/1", id "cr/1", Satisfied, [ id "e/3" ])
          RecordConclusion(id "k/1", "The short timeout caused the declines.", ConfirmedCause, [ id "c/1" ], None)
          ResolveInvestigation ]
    steps |> List.fold run s

// ---- transition tables (AR-006) ----

let private allLifecycles = [ Draft; Active; AwaitingEvidence; AwaitingVerification; Resolved; Closed; Archived ]

let private allLifecycleCommands =
    [ ActivateInvestigationCommand
      MarkAwaitingEvidenceCommand
      MarkAwaitingVerificationCommand
      ResolveInvestigationCommand
      CloseInvestigationCommand
      ReopenInvestigationCommand ]

[<Fact>]
[<Trait("obligation", "VPO-6886a2a6e612f704")>]
[<Trait("obligation", "VPO-199103f9166730e7")>]
[<Trait("task", "T13")>]
let ``T13 VPO-6886a2a6e612f704 VPO-199103f9166730e7 lifecycle commands are accepted exactly where the transition table allows them`` () =
    let resolved = state (resolvedScript ())
    for from in allLifecycles do
        for command in allLifecycleCommands do
            let inv = { resolved with Lifecycle = from }
            let cmd =
                match command with
                | ActivateInvestigationCommand -> ActivateInvestigation
                | MarkAwaitingEvidenceCommand -> MarkAwaitingEvidence "waiting"
                | MarkAwaitingVerificationCommand -> MarkAwaitingVerification
                | ResolveInvestigationCommand -> ResolveInvestigation
                | CloseInvestigationCommand -> CloseInvestigation
                | ReopenInvestigationCommand -> ReopenInvestigation "new evidence"
                | VerificationFailedFollowOn -> ActivateInvestigation
            let env =
                { CommandId = gid "probe"; Investigation = inv.Id; ExpectedVersion = None; Actor = human; RequestedAt = t0; Command = cmd }
            let legal = (Transitions.lifecycle from command).IsSome
            match Decide.decide (Some inv) env with
            | Ok [ LifecycleChanged(f, target, c, _) ] ->
                Assert.True(legal, $"{from} {command} accepted")
                Assert.Equal(from, f)
                Assert.Equal(Some target, Transitions.lifecycle from command)
                Assert.Equal(command, c)
            | Ok other -> failwithf "unexpected events %A" other
            | Error errors ->
                // Structurally legal commands may only be refused by their gate (verification work, resolution gate).
                if legal then
                    Assert.True(
                        errors |> List.forall (fun e -> e.Code.StartsWith "RESOLVE." || e.Code = "LIFECYCLE.NO_VERIFICATION_WORK"),
                        $"{from} {command}: {errors}"
                    )
                else
                    Assert.Equal("TRANSITION.ILLEGAL", (List.head errors).Code)

let private allHypothesisStates = [ Proposed; Plausible; Supported; Weakened; Falsified; Superseded ]
let private allHypothesisCommands = [ MarkPlausible; MarkSupported; MarkWeakened; Falsify; Supersede; Reopen ]

[<Fact>]
[<Trait("obligation", "VPO-6886a2a6e612f704")>]
[<Trait("obligation", "VPO-199103f9166730e7")>]
[<Trait("task", "T13")>]
let ``T13 VPO-6886a2a6e612f704 VPO-199103f9166730e7 hypothesis transitions follow the table and there is no generic state setter`` () =
    let s = start () |> fun s -> run s ActivateInvestigation
    let s = run s (RecordHypothesis(id "h/x", "X", None))
    let s = run s (AdmitEvidence(id "e/new", TextInput "new", Log, "log", None))
    let inv = state s
    for from in allHypothesisStates do
        for command in allHypothesisCommands do
            let h = inv.Hypotheses[id "h/x"]
            // A falsification at version 1 so that e/new (admitted later) is new evidence for Reopen.
            let history = if from = Falsified then [ { From = Plausible; To = Falsified; Command = Falsify; Reason = (Text.create "f").Value; Evidence = []; At = t0; Version = 1L } ] else []
            let inv' = { inv with Hypotheses = inv.Hypotheses.Add(h.Id, { h with State = from; History = history }) }
            let env =
                { CommandId = gid "probe"
                  Investigation = inv.Id
                  ExpectedVersion = None
                  Actor = human
                  RequestedAt = t0
                  Command = TransitionHypothesis(h.Id, command, "because", [ id "e/new" ]) }
            match Decide.decide (Some inv') env, Transitions.hypothesis from command with
            | Ok [ HypothesisTransitioned(_, f, target, c, _, _) ], Some expected ->
                Assert.Equal(from, f)
                Assert.Equal(expected, target)
                Assert.Equal(command, c)
            | Error errors, None -> Assert.Equal("TRANSITION.ILLEGAL", (List.head errors).Code)
            | other, expected -> failwithf "%A %A: got %A, table says %A" from command other expected

[<Fact>]
[<Trait("obligation", "VPO-199103f9166730e7")>]
[<Trait("obligation", "VPO-be22751d9b5b8098")>]
[<Trait("task", "T13")>]
let ``T13 VPO-199103f9166730e7 VPO-be22751d9b5b8098 stale duplicate unauthorized and illegal submissions are typed rejections that change nothing`` () =
    let s = run (start ()) ActivateInvestigation
    let before = s.Session
    // stale version
    match Kernel.submit s.Session { envelope s human (RecordHypothesis(id "h/a", "A", None)) with ExpectedVersion = Some 1L } with
    | Error(StaleVersion(1L, 2L)) -> ()
    | other -> failwithf "expected stale, got %A" other
    // duplicate submission of an applied command id
    match Kernel.submit s.Session { envelope s human (RecordHypothesis(id "h/a", "A", None)) with CommandId = gid "command/0" } with
    | Error(DuplicateCommand _) -> ()
    | other -> failwithf "expected duplicate, got %A" other
    // an agent may only propose (D-004)
    match tryRun s agent (RecordHypothesis(id "h/a", "A", None)) with
    | Error(Unauthorized e) -> Assert.Equal("AGENT.PROPOSAL_ONLY", e.Code)
    | other -> failwithf "expected unauthorized, got %A" other
    // illegal transition
    match tryRun s human (ConfirmCause(id "c/none")) with
    | Error(Refused(e :: _)) -> Assert.Equal("CAUSE.NOT_FOUND", e.Code)
    | other -> failwithf "expected refusal, got %A" other
    // sub-millisecond time has no wire form
    match Kernel.submit s.Session { envelope s human (RecordHypothesis(id "h/a", "A", None)) with RequestedAt = t0.AddTicks 1L } with
    | Error(NotRepresentable _) -> ()
    | other -> failwithf "expected not representable, got %A" other
    Assert.Equal(before, s.Session)
    // each rejection maps to a typed Aegis fault (DEC-026)
    let fault = Faults.ofRejection "submit" (StaleVersion(1L, 2L))
    Assert.Contains("VERSION.STALE", sprintf "%A" fault)

[<Fact>]
[<Trait("obligation", "VPO-199103f9166730e7")>]
[<Trait("task", "T13")>]
let ``T13 D-004 VPO-199103f9166730e7 an agent proposal needs a human to accept it`` () =
    let s = run (start ()) ActivateInvestigation
    let s =
        match tryRun s agent (RecordProposal(id "p/1", ProposedHypothesis, "Cache stampede", Some "pattern")) with
        | Ok a -> { Session = a.Session; Step = s.Step + 1 }
        | Error r -> failwithf "%A" r
    match tryRun s agent (AcceptProposal(id "p/1", gid "h/from-proposal")) with
    | Error(Unauthorized e) -> Assert.Equal("AGENT.PROPOSAL_ONLY", e.Code)
    | other -> failwithf "%A" other
    let s = run s (AcceptProposal(id "p/1", gid "h/from-proposal"))
    let inv = state s
    Assert.Equal(ProposalAccepted, inv.Proposals[id "p/1"].State)
    Assert.True(inv.Hypotheses.ContainsKey(id "h/from-proposal"))

// ---- forged histories (G3, Boundary C) ----

let private forge (events: DomainEvent list) (version: int64) (body: EventBody) =
    events |> List.map (fun e -> if e.Version = version then { e with Body = body } else e)

[<Fact>]
[<Trait("obligation", "VPO-199103f9166730e7")>]
[<Trait("obligation", "VPO-c135d16c59f5baf8")>]
[<Trait("obligation", "VPO-acd13bfa08dba51a")>]
[<Trait("task", "T13")>]
let ``T13 G3 VPO-c135d16c59f5baf8 VPO-acd13bfa08dba51a forged histories are refused by replay with their domain code`` () =
    let history = (resolvedScript ()).Session.History
    Assert.Empty(History.validate history)
    let at (body: EventBody -> bool) = (history |> List.find (fun e -> body e.Body)).Version
    let confirmV = at (function CausalAssertionConfirmed _ -> true | _ -> false)
    // 1. a confirmation inserted before the cause gate could pass
    let early =
        let hyp = at (function HypothesisTransitioned(_, _, Supported, _, _, _) -> true | _ -> false)
        history |> List.filter (fun e -> e.Version < hyp)
        |> fun prefix -> prefix @ [ { (List.last prefix) with Version = int64 prefix.Length + 1L; CommandId = gid "forged"; Body = CausalAssertionConfirmed(id "c/1", Policy.id) } ]
    Assert.Equal(Some "HISTORY.UNGATED_CONFIRMATION", History.firstCode early) // the cause does not exist yet
    // 2. a confirmation stamped with another policy
    Assert.Equal(Some "HISTORY.INADMISSIBLE_EVENT", History.firstCode (forge history confirmV (CausalAssertionConfirmed(id "c/1", "lenient/9"))))
    // 3. an illegal hypothesis transition (supported -> proposed has no command)
    let hv = at (function HypothesisTransitioned(_, Plausible, Supported, _, _, _) -> true | _ -> false)
    Assert.Equal(
        Some "HISTORY.INADMISSIBLE_EVENT",
        History.firstCode (forge history hv (HypothesisTransitioned(id "h/timeout", Plausible, Falsified, MarkSupported, (Text.create "x").Value, [ id "e/1" ])))
    )
    // 4. a resolution without its gate: drop the satisfied verification's evidence
    let resolvedV = at (function LifecycleChanged(_, Resolved, _, _) -> true | _ -> false)
    let noVerification =
        history
        |> List.filter (fun e -> match e.Body with VerificationRecorded _ -> false | CorrectiveActionTransitioned(_, VerifiedEffective) -> false | _ -> true)
        |> List.mapi (fun i e -> { e with Version = int64 i + 1L })
    Assert.True(resolvedV > 0L)
    Assert.Equal(Some "HISTORY.UNGATED_RESOLUTION", History.firstCode noVerification)
    // 5. an agent writing authoritative state
    let agentWrote = history |> List.map (fun e -> if e.Version = 3L then { e with Actor = agent } else e)
    Assert.Equal(Some "HISTORY.INADMISSIBLE_EVENT", History.firstCode agentWrote)
    // 6. a history that does not start with InvestigationStarted
    Assert.Equal(Some "HISTORY.NO_START", History.firstCode (List.tail history |> List.mapi (fun i e -> { e with Version = int64 i + 1L })))

[<Fact>]
[<Trait("obligation", "VPO-199103f9166730e7")>]
[<Trait("task", "T13")>]
let ``T13 G3 an ungated confirmation in a stored history is refused as HISTORY.UNGATED_CONFIRMATION`` () =
    let s = run (start ()) ActivateInvestigation
    let s = run s (RecordHypothesis(id "h/a", "A", Some "m"))
    let s = run s (RecordCause(id "c/a", "Outcome", [ { Hypothesis = id "h/a"; Role = RootCause } ], Some "m"))
    let history = s.Session.History
    let last = List.last history
    let forged = history @ [ { last with Version = last.Version + 1L; CommandId = gid "forged"; Body = CausalAssertionConfirmed(id "c/a", Policy.id) } ]
    Assert.Equal(Some "HISTORY.UNGATED_CONFIRMATION", History.firstCode forged)

// ---- capabilities with reasons (UI-004) ----

let private capability (caps: Capability list) action = caps |> List.find (fun c -> c.Action = action)

[<Fact>]
[<Trait("obligation", "VPO-be99a964783c59b5")>]
[<Trait("obligation", "VPO-9659118aa1ef95ff")>]
[<Trait("obligation", "VPO-04eb901270c2994a")>]
[<Trait("task", "T13")>]
let ``T13 UI-004 VPO-be99a964783c59b5 VPO-9659118aa1ef95ff VPO-04eb901270c2994a capabilities come from the decision path with every blocking reason`` () =
    let s = run (start ()) ActivateInvestigation
    let s = run s (RecordHypothesis(id "h/a", "A", None))
    let s = run s (RecordHypothesis(id "h/b", "B", None))
    let s = run s (IdentifyUnknown(id "u/1", "Is the log complete?", Blocking))
    let s = run s (RecordCause(id "c/a", "Outcome", [ { Hypothesis = id "h/a"; Role = ContributingFactor } ], None))
    let inv = state s
    let caps = Projection.capabilities human t0 inv
    let confirm = capability caps (ConfirmCauseAction(id "c/a"))
    Assert.False(confirm.Allowed)
    let codes = confirm.Blockers |> List.map (fun e -> e.Code) |> Set.ofList
    for expected in [ "CAUSE.NO_ROOT"; "CAUSE.MEMBER_NOT_SUPPORTED"; "CAUSE.NO_SUPPORTING_EVIDENCE"; "CAUSE.OPEN_UNKNOWN"; "CAUSE.ALTERNATIVE_NOT_ADDRESSED"; "CAUSE.NO_MECHANISM" ] do
        Assert.Contains(expected, codes)
    // the gate shown is exactly the engine's gate
    Assert.Equal<DomainError list>(Policy.causeBlockers inv (id "c/a"), confirm.Blockers)
    // structural availability matches the table
    Assert.True((capability caps (HypothesisAction(id "h/a", MarkPlausible))).Allowed)
    Assert.False((capability caps (HypothesisAction(id "h/a", MarkSupported))).Allowed)
    Assert.True((capability caps (LifecycleAction MarkAwaitingEvidenceCommand)).Allowed)
    Assert.False((capability caps (LifecycleAction CloseInvestigationCommand)).Allowed)
    let resolve = capability caps (LifecycleAction ResolveInvestigationCommand)
    Assert.False(resolve.Allowed)
    Assert.NotEmpty(resolve.Blockers)
    // an agent viewer may only propose
    let agentCaps = Projection.capabilities agent t0 inv
    Assert.Equal<CapabilityAction list>([ Create RecordProposalKind ], agentCaps |> List.filter (fun c -> c.Allowed) |> List.map (fun c -> c.Action))
    // a resolved investigation offers no content changes, only close/reopen
    let resolved = state (resolvedScript ())
    let allowed = Projection.capabilities human t0 resolved |> List.filter (fun c -> c.Allowed) |> List.map (fun c -> c.Action) |> Set.ofList
    Assert.Equal<Set<CapabilityAction>>(set [ LifecycleAction CloseInvestigationCommand; LifecycleAction ReopenInvestigationCommand ], allowed)

// ---- report honesty (Boundary E, UI-023, UI-038) ----

[<Fact>]
[<Trait("obligation", "VPO-d0e33c8aaaf705f2")>]
[<Trait("obligation", "VPO-f0256bc31283be14")>]
[<Trait("obligation", "VPO-58231f4725864068")>]
[<Trait("obligation", "VPO-f8666daf6fdf79d7")>]
[<Trait("obligation", "VPO-ffda9fea0324a42e")>]
[<Trait("task", "T13")>]
let ``T13 UI-023 VPO-d0e33c8aaaf705f2 VPO-f0256bc31283be14 VPO-58231f4725864068 a cleanly loaded investigation with disputed findings is not verified and nothing is elevated`` () =
    let s = run (start ()) ActivateInvestigation
    let s = run s (RecordHypothesis(id "h/a", "A", Some "m"))
    let s = run s (AdmitEvidence(id "e/1", TextInput "supports", Log, "log", None))
    let s = run s (AdmitEvidence(id "e/2", TextInput "contradicts", Log, "log", None))
    let s = run s (RelateEvidence(id "r/1", id "e/1", id "h/a", Supports, None))
    let s = run s (RelateEvidence(id "r/2", id "e/2", id "h/a", Contradicts, None))
    let s = run s (TransitionHypothesis(id "h/a", MarkPlausible, "fits", []))
    let s = run s (TransitionHypothesis(id "h/a", MarkSupported, "support", [ id "e/1" ]))
    let s = run s (RecordContradiction(id "x/1", "e/1 vs e/2", [ id "e/1"; id "e/2" ], Material))
    let s = run s (RecordCause(id "c/a", "Outcome", [ { Hypothesis = id "h/a"; Role = RootCause } ], Some "m"))
    // the history replays cleanly (technical integrity) ...
    Assert.Empty(History.validate s.Session.History)
    let inv = state s
    let projection = Projection.ofInvestigation human t0 (Some { EventsSha256 = "x"; StateSha256 = "y"; AdmissibilityChecked = true }) inv
    // ... but the domain asserts no verification: integrity and verification are disjoint fields
    Assert.True(projection.Integrity.Value.AdmissibilityChecked)
    Assert.Empty(projection.Verification.ConfirmedCauses)
    Assert.Empty(projection.Verification.VerifiedEffectiveCorrections)
    Assert.False(projection.Verification.Resolved)
    Assert.Contains(projection.Obligations, fun o -> o.Code = "OPEN_CONTRADICTION")
    // the report keeps a supported hypothesis a hypothesis and an unconfirmed cause unconfirmed
    let report = Projection.report inv
    let cause = report.Causes |> List.exactlyOne
    Assert.False(cause.Confirmed)
    Assert.Equal(CauseProposed, cause.Cause.State)
    Assert.Equal(Supported, (fst (List.exactlyOne cause.Members)).State)
    Assert.Single(report.OpenContradictions) |> ignore
    // every explanation carries its typed provenance
    Assert.True(report.Hypotheses |> List.forall (fun h -> h.Provenance.Actor = human))

[<Fact>]
[<Trait("obligation", "VPO-d0e33c8aaaf705f2")>]
[<Trait("task", "T13")>]
let ``T13 UI-023 VPO-d0e33c8aaaf705f2 verification is asserted only by a resolved investigation with a verified-effective correction`` () =
    let inv = state (resolvedScript ())
    let v = Projection.verification inv
    Assert.True(v.Resolved)
    Assert.Equal<CauseId list>([ id "c/1" ], v.ConfirmedCauses)
    Assert.Equal<CorrectiveActionId list>([ id "ca/1" ], v.VerifiedEffectiveCorrections)
    Assert.True((Projection.report inv).Causes |> List.forall (fun c -> c.Confirmed))

// ---- engine-created file sets (EXT-DATA-008) ----

[<Fact>]
[<Trait("obligation", "VPO-61b2e81f99761d55")>]
[<Trait("obligation", "VPO-9c6055c47e4eb07d")>]
[<Trait("obligation", "VPO-5933d986a6ba6dc3")>]
[<Trait("task", "T13")>]
let ``T13 EXT-DATA-008 VPO-61b2e81f99761d55 VPO-9c6055c47e4eb07d a command-created investigation is schema-valid replayable and round-trips`` () =
    // a new empty investigation
    let fresh = start ()
    let check (s: Script) =
        match Storage.write s.Session.History with
        | Error e -> failwithf "write refused: %A" e
        | Ok set ->
            match Storage.read inv.Text (Storage.filesOf set) with
            | Error e -> failwithf "read refused: %A" e
            | Ok loaded ->
                Assert.Equal(s.Session.State, Some loaded.State)
                Assert.Equal<DomainEvent list>(s.Session.History, loaded.Events)
                match Storage.write loaded.Events with
                | Ok again -> Assert.Equal<byte[]>(set.Events, again.Events)
                | Error e -> failwithf "%A" e
    check fresh
    check (resolvedScript ())

[<Fact>]
[<Trait("obligation", "VPO-d7fbd84e40ded145")>]
[<Trait("obligation", "VPO-be22751d9b5b8098")>]
[<Trait("task", "T13")>]
let ``T13 VPO-d7fbd84e40ded145 VPO-be22751d9b5b8098 an append decided against a stale version is refused and nothing is written`` () =
    let s = run (start ()) ActivateInvestigation
    let loaded =
        match Storage.write s.Session.History |> Result.bind (fun set -> Storage.read inv.Text (Storage.filesOf set)) with
        | Ok l -> l
        | Error e -> failwithf "%A" e
    let next =
        match Kernel.submit (Kernel.ofLoaded loaded) (envelope s human (RecordHypothesis(id "h/a", "A", None))) with
        | Ok a -> a.Events
        | Error r -> failwithf "%A" r
    let bytes = Storage.appendBytes next |> Result.defaultWith (fun e -> failwithf "%A" e)
    match Storage.append loaded (loaded.State.Version - 1L) bytes with
    | Error e -> Assert.Equal("CAUSA.VERSION_STALE", e.Code)
    | Ok _ -> failwith "a stale append was accepted"
    match Storage.append loaded loaded.State.Version bytes with
    | Ok(appended, _) -> Assert.Equal(loaded.State.Version + 1L, appended.State.Version)
    | Error e -> failwithf "%A" e

// ---- hashing (browser-safe, identical to the platform) ----

[<Fact>]
[<Trait("obligation", "VPO-5933d986a6ba6dc3")>]
[<Trait("task", "T15")>]
let ``T15 contract 5.2 managed SHA-256 SHA-1 and UUIDv5 equal the platform and the contract namespace`` () =
    let rnd = Random 42
    for n in [ 0; 1; 55; 56; 63; 64; 65; 1000; 100000 ] do
        let data = Array.init n (fun _ -> byte (rnd.Next 256))
        Assert.Equal(Convert.ToHexString(Security.Cryptography.SHA256.HashData data).ToLowerInvariant(), Hashing.sha256Hex data)
        Assert.Equal(Convert.ToHexString(Security.Cryptography.SHA1.HashData data).ToLowerInvariant(), Hashing.hex (Hashing.sha1 data))
    Assert.Equal(Guid "241fefd4-c810-5a3a-9cfa-4bbd968ae1cb", Hashing.eventIdNamespace)
