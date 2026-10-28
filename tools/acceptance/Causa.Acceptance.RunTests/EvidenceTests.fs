/// Tests of this run's own evidence: the bundle acceptance.sh builds, the T11 lock-gate record, the per-layer
/// results, the preservation gate record and the built site. Each reads what this run produced; nothing is mocked.
module Causa.Acceptance.RunTests.EvidenceTests

open System
open System.IO
open System.Text
open Xunit
open Causa.Acceptance
open Causa.Acceptance.Json
open Causa.Acceptance.RunTests.Run

let private isHex n (s: string) = s.Length = n && s |> Seq.forall Uri.IsHexDigit

// ---------------------------------------------------------------------------------------------------------------
// The bundle

[<Fact; Trait("obligation", "VPO-42705f754abba51b"); Trait("obligation", "VPO-79a72fb717368c95")>]
let ``AR-007 AR-011 every PASS cites its tests, result files, command, environment, run and source SHA; the ledger records the build inputs`` () =
    let b = baseline.Value
    let ledger = loadLedger b.Out
    let manifest = load (Path.Combine(b.Out, "bundle/evidence-manifest.json"))
    let passing = items "requirements" ledger |> List.filter (fun r -> str "status" r = Some "PASS")
    Assert.NotEmpty passing
    for r in passing do
        let paths = strings "evidence_paths" r
        Assert.Single paths |> ignore
        let ev = load (Path.Combine(b.Out, "bundle", paths.Head))
        Assert.Equal(env "SOURCE_SHA", strOr "" "source_sha" ev)
        Assert.Equal(strOr "" "run_id" ledger, strOr "" "run_id" ev)
        Assert.False(String.IsNullOrWhiteSpace(strOr "" "command" ev))
        let envNode = (field "environment" ev).Value
        Assert.False(String.IsNullOrWhiteSpace(strOr "" "dotnet.runtime" envNode))
        Assert.False(String.IsNullOrWhiteSpace(strOr "" "ci.run" envNode))
        for o in items "obligations" ev do
            Assert.Equal(Some "PASS", str "verification" o)
            let tests = items "tests" o
            Assert.NotEmpty tests
            for t in tests do
                Assert.Equal(Some "passed", str "outcome" t)
                Assert.True(File.Exists(Path.Combine(repoRoot, strOr "" "resultFile" t)), strOr "" "resultFile" t)
        // The manifest pins the file's bytes.
        Assert.Contains(items "entries" manifest, fun e -> str "path" e = Some paths.Head && str "sha256" e = Some(sha256File (Path.Combine(b.Out, "bundle", paths.Head))))
    // AR-011-O1: pinned dependency digest, frozen fixture digest at a causa-data commit, interface manifest digest,
    // CI run id, and the tool versions the lock gate observed.
    Assert.True(isHex 64 (strOr "" "dependency_lock_sha" ledger))
    let frozen = strOr "" "frozen_fixture_digest" ledger
    Assert.True(isHex 64 (frozen.Split(' ')[0]), frozen)
    Assert.Contains("causa-data@", frozen)
    Assert.True(isHex 64 ((strOr "" "interface_manifest_sha" ledger).Split(' ')[0]))
    Assert.Equal(env "RUN_ID", strOr "" "run_id" ledger)
    let gate = load (Path.Combine(results (), "lock-gate/lock-gate.json"))
    let observed = items "checks" gate |> List.filter (fun c -> (strOr "" "subject" c).EndsWith " --version")
    Assert.True(observed.Length >= 5, sprintf "%d host tool versions observed" observed.Length)

// ---------------------------------------------------------------------------------------------------------------
// The T11 lock gate record of this run

let lockGate () = load (Path.Combine(results (), "lock-gate/lock-gate.json"))
let checks () = items "checks" (lockGate ())
let subjects () = checks () |> List.map (strOr "" "subject")

