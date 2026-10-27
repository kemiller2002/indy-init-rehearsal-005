/// The only module that touches the filesystem: JSON reading, hashing, writing.
module DeriveRequirements.Io

open System
open System.IO
open System.Security.Cryptography
open System.Text
open System.Text.Encodings.Web
open System.Text.Json
open System.Text.Json.Nodes
open DeriveRequirements.Domain

let sha256Bytes (bytes: byte[]) = Convert.ToHexString(SHA256.HashData bytes).ToLowerInvariant()
let sha256Text (text: string) = sha256Bytes (Encoding.UTF8.GetBytes text)
let sha256File (path: string) = sha256Bytes (File.ReadAllBytes path)

let private str (e: JsonElement) (name: string) = e.GetProperty(name).GetString() |> nonNull

let private optionalTokens (e: JsonElement) =
    match e.TryGetProperty "tokens" with
    | true, t when t.ValueKind = JsonValueKind.Array -> [ for x in t.EnumerateArray() -> x.GetString() |> nonNull ]
    | _ -> []

let private obligation (e: JsonElement) =
    match kindOfString (str e "kind") with
    | Ok kind ->
        { Id = str e "id"
          Kind = kind
          Text = str e "text"
          Tokens = optionalTokens e }
    | Error message -> failwith message

let readBaseline (path: string) : Baseline =
    let bytes = File.ReadAllBytes path
    use doc = JsonDocument.Parse(bytes)
    let root = doc.RootElement

    if str root "schema" <> "praxis.requirements-baseline/1" then
        failwith "baseline is not praxis.requirements-baseline/1"

    { Id = str root "id"
      Commit = str (root.GetProperty "source") "commit"
      Sha256 = sha256Bytes bytes
      Requirements =
        [ for r in root.GetProperty("requirements").EnumerateArray() ->
              { Id = str r "id"
                Title = str r "title"
                Priority = str r "priority"
                Document = str r "document"
                Obligations = [ for o in r.GetProperty("obligations").EnumerateArray() -> obligation o ] } ] }

let readNormalized (path: string) : Map<string, NormalizedEntry> =
    use doc = JsonDocument.Parse(File.ReadAllBytes path)

    [ for r in doc.RootElement.GetProperty("requirements").EnumerateArray() do
          if str r "origin" = "baseline" then
              let entry = { Id = str r "id"; Source = str r "source"; Title = str r "title" }
              entry.Source, entry ]
    |> Map.ofList

let readReviews (path: string) : ReviewRecord =
    use doc = JsonDocument.Parse(File.ReadAllBytes path)
    let root = doc.RootElement

    { Reviewer = str root "reviewer"
      Date = str root "date"
      Basis = str root "basis"
      Obligations = [ for o in root.GetProperty("obligations").EnumerateArray() -> o.GetString() |> nonNull ] }

let private options =
    JsonSerializerOptions(WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping)

let jsonText (node: JsonNode) = node.ToJsonString(options) + "\n"

/// Writes text as UTF-8 without BOM and LF line endings; returns its sha256.
let writeText (path: string) (text: string) =
    Directory.CreateDirectory(nonNull (Path.GetDirectoryName path)) |> ignore
    let bytes = UTF8Encoding(false).GetBytes(text)
    File.WriteAllBytes(path, bytes)
    sha256Bytes bytes

let clearMarkdown (dir: string) =
    if Directory.Exists dir then
        for f in Directory.GetFiles(dir, "*.md") do
            File.Delete f
