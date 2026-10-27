/// Tests of the candidate pipeline itself: the committed `.github/workflows/build-and-test.yml` and
/// `acceptance/reuse.json` as they are at this commit, and the Dokimos reuse decision logic. Each test reads the real
/// files; nothing is mocked.
module Causa.Acceptance.Tests.PipelineTests

open System
open System.IO
open System.Text.RegularExpressions
open Xunit
open Causa.Acceptance
open Causa.Acceptance.Json

let repoRoot =
    let rec up (d: DirectoryInfo) =
        if isNull d then failwith "repository root (App.slnx) not found above the test assembly"
        elif File.Exists(Path.Combine(d.FullName, "App.slnx")) then d.FullName
        else up d.Parent
    up (DirectoryInfo AppContext.BaseDirectory)

let workflowText = lazy (File.ReadAllText(Path.Combine(repoRoot, ".github/workflows/build-and-test.yml")))

/// Top-level jobs of the workflow: name -> its text (from its header to the next job header).
let jobs =
    lazy
        (let text = workflowText.Value
         let jobsAt = text.IndexOf("\njobs:\n")
         let body = text.Substring(jobsAt)
         let headers = Regex.Matches(body, @"(?m)^  ([a-z][a-z0-9-]*):\s*$") |> Seq.toList
         headers
         |> List.mapi (fun i m ->
             let stop = if i + 1 < headers.Length then headers[i + 1].Index else body.Length
             // Comment lines are not configuration (and the comment above a job header ends the previous job's text).
             let text = Regex.Replace(body.Substring(m.Index, stop - m.Index), @"(?m)^\s*#.*\n", "")
             m.Groups[1].Value, text)
         |> Map.ofList)

let job name =
    match jobs.Value.TryFind name with
    | Some t -> t
    | None -> failwithf "job %s is not in build-and-test.yml" name

let needsOf name =
    let m = Regex.Match(job name, @"(?m)^    needs:\s*(\[[^\]]*\]|[a-z-]+)")
    if not m.Success then []
    else m.Groups[1].Value.Trim('[', ']').Split(',') |> Array.map (fun s -> s.Trim()) |> Array.filter ((<>) "") |> List.ofArray

[<Fact; Trait("obligation", "VPO-770de2a43b888b53"); Trait("obligation", "VPO-23b26660a58ef990")>]
let ``AR-014 PF-012 only the live job receives credentials; every other job runs with read-only contents and no secret`` () =
    let text = workflowText.Value
    let header = text.Substring(0, text.IndexOf("\njobs:\n"))
    Assert.Matches(@"(?m)^permissions:\s*\n  contents: read\s*$", header)
    let withSecrets = jobs.Value |> Map.filter (fun _ t -> t.Contains "secrets.") |> Map.keys |> List.ofSeq
    Assert.Equal<string list>([ "live" ], withSecrets)
    // The deterministic layers (domain, site, mocked browser, WASM, gates) run without any secret.
    for j in [ "core"; "site"; "browser"; "wasm-smoke"; "lock-gate"; "preservation-gate"; "percepta" ] do
        Assert.DoesNotContain("secrets.", job j)
    // Elevated permissions only where Pages deployment needs them.
    let elevated = jobs.Value |> Map.filter (fun _ t -> Regex.IsMatch(t, @"(?m)^    permissions:")) |> Map.keys |> List.ofSeq
    Assert.Equal<string list>([ "deploy" ], elevated)
    Assert.Matches(@"pages: write", job "deploy")

[<Fact; Trait("obligation", "VPO-2f4beacb25bb0586"); Trait("obligation", "VPO-92aa50bd20b70eeb")>]
let ``AR-010 PF-017 each verification layer is its own job with its own published result`` () =
    // domain (core), UI with fixtures (browser), live provider (live), deployed browser (deployed-smoke)
    Assert.Contains("dotnet test App.slnx", job "core")
    Assert.Contains("name: core-results", job "core")
    Assert.Contains("--grep-invert @live", job "browser")
    Assert.Contains("name: browser-results", job "browser")
    Assert.Contains("--grep @live", job "live")
    Assert.Contains("name: live-results", job "live")
    Assert.Contains("version.json", job "deployed-smoke")
    // Live and deployed checks run at acceptance (main pushes); the deterministic layers on every push.
    Assert.Contains("refs/heads/main", job "live")
    Assert.Contains("refs/heads/main", job "deploy")
    for j in [ "core"; "browser" ] do
        Assert.DoesNotContain("refs/heads/main'", Regex.Match(job j, @"(?m)^    if:.*$").Value)

[<Fact; Trait("obligation", "VPO-3c80e06a4abb77cf")>]
let ``PF-017 the offline layers do not depend on the live provider and an offline pass never waives a failing live gate`` () =
    for j in [ "core"; "browser"; "site" ] do
        Assert.DoesNotContain("live", needsOf j)
        Assert.DoesNotContain("CAUSA_GITHUB_TOKEN", job j)
    // The required check fails when live fails on main, whatever the offline layers did.
    Assert.Contains("live", needsOf "build-and-test")
    Assert.Contains(@"if [ ""$IS_MAIN"" = true ]; then [ ""$LIVE"" = success ] || exit 1; fi", job "build-and-test")

