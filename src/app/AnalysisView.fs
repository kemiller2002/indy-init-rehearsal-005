/// The analysis screens' view keys (S03 hypothesis battlefield, S04 hypothesis detail, S11 cause review, S12 corrective
/// action, S13 verification, S15 semantic diff, S23 persistence conflict recovery, S24 settings semantic manifest).
/// Kept out of View.project (whose locals are at the interpreter's limit) and added to its result by Dispatch.
/// Pure and total: every key here is present in every projection. Legality is only ever the engine's capability (or the
/// injected fixture's); this module chooses words and which record a screen shows, never whether an action is allowed.
module Causa.App.AnalysisView

open Limen.Contract.Core
open Causa.App.Routes
open Causa.App.Adapter
open Causa.App.Presentation
open Causa.App.Shell

let private text (s: string) = ViewValue.Text s
let private flag (b: bool) = ViewValue.Flag b
let private items (xs: ViewItem list) = ViewValue.Items xs
let private item (fields: (string * string) list) : ViewItem = fields |> List.map (fun (k, v) -> k, ViewPrimitive.Text v) |> Map.ofList

let private itemF (fields: (string * string) list) (flags: (string * bool) list) : ViewItem =
    (fields |> List.map (fun (k, v) -> k, ViewPrimitive.Text v)) @ (flags |> List.map (fun (k, v) -> k, ViewPrimitive.Flag v)) |> Map.ofList

let private safeId (s: string) = s.Replace(":", "-")

/// One action control: the capability the engine (or the fixture) projected under any of `ids`, or, when none was
/// projected, an unavailable control with `absent` as its reason. The control is always exposed.
type Control =
    { Id: string
      Name: string
      Label: string
      Legal: bool
      Reason: string
      BlockerHref: string option }

let control (caps: Capability list) (name: string) (label: string) (ids: string list) (absent: string) (blockerHref: string -> string) =
    match caps |> List.tryFind (fun c -> ids |> List.contains c.Id) with
    | Some c ->
        { Id = c.Id
          Name = name
          Label = c.Label
          Legal = c.Legal
          Reason = (if c.Legal then "" else c.Reason |> Option.defaultValue "Not allowed in the current status.")
          BlockerHref = (if c.Legal then None else c.BlockerId |> Option.map blockerHref) }
    | None -> { Id = name; Name = name; Label = label; Legal = false; Reason = absent; BlockerHref = None }

/// A control as a one-item list for the shared capability template (id, name, label, reason, legal/illegal).
let private controlItems (c: Control) =
    [ itemF
          [ "id", c.Id
            "name", c.Name
            "label", c.Label
            "reason", c.Reason
            "reasonId", "an-reason-" + safeId c.Id
            "blockerHref", (c.BlockerHref |> Option.defaultValue "") ]
          [ "legal", c.Legal; "illegal", not c.Legal; "hasBlocker", c.BlockerHref.IsSome ] ]

/// A control behind a form's submit button: flags for the button and its explanation.
let private formKeys (key: string) (c: Control) =
    [ $"an.{key}.disabled", flag (not c.Legal)
      $"an.{key}.illegal", flag (not c.Legal)
      $"an.{key}.reason", text c.Reason
      $"an.{key}.hasBlocker", flag c.BlockerHref.IsSome
      $"an.{key}.blockerHref", text (c.BlockerHref |> Option.defaultValue "") ]

let private rows (xs: DetailRow list) =
    xs |> List.map (fun r -> item [ "id", r.Id; "text", r.Text; "state", r.State ])

