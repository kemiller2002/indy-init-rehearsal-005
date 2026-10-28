/// G5: a Percepta screen contract may count as NOT-APPLICABLE only by an owner decision. Every `notApplicable` entry of
/// .percepta/bindings.json must cite a research/decisions/DF-… record that exists in the repository and is accepted;
/// only then may `percepta verify-app --allow-not-applicable` run.
module Causa.Acceptance.Bindings

open System
open System.Text.RegularExpressions
open Causa.Acceptance.Json

type Problem = { Contract: string; Reason: string }

let private citation = Regex(@"research/decisions/(DF-[A-Z0-9-]+?)--[A-Za-z0-9.\-]+\.md")

/// `read` returns a repository file's text, or None when it does not exist.
let check (bindings: Text.Json.Nodes.JsonNode) (read: string -> string option) : int * Problem list =
    let entries =
        items "screens" bindings
        |> List.choose (fun s ->
            match field "notApplicable" s with
            | Some n when not (isNull n) -> Some(strOr "" "contract" s, (try n.GetValue<string>() with _ -> ""))
            | _ -> None)
    let problems =
        [ for contract, reason in entries do
              let m = citation.Match reason
              if not m.Success then
                  yield { Contract = contract; Reason = "cites no research/decisions/DF-… record" }
              else
                  let path = m.Value
                  let id = m.Groups[1].Value
                  match read path with
                  | None -> yield { Contract = contract; Reason = sprintf "%s does not exist" path }
                  | Some text ->
                      if not (Regex.IsMatch(text, sprintf @"(?m)^id:\s*%s\s*$" (Regex.Escape id))) then
                          yield { Contract = contract; Reason = sprintf "%s is not decision %s" path id }
                      if not (Regex.IsMatch(text, @"(?m)^status:\s*accepted\s*$")) then
                          yield { Contract = contract; Reason = sprintf "%s is not accepted" path } ]
    entries.Length, problems