[<Fact; Trait("obligation", "VPO-0fcb67bd52732582"); Trait("obligation", "VPO-90a9a6272db10df8"); Trait("obligation", "VPO-7478642aec32b7da")>]
let ``VER-004 AR-023 the exact release set is pinned by digest and the run used exactly that pin`` () =
    let gate = lockGate ()
    Assert.Equal(Some "PASS", str "verdict" gate)
    let pin = load (Path.Combine(repoRoot, "acceptance/stack/pin.json"))
    // An immutable registry commit (not a branch) and the channel file's digest.
    Assert.True(isHex 40 ((gs (pin["registry"]["commit"]))))
    let channelFile = Path.Combine(repoRoot, strOr "" "channelFile" pin)
    Assert.Equal(strOr "" "channelSha256" pin, sha256File channelFile)
    let first = checks () |> List.find (fun c -> strOr "" "subject" c = "acceptance/stack channel resolution")
    Assert.Equal(Some "PASS", str "status" first)
    Assert.Equal(strOr "" "channelSha256" pin, strOr "" "actual" first)
    // Every npm package of the stack is locked to an integrity digest; every channel artifact has a SHA-256.
    for c in checks () |> List.filter (fun c -> (strOr "" "subject" c).StartsWith "package-lock.json ") do
        Assert.StartsWith("sha512-", strOr "" "detail" c)
    let channel = load channelFile
    for comp in items "components" channel do
        for a in items "artifacts" comp do
            Assert.True(isHex 64 (strOr "" "sha256" a), sprintf "%s %s" (strOr "" "systemId" comp) (strOr "" "name" a))
    // No waiver: every check of the gate passed (a waiver would need recorded evidence; none is claimed).
    Assert.All(checks (), fun c -> Assert.Equal(Some "PASS", str "status" c))

[<Fact; Trait("obligation", "VPO-8828df556b382d9a"); Trait("obligation", "VPO-882ef92ed9177a58"); Trait("obligation", "VPO-d1885e94421cf8a4"); Trait("obligation", "VPO-9145c1f2ed57ebca")>]
let ``VER-005 VER-003 AR-023 the installed stack is the published release set, with no prerelease and no fallback`` () =
    let pin = load (Path.Combine(repoRoot, "acceptance/stack/pin.json"))
    let channel = load (Path.Combine(repoRoot, strOr "" "channelFile" pin))
    // AR-023-O1: a released, verified profile of the registry, at the pinned version.
    Assert.Equal(Some "echelon-current", channel["profile"] |> str "id")
    Assert.Equal(strOr "" "profileVersion" pin, (gs (channel["profile"]["version"])))
    let prerelease = Text.RegularExpressions.Regex(@"-(alpha|beta|rc|pre|preview|dev|snapshot)|\+local", Text.RegularExpressions.RegexOptions.IgnoreCase)
    for c in checks () do
        for v in [ strOr "" "expected" c; strOr "" "actual" c ] do
            Assert.False(prerelease.IsMatch v, sprintf "%s: %s" (strOr "" "subject" c) v)
        // VER-003-O3: what is installed is exactly what is expected; nothing fell back to another version.
        Assert.Equal(strOr "" "expected" c, strOr "" "actual" c)
    // AR-023-O2: Dokimos and Percepta are in the set with published artifacts, installed and observed at those versions.
    for exe, system in [ "dokimos", "dokimos"; "percepta", "percepta-semantic"; "percepta-repo", "percepta" ] do
        let comp = items "components" channel |> List.find (fun c -> str "systemId" c = Some system)
        Assert.NotEmpty(items "artifacts" comp)
        let observed = checks () |> List.find (fun c -> strOr "" "subject" c = exe + " --version")
        Assert.Equal(strOr "" "version" comp, strOr "" "actual" observed)

// ---------------------------------------------------------------------------------------------------------------
// Per-layer results

let resultsOf dir =
    Discovery.discover repoRoot [ Path.Combine(results (), dir) ] [ repoRoot ]

[<Fact; Trait("obligation", "VPO-8579de88e6b699f8")>]
let ``PF-011 the domain layer is deterministic F# validation, replay and transition tests that ran and passed without credentials`` () =
    let domain = resultsOf "dotnet" |> List.filter (fun r -> r.TestId.StartsWith "dotnet:App.Engine.Tests.")
    Assert.True(domain.Length >= 20, sprintf "%d domain tests" domain.Length)
    Assert.All(domain, fun r -> Assert.Equal(Discovery.Passed, r.Outcome))
    let has (s: string) = domain |> List.exists (fun r -> r.DisplayName.Contains(s, StringComparison.OrdinalIgnoreCase))
    Assert.True(has "replay", "replay tests")
    Assert.True(has "transition", "transition tests")
    Assert.True(has "refused" || has "rejection", "validation refusals")
    let wf = File.ReadAllText(Path.Combine(repoRoot, ".github/workflows/build-and-test.yml"))
    let core = wf.Substring(wf.IndexOf "\n  core:\n", wf.IndexOf "\n  lock-gate:\n" - wf.IndexOf "\n  core:\n")
    Assert.DoesNotContain("secrets.", core)

