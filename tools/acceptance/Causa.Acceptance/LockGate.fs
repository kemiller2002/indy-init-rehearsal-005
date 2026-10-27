/// T11: stack lock and compatibility gate (VER-004, VER-008, DEP-CURRENT-004..008).
///
/// Compares, against the pinned echelon-current channel resolution (a byte-pinned copy of the Echelon Registry's
/// channels/echelon-current/linux-x64.json):
/// - .conditor/lock.json (and the conditor.json manifest it pins),
/// - the declared npm dependencies, package-lock.json and the installed node_modules,
/// - NuGet PackageReferences and the restored project.assets.json,
/// - the Praxis pin and the host tools on PATH.
/// Any version that differs is a FAIL. Something that cannot be checked yet (no package-lock.json, a tool or a
/// package that is not installed) is a GAP: the gate is then INCOMPLETE, never PASS.
module Causa.Acceptance.LockGate

open System
open System.Diagnostics
open System.IO
open System.Text.RegularExpressions
open System.Xml.Linq
open Causa.Acceptance.Json

type Status =
    | Pass
    | Fail
    | Gap

type Check =
    { Id: string
      Subject: string
      Expected: string
      Actual: string
      Status: Status
      Detail: string }

let statusName =
    function
    | Pass -> "PASS"
    | Fail -> "FAIL"
    | Gap -> "GAP"

let private check id subject expected actual detail =
    { Id = id
      Subject = subject
      Expected = expected
      Actual = actual
      Status = (if expected = actual then Pass else Fail)
      Detail = detail }

let private gap id subject expected detail =
    { Id = id; Subject = subject; Expected = expected; Actual = "unavailable"; Status = Gap; Detail = detail }

/// systemId -> resolved version, from the channel resolution.
let channelVersions (channel: Text.Json.Nodes.JsonNode) =
    items "components" channel |> List.map (fun c -> strOr "" "systemId" c, strOr "" "version" c) |> Map.ofList

/// NuGet package id -> systemId, from channel tags of the form `nuget:<Package>@<version>`.
let nugetPackages (channel: Text.Json.Nodes.JsonNode) =
    items "components" channel
    |> List.choose (fun c ->
        let tag = strOr "" "tag" c
        if tag.StartsWith "nuget:" then
            let body = tag.Substring 6
            let at = body.LastIndexOf '@'
            if at > 0 then Some(body.Substring(0, at), strOr "" "systemId" c) else None
        else None)
    |> Map.ofList

/// Lock component id -> channel systemId. The lock calls the repository-lifecycle Percepta "percepta".
let lockSystemId (id: string) = id

/// npm package -> systemId (Conditor's own mapping for the Echelon web packages).
let npmPackages =
    [ "@echelon-foundry/limen", "limen"
      "@echelon-foundry/design-system", "forma"
      "@echelon-foundry/print-components", "folio" ]

/// Host executable -> systemId, and how to read its version from `--version` output.
let hostTools =
    [ "praxis", "praxis"
      "ordo", "ordo"
      "dokimos", "dokimos"
      "percepta", "percepta-semantic"
      "percepta-repo", "percepta" ]

let semver = Regex(@"(\d+\.\d+\.\d+)(?:[-+][0-9A-Za-z.\-+]*)?")

/// The release version a tool prints: Dokimos prints JSON with ReleaseVersion; the others print a version line.
let parseToolVersion (output: string) =
    let m = Regex.Match(output, "\"ReleaseVersion\"\\s*:\\s*\"([^\"]+)\"")
    if m.Success then Some m.Groups[1].Value
    else
        let s = semver.Match output
        if s.Success then Some s.Groups[1].Value else None

let runVersion (exe: string) : string option =
    try
        let psi = ProcessStartInfo(exe, "--version", RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false)
        use p = Process.Start psi
        let out = p.StandardOutput.ReadToEnd() + p.StandardError.ReadToEnd()
        if p.WaitForExit 30000 then parseToolVersion out else None
    with _ -> None

/// Version a declared npm dependency names: an exact version, or a release tarball URL containing it.
let declaredNpmVersion (spec: string) =
    if Regex.IsMatch(spec, @"^\d+\.\d+\.\d+$") then Some spec
    else
        let m = Regex.Match(spec, @"-(\d+\.\d+\.\d+)\.tgz$")
        if m.Success then Some m.Groups[1].Value else None

