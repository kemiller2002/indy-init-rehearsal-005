/// Requirements proof (S21) and the factory view (S20): screens built only from evidence published with this build
/// (evidence/proof.json, evidence/factory.json and build-info.json, written by scripts/build-site.mjs from files in the
/// checkout and the CI run). Nothing is assumed: what is not proven, or not published, is said in words. A screen
/// module of its own, so the main projection does not grow.
module Causa.App.Evidence

open System.Text.Json
open Limen.Contract.Core
open Causa.App.Routes
open Causa.App.Adapter
open Causa.App.Shell

type Obligation = { Id: string; Source: string; Text: string; Verification: string; Tests: (string * string * string) list }
type Requirement = { Id: string; Title: string option; Priority: string option; Document: string option; SectionSha256: string option; Obligations: Obligation list }

type Proof =
    { Commit: string option
      Run: (string * string) option
      TraceabilitySource: string option
      /// Whether traceability was published at all, and the run and commit it came from (not this build's).
      TraceabilityPublished: bool
      EvidenceRun: string option
      EvidenceSha: string option
      PlanId: string option
      PlanDigest: string option
      Requirements: Requirement list }

let private str (e: JsonElement) (name: string) =
    match e.TryGetProperty name with
    | true, v when v.ValueKind = JsonValueKind.String -> Some(v.GetString())
    | _ -> None

let private arr (e: JsonElement) (name: string) =
    match e.TryGetProperty name with
    | true, v when v.ValueKind = JsonValueKind.Array -> [ for x in v.EnumerateArray() -> x ]
    | _ -> []

let private obj (e: JsonElement) (name: string) =
    match e.TryGetProperty name with
    | true, v when v.ValueKind = JsonValueKind.Object -> Some v
    | _ -> None

let parseProof (text: string) : Result<Proof, string> =
    try
        use doc = JsonDocument.Parse text
        let r = doc.RootElement
        let trace = obj r "traceability"
        let plan = trace |> Option.bind (fun t -> obj t "plan")

        Ok
            { Commit = str r "commit"
              Run = obj r "run" |> Option.bind (fun x -> match str x "id", str x "url" with Some i, Some u -> Some(i, u) | _ -> None)
              TraceabilitySource = trace |> Option.bind (fun t -> str t "source")
              TraceabilityPublished = trace.IsSome
              EvidenceRun = trace |> Option.bind (fun t -> match t.TryGetProperty "run" with | true, v when v.ValueKind = JsonValueKind.String -> Some(v.GetString()) | true, v when v.ValueKind = JsonValueKind.Number -> Some(v.GetRawText()) | _ -> None)
              EvidenceSha = trace |> Option.bind (fun t -> str t "sourceSha")
              PlanId = plan |> Option.bind (fun p -> str p "planId")
              PlanDigest = plan |> Option.bind (fun p -> str p "digest")
              Requirements =
                arr r "requirements"
                |> List.map (fun q ->
                    { Id = str q "id" |> Option.defaultValue ""
                      Title = str q "title"
                      Priority = str q "priority"
                      Document = str q "document"
                      SectionSha256 = str q "sectionSha256"
                      Obligations =
                        arr q "obligations"
                        |> List.map (fun o ->
                            { Id = str o "id" |> Option.defaultValue ""
                              Source = str o "source" |> Option.defaultValue ""
                              Text = str o "text" |> Option.defaultValue ""
                              Verification = str o "verification" |> Option.defaultValue "NOT_RUN"
                              Tests = arr o "tests" |> List.map (fun t -> str t "name" |> Option.defaultValue "", str t "suite" |> Option.defaultValue "", str t "outcome" |> Option.defaultValue "") }) }) }
    with ex ->
        Error("evidence/proof.json could not be read: " + ex.GetType().Name)

/// PASS only when every obligation passed; FAIL when any failed; otherwise not proven (never "passed" by default).
let requirementStatus (r: Requirement) =
    if not r.Obligations.IsEmpty && r.Obligations |> List.forall (fun o -> o.Verification = "PASS") then "Proven"
    elif r.Obligations |> List.exists (fun o -> o.Verification = "FAIL") then "Failing"
    else "Not proven"

let private obligationWords (o: Obligation) =
    match o.Verification, o.Tests with
    | "PASS", _ -> "Proven: every bound test passed"
    | "FAIL", _ -> "Failing: a bound test failed"
    | _, [] -> "Not proven: no test names this obligation"
    | _, _ -> "Not proven: its tests did not run in the published evidence"

let private text (s: string) = ViewValue.Text s
let private flag (b: bool) = ViewValue.Flag b
let private items (fields: (string * string) list list) = fields |> List.map (fun f -> f |> List.map (fun (k, v) -> k, ViewPrimitive.Text v) |> Map.ofList) |> ViewValue.Items

let private loadState (model: Model) (name: string) =
    match model.Evidence.TryFind name with
    | Some(Loaded t) -> Ok t
    | Some(Failed reason) -> Error reason
    | _ -> Error ""

