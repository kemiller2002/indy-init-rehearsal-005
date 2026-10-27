/// PF-015-O4 / PF-020-O2: the live checks report BLOCKED, never PASS, when the provider secret is not there.
module Persistence.Tests.BlockedTests

open System
open System.IO
open Xunit

let private run (args: string[]) =
    for name in [ "CAUSA_GITHUB_TOKEN"; "CAUSA_TEST_REPO_SECRET"; "CAUSA_TEST_REPO_VAR"; "CAUSA_CATALOG_TOKEN" ] do
        Environment.SetEnvironmentVariable(name, null)
    let out = new StringWriter()
    let previous = Console.Out
    Console.SetOut out
    try
        let code = Causa.Persistence.Tool.Program.main args
        code, out.ToString()
    finally
        Console.SetOut previous

[<Fact>]
[<Trait("obligation", "VPO-9a5982e4a5994629")>]
[<Trait("obligation", "VPO-b58ad46c59853677")>]
let ``VPO-9a5982e4a5994629 VPO-b58ad46c59853677 a missing provider secret is BLOCKED with a failing exit code, never a pass`` () =
    let code, text = run [| "record" |]
    Assert.Equal(2, code)
    Assert.Contains("RESULT: BLOCKED", text)
    Assert.DoesNotContain("PASS", text)
    let code, text = run [| "catalog" |]
    Assert.Equal(2, code)
    Assert.Contains("RESULT: BLOCKED", text)
    Assert.DoesNotContain("PASS", text)