type Inputs =
    { Root: string
      Channel: Text.Json.Nodes.JsonNode
      ChannelSha256: string
      Pin: Text.Json.Nodes.JsonNode
      /// executable -> version reported, None when not on PATH.
      HostVersions: Map<string, string option>
      RequiredHostTools: string list
      /// Whether package-lock.json is tracked by git. A lock that `npm install` just generated proves nothing.
      PackageLockCommitted: bool }

let private readText root rel =
    let p = Path.Combine(root, rel)
    if File.Exists p then Some(File.ReadAllText p) else None

let evaluate (i: Inputs) : Check list =
    let versions = channelVersions i.Channel
    let v sys = versions.TryFind sys |> Option.defaultValue "absent-from-channel"
    let root = i.Root

    let pinChecks =
        [ yield check "channel.sha256" "acceptance/stack channel resolution" (strOr "" "channelSha256" i.Pin) i.ChannelSha256 "byte pin of the Echelon Registry channel file"
          yield check "channel.profile" "echelon-current profile version" (strOr "" "profileVersion" i.Pin) (field "profile" i.Channel |> Option.bind (str "version") |> Option.defaultValue "")
              "the channel resolves the profile this project is bootstrapped on"
          for sys, expected in field "expected" i.Pin |> Option.map props |> Option.defaultValue [] do
              let e = expected.GetValue<string>()
              yield check (sprintf "channel.%s" sys) (sprintf "%s resolved by echelon-current" sys) e (v sys) "the stack named for this rehearsal equals the channel resolution" ]

    let lockChecks =
        match Json.tryLoad (Path.Combine(root, ".conditor/lock.json")) with
        | None -> [ { (gap "lock.present" ".conditor/lock.json" "present" "the Conditor lock is missing or unreadable") with Status = Fail } ]
        | Some lock ->
            let manifestSha =
                let p = Path.Combine(root, "conditor.json")
                if File.Exists p then sha256File p else "absent"
            let lockComponents = items "components" lock |> List.map (fun c -> strOr "" "id" c, strOr "" "version" c)
            let manifestComponents =
                Json.tryLoad (Path.Combine(root, "conditor.json"))
                |> Option.map (items "components" >> List.map (fun c -> strOr "" "id" c, strOr "" "version" c))
                |> Option.defaultValue []
            [ yield check "lock.manifest" ".conditor/lock.json manifestSha256" (strOr "" "manifestSha256" lock) manifestSha "the lock pins the exact conditor.json in the tree"
              for id, ver in lockComponents do
                  yield check (sprintf "lock.%s" id) (sprintf ".conditor/lock.json %s" id) (v (lockSystemId id)) ver "locked version equals the channel resolution"
              for id, ver in manifestComponents do
                  yield check (sprintf "manifest.%s" id) (sprintf "conditor.json %s" id) (v (lockSystemId id)) ver "declared version equals the channel resolution" ]

    let npmChecks =
        let pkg = Json.tryLoad (Path.Combine(root, "package.json"))
        let deps =
            pkg
            |> Option.map (fun p ->
                [ "dependencies"; "devDependencies" ]
                |> List.collect (fun k -> field k p |> Option.map props |> Option.defaultValue [])
                |> List.map (fun (k, n) -> k, n.GetValue<string>()))
            |> Option.defaultValue []
            |> Map.ofList
        let packageLock = if i.PackageLockCommitted then Json.tryLoad (Path.Combine(root, "package-lock.json")) else None
        [ for name, sys in npmPackages do
              match deps.TryFind name with
              | None -> yield { (gap (sprintf "npm.declared.%s" sys) (sprintf "package.json %s" name) (v sys) "not declared") with Status = Fail }
              | Some spec ->
                  yield check (sprintf "npm.declared.%s" sys) (sprintf "package.json %s" name) (v sys) (declaredNpmVersion spec |> Option.defaultValue spec) spec
              match packageLock with
              | None -> yield gap (sprintf "npm.lock.%s" sys) (sprintf "package-lock.json %s" name) (v sys) "package-lock.json is not committed: the installed tree is not reproducible"
              | Some l ->
                  let entry = field "packages" l |> Option.bind (field ("node_modules/" + name))
                  match entry |> Option.bind (str "version") with
                  | None -> yield { (gap (sprintf "npm.lock.%s" sys) (sprintf "package-lock.json %s" name) (v sys) "absent from package-lock.json") with Status = Fail }
                  | Some ver -> yield check (sprintf "npm.lock.%s" sys) (sprintf "package-lock.json %s" name) (v sys) ver (entry |> Option.bind (str "integrity") |> Option.defaultValue "no integrity recorded")
              match Json.tryLoad (Path.Combine(root, "node_modules", name, "package.json")) with
              | None -> yield gap (sprintf "npm.installed.%s" sys) (sprintf "node_modules/%s" name) (v sys) "not installed in this checkout"
              | Some installed -> yield check (sprintf "npm.installed.%s" sys) (sprintf "node_modules/%s" name) (v sys) (strOr "" "version" installed) "installed version" ]

    // Echelon packages outside the release set, each pinned in pin.json against an accepted owner decision record:
    // reported as such, version- and integrity-checked like the channel set, never silently ignored. An Echelon
    // package that is neither in the channel mapping nor pinned this way fails the gate.
    let outsideChannelChecks =
        let pkg = Json.tryLoad (Path.Combine(root, "package.json"))
        let deps =
            pkg
            |> Option.map (fun p -> [ "dependencies"; "devDependencies" ] |> List.collect (fun k -> field k p |> Option.map props |> Option.defaultValue []) |> List.map (fun (k, n) -> k, n.GetValue<string>()))
            |> Option.defaultValue []
            |> Map.ofList
        let packageLock = if i.PackageLockCommitted then Json.tryLoad (Path.Combine(root, "package-lock.json")) else None
        let pinned = items "outsideChannel" i.Pin
        let pinnedNames = pinned |> List.map (strOr "" "package") |> Set.ofList
        let channelNames = npmPackages |> List.map fst |> Set.ofList
        [ for entry in pinned do
              let name = strOr "" "package" entry
              let version = strOr "" "version" entry
              let decision = field "decision" entry
              let decisionId = decision |> Option.bind (str "id") |> Option.defaultValue ""
              let decisionPath = decision |> Option.bind (str "path") |> Option.defaultValue ""
              let subject what = sprintf "%s %s (outside echelon-current; owner-approved %s)" what name decisionId
              let id what = sprintf "outside.%s.%s" what name
              let record = readText root decisionPath |> Option.defaultValue ""
              let accepted =
                  record.Contains(sprintf "id: %s" decisionId)
                  && Regex.IsMatch(record, @"(?m)^status: accepted\s*$")
                  && record.Contains(sprintf "`%s` %s" name version)
              yield
                  { Id = id "decision"
                    Subject = subject "decision record"
                    Expected = sprintf "%s accepted for %s %s" decisionId name version
                    Actual = (if record = "" then "absent" elif accepted then sprintf "%s accepted for %s %s" decisionId name version else "not an accepted decision for this version")
                    Status = (if accepted then Pass else Fail)
                    Detail = decisionPath }
              match deps.TryFind name with
              | None -> yield { (gap (id "declared") (subject "package.json") version "not declared") with Status = Fail }
              | Some spec -> yield check (id "declared") (subject "package.json") version (declaredNpmVersion spec |> Option.defaultValue spec) spec
              match packageLock with
              | None -> yield gap (id "lock") (subject "package-lock.json") version "package-lock.json is not committed"
              | Some l ->
                  let e = field "packages" l |> Option.bind (field ("node_modules/" + name))
                  let integrity = e |> Option.bind (str "integrity") |> Option.defaultValue "no integrity recorded"
                  yield check (id "lock") (subject "package-lock.json") version (e |> Option.bind (str "version") |> Option.defaultValue "absent") integrity
                  yield check (id "integrity") (subject "package-lock.json integrity") (strOr "" "integrity" entry) integrity integrity
              match Json.tryLoad (Path.Combine(root, "node_modules", name, "package.json")) with
              | None -> yield gap (id "installed") (subject "node_modules") version "not installed in this checkout"
              | Some installed -> yield check (id "installed") (subject "node_modules") version (strOr "" "version" installed) "installed version"
          for name in deps |> Map.keys do
              if name.StartsWith "@echelon-foundry/" && not (channelNames.Contains name) && not (pinnedNames.Contains name) then
                  yield
                      { Id = sprintf "outside.unpinned.%s" name
                        Subject = sprintf "package.json %s" name
                        Expected = "in echelon-current, or pinned against an accepted owner decision"
                        Actual = deps[name]
                        Status = Fail
                        Detail = "an Echelon package outside the verified release set without an owner decision" } ]

    let nugetChecks =
        let packages = nugetPackages i.Channel
        let projects =
            Directory.GetFiles(root, "*.*proj", SearchOption.AllDirectories)
            |> Array.filter (fun p -> p.EndsWith ".fsproj" || p.EndsWith ".csproj")
            |> Array.filter (fun p -> not (p.Replace('\\', '/').Contains "/node_modules/"))
            |> Array.sort
            |> List.ofArray
        [ for proj in projects do
              let rel = Path.GetRelativePath(root, proj).Replace('\\', '/')
              let doc = XDocument.Load proj
              for pr in doc.Descendants(XName.Get "PackageReference") do
                  let id = match pr.Attribute(XName.Get "Include") with | null -> "" | a -> a.Value
                  match packages.TryFind id with
                  | None -> ()
                  | Some sys ->
                      let ver = match pr.Attribute(XName.Get "Version") with | null -> "unversioned" | a -> a.Value
                      yield check (sprintf "nuget.declared.%s.%s" sys rel) (sprintf "%s %s" rel id) (v sys) ver "PackageReference version"
              let assets = Path.Combine(Path.GetDirectoryName proj, "obj", "project.assets.json")
              match Json.tryLoad assets with
              | None -> ()
              | Some a ->
                  for key, _ in field "libraries" a |> Option.map props |> Option.defaultValue [] do
                      let slash = key.IndexOf '/'
                      if slash > 0 then
                          match packages.TryFind(key.Substring(0, slash)) with
                          | Some sys -> yield check (sprintf "nuget.restored.%s.%s" sys rel) (sprintf "%s restored %s" rel (key.Substring(0, slash))) (v sys) (key.Substring(slash + 1)) "restored version (project.assets.json)"
                          | None -> () ]

    let praxisPin =
        [ match readText root "ros.json" |> Option.map parse with
          | Some r -> yield check "praxis.pin.ros" "ros.json rosVersion" (v "praxis") (strOr "" "rosVersion" r) "the version ./praxis installs"
          | None -> ()
          match readText root ".echelon/ros.json" |> Option.map parse with
          | Some r -> yield check "praxis.pin.installed" ".echelon/ros.json installedVersion" (v "praxis") (strOr "" "installedVersion" r) "the version ./praxis runs"
          | None -> () ]

    let hostChecks =
        [ for exe, sys in hostTools do
              match i.HostVersions.TryFind exe |> Option.flatten with
              | Some ver -> yield check (sprintf "host.%s" exe) (sprintf "%s --version" exe) (v sys) ver "installed host tool"
              | None ->
                  let g = gap (sprintf "host.%s" exe) (sprintf "%s --version" exe) (v sys) "not on PATH in this environment"
                  yield (if List.contains exe i.RequiredHostTools then { g with Status = Fail; Detail = "required here, but not on PATH" } else g) ]

    pinChecks @ lockChecks @ npmChecks @ outsideChannelChecks @ nugetChecks @ praxisPin @ hostChecks

let verdict (checks: Check list) =
    if checks |> List.exists (fun c -> c.Status = Fail) then "FAIL"
    elif checks |> List.exists (fun c -> c.Status = Gap) then "INCOMPLETE"
    else "PASS"

let toJson (checks: Check list) (context: (string * Text.Json.Nodes.JsonNode) list) =
    let n s = checks |> List.filter (fun c -> c.Status = s) |> List.length
    jobj
        [ "schema", jstr "causa.stack-lock-gate/1"
          "task", jstr "T11"
          "verdict", jstr (verdict checks)
          "context", jobj context
          "summary", jobj [ "PASS", jint (n Pass); "FAIL", jint (n Fail); "GAP", jint (n Gap) ]
          "checks",
          jarr (
              checks
              |> List.map (fun c ->
                  jobj
                      [ "id", jstr c.Id
                        "subject", jstr c.Subject
                        "expected", jstr c.Expected
                        "actual", jstr c.Actual
                        "status", jstr (statusName c.Status)
                        "detail", jstr c.Detail ])
          ) ]
