/// Computed gate producers (acceptance/gates.json `producers`): each one is a set of checks over what this run
/// really observed — executed test results, committed files, the verification plan and the defect-class record.
///
/// A producer
/// - succeeds only when every one of its checks passed;
/// - fails when any check failed (a selected test failed, a required file is absent or malformed, a bound
///   requirement failed, a recorded fix SHA is not in the candidate's history);
/// - is not produced otherwise (a selected test did not run, a requirement is not yet bound, a defect class is not
///   closed by the reviewer).
/// Nothing is inferred: a test is selected only by its exact identity prefix, a substring of its name, or its tag.
module Causa.Acceptance.Producers

open System
open Causa.Acceptance.Json
open Causa.Acceptance.Plan
open Causa.Acceptance.Discovery
open Causa.Acceptance.Traceability

type CheckStatus =
    | CheckPass
    | CheckFail
    | CheckNotRun

let checkStatusName =
    function
    | CheckPass -> "PASS"
    | CheckFail -> "FAIL"
    | CheckNotRun -> "NOT_RUN"

type Check =
    { Name: string
      Status: CheckStatus
      Detail: string
      Tests: TestResult list }

/// How a check selects executed tests. All given criteria must hold.
type Selector =
    { Name: string
      TestIdPrefix: string option
      Contains: string option
      Tag: string option
      Minimum: int }

type FileRule = { Path: string; Schema: string option }

type Spec =
    | Tests of id: string * description: string * planGate: string option * files: FileRule list * selectors: Selector list
    | DefectClasses of id: string * description: string

let specId =
    function
    | Tests(id, _, _, _, _) -> id
    | DefectClasses(id, _) -> id

let private selectorOf (n: Text.Json.Nodes.JsonNode) =
    { Name = strOr "" "name" n
      TestIdPrefix = str "testIdPrefix" n
      Contains = str "contains" n
      Tag = str "tag" n
      Minimum =
        match field "minimum" n with
        | Some v -> v.GetValue<int>()
        | None -> 1 }

/// The `producers` array of acceptance/gates.json.
let loadSpecs (gates: Text.Json.Nodes.JsonNode) =
    items "producers" gates
    |> List.map (fun p ->
        let id = strOr "" "id" p
        let description = strOr "" "description" p
        match strOr "tests" "kind" p with
        | "defect-classes" -> DefectClasses(id, description)
        | "tests" ->
            Tests(
                id,
                description,
                str "planGate" p,
                items "files" p |> List.map (fun f -> { Path = strOr "" "path" f; Schema = str "schema" f }),
                items "tests" p |> List.map selectorOf
            )
        | other -> failwithf "producer %s has unknown kind %s" id other)

let matches (s: Selector) (r: TestResult) =
    let ci (hay: string) (needle: string) = hay.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0
    (s.TestIdPrefix.IsSome || s.Contains.IsSome || s.Tag.IsSome)
    && s.TestIdPrefix |> Option.forall (fun p -> r.TestId.StartsWith(p, StringComparison.Ordinal))
    && s.Contains |> Option.forall (fun c -> ci r.DisplayName c || ci r.TestId c)
    && s.Tag |> Option.forall (fun t -> r.Tags |> List.contains t || ci r.DisplayName t)

/// One selector over the run's results: at least `Minimum` selected results, all passed.
let selectorCheck (results: TestResult list) (s: Selector) =
    let selected = results |> List.filter (matches s)
    let failed = selected |> List.filter (fun r -> r.Outcome = Failed)
    let skipped = selected |> List.filter (fun r -> r.Outcome = Skipped)
    let status, detail =
        if not failed.IsEmpty then
            CheckFail, sprintf "%d of %d selected tests failed: %s" failed.Length selected.Length (String.Join("; ", failed |> List.map (fun r -> r.TestId)))
        elif not skipped.IsEmpty then
            CheckNotRun, sprintf "%d selected tests did not run (skipped): %s" skipped.Length (String.Join("; ", skipped |> List.map (fun r -> r.TestId)))
        elif selected.Length < s.Minimum then
            CheckNotRun, sprintf "%d selected tests ran, at least %d required; no such result in this run" selected.Length s.Minimum
        else
            CheckPass, sprintf "%d selected tests ran and passed" selected.Length
    { Name = s.Name; Status = status; Detail = detail; Tests = selected }

