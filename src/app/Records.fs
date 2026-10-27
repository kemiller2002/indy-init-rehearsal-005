/// Shared typed contracts for relationships, selection, the detail inspector, explanations and next steps
/// (UI-032..UI-039). One module serves every screen: the diagram, the evidence, hypothesis, cause, corrective action
/// and timeline lists all select a `Ref`, and `inspect` turns it into the same `Inspected` shape (UI-039-O1).
///
/// Everything here is read from the engine's typed projection (Boundary B) and history: relationships only where the
/// domain records one, next steps only from the policy's obligations, blockers and capabilities, and explanations only
/// from typed records. Words come from Causa.App.Language. Pure.
module Causa.App.Records

open System
open IndyInitRootCauseInvestigator.Engine
open Causa.App.Routes

/// The engine's projection of one investigation and the history it was folded from.
type Source = { Projection: InvestigationProjection; History: DomainEvent list }

type Kind =
    | EvidenceRecord
    | HypothesisRecord
    | CauseRecord
    | CorrectionRecord
    | EventRecord
    | QuestionRecord
    | ContradictionRecord
    | AssumptionRecord
    | CriterionRecord

/// A stable reference to one record: its kind and the id the domain gave it (events: "event-<sequence>").
type Ref = { Kind: Kind; Id: string }

type Field = { Label: string; Value: string; Missing: bool }

type Link =
    { Relation: string
      Target: Ref
      Text: string
      Href: string }

/// One relation the record may have, with its links or, when there are none, the explicit absent-link wording.
type Relation = { Name: string; Links: Link list; Absent: string }

type Inspected =
    { Ref: Ref
      KindLabel: string
      Heading: string
      State: string
      Fields: Field list
      /// A web link to the original artifact, only when it is a safe absolute http(s) address.
      SafeUrl: string option
      Relations: Relation list
      Explanation: string list }

type NextStep =
    { Text: string
      Why: string
      Target: Ref option
      Href: string
      /// The policy's verdict on the action this step needs: Some(label, allowed, reason).
      Action: (string * bool * string) option }

// ---- untrusted text (UI-005-O3) ----

let private bidi (c: char) = (c >= '‪' && c <= '‮') || (c >= '⁦' && c <= '⁩') || c = '‎' || c = '‏'

/// Untrusted text as it may be shown: control and direction-override characters removed. The kernel binds it as
/// text, never as markup, so nothing in it can become an element or a script.
let clean (s: string) =
    s |> String.filter (fun c -> not (bidi c) && (not (Char.IsControl c) || c = '\n' || c = '\t'))

/// An address that may become a link: absolute http or https, no embedded credentials. Anything else is not linked.
let safeUrl (s: string) =
    match Uri.TryCreate(s.Trim(), UriKind.Absolute) with
    | true, u when (u.Scheme = Uri.UriSchemeHttps || u.Scheme = Uri.UriSchemeHttp) && String.IsNullOrEmpty u.UserInfo -> Some u.AbsoluteUri
    | _ -> None

// ---- references and addresses ----

let kindLabel =
    function
    | EvidenceRecord -> "Evidence"
    | HypothesisRecord -> "Hypothesis"
    | CauseRecord -> "Causal relationship"
    | CorrectionRecord -> "Corrective action"
    | EventRecord -> "Timeline event"
    | QuestionRecord -> "Open question"
    | ContradictionRecord -> "Contradiction"
    | AssumptionRecord -> "Assumption"
    | CriterionRecord -> "Verification criterion"

let sectionOf =
    function
    | EvidenceRecord -> Evidence
    | HypothesisRecord -> Hypotheses
    | CauseRecord -> CausalAnalysis
    | CorrectionRecord
    | CriterionRecord -> CorrectiveActions
    | EventRecord -> Timeline
    | QuestionRecord
    | ContradictionRecord
    | AssumptionRecord -> Overview

let eventRef (version: int64) = { Kind = EventRecord; Id = $"event-{version}" }

let href (investigation: string) (r: Ref) = Causa.App.Routes.formatItem (Causa.App.Routes.Investigation(investigation, sectionOf r.Kind)) r.Id

