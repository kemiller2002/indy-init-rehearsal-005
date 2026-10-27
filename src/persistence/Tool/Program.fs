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

/// Commands are decided by the engine (Boundary A); nothing here builds events by hand.
module private Cmd =
    type E = IndyInitRootCauseInvestigator.Engine.CommandEnvelope
    let actor: IndyInitRootCauseInvestigator.Engine.Actor = { ActorId = "causa-provider-recorder"; Kind = IndyInitRootCauseInvestigator.Engine.ActorKind.System }
    let at () =
        let n = DateTimeOffset.UtcNow
        DateTimeOffset(n.Ticks - (n.Ticks % TimeSpan.TicksPerMillisecond), TimeSpan.Zero)
    let envelope (iid: string) (expected: int64) (command: IndyInitRootCauseInvestigator.Engine.Command) : E =
        { CommandId = Guid.NewGuid()
          Investigation = IndyInitRootCauseInvestigator.Engine.Id (Guid iid)
          ExpectedVersion = Some expected
          Actor = actor
          RequestedAt = at ()
          Command = command }
    let start (iid: string) (title: string) =
        envelope iid 0L (IndyInitRootCauseInvestigator.Engine.StartInvestigation(title, IndyInitRootCauseInvestigator.Engine.Id (Guid(Uuid.v5 iid "observation")), "Synthetic investigation written by the provider recorder."))
    let observe (iid: string) (expected: int64) (text: string) =
        envelope iid expected (IndyInitRootCauseInvestigator.Engine.RecordObservation(IndyInitRootCauseInvestigator.Engine.Id (Guid.NewGuid()), text, None))

/// Drive the real Sync program: dispatch, perform its effects against real GitHub, feed the answers back.
/// `lose` marks requests whose answer is discarded after they were sent (a lost answer: outcome unknown).
let private runWith (lose: HttpRequest -> bool) (env: Env) (model: Model) (msg: Msg) : Model * string list * string option =
    let mutable m = model
    let mutable refusals = []
    let mutable stored = None
    let mutable queue = [ msg ]
    while not queue.IsEmpty do
        let next = queue.Head
        queue <- queue.Tail
        match Sync.update env next m with
        | Error r -> refusals <- refusals @ [ r.Reason ]
        | Ok(m2, effects) ->
            m <- m2
            for e in effects do
                match e with
                | StoreLocal(writeId, _, value) ->
                    stored <- Some value
                    queue <- queue @ [ LocalStored(writeId, true) ]
                | Send(id, request) ->
                    match Transport.send request with
                    | Ok _ when lose request -> queue <- queue @ [ HttpFailed(id, TransportFailure.OutcomeUnknown "answer discarded by the live check") ]
                    | Ok r -> queue <- queue @ [ HttpAnswered(id, r) ]
                    | Error f -> queue <- queue @ [ HttpFailed(id, f) ]
    m, refusals, stored

let private runProgram env model msg =
    let m, refusals, _ = runWith (fun _ -> false) env model msg
    m, refusals

