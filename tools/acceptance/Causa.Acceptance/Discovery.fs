/// Discovers executed tests and the verification-plan obligations (VPO ids) each one is tagged with.
///
/// Sources:
/// - VSTest TRX files (`dotnet test --logger trx`). A VPO id is taken from the test name, and from
///   `[<Trait("obligation", "VPO-…")>]` on the test method, read from the test assembly's metadata (TRX does not
///   carry traits) without executing it.
/// - Playwright JSON reports (`--reporter=json`). A VPO id is taken from the title path, the tags
///   (`{ tag: "@VPO-…" }` or `@VPO-…` in a title) and annotations (`{ type: "obligation", description: "VPO-…" }`).
///
/// A test that never ran is never reported here: only results present in a result file are discovered.
module Causa.Acceptance.Discovery

open System
open System.IO
open System.Reflection
open System.Text.RegularExpressions
open System.Xml.Linq
open Causa.Acceptance.Json

type Outcome =
    | Passed
    | Failed
    | Skipped

type Suite =
    | DotnetTest
    | Playwright

type TestResult =
    { /// Stable identity: `dotnet:<Class>.<Method>` or `playwright:<file> > <title path>`.
      TestId: string
      DisplayName: string
      Suite: Suite
      Outcome: Outcome
      Obligations: string list
      /// Repository-relative path of the result file this came from.
      Source: string }

let vpoPattern = Regex(@"VPO-[0-9a-f]{16}", RegexOptions.Compiled)

let obligationsIn (texts: string seq) =
    texts
    |> Seq.filter (String.IsNullOrEmpty >> not)
    |> Seq.collect (fun t -> vpoPattern.Matches t |> Seq.map (fun m -> m.Value))
    |> Seq.distinct
    |> Seq.sort
    |> List.ofSeq

let outcomeName =
    function
    | Passed -> "passed"
    | Failed -> "failed"
    | Skipped -> "skipped"

let suiteName =
    function
    | DotnetTest -> "dotnet"
    | Playwright -> "playwright"

// ---------------------------------------------------------------------------------------------------------------
// Traits from test assemblies (metadata only; the assembly is never executed).

/// (className, methodName) -> obligation ids from `Trait(traitName, …)`.
let traitObligationsNamed (traitName: string) (assemblyPath: string) : Map<string * string, string list> =
    if not (File.Exists assemblyPath) then Map.empty
    else
        let dir = Path.GetDirectoryName assemblyPath
        let runtimeDir = Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory()
        let paths =
            Seq.append (Directory.GetFiles(runtimeDir, "*.dll")) (Directory.GetFiles(dir, "*.dll"))
            |> Seq.distinctBy Path.GetFileName
        use mlc = new MetadataLoadContext(PathAssemblyResolver(paths))
        try
            let asm = mlc.LoadFromAssemblyPath assemblyPath
            let types =
                try asm.GetTypes()
                with :? ReflectionTypeLoadException as ex -> ex.Types |> Array.filter (isNull >> not)
            [ for t in types do
                  let methods =
                      try t.GetMethods(BindingFlags.Public ||| BindingFlags.NonPublic ||| BindingFlags.Static ||| BindingFlags.Instance ||| BindingFlags.DeclaredOnly)
                      with _ -> [||]
                  for m in methods do
                      let ids =
                          try
                              m.GetCustomAttributesData()
                              |> Seq.filter (fun a -> a.AttributeType.FullName = "Xunit.TraitAttribute" && a.ConstructorArguments.Count = 2)
                              |> Seq.filter (fun a -> string a.ConstructorArguments[0].Value = traitName)
                              |> Seq.map (fun a -> string a.ConstructorArguments[1].Value)
                              |> obligationsIn
                          with _ -> []
                      if not ids.IsEmpty then yield (t.FullName.Replace('+', '.'), m.Name), ids ]
            |> List.groupBy fst
            |> List.map (fun (k, v) -> k, v |> List.collect snd |> List.distinct |> List.sort)
            |> Map.ofList
        with _ -> Map.empty

/// (className, methodName) -> obligation ids from `Trait("obligation", …)`.
let traitObligations = traitObligationsNamed "obligation"

// ---------------------------------------------------------------------------------------------------------------
// TRX

let private ns = XNamespace.Get "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"

let private attr (name: string) (e: XElement) =
    match e.Attribute(XName.Get name) with
    | null -> ""
    | a -> a.Value