[<Fact; Trait("obligation", "VPO-91b97073e4e85125")>]
let ``PF-011 the UI layer ran in a real browser against local builds, covering responsive, keyboard, palette, loading and failure`` () =
    let ui = resultsOf "playwright"
    Assert.NotEmpty ui
    Assert.All(ui, fun r -> Assert.Equal(Discovery.Passed, r.Outcome))
    Assert.DoesNotContain(ui, fun r -> r.Tags |> List.contains "@live")
    let has (s: string) = ui |> List.exists (fun r -> r.DisplayName.Contains(s, StringComparison.OrdinalIgnoreCase) || r.TestId.Contains(s, StringComparison.OrdinalIgnoreCase))
    for s in [ "ipad-portrait"; "phone-320"; "keyboard"; "palette"; "WASM-LOAD"; "503" ] do
        Assert.True(has s, s)

[<Fact; Trait("obligation", "VPO-b82b86a55e20e54f")>]
let ``VER-006 the integrations were exercised in this run, not only compared as version strings`` () =
    let ui = resultsOf "playwright"
    let passedTag t = ui |> List.exists (fun r -> r.Tags |> List.contains t && r.Outcome = Discovery.Passed)
    Assert.True(passedTag "@stack-smoke-limen", "Limen")
    Assert.True(passedTag "@stack-smoke-forma", "Forma")
    Assert.True(passedTag "@stack-smoke-folio", "Folio")
    Assert.Contains(ui, fun r -> r.DisplayName.Contains "forma-workflow" && r.Outcome = Discovery.Passed)
    Assert.Contains(ui, fun r -> r.DisplayName.Contains "Percepta contract" && r.Outcome = Discovery.Passed)
    let records = envPath "GATE_RECORDS"
    let preservation = load (Path.Combine(records, "preservation-gate/gate.json"))
    Assert.Equal(Some "pass", str "verdict" preservation)
    let wasm = File.ReadAllText(Path.Combine(records, "wasm-smoke/wasm-smoke.log"))
    Assert.Contains("WASM SMOKE PASSED", wasm)

// ---------------------------------------------------------------------------------------------------------------
// Requirements preservation (this run's re-run of the gate)

[<Fact; Trait("obligation", "VPO-b5f193731c58028b"); Trait("obligation", "VPO-b55ed2a4a2fd465c")>]
let ``AR-002 WDL-015 every baseline obligation, WDL-001 to WDL-015 included, maps to a derived criterion or a recorded disposition`` () =
    let gate = load (Path.Combine(envPath "GATE_RECORDS", "preservation-gate/gate.json"))
    Assert.Equal(Some "pass", str "verdict" gate)
    let summary = (field "summary" gate).Value
    let baselineDoc = load (baselinePath ())
    let reqs = items "requirements" baselineDoc
    let obligations = reqs |> List.sumBy (fun r -> (items "obligations" r).Length)
    Assert.Equal(reqs.Length, (gi (summary["requirements"])))
    Assert.Equal(obligations, (gi (summary["obligations"])))
    for k in [ "exclusions"; "fail"; "drift"; "blocked"; "needsReview" ] do
        Assert.Equal(0, (gi (summary[k])))
    Assert.Equal(sha256File (baselinePath ()), (gs (gate["baseline"]["sha256"])))
    // The independent verbatim-carriage check of the same package passed in the same job.
    let derivation = load (Path.Combine(envPath "GATE_RECORDS", "preservation-gate/derivation-check.json"))
    Assert.Equal(Some "pass", str "verdict" derivation)
    Assert.Equal(obligations, gi (derivation["summary"]["obligations"]))
    // WDL-001..WDL-015 are each present in the derived package with every obligation carried.
    let derived = Directory.GetFiles(Path.Combine(repoRoot, "research/requirements"), "*.md") |> Array.map File.ReadAllText |> String.concat "\n"
    for i in 1..15 do
        let id = sprintf "WDL-%03d" i
        let r = reqs |> List.find (fun r -> str "id" r = Some id)
        for o in items "obligations" r do
            Assert.Contains(sprintf "| %s |" (strOr "" "id" o), derived)

// ---------------------------------------------------------------------------------------------------------------
// The built site carries no investigation records

let private recordMarkers () =
    let data = envPath "CAUSA_DATA_DIR"
    let fixtureIds = Directory.GetDirectories(Path.Combine(data, "fixtures/v1.0.0/investigations")) |> Array.map Path.GetFileName |> List.ofArray
    let active = load (Path.Combine(data, "catalog/active.json"))
    let catalog = load (Path.Combine(data, strOr "" "manifest" active))
    let historicalIds = items "cases" catalog |> List.map (strOr "" "investigationId")
    // Each record's first event line is distinctive content (not only an id).
    let firstEvents =
        [ for d in Directory.GetDirectories(Path.Combine(data, "fixtures/v1.0.0/investigations")) do
              let f = Path.Combine(d, "events.ndjson")
              if File.Exists f then yield File.ReadLines f |> Seq.head
          for c in items "cases" catalog do
              let f = Path.Combine(data, strOr "" "path" c, "events.ndjson")
              if File.Exists f then yield File.ReadLines f |> Seq.head ]
    fixtureIds, historicalIds, firstEvents