let extend (model: Model) (view: ViewState) : ViewState =
    let ws = model.Port.Projection

    let inv, section =
        match View.screenOf model with
        | View.InvestigationScreen(i, s) -> Some i, Some s
        | _ -> None, None

    let on s = section = Some s
    let invId = inv |> Option.map (fun i -> i.Id) |> Option.defaultValue ""
    let caps = inv |> Option.map (fun i -> i.Capabilities) |> Option.defaultValue []
    let detail = inv |> Option.map (fun i -> i.Detail) |> Option.defaultValue noDetail
    let hypotheses = inv |> Option.map (fun i -> i.Hypotheses) |> Option.defaultValue []
    let evidence = inv |> Option.map (fun i -> i.Evidence) |> Option.defaultValue []
    let causes = inv |> Option.map (fun i -> i.Causes) |> Option.defaultValue []
    let blockerAt sec (b: string) = Routes.formatItem (Investigation(invId, sec)) b
    let overviewBlocker = blockerAt Overview
    let field (key: string) = model.CommandInputs |> Map.tryFind key |> Option.defaultValue ""
    let statementOf h = hypotheses |> List.tryFind (fun x -> x.Id = h) |> Option.map (fun x -> x.Statement) |> Option.defaultValue "a hypothesis not in this view"
    let evidenceRows ids =
        evidence |> List.filter (fun e -> ids |> List.contains e.Id) |> List.map (fun e -> item [ "id", e.Id; "text", e.Content; "state", label e.Category ])

    // ---- S03 / S04: hypotheses ----
    let selectedHypothesis = if on Hypotheses then hypotheses |> List.tryFind (fun h -> h.Id = model.Selected) else None

    let support =
        hypotheses
        |> List.map (fun h ->
            let linked = evidence |> List.filter (fun e -> h.Evidence |> List.contains e.Id) |> List.map (fun e -> e.Content)
            item
                [ "id", h.Id
                  "text", h.Statement
                  "state", $"""{count h.Supporting "supporting"}, {count h.Contradicting "contradicting"}"""
                  "linked", (if linked.IsEmpty then "No evidence is linked to it yet." else "Linked evidence: " + String.concat "; " linked) ])

    let falsifiers =
        detail.Predictions
        |> List.map (fun p ->
            let about = p.Related |> List.tryHead |> Option.map statementOf |> Option.defaultValue "a hypothesis not in this view"
            item [ "id", p.Id; "text", p.Text; "state", $"{p.State}. Follows from: {about}" ])

    let prediction = control caps "RecordPrediction" "Record a prediction" [ "RecordPrediction" ] "Recording a prediction is not available for this investigation." overviewBlocker
    let action = control caps "RecordInvestigationAction" "Plan an investigation action" [ "RecordInvestigationAction" ] "Planning an investigation action is not available for this investigation." overviewBlocker

    let hId = selectedHypothesis |> Option.map (fun h -> h.Id) |> Option.defaultValue ""
    let falsify = control caps "FalsifyHypothesis" "Falsify the hypothesis" [ "FalsifyHypothesis"; $"TransitionHypothesis:{hId}:falsify" ] "This hypothesis cannot be falsified in its current state." overviewBlocker
    let reopen = control caps "ReopenHypothesis" "Reopen the hypothesis" [ "ReopenHypothesis"; $"TransitionHypothesis:{hId}:reopen" ] "Only a falsified or superseded hypothesis can be reopened." overviewBlocker

    // ---- S11: cause review (the selected cause, or the first) ----
    let reviewed =
        causes |> List.tryFind (fun c -> c.Id = model.Selected) |> Option.orElse (causes |> List.tryHead)

    let members = reviewed |> Option.map (fun c -> c.Members |> List.map fst) |> Option.defaultValue []
    let memberEvidence = hypotheses |> List.filter (fun h -> members |> List.contains h.Id) |> List.collect (fun h -> h.Evidence) |> List.distinct
    let alternatives = hypotheses |> List.filter (fun h -> not (members |> List.contains h.Id)) |> List.map (fun h -> item [ "id", h.Id; "text", h.Statement; "state", label h.State ])
    let gate = reviewed |> Option.bind (fun c -> detail.CauseGates |> List.tryFind (fun (id, _) -> id = c.Id)) |> Option.map snd

    let confirm =
        let cid = reviewed |> Option.map (fun c -> c.Id) |> Option.defaultValue ""
        control caps "ConfirmCause" "Confirm the cause" [ "ConfirmCause"; "ConfirmCause:" + cid ] "No causal relationship is recorded yet, so there is nothing to confirm." overviewBlocker

    let openQuestions =
        (detail.Unknowns |> List.map (fun u -> item [ "id", u.Id; "text", u.Text; "state", u.State ]))
        @ (detail.Contradictions |> List.map (fun c -> item [ "id", c.Id; "text", c.Text; "state", "Contradiction: " + c.State ]))

    // ---- S12 / S13: corrective actions and verification ----
    let criterion = if on CorrectiveActions then detail.Criteria |> List.tryFind (fun c -> c.Id = model.Selected) else None

    let correction =
        let wanted = criterion |> Option.map (fun c -> c.Correction) |> Option.defaultValue model.Selected
        detail.Corrections |> List.tryFind (fun c -> c.Id = wanted) |> Option.orElse (detail.Corrections |> List.tryHead)

    let caId = correction |> Option.map (fun c -> c.Id) |> Option.defaultValue ""
    let criteriaHere = detail.Criteria |> List.filter (fun c -> c.Correction = caId)

    let recordCorrection = control caps "RecordCorrectiveAction" "Record a corrective action" [ "RecordCorrectiveAction" ] "Recording a corrective action is not available for this investigation." overviewBlocker
    let markImplemented = control caps "MarkCorrectiveActionImplemented" "Mark implemented" [ "MarkCorrectiveActionImplemented"; "MarkCorrectiveActionImplemented:" + caId ] "No corrective action is recorded yet, so none can be marked implemented." overviewBlocker
    let crId = criterion |> Option.map (fun c -> c.Id) |> Option.defaultValue ""
    let verify = control caps "RecordVerification" "Record a verification result" [ "RecordVerification"; "RecordVerification:" + crId ] "No verification criterion is selected." overviewBlocker
    let cited = criterion |> Option.map (fun c -> c.Results |> List.collect snd |> List.distinct) |> Option.defaultValue []
    let causeOutcome c = causes |> List.tryFind (fun x -> x.Id = c) |> Option.map (fun x -> x.Outcome) |> Option.defaultValue "a cause not in this view"

    // ---- S15: semantic diff (the chosen version against the latest) ----
    let history =
        match inv, model.HistoryVersion with
        | Some i, Some v when on Timeline -> model.Port.History i.Id v
        | _ -> None

    let lines xs = xs |> List.mapi (fun k t -> item [ "id", string k; "text", t ])

    // ---- S23: persistence conflict recovery ----
    let conflict = on Overview && ws.Sync = Conflict

    // ---- S08: epistemic blocker detail (an open question or contradiction selected on the overview) ----
    let blocker =
        if not (on Overview) || model.Selected = "" then None
        else
            detail.Unknowns
            |> List.tryFind (fun u -> u.Id = model.Selected)
            |> Option.map (fun u -> "Open question", u)
            |> Option.orElse (detail.Contradictions |> List.tryFind (fun c -> c.Id = model.Selected) |> Option.map (fun c -> "Contradiction", c))

    let blockerId = blocker |> Option.map (fun (_, r) -> r.Id) |> Option.defaultValue ""
    let isContradiction = blocker |> Option.exists (fun (k, _) -> k = "Contradiction")
    let waiting = caps |> List.filter (fun c -> not c.Legal && c.BlockerId = Some blockerId && blockerId <> "")

    let resolveUnknown =
        control caps "ResolveUnknown" "Mark the question answered" [ "ResolveUnknown"; "ResolveUnknown:" + blockerId ]
            (if isContradiction then "This is a contradiction, not an open question." else "This question cannot be marked answered in its current state.") overviewBlocker

    let resolveContradiction =
        control caps "ResolveContradiction" "Mark the contradiction resolved" [ "ResolveContradiction"; "ResolveContradiction:" + blockerId ]
            (if isContradiction then "This contradiction cannot be marked resolved in its current state." else "This is an open question, not a contradiction.") overviewBlocker

    // ---- S09 / S10: investigation actions (designer, and result capture for ?item=<action>) ----
    let actionsHere = on Actions
    let selectedAction = if actionsHere then detail.Actions |> List.tryFind (fun a -> a.Id = model.Selected) else None
    let aId = selectedAction |> Option.map (fun a -> a.Id) |> Option.defaultValue ""
    let actionHref (a: string) = Routes.formatItem (Investigation(invId, Actions)) a
    let targetsText (ids: string list) = if ids.IsEmpty then "No hypothesis is named." else "Tests: " + (ids |> List.map statementOf |> String.concat "; ")
    let prospective = detail.Predictions |> List.filter (fun p -> p.State.Contains "Prospective") |> List.length
    let findingsHere = detail.Findings |> List.filter (fun f -> f.Related |> List.contains aId)
    let recordFinding = control caps "RecordFinding" "Record the result" [ "RecordFinding"; "RecordFinding:" + aId ] "No action is selected, so there is nothing to record a result for." overviewBlocker

    // ---- S19: replay / spectator (read-only, one recorded version at a time) ----
    let replay = on Timeline && model.Selected = "replay"
    let steps = inv |> Option.map (fun i -> i.Timeline |> List.sortBy (fun t -> t.Sequence)) |> Option.defaultValue []
    let step = match model.HistoryVersion with Some v -> Some v | None -> steps |> List.tryHead |> Option.map (fun t -> t.Sequence)
    let replayView = match inv, step with Some i, Some v when replay -> model.Port.History i.Id v | _ -> None
    let stepIndex = steps |> List.tryFindIndex (fun t -> Some t.Sequence = step)
    let stepAt k = if k >= 0 && k < steps.Length then Some steps[k] else None
    let prev = stepIndex |> Option.bind (fun k -> stepAt (k - 1))
    let next = stepIndex |> Option.bind (fun k -> stepAt (k + 1))
    let replayHref (v: int64) = Routes.format (Investigation(invId, Timeline)) + $"?item=replay&version={v}"

    // ---- S17 / S18: multi-agent panel and research tournament (AI proposals ?item=panel / ?item=tournament) ----
    let onProposals = on AiProposals
    let showPanel = onProposals && model.Selected = "panel"
    let showTournament = onProposals && model.Selected = "tournament"
    let mine = ws.Ai.Investigation = Some invId
    let panel = if mine then ws.Ai.Panel else None
    let tournament = if mine then ws.Ai.Tournament else None
    let invVersion = inv |> Option.map (fun i -> i.Version) |> Option.defaultValue 0L
    let canAsk = ws.Ai.KeyHeld && model.Settings.AiModel <> ""
    let askReason =
        if not ws.Ai.KeyHeld then "Enter your provider key in Settings first; it is kept in memory only."
        elif model.Settings.AiModel = "" then "Set the AI model id in Settings first; there is no default model."
        else ""
    let draftsText (ds: DraftRow list) = if ds.IsEmpty then "No drafts." else ds |> List.map (fun d -> $"{d.Kind}: {d.Text}") |> String.concat " · "

    // ---- S24: settings semantic manifest ----
    let pin =
        match ws.Capabilities |> List.tryFind (fun c -> c.Id = "PinPolicyVersion") with
        | Some c -> control [ c ] "PinPolicyVersion" "Pin the policy version" [ "PinPolicyVersion" ] "" (fun b -> Routes.formatItem Settings b)
        | None ->
            control [] "PinPolicyVersion" "Pin the policy version" [] "This release has one built-in policy version, so there is no other version to pin." id

    let wsMaterial = ws.Blockers |> List.filter (fun b -> b.Material)

    let provider =
        match ws.Connection with
        | ConnectedTo(repo, branch, workspace) -> $"Persistence: GitHub repository {repo}, branch {branch}, workspace {workspace}."
        | _ -> "Persistence: not connected."

    let aiProvider =
        if model.Settings.AiModel = "" then "AI proposals: no model id entered, so no provider is called."
        else $"AI proposals: model {model.Settings.AiModel}, called only with the key you enter."

    let keys =
        [ // S03
          "an.hyp.battlefield", flag (on Hypotheses && selectedHypothesis.IsNone)
          "an.hyp.detail", flag selectedHypothesis.IsSome
          "an.support", items support
          "an.support.empty", flag support.IsEmpty
          "an.contradictions", items (rows detail.Contradictions)
          "an.contradictions.empty", flag detail.Contradictions.IsEmpty
          "an.assumptions", items (rows detail.Assumptions)
          "an.assumptions.empty", flag detail.Assumptions.IsEmpty
          "an.falsifiers", items falsifiers
          "an.falsifiers.empty", flag falsifiers.IsEmpty
          "an.form.pred.statement", text (field "RecordPrediction.statement")
          "an.form.act.purpose", text (field "RecordInvestigationAction.purpose")
          // S04
          "an.h.statement", text (selectedHypothesis |> Option.map (fun h -> h.Statement) |> Option.defaultValue "")
          "an.h.state", text (selectedHypothesis |> Option.map (fun h -> label h.State) |> Option.defaultValue "")
          "an.h.tone", text (selectedHypothesis |> Option.map (fun h -> tone h.State) |> Option.defaultValue "neutral")
          "an.h.counts", text (selectedHypothesis |> Option.map (fun h -> $"""{count h.Supporting "supporting"}, {count h.Contradicting "contradicting"}""") |> Option.defaultValue "")
          "an.h.backHref", text (Routes.format (Investigation(invId, Hypotheses)))
          "an.h.evidence", items (selectedHypothesis |> Option.map (fun h -> evidenceRows h.Evidence) |> Option.defaultValue [])
          "an.h.evidence.empty", flag (selectedHypothesis |> Option.forall (fun h -> h.Evidence.IsEmpty))
          "an.h.predictions", items (detail.Predictions |> List.filter (fun p -> p.Related |> List.contains hId) |> rows)
          "an.h.predictions.empty", flag (detail.Predictions |> List.forall (fun p -> not (p.Related |> List.contains hId)))
          "an.negative", items (rows detail.NegativeKnowledge)
          "an.negative.empty", flag detail.NegativeKnowledge.IsEmpty
          "an.form.falsify.reason", text (field "FalsifyHypothesis.reason")
          "an.form.reopen.reason", text (field "ReopenHypothesis.reason")
          // S11
          "an.cause.any", flag reviewed.IsSome
          "an.cause.none", flag reviewed.IsNone
          "an.cause.outcome", text (reviewed |> Option.map (fun c -> c.Outcome) |> Option.defaultValue "")
          "an.cause.state", text (reviewed |> Option.map (fun c -> label c.State) |> Option.defaultValue "")
          "an.cause.tone", text (reviewed |> Option.map (fun c -> tone c.State) |> Option.defaultValue "neutral")
          "an.cause.members",
          text (
              reviewed
              |> Option.map (fun c -> c.Members |> List.map (fun (h, r) -> $"{roleLabel r}: {statementOf h}") |> String.concat "; ")
              |> Option.defaultValue ""
          )
          "an.cause.others", items (causes |> List.filter (fun c -> Some c.Id <> (reviewed |> Option.map (fun r -> r.Id))) |> List.map (fun c -> item [ "id", c.Id; "text", c.Outcome; "href", blockerAt CausalAnalysis c.Id ]))
          "an.cause.evidence", items (evidenceRows memberEvidence)
          "an.cause.evidence.empty", flag memberEvidence.IsEmpty
          "an.cause.alternatives", items alternatives
          "an.cause.alternatives.empty", flag alternatives.IsEmpty
          "an.cause.unknowns", items openQuestions
          "an.cause.unknowns.empty", flag openQuestions.IsEmpty
          "an.cause.gate",
          text (
              match reviewed, gate with
              | None, _ -> "There is no causal relationship to assess against the confirmation policy."
              | Some _, Some [] -> "The confirmation policy has no open requirement for this causal relationship."
              | Some _, Some _ -> "The confirmation policy requires the following before this causal relationship can be confirmed:"
              | Some _, None -> "The confirmation policy's requirements for this causal relationship are not available in this view."
          )
          "an.cause.gates", items (gate |> Option.defaultValue [] |> lines)
          "an.confirm", items (controlItems confirm)
          // S12
          "an.corr.verification", flag criterion.IsSome
          "an.corr.action", flag (on CorrectiveActions && criterion.IsNone)
          "an.ca.any", flag correction.IsSome
          "an.ca.none", flag correction.IsNone
          "an.ca.description", text (correction |> Option.map (fun c -> c.Description) |> Option.defaultValue "No corrective action is recorded yet.")
          "an.ca.state", text (correction |> Option.map (fun c -> c.State) |> Option.defaultValue "")
          "an.ca.kind", text (correction |> Option.map (fun c -> c.Kind) |> Option.defaultValue "Not classified: no corrective action is recorded yet.")
          "an.ca.targets", items (correction |> Option.map (fun c -> c.Targets |> List.map (fun t -> item [ "id", t; "text", causeOutcome t ])) |> Option.defaultValue [])
          "an.ca.targets.empty", flag (correction |> Option.forall (fun c -> c.Targets.IsEmpty))
          "an.ca.others", items (detail.Corrections |> List.filter (fun c -> c.Id <> caId) |> List.map (fun c -> item [ "id", c.Id; "text", c.Description; "href", blockerAt CorrectiveActions c.Id ]))
          "an.ca.criteria",
          items (
              criteriaHere
              |> List.map (fun c -> item [ "id", c.Id; "text", c.Expected; "state", (if c.Satisfied then "Satisfied" else "Not satisfied yet"); "href", blockerAt CorrectiveActions c.Id ])
          )
          "an.ca.criteria.empty", flag criteriaHere.IsEmpty
          "an.markImplemented", items (controlItems markImplemented)
          // S13
          "an.cr.all",
          items (
              criteriaHere
              |> List.map (fun c ->
                  item
                      [ "id", c.Id
                        "text", c.Expected
                        "state", (if c.Satisfied then "Satisfied" else "Not satisfied yet")
                        "href", blockerAt CorrectiveActions c.Id
                        "current", (if c.Id = crId then "true" else "false") ])
          )
          "an.cr.expected", text (criterion |> Option.map (fun c -> c.Expected) |> Option.defaultValue "")
          "an.cr.method", text (criterion |> Option.bind (fun c -> c.Method) |> Option.map (fun m -> "Method: " + m) |> Option.defaultValue "Method: not recorded.")
          "an.cr.results", items (criterion |> Option.map (fun c -> c.Results |> List.map fst |> lines) |> Option.defaultValue [])
          "an.cr.results.empty", flag (criterion |> Option.forall (fun c -> c.Results.IsEmpty))
          "an.cr.evidence", items (evidenceRows cited)
          "an.cr.evidence.empty", flag cited.IsEmpty
          "an.cr.backHref", text (blockerAt CorrectiveActions caId)
          // S15
          "an.timeline.screen", text (if replay then "replay-spectator" elif history.IsSome then "semantic-diff" else "investigation-timeline")
          "an.diff.versions",
          text (
              match history with
              | Some h when h.IsHistorical -> $"Comparing version {h.AtVersion} with the latest version, {h.LatestVersion}."
              | Some h -> $"Version {h.AtVersion} is the latest version; there is nothing later to compare it with."
              | None -> ""
          )
          "an.diff.blockers", items (history |> Option.map (fun h -> lines h.BlockerChanges) |> Option.defaultValue [])
          "an.diff.blockers.empty", flag (history |> Option.forall (fun h -> h.BlockerChanges.IsEmpty))
          "an.diff.capabilities", items (history |> Option.map (fun h -> lines h.CapabilityChanges) |> Option.defaultValue [])
          "an.diff.capabilities.empty", flag (history |> Option.forall (fun h -> h.CapabilityChanges.IsEmpty))
          // S23
          "an.overview.screen", text (if conflict then "persistence-conflict-recovery" elif blocker.IsSome then "epistemic-blocker-detail" else "investigation-overview")
          "an.conflict", flag conflict
          "an.conflict.local", text (inv |> Option.map (fun i -> $"This device: version {i.Version} of “{i.Title}”, including your change. It is kept here and is not saved to GitHub.") |> Option.defaultValue "")
          "an.conflict.remote",
          text (
              match ws.ConnectionStatus |> Option.bind snd with
              | Some d -> "GitHub: " + d
              | None -> "GitHub: the branch holds a version written elsewhere after you opened this investigation. It has not been loaded here, so its content is not shown."
          )
          "an.conflict.pending", text "Your changes since the last confirmed save are held on this device. Nothing is sent to GitHub until you choose below."
          "an.conflict.semantic", text "Whether the two versions disagree in meaning is not decided here: reloading GitHub's version keeps your change so both can be compared, and nothing is merged automatically."
          // S08
          "an.blocker.show", flag blocker.IsSome
          "an.blocker.kind", text (blocker |> Option.map fst |> Option.defaultValue "")
          "an.blocker.text", text (blocker |> Option.map (fun (_, r) -> r.Text) |> Option.defaultValue "")
          "an.blocker.state", text (blocker |> Option.map (fun (_, r) -> r.State) |> Option.defaultValue "")
          "an.blocker.related", items (blocker |> Option.map (fun (_, r) -> evidenceRows r.Related) |> Option.defaultValue [])
          "an.blocker.blocks", items (waiting |> List.map (fun c -> item [ "id", c.Id; "text", c.Label; "state", c.Reason |> Option.defaultValue "" ]))
          "an.blocker.blocks.empty", flag waiting.IsEmpty
          "an.blocker.path",
          text (
              if isContradiction then "Resolve it with a basis that explains the disagreement, or accept it as residual uncertainty. Either is recorded in the history."
              else "Answer it with a basis, or accept it as residual uncertainty. Either is recorded in the history."
          )
          "an.form.resolveUnknown.basis", text (field "ResolveUnknown.basis")
          "an.form.resolveContradiction.basis", text (field "ResolveContradiction.basis")
          // S09
          "an.section.actions", flag actionsHere
          "an.actions.designer", flag (actionsHere && selectedAction.IsNone)
          "an.actions.capture", flag selectedAction.IsSome
          "an.actions.targets",
          items (
              (hypotheses |> List.map (fun h -> item [ "id", h.Id; "text", h.Statement; "state", "Hypothesis: " + label h.State ]))
              @ (detail.Unknowns |> List.map (fun u -> item [ "id", u.Id; "text", u.Text; "state", "Open question: " + u.State ]))
          )
          "an.actions.targets.empty", flag (hypotheses.IsEmpty && detail.Unknowns.IsEmpty)
          "an.actions.list", items (detail.Actions |> List.map (fun a -> item [ "id", a.Id; "text", a.Text; "state", a.State + ". " + targetsText a.Related; "href", actionHref a.Id ]))
          "an.actions.list.empty", flag detail.Actions.IsEmpty
          "an.actions.prereg",
          text (
              match detail.Predictions.Length with
              | 0 -> "No prediction is recorded yet, so nothing is pre-registered."
              | n -> $"{prospective} of {n} predictions were recorded before any finding (prospective); the rest were recorded after one and are marked retrospective."
          )
          "an.actions.cost", text "Cost, time and risk: not recorded. The investigation model has no fields for them, so none are shown."
          // S10
          "an.capture.text", text (selectedAction |> Option.map (fun a -> a.Text) |> Option.defaultValue "")
          "an.capture.state", text (selectedAction |> Option.map (fun a -> a.State + ". " + targetsText a.Related) |> Option.defaultValue "")
          "an.capture.backHref", text (Routes.format (Investigation(invId, Actions)))
          "an.capture.results", items (rows findingsHere)
          "an.capture.results.empty", flag findingsHere.IsEmpty
          "an.capture.anomalies", text "Anomalies: not recorded. The finding record has no anomaly field; note any anomaly in the result text."
          "an.capture.evidence", text "A result is not evidence. To admit it as evidence, record it on the Evidence screen and link it to the hypotheses it bears on."
          "an.capture.evidenceHref", text (Routes.format (Investigation(invId, Evidence)))
          "an.form.finding.result", text (field "RecordFinding.result")
          "an.form.finding.interpretation", text (field "RecordFinding.interpretation")
          // S19
          "an.replay.show", flag replay
          "an.replay.startHref", text (steps |> List.tryHead |> Option.map (fun t -> replayHref t.Sequence) |> Option.defaultValue "")
          "an.replay.problem", text (inv |> Option.bind (fun i -> i.Description) |> Option.defaultValue "The problem statement is not recorded.")
          "an.replay.version",
          text (
              match replayView, stepIndex with
              | Some h, Some k -> $"Step {k + 1} of {steps.Length}: version {h.AtVersion} of {h.LatestVersion}. A past state, shown read-only."
              | _ -> "This version is not available to replay."
          )
          "an.replay.hypotheses", items (replayView |> Option.map (fun h -> h.Hypotheses |> List.mapi (fun k (t, st) -> item [ "id", string k; "text", t; "state", st ])) |> Option.defaultValue [])
          "an.replay.hypotheses.empty", flag (replayView |> Option.forall (fun h -> h.Hypotheses.IsEmpty))
          "an.replay.event",
          text (
              stepIndex
              |> Option.bind stepAt
              |> Option.map (fun t -> $"""#{t.Sequence} {t.EventType}, {t.Actor |> Option.defaultValue "actor not recorded"}, event time {t.OccurredAt}""")
              |> Option.defaultValue ""
          )
          "an.replay.changes", items (replayView |> Option.map (fun h -> lines h.ChangesHere) |> Option.defaultValue [])
          "an.replay.hasPrev", flag prev.IsSome
          "an.replay.prevHref", text (prev |> Option.map (fun t -> replayHref t.Sequence) |> Option.defaultValue "")
          "an.replay.hasNext", flag next.IsSome
          "an.replay.nextHref", text (next |> Option.map (fun t -> replayHref t.Sequence) |> Option.defaultValue "")
          "an.replay.next", text (match next with Some t -> $"Next recorded event: #{t.Sequence} {t.EventType}." | None -> "This is the last recorded event; nothing follows.")
          // S17
          "an.proposals.screen", text (if showPanel then "multi-agent-panel" elif showTournament then "research-tournament" else "investigation-proposals")
          "an.proposals.panelHref", text (Routes.formatItem (Investigation(invId, AiProposals)) "panel")
          "an.proposals.tournamentHref", text (Routes.formatItem (Investigation(invId, AiProposals)) "tournament")
          "an.panel.show", flag showPanel
          "an.panel.source",
          text (
              match panel with
              | Some p -> $"Every role analysed version {p.SourceVersion}, independently, from the same recorded state." + (if p.SourceVersion <> invVersion then $" The investigation is now at version {invVersion}; review the drafts against it." else "")
              | None -> $"No panel has run for this investigation. When it runs, every role analyses version {invVersion}, the version open now."
          )
          "an.panel.roles", items ((match panel with Some p -> p.Roles | None -> EngineView.panelRoles) |> List.mapi (fun k (n, f, v) -> item [ "id", string k; "text", n; "state", f + " Prompt " + v + "." ]))
          "an.panel.answers", items (panel |> Option.map (fun p -> p.Answers |> List.mapi (fun k a -> item [ "id", string k; "text", a.Label + ": " + a.Outcome; "state", draftsText a.Drafts ])) |> Option.defaultValue [])
          "an.panel.answers.empty", flag (panel |> Option.forall (fun p -> p.Answers.IsEmpty))
          "an.panel.running", flag (panel |> Option.exists (fun p -> p.Running))
          "an.panel.disagreements", items (panel |> Option.map (fun p -> lines (p.Disagreements @ p.Agreements)) |> Option.defaultValue [])
          "an.panel.disagreements.empty", flag (panel |> Option.forall (fun p -> p.Disagreements.IsEmpty && p.Agreements.IsEmpty))
          "an.panel.provenance", items (panel |> Option.map (fun p -> p.Answers |> List.mapi (fun k a -> item [ "id", string k; "text", $"{a.Label}: model {a.Model}, prompt {a.PromptVersion}, source version {a.SourceVersion}" ])) |> Option.defaultValue [])
          "an.ai.disabled", flag (not canAsk || (panel |> Option.exists (fun p -> p.Running)) || (tournament |> Option.exists (fun t -> t.Running)))
          "an.ai.reason", text askReason
          // S18
          "an.tour.show", flag showTournament
          "an.tour.experiment",
          text (
              match tournament with
              | Some t -> $"Task: “{t.Task}”. Every treatment ran on version {t.SourceVersion}, frozen when the run started."
              | None -> $"No comparison has run for this investigation. A run sends the same task to each treatment against version {invVersion}, frozen."
          )
          "an.tour.treatments", items (tournament |> Option.map (fun t -> t.Treatments |> List.map (fun r -> item [ "id", r.Id; "text", $"{r.Id}: model {r.Model}, prompt {r.PromptVersion}" ])) |> Option.defaultValue [])
          "an.tour.treatments.empty", flag (tournament |> Option.forall (fun t -> t.Treatments.IsEmpty))
          "an.tour.metrics", items (tournament |> Option.map (fun t -> t.Treatments |> List.map (fun r -> item [ "id", r.Id; "text", r.Id; "state", String.concat " · " r.Measurements ])) |> Option.defaultValue [])
          "an.tour.metrics.empty", flag (tournament |> Option.forall (fun t -> t.Treatments.IsEmpty))
          "an.tour.limitations", items (lines (tournament |> Option.map (fun t -> t.Limitations) |> Option.defaultValue [ "Nothing has been measured yet."; "When a comparison runs, no treatment is ranked or named the winner." ]))
          "an.tour.prompts", items (IndyInitRootCauseInvestigator.Engine.Panel.prompts |> List.map (fun (v, _) -> item [ "id", v; "value", v; "label", v ]))
          "an.form.tour.modelA", text (field "Tournament.modelA")
          "an.form.tour.modelB", text (field "Tournament.modelB")
          // S24
          "an.manifest.semantic", text "Semantic version 1.0.0 of the Causa investigation model (storage contract v1.0.0). It changes only through an approved semantic evolution, never silently."
          "an.manifest.pack", text "No domain pack is installed: the core investigation model applies. Storage contract v1.0.0 allows no domain packs."
          "an.manifest.policy", text "The decision policy built into this release. It is not separately versioned or selectable; AI proposal requests use prompt policy causa.proposal-prompt/1."
          "an.manifest.provider", text provider
          "an.manifest.ai", text aiProvider
          "an.manifest.classification", text "Data classification: not recorded. Storage contract v1.0.0 has no classification field, so none is shown or implied."
          "an.manifest.material", flag (not wsMaterial.IsEmpty)
          "an.manifest.first", text (wsMaterial |> List.tryHead |> Option.map (fun b -> b.Question) |> Option.defaultValue "")
          "an.manifest.questions", text (match wsMaterial.Length with 0 -> "No material open question is recorded." | 1 -> "1 material open question." | n -> $"{n} material open questions.")
          "an.pin", items (controlItems pin) ]
        @ formKeys "pred" prediction
        @ formKeys "act" action
        @ formKeys "falsify" falsify
        @ formKeys "reopen" reopen
        @ formKeys "ca" recordCorrection
        @ formKeys "verify" verify
        @ formKeys "resolveUnknown" resolveUnknown
        @ formKeys "finding" recordFinding
        @ formKeys "resolveContradiction" resolveContradiction

    keys |> List.fold (fun (m: ViewState) (k, v) -> m.Add(k, v)) view
