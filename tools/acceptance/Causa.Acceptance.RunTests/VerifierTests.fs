/// Adversarial tests of the acceptance decision as this pipeline computes it: the pinned Indy-init verifiers
/// (compute-acceptance-verdict.py and requirements-compiler verify-plan), run over this run's own bundle and over
/// mutated copies of it. The candidate cannot be ACCEPTED by an edited claim, a forged row, a missing CI result, a
/// tampered baseline, a self-approved waiver, a narrative report or stale evidence.
module Causa.Acceptance.RunTests.VerifierTests

open System.IO
open Xunit
open Causa.Acceptance.Json
open Causa.Acceptance.RunTests.Run

let notAccepted (v: Verdict) =
    Assert.NotEqual<string>("ACCEPTED", verdictOf v)
    Assert.NotEqual(0, v.Code)

[<Fact; Trait("obligation", "VPO-6f2f2edbfc6b5c65"); Trait("obligation", "VPO-a5795f29e0de763c")>]
let ``AR-019 AR-001 the verdict is computed from the pinned baseline, plan, approvals and this run's evidence`` () =
    let b = baseline.Value
    let ledger = loadLedger b.Out
    // AR-001-O1: the run pins the requirements source, commit and content digest, and the plan.
    let bl = (field "requirements_baseline" ledger).Value
    let plan = load (planPath ())
    Assert.Equal(pin "BASELINE_SHA256", strOr "" "sha256" bl)
    Assert.Equal(sha256File (baselinePath ()), strOr "" "sha256" bl)
    Assert.Equal((gs (plan["source"]["commit"])), strOr "" "commit" bl)
    Assert.Equal((gs (plan["source"]["repository"])), strOr "" "repository" bl)
    Assert.Equal(env "SOURCE_SHA", strOr "" "source_commit_sha" ledger)
    Assert.Equal((gs (plan["planIdentity"]["planId"])), (gs (ledger["verification_plan"]["planId"])))
    // AR-019-O1: both verifiers computed a verdict over exactly these inputs, with no integrity problem and no claim
    // problem; the report states only that computed verdict.
    let v = ledgerVerifier b.Out Map.empty []
    Assert.Contains(v.Code, [ 0; 3 ])
    Assert.Empty(items "claim_problems" v.Json)
    let p = planVerifier b.Out Map.empty
    Assert.Contains(p.Code, [ 0; 3 ])
    Assert.Empty(items "integrityProblems" p.Json)
    Assert.Equal(verdictOf v, strOr "" "verdict" (load (Path.Combine(b.Out, "verdict.json"))))
    let report = File.ReadAllText(Path.Combine(b.Out, "report.md"))
    Assert.Contains(sprintf "<!-- acceptance-verdict: %s -->" (verdictOf v), report)
    // Every reviewer closure the ledger reports PASS carries bundled, manifest-pinned review evidence the verifier accepts.
    let reasons = (items "reasons" v.Json @ items "claim_problems" v.Json) |> List.map (fun n -> n.ToJsonString())
    Assert.DoesNotContain(reasons, fun r -> r.Contains "defect class" && (r.Contains "absent from the trusted manifest" || r.Contains "independent review evidence is missing"))

[<Fact; Trait("obligation", "VPO-a96869ed8f00e776"); Trait("obligation", "VPO-a191144b5315b64a")>]
let ``AR-019 a false accepted flag in the ledger is a contradictory claim, never an acceptance`` () =
    let dir = copyBundle ()
    let ledger = loadLedger dir
    // Make sure the honest outcome is not acceptance (a gate that did not run), then claim acceptance anyway.
    let g = (ledger["gates"].AsArray() |> Seq.find (fun g -> str "id" g = Some "G7")).AsObject()
    g["status"] <- jstr "NOT_RUN"
    g["verified_sha"] <- null
    g["evidence_paths"] <- jarr []
    ledger["accepted"] <- jbool true
    writeLedger dir ledger
    let v = ledgerVerifier dir Map.empty []
    notAccepted v
    Assert.Equal(1, v.Code)
    Assert.NotEmpty(items "claim_problems" v.Json)

[<Fact; Trait("obligation", "VPO-8d0817ad76ab9956"); Trait("obligation", "VPO-b65e916f00fd843a"); Trait("obligation", "VPO-a191144b5315b64a")>]
let ``AR-007 AR-018 rows forged to PASS without their evidence are not accepted by either verifier`` () =
    let dir = copyBundle ()
    let ledger = loadLedger dir
    let src = env "SOURCE_SHA"
    setAll ledger["requirements"] (fun r -> r["status"] <- jstr "PASS"; r["reason"] <- jstr "claimed")
    setAll ledger["gates"] (fun g -> g["status"] <- jstr "PASS"; g["verified_sha"] <- jstr src)
    setAll ledger["defect_classes"] (fun d -> d["status"] <- jstr "PASS")
    ledger["accepted"] <- jbool true
    writeLedger dir ledger
    notAccepted (ledgerVerifier dir Map.empty [])
    notAccepted (planVerifier dir Map.empty)

[<Fact; Trait("obligation", "VPO-9827ff5a95a7c74f"); Trait("obligation", "VPO-a191144b5315b64a")>]
let ``AR-018 AR-019 a gate claimed PASS without its CI evidence in the bundle is not accepted`` () =
    let dir = copyBundle ()
    let ledger = loadLedger dir
    let g = (ledger["gates"].AsArray() |> Seq.find (fun g -> str "id" g = Some "G8")).AsObject()
    g["status"] <- jstr "PASS"
    g["verified_sha"] <- jstr (env "SOURCE_SHA")
    g["evidence_paths"] <- jstrs [ "evidence/gates/G8.json" ] // no such file was produced by any CI job
    writeLedger dir ledger
    let v = ledgerVerifier dir Map.empty []
    notAccepted v
    Assert.Contains("G8", v.Raw.Out)

