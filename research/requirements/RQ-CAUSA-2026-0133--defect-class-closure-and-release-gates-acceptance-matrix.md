---
id: RQ-CAUSA-2026-0133
title: "DEFECT-CLASS-CLOSURE-AND-RELEASE-GATES§acceptance-matrix Acceptance matrix"
status: accepted
version: 1.0.0
owners: [indy-init-rehearsal-005]
created: 2026-10-27
updated: 2026-10-27
priority: medium
tags: [rehearsal-005, identity-derivation]
source: "DEFECT-CLASS-CLOSURE-AND-RELEASE-GATES§acceptance-matrix"
provenance:
  contributions:
    EXE-20261027T201410340Z-c35e9b80:
      operations: [created]
      at: 2026-10-27T20:16:24.000Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: claude-opus-5-5
        runtime: claude-code
      reason: "Identity derivation of the pinned Indy-init baseline into Praxis RQ form (WI-0002, tools/derive-requirements)"
      evidence: [research/requirements-preservation/derivation-record.json]
---

# RQ-CAUSA-2026-0133

Derived from baseline requirement `DEFECT-CLASS-CLOSURE-AND-RELEASE-GATES§acceptance-matrix` (Acceptance matrix). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Acceptance criterion |
|---|---|
| DEFECT-CLASS-CLOSURE-AND-RELEASE-GATES§acceptance-matrix-O1 | Every critical invariant has an owner and proof obligation before implementation fan-out. |
| DEFECT-CLASS-CLOSURE-AND-RELEASE-GATES§acceptance-matrix-O2 | An independent architecture/invariant review occurs before the first release candidate. |
| DEFECT-CLASS-CLOSURE-AND-RELEASE-GATES§acceptance-matrix-O3 | No critical or recurring defect class is closed by instance-only patching. |
| DEFECT-CLASS-CLOSURE-AND-RELEASE-GATES§acceptance-matrix-O4 | Every such closure has SHA-bound regression, adversarial coverage, and independent approval. |
| DEFECT-CLASS-CLOSURE-AND-RELEASE-GATES§acceptance-matrix-O5 | Verified completion is blocked on unresolved critical classes. |
| DEFECT-CLASS-CLOSURE-AND-RELEASE-GATES§acceptance-matrix-O6 | The exact deployed SHA has a successful acceptance-job conclusion before Accepted is asserted. |
| DEFECT-CLASS-CLOSURE-AND-RELEASE-GATES§acceptance-matrix-O7 | Final report includes release count, time breakdown, recurrence rate, token cost, and correctness/coverage evidence. |
