/// T14: interface freeze. The versioned manifest of boundaries A–E is EXTRACTED from the compiled kernel (its public
/// types, union cases, record fields and module function signatures, by reflection), plus examples produced by running
/// the kernel and the list of contract tests found in this test assembly. Nothing in it is hand-written.
/// The committed file must equal the extraction byte for byte; regenerate with CAUSA_WRITE_MANIFEST=1.
module App.Engine.Tests.InterfaceManifestTests

open System
open System.IO
open System.Reflection
open System.Text
open Microsoft.FSharp.Reflection
open Xunit
open IndyInitRootCauseInvestigator.Engine

let manifestVersion = "1.0.0"
let manifestRelativePath = "docs/architecture/interface-manifest.json"

let private repoRoot =
    lazy
        (let rec up (dir: DirectoryInfo | null) =
            match dir with
            | null -> failwith "repository root (App.slnx) not found"
            | d -> if File.Exists(Path.Combine(d.FullName, "App.slnx")) then d.FullName else up d.Parent
         up (DirectoryInfo AppContext.BaseDirectory))

// ---- type rendering ----

let rec private typeName (t: Type) : string =
    if t.IsGenericParameter then "'" + t.Name
    elif t.IsArray then typeName (t.GetElementType() |> nonNull) + "[]"
    elif FSharpType.IsTuple t then FSharpType.GetTupleElements t |> Array.map typeName |> String.concat " * " |> sprintf "(%s)"
    elif FSharpType.IsFunction t then
        let d, r = FSharpType.GetFunctionElements t
        $"({typeName d} -> {typeName r})"
    elif t.IsGenericType then
        let def = t.GetGenericTypeDefinition()
        let args = t.GetGenericArguments() |> Array.map typeName |> String.concat ", "
        let baseName =
            match def.FullName with
            | "Microsoft.FSharp.Collections.FSharpList`1" -> "list"
            | "Microsoft.FSharp.Core.FSharpOption`1" -> "option"
            | "Microsoft.FSharp.Collections.FSharpMap`2" -> "Map"
            | "Microsoft.FSharp.Collections.FSharpSet`1" -> "Set"
            | "Microsoft.FSharp.Core.FSharpResult`2" -> "Result"
            | "System.Lazy`1" -> "Lazy"
            | _ -> def.Name.Split('`')[0]
        $"{baseName}<{args}>"
    else
        match t.FullName with
        | "System.String" -> "string"
        | "System.Int64" -> "int64"
        | "System.Int32" -> "int"
        | "System.Boolean" -> "bool"
        | "System.Byte" -> "byte"
        | "Microsoft.FSharp.Core.Unit" -> "unit"
        | _ -> (t.FullName |> Option.ofObj |> Option.defaultValue t.Name).Replace("IndyInitRootCauseInvestigator.Engine.", "").Replace("+", ".")

let private shortName (t: Type) = (t.FullName |> nonNull).Replace("IndyInitRootCauseInvestigator.Engine.", "").Replace("+", ".")

let private describeType (t: Type) : Json =
    if FSharpType.IsUnion(t, BindingFlags.Public) then
        JObject
            [ "name", JString(shortName t)
              "kind", JString "union"
              "cases",
              JArray(
                  FSharpType.GetUnionCases(t, BindingFlags.Public)
                  |> Array.map (fun c ->
                      JObject
                          [ "name", JString c.Name
                            "fields", JArray(c.GetFields() |> Array.map (fun f -> JString $"{f.Name}: {typeName f.PropertyType}") |> List.ofArray) ])
                  |> List.ofArray
              ) ]
    elif FSharpType.IsRecord(t, BindingFlags.Public) then
        JObject
            [ "name", JString(shortName t)
              "kind", JString "record"
              "fields", JArray(FSharpType.GetRecordFields(t, BindingFlags.Public) |> Array.map (fun f -> JString $"{f.Name}: {typeName f.PropertyType}") |> List.ofArray) ]
    elif FSharpType.IsModule t then
        let members =
            t.GetMethods(BindingFlags.Public ||| BindingFlags.Static ||| BindingFlags.DeclaredOnly)
            |> Array.filter (fun m -> not m.IsSpecialName)
            |> Array.map (fun m ->
                let ps = m.GetParameters() |> Array.map (fun p -> typeName p.ParameterType) |> List.ofArray
                let args = if ps.IsEmpty then [ "unit" ] else ps
                JString($"{m.Name}: " + String.Join(" -> ", args @ [ typeName m.ReturnType ])))
        let values =
            t.GetProperties(BindingFlags.Public ||| BindingFlags.Static ||| BindingFlags.DeclaredOnly)
            |> Array.map (fun p -> JString $"{p.Name}: {typeName p.PropertyType}")
        JObject
            [ "name", JString(shortName t)
              "kind", JString "module"
              "members", JArray(Array.append values members |> Array.sortBy (fun j -> match j with JString s -> s | _ -> "") |> List.ofArray) ]
    else
        JObject [ "name", JString(shortName t); "kind", JString "type" ]

