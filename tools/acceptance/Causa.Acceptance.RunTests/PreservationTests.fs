/// Adversarial tests of the requirements preservation gate as this repository runs it: the recorded command
/// (evidence/requirements-preservation/gate-pre-implementation-command.txt), run against a mutated copy of the
/// derived package. Each mutation is one a derivation could silently make; the gate must not pass it.
/// Run in the preservation-adversarial job (Praxis 3.22.0 from the channel; Indy-init at 459f65a and aab8f80).
module Causa.Acceptance.RunTests.PreservationTests

open System
open System.IO
open Xunit
open Causa.Acceptance.Json
open Causa.Acceptance.RunTests.Run

let recordedCommand () =
    let lines = File.ReadAllLines(Path.Combine(repoRoot, "evidence/requirements-preservation/gate-pre-implementation-command.txt"))
    let start = lines |> Array.findIndex (fun l -> l.StartsWith "praxis requirements preservation verify")
    let stop = lines |> Array.findIndexBack (fun l -> l.TrimEnd().EndsWith "--json")
    String.Join("\n", lines[start..stop])

let rec copyDir (src: string) (dst: string) =
    Directory.CreateDirectory dst |> ignore
    for f in Directory.GetFiles src do File.Copy(f, Path.Combine(dst, Path.GetFileName f))
    for d in Directory.GetDirectories src do copyDir d (Path.Combine(dst, Path.GetFileName d))

/// Runs the recorded gate in a scratch directory laid out like the repository, after `mutate` edits the derived
/// requirements there. Returns (exit code, verdict).
let gate (mutate: string -> unit) =
    let dir = scratch "preservation"
    copyDir (Path.Combine(repoRoot, "research/requirements")) (Path.Combine(dir, "research/requirements"))
    copyDir (Path.Combine(repoRoot, "research/requirements-preservation")) (Path.Combine(dir, "research/requirements-preservation"))
    Directory.CreateSymbolicLink(Path.Combine(dir, "<indy-init>"), env "INDY_INIT") |> ignore
    Directory.CreateSymbolicLink(Path.Combine(dir, "<indy-init@aab8f80>"), env "INDY_INIT_SOURCE") |> ignore
    mutate (Path.Combine(dir, "research/requirements"))
    let out = Path.Combine(dir, "gate.json")
    let command =
        (recordedCommand ()).Replace("--output evidence/requirements-preservation/gate-pre-implementation.json", sprintf "--output '%s'" out)
    let r = exec "sh" [ "-c"; command ] dir []
    let verdict = try strOr "none" "verdict" (load out) with _ -> "none"
    r.Code, verdict

let fileOf (derived: string) (obligation: string) =
    Directory.GetFiles(derived, "*.md") |> Array.find (fun f -> File.ReadAllText(f).Contains(sprintf "| %s |" obligation))

let edit (file: string) (from: string) (into: string) =
    let text = File.ReadAllText file
    Assert.Contains(from, text)
    File.WriteAllText(file, text.Replace(from, into))

let rejected (code, verdict) =
    Assert.NotEqual(0, code)
    Assert.NotEqual<string>("pass", verdict)

[<Fact; Trait("job", "preservation")>]
let ``control: the unmodified derived package passes the recorded gate`` () =
    let code, verdict = gate ignore
    Assert.Equal(0, code)
    Assert.Equal("pass", verdict)

[<Fact; Trait("job", "preservation"); Trait("obligation", "VPO-413ff7efb9ab1eaf"); Trait("obligation", "VPO-4b7f3050c9ba72b7"); Trait("obligation", "VPO-97136db88b6c60fb")>]
let ``AR-002 PF-018 WDL-015 omitting a derived requirement (WDL-005) fails the gate`` () =
    rejected (gate (fun d -> File.Delete(fileOf d "WDL-005-O1")))

[<Fact; Trait("job", "preservation"); Trait("obligation", "VPO-413ff7efb9ab1eaf"); Trait("obligation", "VPO-4b7f3050c9ba72b7")>]
let ``AR-002 PF-018 a negated obligation does not pass the gate`` () =
    rejected (
        gate (fun d ->
            edit (fileOf d "ADRP-001-O2") "| ADRP-001-O2 | This order does not waive mandatory requirements. |" "| ADRP-001-O2 | This order does waive mandatory requirements. |")
    )

[<Fact; Trait("job", "preservation"); Trait("obligation", "VPO-413ff7efb9ab1eaf"); Trait("obligation", "VPO-4b7f3050c9ba72b7")>]
let ``AR-002 PF-018 a weakened obligation (MUST to SHOULD) does not pass the gate`` () =
    rejected (
        gate (fun d ->
            let f = fileOf d "VER-001-O1"
            edit f "| VER-001-O1 | Each build MUST use" "| VER-001-O1 | Each build SHOULD use")
    )

[<Fact; Trait("job", "preservation"); Trait("obligation", "VPO-413ff7efb9ab1eaf")>]
let ``AR-002 a value swap (the WCAG ratios of UI-016-O11 exchanged) does not pass the gate`` () =
    rejected (
        gate (fun d ->
            edit (fileOf d "UI-016-O11") "normal text (4.5:1), large text (3:1)" "normal text (3:1), large text (4.5:1)")
    )

[<Fact; Trait("job", "preservation"); Trait("obligation", "VPO-413ff7efb9ab1eaf")>]
let ``AR-002 a role swap (the order of source precedence in ADRP-001-O1 exchanged) does not pass the gate`` () =
    rejected (
        gate (fun d ->
            edit
                (fileOf d "ADRP-001-O1")
                "(1) organizer rules and explicit safety/security invariants; (2) frozen causa-data v1.0.0 contract and locked domain invariants;"
                "(1) frozen causa-data v1.0.0 contract and locked domain invariants; (2) organizer rules and explicit safety/security invariants;")
    )
