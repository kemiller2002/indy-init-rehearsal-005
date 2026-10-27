module Causa.Acceptance.Tests.AcceptanceTests

open System
open System.IO
open System.Xml.Linq
open Xunit
open Causa.Acceptance
open Causa.Acceptance.Json
open Causa.Acceptance.Discovery
open Causa.Acceptance.Traceability

// ---------------------------------------------------------------------------------------------------------------
// Fixtures

let o1 = "VPO-0000000000000001"
let o2 = "VPO-0000000000000002"
let o3 = "VPO-0000000000000003"

let plan =
    parse
        (sprintf
            """{"planIdentity":{"planId":"VP-test","digest":"d"},
                "source":{"repository":"r","commit":"c","baselineId":"BL","baselineSha256":"b","approvals":{"sha256":"a"}},
                "requirements":[
                  {"requirement":"REQ-1","priority":"P0","title":"t","document":"docs/x.md","verificationMethod":{"gates":["G3"]},
                   "freshness":{"policy":"FP-DETERMINISTIC-INPUT-DIGEST"},
                   "obligations":[{"id":"%s","sourceObligation":"REQ-1-O1","text":"one"},{"id":"%s","sourceObligation":"REQ-1-O2","text":"two"}]},
                  {"requirement":"REQ-2","priority":"P0","title":"t","document":"docs/x.md","verificationMethod":{"gates":["G3"]},
                   "freshness":{"policy":"FP-LIVE-PER-COMMIT"},
                   "obligations":[{"id":"%s","sourceObligation":"REQ-2-O1","text":"three"}]}]}"""
            o1
            o2
            o3)
    |> Plan.ofJson

let result id outcome obligations =
    { TestId = id; DisplayName = id; Suite = DotnetTest; Outcome = outcome; Obligations = obligations; Tags = []; Source = "r.trx" }

let verificationOf (trace: Trace) id =
    (trace.Obligations |> List.find (fun o -> o.Obligation.Id = id)).Verification

let tempDir () =
    let d = Path.Combine(Path.GetTempPath(), "causa-acc-" + Guid.NewGuid().ToString("N"))
    Directory.CreateDirectory d |> ignore
    d

// ---------------------------------------------------------------------------------------------------------------
// Discovery

[<Fact>]
let ``Playwright results are discovered with obligations from titles, tags and annotations`` () =
    let report =
        parse
            (sprintf
                """{"config":{},"suites":[{"title":"a.spec.js","file":"a.spec.js","specs":[
                     {"title":"passes %s","tags":[],"tests":[{"projectName":"chromium","status":"expected","annotations":[],"results":[{"status":"passed"}]}]},
                     {"title":"tagged","tags":["@%s"],"tests":[{"projectName":"chromium","status":"skipped","annotations":[],"results":[{"status":"skipped"}]}]},
                     {"title":"annotated","tags":[],"tests":[{"projectName":"chromium","status":"unexpected","annotations":[{"type":"obligation","description":"%s"}],"results":[{"status":"failed"}]}]},
                     {"title":"flaky","tags":["%s","stack-smoke-limen"],"tests":[{"projectName":"chromium","status":"flaky","annotations":[],"results":[{"status":"failed"},{"status":"passed"}]}]}],
                   "suites":[]}]}"""
                o1
                o2
                o3
                o1)
    let rs = parsePlaywright "TestResults/p.json" report
    Assert.Equal(4, rs.Length)
    Assert.Equal<string list>([ o1 ], rs[0].Obligations)
    Assert.Equal(Passed, rs[0].Outcome)
    Assert.Equal(Skipped, rs[1].Outcome)
    Assert.Equal<string list>([ o2 ], rs[1].Obligations)
    Assert.Equal(Failed, rs[2].Outcome)
    Assert.Equal<string list>([ o3 ], rs[2].Obligations)
    // A retry is not a pass.
    Assert.Equal(Failed, rs[3].Outcome)
    // The JSON reporter drops the `@` of a tag; it is restored, and an id tag still binds.
    Assert.Equal<string list>([ o1 ], rs[3].Obligations)
    Assert.Contains("@stack-smoke-limen", rs[3].Tags)
    Assert.StartsWith("playwright:a.spec.js > passes", rs[0].TestId)

