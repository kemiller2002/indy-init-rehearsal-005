# Invariant inventory (T12)

Each invariant, where the kernel enforces it, and the test that proves it. Tests
live in `tests/App.Engine.Tests/`; each carries its VPO obligation ids in its name
and as `obligation` traits. The complete test → obligation list is generated into
[`interface-manifest.json`](interface-manifest.json) (`contractTests`).

## Write invariants (defect classes, R4-01: tests first)

The tests were committed failing first against a stub (`97859b8`: 16 of 16 T12
tests failing), then the implementation (`e8e32c1`). Each class has a negative
control: the same check run against a deliberately defective step must fail.

| Class | Invariant | Enforced in | Tests (`WriteInvariantTests.fs`) |
|---|---|---|---|
| DC-001 (DCT-001-A) | A publication is parented by the exact commit its change was decided at; a moved branch is a held conflict, never a fast-forward over unread history | `Sync.step` (`Edit` requires `DecidedAt = head`; `Publish` parent = `DecidedAt`; `StaleHead` → `Held Conflict`) | `DCT-001-A …`, negative control `… re-targets stale decisions` |
| DC-001 (DCT-001-B) | Reconciliation classifies by ancestry and change marker, never ref equality | `Sync.classify` | `DCT-001-B …`, negative control `… ref-equality reconciliation` |
| DC-002 (DCT-002-A) | An answer applies only to the request instance in flight | `Sync.step` (book-issued `RequestId`, `StaleAnswer`) | `DCT-002-A …`, negative control `… matched by kind` |
| Request identity (R1 M4 / R3 M1) | An answer to an earlier connection never lands on a newer one | `Sync.step` | `REQUEST-IDENTITY …` + negative control |
| DC-002 (DCT-002-B) | User events legal only in their source states; refusals are typed and change nothing; an unchanged resubmission keeps its parent (R3 D-04) | `Sync.step` | `DCT-002-B …` (3 tests) + negative control `… writes during an unknown outcome` |
| DC-003 (DCT-003-A) | After send, any failure is Unknown: no Retry, reconcile re-reads the head first; saved only after read-back | `Sync.step`, `Sync.canRetry`, `Sync.status` | `DCT-003-A …` (2 tests) + negative control `… stage-blind`; `UI-006 … distinct statuses` |
| DC-004, DC-005 | Advisory fields never authorize; mutable reads never cached | provider level (T18, persistence stream) | — |
| DC-006 | Bounded start, Retry reaches ready | browser level (T16, shell stream) | — |

## Locked domain invariants (DESIGN-DECISION-BOUNDARIES §1)

| Invariant | Enforced in | Tests |
|---|---|---|
| D-001 observation/inference separation | distinct record kinds; `InvestigationStarted` keeps the original observation | `T13 D-001 D-002 …` (`KernelTests.fs`) |
| D-002 provenance, verbatim text | `Text` is never trimmed; every entity carries `Provenance` | `T13 D-001 D-002 …`; `T13 UI-023 …` |
| D-003 unknown is not false | `UnknownState` stays `Identified` until an explicit transition; open material unknowns block the cause gate | `T13 D-007 D-003 …`; `T13 UI-004 …` (`CAUSE.OPEN_UNKNOWN`) |
| D-004 proposal authority | `Decide.decide`: agents may only `RecordProposal`; only humans accept | `T13 D-004 …`; `T13 … typed rejections …` |
| D-005 historical integrity | `History.validate` re-decides every group; `Storage.successor` refuses rewritten history | `T13 G3 …` (forged histories); `T15 … 90 negative fixtures …` |
| D-006 explicit transition legality | `Transitions.*` tables are the only copies; no generic state setter | `T13 … lifecycle commands …`; `T13 … hypothesis transitions …` |
| D-007 blocking obligations | `Policy.causeBlockers`, `Policy.resolutionBlockers` | `T13 D-007 D-003 …`; `T13 UI-004 …` |
| D-008 verification distinction | `VERIFICATION.NOT_AWAITING`, fresh-evidence rule, `RESOLVE.CORRECTION_NOT_VERIFIED` | `T13 D-008 …` |
| D-009 persistence truth | `Sync.status` (Saving / Verifying / MayHaveSaved / Conflicted / Failed are distinct; Clean only after read-back) | `DCT-003-A …`; `UI-006 …` |
| D-010..D-012 extensions | v1.0.0 has no extension namespace: unknown event types and fields fail closed | `T15 … 90 negative fixtures …` (`unknown-event-type`, `unexpected-payload-field`) |
| D-013 domain-neutral core | no domain-pack vocabulary in the kernel | by construction (manifest) |
| D-014 storage abstraction | the domain has no provider metadata; `Sync.Oid` never enters domain files | `T15 … writer reproduces …` (files carry no provider data) |
| D-015 AI provider abstraction | `ActorKind.Agent(provider, model)` is data only | `T13 D-004 …` |
