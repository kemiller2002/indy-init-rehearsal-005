/// The presentation-language contract (UI-024-O1): the one place typed domain, projection and error states become
/// user-facing words. Views never invent meanings; an unmapped value is shown as "Not recorded" rather than raw.
module Causa.App.Presentation

/// Domain values (contract enum text) to concise labels. Unknown values are never echoed raw into the workflow.
let label (value: string) =
    match value with
    | "draft" -> "Draft"
    | "active" -> "Active"
    | "awaitingEvidence" -> "Awaiting evidence"
    | "awaitingVerification" -> "Awaiting verification"
    | "resolved" -> "Resolved"
    | "closed" -> "Closed"
    | "archived" -> "Archived"
    | "proposed" -> "Proposed"
    | "plausible" -> "Plausible"
    | "supported" -> "Supported"
    | "weakened" -> "Weakened"
    | "falsified" -> "Falsified"
    | "superseded" -> "Superseded"
    | "confirmed" -> "Confirmed"
    | "reopened" -> "Reopened"
    | "unverified" -> "Not yet verified"
    | "partiallyVerified" -> "Partly verified"
    | "verified" -> "Verified"
    | "invalidated" -> "Invalidated"
    | "identified" -> "Open"
    | "investigating" -> "Investigating"
    | "acceptedResidual" -> "Accepted as residual"
    | "directObservation" -> "Direct observation"
    | "measurement" -> "Measurement"
    | "log" -> "Log"
    | "document" -> "Document"
    | "experimentResult" -> "Experiment result"
    | "testimony" -> "Testimony"
    | "externalReference" -> "External reference"
    | "supports" -> "Supports"
    | "contradicts" -> "Contradicts"
    | _ -> "Not recorded"

/// The tone a lifecycle or state carries (UI-016): always paired with its text label, never colour alone.
let tone (value: string) =
    match value with
    | "falsified"
    | "invalidated"
    | "weakened" -> "conflict"
    | "awaitingEvidence"
    | "awaitingVerification"
    | "proposed"
    | "plausible"
    | "unverified"
    | "partiallyVerified"
    | "identified"
    | "investigating"
    | "draft" -> "attention"
    | "supported"
    | "confirmed"
    | "verified"
    | "resolved" -> "action"
    | _ -> "neutral"

let roleLabel (role: Adapter.CauseRole) =
    match role with
    | Adapter.RootCause -> "Root cause"
    | Adapter.Trigger -> "Trigger"
    | Adapter.ContributingFactor -> "Contributing factor"
    | Adapter.EnablingCondition -> "Enabling condition"

/// UI-022-O6: unknown counts are never presented as zero.
let count (c: Adapter.Count) (noun: string) =
    match c with
    | Adapter.Known n -> $"{n} {noun}"
    | Adapter.Unknown -> $"{noun}: not known"

let syncText (s: Adapter.SyncStatus) =
    match s with
    | Adapter.NoChanges -> "No changes"
    | Adapter.Unsaved n -> $"{n} unsaved change(s)"
    | Adapter.Saving -> "Saving…"
    | Adapter.RemotelySynced(Some at) -> $"Saved to GitHub (confirmed {at})"
    | Adapter.RemotelySynced None -> "Saved to GitHub"
    | Adapter.Conflict -> "Not saved: someone else changed the repository. Your changes are kept."
    | Adapter.SaveFailed reason -> "Not saved. " + reason
    | Adapter.MayHaveSaved _ -> "Your changes may or may not have reached GitHub; checking the branch before anything else is sent."
    | Adapter.LocalOnly detail -> detail
    | Adapter.ReadOnly -> "Read-only demonstration"

let syncTone (s: Adapter.SyncStatus) =
    match s with
    | Adapter.Conflict
    | Adapter.SaveFailed _ -> "conflict"
    | Adapter.MayHaveSaved _ -> "unknown"
    | Adapter.Unsaved _
    | Adapter.Saving -> "attention"
    | Adapter.RemotelySynced _ -> "action"
    | Adapter.NoChanges
    | Adapter.LocalOnly _
    | Adapter.ReadOnly -> "neutral"

let connectionText (c: Adapter.Connection) =
    match c with
    | Adapter.NotConnected -> "Not connected"
    | Adapter.Connecting -> "Connecting…"
    | Adapter.ConnectedTo(repo, branch, _) -> $"{repo} · {branch}"
    | Adapter.ConnectionFailed _ -> "Not connected"

/// UI-024-O3 default labels.
module Terms =
    let investigations = "Investigations"
    let opening = "Opening investigation"
    let status = "Status"
    let history = "Investigation history"
    let exploreChanges = "Explore changes"
    let changeHistory = "Change history"
    let discardDraft = "Discard draft"