[<Fact; Trait("obligation", "VPO-a71f7df7b092e654")>]
let ``AR-022 CI runs on every push to main and to every stream branch and on every pull request`` () =
    let text = workflowText.Value
    Assert.Matches(@"(?m)^on:\s*\n  pull_request:", text)
    Assert.Matches(@"(?m)^    branches: \[main, ""r5/\*\*""\]", text)

[<Fact; Trait("obligation", "VPO-98deffeeff197c5d"); Trait("obligation", "VPO-7f06980ae32e712b"); Trait("obligation", "VPO-eac3d689399c924e")>]
let ``AR-013 AR-022 PF-019 batching and reuse never bypass a mandatory check`` () =
    let bt = job "build-and-test"
    for j in [ "core"; "lock-gate"; "site"; "browser"; "live"; "wasm-smoke" ] do
        Assert.Contains(j, needsOf "build-and-test")
    Assert.Contains(@"for r in ""$REUSE"" ""$CORE"" ""$LOCK"" ""$SITE"" ""$BROWSER"" ""$WASM""; do [ ""$r"" = success ] || exit 1; done", bt)
    // Main and manual runs never reuse; the live suite has no reuse path at all.
    Assert.Contains(@"if [ ""$IS_MAIN"" = true ] || [ ""${{ github.event_name }}"" = workflow_dispatch ]; then d=run; fi", job "reuse")
    Assert.DoesNotContain("needs.reuse.outputs.live", workflowText.Value)
    let config = Json.load (Path.Combine(repoRoot, "acceptance/reuse.json"))
    for t in items "Tests" config do
        if str "TestId" t = Some "live" then Assert.Equal(Some "live", str "Freshness" t)
    // A skipped test is never a pass: both test jobs fail a run that skipped anything.
    Assert.Contains("A run that skips tests is not a pass", job "core")
    Assert.Contains("A run that skips tests is not a pass", job "browser")

[<Fact; Trait("obligation", "VPO-c83b17e570429db6")>]
let ``AR-013 reuse is selected by changed inputs; the workflow, lock files and solution force every producer to run`` () =
    let config = Json.load (Path.Combine(repoRoot, "acceptance/reuse.json"))
    let globals = strings "GlobalInputs" config
    for g in [ ".github/workflows/build-and-test.yml"; "acceptance/reuse.json"; "App.slnx"; "package.json"; "Directory.Build.props" ] do
        Assert.Contains(g, globals)
    let ids = items "Tests" config |> List.map (strOr "" "TestId")
    Assert.Equal<string list>([ "core"; "lock-gate"; "site"; "browser"; "live" ] |> List.sort, ids |> List.sort)
    // live is declared with live freshness, which Dokimos never reuses and the decision never honours.
    Assert.Equal(Some "live", items "Tests" config |> List.find (fun t -> str "TestId" t = Some "live") |> str "Freshness")
    // The decision: only Dokimos "reuse" on a non-live producer is reused; anything else runs.
    let plan decision freshness =
        parse (sprintf """{"Tests":[{"TestId":"core","Decision":"%s","Freshness":"%s"}]}""" decision freshness)
    Assert.Equal("reuse", Reuse.decision (plan "reuse" "content-addressed") "core")
    Assert.Equal("run", Reuse.decision (plan "run" "content-addressed") "core")
    Assert.Equal("run", Reuse.decision (plan "reuse" "live") "core")
    Assert.Equal("run", Reuse.decision (plan "reuse" "content-addressed") "browser")
    // The request carries the changed paths to Dokimos and the declared inputs of every producer.
    let request = Reuse.request config "abc" (Some [ "src/engine/Kernel.fs" ]) []
    Assert.Equal<string list>([ "src/engine/Kernel.fs" ], strings "ChangedPaths" request)
    Assert.Equal(ids.Length, (items "Tests" request).Length)

[<Fact; Trait("obligation", "VPO-9827ff5a95a7c74f")>]
let ``AR-018 missing live credentials fail the live job and the deployment is not claimed without proof of the served SHA`` () =
    let live = job "live"
    Assert.Contains(@"if [ -z ""$CAUSA_GITHUB_TOKEN"" ] || [ -z ""$CAUSA_TEST_REPO"" ]; then", live)
    Assert.Contains("are not available to this workflow\"; exit 1", live)
    let smoke = job "deployed-smoke"
    Assert.Contains(@"[ ""$served"" = ""$GITHUB_SHA"" ] || {", smoke)
    // The ledger names a deployed SHA only from deployed-smoke's verified output.
    Assert.Contains("DEPLOYED_SHA: ${{ needs.deployed-smoke.outputs.sha }}", job "acceptance")
