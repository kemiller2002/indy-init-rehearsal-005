---
id: RQ-CAUSA-2026-0138
title: "DEP-CURRENT-003 Resolve the stack as a set"
status: accepted
version: 1.0.0
owners: [indy-init-rehearsal-005]
created: 2026-10-27
updated: 2026-10-27
priority: medium
tags: [rehearsal-005, identity-derivation]
source: "DEP-CURRENT-003"
provenance:
  contributions:
    EXE-20261027T201410340Z-c35e9b80:
      operations: [created]
      at: 2026-10-27T20:16:26.040Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: claude-opus-5-5
        runtime: claude-code
      reason: "Identity derivation of the pinned Indy-init baseline into Praxis RQ form (WI-0002, tools/derive-requirements)"
      evidence: [research/requirements-preservation/derivation-record.json]
---

# RQ-CAUSA-2026-0138

Derived from baseline requirement `DEP-CURRENT-003` (Resolve the stack as a set). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Statement |
|---|---|
| DEP-CURRENT-003-O1 | Check cross-component dependency constraints, shared contract versions, WASM boundary compatibility, .NET SDK/toolchain, Forma foundations and component compatibility, Folio integration, and auth/storage adapter contracts. |
| DEP-CURRENT-003-O2 | Never select independent latest versions without resolving the whole graph. |
