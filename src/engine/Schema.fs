namespace IndyInitRootCauseInvestigator.Engine

open System
open System.Collections.Generic
open System.Text
open System.Text.RegularExpressions

/// A JSON Schema (Draft 2020-12) evaluator for exactly the keywords the Causa v1.0.0 schemas use, over the normative
/// schema files embedded in this assembly. Calendar validity of `timestamp` values is applied as part of that
/// definition (contract §2.9: a stage-7 rule the pattern cannot express).
[<RequireQualifiedAccess>]
module Schema =
    let baseUri = "https://github.com/kemiller2002/causa-data/contract/v1.0.0/schemas/"
    let private resourcePrefix = "causa/v1.0.0/schemas/"
    let private timestampRef = baseUri + "common.schema.json#/$defs/timestamp"

    /// The embedded normative schema files: published path -> exact bytes.
    let files: Lazy<Map<string, byte[]>> =
        lazy
            (let assembly = typeof<Json>.Assembly
             assembly.GetManifestResourceNames()
             |> Array.filter (fun n -> n.StartsWith(resourcePrefix, StringComparison.Ordinal))
             |> Array.map (fun n ->
                 use stream =
                     match assembly.GetManifestResourceStream(n) with
                     | null -> failwith $"missing embedded resource {n}"
                     | s -> s
                 use memory = new IO.MemoryStream()
                 stream.CopyTo memory
                 n.Substring resourcePrefix.Length, memory.ToArray())
             |> Map.ofArray)

    let private documents: Lazy<Map<string, Json>> =
        lazy
            (files.Value
             |> Map.toList
             |> List.filter (fun (path, _) -> path.EndsWith(".schema.json", StringComparison.Ordinal))
             |> List.map (fun (path, bytes) ->
                 match Json.parse (Encoding.UTF8.GetString bytes) with
                 | Ok doc -> baseUri + path, doc
                 | Error e -> failwith $"embedded schema {path} is not JSON: {e}")
             |> Map.ofList)

    let private regexes = Dictionary<string, Regex>()

    /// ECMA-262 `$` matches only at the very end; .NET `$` also before a final LF (contract §2.13).
    let private regex (pattern: string) =
        match regexes.TryGetValue pattern with
        | true, r -> r
        | _ ->
            let translated =
                if pattern.EndsWith("$", StringComparison.Ordinal) && not (pattern.EndsWith("\\$", StringComparison.Ordinal)) then
                    pattern.Substring(0, pattern.Length - 1) + "\\z"
                else
                    pattern
            let r = Regex(translated, RegexOptions.CultureInvariant)
            regexes[pattern] <- r
            r

    let private pointer (doc: Json) (fragment: string) =
        if String.IsNullOrEmpty fragment || fragment = "/" then Some doc
        else
            fragment.TrimStart('/').Split('/')
            |> Array.fold
                (fun node token ->
                    let token = token.Replace("~1", "/").Replace("~0", "~")
                    node
                    |> Option.bind (fun n ->
                        match n with
                        | JObject _ -> Json.field token n
                        | JArray items ->
                            match Int32.TryParse token with
                            | true, k when k >= 0 && k < items.Length -> Some items[k]
                            | _ -> None
                        | _ -> None))
                (Some doc)

    /// Resolve a $ref against the document it appears in: (absolute document uri, fragment, schema).
    let private resolve (docUri: string) (reference: string) =
        let absolute = Uri(Uri(docUri), reference).ToString()
        let hash = absolute.IndexOf('#')
        let doc, fragment = if hash < 0 then absolute, "" else absolute.Substring(0, hash), absolute.Substring(hash + 1)
        match documents.Value |> Map.tryFind doc with
        | None -> failwith $"unresolvable schema reference {reference} from {docUri}"
        | Some d ->
            match pointer d fragment with
            | Some s -> doc, absolute, s
            | None -> failwith $"unresolvable schema pointer {reference} from {docUri}"

    let private codePoints (s: string) =
        let mutable count = 0
        let mutable k = 0
        while k < s.Length do
            if Char.IsHighSurrogate s[k] && k + 1 < s.Length && Char.IsLowSurrogate s[k + 1] then k <- k + 2 else k <- k + 1
            count <- count + 1
        count

    /// A real proleptic-Gregorian instant (pattern already checked): no 02-30, no leap second.
    let isCalendarValid (s: string) =
        match DateTime.TryParseExact(s, "yyyy-MM-dd'T'HH:mm:ss.fff'Z'", Globalization.CultureInfo.InvariantCulture, Globalization.DateTimeStyles.AdjustToUniversal ||| Globalization.DateTimeStyles.AssumeUniversal) with
        | true, _ -> true
        | _ -> false

    let private typeMatches (name: string) (v: Json) =
        match name, v with
        | "object", JObject _
        | "array", JArray _
        | "string", JString _
        | "integer", JInt _
        | "number", JInt _
        | "boolean", JBool _
        | "null", JNull -> true
        | _ -> false

    /// Whether `instance` satisfies `schema` (which lives in document `docUri`).
    let rec private valid (docUri: string) (schema: Json) (instance: Json) : bool =
        match schema with
        | JBool b -> b
        | JObject keywords ->
            keywords
            |> List.forall (fun (keyword, arg) ->
                match keyword, arg with
                | "$ref", JString reference ->
                    let doc, absolute, target = resolve docUri reference
                    valid doc target instance
                    && (absolute <> timestampRef
                        || (match instance with
                            | JString s -> isCalendarValid s
                            | _ -> true))
                | "type", JString t -> typeMatches t instance
                | "type", JArray ts -> ts |> List.exists (fun t -> match t with JString s -> typeMatches s instance | _ -> false)
                | "const", c -> Json.equal c instance
                | "enum", JArray options -> options |> List.exists (fun o -> Json.equal o instance)
                | "pattern", JString p ->
                    match instance with
                    | JString s -> (regex p).IsMatch s
                    | _ -> true
                | "minLength", JInt m ->
                    match instance with
                    | JString s -> int64 (codePoints s) >= m
                    | _ -> true
                | "maxLength", JInt m ->
                    match instance with
                    | JString s -> int64 (codePoints s) <= m
                    | _ -> true
                | "minimum", JInt m ->
                    match instance with
                    | JInt i -> i >= m
                    | _ -> true
                | "maximum", JInt m ->
                    match instance with
                    | JInt i -> i <= m
                    | _ -> true
                | "minItems", JInt m ->
                    match instance with
                    | JArray items -> int64 items.Length >= m
                    | _ -> true
                | "maxItems", JInt m ->
                    match instance with
                    | JArray items -> int64 items.Length <= m
                    | _ -> true
                | "uniqueItems", JBool true ->
                    match instance with
                    | JArray items ->
                        let rec distinct xs =
                            match xs with
                            | [] -> true
                            | x :: rest -> not (rest |> List.exists (Json.equal x)) && distinct rest
                        distinct items
                    | _ -> true
                | "items", itemSchema ->
                    match instance with
                    | JArray items -> items |> List.forall (valid docUri itemSchema)
                    | _ -> true
                | "required", JArray names ->
                    match instance with
                    | JObject members ->
                        names
                        |> List.forall (fun n ->
                            match n with
                            | JString name -> members |> List.exists (fun (k, _) -> k = name)
                            | _ -> false)
                    | _ -> true
                | "properties", JObject props ->
                    match instance with
                    | JObject members ->
                        members
                        |> List.forall (fun (k, v) ->
                            match props |> List.tryFind (fun (p, _) -> p = k) with
                            | Some(_, s) -> valid docUri s v
                            | None -> true)
                    | _ -> true
                | "additionalProperties", extra ->
                    match instance with
                    | JObject members ->
                        let declared =
                            match Json.field "properties" schema with
                            | Some(JObject props) -> props |> List.map fst |> Set.ofList
                            | _ -> Set.empty
                        members |> List.forall (fun (k, v) -> declared.Contains k || valid docUri extra v)
                    | _ -> true
                | "allOf", JArray schemas -> schemas |> List.forall (fun s -> valid docUri s instance)
                | "anyOf", JArray schemas -> schemas |> List.exists (fun s -> valid docUri s instance)
                | "oneOf", JArray schemas -> (schemas |> List.filter (fun s -> valid docUri s instance) |> List.length) = 1
                | "not", s -> not (valid docUri s instance)
                | "if", condition ->
                    if valid docUri condition instance then
                        match Json.field "then" schema with
                        | Some t -> valid docUri t instance
                        | None -> true
                    else
                        match Json.field "else" schema with
                        | Some e -> valid docUri e instance
                        | None -> true
                | ("then" | "else" | "$schema" | "$id" | "$defs" | "title" | "description" | "$comment" | "examples"), _ -> true
                | other, _ -> failwith $"unsupported schema keyword {other}")
        | _ -> failwith "a schema must be an object or a boolean"

    /// Validate an instance against a top-level schema file (e.g. "event-envelope.schema.json").
    let validate (schemaPath: string) (instance: Json) : bool =
        let uri = baseUri + schemaPath
        match documents.Value |> Map.tryFind uri with
        | Some doc -> valid uri doc instance
        | None -> failwith $"no embedded schema {schemaPath}"