/// Parses one TRX document. `traits` maps (class, method) to trait-tagged obligations.
let parseTrx (source: string) (traits: Map<string * string, string list>) (doc: XDocument) : TestResult list =
    let definitions =
        doc.Descendants(ns + "UnitTest")
        |> Seq.map (fun ut ->
            let tm = ut.Element(ns + "TestMethod")
            let cls, meth, storage = (if isNull tm then "", "", "" else attr "className" tm, attr "name" tm, attr "codeBase" tm)
            attr "id" ut, (cls, meth, storage))
        |> Map.ofSeq

    doc.Descendants(ns + "UnitTestResult")
    |> Seq.map (fun r ->
        let name = attr "testName" r
        let cls, meth, _ = definitions.TryFind(attr "testId" r) |> Option.defaultValue ("", "", "")
        let fromTraits = traits.TryFind((cls, meth)) |> Option.defaultValue []
        let outcome =
            match attr "outcome" r with
            | "Passed" -> Passed
            | "NotExecuted" | "Inconclusive" | "Pending" -> Skipped
            | _ -> Failed
        { TestId = (if cls <> "" && meth <> "" then sprintf "dotnet:%s.%s" cls meth else "dotnet:" + name)
          DisplayName = name
          Suite = DotnetTest
          Outcome = outcome
          Obligations = obligationsIn (name :: fromTraits)
          Source = source })
    |> List.ofSeq

/// Test assemblies a TRX file references (TestMethod/@codeBase), for trait discovery.
let trxAssemblies (doc: XDocument) =
    doc.Descendants(ns + "TestMethod") |> Seq.map (attr "codeBase") |> Seq.filter (String.IsNullOrEmpty >> not) |> Seq.distinct |> List.ofSeq

// ---------------------------------------------------------------------------------------------------------------
// Playwright JSON reporter

let rec private specsOf (titles: string list) (file: string) (suite: Text.Json.Nodes.JsonNode) =
    let title = strOr "" "title" suite
    let file' = str "file" suite |> Option.defaultValue file
    // The root suite of a file is titled with the file name; keep the title path readable without repeating it.
    let path = if title = "" || title = file' then titles else titles @ [ title ]
    [ for spec in items "specs" suite do
          yield path, file', spec
      for child in items "suites" suite do
          yield! specsOf path file' child ]

let parsePlaywright (source: string) (report: Text.Json.Nodes.JsonNode) : TestResult list =
    [ for top in items "suites" report do
          for titles, file, spec in specsOf [] "" top do
              let specTitle = strOr "" "title" spec
              let tags = strings "tags" spec
              for test in items "tests" spec do
                  let project = strOr "" "projectName" test
                  let annotations =
                      items "annotations" test
                      |> List.collect (fun a -> [ strOr "" "type" a; strOr "" "description" a ])
                  let results = items "results" test |> List.map (strOr "" "status")
                  let outcome =
                      match strOr "" "status" test with
                      | "expected" when results |> List.exists (fun s -> s = "passed") -> Passed
                      | "skipped" -> Skipped
                      | "expected" -> Skipped
                      | _ -> Failed // unexpected, flaky (a retry is not a pass), or unknown
                  let titlePath = String.Join(" > ", titles @ [ specTitle ])
                  let projectSuffix = if project = "" then "" else sprintf " [%s]" project
                  { TestId = sprintf "playwright:%s > %s%s" file titlePath projectSuffix
                    DisplayName = titlePath
                    Suite = Playwright
                    Outcome = outcome
                    Obligations = obligationsIn (titlePath :: tags @ annotations)
                    Source = source } ]

// ---------------------------------------------------------------------------------------------------------------
// Directory scan

let private relative (root: string) (path: string) =
    Path.GetRelativePath(root, path).Replace('\\', '/')

/// Every *.trx and every Playwright JSON report (a JSON file with a top-level "suites" and "config") under `dirs`.
/// When `assemblyRoots` is given, trait tags are read from the test assemblies the TRX files reference, resolved
/// first at their recorded path and otherwise by file name under the assembly roots.
let discover (repoRoot: string) (dirs: string list) (assemblyRoots: string list) : TestResult list =
    let files =
        dirs
        |> List.filter Directory.Exists
        |> List.collect (fun d -> Directory.GetFiles(d, "*", SearchOption.AllDirectories) |> List.ofArray)
        |> List.distinct
        |> List.sort

    let findAssembly (recorded: string) =
        if File.Exists recorded then Some recorded
        else
            let name = Path.GetFileName recorded
            assemblyRoots
            |> List.filter Directory.Exists
            |> List.tryPick (fun r ->
                Directory.GetFiles(r, name, SearchOption.AllDirectories)
                |> Array.filter (fun p -> p.Contains("/bin/") || p.Contains("\\bin\\"))
                |> Array.sortDescending
                |> Array.tryHead)

    files
    |> List.collect (fun f ->
        let src = relative repoRoot f
        if f.EndsWith(".trx", StringComparison.OrdinalIgnoreCase) then
            let doc = XDocument.Load f
            let traits =
                trxAssemblies doc
                |> List.choose findAssembly
                |> List.map traitObligations
                |> List.fold (fun acc m -> Map.fold (fun a k v -> Map.add k v a) acc m) Map.empty
            parseTrx src traits doc
        elif f.EndsWith(".json", StringComparison.OrdinalIgnoreCase) then
            match tryLoad f with
            | Some j when (field "suites" j).IsSome && (field "config" j).IsSome -> parsePlaywright src j
            | _ -> []
        else [])
