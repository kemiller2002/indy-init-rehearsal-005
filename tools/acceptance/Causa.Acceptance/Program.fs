module Causa.Acceptance.Program

open System
open System.IO
open Causa.Acceptance.Json

let usage =
    """causa-acceptance <command> [options]

  lock-gate   --root DIR --stack CHANNEL.json --pin PIN.json --out FILE [--require-host-tool EXE]... [--context k=v]...
              T11: lock, declared, restored and installed versions against echelon-current. Exit 1 on any mismatch.
  bind        --root DIR --plan PLAN.json --out FILE [--results DIR]... [--sources DIR]... [--assemblies DIR]... [--context k=v]...
              Discover TRX / Playwright JSON results tagged with VPO ids and bind every plan obligation.
  ledger      --root DIR --plan PLAN.json --out-dir DIR --run-id ID --source-sha SHA [--deployed-sha SHA]
              --gates FILE --defects FILE [--producers FILE] [--results DIR]... [--sources DIR]... [--assemblies DIR]...
              [--contract-version S] [--frozen-fixture-digest S] [--interface-manifest-sha S] [--command S]
              Evidence bundle + causa.acceptance-ledger/2. Prints the evidence manifest SHA-256.
  reuse-request --config acceptance/reuse.json --revision SHA --out FILE [--changed FILE] [--prior FILE]...
  reuse-decision --plan PLAN.json --test ID          Prints run or reuse.
  provenance  --plan PLAN.json --test ID --revision SHA --run-id ID --outcome passed|failed --out FILE
  curation    --traceability FILE --out FILE         testBindings for Indy-init's verification-plan curation.
  produce     --root DIR --plan PLAN.json --gates FILE --defects FILE --out FILE --records-dir DIR --run-id ID --source-sha SHA
              [--producers FILE] [--results DIR]... [--sources DIR]... [--assemblies DIR]... [--record-prefix P]
              The computed gate producers of acceptance/gates.json `producers`, appended to the workflow-reported ones.
  apply-curation --curation CURATION.json --plan PLAN.json --bindings TESTBINDINGS.json --out FILE
              Replaces `testBindings` in a COPY of Indy-init's curation/verification-plan.json with a run's
              curation-test-bindings.json, after checking every binding against the current plan. Never edits in place.
  report      --verdict FILE --traceability FILE --out FILE [--assessment FILE] [--lock-gate FILE] [--context k=v]...
"""

/// Options: every `--name value` pair, in order (repeatable).
let parseOptions (args: string list) =
    let rec go acc =
        function
        | (name: string) :: value :: rest when name.StartsWith "--" -> go ((name.Substring 2, value) :: acc) rest
        | [] -> Ok(List.rev acc)
        | other :: _ -> Error(sprintf "unexpected argument: %s" other)
    go [] args

let one (opts: (string * string) list) name = opts |> List.tryFind (fst >> (=) name) |> Option.map snd
let many (opts: (string * string) list) name = opts |> List.filter (fst >> (=) name) |> List.map snd

let required opts name =
    match one opts name with
    | Some v -> v
    | None -> failwithf "--%s is required" name

let contextOf opts =
    many opts "context"
    |> List.map (fun kv ->
        let i = kv.IndexOf '='
        if i > 0 then kv.Substring(0, i), kv.Substring(i + 1) else kv, "")

let full (root: string) (p: string) = if Path.IsPathRooted p then p else Path.Combine(root, p)

let traceOf opts root =
    let plan = Plan.load (full root (required opts "plan"))
    let results = Discovery.discover root (many opts "results" |> List.map (full root)) (many opts "assemblies" |> List.map (full root))
    let declared = Traceability.scanSources root (many opts "sources" |> List.map (full root))
    plan, Traceability.build plan results declared

let gitTracks (root: string) (path: string) =
    try
        let psi = Diagnostics.ProcessStartInfo("git", sprintf "-C \"%s\" ls-files --error-unmatch -- %s" root path, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false)
        use p = Diagnostics.Process.Start psi
        p.StandardOutput.ReadToEnd() |> ignore
        p.StandardError.ReadToEnd() |> ignore
        p.WaitForExit()
        p.ExitCode = 0
    with _ -> false

