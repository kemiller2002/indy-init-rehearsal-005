namespace IndyInitRootCauseInvestigator.Engine

open System
open System.Text

/// A JSON value as the Causa contract admits it: integers only (no floats), object members in source order.
type Json =
    | JNull
    | JBool of bool
    | JInt of int64
    | JString of string
    | JArray of Json list
    | JObject of (string * Json) list

/// Strict JSON reading and RFC 8785 (JCS) canonical writing (contract §2.2–§2.3). Pure; no reflection.
[<RequireQualifiedAccess>]
module Json =
    let maxDepth = 128
    let private maxSafe = 9007199254740991L

    exception private Malformed of string

    /// Parse one JSON text. Errors are all `CAUSA.JSON_MALFORMED` class: syntax, duplicate keys, non-integer or
    /// out-of-range numbers, depth > 128, unpaired surrogates, U+FEFF anywhere (raw or escaped).
    let parse (text: string) : Result<Json, string> =
        let mutable i = 0
        let n = text.Length
        let bad message = raise (Malformed message)

        let skipWs () =
            while i < n && (text[i] = ' ' || text[i] = '\t' || text[i] = '\n' || text[i] = '\r') do
                i <- i + 1

        let expect (c: char) =
            if i < n && text[i] = c then i <- i + 1 else bad $"expected '{c}' at {i}"

        let literal (word: string) value =
            if i + word.Length <= n && String.CompareOrdinal(text, i, word, 0, word.Length) = 0 then
                i <- i + word.Length
                value
            else
                bad $"invalid literal at {i}"

        let parseString () =
            expect '"'
            let sb = StringBuilder()
            let mutable closed = false
            while not closed do
                if i >= n then bad "unterminated string"
                let c = text[i]
                if c = '"' then
                    i <- i + 1
                    closed <- true
                elif c = '\\' then
                    if i + 1 >= n then bad "unterminated escape"
                    let e = text[i + 1]
                    i <- i + 2
                    match e with
                    | '"' -> sb.Append('"') |> ignore
                    | '\\' -> sb.Append('\\') |> ignore
                    | '/' -> sb.Append('/') |> ignore
                    | 'b' -> sb.Append('\b') |> ignore
                    | 'f' -> sb.Append('\f') |> ignore
                    | 'n' -> sb.Append('\n') |> ignore
                    | 'r' -> sb.Append('\r') |> ignore
                    | 't' -> sb.Append('\t') |> ignore
                    | 'u' ->
                        if i + 4 > n then bad "short unicode escape"
                        let hex = text.Substring(i, 4)
                        if not (hex |> Seq.forall Uri.IsHexDigit) then bad "invalid unicode escape"
                        sb.Append(char (Convert.ToInt32(hex, 16))) |> ignore
                        i <- i + 4
                    | _ -> bad "invalid escape"
                elif c < ' ' then
                    bad "control character in string"
                else
                    sb.Append(c) |> ignore
                    i <- i + 1
            let s = sb.ToString()
            // Unpaired surrogates and U+FEFF are invalid anywhere (contract §2.3).
            let mutable k = 0
            while k < s.Length do
                let ch = s[k]
                if ch = '﻿' then bad "U+FEFF in string"
                if Char.IsHighSurrogate ch then
                    if k + 1 < s.Length && Char.IsLowSurrogate s[k + 1] then k <- k + 1 else bad "unpaired surrogate"
                elif Char.IsLowSurrogate ch then
                    bad "unpaired surrogate"
                k <- k + 1
            s

        let parseNumber () =
            let start = i
            if i < n && text[i] = '-' then i <- i + 1
            if i >= n || not (Char.IsAsciiDigit text[i]) then bad "invalid number"
            if text[i] = '0' then
                i <- i + 1
            else
                while i < n && Char.IsAsciiDigit text[i] do
                    i <- i + 1
            if i < n && (text[i] = '.' || text[i] = 'e' || text[i] = 'E') then bad "non-integer number"
            let digits = text.Substring(start, i - start)
            match Int64.TryParse(digits, Globalization.NumberStyles.AllowLeadingSign, Globalization.CultureInfo.InvariantCulture) with
            | true, v when v >= -maxSafe && v <= maxSafe -> JInt v
            | _ -> bad "integer out of range"

        let rec value depth =
            skipWs ()
            if i >= n then bad "unexpected end"
            match text[i] with
            | '{' ->
                if depth + 1 > maxDepth then bad "nesting too deep"
                i <- i + 1
                skipWs ()
                let members = ResizeArray<string * Json>()
                let seen = Collections.Generic.HashSet<string>(StringComparer.Ordinal)
                if i < n && text[i] = '}' then
                    i <- i + 1
                else
                    let mutable more = true
                    while more do
                        skipWs ()
                        let key = parseString ()
                        if not (seen.Add key) then bad "duplicate key"
                        skipWs ()
                        expect ':'
                        let v = value (depth + 1)
                        members.Add((key, v))
                        skipWs ()
                        if i < n && text[i] = ',' then i <- i + 1
                        else
                            expect '}'
                            more <- false
                JObject(List.ofSeq members)
            | '[' ->
                if depth + 1 > maxDepth then bad "nesting too deep"
                i <- i + 1
                skipWs ()
                let items = ResizeArray<Json>()
                if i < n && text[i] = ']' then
                    i <- i + 1
                else
                    let mutable more = true
                    while more do
                        items.Add(value (depth + 1))
                        skipWs ()
                        if i < n && text[i] = ',' then i <- i + 1
                        else
                            expect ']'
                            more <- false
                JArray(List.ofSeq items)
            | '"' -> JString(parseString ())
            | 't' -> literal "true" (JBool true)
            | 'f' -> literal "false" (JBool false)
            | 'n' -> literal "null" JNull
            | c when c = '-' || Char.IsAsciiDigit c -> parseNumber ()
            | _ -> bad $"unexpected character at {i}"

        try
            if text.IndexOf('﻿') >= 0 then bad "U+FEFF in text"
            let v = value 0
            skipWs ()
            if i <> n then bad "trailing content"
            Ok v
        with Malformed message ->
            Error message

    let private escape (sb: StringBuilder) (s: string) =
        sb.Append('"') |> ignore
        for c in s do
            match c with
            | '"' -> sb.Append("\\\"") |> ignore
            | '\\' -> sb.Append("\\\\") |> ignore
            | '\b' -> sb.Append("\\b") |> ignore
            | '\f' -> sb.Append("\\f") |> ignore
            | '\n' -> sb.Append("\\n") |> ignore
            | '\r' -> sb.Append("\\r") |> ignore
            | '\t' -> sb.Append("\\t") |> ignore
            | c when c < ' ' -> sb.Append("\\u").Append((int c).ToString("x4")) |> ignore
            | c -> sb.Append(c) |> ignore
        sb.Append('"') |> ignore

    /// RFC 8785 canonical text: keys sorted by UTF-16 code units, no whitespace, JCS string escaping.
    let canonical (value: Json) : string =
        let sb = StringBuilder()
        let rec write v =
            match v with
            | JNull -> sb.Append("null") |> ignore
            | JBool true -> sb.Append("true") |> ignore
            | JBool false -> sb.Append("false") |> ignore
            | JInt i -> sb.Append(i.ToString(Globalization.CultureInfo.InvariantCulture)) |> ignore
            | JString s -> escape sb s
            | JArray items ->
                sb.Append('[') |> ignore
                items
                |> List.iteri (fun k item ->
                    if k > 0 then sb.Append(',') |> ignore
                    write item)
                sb.Append(']') |> ignore
            | JObject members ->
                sb.Append('{') |> ignore
                members
                |> List.sortWith (fun (a, _) (b, _) -> String.CompareOrdinal(a, b))
                |> List.iteri (fun k (key, item) ->
                    if k > 0 then sb.Append(',') |> ignore
                    escape sb key
                    sb.Append(':') |> ignore
                    write item)
                sb.Append('}') |> ignore
        write value
        sb.ToString()

    /// Structural equality (object member order ignored), as JSON Schema `const`/`enum`/`uniqueItems` require.
    let rec equal (a: Json) (b: Json) =
        match a, b with
        | JObject x, JObject y ->
            x.Length = y.Length
            && x |> List.forall (fun (k, v) -> y |> List.exists (fun (k2, v2) -> k = k2 && equal v v2))
        | JArray x, JArray y -> x.Length = y.Length && List.forall2 equal x y
        | _ -> a = b

    let field (name: string) (value: Json) =
        match value with
        | JObject members -> members |> List.tryPick (fun (k, v) -> if k = name then Some v else None)
        | _ -> None

    let str (s: string) = JString s
    let int (i: int64) = JInt i
    let obj (members: (string * Json) list) = JObject members
    let arr (items: Json list) = JArray items
    let opt (f: 'T -> Json) (v: 'T option) = v |> Option.map f |> Option.defaultValue JNull
