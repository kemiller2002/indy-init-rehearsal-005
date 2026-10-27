/// Causa's navigable views. The URL hash is the source of truth, so a reloaded or pasted deep link never 404s on
/// GitHub Pages. Investigations are addressed by their GUID only (never by a case key or title).
/// Primary destinations: Investigations, Administration, Settings (UI-020, UI-029-O1). Secondary destinations within
/// an investigation: Overview, Causal analysis, Evidence, Hypotheses, Timeline, Corrective actions, Report (UI-029-O2).
module Causa.App.Routes

open System

type Section =
    | Overview
    | CausalAnalysis
    | Evidence
    | Hypotheses
    | Timeline
    | CorrectiveActions
    | Report
    | AiProposals

type Route =
    | Home
    | Investigations
    | NewInvestigation
    | Investigation of id: string * section: Section
    | Administration
    | Settings
    | NotFound of path: string

/// Every section with its URL slug and its user-facing name, in navigation order.
let sections =
    [ Overview, "overview", "Overview"
      CausalAnalysis, "causal-analysis", "Causal analysis"
      Evidence, "evidence", "Evidence"
      Hypotheses, "hypotheses", "Hypotheses"
      Timeline, "timeline", "Timeline"
      CorrectiveActions, "corrective-actions", "Corrective actions"
      Report, "report", "Report"
      AiProposals, "proposals", "AI proposals" ]

let sectionSlug (s: Section) = sections |> List.pick (fun (x, slug, _) -> if x = s then Some slug else None)
let sectionLabel (s: Section) = sections |> List.pick (fun (x, _, label) -> if x = s then Some label else None)

/// A canonical (lower-case, hyphenated, non-nil) GUID.
let isInvestigationId (s: string) =
    match Guid.TryParseExact(s, "D") with
    | true, g -> g <> Guid.Empty && s = s.ToLowerInvariant()
    | _ -> false

let private pathOf (hash: string) =
    let fragment = if hash.StartsWith("#", StringComparison.Ordinal) then hash.Substring 1 else hash

    match fragment.IndexOf '?' with
    | -1 -> fragment
    | i -> fragment.Substring(0, i)

/// Pure and total: a location hash (with or without the leading '#') to its route. Unknown paths are a value.
let parse (hash: string) : Route =
    let path = pathOf hash

    match path.Split('/') |> Array.toList with
    | [ "" ]
    | [ ""; "" ] -> Home
    | [ ""; "investigations" ] -> Investigations
    | [ ""; "investigations"; "new" ] -> NewInvestigation
    | [ ""; "investigations"; id ] when isInvestigationId id -> Investigation(id, Overview)
    | [ ""; "investigations"; id; slug ] when isInvestigationId id && sections |> List.exists (fun (_, s, _) -> s = slug) ->
        Investigation(id, sections |> List.pick (fun (x, s, _) -> if s = slug then Some x else None))
    | [ ""; "administration" ] -> Administration
    | [ ""; "settings" ] -> Settings
    | _ when path.StartsWith("/", StringComparison.Ordinal) -> NotFound path
    | _ -> NotFound("/" + path)

/// Pure: the canonical hash of a route. The Overview is the investigation's own address.
let format (route: Route) : string =
    match route with
    | Home -> "#/"
    | Investigations -> "#/investigations"
    | NewInvestigation -> "#/investigations/new"
    | Investigation(id, Overview) -> "#/investigations/" + id
    | Investigation(id, section) -> "#/investigations/" + id + "/" + sectionSlug section
    | Administration -> "#/administration"
    | Settings -> "#/settings"
    | NotFound path -> "#" + path

/// The record a link points at within a section (UI-033): `?item=<id>`; ids are letters, digits and '-' only.
/// Anything else selects nothing. Pure and total.
let selection (hash: string) : string =
    let safe (v: string) = v.Length > 0 && v.Length <= 80 && v |> Seq.forall (fun c -> Char.IsAsciiLetterOrDigit c || c = '-')

    match hash.IndexOf '?' with
    | -1 -> ""
    | i ->
        hash.Substring(i + 1).Split('&')
        |> Array.tryPick (fun kv ->
            match kv.Split('=') with
            | [| "item"; v |] when safe v -> Some v
            | _ -> None)
        |> Option.defaultValue ""

/// A link to one record inside a section.
let formatItem (route: Route) (item: string) = format route + "?item=" + item

/// The primary destination a route belongs to (UI-029-O3: the current destination is indicated).
type Primary =
    | InvestigationsArea
    | AdministrationArea
    | SettingsArea
    | Nowhere

let primaryOf (route: Route) =
    match route with
    | Home
    | Investigations
    | NewInvestigation
    | Investigation _ -> InvestigationsArea
    | Administration -> AdministrationArea
    | Settings -> SettingsArea
    | NotFound _ -> Nowhere
