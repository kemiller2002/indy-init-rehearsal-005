/// The acceptance job's inputs for this run, and one real run of tools/acceptance/ci/acceptance.sh over them into a
/// scratch directory. Every test reads that output or a mutated copy of it. A missing input fails the test that
/// needs it (it is never skipped).
module Causa.Acceptance.RunTests.Run

open System
open System.Diagnostics
open System.IO
open System.Text.RegularExpressions
open Causa.Acceptance.Json

let env name =
    match Environment.GetEnvironmentVariable name with
    | null | "" -> failwithf "%s is not set: these tests run in the acceptance job with its inputs" name
    | v -> v

let envOr name fallback =
    match Environment.GetEnvironmentVariable name with
    | null | "" -> fallback
    | v -> v

let repoRoot =
    let rec up (d: DirectoryInfo) =
        if isNull d then failwith "repository root (App.slnx) not found"
        elif File.Exists(Path.Combine(d.FullName, "App.slnx")) then d.FullName
        else up d.Parent
    up (DirectoryInfo AppContext.BaseDirectory)

/// A path input: relative values are relative to the repository root (the job's working directory), not to the
/// test host's directory.
let envPath name = Path.GetFullPath(Path.Combine(repoRoot, env name))

let indyInit () = envPath "INDY_INIT"
let compiler () = envOr "COMPILER" (Path.Combine(indyInit (), "tools/requirements-compiler/src/bin/Release/net10.0/requirements-compiler.dll"))
let results () = Path.Combine(repoRoot, envOr "RESULTS" "TestResults")

type Exec = { Code: int; Out: string; Err: string }

let exec (file: string) (args: string list) (cwd: string) (extraEnv: (string * string) list) =
    let psi = ProcessStartInfo(file, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, WorkingDirectory = cwd)
    for a in args do psi.ArgumentList.Add a
    for k, v in extraEnv do psi.Environment[k] <- v
    use p = Process.Start psi
    let out = p.StandardOutput.ReadToEndAsync()
    let err = p.StandardError.ReadToEndAsync()
    p.WaitForExit()
    { Code = p.ExitCode; Out = out.Result; Err = err.Result }

/// The pins of tools/acceptance/ci/acceptance.sh, read from the script so the tests use exactly its values.
let pin name =
    let text = File.ReadAllText(Path.Combine(repoRoot, "tools/acceptance/ci/acceptance.sh"))
    let m = Regex.Match(text, sprintf @"(?m)^%s=""?(?:\$\{%s:-)?([^""}\s]+)" name name)
    if m.Success then m.Groups[1].Value else failwithf "pin %s not found in acceptance.sh" name

let baselinePath () = Path.Combine(indyInit (), "requirements/causa-requirements-baseline.json")
let planPath () = Path.Combine(indyInit (), pin "PLAN_PATH")

let scratch prefix =
    let d = Path.Combine(Path.GetTempPath(), prefix + "-" + Guid.NewGuid().ToString("N"))
    Directory.CreateDirectory d |> ignore
    d

/// One real acceptance.sh run over this run's collected results (the same inputs the job's final run uses).
type Baseline = { Out: string; ManifestSha: string; SourceSha: string; Script: Exec }

let baseline =
    lazy
        (let out = scratch "acc-run"
         let sourceSha = env "SOURCE_SHA"
         let script =
             exec
                 "sh"
                 [ "tools/acceptance/ci/acceptance.sh" ]
                 repoRoot
                 [ "RUN_ID", env "RUN_ID"
                   "SOURCE_SHA", sourceSha
                   "DEPLOYED_SHA", envOr "DEPLOYED_SHA" ""
                   "PRODUCERS", envPath "PRODUCERS"
                   "OUT", out
                   "INDY_INIT", indyInit ()
                   "COMPILER", compiler ()
                   "CAUSA_DATA_DIR", envOr "CAUSA_DATA_DIR" ""
                   "RESULTS", results () ]
         if script.Code <> 0 then failwithf "acceptance.sh exited %d: %s" script.Code script.Err
         { Out = out
           ManifestSha = File.ReadAllText(Path.Combine(out, "evidence-manifest.sha256")).Trim()
           SourceSha = sourceSha
           Script = script })