/// Which boundary each public type belongs to (the only hand-written input: the assignment, not the content).
let private boundaryOf (name: string) =
    let a = set [ "AiLoop"; "ProviderKey"; "AiProvider"; "AiProfile"; "AiHttpEffect"; "AiHttpOutcome"; "AiUsage"; "AiResult"; "AiPending"; "AiLoopState"; "AiLoopFault"; "Proposals"; "ProposalDraft"; "AgentIdentity"; "AgentOutputError"; "Kernel"; "Decide"; "Faults"; "Command"; "CommandEnvelope"; "Session"; "Accepted"; "Rejection"; "DomainError"; "Actor"; "ActorKind"; "EvidenceInput"; "HypothesisCommand"; "UnknownCommand"; "ContradictionCommand"; "AssumptionCommand"; "ActionCommand"; "CorrectionCommand"; "Transitions"; "Names" ]
    let b = set [ "Timeline"; "TimelineEntry"; "EventCategory"; "SemanticChange"; "HistoricalView"; "EntityRef"; "Projection"; "InvestigationProjection"; "Capability"; "CapabilityAction"; "RecordKind"; "Obligation"; "DomainVerification"; "DataIntegrity"; "Policy"; "Investigation" ]
    let c = set [ "JsonModule"; "Storage"; "StorageError"; "LoadedInvestigation"; "FileSet"; "History"; "Evolve"; "Wire"; "Json"; "Schema"; "Hashing"; "EventBody"; "DomainEvent"; "LifecycleCommand" ]
    let e = set [ "ReportProjection"; "ReportCause"; "ReportCorrection" ]
    if name = "Sync" || name.StartsWith("Sync.", StringComparison.Ordinal) then "D"
    elif a.Contains name then "A"
    elif b.Contains name then "B"
    elif c.Contains name then "C"
    elif e.Contains name then "E"
    else "shared"

let private engineTypes () =
    typeof<Session>.Assembly.GetExportedTypes()
    |> Array.filter (fun t -> not (t.Name.StartsWith("<", StringComparison.Ordinal)) && (match t.Namespace with null -> false | n -> n.StartsWith("IndyInitRootCauseInvestigator.Engine", StringComparison.Ordinal)))
    |> Array.filter (fun t ->
        // F# union case subclasses and closures are implementation detail; keep declared types and modules.
        match t.DeclaringType with
        | null -> true
        | d -> FSharpType.IsModule d && not (FSharpType.IsUnion(d, BindingFlags.Public)))
    |> Array.sortBy shortName

// ---- examples produced by running the kernel ----

let private exampleTime = DateTimeOffset(2026, 10, 27, 12, 0, 0, TimeSpan.Zero)
let private exampleNs = Guid "3b0c1a52-6c1e-4f0e-9d8e-2a7f4b5c6d70"
let private exampleId name = Hashing.uuidV5 exampleNs ("example/" + name)
let private exampleActor = { ActorId = "investigator-01"; Kind = Human }

let private examples () : (string * Json) list =
    let inv: InvestigationId = Id(exampleId "investigation")
    let env command version step =
        { CommandId = exampleId $"command/{step}"
          Investigation = inv
          ExpectedVersion = version
          Actor = exampleActor
          RequestedAt = exampleTime.AddMinutes(float step)
          Command = command }
    let started =
        match Kernel.submit Kernel.empty (env (StartInvestigation("Checkout failures", Id(exampleId "obs"), "Payments fail since 09:40.")) None 0) with
        | Ok a -> a
        | Error r -> failwithf "%A" r
    let stale = Kernel.submit started.Session (env ActivateInvestigation (Some 7L) 1)
    let refused = Kernel.submit started.Session (env (ConfirmCause(Id(exampleId "missing"))) (Some 1L) 2)
    let rejectionJson (r: Result<Accepted, Rejection>) =
        match r with
        | Ok _ -> failwith "expected a rejection"
        | Error rejection -> JObject [ "code", JString(Kernel.code rejection); "reasons", JArray(Kernel.reasons rejection |> List.map JString) ]
    let caps =
        Projection.capabilities exampleActor exampleTime (started.Session.State |> Option.get)
        |> List.filter (fun c -> match c.Action with LifecycleAction _ -> true | _ -> false)
        |> List.map (fun c ->
            JObject
                [ "action", JString(sprintf "%A" c.Action)
                  "allowed", JBool c.Allowed
                  "blockers", JArray(c.Blockers |> List.map (fun e -> JString $"{e.Code}: {e.Message}")) ])
    let storageError =
        match Storage.read "not-a-uuid" Map.empty with
        | Error e -> JObject [ "code", JString e.Code; "domainCode", Json.opt JString e.DomainCode; "detail", JString e.Detail ]
        | Ok _ -> failwith "expected a refusal"
    [ "A.submit.accepted.events", JArray(started.Events |> List.map Wire.envelope)
      "A.submit.rejected.stale", rejectionJson stale
      "A.submit.rejected.refused", rejectionJson refused
      "B.capabilities.lifecycle", JArray caps
      "C.read.refused", storageError
      "E.report.title", JString((Projection.report (started.Session.State |> Option.get)).Title) ]

