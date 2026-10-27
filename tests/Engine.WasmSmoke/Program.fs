/// The kernel in .NET WebAssembly. Arguments come in groups of four: directory id, manifest.json text,
/// events.ndjson text, snapshot.json text (all UTF-8, passed from node). Prints one line per case and per fault;
/// the exit code is non-zero if anything failed.
open System
open System.Text
open IndyInitRootCauseInvestigator.Engine

[<EntryPoint>]
let main argv =
    let mutable failures = 0
    let schemaOk = Storage.embeddedSchemaIdentity () = Some Storage.schemaIdentity
    printfn "SCHEMA %s" (if schemaOk then "OK" else "MISMATCH")
    if not schemaOk then failures <- failures + 1
    for group in argv |> Array.chunkBySize 4 do
        match group with
        | [| dir; manifest; events; snapshot |] ->
            let files =
                Map.ofList
                    [ Storage.manifestFile, Encoding.UTF8.GetBytes manifest
                      Storage.eventsFile, Encoding.UTF8.GetBytes events
                      Storage.snapshotFile, Encoding.UTF8.GetBytes snapshot ]
            let clock = Diagnostics.Stopwatch.StartNew()
            match Storage.read dir files with
            | Ok loaded -> printfn "CASE %s OK %d %s %d" dir loaded.State.Version loaded.StateSha256 clock.ElapsedMilliseconds
            | Error e ->
                failures <- failures + 1
                printfn "CASE %s REFUSED %s %s" dir e.Code e.Detail
        | _ ->
            failures <- failures + 1
            printfn "ARGS malformed"
    // Aegis faults at the boundary (DEC-026) must also work in the browser runtime.
    try
        let rejection = Faults.ofRejection "wasm-smoke" (StaleVersion(1L, 2L))
        let storage = Faults.ofStorageError "wasm-smoke" { Code = "CAUSA.PATH_INVALID"; DomainCode = None; Detail = "smoke" }
        let text = sprintf "%A %A" rejection.Code storage.Code
        if text.Contains "VERSION.STALE" && text.Contains "CAUSA.PATH_INVALID" then printfn "FAULT OK %s" text
        else
            failures <- failures + 1
            printfn "FAULT WRONG %s" text
    with ex ->
        failures <- failures + 1
        printfn "FAULT THREW %s" (ex.GetType().FullName)
    printfn "FAILURES %d" failures
    if failures = 0 then 0 else 1
