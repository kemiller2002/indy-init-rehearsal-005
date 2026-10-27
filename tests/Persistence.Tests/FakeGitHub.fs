/// A fake GitHub served only from recordings (T18, PF-011-O3, PF-013). The fake keeps repository *state* (branches,
/// commits, files) so it can answer a conversation, but every answer's status, headers and body *shape* is taken
/// from the sanitized observations recorded against real GitHub (recordings/*.ndjson). It never invents a status or
/// a response form: a situation with no recording fails the test that reaches it.
module Persistence.Tests.FakeGitHub

open System
open System.IO
open System.Security.Cryptography
open System.Text
open Causa.Persistence

type Observation =
    { Scenario: string
      Status: int
      Headers: (string * string) list
      BodyShape: JValue
      GraphqlErrorTypes: string list
      Classification: string
      SourceRunId: string }

let private recordingsDir = Path.Combine(AppContext.BaseDirectory, "recordings")

/// Every recorded observation, latest recording last (so it wins).
let observations: Map<string, Observation> =
    Directory.GetFiles(recordingsDir, "*.ndjson")
    |> Array.sort
    |> Array.collect (fun f ->
        let lines = File.ReadAllLines f |> Array.filter (fun l -> l.StartsWith "{")
        let runId =
            lines
            |> Array.tryPick (fun l ->
                match Json.parse l with
                | Ok(JObj o) -> Json.str "sourceRunId" o
                | _ -> None)
            |> Option.defaultValue "unknown"
        lines
        |> Array.choose (fun l ->
            match Json.parse l with
            | Ok(JObj o) when (Json.field "status" o).IsSome ->
                let headers =
                    match Json.field "headers" o with
                    | Some(JObj hs) -> hs |> List.choose (fun (k, v) -> match v with JStr s -> Some(k, s) | _ -> None)
                    | _ -> []
                Some(
                    { Scenario = Json.str "scenario" o |> Option.defaultValue ""
                      Status = Json.int "status" o |> Option.defaultValue 0L |> int
                      Headers = headers
                      BodyShape = Json.field "bodyShape" o |> Option.defaultValue JNull
                      GraphqlErrorTypes =
                        match Json.field "graphqlErrorTypes" o with
                        | Some(JArr xs) -> xs |> List.choose (fun x -> match x with JStr s -> Some s | _ -> None)
                        | _ -> []
                      Classification = Json.str "classification" o |> Option.defaultValue ""
                      SourceRunId = runId }
                )
            | _ -> None))
    |> Array.map (fun o -> o.Scenario, o)
    |> Map.ofArray

let recorded (scenario: string) =
    match Map.tryFind scenario observations with
    | Some o -> o
    | None -> failwithf "No recording for scenario '%s': the fake does not invent provider behaviour." scenario

/// Build a body of exactly the recorded shape, then overlay the values this conversation needs.
let rec private instantiate (shape: JValue) (overlay: JValue option) : JValue =
    match shape, overlay with
    | JObj fields, Some(JObj values) ->
        JObj(fields |> List.map (fun (k, s) -> k, instantiate s (Json.field k values)))
    | JObj fields, _ -> JObj(fields |> List.map (fun (k, s) -> k, instantiate s None))
    | JArr [ element ], Some(JArr items) -> JArr(items |> List.map (fun i -> instantiate element (Some i)))
    | JArr _, _ -> JArr []
    | JStr "null", Some v -> v
    | JStr "null", None -> JNull
    | JStr _, Some v -> v
    | JStr "string", None -> JStr ""
    | JStr "integer", None -> JInt 0L
    | JStr "boolean", None -> JBool false
    | _, Some v -> v
    | _, None -> JNull

let respond (scenario: string) (overlay: JValue option) : HttpResponse =
    let o = recorded scenario
    { Status = o.Status
      Headers = o.Headers
      Body = Json.canonicalText (instantiate o.BodyShape overlay) }

let gitBlobSha (bytes: byte[]) = GitHub.gitBlobSha bytes

type private Auth =
    | Anonymous
    | ValidCredential
    | RejectedCredential