/// S21 keys.
let proofKeys (model: Model) : (string * ViewValue) list =
    let shown = model.Route = RequirementsProof
    let proof = loadState model "proof" |> Result.bind (fun t -> if t = "" then Error "" else parseProof t)
    let loading = match model.Evidence.TryFind "proof" with Some Loading | None -> true | _ -> false
    let failure = match proof with Error e when e <> "" -> e | _ -> ""

    let reqs = match proof with Ok p -> p.Requirements | Error _ -> []
    let selected = reqs |> List.tryFind (fun r -> r.Id = model.Selected) |> Option.orElse (List.tryHead reqs)
    let obligations = reqs |> List.collect (fun r -> r.Obligations)
    let count v = obligations |> List.filter (fun o -> o.Verification = v) |> List.length
    let notProven = obligations.Length - count "PASS"

    let summary =
        match proof with
        | Ok p when not p.TraceabilityPublished -> "Traceability was not published with this build, so nothing is shown as proven."
        | Ok p ->
            let plan = p.PlanId |> Option.defaultValue "the pinned plan"
            $"""Of {obligations.Length} obligations in {plan}: {count "PASS"} proven by passing tests, {count "FAIL"} failing, {obligations.Length - count "PASS" - count "FAIL"} not proven (no test, or not run in the published evidence)."""
        | Error _ -> ""

    let req f = selected |> Option.map f |> Option.defaultValue ""

    [ "proof.show", flag shown
      "proof.loading", flag (shown && loading)
      "proof.failed", flag (shown && failure <> "")
      "proof.failure", text failure
      "proof.ready", flag (shown && Result.isOk proof)
      "proof.summary", text summary
      "proof.notProven", text (if notProven = 0 then "Every published obligation is proven." else $"{notProven} obligation(s) are not proven. They are listed as not proven, never as passed.")
      "proof.requirements", items (reqs |> List.map (fun r -> [ "id", r.Id; "value", r.Id; "label", $"""{r.Id} · {r.Title |> Option.defaultValue "title not published"} · {requirementStatus r}""" ]))
      "proof.selected", text (req (fun r -> r.Id))
      "proof.req.title", text (req (fun r -> $"""{r.Id}: {r.Title |> Option.defaultValue "title not published"}"""))
      "proof.req.priority", text (req (fun r -> "Priority " + (r.Priority |> Option.defaultValue "not published")))
      "proof.req.status", text (req (fun r ->
          let open' = r.Obligations |> List.filter (fun o -> o.Verification <> "PASS") |> List.length
          match requirementStatus r with
          | "Proven" -> $"Proven: all {r.Obligations.Length} obligation(s) have passing tests."
          | "Failing" -> $"Failing: a bound test failed; {open'} of {r.Obligations.Length} obligation(s) are not proven."
          | _ -> $"Not proven: {open'} of {r.Obligations.Length} obligation(s) have no passing test."))
      "proof.req.tone", text (req (fun r -> match requirementStatus r with "Proven" -> "action" | "Failing" -> "conflict" | _ -> "attention"))
      "proof.obligations",
      items (
          selected
          |> Option.map (fun r ->
              r.Obligations
              |> List.map (fun o ->
                  [ "id", o.Id
                    "source", o.Source
                    "text", o.Text
                    "status", obligationWords o
                    "tests", (match o.Tests with [] -> "No test." | ts -> ts |> List.map (fun (n, s, out) -> $"{n} ({s}, {out})") |> String.concat "; ") ]))
          |> Option.defaultValue []
      )
      "proof.workItem", text "No work item is linked to this requirement in the published evidence."
      "proof.source",
      text (req (fun r ->
          match r.Document, r.SectionSha256 with
          | Some d, Some h -> $"{d} (section SHA-256 {h.Substring(0, min 12 h.Length)}…)"
          | Some d, None -> d
          | _ -> "The requirement's source document was not published with this build."))
      "proof.commit",
      text (
          match proof with
          | Ok p ->
              let c = p.Commit |> Option.defaultValue "not recorded"
              let d = p.PlanDigest |> Option.map (fun d -> d.Substring(0, min 12 d.Length) + "…") |> Option.defaultValue "not recorded"
              $"This build: commit {c}; verification plan digest {d}."
          | Error _ -> ""
      )
      "proof.evidenceFrom",
      text (
          match proof with
          | Ok p when not p.TraceabilityPublished -> "Evidence: not published with this build."
          | Ok p ->
              let run = p.EvidenceRun |> Option.defaultValue "not recorded"
              let sha = p.EvidenceSha |> Option.defaultValue "not recorded"
              $"Evidence from main run {run} at {sha}."
          | Error _ -> ""
      )
      "proof.runtime",
      text (
          match proof with
          | Ok { Run = Some(id, url) } -> $"This build: CI run {id} ({url})."
          | Ok _ -> "This build: not made by CI, so no run is recorded."
          | Error _ -> ""
      ) ]

type private Factory = { Text: string }