// ---- contract tests found in this assembly ----

let private contractTests () =
    Assembly.GetExecutingAssembly().GetTypes()
    |> Array.collect (fun t -> t.GetMethods(BindingFlags.Public ||| BindingFlags.Static))
    |> Array.filter (fun m -> m.GetCustomAttributes(typeof<FactAttribute>, false).Length > 0)
    |> Array.map (fun m ->
        let traits =
            CustomAttributeData.GetCustomAttributes(m)
            |> Seq.filter (fun d -> d.AttributeType = typeof<TraitAttribute> && d.ConstructorArguments.Count = 2)
            |> Seq.map (fun d -> string d.ConstructorArguments[0].Value, string d.ConstructorArguments[1].Value)
            |> List.ofSeq
        let vpos = traits |> List.filter (fun (k, _) -> k = "obligation") |> List.map snd |> List.distinct |> List.sort
        JObject
            [ "test", JString($"{(m.DeclaringType |> nonNull).FullName}.{m.Name}")
              "obligations", JArray(vpos |> List.map JString) ])
    |> Array.sortBy (fun j -> match Json.field "test" j with Some(JString s) -> s | _ -> "")
    |> List.ofArray

let mandatoryNegativeCases =
    [ "stale aggregate version", "Kernel.submit -> StaleVersion; Storage.append -> CAUSA.VERSION_STALE"
      "forged/illegal history", "History.validate / Storage.read stage 15 -> CAUSA.HISTORY_INADMISSIBLE + domain code"
      "unknown required semantic extension", "Storage.read stage 6/7 -> CAUSA.UNKNOWN_EVENT_TYPE / CAUSA.SCHEMA_VIOLATION (closed structures)"
      "unauthorized command", "Kernel.submit -> Unauthorized (D-004)"
      "duplicate submission", "Kernel.submit -> DuplicateCommand"
      "conflicting remote write", "Sync.step PublicationRefused StaleHead -> Held Conflict; reconcile by ancestry"
      "offline queue", "Sync: change held locally (Idle pending / Held NotWritten); Retry only when nothing was written"
      "mismatched frozen schema", "Storage.embeddedSchemaIdentity = Storage.schemaIdentity; Storage.read stage 5 -> CAUSA.UNSUPPORTED_VERSION"
      "incomplete verification", "Policy.resolutionBlockers -> RESOLVE.*; DomainVerification only from domain state"
      "incorrect deployed SHA", "not a kernel concern: owned by the integration stream (G9)" ]

let render () : string =
    let types = engineTypes ()
    let boundaries =
        [ "A", "command submission", "core"
          "B", "versioned projection", "core"
          "C", "replay and storage contract", "core"
          "D", "persistence write invariants (request book); provider adapter owned by the persistence stream", "core"
          "E", "report projection", "core" ]
        |> List.map (fun (key, title, owner) ->
            JObject
                [ "boundary", JString key
                  "title", JString title
                  "owner", JString owner
                  "types", JArray(types |> Array.filter (fun t -> boundaryOf (shortName t) = key) |> Array.map describeType |> List.ofArray) ])
    let manifest =
        JObject
            [ "schema", JString "causa.interface-manifest/1"
              "manifestVersion", JString manifestVersion
              "extractedFrom", JString "IndyInitRootCauseInvestigator.Engine (src/engine), by reflection over the compiled assembly"
              "storageContract",
              JObject
                  [ "version", JString "1.0.0"
                    "schemaIdentity", JString Storage.schemaIdentity
                    "datasetIdentity", JString Storage.datasetIdentity
                    "releaseIdentity", JString Storage.releaseIdentity ]
              "policyId", JString Policy.id
              "boundaries", JArray boundaries
              "shared", JArray(types |> Array.filter (fun t -> boundaryOf (shortName t) = "shared") |> Array.map describeType |> List.ofArray)
              "examples", JObject(examples ())
              "mandatoryNegativeCases", JArray(mandatoryNegativeCases |> List.map (fun (c, how) -> JObject [ "case", JString c; "covered", JString how ]))
              "contractTests", JArray(contractTests ()) ]
    Json.canonical manifest + "\n"

[<Fact>]
[<Trait("obligation", "VPO-6886a2a6e612f704")>]
[<Trait("task", "T14")>]
let ``T14 VPO-6886a2a6e612f704 the committed interface manifest equals the extraction from the compiled kernel`` () =
    let path = Path.Combine(repoRoot.Value, manifestRelativePath)
    let rendered = render ()
    if Environment.GetEnvironmentVariable "CAUSA_WRITE_MANIFEST" = "1" then
        Directory.CreateDirectory(Path.GetDirectoryName path |> nonNull) |> ignore
        File.WriteAllText(path, rendered, UTF8Encoding false)
    Assert.True(File.Exists path, $"{manifestRelativePath} is missing; run with CAUSA_WRITE_MANIFEST=1")
    Assert.Equal(rendered, File.ReadAllText(path, UTF8Encoding false))