[<Fact>]
let ``TRX results take obligations from the test name and from trait tags`` () =
    let trx =
        sprintf
            """<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010"><Results>
                 <UnitTestResult testId="1" testName="M.%s named" outcome="Passed" />
                 <UnitTestResult testId="2" testName="M.traited" outcome="Failed" />
                 <UnitTestResult testId="3" testName="M.not run" outcome="NotExecuted" />
                 <UnitTestResult testId="4" testName="M.contract 5.2 dotted" outcome="Passed" />
               </Results><TestDefinitions>
                 <UnitTest id="1"><TestMethod className="M" name="%s named" codeBase="x.dll" /></UnitTest>
                 <UnitTest id="2"><TestMethod className="M" name="traited" codeBase="x.dll" /></UnitTest>
                 <UnitTest id="3"><TestMethod className="M" name="not run" codeBase="x.dll" /></UnitTest>
                 <UnitTest id="4"><TestMethod className="M.contract 5" name="2 dotted" codeBase="x.dll" /></UnitTest>
               </TestDefinitions></TestRun>"""
            o1
            o1
    let traits = Map.ofList [ ("M", "traited"), [ o2 ]; ("M", "contract 5.2 dotted"), [ o3 ] ]
    let rs = parseTrx "r.trx" traits (XDocument.Parse trx)
    Assert.Equal<string list>([ o1 ], rs[0].Obligations)
    Assert.Equal("dotnet:M." + o1 + " named", rs[0].TestId)
    Assert.Equal<string list>([ o2 ], rs[1].Obligations)
    Assert.Equal(Failed, rs[1].Outcome)
    Assert.Equal(Skipped, rs[2].Outcome)
    // VSTest split the dotted method name at its '.'; the trait is still found by the full name.
    Assert.Equal<string list>([ o3 ], rs[3].Obligations)

// A private trait name, so this self-test's fake id never reaches the real binding.
[<Fact; Trait("obligation-selftest", "VPO-00000000000000ff")>]
let ``trait tags are read from a test assembly's metadata without running it`` () =
    let asm = Reflection.Assembly.GetExecutingAssembly().Location
    let traits = traitObligationsNamed "obligation-selftest" asm
    let found = traits |> Map.toList |> List.exists (fun (_, ids) -> ids = [ "VPO-00000000000000ff" ])
    Assert.True(found, sprintf "traits found: %A" traits)

// ---------------------------------------------------------------------------------------------------------------
// Traceability: nothing is reported PASS unless it ran and passed

[<Fact; Trait("obligation", "VPO-95819a9f20306ce1"); Trait("obligation", "VPO-fd43b3e4e7e02f59")>]
let ``failing, missing, skipped or merely declared tests are never reported PASS`` () =
    let trace =
        build
            plan
            [ result "t-pass" Passed [ o1 ]
              result "t-skip" Skipped [ o2 ] ]
            [ o3, "tests/x.fs" ]
    Assert.Equal(Pass, verificationOf trace o1)
    Assert.Equal(NotRun, verificationOf trace o2) // skipped
    Assert.Equal(NotRun, verificationOf trace o3) // declared in source, never ran
    Assert.Equal(NotRun, requirementVerification trace "REQ-1") // one obligation not passing
    let failing = build plan [ result "t-pass" Passed [ o1; o2 ]; result "t-fail" Failed [ o2 ] ] []
    Assert.Equal(Fail, verificationOf failing o2)
    Assert.Equal(Fail, requirementVerification failing "REQ-1")
    let missing = build plan [] []
    Assert.True(missing.Obligations |> List.forall (fun o -> o.Binding = Missing && o.Verification = NotRun))

[<Fact>]
let ``a requirement passes only when every obligation is bound and passing`` () =
    let trace = build plan [ result "a" Passed [ o1 ]; result "b" Passed [ o2 ] ] []
    Assert.Equal(Pass, requirementVerification trace "REQ-1")
    Assert.Equal(NotRun, requirementVerification trace "REQ-2")

