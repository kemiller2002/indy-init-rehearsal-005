/// derive-requirements: identity derivation of the Indy-init baseline into a
/// Praxis RQ package, its praxis.requirements-disposition/1 record, the review
/// requests and a derivation record. Writes no approval and invents no review:
/// --reviews applies an operator record after checking it covers only
/// review-requested, byte-identical obligations.
module DeriveRequirements.Program

open System
open System.IO
open System.Text.Json.Nodes
open DeriveRequirements.Domain
open DeriveRequirements.Derivation

type Args =
    { Baseline: string
      Normalized: string
      Source: string option
      Reviews: string option
      ReviewsRef: string option
      Package: string
      Out: string }

let rec private parse (argv: string list) (acc: Map<string, string>) =
    match argv with
    | flag :: value :: rest when flag.StartsWith("--") -> parse rest (Map.add (flag.Substring 2) value acc)
    | [] -> Ok acc
    | other :: _ -> Error $"unexpected argument '{other}'"

let private toArgs (m: Map<string, string>) =
    match Map.tryFind "baseline" m, Map.tryFind "normalized" m, Map.tryFind "out" m with
    | Some b, Some n, Some o ->
        Ok
            { Baseline = b
              Normalized = n
              Source = Map.tryFind "source" m
              Reviews = Map.tryFind "reviews" m
              ReviewsRef = Map.tryFind "reviews-ref" m
              Package = Map.tryFind "package" m |> Option.defaultValue "indy-init-rehearsal-005"
              Out = o }
    | _ -> Error "usage: derive-requirements --baseline FILE --normalized FILE --out DIR [--source DIR] [--reviews FILE --reviews-ref TEXT] [--package NAME]"

let private arr (items: JsonNode seq) =
    JsonArray(items |> Seq.map (fun x -> (x: JsonNode | null)) |> Seq.toArray) :> JsonNode
let private s (v: string) = nonNull (JsonValue.Create(v)) :> JsonNode
let private n (v: int) = JsonValue.Create(v) :> JsonNode
let private b (v: bool) = JsonValue.Create(v) :> JsonNode

let private obj (pairs: (string * JsonNode) list) =
    let o = JsonObject()
    for k, v in pairs do
        o.Add(k, v)
    o :> JsonNode

let private baselineNode (bl: Baseline) =
    obj [ "id", s bl.Id; "commit", s bl.Commit; "sha256", s bl.Sha256 ]

let dispositionJson (bl: Baseline) (package: string) (ds: Disposition list) =
    let mapping (m: Mapping) =
        obj (
            [ "obligation", s m.Obligation; "criteria", arr (m.Criteria |> List.map s) ]
            @ (match m.Review with
               | Some r -> [ "review", obj [ "reviewer", s r.Reviewer; "date", s r.Date; "basis", s r.Basis ] ]
               | None -> [])
        )

    obj
        [ "schema", s "praxis.requirements-disposition/1"
          "baseline", baselineNode bl
          "package", s package
          "dispositions",
          arr (
              ds
              |> List.map (fun d ->
                  obj
                      [ "requirement", s d.Requirement
                        "disposition", s "preserved"
                        "mappings", arr (d.Mappings |> List.map mapping) ])
          )
          "conflicts", arr [] ]

let reviewRequestsJson (bl: Baseline) (requests: ReviewRequest list) =
    obj
        [ "schema", s "indy-init.r5-review-requests/1"
          "status", s "requested: this file records no review; only a trusted person may decide equivalence (RDP-002)"
          "basis",
          s
              "Identity derivation: each criterion is its obligation's text. Praxis decides a token-less obligation by verbatim carriage only from five words up; each obligation below is shorter, so the gate keeps it needs-review until a person records a review."
          "baselineSha256", s bl.Sha256
          "count", n requests.Length
          "byteIdentical", n (requests |> List.filter (fun q -> q.ByteIdentical) |> List.length)
          "requests",
          arr (
              requests
              |> List.map (fun q ->
                  obj
                      [ "requirement", s q.Requirement
                        "obligation", s q.Obligation
                        "criterion", s q.Criterion
                        "text", s q.Text
                        "criterionText", s q.CriterionText
                        "byteIdentical", b q.ByteIdentical ])
          ) ]

let private sourceViolations (source: string) (bl: Baseline) =
    let docs =
        bl.Requirements
        |> List.map (fun r -> r.Document)
        |> List.distinct
        |> List.map (fun d -> d, File.ReadAllText(Path.Combine(source, d)).Replace("\r\n", "\n").Replace("\r", "\n"))
        |> Map.ofList

    bl.Requirements
    |> List.collect (fun r ->
        r.Obligations
        |> List.filter (fun o -> not (docs.[r.Document].Contains(o.Text, StringComparison.Ordinal)))
        |> List.map (fun o -> $"{o.Id}: obligation text is not verbatim in {r.Document} at the source checkout"))

