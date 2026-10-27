/// causa-persistence record — T18 provider conformance recording and T21 live write-path checks against the real
/// write-capable test repository. Every request is built by Causa.Persistence.GitHub (the same code the
/// application uses) and every answer is interpreted by it; the tool only performs the I/O and prints sanitized
/// observations between markers. Writes go only to workspaces/demo/rec-<runId>/ on the test repository.
module Causa.Persistence.Tool.Program

open System
open System.Diagnostics
open Causa.Persistence

let private beginMarker = "=== CAUSA-PROVIDER-OBSERVATIONS BEGIN ==="
let private endMarker = "=== CAUSA-PROVIDER-OBSERVATIONS END ==="

type private Ctx =
    { Secrets: string list
      mutable Failures: string list }

let private emit (ctx: Ctx) (v: JValue) =
    Console.Out.WriteLine(Sanitize.guard ctx.Secrets (Json.canonicalText v))
    Console.Out.Flush()

let private classification (r: Result<'T, ProviderFailure>) =
    match r with
    | Ok _ -> "ok"
    | Error f ->
        match f with
        | Unauthorized -> "Unauthorized"
        | Forbidden _ -> "Forbidden"
        | RateLimited _ -> "RateLimited"
        | NotFound -> "NotFound"
        | MissingRef _ -> "MissingRef"
        | HeadMoved _ -> "HeadMoved"
        | Unavailable _ -> "Unavailable"
        | Offline _ -> "Offline"
        | Unknown _ -> "Unknown"
        | Malformed _ -> "Malformed"
        | Rejected _ -> "Rejected"

/// Perform one request and print its observation. Returns the response for the caller to interpret.
let private observe (ctx: Ctx) (scenario: string) (route: string) (request: HttpRequest) (classify: HttpResponse -> string) (extra: (string * JValue) list) =
    let sw = Stopwatch.StartNew()
    let result = Transport.send request
    sw.Stop()
    match result with
    | Ok r ->
        emit
            ctx
            (JObj(
                [ "scenario", JStr scenario
                  "method", JStr request.Method
                  "route", JStr route
                  "authenticated", JBool request.Credential.IsSome
                  "fresh", JBool request.Fresh
                  "status", JInt(int64 r.Status)
                  "headers", Sanitize.headers r
                  "bodyShape", Sanitize.bodyShape r.Body
                  "graphqlErrorTypes", JArr(Sanitize.graphqlErrorTypes r.Body)
                  "classification", JStr(classify r)
                  "elapsedMs", JInt sw.ElapsedMilliseconds ]
                @ extra
            ))
        Some r
    | Error f ->
        emit ctx (JObj [ "scenario", JStr scenario; "route", JStr route; "transportFailure", JStr(sprintf "%A" f) ])
        None

let private expect (ctx: Ctx) (scenario: string) (ok: bool) (detail: string) =
    emit ctx (JObj [ "check", JStr scenario; "passed", JBool ok; "detail", JStr detail ])
    if not ok then ctx.Failures <- ctx.Failures @ [ scenario ]

let private now () = Timestamp.ofDateTimeOffset DateTimeOffset.UtcNow

let private actor = JObj [ "actorId", JStr "causa-provider-recorder"; "kind", JStr "system" ]

let private recordAll (token: string) (repoFull: string) (runId: string) =
    let ctx = { Secrets = [ token; repoFull ]; Failures = [] }
    let cred =
        match Credential.create token with
        | Ok c -> c
        | Error _ ->
            eprintfn "BLOCKED: CAUSA_GITHUB_TOKEN is not a usable token."
            exit 2
    let owner, repo =
        match repoFull.Split('/') with
        | [| o; r |] -> o, r
        | _ ->
            eprintfn "BLOCKED: CAUSA_TEST_REPO is not owner/repo."
            exit 2
    let workspace = "rec-" + runId
    let probe =
        match Target.create owner repo "main" workspace with
        | Ok t -> t
        | Error p ->
            eprintfn "BLOCKED: %s" (String.Join(" ", p))
            exit 2
    Console.Out.WriteLine beginMarker
    emit ctx (JObj [ "recording", JStr "causa.provider-observations/1"; "sourceRunId", JStr runId; "recordedAt", JStr(now ()) ])
    let repoOf (r: HttpResponse) = GitHub.classify r |> Result.bind GitHub.parseRepository
    // Repository: authorized, anonymous, rejected credential. Permissions are recorded (booleans only).
    let info =
        observe ctx "repository.read.authorized" "GET /repos/{owner}/{repo}" (GitHub.getRepository (Some cred) probe) (repoOf >> classification) []
        |> Option.bind (fun r ->
            match GitHub.classify r with
            | Ok(JObj o) ->
                match Json.field "permissions" o with
                | Some(JObj perms) ->
                    emit ctx (JObj [ "scenario", JStr "repository.permissions"; "permissions", JObj(perms |> List.filter (fun (_, v) -> match v with JBool _ -> true | _ -> false)) ])
                | _ -> emit ctx (JObj [ "scenario", JStr "repository.permissions"; "permissions", JStr "absent" ])
                repoOf r |> Result.toOption
            | _ -> None)
    observe ctx "repository.read.anonymous" "GET /repos/{owner}/{repo}" (GitHub.getRepository None probe) (repoOf >> classification) [] |> ignore
    let bogus = Credential("causa-invalid-credential-for-recording")
    observe ctx "repository.read.rejectedCredential" "GET /repos/{owner}/{repo}" (GitHub.getRepository (Some bogus) probe) (repoOf >> classification) [] |> ignore
    match info with
    | None ->
        Console.Out.WriteLine endMarker
        eprintfn "FAILED: the repository could not be read with the credential."
        1
    | Some info ->
        let t = { probe with Branch = info.DefaultBranch }
        let headOf (r: HttpResponse) = GitHub.classify r |> Result.bind GitHub.parseHead
        let readHead scenario =
            observe ctx scenario "GET /repos/{owner}/{repo}/git/ref/heads/{branch}?causa_fresh={nonce}" (GitHub.getBranchHead (Some cred) t (Guid.NewGuid().ToString "N")) (headOf >> classification) []
            |> Option.bind (headOf >> Result.toOption)
        let head0 = readHead "ref.read.head"
        // The same mutable read without the fresh marker, to record cache headers GitHub sends on it.
        observe ctx "ref.read.head.cacheable" "GET /repos/{owner}/{repo}/git/ref/heads/{branch}" { GitHub.getBranchHead (Some cred) t "" with Url = GitHub.apiBase + "/repos/" + owner + "/" + repo + "/git/ref/heads/" + t.Branch; Fresh = false } (headOf >> classification) [] |> ignore
        observe ctx "ref.read.missingBranch" "GET /repos/{owner}/{repo}/git/ref/heads/{missing}" (GitHub.getBranchHead (Some cred) { t with Branch = "causa-missing-" + runId } "x") (headOf >> classification) [] |> ignore
        let listOf (r: HttpResponse) = GitHub.classify r |> Result.bind GitHub.parseListing
        match head0 with
        | None ->
            Console.Out.WriteLine endMarker
            eprintfn "FAILED: the branch head could not be read."
            1
        | Some head0 ->
            emit ctx (JObj [ "oid", JStr "head0"; "value", JStr head0 ])
            observe ctx "contents.list.missingPath" "GET /repos/{owner}/{repo}/contents/{path}?ref={commit}" (GitHub.listDirectory (Some cred) t head0 (Target.investigationsDir t)) (listOf >> classification) [] |> ignore
            observe ctx "contents.list.missingRef" "GET /repos/{owner}/{repo}/contents/{path}?ref={missingBranch}" (GitHub.listDirectory (Some cred) t ("causa-missing-" + runId) "workspaces") (listOf >> classification) [] |> ignore
            observe ctx "commits.read.missingSha" "GET /repos/{owner}/{repo}/commits/{missingSha}" { GitHub.getBlob (Some cred) t "x" with Url = GitHub.apiBase + "/repos/" + owner + "/" + repo + "/commits/" + String('0', 40) } (GitHub.classify >> classification) [] |> ignore
            // A new synthetic investigation, built and verified by the same writer the application uses.
            let iid = Uuid.v5 "6ba7b811-9dad-11d1-80b4-00c04fd430c8" ("causa-provider-recorder/" + runId)
            let startedAt = now ()
            let start =
                FileSet.envelope iid 1L (Guid.NewGuid().ToString()) startedAt actor "InvestigationStarted"
                    (JObj [ "title", JStr("Provider recording " + runId); "observationId", JStr(Uuid.v5 iid "observation"); "original", JStr "Synthetic investigation written by the provider recorder." ])
            let created =
                match FileSet.append HistoryEngine.notWired iid None 0L [ start ] with
                | Ok v -> v
                | Error e -> failwithf "synthetic file set invalid: %s" e.Code
            let filesOf (v: Verified) = FileSet.files v |> List.map (fun (n, b) -> Target.investigationDir t iid + "/" + n, b)
            let commitOf (r: HttpResponse) = GitHub.parseCommit r
            let write scenario expected headline (v: Verified) branch credential =
                let req = GitHub.createCommit (Some credential) { t with Branch = branch } expected headline (filesOf v)
                observe ctx scenario "POST /graphql createCommitOnBranch" req (commitOf >> classification) []
            let sw = Stopwatch.StartNew()
            let oid1 = write "write.create" head0 ("causa: create investigation (recorder " + runId + ")") created t.Branch cred |> Option.bind (commitOf >> Result.toOption)
            // Confirm: the listing at the new commit must carry exactly the git blob ids of the sent bytes.
            let confirm scenario (oid: string) (v: Verified) =
                let listing =
                    observe ctx scenario "GET /repos/{owner}/{repo}/contents/{path}?ref={commit}" (GitHub.listDirectory (Some cred) t oid (Target.investigationDir t iid)) (listOf >> classification) []
                    |> Option.bind (listOf >> Result.toOption)
                match listing with
                | Some entries ->
                    let found = entries |> List.map (fun e -> e.Name, e.Sha) |> Map.ofList
                    FileSet.files v |> List.forall (fun (n, b) -> Map.tryFind n found = Some(GitHub.gitBlobSha b)) && entries.Length = 3
                | None -> false
            match oid1 with
            | None ->
                expect ctx "live.create" false "the create commit was not acknowledged"
            | Some oid1 ->
                emit ctx (JObj [ "oid", JStr "create"; "value", JStr oid1 ])
                let confirmed = confirm "write.create.confirm" oid1 created
                sw.Stop()
                expect ctx "live.create" confirmed (sprintf "first verified remote write in %d ms" sw.ElapsedMilliseconds)
                // Blob read (immutable) of the written manifest.
                let blobId = GitHub.gitBlobSha created.Manifest
                observe ctx "blob.read" "GET /repos/{owner}/{repo}/git/blobs/{sha}" (GitHub.getBlob (Some cred) t blobId) (GitHub.classify >> Result.bind GitHub.parseBlob >> classification) [] |> ignore
                // One file through the contents API (used for the catalog pointer and manifest), present and absent.
                let fileOf (r: HttpResponse) = GitHub.classify r |> Result.bind GitHub.parseFile
                let fileRead =
                    observe ctx "contents.read.file" "GET /repos/{owner}/{repo}/contents/{path}?ref={commit}" (GitHub.getFile (Some cred) t oid1 (Target.investigationDir t iid + "/" + FileSet.manifestName)) (fileOf >> classification) []
                    |> Option.bind (fileOf >> Result.toOption)
                expect ctx "live.read.file" (fileRead = Some created.Manifest) "the file read through the contents API is byte-identical"
                observe ctx "contents.read.file.missing" "GET /repos/{owner}/{repo}/contents/{path}?ref={commit}" (GitHub.getFile (Some cred) t oid1 "catalog/active.json") (fileOf >> classification) [] |> ignore
                // Reload: read the head and the files again from the provider, verify through the full reader.
                let headAfter = readHead "ref.read.afterCreate"
                expect ctx "live.reload.head" (headAfter = Some oid1) "the branch head is the acknowledged commit"
                // Append: one more event decided at version 1, guarded by the head it was decided on.
                let append (base': Verified) (version: int64) (title: string) =
                    let e =
                        FileSet.envelope iid version (Guid.NewGuid().ToString()) (now ()) actor "ObservationRecorded"
                            (JObj [ "observationId", JStr(Uuid.v5 iid ("obs/" + title)); "original", JStr title; "observedAt", JNull ])
                    match FileSet.append HistoryEngine.notWired iid (Some base') (version - 1L) [ e ] with
                    | Ok v -> v
                    | Error e -> failwithf "append invalid: %s" e.Code
                let second = append created 2L "second observation"
                let oid2 = write "write.append" oid1 "causa: append (recorder)" second t.Branch cred |> Option.bind (commitOf >> Result.toOption)
                match oid2 with
                | None -> expect ctx "live.append" false "append not acknowledged"
                | Some oid2 ->
                    emit ctx (JObj [ "oid", JStr "append"; "value", JStr oid2 ])
                    expect ctx "live.append" (confirm "write.append.confirm" oid2 second) "appended bytes re-read at the new commit"
                    // Conflict: a concurrent change decided on oid1 (stale head). GitHub must refuse; the head must not move.
                    let sw2 = Stopwatch.StartNew()
                    let concurrent = append created 2L "concurrent observation"
                    let conflict = write "write.conflict.staleHead" oid1 "causa: stale write (recorder)" concurrent t.Branch cred
                    let refused = conflict |> Option.map (commitOf >> classification) = Some "HeadMoved"
                    let headAfterConflict = readHead "ref.read.afterConflict"
                    sw2.Stop()
                    expect ctx "live.conflict" (refused && headAfterConflict = Some oid2) (sprintf "stale write refused and head unchanged; conflict handled in %d ms" sw2.ElapsedMilliseconds)
                    // Missing branch and rejected credential on the write path.
                    write "write.missingBranch" oid2 "causa: missing branch (recorder)" second ("causa-missing-" + runId) cred |> ignore
                    let rejected = write "write.rejectedCredential" oid2 "causa: rejected credential (recorder)" second t.Branch bogus
                    expect ctx "live.rejectedToken" (rejected |> Option.map (commitOf >> classification) = Some "Unauthorized") "a rejected credential writes nothing"
                    expect ctx "live.rejectedToken.head" (readHead "ref.read.afterRejected" = Some oid2) "the head did not move"
                    // Unknown outcome: send a write and discard its answer, then reconcile from a fresh head read only.
                    let third = append second 3L "third observation"
                    Transport.send (GitHub.createCommit (Some cred) t oid2 "causa: unknown-outcome write (recorder)" (filesOf third)) |> ignore
                    emit ctx (JObj [ "scenario", JStr "write.unknownOutcome"; "note", JStr "answer deliberately discarded; outcome reconciled by re-reading" ])
                    match readHead "ref.read.reconcile" with
                    | Some h ->
                        let applied = h <> oid2 && confirm "write.reconcile.listing" h third
                        expect ctx "live.reconcile" applied "the discarded write was found applied by content at the re-read head"
                    | None -> expect ctx "live.reconcile" false "head could not be re-read"
            Console.Out.WriteLine endMarker
            if ctx.Failures.IsEmpty then
                Console.Out.WriteLine "RESULT: PASS (live provider; no mocks)"
                0
            else
                Console.Out.WriteLine("RESULT: FAIL " + String.Join(",", ctx.Failures))
                1

[<EntryPoint>]
let main argv =
    match argv with
    | [| "record" |] ->
        let env name = Environment.GetEnvironmentVariable name |> Option.ofObj |> Option.defaultValue ""
        let token = env "CAUSA_GITHUB_TOKEN"
        let repo = env "CAUSA_TEST_REPO"
        let runId = env "GITHUB_RUN_ID"
        if String.IsNullOrEmpty token || String.IsNullOrEmpty repo then
            Console.Out.WriteLine "RESULT: BLOCKED (CAUSA_GITHUB_TOKEN or CAUSA_TEST_REPO is not available; a missing secret is never a pass)"
            2
        elif repo.Equals("kemiller2002/causa-data", StringComparison.OrdinalIgnoreCase) then
            Console.Out.WriteLine "RESULT: BLOCKED (the recorder never writes to the canonical data repository)"
            2
        else
            recordAll token repo (if String.IsNullOrEmpty runId then "local" else runId)
    | _ ->
        eprintfn "usage: causa-persistence record"
        64