[<Fact>]
let ``tags naming an id outside the plan are reported`` () =
    let trace = build plan [ result "a" Passed [ "VPO-ffffffffffffffff" ] ] []
    Assert.Equal<(string * string) list>([ "VPO-ffffffffffffffff", "a" ], trace.UnknownTags)

// ---------------------------------------------------------------------------------------------------------------
// Ledger

let ledgerInputs trace producers gates : Ledger.Inputs =
    { Plan = plan
      Trace = trace
      RunId = "run-1"
      SourceSha = String('a', 40)
      DeployedSha = None
      Producers = producers
      Gates = gates
      DefectClasses = [ { Id = "DC-1"; Severity = "HIGH"; Tests = []; FixSha = None; ReviewerEvidence = None; Status = "PASS" } ]
      ContractVersion = "c"
      FrozenFixtureDigest = "f"
      DependencyLockSha = "l"
      InterfaceManifestSha = "i"
      Command = "test"
      Environment = [ "runner.os", "Linux" ] }

let gate id requires : Ledger.GateRule = { Id = id; Title = id; Requires = requires; EvidenceType = "ci"; Note = "" }

let rowStatus (ledger: Text.Json.Nodes.JsonNode) collection id =
    items collection ledger |> List.find (fun r -> str "id" r = Some id) |> strOr "" "status"

[<Fact; Trait("obligation", "VPO-95819a9f20306ce1"); Trait("obligation", "VPO-2a6aa540f193ab6c")>]
let ``gates pass only on producers that succeeded in this run; absent, reused or failed producers do not`` () =
    let trace = build plan [ result "a" Passed [ o1 ]; result "b" Passed [ o2 ] ] []
    let producers: Ledger.Producer list =
        [ { Id = "core"; Status = Ledger.Succeeded; Command = "c"; Record = None }
          { Id = "browser"; Status = Ledger.ProducerFailed; Command = "b"; Record = None }
          { Id = "live"; Status = Ledger.producerStatusOf "reused"; Command = "l"; Record = None } ]
    let gates = [ gate "G1" [ "core" ]; gate "G2" [ "core"; "browser" ]; gate "G3" [ "core"; "live" ]; gate "G4" []; gate "G8" [ "traceability" ] ]
    let built = Ledger.build (ledgerInputs trace producers gates)
    Assert.Equal("PASS", rowStatus built.Ledger "gates" "G1")
    Assert.Equal("FAIL", rowStatus built.Ledger "gates" "G2")
    Assert.Equal("NOT_RUN", rowStatus built.Ledger "gates" "G3")
    Assert.Equal("NOT_RUN", rowStatus built.Ledger "gates" "G4")
    // REQ-2 has no test, so the computed traceability producer has not succeeded.
    Assert.Equal("NOT_RUN", rowStatus built.Ledger "gates" "G8")
    Assert.Equal("PASS", rowStatus built.Ledger "requirements" "REQ-1")
    Assert.Equal("NOT_RUN", rowStatus built.Ledger "requirements" "REQ-2")
    // A defect class claimed PASS without fix SHA and reviewer evidence stays open.
    Assert.Equal("NOT_RUN", rowStatus built.Ledger "defect_classes" "DC-1")
    Assert.False(built.Ledger["accepted"].GetValue<bool>())

[<Fact>]
let ``the bundle manifest pins every evidence file and the ledger pins the manifest`` () =
    let trace = build plan [ result "a" Passed [ o1 ]; result "b" Passed [ o2 ] ] []
    let inputs = ledgerInputs trace [] [ gate "G1" [] ]
    let dir = tempDir ()
    let sha = Ledger.finalize inputs dir (Ledger.build inputs)
    let ledger = load (Path.Combine(dir, "ledger.json"))
    Assert.Equal(sha, strOr "" "evidence_manifest_sha256" ledger)
    Assert.Equal(sha, sha256File (Path.Combine(dir, "evidence-manifest.json")))
    let manifest = load (Path.Combine(dir, "evidence-manifest.json"))
    for e in items "entries" manifest do
        Assert.Equal(strOr "" "sha256" e, sha256File (Path.Combine(dir, strOr "" "path" e)))
    let ev = load (Path.Combine(dir, "evidence/requirements/REQ-1.json"))
    Assert.Equal("causa.acceptance-evidence/1", strOr "" "schema" ev)
    Assert.Equal("run-1", strOr "" "run_id" ev)
    Assert.Equal("requirement", strOr "" "subject_type" ev)

