/// The boundary between the application shell and investigation meaning (the F# engine and persistence).
///
/// The shell decides nothing about investigations: it renders a `WorkspaceProjection` and hands the person's
/// intentions back as `AdapterInput`. Whatever implements `Port` (the engine and persistence streams) owns state,
/// legality, history and saving. Until that lands, `Port.unavailable` is the honest stand-in: it never claims a
/// connection, an investigation or a saved change. `Port` is a value of closures, so swapping implementations does
/// not change the shell's types.
module Causa.App.Adapter

open System
open Limen.Contract.Core

/// Fresh ids, the time and the page-load nonce: everything nondeterministic, supplied by Dispatch.
type Entropy = { Now: DateTime; Ids: Guid list; Nonce: string }

/// A count the domain may not know: unknown is never shown as zero (UI-022-O6).
type Count =
    | Known of int
    | Unknown

type Loadable<'a> =
    | NotRequested
    | Loading
    | Loaded of 'a
    /// What happened and what was preserved, in plain words (UI-010-O2).
    | Failed of reason: string

/// One capability the F# policy decided for the current version (UI-004-O1/O2). The shell renders exactly these.
type Capability =
    { /// Unique among the capabilities shown (e.g. "ConfirmCause:<cause id>").
      Id: string
      /// The command's name (e.g. "ConfirmCause"): the Percepta capability and the command the shell submits.
      Name: string
      /// The one text input the command needs from the person, as its label; None when it needs none.
      Input: string option
      Label: string
      Legal: bool
      /// Why it is unavailable, when the engine supplies a reason.
      Reason: string option
      /// The record (blocker) that explains it, when there is one (UI-032-O2).
      BlockerId: string option }

type CardProjection =
    { Id: string
      Title: string
      /// The domain lifecycle, as the presentation-language key (Presentation.lifecycle).
      Lifecycle: string
      /// A frozen demonstration (read-only) versus a writable copy (UI-014-O3).
      Writable: bool
      LastActivity: string option
      Hypotheses: Count
      Evidence: Count
      Contradictions: Count }

type HypothesisRow =
    { Id: string
      Statement: string
      State: string
      Supporting: Count
      Contradicting: Count
      /// Ids of the evidence the domain relates to this hypothesis (any relationship kind), in admission order
      /// (UI-005-O4, UI-033: the graph links a hypothesis to its own evidence records).
      Evidence: string list }
type EvidenceRow = { Id: string; Content: string; Source: string option; Category: string; RecordedAt: string option }
type BlockerRow = { Id: string; Question: string; Material: bool }
type TimelineRow =
    { Sequence: int64
      EventType: string
      OccurredAt: string
      RecordedAt: string option
      Actor: string option
      /// The event's category (Timeline.category), as a filter key.
      Category: string
      /// Records the event touched, as (section slug, record id, words) for links (UI-003-O4).
      Affected: (string * string * string) list
      /// The event's clock is earlier than an event before it in sequence (UI-003-O3).
      ClockBeforePrevious: bool }

/// The investigation as it was at one version (time travel, T28). Never mixed with the current state.
type HistoryView =
    { AtVersion: int64
      LatestVersion: int64
      IsHistorical: bool
      Title: string
      Lifecycle: string
      Counts: (string * int) list
      /// What the command group at AtVersion changed (before -> after).
      ChangesHere: string list
      /// Everything that changed from AtVersion to the latest version.
      ChangesSince: string list
      /// Open questions that differ between AtVersion and the latest version (S15).
      BlockerChanges: string list
      /// Actions whose availability differs between AtVersion and the latest version (S15).
      CapabilityChanges: string list
      /// The hypotheses as they stood at AtVersion, with their state then (S19 replay).
      Hypotheses: (string * string) list }

type CauseRole =
    | RootCause
    | Trigger
    | ContributingFactor
    | EnablingCondition

/// A causal relationship as the domain records it: hypotheses (members, each with its role) explain one outcome.
type CauseRow = { Id: string; Outcome: string; State: string; Members: (string * CauseRole) list }

/// An agent's proposal as recorded in the investigation; only a human accepts or rejects it (D-004).
type ProposalRow = { Id: string; Kind: string; Text: string; Rationale: string option; State: string; By: string }

/// One drafted proposal from the AI loop, not yet recorded.
type DraftRow = { Kind: string; Text: string; Rationale: string option }

