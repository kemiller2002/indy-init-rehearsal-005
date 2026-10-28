/// Builds the evidence bundle (causa.acceptance-evidence/1 files plus a causa.acceptance-evidence-manifest/1) and the
/// causa.acceptance-ledger/2 for one CI run at one source SHA.
///
/// The ledger only records what this run observed:
/// - a P0 requirement is PASS only when every one of its obligations is bound to executed tests that all passed;
///   FAIL when any bound test failed; NOT_RUN otherwise, with the missing obligations as the reason;
/// - a gate is PASS only when it names at least one producer and every producer it names succeeded in this run;
///   FAIL when any of them failed; NOT_RUN otherwise (including "no producer wired yet");
/// - a defect class is PASS only when its closure (fix SHA and reviewer evidence) is recorded in
///   acceptance/defect-classes.json and the record exists in the bundle.
/// The verdict is never computed here: tools/compute-acceptance-verdict.py and `requirements-compiler verify-plan`
/// compute it from this ledger and bundle.
module Causa.Acceptance.Ledger

open System
open System.IO
open Causa.Acceptance.Json
open Causa.Acceptance.Plan
open Causa.Acceptance.Discovery
open Causa.Acceptance.Traceability

type ProducerStatus =
    | Succeeded
    | ProducerFailed
    | NotProduced

/// One CI producer of evidence (a job or a check), as reported by the workflow that ran it.
type Producer =
    { Id: string
      Status: ProducerStatus
      Command: string
      Record: string option }

let producerStatusOf (s: string) =
    match s.ToLowerInvariant() with
    | "success" | "pass" | "passed" -> Succeeded
    | "failure" | "fail" | "failed" | "cancelled" | "timed_out" -> ProducerFailed
    | _ -> NotProduced

type GateRule =
    { Id: string
      Title: string
      Requires: string list
      EvidenceType: string
      Note: string }

type DefectClass =
    { Id: string
      Severity: string
      Tests: string list
      FixSha: string option
      ReviewerEvidence: string option
      Status: string }

type Inputs =
    { Plan: VerificationPlan
      Trace: Trace
      RunId: string
      SourceSha: string
      DeployedSha: string option
      Producers: Producer list
      Gates: GateRule list
      DefectClasses: DefectClass list
      ContractVersion: string
      FrozenFixtureDigest: string
      DependencyLockSha: string
      InterfaceManifestSha: string
      Command: string
      /// Every executed test result of this run (a closed defect class's failing-first tests are found here).
      Results: TestResult list
      /// Whether a commit is an ancestor of (or equal to) the candidate source commit.
      IsAncestor: string -> bool
      /// A repository file's bytes, or None when absent (the reviewer's evidence).
      ReadRepoFile: string -> byte[] option
      /// Where the evidence was produced (runner OS/image, .NET runtime, CI run URL): cited by every evidence file.
      Environment: (string * string) list }

let loadGates (node: Text.Json.Nodes.JsonNode) =
    items "gates" node
    |> List.map (fun g ->
        { Id = strOr "" "id" g
          Title = strOr "" "title" g
          Requires = strings "requires" g
          EvidenceType = strOr "gate" "evidenceType" g
          Note = strOr "" "note" g })

let loadDefectClasses (node: Text.Json.Nodes.JsonNode) =
    items "defectClasses" node
    |> List.map (fun d ->
        { Id = strOr "" "id" d
          Severity = strOr "HIGH" "severity" d
          Tests = strings "tests" d
          FixSha = str "fixSha" d
          ReviewerEvidence = str "reviewerEvidence" d
          Status = strOr "NOT_RUN" "status" d })

let loadProducers (node: Text.Json.Nodes.JsonNode) =
    items "producers" node
    |> List.map (fun p ->
        { Id = strOr "" "id" p
          Status = producerStatusOf (strOr "" "status" p)
          Command = strOr "" "command" p
          Record = str "record" p })

/// The traceability producer is computed, not reported: it succeeds only when every obligation is bound and passing.
let traceabilityProducer (trace: Trace) =
    let missing = trace.Obligations |> List.filter (fun o -> o.Verification <> Pass) |> List.length
    let failed = trace.Obligations |> List.exists (fun o -> o.Verification = Fail)
    { Id = "traceability"
      Status = (if failed then ProducerFailed elif missing = 0 then Succeeded else NotProduced)
      Command = "causa-acceptance bind"
      Record = Some "traceability.json" }

