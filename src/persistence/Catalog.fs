namespace Causa.Persistence

open System.Text.RegularExpressions

/// The active catalog pointer (`catalog/active.json` in the data repository).
type CatalogPointer =
    { ActiveCatalog: string
      CatalogId: string
      CatalogVersion: string
      ManifestPath: string
      Workspace: string }

/// One case the catalog manifest promises: identity and the digests the replay must reproduce.
type CatalogCase =
    { CaseKey: string
      InvestigationId: string
      Title: string
      Path: string
      ExpectedVersion: int64
      ExpectedLifecycle: string
      ExpectedStateSha256: string
      EventsSha256: string
      ManifestSha256: string
      SnapshotSha256: string }

type CatalogManifest =
    { CatalogId: string
      CatalogVersion: string
      FormatVersion: string
      Workspace: string
      Cases: CatalogCase list }

/// The outcome of checking what was loaded from the workspace against the manifest (EXT-DATA-013).
type CatalogCaseResult =
    { Case: CatalogCase
      Outcome: Result<Verified, ContractError> }

/// Reads the historical catalog at runtime (T25). Nothing here names a case or an investigation id: every id comes
/// from the repository's own manifest, and every record from the repository (no bundling, EXT-DATA-001/013).
[<RequireQualifiedAccess>]
module Catalog =
    let pointerPath = "catalog/active.json"

    let private manifestPattern = Regex(@"^catalog/[a-z0-9][a-z0-9-]{0,63}/manifest\.json\z", RegexOptions.CultureInvariant)
    let private workspacePattern = Regex(@"^workspaces/demo/[a-z0-9][a-z0-9-]{0,63}\z", RegexOptions.CultureInvariant)

    let private bad detail : Result<'T, ContractError> = ContractError.fail Codes.schemaViolation ("catalog: " + detail)

    let private parseText (bytes: byte[]) =
        match Json.decodeUtf8 bytes with
        | Error e -> ContractError.fail Codes.encoding ("catalog: " + e)
        | Ok text ->
            match Json.parse (text.TrimEnd('\n')) with
            | Ok v -> Ok v
            | Error e -> ContractError.fail Codes.jsonMalformed ("catalog: " + e)

    let parsePointer (bytes: byte[]) : Result<CatalogPointer, ContractError> =
        match parseText bytes with
        | Error e -> Error e
        | Ok(JObj o) ->
            match Json.str "activeCatalog" o, Json.str "catalogId" o, Json.str "catalogVersion" o, Json.str "manifest" o, Json.str "workspace" o with
            | Some a, Some c, Some v, Some m, Some w when manifestPattern.IsMatch m && workspacePattern.IsMatch w ->
                Ok { ActiveCatalog = a; CatalogId = c; CatalogVersion = v; ManifestPath = m; Workspace = w }
            | _ -> bad "active.json lacks a valid manifest path or workspace"
        | Ok _ -> bad "active.json is not an object"

    let private parseCase (workspace: string) (v: JValue) : Result<CatalogCase, ContractError> =
        match v with
        | JObj o ->
            let s k = Json.str k o
            match s "caseKey", s "investigationId", s "title", s "path", Json.int "expectedVersion" o, s "expectedLifecycle" with
            | Some key, Some id, Some title, Some path, Some version, Some lifecycle ->
                let digests = [ "expectedStateSha256"; "eventsSha256"; "manifestSha256"; "snapshotSha256" ] |> List.map s
                match digests with
                | [ Some st; Some ev; Some mf; Some sn ] when digests |> List.forall (Option.exists Schema.isSha256) ->
                    if not (Uuid.isValid id) then bad "case investigationId is not a canonical UUID"
                    elif path <> workspace + "/investigations/" + id then bad "case path is not inside the catalog workspace"
                    else
                        Ok
                            { CaseKey = key
                              InvestigationId = id
                              Title = title
                              Path = path
                              ExpectedVersion = version
                              ExpectedLifecycle = lifecycle
                              ExpectedStateSha256 = st
                              EventsSha256 = ev
                              ManifestSha256 = mf
                              SnapshotSha256 = sn }
                | _ -> bad "case digests are missing or malformed"
            | _ -> bad "case lacks a required field"
        | _ -> bad "case is not an object"

    let parseManifest (pointer: CatalogPointer) (bytes: byte[]) : Result<CatalogManifest, ContractError> =
        match parseText bytes with
        | Error e -> Error e
        | Ok(JObj o) ->
            match Json.str "workspace" o, Json.field "cases" o, Json.str "formatVersion" o with
            | Some w, _, _ when w <> pointer.Workspace -> bad "manifest workspace differs from the pointer"
            | Some _, _, Some fv when fv <> ContractVersion.formatVersion -> ContractError.fail Codes.unsupportedVersion "catalog: manifest formatVersion is not 1.0.0"
            | Some w, Some(JArr cases), Some fv ->
                match cases |> traverse (parseCase w) with
                | Error e -> Error e
                | Ok cs when cs.IsEmpty -> bad "manifest lists no cases"
                | Ok cs when (cs |> List.map (fun c -> c.InvestigationId) |> List.distinct).Length <> cs.Length -> bad "manifest lists an investigation twice"
                | Ok cs ->
                    Ok
                        { CatalogId = Json.str "catalogId" o |> Option.defaultValue pointer.CatalogId
                          CatalogVersion = Json.str "catalogVersion" o |> Option.defaultValue pointer.CatalogVersion
                          FormatVersion = fv
                          Workspace = w
                          Cases = cs }
            | _ -> bad "manifest lacks workspace, formatVersion or cases"
        | Ok _ -> bad "manifest is not an object"

    let private drift detail : Result<Verified, ContractError> = ContractError.fail Codes.catalogDrift detail

    /// Check one loaded investigation against its manifest entry: every digest, the replayed state digest, the
    /// version, lifecycle and title (CONTRACT §4 FIXTURE_DRIFT semantics applied to the catalog).
    let checkCase (c: CatalogCase) (loaded: Result<Verified, ContractError> option) : CatalogCaseResult =
        let outcome =
            match loaded with
            | None -> ContractError.fail Codes.pathInvalid ("catalog case " + c.CaseKey + " is missing from the workspace")
            | Some(Error e) -> Error e
            | Some(Ok v) ->
                if v.EventsSha256 <> c.EventsSha256 then drift (c.CaseKey + ": eventsSha256 differs from the catalog manifest")
                elif v.ManifestSha256 <> c.ManifestSha256 then drift (c.CaseKey + ": manifestSha256 differs from the catalog manifest")
                elif v.SnapshotSha256 <> c.SnapshotSha256 then drift (c.CaseKey + ": snapshotSha256 differs from the catalog manifest")
                elif v.StateSha256 <> c.ExpectedStateSha256 then drift (c.CaseKey + ": the replayed state does not reach expectedStateSha256")
                elif v.Version <> c.ExpectedVersion then drift (c.CaseKey + ": version differs from expectedVersion")
                elif v.Lifecycle <> c.ExpectedLifecycle then drift (c.CaseKey + ": lifecycle differs from expectedLifecycle")
                elif v.Title <> c.Title then drift (c.CaseKey + ": title differs from the catalog manifest")
                else Ok v
        { Case = c; Outcome = outcome }