// ---------------------------------------------------------------------------------------------------------------
// T11 lock gate

let channel =
    parse
        """{"profile":{"version":"1.29.0"},"components":[
             {"systemId":"praxis","version":"3.22.0","tag":"v3.22.0"},
             {"systemId":"limen","version":"0.9.0","tag":"v0.9.0"},
             {"systemId":"forma","version":"0.9.0","tag":"v0.9.0"},
             {"systemId":"folio","version":"0.4.1","tag":"v0.4.1"},
             {"systemId":"aegis","version":"1.0.0","tag":"nuget:EchelonFoundry.Aegis.Core@1.0.0"},
             {"systemId":"dokimos","version":"0.8.0","tag":"dokimos-v0.8.0"}]}"""

let writeRepo (installedLimen: string) (withPackageLock: bool) =
    let d = tempDir ()
    let w (rel: string) (text: string) =
        let p = Path.Combine(d, rel)
        Directory.CreateDirectory(Path.GetDirectoryName p) |> ignore
        File.WriteAllText(p, text)
    w "conditor.json" """{"components":[{"id":"praxis","version":"3.22.0"},{"id":"limen","version":"0.9.0"}]}"""
    let manifestSha = sha256File (Path.Combine(d, "conditor.json"))
    w ".conditor/lock.json" (sprintf """{"manifestSha256":"%s","components":[{"id":"praxis","version":"3.22.0"},{"id":"limen","version":"0.9.0"}]}""" manifestSha)
    w "package.json" """{"dependencies":{"@echelon-foundry/limen":"0.9.0","@echelon-foundry/design-system":"0.9.0","@echelon-foundry/print-components":"https://x/v0.4.1/echelon-foundry-print-components-0.4.1.tgz"}}"""
    w "node_modules/@echelon-foundry/limen/package.json" (sprintf """{"version":"%s"}""" installedLimen)
    w "node_modules/@echelon-foundry/design-system/package.json" """{"version":"0.9.0"}"""
    w "node_modules/@echelon-foundry/print-components/package.json" """{"version":"0.4.1"}"""
    if withPackageLock then
        w
            "package-lock.json"
            """{"packages":{"node_modules/@echelon-foundry/limen":{"version":"0.9.0"},"node_modules/@echelon-foundry/design-system":{"version":"0.9.0"},"node_modules/@echelon-foundry/print-components":{"version":"0.4.1"}}}"""
    w "src/App.fsproj" """<Project><ItemGroup><PackageReference Include="EchelonFoundry.Aegis.Core" Version="1.0.0" /></ItemGroup></Project>"""
    w "ros.json" """{"rosVersion":"3.22.0"}"""
    d

let gateInputs root hostVersions required : LockGate.Inputs =
    { Root = root
      Channel = channel
      ChannelSha256 = "abc"
      Pin = parse """{"channelSha256":"abc","profileVersion":"1.29.0","expected":{"limen":"0.9.0","dokimos":"0.8.0"}}"""
      HostVersions = hostVersions
      RequiredHostTools = required
      PackageLockCommitted = true }

let allTools = LockGate.hostTools |> List.map (fun (exe, _) -> exe, None) |> Map.ofList

[<Fact; Trait("obligation", "VPO-e49383126481c2f7")>]
let ``the lock gate fails when an installed artifact differs from the locked set`` () =
    let root = writeRepo "0.8.2" true
    let checks = LockGate.evaluate (gateInputs root (allTools |> Map.add "dokimos" (Some "0.8.0")) [ "dokimos" ])
    Assert.Equal("FAIL", LockGate.verdict checks)
    let bad = checks |> List.filter (fun c -> c.Status = LockGate.Fail)
    Assert.Equal<string list>([ "npm.installed.limen" ], bad |> List.map (fun c -> c.Id))
    // The same tree with the locked version installed passes those checks.
    let ok = LockGate.evaluate (gateInputs (writeRepo "0.9.0" true) (allTools |> Map.add "dokimos" (Some "0.8.0")) [ "dokimos" ])
    Assert.DoesNotContain(ok, fun c -> c.Status = LockGate.Fail)

