/// Pure rendering and re-parsing of the derived Praxis RQ markdown.
module DeriveRequirements.Markdown

open System
open DeriveRequirements.Domain

let private escapeCell (text: string) = text.Replace("|", "\\|")

let private table (header: string) (rows: Criterion list) =
    match rows with
    | [] -> []
    | _ ->
        [ $"| ID | {header} |"; "|---|---|" ]
        @ (rows |> List.map (fun c -> $"| {c.Id} | {escapeCell c.Text} |"))
        @ [ "" ]

/// Renders one derived document. Acceptance criteria go to a table whose second
/// header cell says "Acceptance", which is how Praxis classifies them.
let render (doc: DerivedDocument) : string =
    let acceptance, statements = doc.Criteria |> List.partition (fun c -> c.IsAcceptance)

    [ "---"
      $"id: {doc.Id}"
      "status: accepted"
      "version: 1.0.0"
      $"priority: {doc.Priority}"
      $"source: {doc.Source}"
      "---"
      ""
      $"# {doc.Id}"
      ""
      $"Derived from baseline requirement `{doc.Source}` ({doc.Title}). Each obligation is carried verbatim as the criterion that bears its id."
      "" ]
    @ table "Acceptance criterion" acceptance
    @ table "Statement" statements
    |> String.concat "\n"

/// Splits a table row on unescaped pipes and unescapes "\|" inside cells.
let private cells (row: string) =
    let inner = row.Trim().TrimStart('|')
    let inner = if inner.EndsWith("|") && not (inner.EndsWith("\\|")) then inner.Substring(0, inner.Length - 1) else inner

    let rec go (i: int) (current: string) (acc: string list) =
        if i >= inner.Length then
            List.rev (current :: acc)
        elif inner.[i] = '\\' && i + 1 < inner.Length && inner.[i + 1] = '|' then
            go (i + 2) (current + "|") acc
        elif inner.[i] = '|' then
            go (i + 1) "" (current :: acc)
        else
            go (i + 1) (current + string inner.[i]) acc

    go 0 "" [] |> List.map (fun c -> c.Trim())

/// Re-reads the criteria tables of a rendered document independently of the
/// renderer: (criterion id, text, is-acceptance) per row.
let parseCriteria (markdown: string) : (string * string * bool) list =
    let lines = markdown.Split('\n') |> Array.toList

    let rec go (lines: string list) (inTable: bool option) acc =
        match lines with
        | [] -> List.rev acc
        | line :: rest when line.StartsWith("|") ->
            match inTable with
            | None ->
                let header = cells line

                match header with
                | "ID" :: second :: _ ->
                    let isAcceptance = second.Contains("acceptance", StringComparison.OrdinalIgnoreCase)
                    // skip the delimiter row
                    go (List.tail rest) (Some isAcceptance) acc
                | _ -> go rest None acc
            | Some isAcceptance ->
                match cells line with
                | id :: textCells -> go rest inTable ((id, String.Join(" | ", textCells), isAcceptance) :: acc)
                | [] -> go rest inTable acc
        | _ :: rest -> go rest None acc

    go lines None []
