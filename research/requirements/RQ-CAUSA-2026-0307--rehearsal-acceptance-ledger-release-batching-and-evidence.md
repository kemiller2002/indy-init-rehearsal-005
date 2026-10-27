---
id: RQ-CAUSA-2026-0307
title: "REHEARSAL-ACCEPTANCE-LEDGER§release-batching-and-evidence Release batching and evidence"
status: accepted
version: 1.0.0
owners: [indy-init-rehearsal-005]
created: 2026-10-27
updated: 2026-10-27
priority: medium
tags: [rehearsal-005, identity-derivation]
source: "REHEARSAL-ACCEPTANCE-LEDGER§release-batching-and-evidence"
provenance:
  contributions:
    EXE-20261027T201410340Z-c35e9b80:
      operations: [created]
      at: 2026-10-27T20:17:37.001Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: claude-opus-5-5
        runtime: claude-code
      reason: "Identity derivation of the pinned Indy-init baseline into Praxis RQ form (WI-0002, tools/derive-requirements)"
      evidence: [research/requirements-preservation/derivation-record.json]
---

# RQ-CAUSA-2026-0307

Derived from baseline requirement `REHEARSAL-ACCEPTANCE-LEDGER§release-batching-and-evidence` (Release batching and evidence). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Statement |
|---|---|
| REHEARSAL-ACCEPTANCE-LEDGER§release-batching-and-evidence-O1 | Use targeted checks during development; batch compatible fixes into a release candidate. |
| REHEARSAL-ACCEPTANCE-LEDGER§release-batching-and-evidence-O2 | Independent architecture review before first candidate. |
| REHEARSAL-ACCEPTANCE-LEDGER§release-batching-and-evidence-O3 | Preserve R001 baseline and historical results unchanged. |
| REHEARSAL-ACCEPTANCE-LEDGER§release-batching-and-evidence-O4 | Emit one ledger per candidate and one final immutable report with exact source/deployment identities. |
