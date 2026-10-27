/// A synthetic history generator (EXT-DATA-001-O2: synthetic test generators MAY be included). It produces
/// contract-shaped events from fixed, invented ids; no investigation record from any data repository is bundled.
module Persistence.Tests.Synthetic

open Causa.Persistence

let ns = "6ba7b811-9dad-11d1-80b4-00c04fd430c8"

/// A deterministic id for a synthetic entity.
let id (name: string) = Uuid.v5 ns ("persistence-tests/" + name)

let human = JObj [ "actorId", JStr "tester-01"; "kind", JStr "human" ]

let at (minute: int) = sprintf "2026-10-27T10:%02d:00.000Z" minute

/// One engine-shaped event at a version; one command per event (fresh correlation id).
let event (investigationId: string) (version: int64) (eventType: string) (payload: (string * JValue) list) =
    FileSet.envelope investigationId version (id (sprintf "%s/cmd/%d" investigationId version)) (at (int version)) human eventType (JObj payload)

let started (investigationId: string) (title: string) =
    event investigationId 1L "InvestigationStarted" [ "title", JStr title; "observationId", JStr(id (investigationId + "/obs")); "original", JStr "The service returned errors." ]

let hypothesis (investigationId: string) (version: int64) (name: string) =
    event investigationId version "HypothesisRecorded" [ "hypothesisId", JStr(id (investigationId + "/h/" + name)); "statement", JStr("Hypothesis " + name); "mechanism", JNull ]

let activate (investigationId: string) (version: int64) =
    event investigationId version "LifecycleChanged" [ "from", JStr "draft"; "to", JStr "active"; "command", JStr "activateInvestigation"; "reason", JNull ]

/// The engine stand-in used where admissibility is not the subject of the test: it says plainly that it did not decide.
let engine = HistoryEngine.notWired

/// A verified synthetic investigation with `extra` hypotheses after start + activation.
let investigation (name: string) (extra: int) =
    let iid = id ("inv/" + name)
    let events = [ started iid ("Synthetic " + name); activate iid 2L ] @ [ for i in 1..extra -> hypothesis iid (int64 (2 + i)) (string i) ]
    match FileSet.append engine iid None 0L events with
    | Ok v -> v
    | Error e -> failwithf "synthetic investigation invalid: %s %s" e.Code e.Detail

let raw (v: Verified) = { DirectoryName = v.InvestigationId; Entries = FileSet.files v }

let replaceEntry (name: string) (bytes: byte[]) (r: RawFileSet) =
    { r with Entries = r.Entries |> List.map (fun (n, b) -> if n = name then n, bytes else n, b) }

let utf8 (s: string) = System.Text.Encoding.UTF8.GetBytes s
let text (b: byte[]) = System.Text.Encoding.UTF8.GetString b
