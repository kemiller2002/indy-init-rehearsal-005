---
id: RQ-CAUSA-2026-0304
title: "REHEARSAL-ACCEPTANCE-LEDGER§next-implementation-step Next implementation step"
status: accepted
version: 1.0.0
owners: [indy-init-rehearsal-005]
created: 2026-10-27
updated: 2026-10-27
priority: medium
tags: [rehearsal-005, identity-derivation]
source: "REHEARSAL-ACCEPTANCE-LEDGER§next-implementation-step"
provenance:
  contributions:
    EXE-20261027T201410340Z-c35e9b80:
      operations: [created]
      at: 2026-10-27T20:17:35.690Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: claude-opus-5-5
        runtime: claude-code
      reason: "Identity derivation of the pinned Indy-init baseline into Praxis RQ form (WI-0002, tools/derive-requirements)"
      evidence: [research/requirements-preservation/derivation-record.json]
---

# RQ-CAUSA-2026-0304

Derived from baseline requirement `REHEARSAL-ACCEPTANCE-LEDGER§next-implementation-step` (Next implementation step). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Acceptance criterion |
|---|---|
| REHEARSAL-ACCEPTANCE-LEDGER§next-implementation-step-O1 | Implement a small validator/generator consuming requirement IDs, test results and gate artifacts. |
| REHEARSAL-ACCEPTANCE-LEDGER§next-implementation-step-O2 | It must reject duplicate IDs, unknown statuses, missing proof, mismatched SHAs, and an Accepted flag with incomplete required gates. |

| ID | Statement |
|---|---|
| REHEARSAL-ACCEPTANCE-LEDGER§next-implementation-step-O3 | Do not build a new generic workflow platform merely to implement this ledger. |