let private values (m: Map<'K, 'V>) = m |> Map.toList |> List.map snd

let private short (s: string) =
    let s = clean s
    if s.Length <= 90 then s else s.Substring(0, 89) + "…"

let private evidenceText (e: Evidence) =
    match e.Content with
    | TextContent t -> clean t.Value
    | ArtifactContent url -> clean url

/// Resolves a selection id to the record it names, if the investigation holds one.
let resolve (src: Source) (id: string) : Ref option =
    let inv = src.Projection.State
    let has (m: Map<Id<'T>, 'V>) = m |> Map.exists (fun k _ -> k.Text = id)

    if String.IsNullOrEmpty id then None
    elif has inv.Evidence then Some { Kind = EvidenceRecord; Id = id }
    elif has inv.Hypotheses then Some { Kind = HypothesisRecord; Id = id }
    elif has inv.Causes then Some { Kind = CauseRecord; Id = id }
    elif has inv.CorrectiveActions then Some { Kind = CorrectionRecord; Id = id }
    elif has inv.Unknowns then Some { Kind = QuestionRecord; Id = id }
    elif has inv.Contradictions then Some { Kind = ContradictionRecord; Id = id }
    elif has inv.Assumptions then Some { Kind = AssumptionRecord; Id = id }
    elif has inv.Criteria then Some { Kind = CriterionRecord; Id = id }
    elif id.StartsWith("event-", StringComparison.Ordinal) then
        match Int64.TryParse(id.Substring 6) with
        | true, v when src.History |> List.exists (fun e -> e.Version = v) -> Some(eventRef v)
        | _ -> None
    else None

/// The words naming a record in a link.
let title (src: Source) (r: Ref) =
    let inv = src.Projection.State
    let find (m: Map<Id<'T>, 'V>) = m |> Map.tryPick (fun k v -> if k.Text = r.Id then Some v else None)

    match r.Kind with
    | EvidenceRecord -> find inv.Evidence |> Option.map (fun e -> short (evidenceText e))
    | HypothesisRecord -> find inv.Hypotheses |> Option.map (fun h -> short h.Statement.Value)
    | CauseRecord -> find inv.Causes |> Option.map (fun c -> short c.Outcome.Value)
    | CorrectionRecord -> find inv.CorrectiveActions |> Option.map (fun c -> short c.Description.Value)
    | QuestionRecord -> find inv.Unknowns |> Option.map (fun u -> short u.Question.Value)
    | ContradictionRecord -> find inv.Contradictions |> Option.map (fun c -> short c.Statement.Value)
    | AssumptionRecord -> find inv.Assumptions |> Option.map (fun a -> short a.Proposition.Value)
    | CriterionRecord -> find inv.Criteria |> Option.map (fun c -> short c.ExpectedOutcome.Value)
    | EventRecord ->
        match Int64.TryParse(r.Id.Replace("event-", "")) with
        | true, v -> src.History |> List.tryFind (fun e -> e.Version = v) |> Option.map (fun e -> $"#{e.Version} {Wire.eventType e.Body}")
        | _ -> None
    |> Option.defaultValue "a record not in this investigation"

let private link (src: Source) relation (r: Ref) =
    { Relation = relation; Target = r; Text = title src r; Href = href src.Projection.InvestigationId.Text r }

let private relation name absent links = { Name = name; Links = links; Absent = absent }

/// Timeline events that created, changed or cited the record (Timeline.affected), in sequence order.
let eventsTouching (src: Source) (r: Ref) =
    let kinds =
        match r.Kind with
        | EvidenceRecord -> [ "evidence" ]
        | HypothesisRecord -> [ "hypotheses" ]
        | CauseRecord -> [ "causes" ]
        | CorrectionRecord -> [ "correctiveActions" ]
        | QuestionRecord -> [ "unknowns" ]
        | ContradictionRecord -> [ "contradictions" ]
        | AssumptionRecord -> [ "assumptions" ]
        | CriterionRecord -> [ "criteria" ]
        | EventRecord -> []

    src.History
    |> List.sortBy (fun e -> e.Version)
    |> List.filter (fun e -> Timeline.affected e.Investigation e.Body |> List.exists (fun a -> a.Id = r.Id && List.contains a.Kind kinds))
    |> List.map (fun e -> eventRef e.Version)

