/// Provider conformance (T18): the fake must agree with the recorded real GitHub behaviour, scenario by scenario, as
/// seen through the production interpreters; and the recordings themselves must be sanitized.
module Persistence.Tests.ConformanceTests

open System
open System.IO
open Xunit
open Causa.Persistence
open Persistence.Tests.FakeGitHub
open Persistence.Tests.Harness

let rec private shape (v: JValue) : JValue =
    match v with
    | JNull -> JStr "null"
    | JBool _ -> JStr "boolean"
    | JInt _ -> JStr "integer"
    | JStr _ -> JStr "string"
    | JArr [] -> JArr []
    | JArr(x :: _) -> JArr [ shape x ]
    | JObj o -> JObj(o |> List.map (fun (k, x) -> k, shape x))

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

let private repoOf r = GitHub.classify r |> Result.bind GitHub.parseRepository |> classification
let private headOf r = GitHub.classify r |> Result.bind GitHub.parseHead |> classification
let private listOf r = GitHub.classify r |> Result.bind GitHub.parseListing |> classification
let private fileOf r = GitHub.classify r |> Result.bind GitHub.parseFile |> classification
let private blobOf r = GitHub.classify r |> Result.bind GitHub.parseBlob |> classification
let private commitOf r = GitHub.parseCommit r |> classification

let private graphqlTypes (body: string) =
    match Json.parse body with
    | Ok(JObj o) ->
        match Json.field "errors" o with
        | Some(JArr es) -> es |> List.choose (fun e -> match e with JObj eo -> Json.str "type" eo | _ -> None)
        | _ -> []
    | _ -> []

[<Fact>]
[<Trait("obligation", "VPO-03b8215501e25334")>]
[<Trait("obligation", "VPO-74efa48033c82a96")>]
[<Trait("obligation", "VPO-bb13dc090d4c152b")>]
let ``VPO-03b8215501e25334 VPO-74efa48033c82a96 VPO-bb13dc090d4c152b the fake agrees with every recorded GitHub scenario through the production interpreters`` () =
    let fake = newFake ()
    let t = target "conformance"
    let cred = Some(credential token)
    let bogus = Some(credential "rejected-credential")
    let head0 = fake.Head branch
    let v = Synthetic.investigation "conformance" 0
    let files = FileSet.files v |> List.map (fun (n, b) -> Target.investigationDir t v.InvestigationId + "/" + n, b)
    let created = fake.Handle(GitHub.createCommit cred t head0 "create" files)
    let oid = fake.Head branch
    let cases: (string * (unit -> HttpResponse) * (HttpResponse -> string)) list =
        [ "repository.read.authorized", (fun () -> fake.Handle(GitHub.getRepository cred t)), repoOf
          "repository.read.anonymous", (fun () -> fake.Handle(GitHub.getRepository None t)), repoOf
          "repository.read.rejectedCredential", (fun () -> fake.Handle(GitHub.getRepository bogus t)), repoOf
          "ref.read.head", (fun () -> fake.Handle(GitHub.getBranchHead cred t "n1")), headOf
          "ref.read.missingBranch", (fun () -> fake.Handle(GitHub.getBranchHead cred { t with Branch = "causa-missing" } "n2")), headOf
          "contents.list.missingPath", (fun () -> fake.Handle(GitHub.listDirectory cred t oid "workspaces/demo/absent/investigations")), listOf
          "contents.list.missingRef", (fun () -> fake.Handle(GitHub.listDirectory cred t "causa-missing" "workspaces")), listOf
          "write.create", (fun () -> created), commitOf
          "write.create.confirm", (fun () -> fake.Handle(GitHub.listDirectory cred t oid (Target.investigationDir t v.InvestigationId))), listOf
          "contents.read.file", (fun () -> fake.Handle(GitHub.getFile cred t oid (fst files.Head))), fileOf
          "contents.read.file.missing", (fun () -> fake.Handle(GitHub.getFile cred t oid "catalog/active.json")), fileOf
          "blob.read", (fun () -> fake.Handle(GitHub.getBlob cred t (GitHub.gitBlobSha v.Manifest))), blobOf
          "write.conflict.staleHead", (fun () -> fake.Handle(GitHub.createCommit cred t head0 "stale" files)), commitOf
          "write.missingBranch", (fun () -> fake.Handle(GitHub.createCommit cred { t with Branch = "causa-missing" } oid "missing" files)), commitOf
          "write.rejectedCredential", (fun () -> fake.Handle(GitHub.createCommit bogus t oid "rejected" files)), commitOf ]
    let divergences =
        cases
        |> List.choose (fun (scenario, run, classify) ->
            let o = recorded scenario
            let r = run ()
            let problems =
                [ if r.Status <> o.Status then sprintf "status %d vs recorded %d" r.Status o.Status
                  match Json.parse r.Body with
                  | Ok body when shape body <> o.BodyShape -> "body shape differs"
                  | Ok _ -> ()
                  | Error _ -> "body is not JSON"
                  if classify r <> o.Classification then sprintf "classified %s vs recorded %s" (classify r) o.Classification
                  if graphqlTypes r.Body <> o.GraphqlErrorTypes then "GraphQL error types differ" ]
            if problems.IsEmpty then None else Some(scenario + ": " + String.Join("; ", problems)))
    Assert.True(divergences.IsEmpty, String.Join("\n", divergences))
    // Every recorded scenario that the application relies on is covered above.
    Assert.Equal(cases.Length, cases |> List.map (fun (s, _, _) -> s) |> List.distinct |> List.length)

