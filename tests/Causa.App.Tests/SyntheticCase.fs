/// A synthetic investigation driven through the real kernel (Kernel.submit), for the UI-language and record tests.
/// Every id is a fixed test GUID; no historical record is used.
module Causa.App.Tests.SyntheticCase

open System
open IndyInitRootCauseInvestigator.Engine

let g (n: int) = Guid($"00000000-0000-4000-8000-{n:D12}")
let investigation: InvestigationId = Id.ofGuid (g 1)
let person: Actor = { ActorId = "investigator-a"; Kind = Human }
let at0 = DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.Zero)

let obs: ObservationId = Id.ofGuid (g 2)
let hSeal: HypothesisId = Id.ofGuid (g 10)
let hBearing: HypothesisId = Id.ofGuid (g 11)
let hOrphan: HypothesisId = Id.ofGuid (g 12)
let eTemp: EvidenceId = Id.ofGuid (g 20)
let ePhoto: EvidenceId = Id.ofGuid (g 21)
let rSupports: RelationshipId = Id.ofGuid (g 30)
let rContradicts: RelationshipId = Id.ofGuid (g 31)
let contradiction: ContradictionId = Id.ofGuid (g 40)
let question: UnknownId = Id.ofGuid (g 50)
let cause: CauseId = Id.ofGuid (g 60)
let correction: CorrectiveActionId = Id.ofGuid (g 70)
let criterion: CriterionId = Id.ofGuid (g 80)

/// Submits each command in turn at the session's version; fails loudly on a refusal.
let run (commands: Command list) : Session =
    commands
    |> List.indexed
    |> List.fold
        (fun (session: Session) (i, command) ->
            let env =
                { CommandId = g (1000 + i)
                  Investigation = investigation
                  ExpectedVersion = (match session.State with Some s -> Some s.Version | None -> None)
                  Actor = person
                  RequestedAt = at0.AddMinutes(float i)
                  Command = command }
            match Kernel.submit session env with
            | Ok a -> a.Session
            | Error r -> failwithf "step %d %A refused: %A" i command (Kernel.reasons r))
        Kernel.empty

let opening =
    [ StartInvestigation("Pump P-7 overheating", obs, "Pump P-7 tripped on high temperature twice during the night shift.")
      ActivateInvestigation ]

let full =
    opening
    @ [ RecordHypothesis(hSeal, "The mechanical seal leaked coolant.", Some "Coolant loss reduced heat removal.")
        RecordHypothesis(hBearing, "A dry bearing overheated.", None)
        RecordHypothesis(hOrphan, "The thermostat reads high.", None)
        AdmitEvidence(eTemp, TextInput "Casing temperature 96 °C at 02:10.", Measurement, "Control-room trend log", Some(at0.AddHours(-7.0)))
        AdmitEvidence(ePhoto, UrlInput "https://example.org/p7/seal-photo.jpg", DirectObservation, "Maintenance photo, shift B", None)
        RelateEvidence(rSupports, ePhoto, hSeal, Supports, Some "Coolant residue at the seal face.")
        RelateEvidence(rContradicts, eTemp, hBearing, Contradicts, Some "Bearing temperature stayed normal.")
        RecordContradiction(contradiction, "The trend log and the operator's account disagree on the trip time.", [ eTemp; ePhoto ], Material)
        IdentifyUnknown(question, "Was the seal replaced at the last overhaul?", Material)
        RecordCause(cause, "Pump P-7 overheated and tripped.", [ { Hypothesis = hSeal; Role = RootCause } ], None)
        RecordCorrectiveAction(correction, Correction, "Replace the mechanical seal with the upgraded type.", [ cause ])
        RecordCriterion(criterion, correction, "No high-temperature trip in 30 days.", None) ]

let projectionOf (session: Session) =
    Projection.ofInvestigation person (at0.AddDays 1.0) None (Option.get session.State)

let source (session: Session) : Causa.App.Records.Source = { Projection = projectionOf session; History = session.History }
