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

type HypothesisRow = { Id: string; Statement: string; State: string; Supporting: Count; Contradicting: Count }
type EvidenceRow = { Id: string; Content: string; Source: string option; Category: string; RecordedAt: string option }
type BlockerRow = { Id: string; Question: string; Material: bool }
type TimelineRow = { Sequence: int64; EventType: string; OccurredAt: string; RecordedAt: string option; Actor: string option }

type CauseRole =
    | RootCause
    | Trigger
    | ContributingFactor
    | EnablingCondition

/// A causal relationship as the domain records it: hypotheses (members, each with its role) explain one outcome.
type CauseRow = { Id: string; Outcome: string; State: string; Members: (string * CauseRole) list }

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
      Capabilities: Capability list }

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
      Investigations: Loadable<CardProjection list>
      Opened: Map<string, Loadable<InvestigationProjection>>
      Sync: SyncStatus
      /// Workspace-scoped capabilities decided by the F# policy (e.g. StartInvestigation), and what blocks them.
      Capabilities: Capability list
      Blockers: BlockerRow list
      /// A short, plain-language message for the person, when the adapter has one.
      Message: (string * string) option
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

/// The engine and persistence as the shell sees them.
type Port =
    { Name: string
      Projection: WorkspaceProjection
      Handle: Entropy -> AdapterInput -> Port * EffectRequest list }

let emptyWorkspace =
    { Connection = NotConnected
      Investigations = NotRequested
      Opened = Map.empty
      Sync = NoChanges
      Capabilities = []
      Blockers = []
      Message = None
      Administration = [] }

module Port =
    /// The shell's stand-in until the engine and persistence ports are composed in. It refuses Connect in plain
    /// words and claims nothing: no connection, no investigation, no saved change.
    let rec unavailable: Port =
        { Name = "unavailable"
          Projection =
            { emptyWorkspace with
                Administration =
                    [ { Id = "engine"; Section = "persistence"; Label = "Investigation engine"; Value = "Not composed into this build"; State = "Unavailable" }
                      { Id = "provider"; Section = "persistence"; Label = "Data provider"; Value = "Not composed into this build"; State = "Unavailable" } ] }
          Handle =
            fun _ input ->
                match input with
                | Connect _ ->
                    { unavailable with
                        Projection =
                            { unavailable.Projection with
                                Connection = ConnectionFailed "Connecting to a data repository is not available in this build yet. Nothing was sent."
                                Message = Some("warning", "Connecting is not available in this build yet. Nothing was sent and nothing was stored.") } },
                    []
                | _ -> unavailable, [] }

    /// A port that only shows a fixed projection (Percepta state injection and shell tests). Every input is refused.
    let rec fixedAt (projection: WorkspaceProjection) : Port =
        { Name = "fixture"
          Projection = projection
          Handle = fun _ _ -> fixedAt projection, [] }