/// Every plan requirement routed to `gate` must be bound and passing. A plan that routes none to it says so.
let planGateCheck (trace: Trace) (gate: string) =
    let reqs = trace.Plan.Requirements |> List.filter (fun r -> r.Gates |> List.contains gate)
    let byStatus = reqs |> List.map (fun r -> r.Id, requirementVerification trace r.Id)
    let ids s = byStatus |> List.filter (snd >> (=) s) |> List.map fst
    let status, detail =
        if reqs.IsEmpty then CheckPass, sprintf "the verification plan routes no P0 requirement to %s" gate
        elif not (ids Fail).IsEmpty then CheckFail, sprintf "%s requirements with failing obligations: %s" gate (String.Join(", ", ids Fail))
        elif not (ids NotRun).IsEmpty then
            CheckNotRun, sprintf "%d of %d %s requirements not yet bound and passing: %s" (ids NotRun).Length reqs.Length gate (String.Join(", ", ids NotRun))
        else CheckPass, sprintf "all %d %s requirements bound and passing" reqs.Length gate
    { Name = sprintf "plan requirements routed to %s" gate; Status = status; Detail = detail; Tests = [] }

/// A committed file: present, valid JSON, and of the declared schema. `read` returns None for an absent file.
let fileCheck (read: string -> (string * Text.Json.Nodes.JsonNode option) option) (f: FileRule) =
    let status, detail =
        match read f.Path with
        | None -> CheckFail, sprintf "%s is not in the candidate" f.Path
        | Some(sha, None) -> CheckFail, sprintf "%s (sha256 %s) is not valid JSON" f.Path sha
        | Some(sha, Some node) ->
            match f.Schema with
            | Some s when str "schema" node <> Some s -> CheckFail, sprintf "%s (sha256 %s) has schema %A, expected %s" f.Path sha (str "schema" node) s
            | _ -> CheckPass, sprintf "%s sha256 %s" f.Path sha
    { Name = sprintf "committed %s" f.Path; Status = status; Detail = detail; Tests = [] }

/// G7: every recorded defect class closed by the independent reviewer (status PASS, fix SHA in the candidate's
/// history, reviewer evidence), and every failing-first test it lists ran and passed in this run.
let defectClassChecks (results: TestResult list) (isAncestor: string -> bool) (classes: Ledger.DefectClass list) =
    classes
    |> List.collect (fun d ->
        let closure =
            let status, detail =
                match d.FixSha, d.ReviewerEvidence with
                | Some sha, Some ev when d.Status = "PASS" ->
                    if isAncestor sha then CheckPass, sprintf "closed by the reviewer: fix %s, evidence %s" sha ev
                    else CheckFail, sprintf "recorded fix %s is not in the candidate's history" sha
                | _ when d.Status = "FAIL" -> CheckFail, "the reviewer recorded the class as FAIL"
                | _ -> CheckNotRun, "not closed: needs fix SHA, reviewer evidence and status PASS recorded by the independent reviewer"
            { Name = sprintf "%s closure" d.Id; Status = status; Detail = detail; Tests = [] }
        let tests =
            d.Tests
            |> List.map (fun t -> selectorCheck results { Name = sprintf "%s %s" d.Id t; TestIdPrefix = None; Contains = Some t; Tag = None; Minimum = 1 })
        closure :: tests)

let status (checks: Check list) =
    if checks.IsEmpty then Ledger.NotProduced
    elif checks |> List.exists (fun c -> c.Status = CheckFail) then Ledger.ProducerFailed
    elif checks |> List.forall (fun c -> c.Status = CheckPass) then Ledger.Succeeded
    else Ledger.NotProduced

type Environment =
    { Trace: Trace
      Results: TestResult list
      ReadFile: string -> (string * Text.Json.Nodes.JsonNode option) option
      IsAncestor: string -> bool
      DefectClasses: Ledger.DefectClass list }

let checksOf (env: Environment) (spec: Spec) =
    match spec with
    | Tests(_, _, gate, files, selectors) ->
        (gate |> Option.map (planGateCheck env.Trace) |> Option.toList)
        @ (files |> List.map (fileCheck env.ReadFile))
        @ (selectors |> List.map (selectorCheck env.Results))
    | DefectClasses _ -> defectClassChecks env.Results env.IsAncestor env.DefectClasses

let statusName =
    function
    | Ledger.Succeeded -> "success"
    | Ledger.ProducerFailed -> "failure"
    | Ledger.NotProduced -> "not-produced"

let recordJson (spec: Spec) (checks: Check list) (context: (string * Text.Json.Nodes.JsonNode) list) =
    let description =
        match spec with
        | Tests(_, d, _, _, _) -> d
        | DefectClasses(_, d) -> d
    jobj (
        [ "schema", jstr "causa.acceptance-producer/1"
          "id", jstr (specId spec)
          "description", jstr description
          "status", jstr (statusName (status checks))
          "checks",
          jarr (
              checks
              |> List.map (fun c ->
                  jobj
                      [ "name", jstr c.Name
                        "status", jstr (checkStatusName c.Status)
                        "detail", jstr c.Detail
                        "tests",
                        jarr (c.Tests |> List.map (fun t -> jobj [ "testId", jstr t.TestId; "outcome", jstr (outcomeName t.Outcome); "resultFile", jstr t.Source ])) ])
          ) ]
        @ context
    )