/// The domain relationships of one record (UI-033-O1), each relation listed even when empty (UI-033-O2).
let relations (src: Source) (r: Ref) : Relation list =
    let inv = src.Projection.State
    let rels = values inv.Relationships
    let l = link src
    let events = relation "Timeline events" "No recorded event touches it." (eventsTouching src r |> List.map (l "Recorded by"))

    match r.Kind with
    | EvidenceRecord ->
        let byKind kind = rels |> List.filter (fun x -> x.Evidence.Text = r.Id && x.Kind = kind) |> List.map (fun x -> l (Language.relationship kind) { Kind = HypothesisRecord; Id = x.Hypothesis.Text })
        [ relation "Hypotheses it supports" "It is not linked as supporting any hypothesis." (byKind Supports)
          relation "Hypotheses it contradicts" "It is not linked as contradicting any hypothesis." (byKind Contradicts)
          relation "Contradictions citing it" "No contradiction cites it."
              (values inv.Contradictions |> List.filter (fun c -> c.Evidence |> List.exists (fun e -> e.Text = r.Id)) |> List.map (fun c -> l "Cited by" { Kind = ContradictionRecord; Id = c.Id.Text }))
          relation "Verifications citing it" "No verification cites it."
              (values inv.Verifications |> List.filter (fun v -> v.Evidence |> List.exists (fun e -> e.Text = r.Id)) |> List.map (fun v -> l "Cited by" { Kind = CriterionRecord; Id = v.Criterion.Text }))
          events ]
    | HypothesisRecord ->
        let byKind kind = rels |> List.filter (fun x -> x.Hypothesis.Text = r.Id && x.Kind = kind) |> List.map (fun x -> l (Language.relationship kind) { Kind = EvidenceRecord; Id = x.Evidence.Text })
        [ relation "Supporting evidence" "No supporting evidence is linked." (byKind Supports)
          relation "Contradicting evidence" "No contradicting evidence is linked." (byKind Contradicts)
          relation "Causal relationships" "It is not part of any causal relationship."
              (values inv.Causes
               |> List.choose (fun c -> c.Members |> List.tryFind (fun m -> m.Hypothesis.Text = r.Id) |> Option.map (fun m -> l (Language.causeRole m.Role) { Kind = CauseRecord; Id = c.Id.Text })))
          events ]
    | CauseRecord ->
        let cause = inv.Causes |> Map.tryPick (fun k v -> if k.Text = r.Id then Some v else None)
        [ relation "Hypotheses in it" "No hypothesis is recorded in it."
              (cause |> Option.map (fun c -> c.Members |> List.map (fun m -> l (Language.causeRole m.Role) { Kind = HypothesisRecord; Id = m.Hypothesis.Text })) |> Option.defaultValue [])
          relation "Corrective actions" "No corrective action targets it."
              (values inv.CorrectiveActions |> List.filter (fun ca -> ca.Targets |> List.exists (fun t -> t.Text = r.Id)) |> List.map (fun ca -> l (Language.correctiveKind ca.Kind) { Kind = CorrectionRecord; Id = ca.Id.Text }))
          events ]
    | CorrectionRecord ->
        let ca = inv.CorrectiveActions |> Map.tryPick (fun k v -> if k.Text = r.Id then Some v else None)
        [ relation "Causes it targets" "It targets no recorded cause."
              (ca |> Option.map (fun c -> c.Targets |> List.map (fun t -> l "Targets" { Kind = CauseRecord; Id = t.Text })) |> Option.defaultValue [])
          relation "Verification criteria" "No verification criterion is recorded for it."
              (values inv.Criteria |> List.filter (fun c -> c.CorrectiveAction.Text = r.Id) |> List.map (fun c -> l "Criterion" { Kind = CriterionRecord; Id = c.Id.Text }))
          events ]
    | ContradictionRecord ->
        let c = inv.Contradictions |> Map.tryPick (fun k v -> if k.Text = r.Id then Some v else None)
        [ relation "Evidence in conflict" "It cites no evidence."
              (c |> Option.map (fun c -> c.Evidence |> List.map (fun e -> l "Cites" { Kind = EvidenceRecord; Id = e.Text })) |> Option.defaultValue [])
          events ]
    | CriterionRecord ->
        let c = inv.Criteria |> Map.tryPick (fun k v -> if k.Text = r.Id then Some v else None)
        [ relation "Corrective action" "Its corrective action is not in this investigation."
              (c |> Option.filter (fun c -> inv.CorrectiveActions.ContainsKey c.CorrectiveAction) |> Option.map (fun c -> [ l "Criterion of" { Kind = CorrectionRecord; Id = c.CorrectiveAction.Text } ]) |> Option.defaultValue [])
          relation "Evidence cited by verifications" "No verification with evidence is recorded for it."
              (values inv.Verifications |> List.filter (fun v -> v.Criterion.Text = r.Id) |> List.collect (fun v -> v.Evidence) |> List.distinct |> List.map (fun e -> l "Cited" { Kind = EvidenceRecord; Id = e.Text }))
          events ]
    | QuestionRecord
    | AssumptionRecord -> [ events ]
    | EventRecord ->
        let affected =
            match Int64.TryParse(r.Id.Replace("event-", "")) with
            | true, v ->
                src.History
                |> List.filter (fun e -> e.Version = v)
                |> List.collect (fun e -> Timeline.affected e.Investigation e.Body)
                |> List.choose (fun a ->
                    let kind =
                        match a.Kind with
                        | "evidence" -> Some EvidenceRecord
                        | "hypotheses" -> Some HypothesisRecord
                        | "causes" -> Some CauseRecord
                        | "correctiveActions" -> Some CorrectionRecord
                        | "unknowns" -> Some QuestionRecord
                        | "contradictions" -> Some ContradictionRecord
                        | "assumptions" -> Some AssumptionRecord
                        | "criteria" -> Some CriterionRecord
                        | _ -> None
                    kind |> Option.map (fun k -> l "Touched" { Kind = k; Id = a.Id }))
            | _ -> []
        [ relation "Records it touched" "It touched no record that has its own view." affected ]