/// T21: the live write path through the application's own Sync program (connect, create, save, append, reload,
/// conflict, unknown outcome, rejected credential) against the write-capable test repository.
let private programLive (ctx: Ctx) (cred: Credential) (owner: string) (repo: string) (branch: string) (runId: string) =
    match Target.create owner repo branch ("rec-" + runId + "-app") with
    | Error _ -> expect ctx "live.app.target" false "the application target is invalid"
    | Ok t ->
        let env = { Now = now; Nonce = Guid.NewGuid().ToString "N" }
        let mutable stored = None
        let run lose m msg =
            let m2, refusals, s = runWith lose env m msg
            s |> Option.iter (fun v -> stored <- Some v)
            m2, refusals
        let status (m: Model) id = m.Documents |> Map.tryFind id |> Option.map (Sync.documentStatus m) |> Option.defaultValue "absent"
        let isPost (r: HttpRequest) = r.Method = "POST"
        let sw = Stopwatch.StartNew()
        let m, _ = run (fun _ -> false) Sync.initial (Connect(t, cred))
        expect ctx "live.app.connect" (Sync.connectionLabel m = "authorizedEmpty") ("a new workspace connects as " + Sync.connectionLabel m)
        let iid = Uuid.v5 "6ba7b811-9dad-11d1-80b4-00c04fd430c8" ("causa-provider-recorder/app/" + runId)
        let m, _ = run (fun _ -> false) m (Create(Cmd.start iid ("Application live check " + runId)))
        let m, _ = run (fun _ -> false) m (Save iid)
        sw.Stop()
        expect ctx "live.app.create" (status m iid = "remotelySynced") (sprintf "create + save -> %s in %d ms (claimed only after the re-read)" (status m iid) sw.ElapsedMilliseconds)
        let version (m: Model) = m.Documents |> Map.tryFind iid |> Option.map (fun d -> d.Local.Version) |> Option.defaultValue 0L
        let m, _ = run (fun _ -> false) m (Submit(Cmd.observe iid (version m) "live append"))
        let m, _ = run (fun _ -> false) m (Save iid)
        expect ctx "live.app.append" (status m iid = "remotelySynced") ("append + save -> " + status m iid)
        let digest = m.Documents |> Map.tryFind iid |> Option.map (fun d -> d.Local.EventsSha256)
        // Reload: memory is gone; the stored record and a new Connect bring the investigation back from GitHub.
        let r0, _ = run (fun _ -> false) Sync.initial (Restore stored)
        let r1, _ = run (fun _ -> false) r0 (Connect(t, cred))
        let reloaded = r1.Documents |> Map.tryFind iid
        expect ctx "live.app.reload" (reloaded |> Option.exists (fun d -> Some d.Local.EventsSha256 = digest && d.Confirmed.IsSome) && Sync.connectionLabel r1 = "connected") ("after reload the investigation is " + status r1 iid)
        // Conflict: another writer appends to the same investigation; this device's save must be refused.
        let sw2 = Stopwatch.StartNew()
        match reloaded, r1.Head with
        | Some d, Some head ->
            let theirs =
                match Store.submit d.Local (Cmd.observe iid d.Local.Version "another writer") with
                | Ok v -> v
                | Error e -> failwithf "other writer refused: %s" e.Code
            let files = Store.files theirs |> List.map (fun (n, b) -> Target.investigationDir t iid + "/" + n, b)
            let other = Transport.send (GitHub.createCommit (Some cred) t head "causa: another writer (live check)" (GitHub.markerTrailer + "other-" + runId) files)
            let otherHead = other |> Result.toOption |> Option.bind (GitHub.parseCommit >> Result.toOption)
            let c1, _ = run (fun _ -> false) r1 (Submit(Cmd.observe iid d.Local.Version "mine, concurrently"))
            let c2, _ = run (fun _ -> false) c1 (Save iid)
            let headNow =
                Transport.send (GitHub.getBranchHead (Some cred) t (Guid.NewGuid().ToString "N"))
                |> Result.toOption
                |> Option.bind (fun r -> GitHub.classify r |> Result.bind GitHub.parseHead |> Result.toOption)
            let localKept = c2.Documents |> Map.tryFind iid |> Option.exists (fun d -> d.Local.Version = theirs.Version)
            sw2.Stop()
            expect ctx "live.app.conflict" (status c2 iid = "conflict" && otherHead.IsSome && headNow = otherHead && localKept) (sprintf "save after a concurrent write -> %s; remote head untouched; local change kept; %d ms" (status c2 iid) sw2.ElapsedMilliseconds)
            let c3, _ = run (fun _ -> false) c2 (ReloadRemote iid)
            let c4, _ = run (fun _ -> false) c3 (DiscardLocal iid)
            expect ctx "live.app.conflict.resolve" ((status c4 iid = "clean" || status c4 iid = "remotelySynced") && (c4.Documents |> Map.tryFind iid |> Option.exists (fun d -> d.Local.EventsSha256 = theirs.EventsSha256))) ("reload + discard adopts the other writer's copy -> " + status c4 iid)
            // Unknown outcome: the answer to the save is lost after sending; only Reconcile settles it.
            let u1, _ = run (fun _ -> false) c4 (Submit(Cmd.observe iid theirs.Version "answer will be lost"))
            let u2, _ = run isPost u1 (Save iid)
            let unknown = status u2 iid
            let u3, refusals = run (fun _ -> false) u2 (Save iid)
            let u4, _ = run (fun _ -> false) u3 (Reconcile iid)
            expect ctx "live.app.unknown" (unknown = "unknown" && not refusals.IsEmpty && status u4 iid = "remotelySynced") (sprintf "lost answer -> %s, save refused, reconcile -> %s" unknown (status u4 iid))
        | _ -> expect ctx "live.app.conflict" false "the reloaded investigation or head is missing"
        // A rejected credential never shows a connection.
        let bogus = Credential("causa-invalid-credential-for-recording")
        let x, _ = run (fun _ -> false) Sync.initial (Connect(t, bogus))
        expect ctx "live.app.rejectedToken" (Sync.connectionLabel x = "accessDenied") ("a rejected token connects as " + Sync.connectionLabel x)