/// The AI proposal loop as the shell shows it (T27). The key itself never appears here.
type AiView =
    { KeyHeld: bool
      InFlight: bool
      /// The investigation the current request or result belongs to.
      Investigation: string option
      /// The last result in plain words (AiLoop.describe), its tone and whether a retry is honest (AiLoop.retryable).
      Result: (string * string * bool) option
      Drafts: DraftRow list
      /// Set when the drafts were made from an older version than the current one.
      Stale: string option
      /// The last request with its key removed (AiLoop.redacted), for Administration.
      LastRequest: string option }

let noAi = { KeyHeld = false; InFlight = false; Investigation = None; Result = None; Drafts = []; Stale = None; LastRequest = None }

/// What the domain itself asserts (Projection.verification, UI-023): never derived from data integrity.
type DomainFindings = { ConfirmedCauses: int; VerifiedCorrections: int; Resolved: bool }

/// The report projection (Projection.report, Boundary E) in words: every explanation from typed records (UI-038).
type ReportView =
    { Conclusion: string option
      Causes: (string * bool * string) list
      OpenQuestions: string list
      AcceptedResiduals: string list
      OpenContradictions: string list
      UnverifiedAssumptions: string list
      Corrections: (string * string) list }

/// One record the analysis screens list (S03, S04, S11): its words, the engine's state for it, and the ids of the
/// entities it concerns (hypotheses for predictions and actions, evidence for contradictions and negative knowledge).
type DetailRow = { Id: string; Text: string; State: string; Related: string list }

/// A corrective action as the corrective-action screen shows it (S12).
type CorrectionRow = { Id: string; Description: string; Kind: string; State: string; Targets: string list }

/// A verification criterion with what was recorded against it (S13). `Satisfied` is the engine's policy answer.
type CriterionRow =
    { Id: string
      Correction: string
      Expected: string
      Method: string option
      Satisfied: bool
      /// Each recorded verification: outcome and the evidence ids it cites.
      Results: (string * string list) list }

/// The records behind the analysis screens. Text and states only; legality stays in Capabilities.
type InvestigationDetail =
    { Contradictions: DetailRow list
      Assumptions: DetailRow list
      Predictions: DetailRow list
      Actions: DetailRow list
      NegativeKnowledge: DetailRow list
      Unknowns: DetailRow list
      /// Per cause id: what the confirmation gate still requires (empty = the gate is open).
      CauseGates: (string * string list) list
      Corrections: CorrectionRow list
      Criteria: CriterionRow list
      /// Findings: the original result (Text), its interpretation (State), and the action it came from (Related).
      Findings: DetailRow list }

let noDetail =
    { Contradictions = []
      Assumptions = []
      Predictions = []
      Actions = []
      NegativeKnowledge = []
      Unknowns = []
      CauseGates = []
      Corrections = []
      Criteria = []
      Findings = [] }

type InvestigationProjection =
    { Id: string
      Title: string
      /// The authoritative aggregate version every view of this investigation was rendered from (UI-013-O1).
      Version: int64
      Lifecycle: string
      Writable: bool
      /// What happened and its impact, when the domain records it (UI-025-O2: unknown is shown as unknown).
      Description: string option
      Hypotheses: HypothesisRow list
      Evidence: EvidenceRow list
      Blockers: BlockerRow list
      Causes: CauseRow list
      Timeline: TimelineRow list
      Proposals: ProposalRow list
      /// Domain findings, when the engine projected them; None for a shell-only fixture.
      Findings: DomainFindings option
      /// Technical integrity of how the data was obtained, for Administration only (UI-023-O2).
      Integrity: string option
      Report: ReportView option
      Capabilities: Capability list
      /// Records behind the analysis screens (S03, S04, S11, S12, S13).
      Detail: InvestigationDetail }

/// Distinct save and sync states (UI-006-O1). Only `RemotelySynced` may say the provider holds the change.
type SyncStatus =
    | NoChanges
    | Unsaved of count: int
    | Saving
    | RemotelySynced of at: string option
    | Conflict
    | SaveFailed of reason: string
    /// The provider's answer to a save was lost: it may or may not have reached GitHub. No Retry is offered (a
    /// retry could write twice); only "check now", which re-reads the branch before anything else is sent.
    | MayHaveSaved of detail: string
    /// Local fixture mode: the change is kept in this page only; nothing is saved to GitHub (PF-003-O4).
    | LocalOnly of detail: string
    | ReadOnly

