---
id: RQ-CAUSA-2026-0172
title: "KICKOFF-EXECUTION-PACKET§10-final-freeze 10. Final freeze"
status: accepted
version: 1.0.0
owners: [indy-init-rehearsal-005]
created: 2026-10-27
updated: 2026-10-27
priority: medium
tags: [rehearsal-005, identity-derivation]
source: "KICKOFF-EXECUTION-PACKET§10-final-freeze"
provenance:
  contributions:
    EXE-20261027T201410340Z-c35e9b80:
      operations: [created]
      at: 2026-10-27T20:16:39.451Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: claude-opus-5-5
        runtime: claude-code
      reason: "Identity derivation of the pinned Indy-init baseline into Praxis RQ form (WI-0002, tools/derive-requirements)"
      evidence: [research/requirements-preservation/derivation-record.json]
---

# RQ-CAUSA-2026-0172

Derived from baseline requirement `KICKOFF-EXECUTION-PACKET§10-final-freeze` (10. Final freeze). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Acceptance criterion |
|---|---|
| KICKOFF-EXECUTION-PACKET§10-final-freeze-O3 | run full build/tests; |

| ID | Statement |
|---|---|
| KICKOFF-EXECUTION-PACKET§10-final-freeze-O1 | At final freeze: |
| KICKOFF-EXECUTION-PACKET§10-final-freeze-O2 | stop feature branches; |
| KICKOFF-EXECUTION-PACKET§10-final-freeze-O4 | run semantic compiler/contract validation; |
| KICKOFF-EXECUTION-PACKET§10-final-freeze-O5 | run demo health; |
| KICKOFF-EXECUTION-PACKET§10-final-freeze-O6 | run critical accessibility/security checks; |
| KICKOFF-EXECUTION-PACKET§10-final-freeze-O7 | generate exact-build proof bundle; |
| KICKOFF-EXECUTION-PACKET§10-final-freeze-O8 | record deployed commit; |
| KICKOFF-EXECUTION-PACKET§10-final-freeze-O9 | tag/freeze submission; |
| KICKOFF-EXECUTION-PACKET§10-final-freeze-O10 | preserve local fallback/spectator state. |