let private recordAll (token: string) (repoFull: string) (runId: string) =
    let readOnlyToken = Environment.GetEnvironmentVariable "CAUSA_READONLY_TOKEN" |> Option.ofObj |> Option.defaultValue ""
    let ctx = { Secrets = [ token; repoFull; readOnlyToken ] |> List.filter (fun s -> s <> ""); Failures = [] }
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
        observe ctx "repository.read.authorized" "GET /repos/{owner}/{repo}" (GitHub.getRepository (Some cred) probe (Guid.NewGuid().ToString "N")) (repoOf >> classification) []
        |> Option.bind (fun r ->
            match GitHub.classify r with
            | Ok(JObj o) ->
                match Json.field "permissions" o with
                | Some(JObj perms) ->
                    emit ctx (JObj [ "scenario", JStr "repository.permissions"; "permissions", JObj(perms |> List.filter (fun (_, v) -> match v with JBool _ -> true | _ -> false)) ])
                | _ -> emit ctx (JObj [ "scenario", JStr "repository.permissions"; "permissions", JStr "absent" ])
                repoOf r |> Result.toOption
            | _ -> None)
    observe ctx "repository.read.anonymous" "GET /repos/{owner}/{repo}" (GitHub.getRepository None probe (Guid.NewGuid().ToString "N")) (repoOf >> classification) [] |> ignore
    let bogus = Credential("causa-invalid-credential-for-recording")
    observe ctx "repository.read.rejectedCredential" "GET /repos/{owner}/{repo}" (GitHub.getRepository (Some bogus) probe (Guid.NewGuid().ToString "N")) (repoOf >> classification) [] |> ignore
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
            let created =
                match Store.create (Cmd.start iid ("Provider recording " + runId)) with
                | Ok v -> v
                | Error e -> failwithf "synthetic investigation refused: %s" e.Code
            let filesOf (v: Verified) = Store.files v |> List.map (fun (n, b) -> Target.investigationDir t iid + "/" + n, b)
            let commitOf (r: HttpResponse) = GitHub.parseCommit r
            let write scenario expected headline (v: Verified) branch credential =
                let req = GitHub.createCommit (Some credential) { t with Branch = branch } expected headline (GitHub.markerTrailer + "recorder-" + runId + "-" + scenario) (filesOf v)
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
                    Store.files v |> List.forall (fun (n, b) -> Map.tryFind n found = Some(GitHub.gitBlobSha b)) && entries.Length = 3
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
                    observe ctx "contents.read.file" "GET /repos/{owner}/{repo}/contents/{path}?ref={commit}" (GitHub.getFile (Some cred) t oid1 (Target.investigationDir t iid + "/" + Store.manifestName)) (fileOf >> classification) []
                    |> Option.bind (fileOf >> Result.toOption)
                expect ctx "live.read.file" (fileRead = Some created.Manifest) "the file read through the contents API is byte-identical"
                observe ctx "contents.read.file.missing" "GET /repos/{owner}/{repo}/contents/{path}?ref={commit}" (GitHub.getFile (Some cred) t oid1 "catalog/active.json") (fileOf >> classification) [] |> ignore
                // Reload: read the head and the files again from the provider, verify through the full reader.
                let headAfter = readHead "ref.read.afterCreate"
                expect ctx "live.reload.head" (headAfter = Some oid1) "the branch head is the acknowledged commit"
                // Append: one more event decided at version 1, guarded by the head it was decided on.
                let append (base': Verified) (version: int64) (title: string) =
                    match Store.submit base' (Cmd.observe iid (version - 1L) title) with
                    | Ok v -> v
                    | Error e -> failwithf "append refused: %s" e.Code
                let second = append created 2L "second observation"
                let oid2 = write "write.append" oid1 "causa: append (recorder)" second t.Branch cred |> Option.bind (commitOf >> Result.toOption)
                match oid2 with
                | None -> expect ctx "live.append" false "append not acknowledged"
                | Some oid2 ->
                    emit ctx (JObj [ "oid", JStr "append"; "value", JStr oid2 ])
                    expect ctx "live.append" (confirm "write.append.confirm" oid2 second) "appended bytes re-read at the new commit"
                    // Ancestry for reconciliation (core Sync.classify): the compare API, ahead / identical / behind.
                    let compareOf (r: HttpResponse) = GitHub.classify r |> Result.bind GitHub.parseCompare
                    let ahead =
                        observe ctx "compare.ahead" "GET /repos/{owner}/{repo}/compare/{base}...{head}" (GitHub.compare (Some cred) t head0 oid2) (compareOf >> classification) []
                        |> Option.bind (compareOf >> Result.toOption)
                    let markersOk =
                        match ahead with
                        | Some(GitHub.Descends [ c1; c2 ]) ->
                            c1.Sha = oid1 && c1.Parent = head0 && c2.Sha = oid2 && c2.Parent = oid1
                            && c1.Marker = Some("recorder-" + runId + "-write.create") && c2.Marker = Some("recorder-" + runId + "-write.append")
                        | _ -> false
                    expect ctx "live.compare.ancestry" markersOk "the compare API gives first parents and commit markers, oldest first"
                    observe ctx "compare.identical" "GET /repos/{owner}/{repo}/compare/{base}...{head}" (GitHub.compare (Some cred) t oid2 oid2) (compareOf >> classification) [] |> ignore
                    observe ctx "compare.behind" "GET /repos/{owner}/{repo}/compare/{base}...{head}" (GitHub.compare (Some cred) t oid2 oid1) (compareOf >> classification) [] |> ignore
                    // Conflict: a concurrent change decided on oid1 (stale head). GitHub must refuse; the head must not move.
                    let sw2 = Stopwatch.StartNew()
                    let concurrent = append created 2L "concurrent observation"
                    let conflict = write "write.conflict.staleHead" oid1 "causa: stale write (recorder)" concurrent t.Branch cred
                    let refused = conflict |> Option.map (commitOf >> classification) = Some "HeadMoved"
                    let headAfterConflict = readHead "ref.read.afterConflict"
                    sw2.Stop()
                    expect ctx "live.conflict" (refused && headAfterConflict = Some oid2) (sprintf "stale write refused and head unchanged; conflict handled in %d ms" sw2.ElapsedMilliseconds)
                    // DC-004: a credential without write access to the test repository (the workflow's own read-only
                    // installation token). Its repository read may omit permissions; its write is refused by GitHub.
                    let readOnly = Environment.GetEnvironmentVariable "CAUSA_READONLY_TOKEN" |> Option.ofObj |> Option.defaultValue ""
                    if readOnly <> "" then
                        let ro = Credential readOnly
                        let ctxSecrets = ctx.Secrets
                        ignore ctxSecrets
                        observe ctx "repository.read.installationToken" "GET /repos/{owner}/{repo}" (GitHub.getRepository (Some ro) t (Guid.NewGuid().ToString "N")) (repoOf >> classification) [] |> ignore
                        write "write.forbidden" oid2 "causa: read-only credential (recorder)" second t.Branch ro |> ignore
                        expect ctx "live.forbidden.head" (readHead "ref.read.afterForbidden" = Some oid2) "a refused write moved nothing"
                    // Missing branch and rejected credential on the write path.
                    write "write.missingBranch" oid2 "causa: missing branch (recorder)" second ("causa-missing-" + runId) cred |> ignore
                    let rejected = write "write.rejectedCredential" oid2 "causa: rejected credential (recorder)" second t.Branch bogus
                    expect ctx "live.rejectedToken" (rejected |> Option.map (commitOf >> classification) = Some "Unauthorized") "a rejected credential writes nothing"
                    expect ctx "live.rejectedToken.head" (readHead "ref.read.afterRejected" = Some oid2) "the head did not move"
                    // Unknown outcome: send a write and discard its answer, then reconcile from a fresh head read only.
                    let third = append second 3L "third observation"
                    Transport.send (GitHub.createCommit (Some cred) t oid2 "causa: unknown-outcome write (recorder)" (GitHub.markerTrailer + "recorder-unknown-" + runId) (filesOf third)) |> ignore
                    emit ctx (JObj [ "scenario", JStr "write.unknownOutcome"; "note", JStr "answer deliberately discarded; outcome reconciled by re-reading" ])
                    match readHead "ref.read.reconcile" with
                    | Some h ->
                        let applied = h <> oid2 && confirm "write.reconcile.listing" h third
                        expect ctx "live.reconcile" applied "the discarded write was found applied by content at the re-read head"
                    | None -> expect ctx "live.reconcile" false "head could not be re-read"
            programLive ctx cred owner repo t.Branch runId
            Console.Out.WriteLine endMarker
            if ctx.Failures.IsEmpty then
                Console.Out.WriteLine "RESULT: PASS (live provider; no mocks)"
                0
            else
                Console.Out.WriteLine("RESULT: FAIL " + String.Join(",", ctx.Failures))
                1

/// T25 / EXT-DATA-013 / PF-014: connect to the historical catalog's repository and workspace with a credential and
/// load every case through the same program the application runs (no bundled record, no hardcoded id).
let private catalogLive (token: string) (repoFull: string) (branch: string) (workspace: string) =
    let secrets = [ token ]
    let say (v: JValue) = Console.Out.WriteLine(Sanitize.guard secrets (Json.canonicalText v))
    match repoFull.Split('/'), Credential.create token with
    | [| owner; repo |], Ok cred ->
        match Target.create owner repo branch workspace with
        | Error p ->
            Console.Out.WriteLine("RESULT: BLOCKED (" + String.Join(" ", p) + ")")
            2
        | Ok t ->
            let env = { Now = now; Nonce = Guid.NewGuid().ToString "N" }
            let sw = Stopwatch.StartNew()
            let m, refusals = runProgram env Sync.initial (Connect(t, cred))
            sw.Stop()
            say (JObj [ "check", JStr "catalog.connect"; "connection", JStr(Sync.connectionLabel m); "detail", JStr(Sync.connectionDetail m |> Option.defaultValue ""); "elapsedMs", JInt sw.ElapsedMilliseconds; "refusals", JInt(int64 refusals.Length) ])
            match m.Catalog with
            | CatalogVerified(man, results) ->
                for r in results do
                    match r.Outcome with
                    | Ok v ->
                        say (
                            JObj
                                [ "case", JStr r.Case.CaseKey
                                  "investigationId", JStr v.InvestigationId
                                  "title", JStr v.Title
                                  "version", JInt v.Version
                                  "lifecycle", JStr v.Lifecycle
                                  "stateSha256", JStr v.StateSha256
                                  "expectedStateSha256", JStr r.Case.ExpectedStateSha256
                                  "readOnly", JBool(m.Documents |> Map.tryFind v.InvestigationId |> Option.exists (fun d -> d.ReadOnly))
                                  "replayedBy", JStr "engine Storage.read (History.validate, stage 15)" ]
                        )
                    | Error e -> say (JObj [ "case", JStr r.Case.CaseKey; "error", JStr e.Code ])
                let allOk = results |> List.forall (fun r -> Result.isOk r.Outcome)
                let onlyCatalog = m.Documents |> Map.forall (fun _ d -> d.ReadOnly)
                say (JObj [ "catalogId", JStr man.CatalogId; "catalogVersion", JStr man.CatalogVersion; "cases", JInt(int64 results.Length) ])
                if allOk && onlyCatalog && results.Length = 3 && Sync.connectionLabel m = "connected" then
                    Console.Out.WriteLine "RESULT: PASS (live catalog load; digests and replayed state digests match the catalog manifest; every case admitted by the engine's History.validate)"
                    0
                else
                    Console.Out.WriteLine "RESULT: FAIL"
                    1
            | other ->
                say (JObj [ "catalog", JStr(sprintf "%A" other |> fun s -> s.Split('\n')[0]) ])
                Console.Out.WriteLine "RESULT: FAIL (the catalog was not verified)"
                1
    | _, Error _ ->
        Console.Out.WriteLine "RESULT: BLOCKED (no usable credential: every connection needs one, DEC-025)"
        2
    | _ ->
        Console.Out.WriteLine "RESULT: BLOCKED (catalog repository is not owner/repo)"
        2

[<EntryPoint>]
let main argv =
    match argv with
    | [| "record" |] ->
        let env name = Environment.GetEnvironmentVariable name |> Option.ofObj |> Option.defaultValue ""
        let token = env "CAUSA_GITHUB_TOKEN"
        // The test repository: the secret first, then the repository variable; never a built-in default.
        let fromSecret = env "CAUSA_TEST_REPO_SECRET"
        let fromVar = env "CAUSA_TEST_REPO_VAR"
        let repo, source = if fromSecret <> "" then fromSecret, "secret" elif fromVar <> "" then fromVar, "variable" else "", "none"
        let runId = env "GITHUB_RUN_ID"
        Console.Out.WriteLine(if repo = "" then "test repo configured: no" else "test repo configured: yes (from the " + source + ")")
        Console.Out.WriteLine(if token = "" then "test token configured: no" else "test token configured: yes")
        if String.IsNullOrEmpty token || String.IsNullOrEmpty repo then
            Console.Out.WriteLine "RESULT: BLOCKED (CAUSA_GITHUB_TOKEN or CAUSA_TEST_REPO is not available; a missing secret is never a pass)"
            2
        elif repo.Equals("kemiller2002/causa-data", StringComparison.OrdinalIgnoreCase) then
            Console.Out.WriteLine "RESULT: BLOCKED (the recorder never writes to the canonical data repository)"
            2
        else
            recordAll token repo (if String.IsNullOrEmpty runId then "local" else runId)
    | [| "catalog" |] ->
        let env name fallback = Environment.GetEnvironmentVariable name |> Option.ofObj |> Option.filter (fun s -> s <> "") |> Option.defaultValue fallback
        catalogLive (env "CAUSA_CATALOG_TOKEN" "") (env "CAUSA_CATALOG_REPO" "kemiller2002/causa-data") (env "CAUSA_CATALOG_BRANCH" "main") (env "CAUSA_CATALOG_WORKSPACE" "historical-v1")
    | _ ->
        eprintfn "usage: causa-persistence record | catalog"
        64