let run (a: Args) =
    let bl = Io.readBaseline a.Baseline
    // The compiled package is an input of record: it must parse, and its digest is recorded below.
    Io.readNormalized a.Normalized |> ignore
    let docs = deriveDocuments bl
    let rendered = bl.Requirements |> List.map (fun r -> let d = docs.[r.Id] in d, Markdown.render d)
    let parsed = parsedCriteria rendered
    let carriage = verifyCarriage bl docs parsed
    let source = a.Source |> Option.map (fun src -> sourceViolations src bl) |> Option.defaultValue []
    let requests = reviewRequests bl docs parsed

    let reviews =
        match a.Reviews with
        | None -> Ok Map.empty
        | Some path -> validateReviews requests (Io.readReviews path)

    match carriage @ source, reviews with
    | (_ :: _ as errors), _
    | [], Error errors ->
        errors |> List.iter (eprintfn "error: %s")
        1
    | [], Ok reviewMap ->
        let reqDir = Path.Combine(a.Out, "requirements")
        let presDir = Path.Combine(a.Out, "requirements-preservation")
        Io.clearMarkdown reqDir

        let docDigests =
            rendered
            |> List.map (fun (d, md) -> d.FileName, Io.writeText (Path.Combine(reqDir, d.FileName)) md)
            |> List.sortWith (fun (x, _) (y, _) -> String.CompareOrdinal(x, y))

        let manifest = docDigests |> List.map (fun (f, h) -> $"{h}  {f}\n") |> String.concat ""
        let ds = dispositions bl docs reviewMap
        let dispositionSha = Io.writeText (Path.Combine(presDir, "disposition.json")) (Io.jsonText (dispositionJson bl a.Package ds))
        let requestsSha = Io.writeText (Path.Combine(presDir, "review-requests.json")) (Io.jsonText (reviewRequestsJson bl requests))
        let obligations = bl.Requirements |> List.sumBy (fun r -> r.Obligations.Length)

        let record =
            obj
                [ "schema", s "indy-init.r5-derivation-record/1"
                  "task", s "T07"
                  "tool", s "tools/derive-requirements (F#, identity derivation)"
                  "baseline", baselineNode bl
                  "inputs",
                  obj
                      ([ "normalizedRequirementsSha256", s (Io.sha256File a.Normalized) ]
                       @ (match a.Reviews with
                          | Some p ->
                              [ "reviews",
                                obj
                                    [ "ref", s (a.ReviewsRef |> Option.defaultValue (nonNull (Path.GetFileName p)))
                                      "sha256", s (Io.sha256File p) ] ]
                          | None -> []))
                  "outputs",
                  obj
                      [ "derivedDocuments", n docDigests.Length
                        "derivedDocumentsSha256", s (Io.sha256Text manifest)
                        "derivedDocumentsDigestMethod",
                        s "sha256 of the lines '<sha256>  <file name>\\n' for every research/requirements/*.md, ordinal by file name"
                        "dispositionSha256", s dispositionSha
                        "reviewRequestsSha256", s requestsSha ]
                  "counts",
                  obj
                      [ "requirements", n bl.Requirements.Length
                        "p0", n (bl.Requirements |> List.filter (fun r -> r.Priority = "P0") |> List.length)
                        "obligations", n obligations
                        "preserved", n ds.Length
                        "exclusions", n 0
                        "reviewsRequested", n requests.Length
                        "reviewsRequestedByteIdentical", n (requests |> List.filter (fun q -> q.ByteIdentical) |> List.length)
                        "reviewsApplied", n reviewMap.Count
                        "approvalsWritten", n 0 ]
                  "sourceTextChecked", b a.Source.IsSome ]

        Io.writeText (Path.Combine(presDir, "derivation-record.json")) (Io.jsonText record) |> ignore

        printfn
            "derived %d documents, %d obligations; %d review requests (%d byte-identical); %d reviews applied"
            docDigests.Length
            obligations
            requests.Length
            (requests |> List.filter (fun q -> q.ByteIdentical) |> List.length)
            reviewMap.Count

        0

[<EntryPoint>]
let main argv =
    match parse (List.ofArray argv) Map.empty |> Result.bind toArgs with
    | Ok a -> run a
    | Error message ->
        eprintfn "%s" message
        2
