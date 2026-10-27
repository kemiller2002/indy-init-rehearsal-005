/// WASM safety: the kernel assembly must not reference APIs that fail or misbehave in the browser runtime
/// (R4: Environment.WorkingSet broke the engine in WASM). Static scan of the compiled engine's type and member
/// references; the runtime proof is tests/Engine.WasmSmoke.
module App.Engine.Tests.WasmSafetyTests

open System.IO
open System.Reflection.Metadata
open System.Reflection.PortableExecutable
open Xunit
open IndyInitRootCauseInvestigator.Engine

let private forbiddenTypes =
    set
        [ "System.Diagnostics.Process"
          "System.IO.File"
          "System.IO.Directory"
          "System.IO.FileStream"
          "System.IO.DriveInfo"
          "System.Threading.Thread"
          "System.Net.Http.HttpClient"
          "System.Net.Sockets.Socket" ]

let private forbiddenMembers =
    set [ "System.Environment::get_WorkingSet"; "System.Environment::get_ProcessId"; "System.Environment::get_MachineName"; "System.Environment::Exit" ]

let private references () =
    let path = typeof<Session>.Assembly.Location
    use stream = File.OpenRead path
    use pe = new PEReader(stream)
    let md = pe.GetMetadataReader()
    let typeName (h: TypeReferenceHandle) =
        let t = md.GetTypeReference h
        md.GetString(t.Namespace) + "." + md.GetString(t.Name)
    let types = md.TypeReferences |> Seq.map typeName |> Set.ofSeq
    let members =
        md.MemberReferences
        |> Seq.choose (fun h ->
            let m = md.GetMemberReference h
            if m.Parent.Kind = HandleKind.TypeReference then
                Some(typeName (TypeReferenceHandle.op_Explicit m.Parent) + "::" + md.GetString(m.Name))
            else
                None)
        |> Set.ofSeq
    types, members

[<Fact>]
[<Trait("task", "T13")>]
[<Trait("obligation", "VPO-6886a2a6e612f704")>]
let ``WASM-SAFETY the kernel references no process file thread network or crypto-provider API`` () =
    let types, members = references ()
    Assert.Empty(Set.intersect types forbiddenTypes)
    Assert.Empty(types |> Set.filter (fun t -> t.StartsWith "System.Security.Cryptography."))
    Assert.Empty(Set.intersect members forbiddenMembers)
    // the scan sees real references (it is not vacuous)
    Assert.Contains("System.Text.StringBuilder", types)
