/// The presentation-language mapping (UI-024-O1): the one place typed F# domain, projection, error and sync states
/// become the words a person reads. Every function here is total over a closed type, so a new domain state is a
/// compile error here rather than a template guessing at its meaning (UI-024-O2). Views and agents pick from these
/// words; they never compose a meaning, a confidence, a permission or a success claim of their own.
///
/// Rules this module keeps (and Causa.App.Tests/LanguageTests.fs checks):
/// - "Verified" is said only of what the domain asserts verified (an evidence assessment, a verified assumption, a
///   correction verified effective). Technical data integrity is a different vocabulary, for Administration only
///   (UI-023-O3).
/// - Nothing is called saved unless the provider's copy was re-read and confirmed (UI-024-O4).
/// - Routine investigation wording carries no technical codes, hashes, schema or replay vocabulary (UI-024-O5);
///   `technicalTerms` is the list a test holds every routine label against.
/// - Domain terms that carry meaning (root cause, contributing factor, residual uncertainty, provenance) are kept
///   precisely rather than paraphrased (UI-024-O6).
module Causa.App.Language

open System
open IndyInitRootCauseInvestigator.Engine

// ---- lifecycle and record states (domain-authoritative) ----

let lifecycle =
    function
    | Draft -> "Draft"
    | Active -> "Active"
    | AwaitingEvidence -> "Awaiting evidence"
    | AwaitingVerification -> "Awaiting verification"
    | Resolved -> "Resolved"
    | Closed -> "Closed"
    | Archived -> "Archived"

/// One sentence on what the lifecycle means for the person's work.
let lifecycleExplanation =
    function
    | Draft -> "Being set up; not yet active."
    | Active -> "Open: evidence, hypotheses and causes are being worked on."
    | AwaitingEvidence -> "Open, waiting for evidence that has been asked for."
    | AwaitingVerification -> "Open, waiting for corrective actions to be verified."
    | Resolved -> "Resolved: a confirmed cause and a verified correction are recorded."
    | Closed -> "Closed: no further changes are expected."
    | Archived -> "Archived: kept for reference only."

let hypothesis =
    function
    | Proposed -> "Proposed"
    | Plausible -> "Plausible"
    | Supported -> "Supported"
    | Weakened -> "Weakened"
    | Falsified -> "Falsified"
    | Superseded -> "Superseded"

let cause =
    function
    | CauseProposed -> "Proposed, not confirmed"
    | CauseConfirmed -> "Confirmed"
    | CauseReopened -> "Reopened, not confirmed"
    | CauseSuperseded -> "Superseded"

let causeRole =
    function
    | RootCause -> "Root cause"
    | ContributingFactor -> "Contributing factor"
    | Trigger -> "Trigger"
    | EnablingCondition -> "Enabling condition"

let evidenceCategory =
    function
    | DirectObservation -> "Direct observation"
    | Measurement -> "Measurement"
    | Log -> "Log"
    | Document -> "Document"
    | ExperimentResult -> "Experiment result"
    | Testimony -> "Testimony"
    | ExternalReference -> "External reference"

/// The investigator's assessment of one piece of evidence (a domain fact, recorded by a command).
let evidenceAssessment =
    function
    | Unverified -> "Not yet assessed"
    | PartiallyVerified -> "Partly verified"
    | Verified -> "Verified by assessment"
    | Invalidated -> "Invalidated"

let relationship =
    function
    | Supports -> "Supports"
    | Contradicts -> "Contradicts"
    | Discriminates -> "Discriminates between hypotheses"
    | Insufficient -> "Insufficient to decide"

let materiality =
    function
    | Blocking -> "Blocking"
    | Material -> "Material"
    | NonMaterial -> "Not material"
    | Informational -> "Informational"

let unknownState =
    function
    | Identified -> "Open"
    | Investigating -> "Being investigated"
    | UnknownResolved -> "Answered"
    | AcceptedResidual -> "Accepted as residual uncertainty"
    | UnknownSuperseded -> "Superseded"

let contradictionState =
    function
    | Detected -> "Unresolved"
    | Triaged -> "Unresolved, triaged"
    | ContradictionInvestigating -> "Unresolved, being investigated"
    | ContradictionResolved -> "Resolved"
    | ContradictionAcceptedResidual -> "Accepted as residual uncertainty"
    | ContradictionSuperseded -> "Superseded"

