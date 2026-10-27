namespace Causa.Persistence

open System
open System.Security.Cryptography
open System.Text

/// A directory entry as listed by the provider at an immutable commit.
type ListedEntry = { Name: string; Kind: string; Sha: string }

/// Repository facts read on connect. `CanPush` is advisory only (an installation token is reported with
/// `push: false` although it may write): GitHub's answer to the write itself decides.
type RepositoryInfo =
    { FullName: string
      Private: bool
      CanPush: bool option
      DefaultBranch: string }

/// The GitHub provider as data (CONTRACT §9.4): request builders and response interpreters. Every mutable read
/// (the branch head) is Fresh; every content read is addressed by an immutable commit or blob id.
[<RequireQualifiedAccess>]
module GitHub =
    let apiBase = "https://api.github.com"

    let private baseHeaders =
        [ "Accept", "application/vnd.github+json"
          "X-GitHub-Api-Version", "2022-11-28" ]

    let private seg (s: string) = Uri.EscapeDataString s

    let private pathSegs (p: string) = String.Join("/", p.Split('/') |> Array.map seg)

    let private repo (t: Target) = "/repos/" + seg t.Owner + "/" + seg t.Repo

    let private get (cred: Credential option) (fresh: bool) (path: string) =
        { Method = "GET"
          Url = apiBase + path
          Headers = baseHeaders
          Body = None
          Fresh = fresh
          Credential = cred }

    let getRepository (cred: Credential option) (t: Target) = get cred true (repo t)

    /// The branch head: a mutable read, never served from a cache (unique query value + Fresh).
    let getBranchHead (cred: Credential option) (t: Target) (nonce: string) =
        get cred true (repo t + "/git/ref/heads/" + pathSegs t.Branch + "?causa_fresh=" + seg nonce)

    /// A directory listing at an immutable commit.
    let listDirectory (cred: Credential option) (t: Target) (commit: string) (path: string) =
        get cred false (repo t + "/contents/" + pathSegs path + "?ref=" + seg commit)

    /// A blob by id: immutable.
    let getBlob (cred: Credential option) (t: Target) (sha: string) = get cred false (repo t + "/git/blobs/" + seg sha)

    let private jstr (s: string) = Json.canonicalText (JStr s)

    /// One atomic commit of every file of a semantic transition, guarded by the head the change was decided on
    /// (GraphQL createCommitOnBranch with expectedHeadOid). A moved branch is refused by GitHub, never merged.
    let createCommit (cred: Credential option) (t: Target) (expectedHead: string) (headline: string) (files: (string * byte[]) list) =
        let additions =
            files
            |> List.map (fun (p, b) -> JObj [ "path", JStr p; "contents", JStr(Convert.ToBase64String b) ])
        let input =
            JObj
                [ "branch", JObj [ "repositoryNameWithOwner", JStr(Target.fullName t); "branchName", JStr t.Branch ]
                  "message", JObj [ "headline", JStr headline ]
                  "expectedHeadOid", JStr expectedHead
                  "fileChanges", JObj [ "additions", JArr additions ] ]
        let body =
            JObj
                [ "query", JStr "mutation($input: CreateCommitOnBranchInput!) { createCommitOnBranch(input: $input) { commit { oid } } }"
                  "variables", JObj [ "input", input ] ]
        { Method = "POST"
          Url = apiBase + "/graphql"
          Headers = baseHeaders @ [ "Content-Type", "application/json" ]
          Body = Some(Json.canonicalText body)
          Fresh = true
          Credential = cred }

    let private header (name: string) (r: HttpResponse) =
        r.Headers |> List.tryPick (fun (k, v) -> if String.Equals(k, name, StringComparison.OrdinalIgnoreCase) then Some v else None)

    let private message (body: string) =
        match Json.parse body with
        | Ok(JObj o) -> Json.str "message" o |> Option.defaultValue ""
        | _ -> ""

    /// Status-level classification of a REST response (PF-008: provider errors, never domain errors).
    let classify (r: HttpResponse) : Result<JValue, ProviderFailure> =
        match r.Status with
        | s when s >= 200 && s < 300 ->
            match Json.parse r.Body with
            | Ok v -> Ok v
            | Error e -> Error(Malformed e)
        | 401 -> Error Unauthorized
        | 403
        | 429 when header "x-ratelimit-remaining" r = Some "0" || header "retry-after" r |> Option.isSome || r.Status = 429 ->
            Error(RateLimited(header "x-ratelimit-reset" r))
        | 403 -> Error(Forbidden(message r.Body))
        | 404 -> Error NotFound
        | 422 -> Error(MissingRef(message r.Body))
        | s when s >= 500 -> Error(Unavailable s)
        | s -> Error(Rejected(s, message r.Body))

    let private str (path: string list) (v: JValue) =
        let rec go p x =
            match p, x with
            | [], JStr s -> Some s
            | k :: rest, JObj o -> Json.field k o |> Option.bind (go rest)
            | _ -> None
        go path v

    let private shaPattern = Text.RegularExpressions.Regex(@"^[0-9a-f]{40}\z")
    let isOid (s: string) = shaPattern.IsMatch s

    let parseRepository (v: JValue) : Result<RepositoryInfo, ProviderFailure> =
        match v with
        | JObj o ->
            match Json.str "full_name" o, Json.field "private" o, Json.str "default_branch" o with
            | Some name, Some(JBool p), Some b ->
                let push =
                    match Json.field "permissions" o with
                    | Some(JObj perms) ->
                        match Json.field "push" perms with
                        | Some(JBool x) -> Some x
                        | _ -> None
                    | _ -> None
                Ok { FullName = name; Private = p; CanPush = push; DefaultBranch = b }
            | _ -> Error(Malformed "repository answer lacks full_name/private/default_branch")
        | _ -> Error(Malformed "repository answer is not an object")

    let parseHead (v: JValue) : Result<string, ProviderFailure> =
        match str [ "object"; "sha" ] v, str [ "object"; "type" ] v with
        | Some sha, Some "commit" when isOid sha -> Ok sha
        | _ -> Error(Malformed "branch answer lacks a commit id")

    let parseListing (v: JValue) : Result<ListedEntry list, ProviderFailure> =
        match v with
        | JArr items ->
            items
            |> traverse (fun i ->
                match str [ "name" ] i, str [ "type" ] i, str [ "sha" ] i with
                | Some n, Some k, Some s when isOid s -> Ok { Name = n; Kind = k; Sha = s }
                | _ -> Error(Malformed "directory entry lacks name/type/sha"))
        | JObj _ -> Error(Malformed "the path is a file, not a directory")
        | _ -> Error(Malformed "directory answer is not an array")

    let parseBlob (v: JValue) : Result<byte[], ProviderFailure> =
        match str [ "content" ] v, str [ "encoding" ] v with
        | Some content, Some "base64" ->
            try Ok(Convert.FromBase64String(content.Replace("\n", "")))
            with :? FormatException -> Error(Malformed "blob content is not base64")
        | _ -> Error(Malformed "blob answer is not base64")

    /// Interpret the createCommitOnBranch answer. GraphQL reports refusals with HTTP 200 and an `errors` array.
    let parseCommit (r: HttpResponse) : Result<string, ProviderFailure> =
        match classify r with
        | Error e -> Error e
        | Ok v ->
            let errors =
                match v with
                | JObj o ->
                    match Json.field "errors" o with
                    | Some(JArr es) -> es
                    | _ -> []
                | _ -> []
            match errors with
            | [] ->
                match str [ "data"; "createCommitOnBranch"; "commit"; "oid" ] v with
                | Some oid when isOid oid -> Ok oid
                | _ -> Error(Malformed "commit answer lacks an oid")
            | e :: _ ->
                let kind = str [ "type" ] e |> Option.defaultValue ""
                let msg = str [ "message" ] e |> Option.defaultValue ""
                if kind = "STALE_DATA" || msg.Contains("Expected branch to point to", StringComparison.Ordinal) then Error(HeadMoved kind)
                elif kind = "FORBIDDEN" then Error(Forbidden kind)
                elif kind = "NOT_FOUND" then Error NotFound
                elif kind = "UNPROCESSABLE" || msg.Contains("does not exist", StringComparison.Ordinal) then Error(MissingRef kind)
                elif kind = "RATE_LIMITED" then Error(RateLimited None)
                else Error(Rejected(200, kind))

    /// Git's blob id of exact bytes: SHA-1 of "blob <length>\0" + bytes. Lets a re-read prove which bytes are at a
    /// commit without downloading them.
    let gitBlobSha (bytes: byte[]) =
        let header = Encoding.ASCII.GetBytes(sprintf "blob %d\000" bytes.Length)
        Convert.ToHexStringLower(SHA1.HashData(Array.append header bytes))
