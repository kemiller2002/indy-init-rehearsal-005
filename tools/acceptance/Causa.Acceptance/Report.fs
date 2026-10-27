/// The human-readable acceptance report. Its only verdict line is the one computed by
/// tools/compute-acceptance-verdict.py; the plan assessment and the counts are reported beside it, never instead.
module Causa.Acceptance.Report

open System
open System.Text
open Causa.Acceptance.Json

let render
    (verdict: Text.Json.Nodes.JsonNode)
    (assessment: Text.Json.Nodes.JsonNode option)
    (trace: Text.Json.Nodes.JsonNode)
    (lockGate: Text.Json.Nodes.JsonNode option)
    (context: (string * string) list)
    =
    let ledgerVerdict = strOr "INVALID" "verdict" verdict
    let planVerdict = assessment |> Option.bind (str "verdict") |> Option.defaultValue "NOT_COMPUTED"
    let accepted = ledgerVerdict = "ACCEPTED" && planVerdict = "ACCEPTED"
    let sb = StringBuilder()
    let line (s: string) = sb.Append(s).Append('\n') |> ignore
    let num path (node: Text.Json.Nodes.JsonNode) =
        path
        |> List.fold (fun (n: Text.Json.Nodes.JsonNode option) k -> n |> Option.bind (field k)) (Some node)
        |> Option.map (fun n -> n.ToJsonString())
        |> Option.defaultValue "?"
    line "# Causa acceptance (computed)"
    line ""
    line (sprintf "<!-- acceptance-verdict: %s -->" ledgerVerdict)
    line ""
    line (sprintf "Combined result: %s." (if accepted then "accepted by both verifiers" else sprintf "NOT ACCEPTED (ledger verifier %s, plan verifier %s)" ledgerVerdict planVerdict))
    line ""
    line "| Input | Value |"
    line "| --- | --- |"
    for k, v in context do
        line (sprintf "| %s | `%s` |" k v)
    line ""
    line "## Obligations (traceability)"
    line ""
    line (sprintf "- obligations: %s; bound %s, declared but not run %s, missing %s" (num [ "summary"; "obligations" ] trace) (num [ "summary"; "binding"; "bound" ] trace) (num [ "summary"; "binding"; "declared" ] trace) (num [ "summary"; "binding"; "missing" ] trace))
    line (sprintf "- obligation results: PASS %s, FAIL %s, NOT_RUN %s" (num [ "summary"; "verification"; "PASS" ] trace) (num [ "summary"; "verification"; "FAIL" ] trace) (num [ "summary"; "verification"; "NOT_RUN" ] trace))
    line (sprintf "- P0 requirements: PASS %s, FAIL %s, NOT_RUN %s (of %s)" (num [ "summary"; "requirementsByVerification"; "PASS" ] trace) (num [ "summary"; "requirementsByVerification"; "FAIL" ] trace) (num [ "summary"; "requirementsByVerification"; "NOT_RUN" ] trace) (num [ "summary"; "requirements" ] trace))
    line ""
    line "## Plan verifier (requirements-compiler verify-plan)"
    line ""
    match assessment with
    | None -> line "- not computed in this run"
    | Some a ->
        line (sprintf "- result %s" planVerdict)
        for k, v in field "summary" a |> Option.bind (field "byStatus") |> Option.map props |> Option.defaultValue [] do
            line (sprintf "- requirements %s: %s" k (v.ToJsonString()))
        for k, v in field "summary" a |> Option.bind (field "obligationsByStatus") |> Option.map props |> Option.defaultValue [] do
            line (sprintf "- obligations %s: %s" k (v.ToJsonString()))
        for p in strings "integrityProblems" a do
            line (sprintf "- integrity: %s" p)
    line ""
    line "## Stack lock gate (T11)"
    line ""
    match lockGate with
    | None -> line "- not run"
    | Some g ->
        line (sprintf "- gate %s (PASS %s, FAIL %s, GAP %s)" (strOr "?" "verdict" g) (num [ "summary"; "PASS" ] g) (num [ "summary"; "FAIL" ] g) (num [ "summary"; "GAP" ] g))
        for c in items "checks" g do
            if str "status" c <> Some "PASS" then
                line (sprintf "- %s %s: expected `%s`, actual `%s` (%s)" (strOr "" "status" c) (strOr "" "subject" c) (strOr "" "expected" c) (strOr "" "actual" c) (strOr "" "detail" c))
    line ""
    line "## Ledger verifier reasons (compute-acceptance-verdict.py)"
    line ""
    let reasons = strings "reasons" verdict
    for r in reasons |> List.truncate 60 do
        line (sprintf "- %s" r)
    if reasons.Length > 60 then line (sprintf "- ... and %d more (see verdict.json)" (reasons.Length - 60))
    sb.ToString()