[<Fact; Trait("obligation", "VPO-22f350dce03290a4")>]
let ``missing dependencies or unverifiable artifacts never yield PASS`` () =
    // No package-lock.json: INCOMPLETE, not PASS.
    let noLock = LockGate.evaluate (gateInputs (writeRepo "0.9.0" false) allTools [])
    Assert.Equal("INCOMPLETE", LockGate.verdict noLock)
    // A package-lock.json that exists but is not committed (generated by this install) proves nothing.
    let generated = LockGate.evaluate { gateInputs (writeRepo "0.9.0" true) allTools [] with PackageLockCommitted = false }
    Assert.Equal("INCOMPLETE", LockGate.verdict generated)
    // A required host tool that is not installed: FAIL.
    let noTool = LockGate.evaluate (gateInputs (writeRepo "0.9.0" true) allTools [ "dokimos" ])
    Assert.Equal("FAIL", LockGate.verdict noTool)
    // A host tool at another version: FAIL.
    let drift = LockGate.evaluate (gateInputs (writeRepo "0.9.0" true) (allTools |> Map.add "dokimos" (Some "0.7.0")) [])
    Assert.Equal("FAIL", LockGate.verdict drift)
    // A tampered channel copy: FAIL.
    let tampered = LockGate.evaluate { gateInputs (writeRepo "0.9.0" true) allTools [] with ChannelSha256 = "other" }
    Assert.Equal("FAIL", LockGate.verdict tampered)

[<Fact>]
let ``tool versions are read from both version formats`` () =
    Assert.Equal(Some "0.8.0", LockGate.parseToolVersion """{ "ReleaseVersion": "0.8.0", "DokimosVersion": "0.8.0+49a9" }""")
    Assert.Equal(Some "0.3.0", LockGate.parseToolVersion "percepta 0.3.0+c9747c2632074fc2cac94c1ad44ed22f5c2a251b")
    Assert.Equal(Some "1.18.0", LockGate.parseToolVersion "1.18.0")

// ---------------------------------------------------------------------------------------------------------------
// Computed gate producers

let selector name prefix contains tag minimum : Producers.Selector =
    { Name = name; TestIdPrefix = prefix; Contains = contains; Tag = tag; Minimum = minimum }

[<Fact; Trait("obligation", "VPO-95819a9f20306ce1")>]
let ``a producer succeeds only when every selected test ran and passed`` () =
    let results = [ result "dotnet:Suite.a illegal move" Passed []; result "dotnet:Suite.b forged" Failed []; result "dotnet:Other.c" Skipped [] ]
    let check s = (Producers.selectorCheck results s).Status
    Assert.Equal(Producers.CheckPass, check (selector "illegal" (Some "dotnet:Suite.") (Some "illegal") None 1))
    Assert.Equal(Producers.CheckFail, check (selector "suite" (Some "dotnet:Suite.") None None 1))
    Assert.Equal(Producers.CheckNotRun, check (selector "too few" (Some "dotnet:Suite.a") None None 2))
    Assert.Equal(Producers.CheckNotRun, check (selector "absent" None None (Some "@stack-smoke-limen") 1))
    Assert.Equal(Producers.CheckNotRun, check (selector "skipped" (Some "dotnet:Other.") None None 1))
    // A selector with no criterion selects nothing rather than everything.
    Assert.Equal(Producers.CheckNotRun, check (selector "empty" None None None 1))
    let c st : Producers.Check = { Name = "x"; Status = st; Detail = ""; Tests = [] }
    Assert.Equal(Ledger.Succeeded, Producers.status [ c Producers.CheckPass; c Producers.CheckPass ])
    Assert.Equal(Ledger.NotProduced, Producers.status [ c Producers.CheckPass; c Producers.CheckNotRun ])
    Assert.Equal(Ledger.ProducerFailed, Producers.status [ c Producers.CheckNotRun; c Producers.CheckFail ])
    Assert.Equal(Ledger.NotProduced, Producers.status [])

