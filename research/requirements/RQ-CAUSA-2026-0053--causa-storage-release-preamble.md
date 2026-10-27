---
id: RQ-CAUSA-2026-0053
title: "CAUSA-STORAGE-RELEASE§preamble Causa storage contract v1.0.0: release identity"
status: accepted
version: 1.0.0
owners: [indy-init-rehearsal-005]
created: 2026-10-27
updated: 2026-10-27
priority: medium
tags: [rehearsal-005, identity-derivation]
source: "CAUSA-STORAGE-RELEASE§preamble"
provenance:
  contributions:
    EXE-20261027T201410340Z-c35e9b80:
      operations: [created]
      at: 2026-10-27T20:15:47.078Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: claude-opus-5-5
        runtime: claude-code
      reason: "Identity derivation of the pinned Indy-init baseline into Praxis RQ form (WI-0002, tools/derive-requirements)"
      evidence: [research/requirements-preservation/derivation-record.json]
---

# RQ-CAUSA-2026-0053

Derived from baseline requirement `CAUSA-STORAGE-RELEASE§preamble` (Causa storage contract v1.0.0: release identity). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Acceptance criterion |
|---|---|
| CAUSA-STORAGE-RELEASE§preamble-O5 | tag, the 17-case self-test and all three consumers. |

| ID | Statement |
|---|---|
| CAUSA-STORAGE-RELEASE§preamble-O1 | **Status: RELEASED.** The tag `causa-storage-v1.0.0` (annotated tag object `fd2991a392e913b741f0ed2bc4daa4bc4be57f6c`) |
| CAUSA-STORAGE-RELEASE§preamble-O2 | points to commit `c947c7e354be80766573a782546c8186336774cc` of `kemiller2002/causa-data`. |
| CAUSA-STORAGE-RELEASE§preamble-O3 | It was created by the |
| CAUSA-STORAGE-RELEASE§preamble-O4 | repository's `release-tag` workflow (run 39874016465) only after that exact commit passed every gate anchored to the |
| CAUSA-STORAGE-RELEASE§preamble-O6 | Experiment record: `kemiller2002/research-documents`, `research/experiments/EX-CAUSA-STORAGE-2026-001/` (RESULT.md). |
| CAUSA-STORAGE-RELEASE§preamble-O7 | \| identity \| value \| |
| CAUSA-STORAGE-RELEASE§preamble-O8 | \| contract version \| `1.0.0` \| |
| CAUSA-STORAGE-RELEASE§preamble-O9 | \| release tag \| `causa-storage-v1.0.0` (tag object `fd2991a3…`) → `c947c7e354be80766573a782546c8186336774cc` \| |
| CAUSA-STORAGE-RELEASE§preamble-O10 | \| schema identity: SHA-256 of `contract/v1.0.0/schemas/bundle.json` \| `0a68af8c93190d2e4d6c27beb2f8cb67c3aebdda586d01beb7122b71af179152` \| |
| CAUSA-STORAGE-RELEASE§preamble-O11 | \| dataset identity: SHA-256 of `fixtures/v1.0.0/manifest.json` \| `f650a2e509a420872b46177b21f57ef6ebd7625ae4c69dd92198d3ca472c2b86` \| |
| CAUSA-STORAGE-RELEASE§preamble-O12 | \| release identity: SHA-256 of `compatibility/v1.0.0/lock.json` \| `6a7e7802ff2a3ff500ab6065662514a96e09e6c15eb30212ba1377fcc5bf8fc5` \| |
| CAUSA-STORAGE-RELEASE§preamble-O13 | \| domain engine \| `indy-init-rehearsal-001@55f49f61c939a3a780cee592f36f732a1328f981` \| |
| CAUSA-STORAGE-RELEASE§preamble-O14 | \| engine assembly: SHA-256 of `IndyInitRootCauseInvestigator.Engine.dll`, sterile build \| `b3904277e72390667a35ccaeb7986f5d6c4848ffd5b8c28fc13f9673b5e13a44` \| |
| CAUSA-STORAGE-RELEASE§preamble-O15 | \| .NET SDK \| `10.0.402` (`rollForward: disable`) \| |
| CAUSA-STORAGE-RELEASE§preamble-O16 | **Registry:** 31 event types, 107 payload fields, 60 JSON Schemas (Draft 2020-12), 284 schema fields. |
| CAUSA-STORAGE-RELEASE§preamble-O17 | **Fixtures:** 5 frozen scenarios A–E, 184 events, plus 90 negative fixtures. |
| CAUSA-STORAGE-RELEASE§preamble-O18 | The normative contract is `causa-data/contract/v1.0.0/CONTRACT.md` at the tag. |
| CAUSA-STORAGE-RELEASE§preamble-O19 | [CAUSA-DATA-CONTRACT-v1.md](CAUSA-DATA-CONTRACT-v1.md) was the *candidate* that this release was reconciled against. |
| CAUSA-STORAGE-RELEASE§preamble-O20 | Where the two differ, CONTRACT.md wins. |
| CAUSA-STORAGE-RELEASE§preamble-O21 | The differences are listed in the experiment's `COMPATIBILITY-DECISIONS.md`. |
| CAUSA-STORAGE-RELEASE§preamble-O22 | Pin the tag `causa-storage-v1.0.0` and check that the three identities above match. |
| CAUSA-STORAGE-RELEASE§preamble-O23 | Any mismatch is a FAIL |
| CAUSA-STORAGE-RELEASE§preamble-O24 | (STORAGE-007). |
| CAUSA-STORAGE-RELEASE§preamble-O25 | Run `tools/fetch-engine.sh`, then `causa gate . --anchor causa-storage-v1.0.0` in causa-data. |
| CAUSA-STORAGE-RELEASE§preamble-O26 | Write only under `workspaces/demo/<runId>/`, never under `fixtures/`. |
| CAUSA-STORAGE-RELEASE§preamble-O27 | The provider token stays in the local store. |
| CAUSA-STORAGE-RELEASE§preamble-O28 | The agent session that froze the release could not push tags. |
| CAUSA-STORAGE-RELEASE§preamble-O29 | `.github/workflows/release-tag.yml` in causa-data does |
| CAUSA-STORAGE-RELEASE§preamble-O30 | it instead. |
| CAUSA-STORAGE-RELEASE§preamble-O31 | It: |
| CAUSA-STORAGE-RELEASE§preamble-O32 | reads `.github/release-tag/request.txt`; |
| CAUSA-STORAGE-RELEASE§preamble-O33 | checks that the commit is in the branch history and that `RELEASE` names the tag; |
| CAUSA-STORAGE-RELEASE§preamble-O34 | tags the commit locally and runs every gate anchored to that tag; |
| CAUSA-STORAGE-RELEASE§preamble-O35 | only then pushes the tag. |
| CAUSA-STORAGE-RELEASE§preamble-O36 | It never moves or replaces an existing tag. |
| CAUSA-STORAGE-RELEASE§preamble-O37 | Protecting `causa-storage-v*` with a tag ruleset is recommended. |