// ---- explanations (UI-038) ----

let private hypothesisHistory (h: Hypothesis) =
    match h.History with
    | [] -> [ $"State history: recorded as {Language.hypothesis Proposed}; no change of state is recorded since." ]
    | changes ->
        changes
        |> List.sortBy (fun c -> c.Version)
        |> List.map (fun c ->
            let cited = if c.Evidence.IsEmpty then "citing no evidence" else $"citing {c.Evidence.Length} evidence item(s)"
            $"State history: {Language.hypothesis c.From} → {Language.hypothesis c.To} at version {c.Version} ({Language.stamp c.At}), {cited}. Reason: {clean c.Reason.Value}")

/// Why a conclusion stands or does not, from typed records only (UI-038-O1/O2).
let explain (src: Source) (r: Ref) : string list =
    let inv = src.Projection.State
    let rels = values inv.Relationships
    let evidenceWords (eid: EvidenceId) =
        match inv.Evidence |> Map.tryFind eid with
        | Some e -> $"\"{short (evidenceText e)}\" (source: {clean e.Source.Value})"
        | None -> "evidence that is not in this investigation"

    let evidenceFor (hid: HypothesisId) kind = rels |> List.filter (fun x -> x.Hypothesis = hid && x.Kind = kind)

    match r.Kind with
    | CauseRecord ->
        match inv.Causes |> Map.tryPick (fun k v -> if k.Text = r.Id then Some v else None) with
        | None -> []
        | Some c ->
            let members =
                c.Members
                |> List.collect (fun m ->
                    match inv.Hypotheses |> Map.tryFind m.Hypothesis with
                    | None -> [ $"{Language.causeRole m.Role}: a hypothesis that is not in this investigation." ]
                    | Some h ->
                        let sup = evidenceFor h.Id Supports
                        let con = evidenceFor h.Id Contradicts
                        [ $"{Language.causeRole m.Role}: \"{short h.Statement.Value}\", currently {Language.hypothesis h.State}."
                          (if sup.IsEmpty then "  Supporting evidence: none is linked." else "  Supporting evidence: " + (sup |> List.map (fun x -> evidenceWords x.Evidence) |> String.concat "; ") + ".")
                          (if con.IsEmpty then "  Conflicting evidence: none is linked." else "  Conflicting evidence: " + (con |> List.map (fun x -> evidenceWords x.Evidence) |> String.concat "; ") + ".") ]
                        @ (hypothesisHistory h |> List.map (fun s -> "  " + s)))

            let mechanism =
                match c.Mechanism with
                | Some m -> $"Mechanism: {clean m.Value}"
                | None -> "Mechanism: not stated."

            let status =
                match c.State with
                | CauseConfirmed -> [ "Status: confirmed by the investigator through the confirmation gate." ]
                | s ->
                    let blockers = Policy.causeBlockers inv c.Id
                    $"Status: {Language.cause s}." :: (if blockers.IsEmpty then [ "Why it is not confirmed: nothing blocks confirmation; it has not been confirmed yet." ] else blockers |> List.map (fun b -> "Why it is not confirmed: " + b.Message))

            let corrections =
                values inv.CorrectiveActions
                |> List.filter (fun ca -> ca.Targets |> List.contains c.Id)
                |> List.map (fun ca -> $"Corrective action: {short ca.Description.Value} ({Language.correctiveKind ca.Kind}, {Language.correctiveState ca.State}).")

            status @ [ mechanism ] @ members @ (if corrections.IsEmpty then [ "Corrective actions: none recorded for this cause." ] else corrections)
    | HypothesisRecord ->
        match inv.Hypotheses |> Map.tryPick (fun k v -> if k.Text = r.Id then Some v else None) with
        | None -> []
        | Some h ->
            let sup = evidenceFor h.Id Supports
            let con = evidenceFor h.Id Contradicts
            [ $"Status: {Language.hypothesis h.State}."
              (if sup.IsEmpty then "Supporting evidence: none is linked." else "Supporting evidence: " + (sup |> List.map (fun x -> evidenceWords x.Evidence) |> String.concat "; ") + ".")
              (if con.IsEmpty then "Conflicting evidence: none is linked." else "Conflicting evidence: " + (con |> List.map (fun x -> evidenceWords x.Evidence) |> String.concat "; ") + ".") ]
            @ hypothesisHistory h
    | ContradictionRecord ->
        match inv.Contradictions |> Map.tryPick (fun k v -> if k.Text = r.Id then Some v else None) with
        | None -> []
        | Some c when Policy.isOpenContradiction c ->
            [ $"Why it is unresolved: it is {Language.contradictionState c.State}; no resolution or accepted residual is recorded."
              $"Materiality: {Language.materiality c.Materiality}." ]
        | Some c -> [ $"Status: {Language.contradictionState c.State}." ] @ (c.Basis |> Option.map (fun b -> [ "Basis: " + clean b.Value ]) |> Option.defaultValue [ "Basis: not recorded." ])
    | QuestionRecord ->
        match inv.Unknowns |> Map.tryPick (fun k v -> if k.Text = r.Id then Some v else None) with
        | None -> []
        | Some u when Policy.isOpenUnknown u -> [ $"Why it is unresolved: it is {Language.unknownState u.State}; no answer or accepted residual is recorded."; $"Materiality: {Language.materiality u.Materiality}." ]
        | Some u -> [ $"Status: {Language.unknownState u.State}." ] @ (u.Basis |> Option.map (fun b -> [ "Basis: " + clean b.Value ]) |> Option.defaultValue [ "Basis: not recorded." ])
    | CorrectionRecord ->
        match inv.CorrectiveActions |> Map.tryPick (fun k v -> if k.Text = r.Id then Some v else None) with
        | None -> []
        | Some ca ->
            let criteria = values inv.Criteria |> List.filter (fun c -> c.CorrectiveAction = ca.Id)
            $"Status: {Language.correctiveState ca.State}."
            :: (if criteria.IsEmpty then [ "Verification: no criterion is recorded, so its effect cannot be verified yet." ]
                else
                    criteria
                    |> List.map (fun c ->
                        let met = Policy.criterionSatisfied inv c
                        let verifs = values inv.Verifications |> List.filter (fun v -> v.Criterion = c.Id) |> List.sortBy (fun v -> v.RecordedAtVersion)
                        let history = if verifs.IsEmpty then "no verification recorded" else verifs |> List.map (fun v -> $"{Language.verificationOutcome v.Outcome} at version {v.RecordedAtVersion}") |> String.concat ", "
                        $"""Criterion "{short c.ExpectedOutcome.Value}": {(if met then "satisfied" else "not satisfied")} ({history})."""))
    | _ -> []