[<Fact>]
let ``playwright tags select stack smoke tests`` () =
    let r = { result "playwright:smoke.spec.js > Limen starts" Passed [] with Tags = [ "@stack-smoke-limen" ]; Suite = Playwright }
    Assert.Equal(Producers.CheckPass, (Producers.selectorCheck [ r ] (selector "limen" None None (Some "@stack-smoke-limen") 1)).Status)
    Assert.Equal(Producers.CheckNotRun, (Producers.selectorCheck [ r ] (selector "forma" None None (Some "@stack-smoke-forma") 1)).Status)

[<Fact>]
let ``a plan gate check passes only when every routed requirement passes`` () =
    let partial = build plan [ result "a" Passed [ o1 ]; result "b" Passed [ o2 ] ] []
    Assert.Equal(Producers.CheckNotRun, (Producers.planGateCheck partial "G3").Status)
    let failing = build plan [ result "a" Passed [ o1; o2 ]; result "c" Failed [ o3 ] ] []
    Assert.Equal(Producers.CheckFail, (Producers.planGateCheck failing "G3").Status)
    let full = build plan [ result "a" Passed [ o1; o2; o3 ] ] []
    Assert.Equal(Producers.CheckPass, (Producers.planGateCheck full "G3").Status)
    let none = Producers.planGateCheck full "G4"
    Assert.Equal(Producers.CheckPass, none.Status)
    Assert.Contains("routes no P0 requirement to G4", none.Detail)

[<Fact; Trait("obligation", "VPO-95819a9f20306ce1")>]
let ``a defect class counts only when the reviewer closed it at a fix in the candidate's history`` () =
    let dc status fix ev : Ledger.DefectClass =
        { Id = "DC-1"; Severity = "HIGH"; Tests = [ "DCT-1-A" ]; FixSha = fix; ReviewerEvidence = ev; Status = status }
    let results = [ result "dotnet:S.DCT-1-A reproduces" Passed [] ]
    let statusOf isAncestor d = Producers.defectClassChecks results isAncestor [ d ] |> Producers.status
    Assert.Equal(Ledger.NotProduced, statusOf (fun _ -> true) (dc "NOT_RUN" None None))
    Assert.Equal(Ledger.NotProduced, statusOf (fun _ -> true) (dc "PASS" (Some "abc") None))
    Assert.Equal(Ledger.ProducerFailed, statusOf (fun _ -> false) (dc "PASS" (Some "abc") (Some "review.json")))
    Assert.Equal(Ledger.Succeeded, statusOf (fun _ -> true) (dc "PASS" (Some "abc") (Some "review.json")))
    let failingTest = Producers.defectClassChecks [ result "dotnet:S.DCT-1-A reproduces" Failed [] ] (fun _ -> true) [ dc "PASS" (Some "abc") (Some "r") ]
    Assert.Equal(Ledger.ProducerFailed, Producers.status failingTest)

[<Fact>]
let ``a committed file must exist and have its declared schema`` () =
    let read =
        function
        | "ok.json" -> Some("1", Some(parse """{"schema":"s/1"}"""))
        | "bad.json" -> Some("2", Some(parse """{"schema":"s/2"}"""))
        | _ -> None
    let st p = (Producers.fileCheck read { Path = p; Schema = Some "s/1" }).Status
    Assert.Equal(Producers.CheckPass, st "ok.json")
    Assert.Equal(Producers.CheckFail, st "bad.json")
    Assert.Equal(Producers.CheckFail, st "missing.json")