/// Copies the baseline run's bundle so a test can mutate it.
let copyBundle () =
    let b = baseline.Value
    let dst = scratch "acc-mut"
    for f in Directory.GetFiles(b.Out, "*", SearchOption.AllDirectories) do
        let rel = Path.GetRelativePath(b.Out, f)
        let target = Path.Combine(dst, rel)
        Directory.CreateDirectory(Path.GetDirectoryName target) |> ignore
        File.Copy(f, target)
    dst

type Verdict = { Code: int; Json: Text.Json.Nodes.JsonNode; Raw: Exec }

/// compute-acceptance-verdict.py exactly as acceptance.sh calls it, over a bundle directory (overridable inputs).
let ledgerVerifier (dir: string) (overrides: Map<string, string>) (extra: string list) =
    let o key fallback = overrides.TryFind key |> Option.defaultValue fallback
    let manifest = Path.Combine(dir, "bundle/evidence-manifest.json")
    let r =
        exec
            "python3"
            ([ Path.Combine(indyInit (), "tools/compute-acceptance-verdict.py")
               o "ledger" (Path.Combine(dir, "bundle/ledger.json"))
               "--repo"; repoRoot
               "--baseline"; o "baseline" (baselinePath ())
               "--baseline-sha256"; o "baseline-sha256" (pin "BASELINE_SHA256")
               "--baseline-source"; indyInit ()
               "--approvals"; Path.Combine(dir, "inputs/approvals.causa.json")
               "--approvals-sha256"; pin "APPROVALS_SHA256"
               "--evidence-manifest"; manifest
               "--evidence-manifest-sha256"; o "manifest-sha256" (sha256File manifest)
               "--evidence-root"; Path.Combine(dir, "bundle")
               "--trusted-source-sha"; o "source-sha" baseline.Value.SourceSha
               "--trusted-operator-id"; pin "OPERATOR" ]
             @ extra)
            repoRoot
            []
    let json = try parse r.Out with _ -> jobj [ "verdict", jstr "UNREADABLE" ]
    { Code = r.Code; Json = json; Raw = r }

/// requirements-compiler verify-plan exactly as acceptance.sh calls it.
let planVerifier (dir: string) (overrides: Map<string, string>) =
    let o key fallback = overrides.TryFind key |> Option.defaultValue fallback
    let manifest = Path.Combine(dir, "bundle/evidence-manifest.json")
    let outFile = Path.Combine(dir, sprintf "assessment-%s.json" (Guid.NewGuid().ToString("N")))
    let r =
        exec
            "dotnet"
            [ compiler (); "verify-plan"
              "--plan"; planPath ()
              "--baseline"; o "baseline" (baselinePath ())
              "--ledger"; o "ledger" (Path.Combine(dir, "bundle/ledger.json"))
              "--evidence-manifest"; manifest
              "--evidence-manifest-sha256"; o "manifest-sha256" (sha256File manifest)
              "--evidence-root"; Path.Combine(dir, "bundle")
              "--trusted-source-sha"; o "source-sha" baseline.Value.SourceSha
              "--approvals"; Path.Combine(dir, "inputs/approvals.causa.json")
              "--trusted-operator-id"; pin "OPERATOR"
              "--out"; outFile ]
            repoRoot
            []
    let json = try load outFile with _ -> jobj [ "verdict", jstr "UNREADABLE" ]
    { Code = r.Code; Json = json; Raw = r }

let gs (n: Text.Json.Nodes.JsonNode) = n.GetValue<string>()
let gi (n: Text.Json.Nodes.JsonNode) = n.GetValue<int>()

let verdictOf (v: Verdict) = strOr "" "verdict" v.Json

let writeLedger (dir: string) (ledger: Text.Json.Nodes.JsonNode) =
    write (Path.Combine(dir, "bundle/ledger.json")) ledger |> ignore

let loadLedger (dir: string) = load (Path.Combine(dir, "bundle/ledger.json"))

let setAll (arr: Text.Json.Nodes.JsonNode) (f: Text.Json.Nodes.JsonObject -> unit) =
    for n in arr.AsArray() do
        f (n.AsObject())