let assumptionState =
    function
    | ExplicitUnverified -> "Not verified"
    | ProvisionallyAccepted -> "Provisionally accepted"
    | AssumptionVerified -> "Verified"
    | AssumptionRejected -> "Rejected"
    | Expired -> "Expired"

let correctiveKind =
    function
    | Mitigation -> "Mitigation"
    | Workaround -> "Workaround"
    | Correction -> "Root-cause correction"
    | Prevention -> "Prevention"

let correctiveState =
    function
    | CorrectionProposed -> "Proposed"
    | CorrectionApproved -> "Approved"
    | CorrectionInProgress -> "In progress"
    | Implemented -> "Implemented, effect not verified"
    | AwaitingVerificationOfEffect -> "Awaiting verification of effect"
    | VerifiedEffective -> "Verified effective"
    | VerificationFailed -> "Verification failed"
    | ReworkRequired -> "Rework required"
    | CorrectionCancelled -> "Cancelled"

let actionState =
    function
    | ActionProposed -> "Proposed"
    | ActionApproved -> "Approved"
    | ActionInProgress -> "In progress"
    | ActionCompleted -> "Completed"
    | ActionInterpreted -> "Interpreted"
    | ActionRejected -> "Rejected"
    | ActionCancelled -> "Cancelled"

let verificationOutcome =
    function
    | Satisfied -> "Criterion satisfied"
    | VerificationOutcome.Failed -> "Criterion not met"
    | Inconclusive -> "Inconclusive"

let conclusionStrength =
    function
    | PlausibleExplanation -> "Plausible explanation"
    | LeadingExplanation -> "Leading explanation"
    | SupportedCause -> "Supported cause"
    | ConfirmedCause -> "Confirmed cause"
    | AcceptedWithResidualUncertainty -> "Accepted with residual uncertainty"

let proposalState =
    function
    | ProposalActive -> "Awaiting your decision"
    | ProposalAccepted -> "Accepted by a person"
    | ProposalRejected -> "Rejected"

/// Who recorded something, as provenance (UI-024-O6): an agent is always named as an agent.
let actor (a: Actor) =
    match a.Kind with
    | Human -> a.ActorId
    | System -> $"{a.ActorId} (system)"
    | Agent(provider, model) -> $"{a.ActorId} (AI agent, {provider} {model})"

/// A timestamp as provenance: always UTC, always labelled, never reformatted into a relative phrase.
let stamp (t: DateTimeOffset) = t.UtcDateTime.ToString("yyyy-MM-dd HH:mm 'UTC'")

// ---- findings versus data integrity (UI-023) ----

/// What the domain asserts about the investigation's findings (Projection.verification). This is the only source of
/// investigation-level "verified" wording.
let findings (v: DomainVerification) : string list =
    [ (match v.ConfirmedCauses.Length with
       | 0 -> "No cause is confirmed."
       | 1 -> "1 cause confirmed."
       | n -> $"{n} causes confirmed.")
      (match v.VerifiedEffectiveCorrections.Length with
       | 0 -> "No correction is verified effective."
       | 1 -> "1 correction verified effective."
       | n -> $"{n} corrections verified effective.")
      (if v.Resolved then "Resolved." else "Not resolved.") ]

/// Technical integrity of how the data was read (Administration only). Deliberately never the word "Verified": a
/// file that was fetched, validated and replayed says nothing about whether the investigation's findings hold.
let integrity (i: DataIntegrity option) =
    match i with
    | Some d when d.AdmissibilityChecked -> "Integrity check passed (history replayed and admissible)"
    | Some _ -> "Integrity not checked"
    | None -> "Not read from a repository; no integrity check applies"

// ---- authority and next steps ----

