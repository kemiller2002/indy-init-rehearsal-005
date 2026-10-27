/// Test build only (Percepta state injection): the fixture investigation's typed engine records, built through the
/// real kernel (Kernel.submit) with fixed synthetic ids, so the inspector and evidence screens render in injected
/// states exactly as they do for a real investigation. The words mirror TestState's fixture rows. No historical data.
module Causa.App.UxFixture

open System
open IndyInitRootCauseInvestigator.Engine

let private g (s: string) = Guid s
let investigationId = "00000000-0000-4000-8000-0000000000f1"
let evidenceId = "00000000-0000-4000-8000-0000000000e1"
let private actor: Actor = { ActorId = "fixture-investigator"; Kind = Human }
let private at0 = DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)

let source: Records.Source option =
    let iid: InvestigationId = Id.ofGuid (g investigationId)
    let h: HypothesisId = Id.ofGuid (g "00000000-0000-4000-8000-0000000000a1")
    let e1: EvidenceId = Id.ofGuid (g evidenceId)
    let e2: EvidenceId = Id.ofGuid (g "00000000-0000-4000-8000-0000000000e2")
    let commands =
        [ StartInvestigation("Fixture investigation", Id.ofGuid (g "00000000-0000-4000-8000-0000000000b1"), "A fixture state injected by the Percepta verification run.")
          ActivateInvestigation
          RecordHypothesis(h, "A seal lost resilience in the cold.", None)
          AdmitEvidence(e1, TextInput "Launch-time temperature was recorded below the qualified range.", Measurement, "Fixture source", None)
          AdmitEvidence(e2, TextInput "The pad thermometer log shows the same low reading.", Log, "Fixture source", Some at0)
          RelateEvidence(Id.ofGuid (g "00000000-0000-4000-8000-0000000000c1"), e1, h, Supports, None)
          RecordCause(Id.ofGuid (g "00000000-0000-4000-8000-0000000000d1"), "The joint failed during ascent.", [ { Hypothesis = h; Role = RootCause } ], None) ]
    let step (session: Session option) (i, command) =
        session
        |> Option.bind (fun s ->
            let env =
                { CommandId = g $"00000000-0000-4000-8000-{(4000 + i):D12}"
                  Investigation = iid
                  ExpectedVersion = s.State |> Option.map (fun x -> x.Version)
                  Actor = actor
                  RequestedAt = at0.AddMinutes(float i)
                  Command = command }
            match Kernel.submit s env with
            | Ok a -> Some a.Session
            | Error _ -> None)
    commands
    |> List.indexed
    |> List.fold step (Some Kernel.empty)
    |> Option.bind (fun s -> s.State |> Option.map (fun inv -> s, inv))
    |> Option.map (fun (s, inv) -> { Projection = Projection.ofInvestigation actor (at0.AddDays 1.0) None inv; History = s.History })
