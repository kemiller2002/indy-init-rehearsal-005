/// Dokimos 0.8.0 `dokimos evidence plan` glue: builds the dokimos.evidence-reuse-request/1 from the declared
/// test-to-input mappings (acceptance/reuse.json), reads the plan's decision for one producer, and records
/// dokimos.evidence-provenance/1 after a producer ran, from exactly the inputs the plan resolved.
///
/// Reuse only ever skips re-running an unchanged check. It never carries acceptance evidence: the acceptance ledger
/// and bundle are always produced from the candidate run itself, and `live` producers are never reused.
module Causa.Acceptance.Reuse

open System
open Causa.Acceptance.Json

let request (config: Text.Json.Nodes.JsonNode) (revision: string) (changed: string list option) (prior: Text.Json.Nodes.JsonNode list) =
    let pass name = field name config |> Option.map (fun n -> n.DeepClone()) |> Option.defaultValue (jarr [])
    let tests =
        items "Tests" config
        |> List.map (fun t ->
            let o = t.DeepClone().AsObject()
            o.Remove "Description" |> ignore
            o :> Text.Json.Nodes.JsonNode)
    let priorIds = Collections.Generic.HashSet<string>()
    let uniquePrior =
        prior
        |> List.filter (fun p -> priorIds.Add(strOr "" "EvidenceId" p))
    jobj (
        [ "SchemaVersion", jstr "dokimos.evidence-reuse-request/1"
          "Revision", jstr revision ]
        @ (match changed with
           | Some paths -> [ "ChangedPaths", jstrs paths ]
           | None -> [])
        @ [ "GlobalInputs", pass "GlobalInputs"
            "IgnoredPaths", pass "IgnoredPaths"
            "Tests", jarr tests
            "PriorEvidence", jarr uniquePrior ]
    )

let private planTest (plan: Text.Json.Nodes.JsonNode) (testId: string) =
    items "Tests" plan |> List.tryFind (fun t -> str "TestId" t = Some testId)

/// "reuse" only when Dokimos decided reuse and the producer is not live; "run" otherwise.
let decision (plan: Text.Json.Nodes.JsonNode) (testId: string) =
    match planTest plan testId with
    | Some t when str "Decision" t = Some "reuse" && str "Freshness" t <> Some "live" -> "reuse"
    | _ -> "run"

/// A provenance record for a producer that ran at `revision`, built from the plan's resolved inputs and identity.
let provenance (plan: Text.Json.Nodes.JsonNode) (testId: string) (revision: string) (runId: string) (outcome: string) =
    match planTest plan testId with
    | None -> None
    | Some t ->
        let identity = str "CurrentIdentity" t
        let inputs = field "Inputs" t
        Some(
            jobj (
                [ "SchemaVersion", jstr "dokimos.evidence-provenance/1"
                  "EvidenceId", jstr (sprintf "%s@%s#%s" testId revision runId)
                  "TestId", jstr testId
                  "Outcome", jstr outcome
                  "ProducedAtRevision", jstr revision
                  "Freshness", jstr (strOr "content-addressed" "Freshness" t) ]
                @ (match identity with Some i -> [ "EvidenceIdentity", jstr i ] | None -> [])
                @ (match inputs with Some i -> [ "Inputs", i ] | None -> [])
            )
        )