type Built =
    { Ledger: Text.Json.Nodes.JsonNode
      /// (bundle-relative path, JSON) of every evidence file.
      Evidence: (string * Text.Json.Nodes.JsonNode) list }

let private evidence (i: Inputs) subjectType subjectId status evidenceType (extra: (string * Text.Json.Nodes.JsonNode) list) =
    jobj (
        [ "schema", jstr "causa.acceptance-evidence/1"
          "run_id", jstr i.RunId
          "source_sha", jstr i.SourceSha
          "subject_type", jstr subjectType
          "subject_id", jstr subjectId
          "status", jstr status
          "exit_code", jint (if status = "PASS" then 0 else 1)
          "evidence_type", jstr evidenceType
          "command", jstr i.Command
          "environment", jobj (i.Environment |> List.map (fun (k, v) -> k, jstr v)) ]
        @ extra
    )

let build (i: Inputs) : Built =
    let producers =
        (traceabilityProducer i.Trace :: i.Producers) |> List.map (fun p -> p.Id, p) |> Map.ofList

    // Requirements -----------------------------------------------------------------------------------------
    let requirementRow (r: Requirement) =
        let os = i.Trace.Obligations |> List.filter (fun o -> o.Obligation.Requirement = r.Id)
        let tests =
            os
            |> List.collect (fun o ->
                match o.Binding with
                | Bound rs -> rs |> List.map (fun t -> t.TestId)
                | _ -> [])
            |> List.distinct
            |> List.sort
        let status = requirementVerification i.Trace r.Id
        let missing = os |> List.filter (fun o -> o.Binding = Missing) |> List.map (fun o -> o.Obligation.Id)
        let declared =
            os |> List.filter (fun o -> bindingName o.Binding = "declared") |> List.map (fun o -> o.Obligation.Id)
        let failing = os |> List.filter (fun o -> o.Verification = Fail) |> List.map (fun o -> o.Obligation.Id)
        let notPassed =
            os |> List.filter (fun o -> o.Verification = NotRun && bindingName o.Binding = "bound") |> List.map (fun o -> o.Obligation.Id)
        let reason =
            match status with
            | Pass -> sprintf "all %d obligations bound to executed tests that passed in run %s" os.Length i.RunId
            | Fail -> sprintf "bound tests failed for %s" (String.Join(", ", failing))
            | NotRun ->
                [ if not missing.IsEmpty then yield sprintf "%d of %d obligations have no named test (%s)" missing.Length os.Length (String.Join(", ", missing))
                  if not declared.IsEmpty then yield sprintf "tests declared in source but not run: %s" (String.Join(", ", declared))
                  if not notPassed.IsEmpty then yield sprintf "bound tests did not pass (skipped): %s" (String.Join(", ", notPassed)) ]
                |> fun parts -> String.Join("; ", parts)
        let statusName = verificationName status
        let path = sprintf "evidence/requirements/%s.json" r.Id
        let ev =
            if status = NotRun then None
            else
                Some(
                    path,
                    evidence i "requirement" r.Id statusName "test" [
                        "obligations",
                        jarr (
                            os
                            |> List.map (fun o ->
                                jobj
                                    [ "id", jstr o.Obligation.Id
                                      "verification", jstr (verificationName o.Verification)
                                      "tests",
                                      (match o.Binding with
                                       | Bound rs ->
                                           jarr (rs |> List.map (fun t -> jobj [ "testId", jstr t.TestId; "outcome", jstr (outcomeName t.Outcome); "resultFile", jstr t.Source ]))
                                       | _ -> jarr []) ])
                        )
                    ]
                )
        let row =
            jobj
                [ "id", jstr r.Id
                  "source_doc", jstr r.Document
                  "priority", jstr r.Priority
                  "owner", jstr "rehearsal-005"
                  "implementation_paths", jarr []
                  "tests", jstrs tests
                  "evidence_paths", (match ev with Some(p, _) -> jstrs [ p ] | None -> jarr [])
                  "status", jstr statusName
                  "reason", jstr reason ]
        row, ev

    let reqRows = i.Plan.Requirements |> List.map requirementRow

    // Gates ------------------------------------------------------------------------------------------------
    let gateRow (g: GateRule) =
        let seen = g.Requires |> List.map (fun id -> id, producers.TryFind id)
        let failed = seen |> List.filter (fun (_, p) -> p |> Option.exists (fun p -> p.Status = ProducerFailed)) |> List.map fst
        let absent = seen |> List.filter (fun (_, p) -> p |> Option.forall (fun p -> p.Status = NotProduced)) |> List.map fst
        let status, reason =
            if g.Requires.IsEmpty then "NOT_RUN", sprintf "no evidence producer is wired for %s yet%s" g.Id (if g.Note = "" then "" else ": " + g.Note)
            elif not failed.IsEmpty then "FAIL", sprintf "producer(s) failed in this run: %s" (String.Join(", ", failed))
            elif not absent.IsEmpty then "NOT_RUN", sprintf "producer(s) did not succeed in this run: %s" (String.Join(", ", absent))
            else "PASS", sprintf "every producer succeeded in run %s: %s" i.RunId (String.Join(", ", g.Requires))
        let path = sprintf "evidence/gates/%s.json" g.Id
        let ev =
            if status = "NOT_RUN" then None
            else
                Some(
                    path,
                    evidence i "gate" g.Id status g.EvidenceType [
                        "producers",
                        jarr (
                            seen
                            |> List.map (fun (id, p) ->
                                jobj
                                    [ "id", jstr id
                                      "status",
                                      jstr (
                                          match p with
                                          | Some { Status = Succeeded } -> "success"
                                          | Some { Status = ProducerFailed } -> "failure"
                                          | _ -> "not-produced"
                                      )
                                      "command", jstr (p |> Option.map (fun p -> p.Command) |> Option.defaultValue "")
                                      "record", (match p |> Option.bind (fun p -> p.Record) with Some r -> jstr r | None -> jnull) ])
                        )
                    ]
                )
        let row =
            jobj
                [ "id", jstr g.Id
                  "title", jstr g.Title
                  "status", jstr status
                  "verified_sha", (if status = "PASS" then jstr i.SourceSha else jnull)
                  "evidence_paths", (match ev with Some(p, _) -> jstrs [ p ] | None -> jarr [])
                  "requires", jstrs g.Requires
                  "reason", jstr reason ]
        row, ev

    let gateRows = i.Gates |> List.map gateRow

    // Defect classes ---------------------------------------------------------------------------------------
    // A class is PASS only when the independent reviewer closed it in acceptance/defect-classes.json (status PASS,
    // fix SHA, reviewer evidence) AND this run verifies that closure: the fix SHA is in the candidate's history, the
    // reviewer's evidence file exists in the candidate, and every failing-first test it lists ran and passed here.
    // Its bundle evidence (evidence_type "review") carries all of that, so the verifiers can check it. A class the
    // reviewer left open is never PASS.
    let defectRow (d: DefectClass) =
        let reviewerClosed = d.Status = "PASS" && d.FixSha.IsSome && d.ReviewerEvidence.IsSome
        let reviewPath, anchor =
            match d.ReviewerEvidence with
            | Some e ->
                let h = e.IndexOf '#'
                if h >= 0 then e.Substring(0, h), e.Substring(h + 1) else e, ""
            | None -> "", ""
        let reviewBytes = if reviewPath = "" then None else i.ReadRepoFile reviewPath
        let testRuns =
            d.Tests
            |> List.map (fun t ->
                t,
                i.Results
                |> List.filter (fun r -> r.DisplayName.Contains(t, StringComparison.Ordinal) || r.TestId.Contains(t, StringComparison.Ordinal)))
        let failedTests = testRuns |> List.filter (fun (_, rs) -> rs |> List.exists (fun r -> r.Outcome <> Passed && r.Outcome <> Skipped)) |> List.map fst
        let notRun = testRuns |> List.filter (fun (_, rs) -> rs.IsEmpty || rs |> List.exists (fun r -> r.Outcome = Skipped)) |> List.map fst
        let ancestor = d.FixSha |> Option.map i.IsAncestor |> Option.defaultValue false
        let status, reason =
            if not reviewerClosed then
                (if d.Status = "FAIL" then "FAIL" else "NOT_RUN"), "class closure not recorded by the independent reviewer: needs fix SHA, reviewer evidence and status PASS (G7)"
            elif not ancestor then "FAIL", sprintf "the recorded fix %s is not in the candidate's history" d.FixSha.Value
            elif reviewBytes.IsNone then "FAIL", sprintf "the reviewer evidence %s is not in the candidate" reviewPath
            elif not failedTests.IsEmpty then "FAIL", sprintf "failing-first tests failed in this run: %s" (String.Join(", ", failedTests))
            elif not notRun.IsEmpty then "NOT_RUN", sprintf "failing-first tests did not run in this run: %s" (String.Join(", ", notRun))
            else "PASS", sprintf "closed by the independent reviewer at fix %s; the fix is in the candidate's history and every failing-first test passed in run %s" d.FixSha.Value i.RunId
        let safeId = Text.RegularExpressions.Regex.Replace(d.Id, "[^A-Za-z0-9-]", "_")
        let path = sprintf "evidence/defects/%s.json" safeId
        let ev =
            if status <> "PASS" then None
            else
                Some(
                    path,
                    evidence i "defect" d.Id "PASS" "review" [
                        "fix_sha", jstr d.FixSha.Value
                        "fix_in_source_history", jbool true
                        "reviewer_evidence",
                        jobj
                            [ "path", jstr reviewPath
                              "anchor", jstr anchor
                              "sha256", jstr (sha256Hex reviewBytes.Value) ]
                        "tests",
                        jarr (
                            testRuns
                            |> List.collect (fun (t, rs) ->
                                rs |> List.map (fun r -> jobj [ "test", jstr t; "testId", jstr r.TestId; "outcome", jstr (outcomeName r.Outcome); "resultFile", jstr r.Source ]))
                        )
                    ]
                )
        let row =
            jobj
                [ "id", jstr d.Id
                  "severity", jstr d.Severity
                  "status", jstr status
                  "fix_sha", (match d.FixSha with Some s -> jstr s | None -> jnull)
                  "reviewer_evidence", (match d.ReviewerEvidence with Some s -> jstr s | None -> jnull)
                  "evidence_paths", (match ev with Some(p, _) -> jstrs [ p ] | None -> jarr [])
                  "tests", jstrs d.Tests
                  "reason", jstr reason ]
        row, ev

    let defectRows = i.DefectClasses |> List.map defectRow

    let allEvidence = (reqRows |> List.choose snd) @ (gateRows |> List.choose snd) @ (defectRows |> List.choose snd)

    let localAccepted =
        i.DeployedSha = Some i.SourceSha
        && gateRows |> List.forall (fun (g, _) -> str "status" g = Some "PASS")
        && reqRows |> List.forall (fun (r, _) -> str "status" r = Some "PASS")
        && defectRows |> List.forall (fun (d, _) -> str "status" d = Some "PASS")

    let ledger =
        jobj
            [ "schema", jstr "causa.acceptance-ledger/2"
              "run_id", jstr i.RunId
              "source_commit_sha", jstr i.SourceSha
              "deployed_commit_sha", (match i.DeployedSha with Some s -> jstr s | None -> jnull)
              "contract_version", jstr i.ContractVersion
              "frozen_fixture_digest", jstr i.FrozenFixtureDigest
              "dependency_lock_sha", jstr i.DependencyLockSha
              "interface_manifest_sha", jstr i.InterfaceManifestSha
              "requirements_baseline",
              jobj
                  [ "id", jstr i.Plan.BaselineId
                    "repository", jstr i.Plan.BaselineRepository
                    "commit", jstr i.Plan.BaselineCommit
                    "sha256", jstr i.Plan.BaselineSha256 ]
              "approvals_sha256", jstr i.Plan.ApprovalsSha256
              "evidence_manifest_sha256", jstr "" // filled by finalize, after the manifest is written
              "verification_plan", jobj [ "planId", jstr i.Plan.PlanId; "digest", jstr i.Plan.Digest ]
              "scope_changes", jarr []
              "requirements", jarr (reqRows |> List.map fst)
              "gates", jarr (gateRows |> List.map fst)
              "defect_classes", jarr (defectRows |> List.map fst)
              "unresolved_decisions", jarr []
              "accepted", jbool localAccepted ]

    { Ledger = ledger; Evidence = allEvidence }

/// Writes evidence files and the manifest under `outDir`, pins the manifest digest into the ledger, writes the
/// ledger, and returns the manifest SHA-256.
let finalize (i: Inputs) (outDir: string) (built: Built) =
    let entries =
        built.Evidence
        |> List.map (fun (path, node) ->
            let sha = Json.write (Path.Combine(outDir, path)) node
            jobj [ "path", jstr path; "sha256", jstr sha ])
    let manifest =
        jobj
            [ "schema", jstr "causa.acceptance-evidence-manifest/1"
              "run_id", jstr i.RunId
              "source_sha", jstr i.SourceSha
              "entries", jarr entries ]
    let manifestSha = Json.write (Path.Combine(outDir, "evidence-manifest.json")) manifest
    let ledger = built.Ledger.DeepClone()
    ledger["evidence_manifest_sha256"] <- jstr manifestSha
    Json.write (Path.Combine(outDir, "ledger.json")) ledger |> ignore
    manifestSha