[<Fact>]
let ``curation bindings are applied to a copy only when every binding names a plan obligation`` () =
    let dir = tempDir ()
    let p name (text: string) =
        let f = Path.Combine(dir, name)
        File.WriteAllText(f, text)
        f
    let planFile = p "plan.json" (serialize (parse (sprintf """{"planIdentity":{"planId":"VP-x"},"requirements":[{"requirement":"REQ-1","obligations":[{"id":"%s","sourceObligation":"REQ-1-O1"},{"id":"%s","sourceObligation":"REQ-1-O2"}]}]}""" o1 o2)))
    let curationFile = p "curation.json" """{"schema":"indy-init.verification-plan-curation/1","testBindings":[],"defaultPolicy":"X"}"""
    let good = p "good.json" """{"testBindings":[{"obligation":"REQ-1-O2","tests":[{"id":"dotnet:B","kind":"deterministic"}]},{"obligation":"REQ-1-O1","tests":[{"id":"dotnet:A","kind":"live"}]}]}"""
    let bad = p "bad.json" """{"testBindings":[{"obligation":"REQ-9-O1","tests":[{"id":"dotnet:A","kind":"deterministic"}]}]}"""
    let out = Path.Combine(dir, "out.json")
    let run b = Program.applyCuration [ "curation", curationFile; "plan", planFile; "bindings", b; "out", out ]
    Assert.Equal(1, run bad)
    Assert.False(File.Exists out)
    Assert.Equal(0, run good)
    let applied = load out
    Assert.Equal<string list>([ "REQ-1-O1"; "REQ-1-O2" ], items "testBindings" applied |> List.map (strOr "" "obligation"))
    Assert.Equal("X", strOr "" "defaultPolicy" applied)

[<Fact; Trait("obligation", "VPO-8deeef0ef7b7e4ab")>]
let ``VER-005 an Echelon package outside the release set passes only when pinned against an accepted owner decision`` () =
    let root = writeRepo "0.9.0" true
    let w (rel: string) (text: string) =
        let p = Path.Combine(root, rel)
        Directory.CreateDirectory(Path.GetDirectoryName p) |> ignore
        File.WriteAllText(p, text)
    let pkg = parse (File.ReadAllText(Path.Combine(root, "package.json")))
    pkg["dependencies"]["@echelon-foundry/forma-workflow"] <- jstr "1.1.0"
    w "package.json" (serialize pkg)
    let lock = parse (File.ReadAllText(Path.Combine(root, "package-lock.json")))
    lock["packages"]["node_modules/@echelon-foundry/forma-workflow"] <- parse """{"version":"1.1.0","integrity":"sha512-abc"}"""
    w "package-lock.json" (serialize lock)
    w "node_modules/@echelon-foundry/forma-workflow/package.json" """{"version":"1.1.0"}"""
    let decision status = sprintf "---\nid: DF-1\nstatus: %s\n---\n# Decision\n\n`@echelon-foundry/forma-workflow` 1.1.0 approved.\n" status
    let pinWith (outside: string) =
        parse (sprintf """{"channelSha256":"abc","profileVersion":"1.29.0","expected":{"limen":"0.9.0"},"outsideChannel":%s}""" outside)
    let entry = """[{"package":"@echelon-foundry/forma-workflow","version":"1.1.0","integrity":"sha512-abc","decision":{"id":"DF-1","path":"decisions/DF-1.md"}}]"""
    let outside (pin: Text.Json.Nodes.JsonNode) =
        LockGate.evaluate { gateInputs root allTools [] with Pin = pin } |> List.filter (fun c -> c.Id.StartsWith "outside.")
    // Not pinned at all: the gate fails rather than ignoring the package.
    let unpinned = outside (pinWith "[]")
    Assert.Contains(unpinned, fun c -> c.Status = LockGate.Fail && c.Id = "outside.unpinned.@echelon-foundry/forma-workflow")
    // Pinned, but the decision is not accepted: fails.
    w "decisions/DF-1.md" (decision "proposed")
    Assert.Contains(outside (pinWith entry), fun c -> c.Id.StartsWith "outside.decision" && c.Status = LockGate.Fail)
    // Accepted decision, matching version and integrity: every check passes and is labelled outside the channel.
    w "decisions/DF-1.md" (decision "accepted")
    let ok = outside (pinWith entry)
    Assert.Equal(5, ok.Length)
    Assert.All(ok, fun c -> Assert.Equal(LockGate.Pass, c.Status); Assert.Contains("outside echelon-current; owner-approved DF-1", c.Subject))
    // A different integrity than the decision pinned fails.
    let other = entry.Replace("sha512-abc", "sha512-other")
    Assert.Contains(outside (pinWith other), fun c -> c.Id.StartsWith "outside.integrity" && c.Status = LockGate.Fail)