let private scan (roots: string list) (needles: string list) =
    let bytes = needles |> List.map (fun n -> n, Encoding.UTF8.GetBytes n)
    [ for root in roots do
          for f in Directory.GetFiles(root, "*", SearchOption.AllDirectories) do
              let content = File.ReadAllBytes f
              for n, b in bytes do
                  if content.AsSpan().IndexOf(ReadOnlySpan b) >= 0 then yield sprintf "%s contains %s" (Path.GetRelativePath(repoRoot, f)) (n.Substring(0, min 60 n.Length)) ]

[<Fact; Trait("obligation", "VPO-f8cbaa452cd4eb0b"); Trait("obligation", "VPO-0304a8342e7147dd"); Trait("obligation", "VPO-77f6e22e08d5bba0")>]
let ``EXT-DATA-001 EXT-DATA-013 the built site and application source contain none of the eight investigation records`` () =
    let site = envPath "SITE_DIR"
    Assert.True(File.Exists(Path.Combine(site, "index.html")), "the built site (the artifact Pages deploys) is present")
    Assert.True(Directory.GetFiles(site, "*", SearchOption.AllDirectories).Length > 50)
    let fixtureIds, historicalIds, firstEvents = recordMarkers ()
    Assert.Equal(5, fixtureIds.Length)
    Assert.Equal(3, historicalIds.Length)
    Assert.Equal(8, firstEvents.Length)
    let needles = fixtureIds @ historicalIds @ firstEvents
    let srcRoots = [ "src" ] |> List.map (fun d -> Path.Combine(repoRoot, d)) |> List.filter Directory.Exists
    Assert.Empty(scan (site :: srcRoots) needles)
    // The synthetic fixtures stay the storage contract's test corpus: the tests read them from causa-data at run time.
    let replay = resultsOf "dotnet" |> List.filter (fun r -> r.TestId.StartsWith "dotnet:App.Engine.Tests.FixtureReplayTests.")
    Assert.NotEmpty replay
    Assert.All(replay, fun r -> Assert.Equal(Discovery.Passed, r.Outcome))

[<Fact; Trait("obligation", "VPO-d8a63d43b7a2cf56")>]
let ``EXT-DATA-010 R001 is unchanged: its repository head is still the commit Indy-init records as the R1 baseline`` () =
    let sources = load (Path.Combine(indyInit (), "requirements-compiled/v1/curation/sources.json"))
    let entries = items "repositories" sources
    let recorded = entries |> List.find (fun e -> str "name" e = Some "indy-init-rehearsal-001") |> strOr "" "commit"
    Assert.True(isHex 40 recorded)
    let r = exec "git" [ "ls-remote"; "https://github.com/kemiller2002/indy-init-rehearsal-001.git"; "HEAD" ] repoRoot []
    Assert.Equal(0, r.Code)
    Assert.Equal(recorded, r.Out.Split('\t').[0].Trim())

// ---------------------------------------------------------------------------------------------------------------
// Layer timings, the cross-system integrations, and the test/fixture to requirement map

[<Fact; Trait("obligation", "VPO-39d6eb77cb2097af")>]
let ``PF-012 this run recorded each layer's measured time and outcome, and sets no budget before a baseline`` () =
    let t = load (Path.Combine(envPath "GATE_RECORDS", "layer-timings.json"))
    Assert.Equal(Some "causa.layer-timings/1", str "schema" t)
    Assert.Equal(env "RUN_ID", strOr "" "run" t)
    Assert.Null(t["budgets"])
    let jobs = items "jobs" t
    for layer, job in [ "domain", "core"; "ui", "browser"; "build", "site"; "dependencies", "lock-gate" ] do
        let j = jobs |> List.find (fun j -> str "name" j = Some job)
        Assert.Equal(Some layer, str "layer" j)
        Assert.True((gi j["seconds"]) >= 0, job)
        Assert.False(String.IsNullOrEmpty(strOr "" "conclusion" j), job)
    // A failure is reported by its layer: every job carries its own conclusion.
    Assert.All(jobs, fun j -> Assert.True((str "conclusion" j).IsSome || str "status" j <> Some "completed"))

