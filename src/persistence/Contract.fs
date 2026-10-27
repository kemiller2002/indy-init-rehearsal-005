namespace Causa.Persistence

open System
open System.Globalization
open System.Text
open System.Text.RegularExpressions

/// A contract refusal: the primary code of the first failing stage (causa-data CONTRACT §11) and a safe detail.
/// Details never contain credentials (they are built only from repository content and fixed text).
type ContractError =
    { Code: string
      Detail: string
      /// For CAUSA.HISTORY_INADMISSIBLE, the engine's domain code (§11.3).
      DomainCode: string option }

[<RequireQualifiedAccess>]
module Codes =
    let pathInvalid = "CAUSA.PATH_INVALID"
    let encoding = "CAUSA.ENCODING"
    let jsonMalformed = "CAUSA.JSON_MALFORMED"
    let nonCanonical = "CAUSA.NON_CANONICAL"
    let unsupportedVersion = "CAUSA.UNSUPPORTED_VERSION"
    let unknownEventType = "CAUSA.UNKNOWN_EVENT_TYPE"
    let schemaViolation = "CAUSA.SCHEMA_VIOLATION"
    let manifestMismatch = "CAUSA.MANIFEST_MISMATCH"
    let eventsHashMismatch = "CAUSA.EVENTS_HASH_MISMATCH"
    let investigationMismatch = "CAUSA.INVESTIGATION_MISMATCH"
    let eventIdDuplicate = "CAUSA.EVENT_ID_DUPLICATE"
    let sequenceInvalid = "CAUSA.SEQUENCE_INVALID"
    let eventIdMismatch = "CAUSA.EVENT_ID_MISMATCH"
    let correlationReused = "CAUSA.CORRELATION_REUSED"
    let historyInadmissible = "CAUSA.HISTORY_INADMISSIBLE"
    let snapshotHashMismatch = "CAUSA.SNAPSHOT_HASH_MISMATCH"
    let snapshotMismatch = "CAUSA.SNAPSHOT_MISMATCH"
    let versionStale = "CAUSA.VERSION_STALE"
    let historyRewritten = "CAUSA.HISTORY_REWRITTEN"
    let unrepresentable = "CAUSA.UNREPRESENTABLE"
    /// Catalog level (historical catalog manifest vs the investigation read from the repository).
    let catalogDrift = "CAUSA.FIXTURE_DRIFT"

[<RequireQualifiedAccess>]
module ContractError =
    let make code detail = { Code = code; Detail = detail; DomainCode = None }
    let fail code detail : Result<'T, ContractError> = Error(make code detail)

/// Contract identity (CONTRACT §1, header).
[<RequireQualifiedAccess>]
module ContractVersion =
    let formatVersion = "1.0.0"
    let schemaVersion = "1.0.0"
    let semanticVersion = "1.0.0"

[<RequireQualifiedAccess>]
module Uuid =
    let private pattern =
        Regex(@"^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\z", RegexOptions.CultureInvariant)

    let nil = "00000000-0000-0000-0000-000000000000"

    /// Lowercase canonical, non-nil (CONTRACT §2.8). `\z`, not `$` (§2.13).
    let isValid (s: string) = pattern.IsMatch s && s <> nil

    /// RFC 4122 name-based UUID (version 5), computed by the engine's managed SHA-1 (browser-safe).
    let v5 (ns: string) (name: string) =
        IndyInitRootCauseInvestigator.Engine.Hashing.uuidV5(System.Guid ns) name |> fun g -> g.ToString("D")

    /// N = UUIDv5(NAMESPACE_URL, "https://github.com/kemiller2002/causa-data/contract/v1/event-id") (§5.2).
    let eventIdNamespace = "241fefd4-c810-5a3a-9cfa-4bbd968ae1cb"

    /// eventId = UUIDv5(N, investigationId + "/" + aggregateVersion + "/" + correlationId) (§5.2).
    let eventId (investigationId: string) (aggregateVersion: int64) (correlationId: string) =
        v5 eventIdNamespace (investigationId + "/" + aggregateVersion.ToString(CultureInfo.InvariantCulture) + "/" + correlationId)

[<RequireQualifiedAccess>]
module Timestamp =
    let private pattern =
        Regex(@"^[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}\.[0-9]{3}Z\z", RegexOptions.CultureInvariant)

    /// RFC 3339 UTC, exactly three fractional digits, a real instant in 0001–9999 (§2.9).
    let isValid (s: string) =
        pattern.IsMatch s
        && fst (
            DateTime.TryParseExact(
                s,
                "yyyy-MM-dd'T'HH:mm:ss.fff'Z'",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal ||| DateTimeStyles.AssumeUniversal
            )
        )

    /// Millisecond-truncated UTC text; the command boundary must supply this form (§2.9).
    let ofDateTimeOffset (d: DateTimeOffset) =
        d.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture)

[<RequireQualifiedAccess>]
module Digest =
    let private sha256Pattern = Regex(@"^[0-9a-f]{64}\z", RegexOptions.CultureInvariant)
    let isSha256 (s: string) = sha256Pattern.IsMatch s
