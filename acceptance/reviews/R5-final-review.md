# Rehearsal 5: fresh reviewer before the final verdict (AR-008, T33)

| | |
| --- | --- |
| Candidate SHA | `be0185128beb3143c9b6be9ad133d2f7dc5e7007` (origin/main, "Praxis: reconcile the ux.spec.js port fix under WI-0007"). It differs from `ad17e79b72c5b06b031c498375af1266cbb5ce86` only in `tests/browser/ux.spec.js` (default test-site port 4322 to 4822) and one `.ros/events/events.jsonl` line; `src/` and every persistence test are byte-identical. |
| Reviewer | fresh review agent; did not build any of the reviewed code and did not do the earlier review (`R5-independent-review.md`, whose claims were treated as claims to check) |
| Branch | `r5/review2` (this file and `acceptance/defect-classes.json` only; no product code, no tests, no `.ros/` or `.praxis/` changes) |
| Requirements | Indy-init `requirements-compiled/v1` (plan `VP-e77a2134e1b0`; `curation/defect-class-tests.json`, `curation/tasks.json` T30-T34), `docs/DEFECT-CLASS-CLOSURE-AND-RELEASE-GATES.md`, `docs/AGENT-RELIABILITY-REQUIREMENTS.md` (AR-008); in this repository `docs/PERSISTENCE-CONTRACT.md`, `docs/architecture/BOUNDARIES.md`, `docs/architecture/INVARIANTS.md`, `acceptance/README.md` |
| Candidate CI | Build and test run [40615707138](https://github.com/kemiller2002/indy-init-rehearsal-005/actions/runs/40615707138) at `be01851`. When this review was written (01:50Z), its `core` job had completed successfully at `be0185128beb…`: Acceptance 32, App.Engine 71, Persistence 60 and Causa.App 72 passed, 0 failed, 0 skipped. Its browser, live and acceptance jobs were still running, so the defect-class producer at this SHA is not cited. The previous main run [40615430960](https://github.com/kemiller2002/indy-init-rehearsal-005/actions/runs/40615430960) at `ad17e79` (same `src/`) finished with failure because of the ux.spec port, but its `live` job passed all 3 `@live` tests, including the new DCT-LIVE-A two-tab variant. |

## Method

1. Read the F-1/F-2 fix (`5336ec4`, failing-first tests `81c8ae9`, nonce `efdfa27`, live two-tab variant `3627f92`) against
   the engine's request book (`src/engine/Sync.fs`: `classify`, `Reconciling`) and the outer program
   (`src/persistence/Sync.fs`: `markerFor`, `BookCompare` -> `BookReconcileRead`, `bookEffects`; `GitHub.parseCommit`,
   `GitHub.classify`; `src/app/Dispatch.fs` nonce; `tests/Persistence.Tests/Harness.fs` per-page nonces).
2. Ran `dotnet test App.slnx -c Release` at the candidate's source (identical to `ad17e79`): 32 + 71 + 60 + 72 = 235
   passed, 0 failed.
3. Tried to break the fix with 11 reviewer scenarios (V1-V11) against the real `Sync.update` program and the
   recorded-behaviour fake, in a throwaway `git archive` copy (a scratch `ReviewTests.fs`; the scratch harness only gained
   a setter on `App.Store` to model one browser's shared `localStorage`). Nothing was committed.
4. Mutation runs in the same copy, against the committed `DcTableTests`:
   - M1: reconcile skips the read-back (trust the marker): 3 committed tests fail (forged marker, read-back fails,
     applied-then-lost read-back). Killed.
   - M2: marker without the page nonce (`<id>-<counter>`, the pre-fix form): the marker-uniqueness test fails. Killed.
     The two-device F-1 test still passes under M2 because the read-back alone catches it (defence in depth).
5. Defect classes judged against DCC-004/DCC-005: (a) original reproduction, (b) adversarial variants across the class,
   (c) enforcement at the authority boundary, (d) independent review, all SHA-bound to the candidate.
6. Spot-checked 8 obligations that were not among the earlier review's 14 and that the candidate's tags bind as
   proven, reading each bound test against the obligation text in `verification-plan.json`.

## Findings

### R2-F1 (high): two tabs of one browser share a single local-store key, so a change that exists only on this device is silently lost

The persistence program writes its whole model to one `localStorage` key (`Sync.storageKey = "causa.persistence/2"`,
`persistLocal` -> `StoreLocal(writeId, storageKey, record m)`), and nothing merges or listens for the other tab's writes.
Two tabs of the same origin therefore overwrite each other's record (last writer wins). A change that was never sent
(offline, "queued") or whose outcome is unknown lives only in that record; when its tab is closed or reloaded, the
restore reads the other tab's record and the change is gone, with no message. This is the local half of the same
two-writer situation F-1 was about (D-009 / contract §19-§21: "a user should be able to tell whether a change exists
only on the device"; restart recovery must "recover queued commands").

Reproduction (scratch test; `b.Store <- a.Store` models the one `localStorage` both tabs see):

```fsharp
let a = App(fake)
let b = App(fake)
a.Connect ws; b.Connect ws
a.Faults <- (fun r -> if r.Method = "POST" then NotSentFault else Deliver)
a.Step(Submit(hypothesis iid v.Version "offline in tab A")); a.Step(Save iid)   // A: queuedOffline
b.Store <- a.Store
b.Step(Submit(hypothesis iid v.Version "tab B edit"))                           // B persists its own model
a.Store <- b.Store
a.Reload()
// A's restored document no longer contains "offline in tab A"
```

Observed at the candidate's source: V11 "A before close=queuedOffline; after reopen has A's offline change=false".
V10 (A's write applied but its answer lost, so A is `unknown`): after A reopens, the restored document is B's
(`queuedOffline`), A's attempt and marker are gone, and the next connect shows a conflict against A's own commit.
Not observed in a real browser (time); the mechanism is a single key written whole by every tab, which a browser
shares across tabs of one origin. Suggested class-level prevention: per-tab or per-change keys merged on restore, or
a `storage`-event merge, plus a two-tab restore test.

### R2-F2 (medium): an HTTP status the program does not recognise, answered after the write was sent, is "not written" with Retry (DC-003)

F-2 was fixed only for GraphQL `errors[].type`. `GitHub.classify` still maps any status it does not list (400, 408,
409, 418, ...) to `Rejected(status, _)`, and the `BookPublish` handler turns that into
`PublicationRefused(Invalid)` -> `Held(NotWritten)` -> `canRetry = true`, even though the request was sent. 408 is
literally a timeout. DCT-003-A's invariant: "once the publication request has been sent, any failure ... is
'unknown', never 'not written'". 403/404/422 are likewise mapped to "not written" refusals; whether those are
documented as "not applied" for `createCommitOnBranch` is not recorded anywhere I could find.

Reproduction (scratch test): the fake applies the POST, then the client receives `{Status = 408; Body = {"message":"x"}}`.

| Status after the write was applied | Status shown | Save offered | Second Save |
| --- | --- | --- | --- |
| 400, 408, 409 | `failed` | yes | accepted; ends in `conflict` against this device's own commit |
| 403, 404, 422 | `failed` | yes | same |

No remote history is lost (`expectedHeadOid` refuses the retry), but the user is offered Retry for a write that may
have been applied and then shown a conflict with themselves. Variant V1: a 200 answer carrying both
`data.createCommitOnBranch.commit.oid` and an `errors` entry of a "not applied" type (`NOT_FOUND`, `UNPROCESSABLE`,
`FORBIDDEN`, `RATE_LIMITED`) is classified `failed` although the answer names the commit; a second Save is accepted and
sends a second POST. Suggested: after send, only statuses/types documented as "not applied" are refusals; everything
else is Unknown; an answer with a commit oid is never a refusal.

### Earlier findings re-checked

| Earlier finding | At the candidate |
| --- | --- |
| F-1 (critical) marker collision | **Fixed.** Marker = `<id>-<page-load nonce (full GUID)>-<counter>`; reconcile reads the investigation directory back at the marked commit and requires exactly the sent blob ids; mismatch -> conflict, read failure -> unknown. Adversarial variants V3-V9 (below) all hold; mutations M1 and M2 are killed. |
| F-2 (low) unrecognised GraphQL type | **Fixed for GraphQL types** (`Unknown`, committed test passes). The same class at HTTP-status level remains: R2-F2. |
| F-3 (low) public catalog needs a token | **Still present** (`Credential.create` rejects blank); not re-reported beyond this line. |
| F-4 (low) local build falls back to the committed traceability | **Still present** (`scripts/build-site.mjs` line 89). |
| F-5 (medium) closure evidence not bundled | **Fixed by reading**: `Ledger.fs` now emits `evidence/defects/<id>.json` for a reviewer-closed class only when the fix is an ancestor, the review file exists and every listed test passed in the run. Not observed in a CI run by me. |

### F-1 fix: what I tried and what happened

| # | Scenario | Result |
| --- | --- | --- |
| V3 | Two devices write byte-identical files at the same head; B's write lost; B reconciles. | B `conflict` (different markers, so not claimed). Conservative, no loss. |
| V4 | A's write lost, B's applied-then-lost; both reconcile; A saves again. | A `conflict`, B `remotelySynced` (read-back ok), A's Save refused; head has B, not A; A's change kept locally. Correct. |
| V5 | Reload while the reconcile read-back is in flight; late answer belongs to the old page. | After reload `unknown`; connect reconciles again and reaches `remotelySynced` (via read-back). |
| V6 | Reconnect while the read-back is held. | Connect refused ("still settling"); the held answer then lands and confirms. No stale answer accepted. |
| V7 | Read-back answers 401. | `unknown`, Save not offered, connection `accessDenied`. Correct. |
| V8 | Our write applied, then another writer rewrote our investigation; reconcile. | `conflict` (read-back at our commit ok, `Applied(ours, Some advanced)`; reload finds other bytes). Not claimed saved over the other writer. |
| V9 | F-1 with a reload: B's write lost, A saves, B reloads and connects. | B `conflict`; B's Save refused; head has A, not B. |
| committed | Two devices (F-1 original), forged marker with other bytes, read-back failure, applied-then-lost, unrecognised GraphQL type, marker uniqueness across tabs and reload. | All pass at the candidate's source. |
| live | `DCT-LIVE-A` two-tab variant (`3627f92`): tab B's GraphQL and branch-head requests reset, tab A saves, B's "Check now" must show a conflict and the branch must hold A's change only. | Passed against real GitHub in run 40615430960 (live job, at `ad17e79`, same `src/` and same spec as the candidate). The live job at `be01851` had not run when this was written. |

Residual, not a finding: a page-load nonce from `Guid.NewGuid()` (122 random bits) is unguessable for a non-malicious
writer; markers are public in commit messages, but a forged marker now also needs identical bytes, which is harmless.

## Defect classes (T32/T33)

Closure follows DCC-004/DCC-005. `fixSha` is the first commit at which both the enforcement and the class's tests
existed and is an ancestor of the candidate (`git merge-base --is-ancestor`). Earlier closures were re-checked against
what changed between `fbe4d87` and the candidate (`src/app/Dispatch.fs`, `src/kernel/causa.css`, `src/kernel/index.html`,
`src/persistence/{Sync,GitHub}.fs`).

| Class | Decision | fixSha |
| --- | --- | --- |
| DC-001 | PASS (closed) | `5336ec40372323dbdbbb821878ad87c51597af7a` |
| DC-002 | PASS (still holds) | `e8e32c1ebf6db77344cb8e8bc5bd30b422cdd379` |
| DC-003 | **OPEN** | — |
| DC-004 | PASS (still holds) | `895fb6da3031da689cd3a6f54b4005f17f07edbe` |
| DC-005 | PASS (still holds) | `895fb6da3031da689cd3a6f54b4005f17f07edbe` |
| DC-006 | PASS (still holds) | `578ebd3b0bbc99862dc2da945e9c3b9e3f719bba` |
| R3-LESSON-3 | PASS (still holds) | `578ebd3b0bbc99862dc2da945e9c3b9e3f719bba` |
| DC-001..DC-005 (DCT-LIVE-A) | **OPEN** | — |

<a id="dc-001"></a>
### DC-001: closed

- (a) Original reproduction: `DcTableTests` "DCT-001-B DCT-003-A … two devices: a lost write is never reconciled as applied
  because another device's commit followed the same head" is the earlier F-1 reproduction. It was committed failing first
  in `81c8ae9` and passes from `5336ec4`. Also passing: the original `DCT-001-A` (parented by the decided-at commit; a
  moved branch is a held conflict without retry) and `DCT-001-B` (another writer's commit is concurrent, by ancestry and
  marker), at engine and application level.
- (b) Adversarial variants: committed tests cover a forged marker with other bytes, uniqueness of markers across
  tabs/devices and after a reload, an applied-then-lost write confirmed only after its read-back, and a failed read-back
  staying unknown. My scenarios V3-V9 cover same content, crossed loss, reload or reconnect mid read-back, a 401 read-back,
  another writer after ours, and the reload variant. All hold. Live: the DCT-LIVE-A two-tab variant against real GitHub
  (run 40615430960, `ad17e79`, identical `src/`).
- (c) Enforcement at the authority boundary: `Sync.fs` `BookCompare` sends `BookReconcileRead`, so the book sees
  `Descends` with our marker only after the listing at the marked commit equals the sent blob ids. `markerFor` carries
  the page-load nonce. Mutations M1 (skip the read-back) and M2 (counter-only marker) are each killed by committed tests.
- (d) This review. R2-F1 (two tabs share one local-store key) is a different invariant: local durability, not remote
  history. It overwrites no remote history and so does not keep DC-001 open. It is reported as its own high finding.
- SHA-bound: run 40615707138, `core` job 122582109805, at `be0185128beb3143c9b6be9ad133d2f7dc5e7007`: Persistence
  60/60 and App.Engine 71/71 passed, which includes every DCT-001-A/B test above. `fixSha` `5336ec4` is an ancestor of
  the candidate.

<a id="dc-003"></a>
### DC-003: OPEN

The F-1 half of the earlier reopening is fixed: the reconcile path no longer claims saved without a read-back (M1 is
killed by three committed tests, and V5-V9 hold). F-2 is fixed for GraphQL error types. But the class invariant
("once sent, any failure is unknown, never not written") is broken by an adversarial variant across the class:
R2-F2 (an unrecognised or ambiguous HTTP status after send, including 408, is `Held(NotWritten)` with Retry; a 200
answer naming the commit plus a typed error is `failed`). DCC-004 forbids closing on the original examples alone.
Close when after-send classification treats every answer not documented as "not applied" as Unknown, with R2-F2's
table as tests.

<a id="dct-live-a"></a>
### DC-001..DC-005 (DCT-LIVE-A): OPEN

The live scenarios, including the new two-tab F-1 variant, passed against real GitHub (see above). The composite
class still covers DC-003, which stays open (R2-F2), so it cannot be closed. Close together with DC-003.

### Earlier closures re-confirmed at the candidate

- DC-002: `Sync.step`'s request identity and the outer `InFlight` map are unchanged; the new `BookReconcileRead`
  purpose carries the book's `RequestId` and is kept/filtered like the other book reads (V6: a held answer across a
  reconnect lands only on its own request). DCT-002-A/B pass locally at the candidate's source. Still closed.
- DC-004: no `CanPush` gate added (grep); DCT-004-A passes. Still closed.
- DC-005: the nonce change (`efdfa27`) widens the page-load nonce to the full GUID; `getBranchHead`/`getRepository`
  still take nonce + request id; DCT-005-A passes; the live job's URL-uniqueness assertion now also covers tab B.
  Still closed.
- DC-006: `src/kernel/main.ts` is unchanged since `fbe4d87`. Still closed; locally at the candidate, chromium `shell.spec.js` (DCT-006-A and DCT-INPUT-A included), `fixture-mode.spec.js` and `forms.spec.js`: 30 passed, 0 failed (ports 5421/5422). The browser job of run 40615707138 had not finished when this was written.
- R3-LESSON-3: `src/kernel/index.html` changed (+167 lines). Re-checked statically: 28 of 32 `<input>`/`<textarea>`
  carry `data-bind-value`, and all 28 carry `data-on="input"`. Still closed; locally at the candidate, chromium `shell.spec.js` (DCT-006-A and DCT-INPUT-A included), `fixture-mode.spec.js` and `forms.spec.js`: 30 passed, 0 failed (ports 5421/5422). The browser job of run 40615707138 had not finished when this was written.

## Over-tagging (traceability spot-check)

Eight obligations not in the earlier 14, each bound by tags at the candidate:

| # | Obligation | Bound test | Verdict | Why |
| --- | --- | --- | --- | --- |
| P-1 | VPO-cb75698b278332f5 UI-011-O4 "Preserve focus and scroll context during transitions and WebAssembly handoff" | `shell.spec.js` "the branded shell shows before the WebAssembly runtime and hands off without a blank frame" | **over-tagged (partial)** | Checks only that focus lands on `#causa-ready` after the handoff. Scroll context is never checked, and no route transition is exercised. |
| P-2 | VPO-4c6f9ea0dcc0bd29 WDL-014-O1 "prove the application invokes the layout capability, records the pinned version/SHA, and passes fixture, accessibility, and deployed-browser tests" | `shell.spec.js` "the causal analysis renders in the real forma-workflow frame …" | **over-tagged (partial)** | Proves invocation of the packaged frame against the local test site. The pinned version is checked by a different, unbound test (`graph-layout.spec.js`), and nothing here runs against the deployed site. |
| P-3 | VPO-5c8d5917d17f8960 UI-015-O3 "Test on deployed exact SHA; mocked persistence MUST NOT be represented as live write verification" | `fixture-mode.spec.js` "UI-015-O3 mocked persistence is never represented as a live write …" | **over-tagged (partial)** | Proves the second sentence, with a negative control. It runs against the locally served test build, not the deployed exact SHA. |
| P-4 | VPO-6051ea46b65ebe27 UI-016-O11 "WCAG AA contrast for normal text (4.5:1), large text (3:1), non-text indicators (3:1)" | `shell.spec.js` "the Causa palette is mapped to named Forma tokens with measured contrast" | **over-tagged (partial)** | Computes contrast for the declared ink tokens on ivory and for teal on ivory. The pairs the CSS actually renders on tinted surfaces (`*-ink` on `--causa-teal-wash`/`amber-wash`/`coral-wash` in notices, tags, checks and the report banner) are not measured. |
| P-5 | VPO-c5d77a4b2eb52927 UI-019-O5 "define persistence and scope of each setting, show effective values, no credentials in local storage" | `shell.spec.js` "Settings apply for the session only …" | proven | States the session scope, shows effective values, asserts empty local/session storage and a reset after reload. |
| P-6 | VPO-c3326322b8ed473a UI-003-O2 "filtering by category and a before/after detail view" | `history.spec.js` (two tests) | proven | The filter reduces 3 entries to 1 and back. The historical view shows "Status: Draft → Active" and per-version changes. |
| P-7 | VPO-b9af1bcfc961c8bb UI-004-O3 "validated forms and confirmation for destructive or irreversible operations" | `forms.spec.js` (two tests) | proven | Required-field validation before sending. Falsify asks for confirmation, Cancel leaves the version unchanged, and confirming records it. |
| P-8 | VPO-b34d65289bec1814 WDL-001-O1 "identify the layout engine's owner, package, API, version, licence and constraints before implementation" | `graph-layout.spec.js` "the recorded engine is the installed one …" | proven (except "before implementation", which no test can show) | Compares the capability record with the installed package, the lock integrity and the licence, and runs a working example. |

Over-tagging count: **4 of 8** (P-1 to P-4, all partial: the test proves part of the obligation and the tag claims
all of it).

## Decisions recorded as given (not ratified; AR-008-O2)

I did not ratify or change any owner decision. These are recorded exactly as I found them: the AI adapter decision in
`BOUNDARIES.md` ("Open owner decision"), the coordinator's Boundary C stable APIs, the S16 NOT-APPLICABLE binding that
cites DF-CAUSA-2026-0002, and the builders' narrowed DCT-002-B legal source states.

## What I could not check

- **Real-browser reproduction of R2-F1.** I reproduced it with the program and the harness only. A two-tab Playwright
  run was not done in the time available.
- **Local Playwright.** I ran only chromium `shell.spec.js`, `fixture-mode.spec.js` and `forms.spec.js` (30 passed). I did not run the other specs locally; for those I rely on the candidate run's browser job.
- **WebKit projects.** I did not run them.
- **The deployed site at the candidate.** Not checked.
- **The live suite locally.** Not run (no credentials, by design). I cite CI instead.
- **Whether GitHub documents 403, 404 and 422 as "not applied" for `createCommitOnBranch`.** This affects how far
  R2-F2 reaches. 400, 408 and 409 stand regardless.
- **Praxis/ROS state.** Out of scope; I did not touch it.

## Re-review at `543ddc2` (R2-F1 / R2-F2 fixes)

| | |
| --- | --- |
| Candidate SHA | `543ddc2981503f0498d6fccbbc2c62b318c05f47` (origin/main, "Praxis: reconcile the R2-F1/R2-F2 fixes under WI-0005"). Fix commits: `7e3f3625949111d7829bacf559a20c411dc68820` (failing-first tests) and `b07d2a5def150e1cd741f0540667eac31b78330e` (fix), both ancestors of the candidate. |
| Candidate CI | Build and test run [40618681305](https://github.com/kemiller2002/indy-init-rehearsal-005/actions/runs/40618681305). Its `core` job (122588438544) checked out `543ddc2…` and passed: Acceptance 32, App 72, App.Engine 71, Persistence 75 (60 + 15 new), with 0 failed and 0 skipped. Its `live` job (122589145039) also checked out `543ddc2…` and passed all 3 `@live` tests against real GitHub, including `DCT-LIVE-A`. The browser job was still running when I wrote this. |
| Branch | `r5/review2`, with origin/main (`543ddc2`) merged in. This section, `acceptance/defect-classes.json` |

### Method

I read the changes to `src/persistence/Sync.fs` (status rule after send, `mergeRecords`, `restoreRecord`, the
`writer`/`basedOn` fields), `GitHub.parseCommit` and `src/app/PersistencePort.fs` (StoreLocal is now a Get, followed by a
merge and a Set). I then ran scratch scenarios V12-V15 against the real program, in a `git archive` copy of `543ddc2` with
the harness's shared-store convention. To model one browser's `localStorage`, I copy the shared map into a tab before
each of its steps and back out after. I also ran two mutations against the committed `DcTableTests`:

- M3: after send, only 5xx is unknown (the pre-fix rule). 7 committed tests fail, so the mutation is killed.
- M4: a 200 that names a commit becomes a refusal again. 4 committed tests fail, so the mutation is killed.

### R2-F2: fixed

V14: the fake applies the write, then the client receives each of these statuses: 400, 403, 404, 408, 409, 422, 429 and
201. Every one is `unknown` with Save withheld, and Reconcile reaches `remotelySynced` through the read-back.

A 200 that names a commit and also carries a typed error is `Unknown` (M4 is killed by the committed tests). A 200 with
a typed "not applied" error and no commit is still a refusal with Retry (V15). That is the documented contract, not a
finding: the answer itself says the write was not applied.

### R3-F1 (high): the R2-F1 merge still loses one tab's device-only change after the other tab reloads

`mergeRecords` treats the stored record as the page's own whenever its `writer` equals this page's `basedOn`, which is
the record the page restored from. In that case the stored record is replaced wholesale: its documents are neither kept
nor preserved. After tab A reloads, A's `basedOn` is tab B's writer id. Every later record B writes then counts as
"A's own", so A's next local write erases B's newer device-only changes.

Reproduction (scratch V12, at `543ddc2`):

1. Tabs A and B connect and share one store. Both are offline (`NotSentFault` on POST).
2. B records X and saves; X is queued.
3. A reloads. It restores B's record, so A holds X and A's `basedOn` becomes B's writer id.
4. B records Y and saves; Y is queued. The store now holds Y.
5. A reconnects, which writes A's record.
6. The store no longer holds Y. B's tab is closed and reopened, and Y is gone: `preservedElsewhere = []`, and nothing
   tells the user.

Observed: "storeHasY before A writes=true after=false; B reopened has Y=false preserved=[]".

The trigger is realistic: two tabs, a reload of one, and the other tab's later change made before it next writes.
Suggested fix: a record is "own" only when its `writer` is this page's own nonce. A record restored from another tab is
superseded only for the documents this page's copy covers, using the same `covered` test that already guards other
tabs' records.

### R3-F2 (medium): the local read-modify-write is not atomic across tabs

`PersistencePort` performs StoreLocal as a Storage `Get`, then sends a `Set` of the merge when the Get's result comes
back. Between those two effects another tab can Set. That tab's write is then overwritten by a merge computed from the
older value, which is a lost update.

Reproduction (scratch V13, at `543ddc2`, modelling the interleaving): B's Get returns the store before A's write. A then
writes its queued offline change. B's Set lands afterwards. A's reopened tab no longer has its change.

The window is short (one asynchronous round trip in the kernel), so this is rarer than R3-F1. Suggested fix: a `storage`
event re-merge, or per-tab keys merged on read.

### Defect classes at `543ddc2`

| Class | Decision | fixSha |
| --- | --- | --- |
| DC-001 | PASS (still holds) | `5336ec40372323dbdbbb821878ad87c51597af7a` |
| DC-002 | PASS (still holds) | `e8e32c1ebf6db77344cb8e8bc5bd30b422cdd379` |
| DC-003 | **PASS (closed now)** | `b07d2a5def150e1cd741f0540667eac31b78330e` |
| DC-004 | PASS (still holds) | `895fb6da3031da689cd3a6f54b4005f17f07edbe` |
| DC-005 | PASS (still holds) | `895fb6da3031da689cd3a6f54b4005f17f07edbe` |
| DC-006 | PASS (still holds) | `578ebd3b0bbc99862dc2da945e9c3b9e3f719bba` |
| R3-LESSON-3 | PASS (still holds) | `578ebd3b0bbc99862dc2da945e9c3b9e3f719bba` |
| DC-001..DC-005 (DCT-LIVE-A) | **PASS (closed now)** | `b07d2a5def150e1cd741f0540667eac31b78330e` |

<a id="dc-003-543ddc2"></a>
#### DC-003: closed at `543ddc2`

- **(a) Original reproduction.** `DCT-003-A a failure after send is unknown with no retry and reconcile reads the head
  first`, and `… saved is claimed only after the read-back …`.
- **(b) Adversarial variants.** All are committed in `DcTableTests` and pass in core job 122588438544:
  - the read-back on reconcile, and a read-back that fails, from F-1;
  - an unrecognised GraphQL type (F-2);
  - every HTTP status after send, 403/404/408/409/418/422/500/502 (R2-F2);
  - a 200 naming a commit together with each typed error (R2-F2).

  My V14 extends the status set to 400, 429 and 201. The class invariant ("once sent, any failure is unknown, never not
  written") held in every case I tried.
- **(c) Enforcement at the authority boundary.** `Sync.fs`'s `BookPublish` answer rule: 401 is a refusal; any other
  non-200 is `PublicationFailed`, which becomes `Unknown` after `PublicationSent`. `parseCommit` never refuses an answer
  that names a commit. The reconcile path reads back before it claims saved. Mutations M1, M3 and M4 are each killed by
  committed tests.
- **(d) Independent review.** This review. R3-F1 and R3-F2 are local-durability defects. They do not make a sent write
  look "not written", so they are reported as separate findings and do not reopen DC-003.
- **SHA-bound proof.** Run 40618681305, core job 122588438544, at `543ddc2`: Persistence 75/75 and App.Engine 71/71
  passed, including every DCT-003-A test.

#### DC-001 re-confirmed at `543ddc2`

The reconcile and marker code is unchanged since `5336ec4`. Every DCT-001-A/B test passed in the same core job. The
`543ddc2` changes touch only the publication answer rule and the local store, neither of which can parent a write on a
commit it did not read. DC-001 still holds.

<a id="dct-live-a-543ddc2"></a>
#### DC-001..DC-005 (DCT-LIVE-A): closed at `543ddc2`

Closed at `543ddc2`.

- **(a) Original live scenarios.** Create, write, reload, conflict (resolved by Reload) and rejected token, each
  confirmed by an independent SHA re-read against the real test repository.
- **(b) Adversarial live variant.** The two-tab F-1 variant (`3627f92`): tab B's write and its check are reset before
  reaching GitHub, tab A saves, and B's check must show a conflict, never "Saved". The same test also checks fresh-read
  URL uniqueness across the reload and both extra tabs (DC-005), and that the token is absent from URLs, page and storage.
- **(c) Enforcement.** Every component class it covers, DC-001 to DC-005, is now closed with its enforcement at the
  authority boundary (sections above).
- **(d) Independent review.** This review.
- **SHA-bound proof.** Run 40618681305, live job 122589145039, at `543ddc2`: `DCT-LIVE-A …` passed (3 of 3 live tests).
- **`fixSha`.** `b07d2a5` is the first commit holding every component fix (the last was DC-003's) and the two-tab
  live variant. It is an ancestor of the candidate.
- **Limit.** The live test does not drive R2-F2's status answers; those are proven only at fixture level against the
  recorded-behaviour fake.

### Over-tagging at `543ddc2`

The four partial tags reported above (UI-011-O4, WDL-014-O1, UI-015-O3, UI-016-O11) were removed in `f412598`. I did
not re-sample other obligations in this pass.

### What I could not check in this pass

- **R3-F1 and R3-F2 in a real browser.** I reproduced both with the program and the harness only.
- **The candidate run's browser job.** It was still running when I wrote this, so DC-006 and R3-LESSON-3 rest on the
  earlier evidence. The kernel files they depend on are unchanged since `be01851`.
- **WebKit and the deployed site.** Not checked.