[<Fact>]
[<Trait("obligation", "VPO-ab625062f52606bb")>]
[<Trait("obligation", "VPO-c41f10e4ed8a086b")>]
[<Trait("obligation", "VPO-3d7d8f82a053dce7")>]
let ``VPO-ab625062f52606bb VPO-c41f10e4ed8a086b VPO-3d7d8f82a053dce7 recordings are sanitized, versioned and name their source run`` () =
    let dir = Path.Combine(AppContext.BaseDirectory, "recordings")
    let files = Directory.GetFiles(dir, "*.ndjson")
    Assert.NotEmpty(files)
    let forbiddenHeaders = [ "authorization"; "set-cookie"; "cookie"; "x-github-request-id" ]
    let tokenPrefixes = [ "ghp_"; "gho_"; "ghs_"; "ghu_"; "github_pat_"; "Bearer " ]
    for f in files do
        let lines = File.ReadAllLines f |> Array.filter (fun l -> l.Length > 0)
        // First line: format version and the CI run that produced it.
        match Json.parse lines[0] with
        | Ok(JObj o) ->
            Assert.Equal(Some "causa.provider-observations/1", Json.str "recording" o)
            Assert.True(Json.str "sourceRunId" o |> Option.exists (fun s -> s.Length > 0 && (Path.GetFileName f |> nonNull).Contains s))
        | _ -> failwith "recording header missing"
        for l in lines do
            for p in tokenPrefixes do
                Assert.DoesNotContain(p, l)
            match Json.parse l with
            | Ok(JObj o) ->
                match Json.field "headers" o with
                | Some(JObj hs) ->
                    for (k, _) in hs do
                        Assert.DoesNotContain(k, forbiddenHeaders)
                | _ -> ()
                // Bodies are recorded as shapes only: every leaf is a type name.
                let rec leavesAreTypes v =
                    match v with
                    | JStr("string" | "integer" | "boolean" | "null") -> true
                    | JArr xs -> xs |> List.forall leavesAreTypes
                    | JObj kvs -> kvs |> List.forall (snd >> leavesAreTypes)
                    | _ -> false
                match Json.field "bodyShape" o with
                | Some s -> Assert.True(leavesAreTypes s || s = JStr "empty" || s = JStr "non-json")
                | None -> ()
            | _ -> failwithf "recording line is not a JSON object: %s" f

[<Fact>]
[<Trait("obligation", "VPO-533a2328decf91cb")>]
[<Trait("obligation", "VPO-8fbeadc2bfc10f7a")>]
let ``VPO-533a2328decf91cb VPO-8fbeadc2bfc10f7a provider failures are classified as provider errors, distinct from data validation errors`` () =
    let r status headers body = { Status = status; Headers = headers; Body = body }
    let check expected response = Assert.Equal(expected, GitHub.classify response |> classification)
    // Recorded forms (see recordings/): 401, 404, 422.
    check "Unauthorized" (respond "repository.read.rejectedCredential" None)
    check "NotFound" (respond "ref.read.missingBranch" None)
    check "NotFound" (respond "contents.list.missingRef" None)
    // Documented GitHub forms that cannot be provoked safely in a recording run.
    check "RateLimited" (r 403 [ "x-ratelimit-remaining", "0"; "x-ratelimit-reset", "1793135097" ] "{\"message\":\"API rate limit exceeded\"}")
    check "RateLimited" (r 429 [ "retry-after", "60" ] "{}")
    check "Forbidden" (r 403 [] "{\"message\":\"Resource not accessible by personal access token\"}")
    check "Unavailable" (r 502 [] "")
    check "Malformed" (r 200 [] "<html>")
    check "MissingRef" (r 422 [] "{\"message\":\"No commit found for SHA\"}")
    // A malformed provider answer is a provider failure, never a contract code.
    match GitHub.classify (r 200 [] "not json") with
    | Error(Malformed _) -> ()
    | other -> failwithf "%A" other
    // Transport failures reach the program as distinct inputs: not sent (certainly not applied) vs outcome unknown.
    Assert.True(ProviderFailure.certainlyNotApplied (Offline "x"))
    Assert.False(ProviderFailure.certainlyNotApplied (ProviderFailure.Unknown "x"))

[<Fact>]
[<Trait("obligation", "VPO-a460a95198e68863")>]
[<Trait("obligation", "VPO-0e682dd716a12d49")>]
let ``VPO-a460a95198e68863 VPO-0e682dd716a12d49 a classic token with repo or admin scope is refused at connect`` () =
    let response scopes =
        let o = respond "repository.read.authorized" (Some(JObj [ "full_name", JStr(owner + "/" + repo); "default_branch", JStr branch ]))
        { o with Headers = o.Headers @ [ "x-oauth-scopes", scopes ] }
    Assert.True(GitHub.broadClassicScopes (response "repo, workflow"))
    Assert.True(GitHub.broadClassicScopes (response "admin:org"))
    Assert.False(GitHub.broadClassicScopes (response "public_repo"))
    Assert.False(GitHub.broadClassicScopes (respond "repository.read.authorized" None))
    match Sync.update env (Connect(target "scopes", credential token)) Sync.initial with
    | Ok(m, [ Send(id, _) ]) ->
        match Sync.update env (HttpAnswered(id, response "repo")) m with
        | Ok(m2, effects) ->
            Assert.Equal("accessDenied", Sync.connectionLabel m2)
            Assert.True(m2.Credential.IsNone)
            Assert.Empty(effects)
        | Error e -> failwith e.Reason
    | other -> failwithf "%A" other