[<Fact; Trait("obligation", "VPO-e30db6532f5cf262"); Trait("obligation", "VPO-a191144b5315b64a")>]
let ``AR-001 AR-019 a modified baseline is detected by both verifiers`` () =
    let dir = copyBundle ()
    let tampered = Path.Combine(dir, "tampered-baseline.json")
    let text = File.ReadAllText(baselinePath ())
    File.WriteAllText(tampered, text.Replace("\"P0\"", "\"P1\"")) // demote requirements: a silent weakening
    Assert.NotEqual<string>(sha256File (baselinePath ()), sha256File tampered)
    // With the pinned digest the tampered file is refused...
    let v = ledgerVerifier dir (Map [ "baseline", tampered ]) []
    notAccepted v
    Assert.NotEqual<string>("INCOMPLETE", verdictOf v)
    // ...and pinning the tampered file's own digest is still refused against the plan and the ledger.
    let p = planVerifier dir (Map [ "baseline", tampered ])
    notAccepted p
    Assert.NotEmpty(items "integrityProblems" p.Json)
    // acceptance.sh itself refuses to run on a baseline whose digest differs from its pin.
    let check = exec "sh" [ "-c"; sprintf "echo '%s  %s' | sha256sum -c -" (pin "BASELINE_SHA256") tampered ] repoRoot []
    Assert.NotEqual(0, check.Code)

[<Fact; Trait("obligation", "VPO-9b0ea135ac4e6776"); Trait("obligation", "VPO-74e10fdbcee13f97"); Trait("obligation", "VPO-a191144b5315b64a")>]
let ``AR-003 AR-019 an exclusion the agent approved itself is refused by both verifiers`` () =
    let dir = copyBundle ()
    let ledger = loadLedger dir
    let reqs = ledger["requirements"].AsArray()
    let first = reqs[0]
    let rid = strOr "" "id" first
    reqs.Remove first |> ignore
    ledger["scope_changes"] <-
        jarr [ jobj [ "requirement_id", jstr rid; "action", jstr "exclude"; "approval_id", jstr "AGENT-SELF-APPROVED"; "reason", jstr "out of time" ] ]
    writeLedger dir ledger
    let v = ledgerVerifier dir Map.empty []
    notAccepted v
    let p = planVerifier dir Map.empty
    notAccepted p
    Assert.Equal("INVALID", strOr "" "verdict" p.Json)
    Assert.Contains(items "integrityProblems" p.Json, fun n -> n.GetValue<string>().Contains "unauthorized exclusion")

[<Fact; Trait("obligation", "VPO-b65e916f00fd843a")>]
let ``AR-018 a report narrating acceptance cannot override the computed verdict`` () =
    let dir = copyBundle ()
    let report = Path.Combine(dir, "narrative.md")
    File.WriteAllText(report, "<!-- acceptance-verdict: ACCEPTED -->\n# Candidate\n\nOutcome: ACCEPTED. Every gate is green.\n")
    let v = ledgerVerifier dir Map.empty [ "--report"; report ]
    Assert.Equal(1, v.Code)
    Assert.NotEmpty(items "claim_problems" v.Json)

[<Fact; Trait("obligation", "VPO-9827ff5a95a7c74f")>]
let ``AR-018 evidence presented for another source SHA, or a tampered manifest, is not accepted`` () =
    let dir = copyBundle ()
    notAccepted (ledgerVerifier dir (Map [ "source-sha", String.replicate 40 "0" ]) [])
    notAccepted (planVerifier dir (Map [ "source-sha", String.replicate 40 "0" ]))
    notAccepted (ledgerVerifier dir (Map [ "manifest-sha256", String.replicate 64 "0" ]) [])
    let p = planVerifier dir (Map [ "manifest-sha256", String.replicate 64 "0" ])
    Assert.Equal("INVALID", strOr "" "verdict" p.Json)

[<Fact; Trait("obligation", "VPO-60d3de8d19b647a8")>]
let ``AR-004 the acceptance step fails closed on an unpinned plan, baseline or approvals`` () =
    let out = scratch "acc-pin"
    let run extra =
        exec "sh" [ "tools/acceptance/ci/acceptance.sh" ] repoRoot
            ([ "RUN_ID", "pin-check"; "SOURCE_SHA", env "SOURCE_SHA"; "PRODUCERS", envPath "PRODUCERS"; "OUT", out
               "INDY_INIT", indyInit (); "COMPILER", compiler (); "RESULTS", results () ] @ extra)
    let wrongPlan = run [ "PLAN_ID", "VP-000000000000" ]
    Assert.NotEqual(0, wrongPlan.Code)
    Assert.Contains("pinned VP-000000000000", wrongPlan.Err)
    // The script checks the baseline and the approvals against their pinned digests before anything else.
    let script = File.ReadAllText(Path.Combine(repoRoot, "tools/acceptance/ci/acceptance.sh"))
    Assert.Contains("echo \"$BASELINE_SHA256  $baseline\" | sha256sum -c -", script)
    Assert.Contains("echo \"$APPROVALS_SHA256  $OUT/inputs/approvals.causa.json\" | sha256sum -c -", script)
    Assert.Contains("set -eu", script)
    // The acceptance job runs on every run, whatever the other jobs did.
    let wf = File.ReadAllText(Path.Combine(repoRoot, ".github/workflows/build-and-test.yml"))
    Assert.Contains("if: always() && needs.reuse.result != 'cancelled'", wf)
