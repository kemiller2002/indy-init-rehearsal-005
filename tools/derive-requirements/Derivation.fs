/// The identity derivation: pure functions from the baseline to the derived
/// package, the disposition and the review requests.
module DeriveRequirements.Derivation

open System
open DeriveRequirements.Domain

let private whitespace = [| ' '; '\t'; '\n'; '\r' |]

let wordCount (text: string) =
    text.Split(whitespace, StringSplitOptions.RemoveEmptyEntries).Length

/// Praxis decides a token-less obligation by verbatim carriage only when it is
/// at least five words long; anything shorter needs a person's review.
let needsReview (o: Obligation) = List.isEmpty o.Tokens && wordCount o.Text < 5

/// Praxis RQ priority vocabulary. A P0 source requirement is carried as critical
/// and P1 as high, so the gate never sees a source priority lowered; a source
/// without a stated priority is carried as medium.
let praxisPriority (sourcePriority: string) =
    match sourcePriority with
    | "P0" -> "critical"
    | "P1" -> "high"
    | _ -> "medium"

/// Lower-case ASCII slug of a baseline requirement id for the file name, at most
/// 60 characters; every other character becomes a single hyphen.
let fileSlug (sourceId: string) =
    let mapped =
        sourceId.ToLowerInvariant()
        |> String.map (fun c -> if (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') then c else '-')

    let collapsed = Text.RegularExpressions.Regex.Replace(mapped, "-{2,}", "-").Trim('-')
    if collapsed.Length <= 60 then collapsed else collapsed.Substring(0, 60).TrimEnd('-')

/// The Praxis RQ identifier of the requirement at ordinal position `ordinal`
/// (0-based) of the baseline sorted by id.
let rqIdOf (ordinal: int) = sprintf "RQ-CAUSA-2026-%04d" (ordinal + 1)

let deriveDocument (ordinal: int) (r: Requirement) : DerivedDocument =
    let id = rqIdOf ordinal

    { Id = id
      Source = r.Id
      Title = r.Title
      Priority = praxisPriority r.Priority
      FileName = $"{id}--{fileSlug r.Id}.md"
      Criteria =
        r.Obligations
        |> List.map (fun o ->
            { Id = o.Id
              Text = o.Text
              IsAcceptance = (o.Kind = Acceptance) }) }

/// Derived documents keyed by baseline requirement id, numbered in ordinal
/// baseline-id order.
let deriveDocuments (baseline: Baseline) : Map<string, DerivedDocument> =
    baseline.Requirements
    |> List.sortWith (fun a b -> String.CompareOrdinal(a.Id, b.Id))
    |> List.mapi (fun i r -> r.Id, deriveDocument i r)
    |> Map.ofList

let criterionRef (doc: DerivedDocument) (obligationId: string) = $"{doc.Id}#{obligationId}"

/// Every obligation's text as re-parsed from the rendered markdown, keyed by
/// criterion reference. Built from the rendered bytes, not from the model.
let parsedCriteria (rendered: (DerivedDocument * string) list) : Map<string, string * bool> =
    rendered
    |> List.collect (fun (doc, markdown) ->
        Markdown.parseCriteria markdown
        |> List.map (fun (id, text, isAcceptance) -> criterionRef doc id, (text, isAcceptance)))
    |> Map.ofList

let reviewRequests
    (baseline: Baseline)
    (docs: Map<string, DerivedDocument>)
    (parsed: Map<string, string * bool>)
    : ReviewRequest list =
    baseline.Requirements
    |> List.collect (fun r ->
        let doc = docs.[r.Id]

        r.Obligations
        |> List.filter needsReview
        |> List.map (fun o ->
            let reference = criterionRef doc o.Id
            let criterionText = parsed |> Map.tryFind reference |> Option.map fst |> Option.defaultValue ""

            { Requirement = r.Id
              Obligation = o.Id
              Criterion = reference
              Text = o.Text
              CriterionText = criterionText
              ByteIdentical = String.Equals(criterionText, o.Text, StringComparison.Ordinal) }))

/// Checks that an operator review record covers only review-requested,
/// byte-identical obligations. Returns the obligation -> review map, or every
/// reason the record cannot be applied.
let validateReviews (requests: ReviewRequest list) (record: ReviewRecord) : Result<Map<string, Review>, string list> =
    let byObligation = requests |> List.map (fun q -> q.Obligation, q) |> Map.ofList

    let duplicates =
        record.Obligations
        |> List.countBy id
        |> List.filter (fun (_, n) -> n > 1)
        |> List.map (fun (o, _) -> $"review lists {o} more than once")

    let problems =
        record.Obligations
        |> List.distinct
        |> List.choose (fun o ->
            match Map.tryFind o byObligation with
            | None -> Some $"review covers {o}, which is not a review request"
            | Some q when not q.ByteIdentical -> Some $"review covers {o}, whose criterion is not byte-identical to the obligation"
            | Some _ -> None)

    let incomplete =
        [ if String.IsNullOrWhiteSpace record.Reviewer then "review record has no reviewer"
          if String.IsNullOrWhiteSpace record.Basis then "review record has no basis"
          match DateOnly.TryParseExact(record.Date, "yyyy-MM-dd") with
          | true, _ -> ()
          | _ -> "review record date is not yyyy-MM-dd" ]

    match duplicates @ problems @ incomplete with
    | [] ->
        let review = { Reviewer = record.Reviewer; Date = record.Date; Basis = record.Basis }
        Ok(record.Obligations |> List.map (fun o -> o, review) |> Map.ofList)
    | errors -> Error errors

let dispositions (baseline: Baseline) (docs: Map<string, DerivedDocument>) (reviews: Map<string, Review>) =
    baseline.Requirements
    |> List.map (fun r ->
        let doc = docs.[r.Id]

        { Requirement = r.Id
          Mappings =
            r.Obligations
            |> List.map (fun o ->
                { Obligation = o.Id
                  Criteria = [ criterionRef doc o.Id ]
                  Review = Map.tryFind o.Id reviews }) })

/// Every obligation must re-parse from the rendered markdown as exactly its
/// own text, in the right kind of table. Returns the violations.
let verifyCarriage (baseline: Baseline) (docs: Map<string, DerivedDocument>) (parsed: Map<string, string * bool>) =
    baseline.Requirements
    |> List.collect (fun r ->
        r.Obligations
        |> List.choose (fun o ->
            let reference = criterionRef docs.[r.Id] o.Id

            match Map.tryFind reference parsed with
            | None -> Some $"{reference}: criterion not found in rendered markdown"
            | Some(text, _) when not (String.Equals(text, o.Text, StringComparison.Ordinal)) ->
                Some $"{reference}: rendered criterion differs from the obligation text"
            | Some(_, isAcceptance) when isAcceptance <> (o.Kind = Acceptance) ->
                Some $"{reference}: criterion is in the wrong table for a {o.Kind} obligation"
            | Some _ -> None))
