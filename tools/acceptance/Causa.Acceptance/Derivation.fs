/// AR-002-O2: an independent check of the derived requirements package, alongside (not replacing) the Praxis
/// preservation gate. The derivation is verbatim carriage: every baseline obligation must be carried, through the
/// disposition's mapping, as a derived criterion whose text is byte-identical to the obligation's text in the
/// pinned baseline. Any difference fails, including swapped numbers, ratios, orderings or roles that keep every
/// token, unless the disposition records an approved transformation for that obligation whose approval is in the
/// pinned approvals file. Every derived criterion must also come from a baseline obligation.
///
/// This parser is written independently of tools/derive-requirements (it shares no code with the derivation it
/// checks).
module Causa.Acceptance.Derivation

open System
open System.IO
open System.Text.RegularExpressions
open Causa.Acceptance.Json

type FindingKind =
    | Unmapped // a baseline obligation the disposition does not map
    | MissingCriterion // the mapping names a criterion no derived document carries
    | Differs // the criterion's text is not the obligation's text
    | Extra // a derived criterion with no baseline obligation behind it
    | UnapprovedTransformation // a recorded transformation whose approval is not in the pinned approvals file

let kindName =
    function
    | Unmapped -> "unmapped"
    | MissingCriterion -> "missing-criterion"
    | Differs -> "differs"
    | Extra -> "extra"
    | UnapprovedTransformation -> "unapproved-transformation"

type Finding =
    { Kind: FindingKind
      Obligation: string
      Criterion: string
      Expected: string
      Actual: string }

/// One criterion row: the cells of `| <id> | <text> |` with `\|` unescaped.
let parseRow (line: string) : (string * string) option =
    let t = line.Trim()
    if not (t.StartsWith "| ") || not (t.EndsWith " |") || t.StartsWith "| ID |" || t.StartsWith "|---" then None
    else
        let inner = t.Substring(2, t.Length - 4)
        let sep = inner.IndexOf " | "
        if sep <= 0 then None
        else
            let id = inner.Substring(0, sep)
            let text = inner.Substring(sep + 3).Replace("\\|", "|")
            if id.Contains "|" then None else Some(id, text)

/// A derived document: its front-matter id and its criterion rows.
let parseDocument (text: string) =
    let lines = text.Replace("\r\n", "\n").Split('\n')
    let id =
        lines
        |> Array.tryPick (fun l -> let m = Regex.Match(l, @"^id:\s*(\S+)\s*$") in if m.Success then Some m.Groups[1].Value else None)
        |> Option.defaultValue ""
    id, lines |> Array.choose parseRow |> List.ofArray

/// The first position where two texts differ, with a little context (for the finding).
let firstDifference (a: string) (b: string) =
    let n = min a.Length b.Length
    let i = Seq.init n id |> Seq.tryFind (fun i -> a[i] <> b[i]) |> Option.defaultValue n
    let ctx (s: string) = s.Substring(max 0 (i - 20), min s.Length (i + 40) - max 0 (i - 20))
    sprintf "at character %d: expected \"…%s…\", derived \"…%s…\"" i (ctx a) (ctx b)

type Inputs =
    { Baseline: Text.Json.Nodes.JsonNode
      /// (file name, contents) of every derived document.
      Documents: (string * string) list
      Disposition: Text.Json.Nodes.JsonNode
      Approvals: Text.Json.Nodes.JsonNode }

let check (i: Inputs) : Finding list =
    let obligations =
        items "requirements" i.Baseline
        |> List.collect (fun r -> items "obligations" r |> List.map (fun o -> strOr "" "id" o, strOr "" "text" o))
    let textOf = obligations |> Map.ofList
    let docs = i.Documents |> List.map (fun (_, t) -> parseDocument t)
    let criteria = docs |> List.collect (fun (docId, rows) -> rows |> List.map (fun (cid, text) -> sprintf "%s#%s" docId cid, text)) |> Map.ofList
    let approvalIds = items "approvals" i.Approvals |> List.choose (str "id") |> Set.ofList
    let mappings =
        items "dispositions" i.Disposition
        |> List.collect (fun d -> items "mappings" d)
        |> List.map (fun m -> strOr "" "obligation" m, m)
    let mapped = mappings |> List.map fst |> Set.ofList
    [ for oid, _ in obligations do
          if not (mapped.Contains oid) then
              yield { Kind = Unmapped; Obligation = oid; Criterion = ""; Expected = textOf[oid]; Actual = "" }
      for oid, m in mappings do
          let expected = textOf.TryFind oid |> Option.defaultValue ""
          let transformation = field "approvedTransformation" m
          for c in strings "criteria" m do
              match criteria.TryFind c with
              | None -> yield { Kind = MissingCriterion; Obligation = oid; Criterion = c; Expected = expected; Actual = "" }
              | Some actual when actual = expected -> ()
              | Some actual ->
                  match transformation with
                  | Some t when approvalIds.Contains(strOr "" "approval" t) && strOr "" "text" t = actual -> ()
                  | Some t ->
                      yield { Kind = UnapprovedTransformation; Obligation = oid; Criterion = c; Expected = expected; Actual = sprintf "%s (approval %s)" actual (strOr "" "approval" t) }
                  | None -> yield { Kind = Differs; Obligation = oid; Criterion = c; Expected = expected; Actual = actual }
      let claimed = mappings |> List.collect (fun (_, m) -> strings "criteria" m) |> Set.ofList
      for KeyValue(c, text) in criteria do
          if not (claimed.Contains c) then
              yield { Kind = Extra; Obligation = ""; Criterion = c; Expected = ""; Actual = text } ]

let toJson (i: Inputs) (findings: Finding list) (context: (string * Text.Json.Nodes.JsonNode) list) =
    let obligations = items "requirements" i.Baseline |> List.sumBy (fun r -> (items "obligations" r).Length)
    jobj (
        [ "schema", jstr "causa.derivation-check/1"
          "verdict", jstr (if findings.IsEmpty then "pass" else "fail")
          "rule", jstr "verbatim carriage: every baseline obligation's derived criterion is byte-identical to its text, unless an approved transformation is recorded"
          "summary",
          jobj
              [ "requirements", jint (items "requirements" i.Baseline).Length
                "obligations", jint obligations
                "documents", jint i.Documents.Length
                "findings", jint findings.Length ]
          "findings",
          jarr (
              findings
              |> List.map (fun f ->
                  jobj
                      [ "kind", jstr (kindName f.Kind)
                        "obligation", jstr f.Obligation
                        "criterion", jstr f.Criterion
                        "expected", jstr f.Expected
                        "actual", jstr f.Actual
                        "difference", jstr (if f.Kind = Differs then firstDifference f.Expected f.Actual else "") ])
          ) ]
        @ context
    )

let load (baseline: string) (derived: string) (disposition: string) (approvals: string) =
    { Baseline = Json.load baseline
      Documents =
        Directory.GetFiles(derived, "*.md")
        |> Array.sort
        |> Array.map (fun f -> Path.GetFileName f, File.ReadAllText f)
        |> List.ofArray
      Disposition = Json.load disposition
      Approvals = Json.load approvals }
