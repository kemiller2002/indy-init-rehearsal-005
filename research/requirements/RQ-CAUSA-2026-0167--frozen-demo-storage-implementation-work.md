---
id: RQ-CAUSA-2026-0167
title: "FROZEN-DEMO-STORAGE§implementation-work Implementation work"
status: accepted
version: 1.0.0
owners: [indy-init-rehearsal-005]
created: 2026-10-27
updated: 2026-10-27
priority: medium
tags: [rehearsal-005, identity-derivation]
source: "FROZEN-DEMO-STORAGE§implementation-work"
provenance:
  contributions:
    EXE-20261027T201410340Z-c35e9b80:
      operations: [created]
      at: 2026-10-27T20:16:37.533Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: claude-opus-5-5
        runtime: claude-code
      reason: "Identity derivation of the pinned Indy-init baseline into Praxis RQ form (WI-0002, tools/derive-requirements)"
      evidence: [research/requirements-preservation/derivation-record.json]
---

# RQ-CAUSA-2026-0167

Derived from baseline requirement `FROZEN-DEMO-STORAGE§implementation-work` (Implementation work). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Statement |
|---|---|
| FROZEN-DEMO-STORAGE§implementation-work-O1 | Agent must inspect docs/PERSISTENCE-CONTRACT.md and actual rehearsal serialization before freezing version 1.0. |
| FROZEN-DEMO-STORAGE§implementation-work-O2 | Existing example physical layouts in that document are reversible proposals, not yet a committed frozen schema. |
| FROZEN-DEMO-STORAGE§implementation-work-O3 | Resolve that explicitly and record the decision. |
| FROZEN-DEMO-STORAGE§implementation-work-O4 | Place executable implementation work into each target repository's documented Praxis input folder, never GitHub Issues. |
