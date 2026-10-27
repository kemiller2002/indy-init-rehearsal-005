---
id: RQ-CAUSA-2026-0169
title: "FROZEN-DEMO-STORAGE§required-evidence Required evidence"
status: accepted
version: 1.0.0
owners: [indy-init-rehearsal-005]
created: 2026-10-27
updated: 2026-10-27
priority: medium
tags: [rehearsal-005, identity-derivation]
source: "FROZEN-DEMO-STORAGE§required-evidence"
provenance:
  contributions:
    EXE-20261027T201410340Z-c35e9b80:
      operations: [created]
      at: 2026-10-27T20:16:38.313Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: claude-opus-5-5
        runtime: claude-code
      reason: "Identity derivation of the pinned Indy-init baseline into Praxis RQ form (WI-0002, tools/derive-requirements)"
      evidence: [research/requirements-preservation/derivation-record.json]
---

# RQ-CAUSA-2026-0169

Derived from baseline requirement `FROZEN-DEMO-STORAGE§required-evidence` (Required evidence). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Acceptance criterion |
|---|---|
| FROZEN-DEMO-STORAGE§required-evidence-O1 | STORAGE-CONTRACT-v1.0.md and machine-readable schema |
| FROZEN-DEMO-STORAGE§required-evidence-O2 | Fixture manifest and checksums |
| FROZEN-DEMO-STORAGE§required-evidence-O3 | Schema compatibility test matrix |
| FROZEN-DEMO-STORAGE§required-evidence-O4 | Replay/projection golden results |
| FROZEN-DEMO-STORAGE§required-evidence-O5 | GitHub read/write/conflict/reload evidence |
| FROZEN-DEMO-STORAGE§required-evidence-O6 | Explicit release gate result for each run |
