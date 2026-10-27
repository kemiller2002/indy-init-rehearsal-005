---
id: RQ-CAUSA-2026-0143
title: "DEP-CURRENT-008 Enforcement"
status: accepted
version: 1.0.0
owners: [indy-init-rehearsal-005]
created: 2026-10-27
updated: 2026-10-27
priority: medium
tags: [rehearsal-005, identity-derivation]
source: "DEP-CURRENT-008"
provenance:
  contributions:
    EXE-20261027T201410340Z-c35e9b80:
      operations: [created]
      at: 2026-10-27T20:16:28.129Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: claude-opus-5-5
        runtime: claude-code
      reason: "Identity derivation of the pinned Indy-init baseline into Praxis RQ form (WI-0002, tools/derive-requirements)"
      evidence: [research/requirements-preservation/derivation-record.json]
---

# RQ-CAUSA-2026-0143

Derived from baseline requirement `DEP-CURRENT-008` (Enforcement). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Statement |
|---|---|
| DEP-CURRENT-008-O1 | Add an automated dependency-discovery job and a compatibility matrix gate before agent fan-out. |
| DEP-CURRENT-008-O2 | Fail CI if actual installed versions differ from the run lock, or if required version, SHA, provenance, or verification evidence is missing. |
| DEP-CURRENT-008-O3 | Store a diff against the previous run so updates are explicit. |
