/// A synthetic history generator (EXT-DATA-001-O2: synthetic test generators MAY be included). Every history is
/// decided by the engine (Kernel.submit) and written by its storage writer; no record from any data repository is
/// bundled, and no event is built by hand.
module Persistence.Tests.Synthetic

open System
open Causa.Persistence

type Envelope = IndyInitRootCauseInvestigator.Engine.CommandEnvelope

let ns = "6ba7b811-9dad-11d1-80b4-00c04fd430c8"

/// A deterministic id for a synthetic entity.
let id (name: string) = Uuid.v5 ns ("persistence-tests/" + name)

let private guid (name: string) = Guid(id name)

let human: IndyInitRootCauseInvestigator.Engine.Actor = { ActorId = "tester-01"; Kind = IndyInitRootCauseInvestigator.Engine.ActorKind.Human }

let at (minute: int) = DateTimeOffset(2026, 10, 27, 10, 0, 0, TimeSpan.Zero).AddMinutes(float minute)

/// One command envelope, decided at `expected`. The command id is derived from the investigation, the version and
/// a label, so the same decision always yields the same bytes and different decisions never share an id.
let envelope (investigationId: string) (expected: int64) (label: string) (command: IndyInitRootCauseInvestigator.Engine.Command) : Envelope =
    { CommandId = Guid(Uuid.v5 ns (sprintf "persistence-tests/cmd/%s/%d/%s" investigationId expected label))
      Investigation = IndyInitRootCauseInvestigator.Engine.Id(Guid investigationId)
      ExpectedVersion = Some expected
      Actor = human
      RequestedAt = at (int expected)
      Command = command }

let start (investigationId: string) (title: string) =
    envelope investigationId 0L "start" (IndyInitRootCauseInvestigator.Engine.StartInvestigation(title, IndyInitRootCauseInvestigator.Engine.Id(guid (investigationId + "/obs")), "The service returned errors."))

let activate (investigationId: string) (expected: int64) =
    envelope investigationId expected "activate" IndyInitRootCauseInvestigator.Engine.ActivateInvestigation

let hypothesis (investigationId: string) (expected: int64) (name: string) =
    envelope investigationId expected ("h/" + name) (IndyInitRootCauseInvestigator.Engine.RecordHypothesis(IndyInitRootCauseInvestigator.Engine.Id(guid (investigationId + "/h/" + name)), "Hypothesis " + name, None))

let private ok r =
    match r with
    | Ok v -> v
    | Error(e: Store.ChangeRefusal) -> failwithf "synthetic command refused: %s %A" e.Code e.Reasons

/// A verified synthetic investigation: start, activate, then `extra` hypotheses.
let investigation (name: string) (extra: int) =
    let iid = id ("inv/" + name)
    let v = Store.create (start iid ("Synthetic " + name)) |> ok
    let v = Store.submit v (activate iid v.Version) |> ok
    [ 1..extra ] |> List.fold (fun (v: Verified) i -> Store.submit v (hypothesis iid v.Version (string i)) |> ok) v

let raw (v: Verified) = { DirectoryName = v.InvestigationId; Entries = Store.files v }

let replaceEntry (name: string) (bytes: byte[]) (r: RawFileSet) =
    { r with Entries = r.Entries |> List.map (fun (n, b) -> if n = name then n, bytes else n, b) }

let utf8 (s: string) = Text.Encoding.UTF8.GetBytes s
let text (b: byte[]) = Text.Encoding.UTF8.GetString b
