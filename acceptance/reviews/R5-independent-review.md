# Rehearsal 5: independent adversarial review (AR-008, T30 and T32/T33)

| | |
| --- | --- |
| Reviewed SHA | `fbe4d87f76d23724a660835123984585a458efa3` (origin/main at 00:40Z, "Praxis: reconcile core (S08-S10, S17-S19), shell (S20/S21) and acc (proof-page wiring) commits") |
| Reviewer | independent review agent; did not build any of the reviewed code |
| Branch | `r5/review` (this file and `acceptance/defect-classes.json` only; no product code, no tests, no `.ros/` or `.praxis/` changes) |
| Requirements | Indy-init `requirements-compiled/v1` (plan `VP-e77a2134e1b0`), `docs/DEFECT-CLASS-CLOSURE-AND-RELEASE-GATES.md`, `docs/DESIGN-DECISION-BOUNDARIES.md`, `docs/AGENT-RELIABILITY-REQUIREMENTS.md` (AR-008) |
| Candidate CI | Build and test run [40610187284](https://github.com/kemiller2002/indy-init-rehearsal-005/actions/runs/40610187284) at the reviewed SHA (completed, success: all 16 jobs including core, browser, live, percepta, deploy, deployed-smoke and acceptance; its computed traceability: 256 PASS / 70 NOT_RUN; ledger gates G0-G4 and G6 PASS, G5/G7/G8/G9 NOT_RUN) |
| Deployed site | `https://kevinmmiller.us/indy-init-rehearsal-005/version.json` reported `e086147…` (run 40606127778) at 00:47Z and `{"sha":"fbe4d87f76d23724a660835123984585a458efa3","run_id":"40610187284"}` at 00:56Z, after the candidate run's deploy and deployed-smoke jobs succeeded. I did not browse the deployed site beyond `version.json`, `build-info.json` and `main.js` (`TEST_BUILD = false`). |

## Method

1. Read the boundary and invariant documents (`docs/architecture/BOUNDARIES.md`, `INVARIANTS.md`, `acceptance/README.md`,
   `defect-classes.json`, `gates.json`) and then the code that is supposed to enforce them: `src/engine/Sync.fs` (the
   request book), `src/persistence/Sync.fs` (the outer program), `GitHub.fs`, `Provider.fs`, `src/app/PersistencePort.fs`,
   `src/app/Dispatch.fs`, `src/wasm/Program.cs`, `src/kernel/main.ts`, `scripts/build-site.mjs`.
2. Built and ran everything locally at the reviewed SHA: `dotnet test App.slnx -c Release` (219 tests: 29 + 71 + 54 + 65,
   all passed); `npm ci && npm run build`; Playwright `--grep-invert @live --project=chromium` on ports 5321/5322
   (68 passed, 0 failed, 0 skipped, 10.1 min).
3. Tried to falsify each invariant with concrete inputs: reviewer-only adversarial tests and source mutations, run in a
   throwaway copy of the tree (`git archive HEAD`), never committed. The reproductions are reproduced below so the
   builders can turn them into failing-first tests.
4. Checked the defect classes against DCC-004/DCC-005 (original reproduction, adversarial variants, invariant enforcement
   at the authority boundary, SHA-bound proof), including mutation runs to confirm the DCT tests fail on the defect.
5. Spot-checked 14 obligations that the computed traceability marks PASS, reading each bound test against the
   obligation text. I started from run 40606127778 (source `e086147`, 244 PASS) and confirmed that each of the 14 has the
   identical bound-test set and PASS in the candidate run 40610187284 (`acceptance-evidence/bundle/traceability.json`,
   source `fbe4d87`). The committed `acceptance/traceability.json` is stale (3 bound) and was not used.

## Findings

### F-1 (critical): change markers are not unique, so reconciliation claims another writer's commit as ours and the next save erases that writer's history

Invariants violated: DC-001 (DCT-001-B: reconciliation recognises *our* commit by ancestry and marker), DC-003 / D-009
(saved is claimed only after the read-back), EXT-DATA-006-O2 (no overwrite of unread remote history), UI-006-O2.

Cause: the engine's `Sync.Change.Marker` is documented as "a fresh per-change token carried in the commit so
reconciliation can recognise it", and `Sync.classify` treats `first.Parent = DecidedAt && first.Marker = Some marker` as
proof that the commit is ours. The persistence program builds the marker as `sprintf "%s-%d" id m.NextId`
(`src/persistence/Sync.fs`, `createWith`, `changeLocal`, `integrate`), where `NextId` is a per-page counter that starts at
1 on every page load and advances deterministically with the requests of connect/load. Two tabs or two devices that
connect to the same workspace and edit the same investigation therefore produce **identical markers** for changes decided
at the same head (observed: both `e50a7d2a-…-11`). The reconcile path (`Reconciling` + `HeadObserved` → `Applied`) then
goes straight to `Idle(ours, None)` and the outer program sets `Confirmed = Local`, `ConfirmedHead = <other writer's
commit>`, without reading back any bytes.

Reproduction (reviewer test against the real `Sync.update` program and the repository's recorded-behaviour fake; add to
`tests/Persistence.Tests` after `ConformanceTests.fs`):

```fsharp
[<Fact>]
let ``REVIEW two devices: B's lost write is reconciled as applied because A's commit carries the same marker`` () =
    let ws = "rev-two-devices"
    let fake = newFake ()
    let v = investigation "rev-two" 0
    let t = target ws
    fake.CommitFiles(branch, Store.files v |> List.map (fun (n, b) -> Target.investigationDir t v.InvestigationId + "/" + n, b)) |> ignore
    let iid = v.InvestigationId
    let a = App(fake)
    let b = App(fake)
    ok (a.Connect ws)
    ok (b.Connect ws)
    ok (a.Step(Submit(hypothesis iid v.Version "from device A")))
    ok (b.Step(Submit(hypothesis iid v.Version "from device B")))   // same marker as A's change
    ok (a.Step(Save iid))                                            // A: remotelySynced
    b.Faults <- (fun r -> if r.Method = "POST" then LoseWithoutApplying else Deliver)
    ok (b.Step(Save iid))                                            // B: unknown (never applied)
    b.Faults <- (fun _ -> Deliver)
    ok (b.Step(Reconcile iid))
    Assert.Equal("conflict", b.Status iid)                           // ACTUAL: "remotelySynced"
```

Observed at the reviewed SHA: markers `A=e50a7d2a-8881-55b1-ab89-bf23bfcad320-11 B=e50a7d2a-8881-55b1-ab89-bf23bfcad320-11`;
B after Reconcile: `remotelySynced`. Continuing the scenario (B records a second hypothesis and saves; a third tab
connects): B again reports `remotelySynced`, and the investigation on the branch **no longer contains A's event**
("from device A": false, "from device B": true). A's accepted, published history was silently replaced, and both
devices show "Saved to GitHub".

Realistic trigger: two tabs or devices on one workspace plus one save whose answer is lost (timeout, connection drop, or
a tab closed mid-save, which `restore` turns into Unknown and the next connect reconciles automatically).

Class-level prevention (DCC-004), for the builders: make the marker unguessable and unique (entropy, e.g. one of the
`Entropy.Ids` GUIDs, never a counter), and do not claim saved from a marker match alone: after `Applied`, read back the
investigation's blob ids at `ours` exactly as `Confirming` does. Add the reproduction above (and a same-device two-tab
variant) as DCT-001-B / DCT-003-A failing-first tests.