[<Fact; Trait("obligation", "VPO-4e417c6f238cb460")>]
let ``VER-006 every integrated Echelon system accepted this candidate's real inputs in this run`` () =
    let b = baseline.Value
    // Ordo: the admitted plan scheduled over this run's evidence manifest, bound to this run and SHA.
    Assert.Equal("0", File.ReadAllText(Path.Combine(b.Out, "verification-schedule.exit")).Trim())
    let schedule = load (Path.Combine(b.Out, "verification-schedule.json"))
    let plan = load (planPath ())
    Assert.Contains(gs (plan["planIdentity"]["digest"]), strOr "" "planDigest" schedule)
    Assert.Equal(0, gi (schedule["summary"]["staleEvidence"]))
    Assert.Equal(items "requirements" plan |> List.length, gi (schedule["summary"]["requirements"]))
    // Dokimos: this run's evidence plan decided every declared producer.
    let reuse = load (Path.Combine(envPath "GATE_RECORDS", "reuse-plan/plan.json"))
    for p in [ "core"; "site"; "browser"; "lock-gate" ] do
        let t = items "Tests" reuse |> List.find (fun t -> str "TestId" t = Some p)
        Assert.True((str "Decision" t).IsSome, p)
    // Praxis: the preservation gate passed on this run's derived package.
    Assert.Equal(Some "pass", str "verdict" (load (Path.Combine(envPath "GATE_RECORDS", "preservation-gate/gate.json"))))
    // Percepta, Limen, Forma, workflow layout, Folio: exercised in the browser.
    let ui = resultsOf "playwright"
    let passed (f: Discovery.TestResult -> bool) = ui |> List.exists (fun r -> f r && r.Outcome = Discovery.Passed)
    Assert.True(passed (fun r -> r.DisplayName.Contains "Percepta contract"), "Percepta")
    Assert.True(passed (fun r -> r.Tags |> List.contains "@stack-smoke-limen"), "Limen")
    Assert.True(passed (fun r -> r.Tags |> List.contains "@stack-smoke-forma"), "Forma")
    Assert.True(passed (fun r -> r.DisplayName.Contains "forma-workflow"), "Forma workflow layout")
    Assert.True(passed (fun r -> r.Tags |> List.contains "@stack-smoke-folio"), "Folio")
    // The engine in .NET WebAssembly (Limen guest).
    Assert.Contains("WASM SMOKE PASSED", File.ReadAllText(Path.Combine(envPath "GATE_RECORDS", "wasm-smoke/wasm-smoke.log")))

[<Fact; Trait("obligation", "VPO-dfe1c434e9f7f329")>]
let ``PF-018 every executed test and fixture set is mapped to its requirements and authoritative source commit`` () =
    let b = baseline.Value
    let map = load (Path.Combine(b.Out, "test-requirement-map.json"))
    let plan = load (planPath ())
    Assert.Equal(gs (plan["source"]["commit"]), gs (map["requirementsSource"]["commit"]))
    Assert.Equal(gs (plan["source"]["repository"]), gs (map["requirementsSource"]["repository"]))
    let planReqs = items "requirements" plan |> List.map (strOr "" "requirement") |> Set.ofList
    let tests = items "tests" map
    Assert.NotEmpty tests
    for t in tests do
        let reqs = strings "requirements" t
        Assert.NotEmpty reqs
        Assert.All(reqs, fun r -> Assert.Contains(r, planReqs))
    // Fixture sets: present at the pinned causa-data commit, and every consumer test ran and passed in this run.
    let fx = load (Path.Combine(repoRoot, "acceptance/fixtures.json"))
    let data = envPath "CAUSA_DATA_DIR"
    let head = (exec "git" [ "-C"; data; "rev-parse"; "HEAD" ] repoRoot []).Out.Trim()
    Assert.Equal(strOr "" "commit" fx, head)
    Assert.Equal(strOr "" "commit" fx, gs (map["fixturesSource"]["commit"]))
    let dotnet = resultsOf "dotnet"
    let records = envPath "GATE_RECORDS"
    for set in items "fixtures" map do
        let ids = strings "ids" set
        Assert.NotEmpty ids
        Assert.All(strings "requirements" set, fun r -> Assert.Contains(r, planReqs))
        for c in strings "consumers" set do
            if c = "wasm-smoke" then
                let log = File.ReadAllText(Path.Combine(records, "wasm-smoke/wasm-smoke.log"))
                Assert.Contains("WASM SMOKE PASSED", log)
            else
                let consumed = dotnet |> List.filter (fun r -> r.TestId.StartsWith c)
                Assert.NotEmpty consumed
                Assert.All(consumed, fun r -> Assert.Equal(Discovery.Passed, r.Outcome))