/// The next step a policy blocker asks for, in words, by the engine's stable code. Unknown codes fall back to the
/// engine's own message: the UI never invents a recommendation (UI-032-O2).
let nextStepFor (e: DomainError) =
    match e.Code with
    | "OPEN_UNKNOWN" -> "Answer this open question, or accept it as residual uncertainty."
    | "OPEN_CONTRADICTION" -> "Resolve this contradiction, or accept it as residual uncertainty."
    | "UNVERIFIED_ASSUMPTION" -> "Verify or reject this assumption."
    | "UNSATISFIED_CRITERION" -> "Record a verification with fresh evidence for this criterion."
    | "RESOLVE.NO_CONFIRMED_CAUSE" -> "Confirm a cause before resolving."
    | "RESOLVE.NO_CORRECTION" -> "Record a root-cause correction for the confirmed cause."
    | "RESOLVE.CORRECTION_NOT_VERIFIED" -> "Verify that a correction was effective."
    | "RESOLVE.CRITERION_UNSATISFIED" -> "Satisfy the correction's verification criterion."
    | "RESOLVE.BLOCKING_UNKNOWN" -> "Answer the blocking open question."
    | "RESOLVE.NO_CONCLUSION" -> "Record a conclusion, with any residual uncertainty."
    | "RESOLVE.CAUSE_NO_LONGER_HOLDS" -> "Reopen and reassess the confirmed cause."
    | "RESOLVE.CONCLUSION_STALE" -> "Record a new conclusion."
    | "RESOLVE.RESIDUAL_NOT_STATED" -> "State the residual uncertainty in the conclusion."
    | _ -> e.Message

/// Why something is not available: the engine's own message (never a guess), or a plain statement when the
/// engine gave none.
let unavailable (blockers: DomainError list) =
    match blockers |> List.map (fun e -> e.Message) |> List.filter (String.IsNullOrWhiteSpace >> not) with
    | [] -> "Not available now; no reason was given."
    | ms -> String.concat " " ms

// ---- durability and sync (UI-024-O4) ----

/// How an investigation shown on screen is held. Only `Persisted` may be called saved.
type Durability =
    /// A frozen demonstration: nothing typed here changes it.
    | ReadOnlyDemonstration
    /// Held in this tab only (e.g. the test build's in-memory workspace): nothing is saved anywhere.
    | ThisTabOnly
    /// A working copy whose provider copy was read and confirmed at this time.
    | Persisted of confirmedAt: string option
    | NotYetSaved of changes: int

let durability =
    function
    | ReadOnlyDemonstration -> "Read-only demonstration: nothing you do here changes it."
    | ThisTabOnly -> "Held in this tab only. Nothing is saved anywhere."
    | Persisted(Some at) -> $"Saved and confirmed at {at}."
    | Persisted None -> "Saved and confirmed."
    | NotYetSaved 1 -> "1 change not saved yet."
    | NotYetSaved n -> $"{n} changes not saved yet."

/// The save state of a working copy, from the Sync machine's own phase. Writing and confirming are never "saved";
/// a lost answer is "may or may not have been saved"; a conflict keeps the person's work and says so.
let syncPhase (phase: Sync.Phase) =
    match phase with
    | Sync.Disconnected -> "Not connected: nothing can be saved."
    | Sync.Connecting _ -> "Connecting…"
    | Sync.ConnectRefused _ -> "Not connected: the repository could not be opened."
    | Sync.Idle(_, None) -> "No unsaved changes."
    | Sync.Idle(_, Some _) -> "Changes not saved yet."
    | Sync.Writing _ -> "Saving… not confirmed yet."
    | Sync.Confirming _ -> "Saving… checking that the repository holds it."
    | Sync.Unknown _ -> "Your changes may or may not have been saved; the repository is checked before anything else is sent."
    | Sync.Reconciling _ -> "Checking whether your changes were saved…"
    | Sync.Held(_, _, Sync.Conflict _) -> "Not saved: the repository changed elsewhere. Your changes are kept."
    | Sync.Held _ -> "Not saved. Your changes are kept."

// ---- what routine investigation copy must not say (UI-017-O2, UI-024-O5) ----

/// Words that belong to Administration or a technical detail view, never to routine investigation wording.
let technicalTerms =
    [ "SHA"
      "sha256"
      "commit"
      "schema"
      "replay"
      "replayed"
      "catalog"
      "historical-v1"
      "manifest"
      "NDJSON"
      "events.ndjson"
      "WebAssembly"
      "WASM"
      ".NET"
      "Limen"
      "Forma"
      "GitHub API"
      "HTTP"
      "CAUSA."
      "RESOLVE."
      "Integrity check" ]

/// The technical terms a piece of routine copy contains (empty is the only acceptable answer).
let leaks (copy: string) =
    technicalTerms
    |> List.filter (fun t ->
        Text.RegularExpressions.Regex.IsMatch(copy, "(?<![A-Za-z])" + Text.RegularExpressions.Regex.Escape t + "(?![A-Za-z])", Text.RegularExpressions.RegexOptions.IgnoreCase))