// ---- the inspector (UI-034, UI-005) ----

let private field label value = { Label = label; Value = value; Missing = false }
let private missing label text = { Label = label; Value = text; Missing = true }

let private provenance (p: Provenance) =
    [ field "Recorded by" (clean (Language.actor p.Actor)); field "Recorded at" (Language.stamp p.RecordedAt) ]

let inspect (src: Source) (r: Ref) : Inspected option =
    let inv = src.Projection.State
    let find (m: Map<Id<'T>, 'V>) = m |> Map.tryPick (fun k v -> if k.Text = r.Id then Some v else None)

    let build heading state fields url =
        Some
            { Ref = r
              KindLabel = kindLabel r.Kind
              Heading = heading
              State = state
              Fields = fields
              SafeUrl = url
              Relations = relations src r
              Explanation = explain src r }

    match r.Kind with
    | EvidenceRecord ->
        find inv.Evidence
        |> Option.bind (fun e ->
            let content, url =
                match e.Content with
                | TextContent t -> field "Original content" (clean t.Value), None
                | ArtifactContent u ->
                    match safeUrl u with
                    | Some s -> field "Original artifact" s, Some s
                    | None -> missing "Original artifact" "The recorded address is not a safe web link, so it is not linked.", None

            build (short (evidenceText e)) (Language.evidenceAssessment e.Verification)
                ([ content
                   field "Source" (clean e.Source.Value)
                   field "Category" (Language.evidenceCategory e.Category)
                   (match e.CapturedAt with
                    | Some t -> field "Captured at" (Language.stamp t)
                    | None -> missing "Captured at" "Capture time not recorded")
                   field "Assessment" (Language.evidenceAssessment e.Verification)
                   (match e.SupersededBy |> Option.bind (fun s -> inv.Evidence |> Map.tryFind s) with
                    | Some s -> field "Superseded by" (short (evidenceText s))
                    | None -> field "Superseded by" "Not superseded") ]
                 @ provenance e.Provenance
                 @ [ field "Admitted at version" (string e.AdmittedAtVersion) ])
                url)
    | HypothesisRecord ->
        find inv.Hypotheses
        |> Option.bind (fun h ->
            build (short h.Statement.Value) (Language.hypothesis h.State)
                ([ field "Statement" (clean h.Statement.Value)
                   (match h.Mechanism with
                    | Some m -> field "Mechanism" (clean m.Value)
                    | None -> missing "Mechanism" "Mechanism not stated") ]
                 @ provenance h.Provenance)
                None)
    | CauseRecord ->
        find inv.Causes
        |> Option.bind (fun c ->
            build (short c.Outcome.Value) (Language.cause c.State)
                ([ field "Outcome" (clean c.Outcome.Value)
                   (match c.Mechanism with
                    | Some m -> field "Mechanism" (clean m.Value)
                    | None -> missing "Mechanism" "Mechanism not stated") ]
                 @ provenance c.Provenance)
                None)
    | CorrectionRecord ->
        find inv.CorrectiveActions
        |> Option.bind (fun ca ->
            build (short ca.Description.Value) (Language.correctiveState ca.State)
                ([ field "Description" (clean ca.Description.Value); field "Kind" (Language.correctiveKind ca.Kind) ] @ provenance ca.Provenance)
                None)
    | QuestionRecord ->
        find inv.Unknowns
        |> Option.bind (fun u ->
            build (short u.Question.Value) (Language.unknownState u.State)
                ([ field "Question" (clean u.Question.Value); field "Materiality" (Language.materiality u.Materiality) ] @ provenance u.Provenance)
                None)
    | ContradictionRecord ->
        find inv.Contradictions
        |> Option.bind (fun c ->
            build (short c.Statement.Value) (Language.contradictionState c.State)
                ([ field "Statement" (clean c.Statement.Value); field "Materiality" (Language.materiality c.Materiality) ] @ provenance c.Provenance)
                None)
    | AssumptionRecord ->
        find inv.Assumptions
        |> Option.bind (fun a ->
            build (short a.Proposition.Value) (Language.assumptionState a.State)
                ([ field "Proposition" (clean a.Proposition.Value); field "Materiality" (Language.materiality a.Materiality) ] @ provenance a.Provenance)
                None)
    | CriterionRecord ->
        find inv.Criteria
        |> Option.bind (fun c ->
            build (short c.ExpectedOutcome.Value) (if Policy.criterionSatisfied inv c then "Satisfied" else "Not satisfied")
                ([ field "Expected outcome" (clean c.ExpectedOutcome.Value)
                   (match c.Method with
                    | Some m -> field "Method" (clean m.Value)
                    | None -> missing "Method" "Method not stated") ]
                 @ provenance c.Provenance)
                None)
    | EventRecord ->
        match Int64.TryParse(r.Id.Replace("event-", "")) with
        | true, v ->
            src.History
            |> List.tryFind (fun e -> e.Version = v)
            |> Option.bind (fun e ->
                build $"#{e.Version} {Wire.eventType e.Body}" $"Sequence {e.Version}"
                    [ field "Event" (Wire.eventType e.Body)
                      field "Sequence" (string e.Version)
                      field "Recorded by" (clean (Language.actor e.Actor))
                      field "Event time" (Language.stamp e.At) ]
                    None)
        | _ -> None