let lockGate opts =
    let root = one opts "root" |> Option.defaultValue "."
    let stackPath = full root (required opts "stack")
    let inputs: LockGate.Inputs =
        { Root = root
          Channel = Json.load stackPath
          ChannelSha256 = sha256File stackPath
          Pin = Json.load (full root (required opts "pin"))
          HostVersions = LockGate.hostTools |> List.map (fun (exe, _) -> exe, LockGate.runVersion exe) |> Map.ofList
          RequiredHostTools = many opts "require-host-tool"
          PackageLockCommitted = gitTracks root "package-lock.json" }
    let checks = LockGate.evaluate inputs
    let ctx = contextOf opts |> List.map (fun (k, v) -> k, jstr v)
    Json.write (required opts "out") (LockGate.toJson checks ctx) |> ignore
    let verdict = LockGate.verdict checks
    for c in checks do
        if c.Status <> LockGate.Pass then
            eprintfn "%s %s: expected %s, actual %s (%s)" (LockGate.statusName c.Status) c.Subject c.Expected c.Actual c.Detail
    printfn "stack lock gate: %s (%d checks)" verdict checks.Length
    if verdict = "FAIL" then 1 else 0

let bind opts =
    let root = one opts "root" |> Option.defaultValue "."
    let _, trace = traceOf opts root
    let ctx = contextOf opts |> List.map (fun (k, v) -> k, jstr v)
    Json.write (required opts "out") (Traceability.toJson trace ctx) |> ignore
    let c pred = Traceability.count pred trace
    printfn
        "obligations %d: bound %d, declared %d, missing %d; PASS %d, FAIL %d; unknown tags %d"
        trace.Obligations.Length
        (c (fun o -> Traceability.bindingName o.Binding = "bound"))
        (c (fun o -> Traceability.bindingName o.Binding = "declared"))
        (c (fun o -> o.Binding = Traceability.Missing))
        (c (fun o -> o.Verification = Traceability.Pass))
        (c (fun o -> o.Verification = Traceability.Fail))
        trace.UnknownTags.Length
    for id, by in trace.UnknownTags do
        eprintfn "unknown obligation id %s tagged by %s" id by
    if trace.UnknownTags.IsEmpty then 0 else 1

let dependencyLockSha root =
    [ ".conditor/lock.json"; "package-lock.json"; "packages.lock.json" ]
    |> List.map (full root)
    |> List.filter File.Exists
    |> List.map (fun p -> sprintf "%s %s" (sha256File p) (Path.GetRelativePath(root, p).Replace('\\', '/')))
    |> String.concat "\n"
    |> Text.Encoding.UTF8.GetBytes
    |> sha256Hex

let ledger opts =
    let root = one opts "root" |> Option.defaultValue "."
    let plan, trace = traceOf opts root
    let outDir = required opts "out-dir"
    let producers = one opts "producers" |> Option.map (full root >> Json.load >> Ledger.loadProducers) |> Option.defaultValue []
    let inputs: Ledger.Inputs =
        { Plan = plan
          Trace = trace
          RunId = required opts "run-id"
          SourceSha = required opts "source-sha"
          DeployedSha = one opts "deployed-sha" |> Option.filter (String.IsNullOrWhiteSpace >> not)
          Producers = producers
          Gates = Json.load (full root (required opts "gates")) |> Ledger.loadGates
          DefectClasses = Json.load (full root (required opts "defects")) |> Ledger.loadDefectClasses
          ContractVersion = one opts "contract-version" |> Option.defaultValue (sprintf "causa.acceptance-ledger/2; plan %s" plan.PlanId)
          FrozenFixtureDigest = one opts "frozen-fixture-digest" |> Option.defaultValue "unavailable: no frozen fixture set in this candidate"
          DependencyLockSha = dependencyLockSha root
          InterfaceManifestSha = one opts "interface-manifest-sha" |> Option.defaultValue "unavailable: no interface manifest in this candidate"
          Command = one opts "command" |> Option.defaultValue "causa-acceptance ledger" }
    let ctx =
        [ "runId", jstr inputs.RunId; "sourceSha", jstr inputs.SourceSha ] @ (contextOf opts |> List.map (fun (k, v) -> k, jstr v))
    Json.write (Path.Combine(outDir, "traceability.json")) (Traceability.toJson trace ctx) |> ignore
    let built = Ledger.build inputs
    let sha = Ledger.finalize inputs outDir built
    printfn "%s" sha
    0

