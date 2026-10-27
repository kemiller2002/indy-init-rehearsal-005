namespace Causa.Persistence

open System

/// The storage contract's normative state fold (causa-data CONTRACT §8.3): wire events -> wire `state` object.
///
/// This is a *storage* rule, not domain policy: it rebuilds the derived snapshot so that a reader can check
/// `snapshot.json` (§8.1, stage 17) and a writer can recompute it after an append (§9.2). It decides nothing:
/// admissibility of the history is the domain engine's (§11.3, the HistoryEngine port in FileSet.fs).
[<RequireQualifiedAccess>]
module Projection =
    let arrays =
        [ "observations"; "hypotheses"; "evidence"; "relationships"; "unknowns"; "assumptions"; "contradictions"
          "predictions"; "actions"; "findings"; "negativeKnowledge"; "causes"; "correctiveActions"; "criteria"
          "verifications"; "conclusions"; "proposals" ]

    type private Entity = (string * JValue) list

    type private Fold =
        { Header: Map<string, JValue>
          Entities: Map<string, Map<string, Entity>> }

    let private empty =
        { Header = Map.empty
          Entities = arrays |> List.map (fun a -> a, Map.empty) |> Map.ofList }

    let private set (k: string) (v: JValue) (e: Entity) : Entity =
        if e |> List.exists (fst >> (=) k) then e |> List.map (fun (n, x) -> if n = k then n, v else n, x)
        else e @ [ k, v ]

    let private get (k: string) (e: Entity) = Json.field k e

    let private add (array: string) (id: string) (fields: Entity) (provenance: JValue) (f: Fold) =
        let entity = ("id", JStr id) :: fields @ [ "provenance", provenance ]
        { f with Entities = f.Entities |> Map.add array (f.Entities[array] |> Map.add id entity) }

    /// Updates to an id that does not exist leave state unchanged (§8.3).
    let private update (array: string) (id: string) (change: Entity -> Entity) (f: Fold) =
        match f.Entities[array] |> Map.tryFind id with
        | Some e -> { f with Entities = f.Entities |> Map.add array (f.Entities[array] |> Map.add id (change e)) }
        | None -> f

    let private p (k: string) (o: Entity) = get k o |> Option.defaultValue JNull

    let private ps (k: string) (o: Entity) =
        match get k o with
        | Some(JStr s) -> s
        | _ -> ""

    let private apply (f: Fold) (event: Entity) : Fold =
        let pl =
            match get "payload" event with
            | Some(JObj o) -> o
            | _ -> []
        let version = p "aggregateVersion" event
        let at = p "occurredAt" event
        let provenance = JObj [ "actor", p "actor" event; "recordedAt", at ]
        let f = { f with Header = f.Header |> Map.add "version" version }
        let pick names = names |> List.map (fun n -> n, p n pl)
        match ps "eventType" event with
        | "InvestigationStarted" ->
            let f =
                { f with
                    Header =
                        f.Header
                        |> Map.add "investigationId" (p "investigationId" event)
                        |> Map.add "title" (p "title" pl)
                        |> Map.add "lifecycle" (JStr "draft")
                        |> Map.add "createdAt" at }
            add "observations" (ps "observationId" pl) [ "original", p "original" pl; "observedAt", JNull ] provenance f
        | "LifecycleChanged" -> { f with Header = f.Header |> Map.add "lifecycle" (p "to" pl) }
        | "ObservationRecorded" -> add "observations" (ps "observationId" pl) (pick [ "original"; "observedAt" ]) provenance f
        | "HypothesisRecorded" ->
            add "hypotheses" (ps "hypothesisId" pl) (pick [ "statement"; "mechanism" ] @ [ "state", JStr "proposed"; "history", JArr [] ]) provenance f
        | "HypothesisTransitioned" ->
            let change =
                JObj(pick [ "from"; "to"; "command"; "reason"; "evidenceIds" ] @ [ "at", at; "aggregateVersion", version ])
            f
            |> update "hypotheses" (ps "hypothesisId" pl) (fun h ->
                let history =
                    match get "history" h with
                    | Some(JArr xs) -> xs
                    | _ -> []
                h |> set "state" (p "to" pl) |> set "history" (JArr(history @ [ change ])))
        | "EvidenceAdmitted" ->
            add
                "evidence"
                (ps "evidenceId" pl)
                (pick [ "contentKind"; "content"; "category"; "source"; "capturedAt" ]
                 @ [ "verification", JStr "unverified"; "supersededBy", JNull; "admittedAtVersion", version ])
                provenance
                f
        | "EvidenceSuperseded" -> f |> update "evidence" (ps "evidenceId" pl) (set "supersededBy" (p "replacementEvidenceId" pl))
        | "EvidenceVerificationAssessed" -> f |> update "evidence" (ps "evidenceId" pl) (set "verification" (p "verification" pl))
        | "EvidenceRelated" -> add "relationships" (ps "relationshipId" pl) (pick [ "evidenceId"; "hypothesisId"; "kind"; "rationale" ]) provenance f
        | "UnknownIdentified" ->
            add "unknowns" (ps "unknownId" pl) (pick [ "question"; "materiality" ] @ [ "state", JStr "identified"; "basis", JNull ]) provenance f
        | "UnknownTransitioned" -> f |> update "unknowns" (ps "unknownId" pl) (set "state" (p "state" pl) >> set "basis" (p "basis" pl))
        | "AssumptionRecorded" ->
            add "assumptions" (ps "assumptionId" pl) (pick [ "proposition"; "materiality" ] @ [ "state", JStr "explicitUnverified" ]) provenance f
        | "AssumptionTransitioned" -> f |> update "assumptions" (ps "assumptionId" pl) (set "state" (p "state" pl))
        | "ContradictionRecorded" ->
            add
                "contradictions"
                (ps "contradictionId" pl)
                (pick [ "statement"; "evidenceIds"; "materiality" ] @ [ "state", JStr "detected"; "basis", JNull ])
                provenance
                f
        | "ContradictionTransitioned" ->
            f |> update "contradictions" (ps "contradictionId" pl) (set "state" (p "state" pl) >> set "basis" (p "basis" pl))
        | "PredictionRecorded" -> add "predictions" (ps "predictionId" pl) (pick [ "hypothesisId"; "statement"; "strength"; "timing" ]) provenance f
        | "ActionRecorded" ->
            add "actions" (ps "actionId" pl) (pick [ "kind"; "purpose"; "targetHypothesisIds" ] @ [ "state", JStr "proposed" ]) provenance f
        | "ActionTransitioned" -> f |> update "actions" (ps "actionId" pl) (set "state" (p "state" pl))
        | "FindingRecorded" -> add "findings" (ps "findingId" pl) (pick [ "actionId"; "originalResult"; "interpretation" ]) provenance f
        | "NegativeKnowledgeEstablished" ->
            add "negativeKnowledge" (ps "negativeKnowledgeId" pl) (pick [ "statement"; "scope"; "basisEvidenceIds" ]) provenance f
        | "CausalAssertionRecorded" ->
            add "causes" (ps "causeId" pl) (pick [ "outcome"; "members"; "mechanism" ] @ [ "state", JStr "proposed" ]) provenance f
        | "CausalAssertionConfirmed" -> f |> update "causes" (ps "causeId" pl) (set "state" (JStr "confirmed"))
        | "CausalAssertionReopened" -> f |> update "causes" (ps "causeId" pl) (set "state" (JStr "reopened"))
        | "CorrectiveActionRecorded" ->
            add
                "correctiveActions"
                (ps "correctiveActionId" pl)
                (pick [ "kind"; "description"; "targetCauseIds" ] @ [ "state", JStr "proposed"; "awaitingSinceVersion", JInt 0L ])
                provenance
                f
        | "CorrectiveActionTransitioned" ->
            f
            |> update "correctiveActions" (ps "correctiveActionId" pl) (fun c ->
                let c = c |> set "state" (p "state" pl)
                if p "state" pl = JStr "awaitingVerificationOfEffect" then c |> set "awaitingSinceVersion" version else c)
        | "CriterionRecorded" -> add "criteria" (ps "criterionId" pl) (pick [ "correctiveActionId"; "expectedOutcome"; "method" ]) provenance f
        | "VerificationRecorded" ->
            add "verifications" (ps "verificationId" pl) (pick [ "criterionId"; "outcome"; "evidenceIds" ] @ [ "recordedAtVersion", version ]) provenance f
        | "ConclusionRecorded" ->
            add
                "conclusions"
                (ps "conclusionId" pl)
                (pick [ "statement"; "strength"; "causeIds"; "residualUncertainty" ] @ [ "recordedAtVersion", version ])
                provenance
                f
        | "ProposalRecorded" ->
            add "proposals" (ps "proposalId" pl) (pick [ "kind"; "text"; "rationale" ] @ [ "state", JStr "active"; "decision", JNull ]) provenance f
        | "ProposalWasAccepted" -> f |> update "proposals" (ps "proposalId" pl) (set "state" (JStr "accepted"))
        | "ProposalWasRejected" -> f |> update "proposals" (ps "proposalId" pl) (set "state" (JStr "rejected") >> set "decision" (p "reason" pl))
        | _ -> f

    /// Fold events (already ordered by aggregateVersion) into the canonical `state` object. None for an empty history.
    let state (events: JValue list) : JValue option =
        let folded =
            events
            |> List.fold (fun f e ->
                match e with
                | JObj o -> apply f o
                | _ -> f) empty
        if folded.Header.IsEmpty || not (folded.Header.ContainsKey "investigationId") then None
        else
            let header = [ "investigationId"; "title"; "lifecycle"; "version"; "createdAt" ] |> List.map (fun k -> k, folded.Header[k])
            let entityArrays =
                arrays
                |> List.map (fun a ->
                    a,
                    folded.Entities[a]
                    |> Map.toList
                    |> List.sortWith (fun (x, _) (y, _) -> String.CompareOrdinal(x, y))
                    |> List.map (snd >> JObj)
                    |> JArr)
            Some(JObj(header @ entityArrays))

    /// The snapshot envelope a writer produces and a reader expects (§8.1).
    let snapshot (events: JValue list) : JValue option =
        match state events, List.tryLast events with
        | Some s, Some(JObj last) ->
            Some(
                JObj
                    [ "formatVersion", JStr ContractVersion.formatVersion
                      "investigationId", Json.field "investigationId" last |> Option.defaultValue JNull
                      "aggregateVersion", Json.field "aggregateVersion" last |> Option.defaultValue JNull
                      "projectionVersion", JInt 1L
                      "semanticVersion", JStr ContractVersion.semanticVersion
                      "generatedAt", Json.field "occurredAt" last |> Option.defaultValue JNull
                      "state", s ]
            )
        | _ -> None
