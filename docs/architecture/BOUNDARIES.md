# Kernel boundaries A–E (interface freeze, T14)

The authoritative interface manifest is
[`interface-manifest.json`](interface-manifest.json) (`causa.interface-manifest/1`,
manifest version 1.0.0). It is **extracted, not written**: the test
`T14 ... the committed interface manifest equals the extraction from the compiled kernel`
(`tests/App.Engine.Tests/InterfaceManifestTests.fs`) reflects over the compiled
`IndyInitRootCauseInvestigator.Engine` assembly (public types, union cases,
record fields, module function signatures), runs the kernel to produce the
request/response examples, lists every contract test with the VPO obligations it
proves, and fails if the committed file differs by one byte. Regenerate with
`CAUSA_WRITE_MANIFEST=1 dotnet test tests/App.Engine.Tests`.

The manifest's SHA-256 is recorded in the kernel stream's report for the ledger;
consumers sign off against that exact SHA. A breaking change needs a coordinator
decision and a new manifest version (REHEARSAL-AGENT-INTERFACE-FREEZE §5).

| Boundary | Entry points (module.function) | Owner | Consumers |
|---|---|---|---|
| A — command submission | `Kernel.submit : Session -> CommandEnvelope -> Result<Accepted, Rejection>`; `Kernel.ofLoaded`, `Kernel.replay`, `Kernel.code`, `Kernel.reasons`; `Faults.ofRejection` (Aegis) | core | shell (commands from the UI), persistence (append after accept) |
| B — versioned projection | `Projection.ofInvestigation : Actor -> DateTimeOffset -> DataIntegrity option -> Investigation -> InvestigationProjection`; `Projection.capabilities`, `Projection.obligations`, `Projection.verification`; `Policy.causeBlockers`, `Policy.resolutionBlockers` | core | shell (every screen) |
| C — replay and storage contract v1.0.0 | `Storage.read : string -> Map<string, byte[]> -> Result<LoadedInvestigation, StorageError>` (17 stages, contract §11.1); `Storage.write`, `Storage.append`, `Storage.successor`, `Storage.appendBytes`; `History.validate`; `Wire.*`; `Hashing.eventId` | core | persistence (GitHub/fixture adapters), shell (load after Connect) |
| D — persistence write invariants | `Sync.step : Sync.State -> Sync.Event -> Result<Sync.State * Sync.Effect list, Sync.Fault>`; `Sync.status`, `Sync.canRetry`, `Sync.classify` | core (invariants); persistence owns the provider port and adapters | persistence, shell (save status) |
| E — report | `Projection.report : Investigation -> ReportProjection` | core | shell / Folio |

## Rules every consumer relies on

- The engine is the only authority (DEC-001, PF-002). The UI renders
  `Capability` values and never decides legality; a blocked action carries
  every `DomainError` the engine gives. Agents may only `RecordProposal` (D-004).
- Every loaded history goes through `Storage.read` (or `Kernel.replay`), which
  re-decides each command group; there is no second validator (Boundary C).
- Writes: decide with `Kernel.submit` (expected version = the version read),
  serialise with `Storage.appendBytes`/`Storage.write`, verify with
  `Storage.append` exactly as a reader would, publish through `Sync.step`
  (`Publish` effect: parent = the commit the change was decided at, i.e.
  `expectedHeadOid`). Saved is claimed only after `ReadBack(_, true)`.
- `DomainVerification` (domain claims) and `DataIntegrity` (technical
  integrity) are disjoint (UI-023); never label an investigation "Verified"
  from integrity.
- Browser-safe: no platform crypto (managed SHA-256/SHA-1/UUIDv5), no
  `Environment` process APIs, no reflection-based printing on the hot path;
  the normative schemas are embedded resources (schema identity
  `0a68af8c…9152`, checked at test time).

## Storage contract identities (STORAGE-007)

| identity | SHA-256 |
|---|---|
| schema (`contract/v1.0.0/schemas/bundle.json`) | `0a68af8c93190d2e4d6c27beb2f8cb67c3aebdda586d01beb7122b71af179152` |
| dataset (`fixtures/v1.0.0/manifest.json`) | `f650a2e509a420872b46177b21f57ef6ebd7625ae4c69dd92198d3ca472c2b86` |
| release (`compatibility/v1.0.0/lock.json`) | `6a7e7802ff2a3ff500ab6065662514a96e09e6c15eb30212ba1377fcc5bf8fc5` |

The fixture replay tests read a causa-data checkout at test time
(`CAUSA_DATA_DIR`, or a sibling `causa-data/` directory). CI must provide one:
`kemiller2002/causa-data` is public; check out a commit at or after
`causa-storage-v1.0.0` that contains `catalog/historical-v1` (for example
`3d861bc`), whose frozen paths equal the tag.