// ---- next steps (UI-032) ----

let private capability (p: InvestigationProjection) (action: CapabilityAction) =
    p.Capabilities |> List.tryFind (fun c -> c.Action = action)

let private verdict (p: InvestigationProjection) (label: string) (action: CapabilityAction) =
    capability p action
    |> Option.map (fun c -> label, c.Allowed, (if c.Allowed then "" else Language.unavailable c.Blockers))

/// What the domain says remains to be done, each linked to its record and to the policy's verdict on the action it
/// needs. Nothing is recommended that the projection does not hold.
let nextSteps (src: Source) : NextStep list =
    let p = src.Projection
    let inv = p.State
    let invId = p.InvestigationId.Text
    let step text why target action =
        { Text = text
          Why = why
          Target = target
          Href = (match target with Some r -> href invId r | None -> Causa.App.Routes.format (Causa.App.Routes.Investigation(invId, Causa.App.Routes.CausalAnalysis)))
          Action = action }

    let parse (s: string) = match Guid.TryParse s with | true, g -> Some g | _ -> None

    let obligations =
        p.Obligations
        |> List.choose (fun o ->
            let why = Language.nextStepFor { Code = o.Code; Rule = ""; Message = o.Message }
            match o.Code, parse o.EntityId with
            | "OPEN_CONTRADICTION", Some g ->
                Some(step ("Unresolved contradiction: " + clean o.Message) why (Some { Kind = ContradictionRecord; Id = o.EntityId }) (verdict p "Resolve the contradiction" (ContradictionAction(Id g, ResolveContradiction))))
            | "OPEN_UNKNOWN", Some g ->
                Some(step ("Open question: " + clean o.Message) why (Some { Kind = QuestionRecord; Id = o.EntityId }) (verdict p "Answer the question" (UnknownAction(Id g, ResolveUnknown))))
            | "UNVERIFIED_ASSUMPTION", Some g ->
                Some(step ("Unverified assumption: " + clean o.Message) why (Some { Kind = AssumptionRecord; Id = o.EntityId }) (verdict p "Verify the assumption" (AssumptionAction(Id g, VerifyAssumption))))
            | "UNSATISFIED_CRITERION", Some g ->
                Some(step ("Pending verification: " + clean o.Message) why (Some { Kind = CriterionRecord; Id = o.EntityId }) (verdict p "Record a verification" (RecordVerificationAction(Id g))))
            | _ -> None)

    let missingEvidence =
        values inv.Hypotheses
        |> List.filter (fun h -> (h.State <> Falsified && h.State <> Superseded) && not (inv.Relationships |> Map.exists (fun _ r -> r.Hypothesis = h.Id)))
        |> List.sortBy (fun h -> h.Provenance.RecordedAt, h.Id.Text)
        |> List.map (fun h ->
            step ("Missing evidence: no evidence is linked to the hypothesis \"" + short h.Statement.Value + "\"")
                "Link evidence that supports or contradicts it."
                (Some { Kind = HypothesisRecord; Id = h.Id.Text })
                (verdict p "Link evidence to a hypothesis" (Create RelateEvidenceKind)))

    let pendingVerification =
        values inv.CorrectiveActions
        |> List.filter (fun ca -> ca.State = Implemented || ca.State = AwaitingVerificationOfEffect || ca.State = ReworkRequired || ca.State = VerificationFailed)
        |> List.map (fun ca ->
            step ($"Corrective action \"{short ca.Description.Value}\": {Language.correctiveState ca.State}")
                (match ca.State with
                 | Implemented -> "Start verifying its effect."
                 | ReworkRequired
                 | VerificationFailed -> "Rework it, then verify its effect again."
                 | _ -> "Record a verification against each of its criteria.")
                (Some { Kind = CorrectionRecord; Id = ca.Id.Text })
                (match ca.State with
                 | Implemented -> verdict p "Start verification of effect" (CorrectionAction(ca.Id, StartVerificationOfEffect))
                 | _ -> None))

    let resolution =
        if Transitions.isOpen inv.Lifecycle then
            p.ResolutionBlockers
            |> List.filter (fun b -> b.Code <> "RESOLVE.CRITERION_UNSATISFIED" && b.Code <> "RESOLVE.BLOCKING_UNKNOWN")
            |> List.map (fun b ->
                let target, action =
                    match b.Code with
                    | "RESOLVE.NO_CORRECTION" -> None, verdict p "Record a corrective action" (Create RecordCorrectiveActionKind)
                    | "RESOLVE.NO_CONCLUSION"
                    | "RESOLVE.CONCLUSION_STALE"
                    | "RESOLVE.RESIDUAL_NOT_STATED" -> None, verdict p "Record a conclusion" (Create RecordConclusionKind)
                    | _ -> None, None
                step ("Before resolving: " + b.Message) (Language.nextStepFor b) target action)
        else []

    obligations @ missingEvidence @ pendingVerification @ resolution

// ---- catalog (UI-001, UI-022) ----

/// The open questions a card counts: open unknowns (any materiality).
let openQuestions (inv: Investigation) = inv.Unknowns |> Map.filter (fun _ u -> Policy.isOpenUnknown u) |> Map.count

/// The next lifecycle action the F# policy allows now, if any (UI-001-O4). None: the card offers none.
let nextLegalAction (p: InvestigationProjection) =
    p.Capabilities
    |> List.tryPick (fun c ->
        match c.Action with
        | LifecycleAction cmd when c.Allowed ->
            match cmd with
            | ActivateInvestigationCommand -> Some "Activate the investigation"
            | MarkAwaitingEvidenceCommand -> Some "Mark as awaiting evidence"
            | MarkAwaitingVerificationCommand -> Some "Mark as awaiting verification"
            | ResolveInvestigationCommand -> Some "Resolve the investigation"
            | CloseInvestigationCommand -> Some "Close the investigation"
            | ReopenInvestigationCommand -> Some "Reopen the investigation"
            | VerificationFailedFollowOn -> None
        | _ -> None)