### F-2 (low): a typed but unrecognised GraphQL error on the write is treated as "not written" and offers Retry

`GitHub.parseCommit` maps an error whose `type` is not one of `STALE_DATA`, `FORBIDDEN`, `NOT_FOUND`, `UNPROCESSABLE`,
`RATE_LIMITED` to `Rejected(200, kind)`, which the program turns into `PublicationRefused(Invalid)` → `Held(NotWritten)` →
`canRetry = true`. An untyped error is already (correctly) Unknown. DCT-003-A's invariant is "once sent, any failure is
unknown, never not written" unless the provider's answer says the request was not applied; an unknown error type says
nothing either way. Reproduction: answer the `BookPublish` request with
`{"data":{"createCommitOnBranch":null},"errors":[{"type":"INTERNAL","message":"x"}]}`; the document status becomes
`failed` with Retry offered. Suggested: treat unrecognised types as `Unknown` (reconcile first).

### F-3 (low): the public historical catalog cannot be opened without a token

`Credential.create` rejects a blank token and `PersistencePort` requires one for every Connect, so the read-only public
catalog (`kemiller2002/causa-data`, anonymous reads work, as the live test's own `gh(..., true)` shows) cannot be loaded
anonymously. PF-015-O1 is nevertheless marked PASS (see O-3). Recorded as a gap, not as a decision.

### F-5 (medium, acceptance pipeline): a reviewer's closure can never satisfy the pinned verifier

`Ledger.fs` writes a closed class's `reviewerEvidence` into the ledger's `evidence_paths`, but nothing stages a
`causa.acceptance-evidence/1` artifact for subject type `defect` in the bundle (`allEvidence` covers requirements and
gates only). Indy-init's `tools/compute-acceptance-verdict.py` (`evidence_problems`) checks every PASS item's evidence
path against the trusted manifest, so every closed class will be reported as "evidence … is absent from the trusted
manifest", whatever the reviewer records. `acceptance/defect-classes.json` asks for "a bundle path", which does not exist
for any class. Reproduction (by reading, not yet observed in a run): with the closures on this branch, the verifier's
`evidence_problems` has no manifest entry for any `acceptance/reviews/…` path, so it should report one problem per
closed class; the first CI run of `r5/review` will show it. Suggested: have `causa-acceptance ledger` emit
`evidence/defects/<id>.json` from the `defect-class-review` producer's checks and the recorded closure.

### F-4 (low): a local `npm run build` publishes the stale committed traceability on the proof page

`scripts/build-site.mjs` falls back to `acceptance/traceability.json` (committed, 3 of 326 bound) when `CAUSA_TRACEABILITY`
is unset. CI always sets it (to "not published" when no prior run exists), so the deployed page is not affected; a local
or other build is. Suggested: no fallback to the committed file.

### Invariants checked and not broken

| Invariant | What was tried | Result |
| --- | --- | --- |
| Single validator (Boundary C) | Every load path: workspace load, reload, restore from local store (`parseStored` → `Store.read` + `Store.successor`), fixture provider (same program). | All go through `Store.read`/`Storage.read`; no second parser found. |
| Sync owns every write | Searched every `createCommit`/`Publish` path. | The only publication is `Book.Publish` emitted by `Sync.step`; the outer program calls `Book.step` directly only for `Edit`/`Reapply`/`Discard`/`Reloaded` (documented hooks). |
| DC-001 parenting | Publication body (`expectedHeadOid`), `Reloaded` on a moved head, `integrate` rebase. | Parent is always `DecidedAt`; a rebase happens only when the investigation's remote bytes equal the confirmed ones (contract §9.4.2 a). Broken only through F-1. |
| DC-002 request identity | Mutation: engine `StaleAnswer` guard weakened to "anything in flight". | Engine `DCT-002-A` fails (mutation killed). The persistence-level `DCT-002-A` still passes because the outer `InFlight` map refuses the answer too (defence in depth, not a gap). |
| DC-003 stage | `Failure` on a write → `OutcomeUnknown` in `PersistencePort`; 5xx/malformed after send → `PublicationFailed` after `PublicationSent` → Unknown. | Holds except F-1 (reconcile claims saved without read-back) and F-2. |
| DC-004 advisory permissions | `CanPush` is read but never used to gate anything in `src/`. Mutation: gate `canSave` on `CanPush = Some true`. | Both DCT-004-A tests fail (mutation killed). |
| DC-005 freshness | Mutation: drop the per-request part of the branch-head nonce. | DCT-005-A fails (mutation killed). Live test checks unique `causa_fresh` URLs across reload and a second tab against real GitHub. |
| Token in memory / Authorization only | `Credential` redacts `ToString`; `toLimen` adds it only as `Authorization`; `HttpCredentials.Omit`; local record has no credential; shell clears the draft and bumps `data-secret-epoch`. | No other route found. |
| No test controls in production | `EnableTestHooks` is `#if CAUSA_TEST_BUILD`; `build-site.mjs` refuses a production wasm containing it; `inject` is a no-op without the flag; fixture Connect is refused without it; deployed `main.js` has `TEST_BUILD = false`. | Holds. |
| Historical data never bundled | Grepped `dist/` and `obj/site-test/` for the three catalog investigation ids and their digests. | Only requirement text in `evidence/proof.json` mentions the case names; no record, id or digest is shipped. |

## Defect classes (T32/T33)

Closure follows DCC-004/DCC-005: (a) regression reproducing the original failure, (b) adversarial variants across the
class, (c) enforcement at the authority boundary, (d) this independent review, all tied to the reviewed SHA.
`fixSha` is the commit that introduced the enforcement (each an ancestor of the reviewed SHA, checked with
`git merge-base --is-ancestor`).

Convention: `fixSha` is the first commit at which both the enforcement and the class's DCT tests existed.
`reviewerEvidence` points at the section of this document for that class. The CI evidence for every class is the
candidate run [40610187284](https://github.com/kemiller2002/indy-init-rehearsal-005/actions/runs/40610187284):
its `acceptance-evidence/producers/defect-class-review.json` records each class's DCT tests as selected, run and
passed at `fbe4d87`.

| Class | Decision | fixSha |
| --- | --- | --- |
| DC-001 | **OPEN** | — |
| DC-002 | PASS (closed) | `e8e32c1ebf6db77344cb8e8bc5bd30b422cdd379` |
| DC-003 | **OPEN** | — |
| DC-004 | PASS (closed) | `895fb6da3031da689cd3a6f54b4005f17f07edbe` |
| DC-005 | PASS (closed) | `895fb6da3031da689cd3a6f54b4005f17f07edbe` |
| DC-006 | PASS (closed) | `578ebd3b0bbc99862dc2da945e9c3b9e3f719bba` |
| R3-LESSON-3 | PASS (closed) | `578ebd3b0bbc99862dc2da945e9c3b9e3f719bba` |
| DC-001..DC-005 (DCT-LIVE-A) | **OPEN** | — |

G7 still cannot pass: three classes stay open. Also see F-5: even a closed class's evidence is rejected by the pinned
verifier as things stand.

<a id="dc-001"></a>
### DC-001: OPEN

The DCT-001-A/B tests (engine and app level, with negative controls) exist and passed at the reviewed SHA, but F-1 is a
new manifestation of the class: reconciliation recognises another writer's commit as ours, because the marker the
classification relies on is a predictable counter. DCC-004 forbids closing on the original examples alone. Reopen
criteria to close: unique, unguessable markers; the F-1 reproduction and a two-tab variant as failing-first tests.

<a id="dc-002"></a>
### DC-002: closed

- (a) Original reproduction: `WriteInvariantTests.DCT-002-A a late answer for a replaced write is discarded`,
  `DcTableTests.DCT-002-A … a late answer for a replaced write is discarded` (application level),
  `REQUEST-IDENTITY …` (engine and application).
- (b) Adversarial variants: `DCT-002-B … every user event is accepted only in its legal source states` delivers every
  user event in every phase of the book (engine) and in pending/sending/unknown phases of the program; the R3 D-04
  resubmission case; negative controls `… matched by kind` and `… writes during an unknown outcome`.
- (c) Enforcement at the authority boundary: `Sync.step` (book-issued `RequestId`, `StaleAnswer`, `IllegalInState`), plus
  the outer program's `InFlight` map. Reviewer mutation: weakening the `StaleAnswer` guard makes the engine DCT-002-A
  test fail (killed); the application-level test still passes because the outer `InFlight` check refuses the answer
  independently (defence in depth).
- (d) This review; I found no way to make an answer land on a request that did not ask it.
- SHA-bound: run 40610187284, core job: DCT-002-A (3 tests) and DCT-002-B (5 tests) passed.

<a id="dc-003"></a>
### DC-003: OPEN

DCT-003-A (stage-aware classification, read-back before saved) passed, and the stage rule holds for transport failures.
But the class invariant "saved is claimed only after the read-back" does not hold on the reconcile path: `Reconciling`
→ `Applied` claims synced on a marker match with no read-back, and F-1 shows that claim made for a write that never
reached GitHub. F-2 (a typed but unknown GraphQL error after send offers Retry) is a second, low-severity gap in the
same class. Close when the reconcile path reads back the bytes before claiming saved, with F-1 and F-2 as tests.

<a id="dc-004"></a>
### DC-004: closed

- (a) `DctProviderTests.DCT-004-A … an absent permissions field is not denial` uses the recorded real repository
  answer without `permissions` (`recordings/run-40585189886`) and saves.
- (b) `DCT-004-A … push false is advisory; a refused write is held as write-denied and a renewed credential saves it`
  (installation-token form, recorded FORBIDDEN refusal).
- (c) `RepositoryInfo.CanPush` is parsed but used nowhere in `src/` to gate saving; only GitHub's answer to the write
  decides. Reviewer mutation: gating `Sync.canSave` on `CanPush = Some true` makes both DCT-004-A tests fail (killed).
- (d) This review.
- SHA-bound: run 40610187284, core job: DCT-004-A (2 tests) passed.

<a id="dc-005"></a>
### DC-005: closed

- (a) `DctProviderTests.DCT-005-A …` reproduces R2's repeat across page loads: write, another writer moves the branch,
  reload (new page nonce) and a second tab; every repository and branch-head read has a distinct `causa_fresh` URL and the
  new head is observed.
- (b) In a real browser against real GitHub (which sends `Cache-Control: private, max-age=60`): `DCT-LIVE-A`'s DC-005
  section asserts the same URL uniqueness across the reload and a second tab, and the conflict step observes the moved
  head. Content reads are addressed by commit/blob id (immutable) by construction (`GitHub.fs`).
- (c) `GitHub.getBranchHead` / `getRepository` take the page-load nonce plus the request id. Reviewer mutation: dropping
  the per-request part of the nonce makes DCT-005-A fail (killed).
- (d) This review.
- SHA-bound: run 40610187284: core job DCT-005-A passed; live job (DCT-LIVE-A) passed.

<a id="dc-006"></a>
### DC-006: closed

- (a) `shell.spec.js` `DCT-006-A a 503 on one framework .wasm (dotnet.native) …` reaches the failed state in < 20 s
  (timeout 60 s), focus on Retry, and Retry reaches ready (chromium and webkit).
- (b) The committed `a 503 for the runtime …` (every `.wasm` 503). Reviewer variants (scratch spec, not committed),
  503 on one file at a time at the reviewed SHA: `Causa.App` failed state in 358 ms, `FSharp.Core` 630 ms,
  `IndyInitRootCauseInvestigator.Engine` 459 ms, `dotnet.runtime` 347 ms; in each case Retry was focused and reached
  "Causa is ready." (4/4 passed).
- (c) `src/kernel/main.ts`: `unhandledrejection` → `startFailed`, bounded `Promise.race` with the 60 s timeout, Retry
  reloads.
- (d) This review.
- SHA-bound: run 40610187284, browser job: DCT-006-A (2 tests) passed.

<a id="r3-lesson-3"></a>
### R3-LESSON-3: closed

- (a)+(b) `DCT-INPUT-A typed text in bound inputs survives …`: a static half that requires `data-on="input"` on
  **every** `<input>`/`<textarea>` with `data-bind-value` in `src/kernel/index.html` (the class, not one field), and a
  dynamic half that types into two bound fields with a round trip per keystroke, then forces an unrelated projection.
- (c) The Limen binding vocabulary in `index.html`.
- (d) This review; I found no bound editable control without `data-on="input"`.
- SHA-bound: run 40610187284, browser job: DCT-INPUT-A (2 tests) passed.

<a id="dct-live-a"></a>
### DC-001..DC-005 (DCT-LIVE-A): OPEN

The live scenarios (create, write, reload, conflict, rejected token, each with independent SHA re-reads) passed in the
candidate run's live job. The composite class still covers DC-001 and DC-003, which F-1 reopens, so it cannot be
closed. It should be closed together with them, ideally with a live variant of F-1 (two pages, one write whose answer
is lost).

## Over-tagging (traceability spot-check)

Fourteen PASS obligations from the candidate run's computed traceability (40610187284) were read against their bound tests.

| # | Obligation | Verdict | Why |
| --- | --- | --- | --- |
| O-1 | VPO-0b0fb80724185366 EXT-DATA-005-O1 "check repository and branch permissions, validate file paths and manifests, load only schema-compatible data through the full pipeline" | **over-tagged** | The only bound test is DCT-004-A, which proves that permission fields are *not* used to gate saving. Nothing bound tests path or manifest validation or the load pipeline. |
| O-2 | VPO-2a3f7cbe7df14c03 WDL-005-O2 "layout must remain valid for zero, one, many, disconnected, cyclic graphs" | **over-tagged** | `GraphTests` "zero, one, many and disconnected graphs are all valid" counts projected nodes/edges; no layout is computed or checked. The layout checks live in `graph-layout.spec.js`, which is not bound to this obligation. |
| O-3 | VPO-a59ccf551bbd8407 PF-015-O1 "public read-only catalog tests should not require a PAT" | **over-tagged** | The bound live test throws BLOCKED without `CAUSA_GITHUB_TOKEN` and connects the app with the token; the app cannot connect without one (F-3). Only the test's own expectation reads are anonymous. |
| O-4 | VPO-c81b5785b2ffa1c2 PF-014-O2 "…three historical reconstructions, expected versions/digests, and deployed application behavior" | **over-tagged (partial)** | The bound live test checks three cards and titles against a locally served `dist/`; no version or digest is asserted and it is not the deployed application. |
| O-5 | VPO-f1a416eaeec6c129 UI-013-O1 "dashboard, graph, timeline, evidence inspector, comparison, and report refer to the same investigation id and version" | **over-tagged (partial)** | The bound tests check overview, timeline and list only; graph, evidence inspector, comparison and report are not checked, and the id is never compared across views. |
| O-6 | VPO-06e616ae2378cfcd UI-031-O5 "apply role/access controls to sensitive operational information and never expose credentials" | **over-tagged (partial)** | The bound test proves the token never reaches page, URL or storage; no role/access control is tested. |
| O-7 | VPO-8c435e1ebdf43e7c PF-009-O3 "request identity, active provider, credential generation, freshness" | **over-tagged (partial)** | Bound tests prove request identity and the earlier-connection case; active-provider switching and freshness are not exercised by any bound test (DCT-005-A is bound to PF-009-O2, not O3). |
| O-8 | VPO-199103f9166730e7 AR-006-O2 "invalid or unauthorized transitions rejected before side effects" | **over-tagged (individual tests)** | Most of the 20 bound tests prove it, but e.g. `AiLoopTests` "a successful answer yields drafts…" and "every provider outcome is classified…" do not concern rejecting a transition. |
| O-9 | VPO-be99a964783c59b5 UI-004-O1 (via the T17 checkpoint test) | **over-tagged (individual test)** | The checkpoint test's title claims "a refusal changes nothing" but performs no refused command, and does not check that unauthorised commands are absent. Other bound tests do prove UI-004-O1. |
| O-10 | VPO-d7fbd84e40ded145 EXT-DATA-006-O2 "…no overwrite of unread remote history" | **falsified** (not over-tagging) | The bound tests pass, but F-1 overwrites unread remote history. |
| O-11 | VPO-b3efad6eb85360be UI-006-O2 "only claim remote synchronization after confirmed provider acknowledgement" | **falsified** (not over-tagging) | F-1 claims "Saved to GitHub" for a write that was never applied. |
| O-12 | VPO-cc57508f3ac056de UI-005-O2 | proven | Exercises every interpretation command and asserts content, source, capture time, version and provenance unchanged, plus the closed evidence command set. |
| O-13 | VPO-ab625062f52606bb PF-013-O3 | proven | Scans every committed recording for token prefixes and forbidden headers, and requires bodies to be shapes only. |
| O-14 | VPO-21abf6acd60d0acc UI-013-O2 | proven | Engine port test and browser checkpoint show every view moving to the one new version after a command. |

Over-tagging count: **9** (O-1 … O-9; 3 complete, 6 partial or individual-test). Two further PASS obligations (O-10,
O-11) are contradicted by F-1.

## Decisions recorded as given (not ratified; AR-008-O2)

- `BOUNDARIES.md` "Open owner decision": the AI adapter is the Anthropic Messages API, browser-direct, model id as
  configuration with no default. Recorded as found; the reviewer cannot approve it.
- `BOUNDARIES.md` "Stable APIs (decision of the coordinator, R5)": Boundary C entry points. Recorded as given.
- DCT-002-B legal source states: `Write` is legal only from `Held(NotWritten)`, not `Held(Conflict)`, which narrows the
  curation text "write from Idle/Held". Recorded as the builders' documented choice.

## What I could not check

- The deployed site at the reviewed SHA: Pages still served `e086147` when checked; the candidate's deploy and
  deployed-smoke had not run.
- WebKit (`@webkit`) projects: only `--project=chromium` was run locally.
- The live suite: not run locally (no credentials, by design); its CI result at the reviewed SHA is cited instead.
- Percepta `verify-app` locally; CI's job result is cited.
- Load behaviour under real GitHub latency for F-1 (reproduced against the recorded-behaviour fake, which the repository
  itself uses as the conformance reference).
- Praxis/ROS state: out of scope for this reviewer (not touched).
