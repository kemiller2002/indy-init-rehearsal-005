/// View keys for the investigation catalog, the detail inspector, next steps and record links (stream ux). Pure:
/// everything comes from the port's projection (catalog cards) or the engine's typed projection (Records) and is
/// worded by Causa.App.Language. View.project appends these keys.
module Causa.App.UxView

open Limen.Contract.Core
open Causa.App.Routes
open Causa.App.Adapter

let private text (s: string) = ViewValue.Text s
let private flag (b: bool) = ViewValue.Flag b
let private items (xs: ViewItem list) = ViewValue.Items xs

let private itemF (fields: (string * string) list) (flags: (string * bool) list) : ViewItem =
    (fields |> List.map (fun (k, v) -> k, ViewPrimitive.Text v)) @ (flags |> List.map (fun (k, v) -> k, ViewPrimitive.Flag v)) |> Map.ofList

// ---- catalog (UI-001, UI-022) ----

let sortOptions = [ "activity", "Latest activity first"; "title", "Title (A to Z)"; "status", "Status" ]

let private lifecycleOrder =
    [ "draft"; "active"; "awaitingEvidence"; "awaitingVerification"; "resolved"; "closed"; "archived" ]

/// The cards in the chosen order. Unknown activity sorts last; ties keep the catalog's order.
let sortCards (sort: string) (cards: CardProjection list) =
    match sort with
    | "title" -> cards |> List.sortBy (fun c -> c.Title.ToLowerInvariant())
    | "status" -> cards |> List.sortBy (fun c -> match List.tryFindIndex ((=) c.Lifecycle) lifecycleOrder with Some i -> i | None -> 99)
    | _ ->
        // Activity text is "<what>, yyyy-MM-dd HH:mm UTC": order by the timestamp at its end, newest first.
        let stampOf (c: CardProjection) =
            c.LastActivity |> Option.map (fun a -> match a.LastIndexOf ", " with -1 -> a | i -> a.Substring(i + 2)) |> Option.defaultValue ""
        cards |> List.sortByDescending stampOf

let cardItems (sort: string) (selected: string) (cards: CardProjection list) : ViewItem list =
    sortCards sort cards
    |> List.map (fun c ->
        itemF
            [ "id", c.Id
              "title", c.Title
              "status", Presentation.label c.Lifecycle
              "tone", Presentation.tone c.Lifecycle
              "kind", (if c.Writable then "Your working copy" else "Read-only demonstration")
              "activity", (c.LastActivity |> Option.map (fun a -> "Latest activity: " + a) |> Option.defaultValue "Latest activity: not recorded")
              "questions", Presentation.count c.OpenQuestionCount "open questions"
              "hypotheses", Presentation.count c.Hypotheses "hypotheses"
              "evidence", Presentation.count c.Evidence "evidence items"
              "contradictions", Presentation.count c.Contradictions "unresolved contradictions"
              "next", (c.NextAction |> Option.map (fun a -> "Next step allowed now: " + a) |> Option.defaultValue "")
              "openLabel", $"Open investigation: {c.Title}"
              "href", Routes.format (Investigation(c.Id, Overview))
              "reference", "Reference " + c.Id
              "selected", (if selected = c.Id then "true" else "false") ]
            [ "hasNext", c.NextAction.IsSome ])

// ---- inspector, links and next steps (UI-032..UI-034, UI-005) ----

/// "from-<id>" is the selection a closed inspector leaves behind, so focus returns to the record's row.
let returning (selected: string) = if selected.StartsWith "from-" then selected.Substring 5 else ""

let isSelected (selected: string) (id: string) = id <> "" && (selected = id || returning selected = id)

/// A row's selection state: "open" while its inspector is open, "true" when the inspector was just closed (the
/// kernel then moves focus back to the row), "false" otherwise.
let selectionState (selected: string) (id: string) =
    if id = "" then "false" elif returning selected = id then "true" elif selected = id then "open" else "false"