let reuseRequest opts =
    let changed = one opts "changed" |> Option.map (fun p -> File.ReadAllLines p |> Array.filter (String.IsNullOrWhiteSpace >> not) |> List.ofArray)
    let prior =
        many opts "prior"
        |> List.filter File.Exists
        |> List.collect (fun p ->
            match Json.tryLoad p with
            | Some(:? Text.Json.Nodes.JsonArray as a) -> a |> Seq.filter (isNull >> not) |> List.ofSeq
            | Some n when (str "SchemaVersion" n).IsSome -> [ n ]
            | _ -> [])
    Json.write (required opts "out") (Reuse.request (Json.load (required opts "config")) (required opts "revision") changed prior) |> ignore
    0

let reuseDecision opts =
    let plan = one opts "plan" |> Option.bind Json.tryLoad
    printfn "%s" (match plan with Some p -> Reuse.decision p (required opts "test") | None -> "run")
    0

let provenance opts =
    match Json.tryLoad (required opts "plan") with
    | None ->
        eprintfn "no reuse plan; no provenance recorded"
        0
    | Some plan ->
        match Reuse.provenance plan (required opts "test") (required opts "revision") (required opts "run-id") (required opts "outcome") with
        | None ->
            eprintfn "test %s is not in the reuse plan" (required opts "test")
            0
        | Some p ->
            Json.write (required opts "out") (jarr [ p ]) |> ignore
            0

/// testBindings for Indy-init requirements-compiled/v1/curation/verification-plan.json, from a traceability file:
/// the exact test identities bound to each baseline obligation (kind live for the live suite, else deterministic).
/// The plan verifier reports PASS only for obligations the recompiled plan binds; this is the input for that
/// curation, which the requirements owner applies and recompiles (new plan id). It never binds a missing obligation.
let curation opts =
    let trace = Json.load (required opts "traceability")
    let bindings =
        items "obligations" trace
        |> List.choose (fun o ->
            let tests =
                items "tests" o
                |> List.map (fun t ->
                    let kind = if (strOr "" "resultFile" t).Contains "/live/" then "live" else "deterministic"
                    strOr "" "testId" t, kind)
                |> List.distinct
                |> List.sort
            if tests.IsEmpty then None
            else
                Some(
                    jobj
                        [ "obligation", jstr (strOr "" "sourceObligation" o)
                          "tests", jarr (tests |> List.map (fun (id, k) -> jobj [ "id", jstr id; "kind", jstr k ])) ]
                ))
    Json.write (required opts "out") (jobj [ "testBindings", jarr bindings ]) |> ignore
    printfn "%d obligations with bound tests" bindings.Length
    0

let gitIsAncestor (root: string) (sha: string) =
    try
        let psi = Diagnostics.ProcessStartInfo("git", sprintf "-C \"%s\" merge-base --is-ancestor %s HEAD" root sha, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false)
        use p = Diagnostics.Process.Start psi
        p.StandardOutput.ReadToEnd() |> ignore
        p.StandardError.ReadToEnd() |> ignore
        p.WaitForExit()
        p.ExitCode = 0
    with _ -> false

let readCommitted (root: string) (rel: string) =
    let p = full root rel
    if not (File.Exists p) then None
    else
        let sha = sha256File p
        let node = try Some(Json.load p) with _ -> None
        Some(sha, node |> Option.filter (isNull >> not))