type Connection =
    | NotConnected
    | Connecting
    | ConnectedTo of repository: string * branch: string * workspace: string
    | ConnectionFailed of reason: string

/// An Administration fact: state is one of Verified, Failed, Unknown or Unavailable (UI-018-O3). Section is one of
/// deployment, persistence, ai, report, security, smoke-tests.
type AdminFact = { Id: string; Section: string; Label: string; Value: string; State: string }

type WorkspaceProjection =
    { Connection: Connection
      /// The exact connection state in words and its detail, when persistence reports them (EXT-DATA-007).
      ConnectionStatus: (string * string option) option
      Investigations: Loadable<CardProjection list>
      Opened: Map<string, Loadable<InvestigationProjection>>
      Sync: SyncStatus
      /// Workspace-scoped capabilities decided by the F# policy (e.g. StartInvestigation), and what blocks them.
      Capabilities: Capability list
      Blockers: BlockerRow list
      /// A short, plain-language message for the person, when the adapter has one.
      Message: (string * string) option
      Ai: AiView
      Administration: AdminFact list }

type ConnectRequest = { Token: string; Repository: string; Branch: string; Workspace: string }

type AdapterInput =
    | Connect of ConnectRequest
    | Disconnect
    | Open of id: string
    /// A semantic command with its form fields and the version the form was rendered from (Boundary A).
    | Command of investigation: string option * name: string * fields: Map<string, string> * expectedVersion: int64
    | Answer of correlation: string * EffectResult
    /// "Check now": re-read the branch to settle a save whose outcome is unknown (MayHaveSaved).
    | CheckRemote
    /// The provider key typed on the key screen: held in memory by the port only, never in the shell's model.
    | SetAiKey of key: string
    | ClearAiKey
    /// Ask the AI for proposals about an investigation with the configured model (never a default).
    | RequestProposals of investigation: string * model: string * task: string
    | CancelProposals
    /// Record the drafted proposals (still proposals: a human accepts each one) or discard them.
    | RecordDrafts of investigation: string * model: string
    | DiscardDrafts
    /// Conflict resolution: drop this device's change to the investigation and adopt the repository's copy.
    | DiscardLocal of id: string
    /// Re-read the investigation from the repository (after a conflict), keeping this device's change.
    | ReloadRemote of id: string
    /// Page start: restore this device's unsaved work before anything else (call once from Initialize).
    | Boot

/// The engine and persistence as the shell sees them.
type Port =
    { Name: string
      Projection: WorkspaceProjection
      /// The state of an open investigation at a version, when the port can derive it (T28).
      History: string -> int64 -> HistoryView option
      Handle: Entropy -> AdapterInput -> Port * EffectRequest list }

let emptyWorkspace =
    { Connection = NotConnected
      ConnectionStatus = None
      Investigations = NotRequested
      Opened = Map.empty
      Sync = NoChanges
      Capabilities = []
      Blockers = []
      Message = None
      Ai = noAi
      Administration = [] }

module Port =
    /// The shell's stand-in until the engine and persistence ports are composed in. It refuses Connect in plain
    /// words and claims nothing: no connection, no investigation, no saved change.
    let private unavailableProjection =
        { emptyWorkspace with
            Administration =
                [ { Id = "engine"; Section = "persistence"; Label = "Investigation engine"; Value = "Not composed into this build"; State = "Unavailable" }
                  { Id = "provider"; Section = "persistence"; Label = "Data provider"; Value = "Not composed into this build"; State = "Unavailable" } ] }

    let rec private unavailableAt (projection: WorkspaceProjection) : Port =
        { Name = "unavailable"
          History = fun _ _ -> None
          Projection = projection
          Handle =
            fun _ input ->
                match input with
                | Connect _ ->
                    unavailableAt
                        { unavailableProjection with
                            Connection = ConnectionFailed "Connecting to a data repository is not available in this build yet. Nothing was sent."
                            Message = Some("warning", "Connecting is not available in this build yet. Nothing was sent and nothing was stored.") },
                    []
                | _ -> unavailableAt unavailableProjection, [] }

    let unavailable: Port = unavailableAt unavailableProjection

    /// A port that only shows a fixed projection (Percepta state injection and shell tests). Every input is refused.
    let rec fixedAt (projection: WorkspaceProjection) : Port =
        { Name = "fixture"
          History = fun _ _ -> None
          Projection = projection
          Handle = fun _ _ -> fixedAt projection, [] }