let private factoryKeys (model: Model) : (string * ViewValue) list =
    let shown = model.Route = FactoryView
    let factory = loadState model "factory"
    let loading = match model.Evidence.TryFind "factory" with Some Loading | None -> true | _ -> false
    let failure = match factory with Error e when e <> "" -> e | _ -> ""
    let proof = loadState model "proof" |> Result.bind (fun t -> if t = "" then Error "" else parseProof t)

    let parsed =
        match factory with
        | Ok t when t <> "" ->
            try
                Some(JsonDocument.Parse t)
            with _ ->
                None
        | _ -> None

    let root = parsed |> Option.map (fun d -> d.RootElement)
    let section name = root |> Option.bind (fun r -> obj r name)
    let ros = section "ros"
    let rosItems = ros |> Option.map (fun r -> arr r "items") |> Option.defaultValue []
    let byStatus = rosItems |> List.countBy (fun i -> str i "status" |> Option.defaultValue "unknown") |> List.map (fun (s, n) -> $"{n} {s}") |> String.concat ", "
    let num (e: JsonElement option) name = e |> Option.bind (fun x -> match x.TryGetProperty(name: string) with | true, v when v.ValueKind = JsonValueKind.Number -> Some(v.GetInt32()) | _ -> None)

    let obligations = match proof with Ok p -> p.Requirements |> List.collect (fun r -> r.Obligations) | Error _ -> []
    let tests = obligations |> List.collect (fun o -> o.Tests) |> List.distinct
    let testsBy outcome = tests |> List.filter (fun (_, _, o) -> o = outcome) |> List.length
    let reqs = match proof with Ok p -> p.Requirements | Error _ -> []
    let reqBy s = reqs |> List.filter (fun r -> requirementStatus r = s) |> List.length
    let build = match model.Build with Loaded pairs -> pairs | _ -> []
    let buildValue k = build |> List.tryFind (fun (n, _) -> n = k) |> Option.map snd

    let result =
        [ "factory.show", flag shown
          "factory.loading", flag (shown && loading)
          "factory.failed", flag (shown && failure <> "")
          "factory.failure", text failure
          "factory.ready", flag (shown && root.IsSome)
          "factory.ros.summary",
          text (
              match ros with
              | Some r ->
                  let events = num ros "events" |> Option.map string |> Option.defaultValue "not published"
                  let executions = num ros "executions" |> Option.map string |> Option.defaultValue "not published"
                  $"""{rosItems.Length} ROS work item(s): {byStatus}. ROS events recorded: {events}. Executions recorded: {executions}. Source: .ros in commit {(root |> Option.bind (fun r -> str r "commit") |> Option.defaultValue "not recorded")}."""
              | None -> "ROS work data was not published with this build."
          )
          "factory.ros.items",
          items (rosItems |> List.map (fun i -> [ "id", str i "id" |> Option.defaultValue ""; "text", $"""{str i "id" |> Option.defaultValue ""} · {str i "status" |> Option.defaultValue "status not recorded"} · {str i "title" |> Option.defaultValue ""}""" ]))
          "factory.requirements",
          text (
              if reqs.IsEmpty then "The requirements traceability was not published with this build."
              else $"""{reqs.Length} P0 requirements: {reqBy "Proven"} proven, {reqBy "Failing"} failing, {reqBy "Not proven"} not proven, by the traceability published with this build."""
          )
          "factory.tests",
          text (
              if obligations.IsEmpty then "No test results were published with this build."
              else $"""{tests.Length} bound test(s) in the published evidence: {testsBy "passed"} passed, {testsBy "failed"} failed; {obligations |> List.filter (fun o -> o.Tests.IsEmpty) |> List.length} obligation(s) have no test."""
          )
          "factory.praxis",
          text (
              match section "praxis" with
              | Some p -> $"""Praxis {str p "version" |> Option.defaultValue "version not recorded"}, instance {str p "instanceId" |> Option.defaultValue "not recorded"}, repository {str p "repository" |> Option.defaultValue "not recorded"} (.praxis/instance.json)."""
              | None -> "Praxis data was not published with this build."
          )
          "factory.aegis",
          text (
              match section "aegis" with
              | Some a -> $"""{num (Some a) "boundaries" |> Option.map string |> Option.defaultValue "Unknown number of"} Aegis boundaries declared (aegis-boundaries.json). Runtime faults: none are published with this build, so none are shown."""
              | None -> "Aegis data was not published with this build."
          )
          "factory.deployment",
          text (
              let commit = buildValue "commit" |> Option.defaultValue "not recorded"
              let built = buildValue "builtAt" |> Option.defaultValue "not recorded"
              let run = root |> Option.bind (fun r -> obj r "run") |> Option.bind (fun r -> str r "url") |> Option.defaultValue "no CI run recorded"
              $"Commit {commit}, built {built}; {run}."
          ) ]

    parsed |> Option.iter (fun d -> d.Dispose())
    result

let keys (model: Model) : (string * ViewValue) list = proofKeys model @ factoryKeys model
