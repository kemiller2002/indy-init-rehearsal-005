/// Small, total helpers over System.Text.Json.Nodes, and the one writer every output of this tool uses.
module Causa.Acceptance.Json

open System
open System.IO
open System.Security.Cryptography
open System.Text
open System.Text.Encodings.Web
open System.Text.Json
open System.Text.Json.Nodes

let sha256Hex (bytes: byte[]) =
    SHA256.HashData bytes |> Convert.ToHexString |> fun s -> s.ToLowerInvariant()

let sha256File (path: string) = File.ReadAllBytes path |> sha256Hex

let parse (text: string) : JsonNode =
    JsonNode.Parse(text, documentOptions = JsonDocumentOptions(CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true))

let load (path: string) = File.ReadAllText path |> parse

let tryLoad (path: string) =
    if File.Exists path then
        try Some(load path) with _ -> None
    else None

let field (name: string) (node: JsonNode) : JsonNode option =
    match node with
    | :? JsonObject as o ->
        let mutable v: JsonNode = null
        if o.TryGetPropertyValue(name, &v) && not (isNull v) then Some v else None
    | _ -> None

let str (name: string) (node: JsonNode) : string option =
    field name node
    |> Option.bind (fun v ->
        match v with
        | :? JsonValue as jv ->
            match jv.TryGetValue<string>() with
            | true, s -> Some s
            | _ -> None
        | _ -> None)

let strOr fallback name node = str name node |> Option.defaultValue fallback

let items (name: string) (node: JsonNode) : JsonNode list =
    match field name node with
    | Some(:? JsonArray as a) -> a |> Seq.filter (isNull >> not) |> List.ofSeq
    | _ -> []

let strings name node =
    items name node
    |> List.choose (fun n ->
        match n with
        | :? JsonValue as v ->
            match v.TryGetValue<string>() with
            | true, s -> Some s
            | _ -> None
        | _ -> None)

let props (node: JsonNode) : (string * JsonNode) list =
    match node with
    | :? JsonObject as o -> o |> Seq.map (fun kv -> kv.Key, kv.Value) |> List.ofSeq
    | _ -> []

// Builders. Values are JsonNode so records can be composed without ceremony.
let jstr (s: string) : JsonNode = JsonValue.Create s
let jint (i: int) : JsonNode = JsonValue.Create i
let jbool (b: bool) : JsonNode = JsonValue.Create b
let jnull : JsonNode = null

let jobj (pairs: (string * JsonNode) list) : JsonNode =
    let o = JsonObject()
    for k, v in pairs do
        o[k] <- (if isNull v then null else v.DeepClone())
    o :> JsonNode

let jarr (nodes: JsonNode seq) : JsonNode =
    let a = JsonArray()
    for n in nodes do
        a.Add(if isNull n then null else n.DeepClone())
    a :> JsonNode

let jstrs (xs: string seq) = xs |> Seq.map jstr |> jarr

let private options =
    JsonSerializerOptions(WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping)

/// UTF-8, two-space indent, LF line endings, trailing newline. Deterministic for a given node.
let serialize (node: JsonNode) =
    node.ToJsonString(options).Replace("\r\n", "\n") + "\n"

let bytesOf (node: JsonNode) = Encoding.UTF8.GetBytes(serialize node)

let write (path: string) (node: JsonNode) =
    let dir = Path.GetDirectoryName(Path.GetFullPath path)
    Directory.CreateDirectory dir |> ignore
    let bytes = bytesOf node
    File.WriteAllBytes(path, bytes)
    sha256Hex bytes