let produce opts =
    let root = one opts "root" |> Option.defaultValue "."
    let _, trace = traceOf opts root
    let results = Discovery.discover root (many opts "results" |> List.map (full root)) (many opts "assemblies" |> List.map (full root))
    let gates = Json.load (full root (required opts "gates"))
    let specs = Producers.loadSpecs gates
    let env: Producers.Environment =
        { Trace = trace
          Results = results
          ReadFile = readCommitted root
          IsAncestor = gitIsAncestor root
          DefectClasses = Json.load (full root (required opts "defects")) |> Ledger.loadDefectClasses }
    let baseProducers =
        one opts "producers" |> Option.map (fun p -> items "producers" (Json.load (full root p))) |> Option.defaultValue []
    let reported = baseProducers |> List.map (strOr "" "id") |> Set.ofList
    for s in specs do
        if reported.Contains(Producers.specId s) then failwithf "producer %s is both reported and computed" (Producers.specId s)
    let recordsDir = required opts "records-dir"
    let prefix = one opts "record-prefix" |> Option.defaultValue ""
    let ctx = [ "runId", jstr (required opts "run-id"); "sourceSha", jstr (required opts "source-sha") ]
    let computed =
        specs
        |> List.map (fun spec ->
            let checks = Producers.checksOf env spec
            let id = Producers.specId spec
            Json.write (Path.Combine(recordsDir, id + ".json")) (Producers.recordJson spec checks ctx) |> ignore
            let st = Producers.status checks
            printfn "%s: %s" id (Producers.statusName st)
            for c in checks do
                if c.Status <> Producers.CheckPass then printfn "  %s %s: %s" (Producers.checkStatusName c.Status) c.Name c.Detail
            jobj
                [ "id", jstr id
                  "status", jstr (Producers.statusName st)
                  "command", jstr (sprintf "causa-acceptance produce (acceptance/gates.json producers.%s)" id)
                  "record", jstr (prefix + id + ".json") ])
    Json.write (required opts "out") (jobj [ "producers", jarr (baseProducers @ computed) ]) |> ignore
    0

/// Applies a run's curation-test-bindings.json to a copy of Indy-init's curation/verification-plan.json. Every
/// binding must name a baseline obligation of the current plan, at least one test, and a known kind; any problem
/// refuses the whole application (nothing is written).
let applyCuration opts =
    let curation = Json.load (required opts "curation")
    let plan = Plan.load (required opts "plan")
    let bindings = items "testBindings" (Json.load (required opts "bindings"))
    let known = Plan.obligations plan |> List.map (fun o -> o.SourceObligation) |> Set.ofList
    let kinds = set [ "live"; "deterministic"; "review" ]
    let problems =
        [ for b in bindings do
              let o = strOr "" "obligation" b
              let tests = items "tests" b
              if not (known.Contains o) then yield sprintf "binding names %s, which is not an obligation of plan %s" o plan.PlanId
              if tests.IsEmpty then yield sprintf "binding for %s names no test" o
              for t in tests do
                  if String.IsNullOrWhiteSpace(strOr "" "id" t) then yield sprintf "binding for %s has an empty test id" o
                  if not (kinds.Contains(strOr "" "kind" t)) then yield sprintf "binding for %s has unknown kind %s" o (strOr "" "kind" t)
          for o, n in bindings |> List.countBy (strOr "" "obligation") do
              if n > 1 then yield sprintf "obligation %s is bound more than once" o ]
    if not problems.IsEmpty then
        for p in problems do eprintfn "%s" p
        1
    else
        let sorted = bindings |> List.sortBy (strOr "" "obligation")
        let out = curation.DeepClone()
        out["testBindings"] <- jarr sorted
        Json.write (required opts "out") out |> ignore
        let bound = sorted |> List.map (strOr "" "obligation") |> Set.ofList
        printfn "testBindings: %d of %d obligations of plan %s bound (%d unbound stay missing)" bound.Count known.Count plan.PlanId (known.Count - bound.Count)
        0

let report opts =
    let verdict = Json.load (required opts "verdict")
    let assessment = one opts "assessment" |> Option.bind Json.tryLoad
    let trace = Json.load (required opts "traceability")
    let gate = one opts "lock-gate" |> Option.bind Json.tryLoad
    let text = Report.render verdict assessment trace gate (contextOf opts)
    let out = required opts "out"
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath out)) |> ignore
    File.WriteAllText(out, text)
    0

[<EntryPoint>]
let main argv =
    match List.ofArray argv with
    | [] | [ "--help" ] | [ "-h" ] ->
        printf "%s" usage
        2
    | command :: rest ->
        match parseOptions rest with
        | Error e ->
            eprintfn "%s\n%s" e usage
            2
        | Ok opts ->
            try
                match command with
                | "lock-gate" -> lockGate opts
                | "bind" -> bind opts
                | "ledger" -> ledger opts
                | "reuse-request" -> reuseRequest opts
                | "reuse-decision" -> reuseDecision opts
                | "provenance" -> provenance opts
                | "report" -> report opts
                | "curation" -> curation opts
                | "produce" -> produce opts
                | "apply-curation" -> applyCuration opts
                | other ->
                    eprintfn "unknown command %s\n%s" other usage
                    2
            with ex ->
                eprintfn "causa-acceptance %s: %s" command ex.Message
                2
