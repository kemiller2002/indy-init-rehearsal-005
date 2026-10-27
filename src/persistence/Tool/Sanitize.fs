/// Allow-list sanitizer for provider observations (PF-013-O1/O3, AR-014-O2). Nothing leaves this module unless it
/// is on an explicit allow-list: status codes, allow-listed header names and values, the JSON *shape* of bodies
/// (keys and types, never values), GraphQL error `type` enums, and commit ids.
module Causa.Persistence.Tool.Sanitize

open System
open Causa.Persistence

let allowedHeaders =
    set
        [ "cache-control"; "etag"; "last-modified"; "vary"; "content-type"; "retry-after"
          "x-ratelimit-limit"; "x-ratelimit-remaining"; "x-ratelimit-used"; "x-ratelimit-resource"
          "x-accepted-github-permissions"; "x-accepted-oauth-scopes"; "x-oauth-scopes"; "x-github-media-type"
          "x-github-api-version-selected" ]

/// Keys and types only. Arrays are described by length class and the shape of their first element.
let rec shape (v: JValue) : JValue =
    match v with
    | JNull -> JStr "null"
    | JBool _ -> JStr "boolean"
    | JInt _ -> JStr "integer"
    | JStr _ -> JStr "string"
    | JArr [] -> JArr []
    | JArr(x :: _) -> JArr [ shape x ]
    | JObj o -> JObj(o |> List.map (fun (k, x) -> k, shape x))

let bodyShape (body: string) =
    match Json.parse body with
    | Ok v -> shape v
    | Error _ -> JStr(if String.IsNullOrEmpty body then "empty" else "non-json")

let headers (r: HttpResponse) =
    r.Headers
    |> List.filter (fun (k, _) -> allowedHeaders.Contains k)
    |> List.groupBy fst
    |> List.map (fun (k, vs) -> k, JStr(String.Join(", ", vs |> List.map snd)))
    |> JObj

let graphqlErrorTypes (body: string) =
    match Json.parse body with
    | Ok(JObj o) ->
        match Json.field "errors" o with
        | Some(JArr es) ->
            es
            |> List.choose (fun e ->
                match e with
                | JObj eo -> Json.str "type" eo |> Option.map JStr
                | _ -> None)
        | _ -> []
    | _ -> []

/// The last line of defence: abort before printing anything that contains a secret.
let guard (secrets: string list) (line: string) =
    let leaked = secrets |> List.exists (fun s -> s.Length >= 4 && line.Contains(s, StringComparison.Ordinal))
    if leaked then
        eprintfn "ABORT: an observation line contained a secret value; nothing further is printed."
        exit 3
    line