type Fake(owner: string, repo: string, defaultBranch: string, publicRepo: bool) as this =
    let mutable branches: Map<string, string> = Map.empty
    let mutable commits: Map<string, Map<string, byte[]>> = Map.empty
    let mutable valid: Set<string> = Set.empty
    let mutable parents: Map<string, string> = Map.empty
    let mutable messages: Map<string, string> = Map.empty
    let mutable log: HttpRequest list = []
    let mutable counter = 0

    let newOid () =
        counter <- counter + 1
        Convert.ToHexStringLower(SHA1.HashData(Encoding.UTF8.GetBytes(sprintf "fake-commit-%d" counter)))

    let dirSha (path: string) = Convert.ToHexStringLower(SHA1.HashData(Encoding.UTF8.GetBytes("tree:" + path)))

    do
        // Two initial commits, so that every commit a conversation can name has a parent (as on GitHub).
        let root = newOid ()
        let first = newOid ()
        commits <- commits |> Map.add root Map.empty |> Map.add first (Map.ofList [ "README.md", Encoding.UTF8.GetBytes "fake data repository\n" ])
        parents <- parents |> Map.add first root
        branches <- branches |> Map.add defaultBranch first

    member _.Requests = List.rev log
    /// Which recorded repository form answers an authorized read (DC-004: forms without permissions exist).
    member val RepositoryForm = "repository.read.authorized" with get, set
    member val Push = true with get, set
    /// Credentials GitHub refuses writes for (recorded "write.forbidden"), though they may read.
    member val ReadOnly: Set<string> = Set.empty with get, set
    member _.Head(branch: string) = Map.find branch branches
    member _.Branches = branches

    member _.Authorize(token: string) = valid <- valid.Add("Bearer " + token)
    member _.Revoke(token: string) = valid <- valid.Remove("Bearer " + token)

    member _.FilesAt(commit: string) = Map.find commit commits

    /// Another writer (or setup) commits directly to a branch.
    member this.CommitFiles(branch: string, files: (string * byte[]) list) = this.CommitFiles(branch, files, "fake commit")

    member _.CommitFiles(branch: string, files: (string * byte[]) list, message: string) =
        let parent = Map.find branch branches
        let tree = files |> List.fold (fun t (p, b) -> Map.add p b t) (Map.find parent commits)
        let oid = newOid ()
        commits <- commits |> Map.add oid tree
        parents <- parents |> Map.add oid parent
        messages <- messages |> Map.add oid message
        branches <- branches |> Map.add branch oid
        oid

    /// The first-parent chain from a commit back to the root, newest first.
    member _.Chain(oid: string) =
        let rec go o acc =
            match Map.tryFind o parents with
            | Some p -> go p (o :: acc)
            | None -> List.rev (o :: acc)
        go oid []

    member _.Handle(r: HttpRequest) : HttpResponse =
        log <- r :: log
        let url = r.Url.Substring(GitHub.apiBase.Length)
        let path, query =
            match url.IndexOf '?' with
            | -1 -> url, ""
            | i -> url.Substring(0, i), url.Substring(i + 1)
        let queryValue (k: string) =
            query.Split('&') |> Array.tryPick (fun kv -> match kv.Split('=') with [| a; b |] when a = k -> Some(Uri.UnescapeDataString b) | _ -> None)
        let auth =
            match r.Credential with
            | None -> Anonymous
            | Some c when valid.Contains c.AuthorizationHeader -> ValidCredential
            | Some _ -> RejectedCredential
        let repoPrefix = "/repos/" + owner + "/" + repo
        let resolve (refOrSha: string) =
            match Map.tryFind refOrSha branches with
            | Some oid -> Some oid
            | None -> if commits.ContainsKey refOrSha then Some refOrSha else None
        match auth, r.Method with
        | RejectedCredential, "POST" -> respond "write.rejectedCredential" None
        | RejectedCredential, _ -> respond "repository.read.rejectedCredential" None
        | Anonymous, _ when not publicRepo -> respond "ref.read.missingBranch" None
        | _, "GET" when path = repoPrefix ->
            let scenario = if auth = ValidCredential then this.RepositoryForm else "repository.read.anonymous"
            respond
                scenario
                (Some(
                    JObj
                        [ "full_name", JStr(owner + "/" + repo)
                          "name", JStr repo
                          "private", JBool(not publicRepo)
                          "default_branch", JStr defaultBranch
                          "permissions", JObj [ "admin", JBool false; "maintain", JBool false; "pull", JBool true; "push", JBool this.Push; "triage", JBool false ] ]
                ))
        | _, "GET" when path.StartsWith(repoPrefix + "/git/ref/heads/") ->
            let branch = Uri.UnescapeDataString(path.Substring((repoPrefix + "/git/ref/heads/").Length))
            match Map.tryFind branch branches with
            | Some oid -> respond "ref.read.head" (Some(JObj [ "ref", JStr("refs/heads/" + branch); "object", JObj [ "sha", JStr oid; "type", JStr "commit" ] ]))
            | None -> respond "ref.read.missingBranch" None
        | _, "GET" when path.StartsWith(repoPrefix + "/contents/") ->
            let p = Uri.UnescapeDataString(path.Substring((repoPrefix + "/contents/").Length))
            match queryValue "ref" |> Option.bind resolve with
            | None -> respond "contents.list.missingRef" None
            | Some oid ->
                let tree = Map.find oid commits
                match Map.tryFind p tree with
                | Some bytes ->
                    respond
                        "contents.read.file"
                        (Some(
                            JObj
                                [ "type", JStr "file"; "name", JStr((Path.GetFileName p |> nonNull)); "path", JStr p; "sha", JStr(gitBlobSha bytes)
                                  "size", JInt(int64 bytes.Length); "encoding", JStr "base64"; "content", JStr(Convert.ToBase64String bytes) ]
                        ))
                | None ->
                    let prefix = p + "/"
                    let children =
                        tree
                        |> Map.toList
                        |> List.filter (fun (k, _) -> k.StartsWith(prefix, StringComparison.Ordinal))
                        |> List.map (fun (k, b) ->
                            let rest = k.Substring prefix.Length
                            match rest.IndexOf '/' with
                            | -1 -> rest, "file", gitBlobSha b
                            | i -> rest.Substring(0, i), "dir", dirSha (prefix + rest.Substring(0, i)))
                        |> List.distinctBy (fun (n, _, _) -> n)
                    if children.IsEmpty then respond "contents.list.missingPath" None
                    else
                        respond
                            "write.create.confirm"
                            (Some(JArr(children |> List.map (fun (n, k, s) -> JObj [ "name", JStr n; "path", JStr(prefix + n); "type", JStr k; "sha", JStr s ]))))
        | _, "GET" when path.StartsWith(repoPrefix + "/compare/") ->
            match path.Substring((repoPrefix + "/compare/").Length).Split("...") with
            | [| b; h |] when commits.ContainsKey b && commits.ContainsKey h ->
                let headChain = this.Chain h
                let commitRef (c: string) =
                    JObj [ "sha", JStr c; "parents", JArr(Map.tryFind c parents |> Option.toList |> List.map (fun p -> JObj [ "sha", JStr p ])) ]
                let summary status (cs: string list) =
                    JObj
                        [ "status", JStr status
                          "base_commit", commitRef b
                          "merge_base_commit", commitRef b
                          "ahead_by", JInt(int64 cs.Length)
                          "files",
                          JArr(
                              let bt, ht = Map.find b commits, Map.find h commits
                              Set.union (bt |> Map.keys |> Set.ofSeq) (ht |> Map.keys |> Set.ofSeq)
                              |> Set.toList
                              |> List.filter (fun k -> Map.tryFind k bt <> Map.tryFind k ht)
                              |> List.map (fun k -> JObj [ "filename", JStr k; "sha", JStr(Map.tryFind k ht |> Option.map gitBlobSha |> Option.defaultValue "") ])
                          )
                          "total_commits", JInt(int64 cs.Length)
                          "commits",
                          JArr(
                              cs
                              |> List.map (fun c ->
                                  JObj
                                      [ "sha", JStr c
                                        "parents", JArr [ JObj [ "sha", JStr(Map.find c parents) ] ]
                                        "commit", JObj [ "message", JStr(Map.tryFind c messages |> Option.defaultValue "") ] ])
                          ) ]
                if b = h then respond "compare.identical" (Some(summary "identical" []))
                elif List.contains b headChain then
                    let after = headChain |> List.takeWhile ((<>) b) |> List.rev
                    respond "compare.ahead" (Some(summary "ahead" after))
                elif List.contains h (this.Chain b) then respond "compare.behind" (Some(summary "behind" []))
                // Not recorded as its own scenario: GitHub answers a diverged comparison in the same form as
                // "behind" (status value differs); the recorded "behind" form is used.
                else respond "compare.behind" (Some(summary "diverged" []))
            | _ -> respond "contents.list.missingPath" None
        | _, "GET" when path.StartsWith(repoPrefix + "/git/blobs/") ->
            let sha = path.Substring((repoPrefix + "/git/blobs/").Length)
            let found = commits |> Map.toSeq |> Seq.collect (fun (_, t) -> Map.values t) |> Seq.tryFind (fun b -> gitBlobSha b = sha)
            match found with
            | Some b -> respond "blob.read" (Some(JObj [ "sha", JStr sha; "size", JInt(int64 b.Length); "encoding", JStr "base64"; "content", JStr(Convert.ToBase64String b) ]))
            | None -> respond "contents.list.missingPath" None
        | Anonymous, "POST" -> respond "write.rejectedCredential" None
        | _, "POST" when path = "/graphql" && (r.Credential |> Option.exists (fun c -> this.ReadOnly.Contains c.AuthorizationHeader)) ->
            respond
                "write.forbidden"
                (Some(JObj [ "errors", JArr [ JObj [ "type", JStr "FORBIDDEN"; "message", JStr "fake: not accessible"; "path", JArr [ JStr "createCommitOnBranch" ]; "locations", JArr [ JObj [ "line", JInt 1L; "column", JInt 1L ] ]; "extensions", JObj [ "saml_failure", JBool false; "ip_allow_list_failure", JBool false ] ] ] ]))
        | _, "POST" when path = "/graphql" ->
            let input =
                match r.Body |> Option.map Json.parse with
                | Some(Ok(JObj o)) ->
                    match Json.field "variables" o with
                    | Some(JObj v) -> Json.field "input" v
                    | _ -> None
                | _ -> None
            match input with
            | Some(JObj i) ->
                let branch =
                    match Json.field "branch" i with
                    | Some(JObj b) -> Json.str "branchName" b |> Option.defaultValue ""
                    | _ -> ""
                let expected = Json.str "expectedHeadOid" i |> Option.defaultValue ""
                let additions =
                    match Json.field "fileChanges" i with
                    | Some(JObj fc) ->
                        match Json.field "additions" fc with
                        | Some(JArr xs) ->
                            xs
                            |> List.choose (fun x ->
                                match x with
                                | JObj a -> Some(Json.str "path" a |> Option.defaultValue "", Convert.FromBase64String(Json.str "contents" a |> Option.defaultValue ""))
                                | _ -> None)
                        | _ -> []
                    | _ -> []
                match Map.tryFind branch branches with
                | None -> respond "write.missingBranch" (Some(JObj [ "errors", JArr [ JObj [ "type", JStr "NOT_FOUND"; "message", JStr "fake: branch not found"; "path", JArr [ JStr "createCommitOnBranch" ]; "locations", JArr [ JObj [ "line", JInt 1L; "column", JInt 1L ] ] ] ] ]))
                | Some head when head <> expected ->
                    respond "write.conflict.staleHead" (Some(JObj [ "errors", JArr [ JObj [ "type", JStr "STALE_DATA"; "message", JStr "fake: expected head moved"; "path", JArr [ JStr "createCommitOnBranch" ]; "locations", JArr [ JObj [ "line", JInt 1L; "column", JInt 1L ] ] ] ] ]))
                | Some _ ->
                    let message =
                        match Json.field "message" i with
                        | Some(JObj msg) -> (Json.str "headline" msg |> Option.defaultValue "") + "\n\n" + (Json.str "body" msg |> Option.defaultValue "")
                        | _ -> ""
                    let oid = this.CommitFiles(branch, additions, message)
                    respond "write.create" (Some(JObj [ "data", JObj [ "createCommitOnBranch", JObj [ "commit", JObj [ "oid", JStr oid ] ] ] ]))
            | _ -> failwith "fake: unrecognized GraphQL body"
        | _ -> failwithf "fake: no recorded behaviour for %s %s" r.Method path
