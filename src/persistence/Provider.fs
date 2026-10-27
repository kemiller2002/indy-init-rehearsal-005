namespace Causa.Persistence

open System
open System.Text.RegularExpressions

/// The data repository and workspace the person explicitly selected (EXT-DATA-003). It carries no credential, and
/// nothing in it is inferred from a credential (EXT-DATA-003-O2).
type Target =
    { Owner: string
      Repo: string
      Branch: string
      /// The isolated workspace namespace: `workspaces/demo/<workspace>/` (CONTRACT §3, §9.4.4).
      Workspace: string }

[<RequireQualifiedAccess>]
module Target =
    // \z, not $ (CONTRACT §2.13).
    let private owner = Regex(@"^[A-Za-z0-9](?:[A-Za-z0-9-]{0,38})\z", RegexOptions.CultureInvariant)
    let private repo = Regex(@"^[A-Za-z0-9._-]{1,100}\z", RegexOptions.CultureInvariant)
    let private branch = Regex(@"^[A-Za-z0-9._-]+(?:/[A-Za-z0-9._-]+)*\z", RegexOptions.CultureInvariant)
    let private workspace = Regex(@"^[a-z0-9][a-z0-9-]{0,63}\z", RegexOptions.CultureInvariant)

    let branchValid (s: string) =
        branch.IsMatch s
        && not (s.Contains("..", StringComparison.Ordinal))
        && s.Split('/') |> Array.forall (fun seg -> not (seg.StartsWith('.')) && not (seg.EndsWith(".lock", StringComparison.Ordinal)))

    /// Validate an explicit selection. Every field is required and checked; nothing is defaulted.
    let create (o: string) (r: string) (b: string) (w: string) : Result<Target, string list> =
        let problems =
            [ if not (owner.IsMatch o) then "Owner is not a valid GitHub user or organization name."
              if not (repo.IsMatch r) || r = "." || r = ".." then "Repository is not a valid GitHub repository name."
              if not (branchValid b) then "Branch is not a valid branch name."
              if not (workspace.IsMatch w) then "Workspace must be lowercase letters, digits and '-', up to 64 characters." ]
        if problems.IsEmpty then Ok { Owner = o; Repo = r; Branch = b; Workspace = w } else Error problems

    let fullName (t: Target) = t.Owner + "/" + t.Repo
    let workspaceRoot (t: Target) = "workspaces/demo/" + t.Workspace
    let investigationsDir (t: Target) = workspaceRoot t + "/investigations"
    let investigationDir (t: Target) (investigationId: string) = investigationsDir t + "/" + investigationId

    /// A writer may touch only `workspaces/demo/<workspace>/investigations/<id>/{three files}` (§9.4.4).
    let pathAllowed (t: Target) (path: string) =
        let prefix = investigationsDir t + "/"
        path.StartsWith(prefix, StringComparison.Ordinal)
        && (let rest = path.Substring prefix.Length
            match rest.Split('/') with
            | [| id; file |] -> Uuid.isValid id && List.contains file FileSet.names
            | _ -> false)

/// A credential held only in memory (EXT-DATA-004, SECURITY-THREAT-MODEL). It is never serialized, logged, put in a
/// URL, a body, a view or a diagnostic: the only way out is the Authorization header value, which the effect runner
/// reads when it performs a request. Its text form is redacted, so `%A`/ToString of any state never shows it.
[<Sealed; AllowNullLiteral>]
type Credential(token: string) =
    member _.AuthorizationHeader = "Bearer " + token
    override _.ToString() = "[credential]"

[<RequireQualifiedAccess>]
module Credential =
    /// A token is accepted only as an opaque non-blank value without whitespace or control characters.
    let create (token: string) : Result<Credential, string> =
        if String.IsNullOrWhiteSpace token then Error "A credential is required."
        elif token |> Seq.exists (fun c -> Char.IsWhiteSpace c || Char.IsControl c) then Error "The credential contains characters a token cannot contain."
        else Ok(Credential token)

/// One HTTP exchange described as data. The Limen kernel performs it; this library never does I/O.
type HttpRequest =
    { Method: string
      Url: string
      /// Non-secret headers only. The credential travels separately and becomes the Authorization header alone.
      Headers: (string * string) list
      Body: string option
      /// A read of mutable state (a branch head): must never be answered from an HTTP cache (GitHub sends
      /// `Cache-Control: private, max-age=60` on such reads). The URL also carries a unique query value.
      Fresh: bool
      Credential: Credential option }

type HttpResponse =
    { Status: int
      /// Lower-case header names.
      Headers: (string * string) list
      Body: string }

/// How a request ended without a response.
type TransportFailure =
    /// The request was not sent (offline, DNS, refused before sending): GitHub certainly did not apply it.
    | NotSent of detail: string
    /// The request may have reached GitHub (timeout after sending, connection lost): outcome unknown.
    | OutcomeUnknown of detail: string
    | Cancelled

/// Provider failures, distinct from contract (domain-data) validation failures (PF-008-O2, PERSISTENCE §42).
type ProviderFailure =
    /// 401: the credential was rejected (expired, revoked, malformed).
    | Unauthorized
    /// 403 without rate limiting: the credential lacks permission for this operation.
    | Forbidden of detail: string
    | RateLimited of resetAt: string option
    /// 404: absent, or not visible to this credential (GitHub does not distinguish).
    | NotFound
    /// 422: the ref or commit does not exist (observed for missing branches/commits).
    | MissingRef of detail: string
    /// The guarded write's expected head was not the branch head: another writer moved the branch.
    | HeadMoved of detail: string
    | Unavailable of status: int
    | Offline of detail: string
    | Unknown of detail: string
    | Malformed of detail: string
    | Rejected of status: int * detail: string

[<RequireQualifiedAccess>]
module ProviderFailure =
    /// A short safe description. Built from fixed text and provider status; never from request data.
    let describe =
        function
        | Unauthorized -> "GitHub rejected the credential. Reconnect with a valid token."
        | Forbidden _ -> "The credential is not permitted to do this in the selected repository."
        | RateLimited _ -> "GitHub's rate limit is exhausted. Try again later."
        | NotFound -> "The repository, branch or path was not found, or is not visible to this credential."
        | MissingRef _ -> "The branch or commit does not exist."
        | HeadMoved _ -> "Another writer changed the branch."
        | Unavailable s -> sprintf "GitHub is unavailable (HTTP %d)." s
        | Offline _ -> "The network is unavailable."
        | Unknown _ -> "The request may or may not have reached GitHub."
        | Malformed _ -> "GitHub's answer could not be understood."
        | Rejected(s, _) -> sprintf "GitHub refused the request (HTTP %d)." s

    /// True when the provider certainly did not apply the request.
    let certainlyNotApplied =
        function
        | Unknown _ -> false
        | _ -> true
