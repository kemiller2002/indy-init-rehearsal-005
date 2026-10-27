/// Binds every verification-plan obligation to the named tests that claim it.
///
/// An obligation is
/// - `missing`  when no test anywhere names its VPO id;
/// - `declared` when a test source names it, but no result file in this run contains such a test;
/// - `bound`    when at least one executed test result names it.
/// Its verification is PASS only when it is bound and every bound result passed, FAIL when any bound result failed,
/// and NOT_RUN otherwise. Nothing is inferred: an obligation with no tagged test is missing, never guessed.
module Causa.Acceptance.Traceability

open System
open System.IO
open System.Text.Json.Nodes
open Causa.Acceptance.Json
open Causa.Acceptance.Plan
open Causa.Acceptance.Discovery

type Binding =
    | Missing
    | Declared of sources: string list
    | Bound of results: TestResult list

type Verification =
    | Pass
    | Fail
    | NotRun

type ObligationTrace =
    { Obligation: Obligation
      Binding: Binding
      Verification: Verification }

type Trace =
    { Plan: VerificationPlan
      Obligations: ObligationTrace list
      /// Tags naming an id that is not an obligation of this plan (a typo, or a stale id): always reported.
      UnknownTags: (string * string) list }

let bindingName =
    function
    | Missing -> "missing"
    | Declared _ -> "declared"
    | Bound _ -> "bound"

let verificationName =
    function
    | Pass -> "PASS"
    | Fail -> "FAIL"
    | NotRun -> "NOT_RUN"

let verify binding =
    match binding with
    | Bound results when results |> List.exists (fun r -> r.Outcome = Failed) -> Fail
    | Bound results when not results.IsEmpty && results |> List.forall (fun r -> r.Outcome = Passed) -> Pass
    | _ -> NotRun

/// Test source files that name VPO ids: (obligation id, repository-relative path).
let scanSources (repoRoot: string) (dirs: string list) : (string * string) list =
    let isTestSource (p: string) =
        [ ".fs"; ".fsx"; ".cs"; ".js"; ".mjs"; ".ts" ] |> List.exists (fun e -> p.EndsWith(e, StringComparison.OrdinalIgnoreCase))
    let skip (p: string) =
        [ "/bin/"; "/obj/"; "/node_modules/" ] |> List.exists (fun s -> p.Replace('\\', '/').Contains s)
    dirs
    |> List.filter Directory.Exists
    |> List.collect (fun d -> Directory.GetFiles(d, "*", SearchOption.AllDirectories) |> List.ofArray)
    |> List.filter (fun p -> isTestSource p && not (skip p))
    |> List.distinct
    |> List.collect (fun p ->
        let rel = Path.GetRelativePath(repoRoot, p).Replace('\\', '/')
        obligationsIn [ File.ReadAllText p ] |> List.map (fun id -> id, rel))

let build (plan: VerificationPlan) (results: TestResult list) (declared: (string * string) list) : Trace =
    let known = plan |> obligations |> List.map (fun o -> o.Id) |> Set.ofList
    let byObligation =
        results
        |> List.collect (fun r -> r.Obligations |> List.map (fun o -> o, r))
        |> List.groupBy fst
        |> List.map (fun (k, v) -> k, v |> List.map snd |> List.sortBy (fun r -> r.TestId, r.DisplayName))
        |> Map.ofList
    let declaredBy =
        declared |> List.groupBy fst |> List.map (fun (k, v) -> k, v |> List.map snd |> List.distinct |> List.sort) |> Map.ofList

    let trace (o: Obligation) =
        let binding =
            match byObligation.TryFind o.Id, declaredBy.TryFind o.Id with
            | Some rs, _ -> Bound rs
            | None, Some srcs -> Declared srcs
            | None, None -> Missing
        { Obligation = o; Binding = binding; Verification = verify binding }

    let unknown =
        [ for r in results do
              for o in r.Obligations do
                  if not (known.Contains o) then yield o, r.TestId
          for o, src in declared do
              if not (known.Contains o) then yield o, "source:" + src ]
        |> List.distinct
        |> List.sort

    { Plan = plan; Obligations = plan |> obligations |> List.map trace; UnknownTags = unknown }

/// Requirement-level verification: FAIL if any obligation fails, PASS only if every obligation passes.
let requirementVerification (trace: Trace) (requirementId: string) =
    let os = trace.Obligations |> List.filter (fun o -> o.Obligation.Requirement = requirementId)
    if os |> List.exists (fun o -> o.Verification = Fail) then Fail
    elif not os.IsEmpty && os |> List.forall (fun o -> o.Verification = Pass) then Pass
    else NotRun

let count pred (trace: Trace) = trace.Obligations |> List.filter pred |> List.length

let toJson (trace: Trace) (context: (string * JsonNode) list) : JsonNode =
    let p = trace.Plan
    let resultJson (r: TestResult) =
        jobj
            [ "testId", jstr r.TestId
              "name", jstr r.DisplayName
              "suite", jstr (suiteName r.Suite)
              "outcome", jstr (outcomeName r.Outcome)
              "resultFile", jstr r.Source ]
    let requirementIds = p.Requirements |> List.map (fun r -> r.Id)
    let reqStatus = requirementIds |> List.map (fun r -> r, requirementVerification trace r)
    jobj
        [ "schema", jstr "causa.acceptance-traceability/1"
          "description",
          jstr
              "Every obligation of the pinned verification plan, bound to the named tests that claim it by VPO id. Generated by tools/acceptance (causa-acceptance bind); never edited by hand. missing = no test names the obligation; declared = a test source names it but no result was found in this run; bound = an executed result names it."
          "plan", jobj [ "planId", jstr p.PlanId; "digest", jstr p.Digest; "baselineId", jstr p.BaselineId ]
          "context", jobj context
          "summary",
          jobj
              [ "requirements", jint p.Requirements.Length
                "obligations", jint trace.Obligations.Length
                "binding",
                jobj
                    [ "bound", jint (count (fun o -> bindingName o.Binding = "bound") trace)
                      "declared", jint (count (fun o -> bindingName o.Binding = "declared") trace)
                      "missing", jint (count (fun o -> o.Binding = Missing) trace) ]
                "verification",
                jobj
                    [ "PASS", jint (count (fun o -> o.Verification = Pass) trace)
                      "FAIL", jint (count (fun o -> o.Verification = Fail) trace)
                      "NOT_RUN", jint (count (fun o -> o.Verification = NotRun) trace) ]
                "requirementsByVerification",
                jobj
                    [ for v in [ Pass; Fail; NotRun ] ->
                          verificationName v, jint (reqStatus |> List.filter (fun (_, s) -> s = v) |> List.length) ] ]
          "unknownTags", jarr (trace.UnknownTags |> List.map (fun (o, t) -> jobj [ "id", jstr o; "taggedBy", jstr t ]))
          "obligations",
          jarr (
              trace.Obligations
              |> List.map (fun t ->
                  jobj
                      [ "id", jstr t.Obligation.Id
                        "requirement", jstr t.Obligation.Requirement
                        "sourceObligation", jstr t.Obligation.SourceObligation
                        "binding", jstr (bindingName t.Binding)
                        "verification", jstr (verificationName t.Verification)
                        "declaredIn",
                        (match t.Binding with
                         | Declared srcs -> jstrs srcs
                         | _ -> jarr [])
                        "tests",
                        (match t.Binding with
                         | Bound rs -> jarr (rs |> List.map resultJson)
                         | _ -> jarr []) ])
          ) ]