let keys (route: Route) (selected: string) (inv: InvestigationProjection option) : (string * ViewValue) list =
    let src = inv |> Option.bind (fun i -> i.Typed)
    let target = src |> Option.bind (fun s -> Records.resolve s selected)
    let inspected = Option.map2 Records.inspect src target |> Option.flatten
    let section = match route with Investigation(_, s) -> s | _ -> Overview
    let invId = inv |> Option.map (fun i -> i.Id) |> Option.defaultValue ""

    let fields =
        inspected
        |> Option.map (fun i -> i.Fields |> List.mapi (fun k f -> itemF [ "id", string k; "label", f.Label; "value", f.Value ] [ "missing", f.Missing ]))
        |> Option.defaultValue []

    let links =
        inspected
        |> Option.map (fun i ->
            i.Relations
            |> List.mapi (fun k r ->
                match r.Links with
                | [] -> [ itemF [ "id", $"{k}-none"; "group", r.Name; "text", r.Absent; "href", ""; "relation", "" ] [ "link", false; "absent", true ] ]
                | ls -> ls |> List.mapi (fun j l -> itemF [ "id", $"{k}-{j}"; "group", r.Name; "text", l.Text; "href", l.Href; "relation", l.Relation ] [ "link", true; "absent", false ]))
            |> List.concat)
        |> Option.defaultValue []

    let explanation =
        inspected |> Option.map (fun i -> i.Explanation |> List.mapi (fun k s -> itemF [ "id", string k; "text", s ] [])) |> Option.defaultValue []

    let steps = src |> Option.map Records.nextSteps

    let stepItems =
        steps
        |> Option.defaultValue []
        |> List.mapi (fun k s ->
            let actionText, allowed =
                match s.Action with
                | Some(label, true, _) -> $"Allowed now: {label}.", true
                | Some(label, false, reason) -> $"{label} is not available now: {reason}", false
                | None -> "", false
            itemF [ "id", string k; "text", s.Text; "why", s.Why; "href", s.Href; "action", actionText ] [ "hasAction", actionText <> ""; "allowed", allowed ])

    let corrections =
        src
        |> Option.map (fun s ->
            s.Projection.State.CorrectiveActions
            |> Map.toList
            |> List.map snd
            |> List.sortBy (fun c -> c.Provenance.RecordedAt, c.Id.Text)
            |> List.map (fun c ->
                let r: Records.Ref = { Kind = Records.CorrectionRecord; Id = c.Id.Text }
                let inv = s.Projection.State
                let criteria = inv.Criteria |> Map.toList |> List.map snd |> List.filter (fun k -> k.CorrectiveAction = c.Id)
                let met = criteria |> List.filter (IndyInitRootCauseInvestigator.Engine.Policy.criterionSatisfied inv) |> List.length
                itemF
                    [ "id", c.Id.Text
                      "text", Records.clean c.Description.Value
                      "kind", Language.correctiveKind c.Kind
                      "state", Language.correctiveState c.State
                      "verification", Language.correctionVerification (c.State = IndyInitRootCauseInvestigator.Engine.VerifiedEffective) met criteria.Length
                      "href", Records.href s.Projection.InvestigationId.Text r
                      "detailsLabel", "Details: " + Records.title s r
                      "selected", selectionState selected c.Id.Text ]
                    []))
        |> Option.defaultValue []

    let closeHref =
        match target with
        | Some r -> Routes.formatItem (Investigation(invId, section)) ("from-" + r.Id)
        | None -> Routes.format (Investigation(invId, section))

    [ "inspector.show", flag inspected.IsSome
      "inspector.kind", text (inspected |> Option.map (fun i -> i.KindLabel) |> Option.defaultValue "")
      "inspector.heading", text (inspected |> Option.map (fun i -> i.Heading) |> Option.defaultValue "")
      "inspector.state", text (inspected |> Option.map (fun i -> i.State) |> Option.defaultValue "")
      "inspector.selected", text (if inspected.IsSome then "true" else "false")
      "inspector.fields", items fields
      "inspector.hasUrl", flag (inspected |> Option.exists (fun i -> i.SafeUrl.IsSome))
      "inspector.url", text (inspected |> Option.bind (fun i -> i.SafeUrl) |> Option.defaultValue "")
      "inspector.links", items links
      "inspector.explained", flag (not explanation.IsEmpty)
      "inspector.explanation", items explanation
      "inspector.close", text closeHref
      // A selection that names no record of this investigation is said plainly (never an invented record).
      "inspector.unknown", flag (inv.IsSome && selected <> "" && returning selected = "" && target.IsNone && not (selected.StartsWith "blocker-") && (inv |> Option.forall (fun i -> not (i.Blockers |> List.exists (fun b -> b.Id = selected)))))
      "next.available", flag steps.IsSome
      "next.unavailable", flag (inv.IsSome && steps.IsNone)
      "next.steps", items stepItems
      "next.none", flag (steps |> Option.exists List.isEmpty)
      "ux.corrections", items corrections
      "ux.corrections.empty", flag (src.IsSome && corrections.IsEmpty) ]
