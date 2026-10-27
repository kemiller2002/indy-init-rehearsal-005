---
id: RQ-CAUSA-2026-0136
title: "DEP-CURRENT-001 Discover every run"
status: accepted
version: 1.0.0
owners: [indy-init-rehearsal-005]
created: 2026-10-27
updated: 2026-10-27
priority: medium
tags: [rehearsal-005, identity-derivation]
source: "DEP-CURRENT-001"
provenance:
  contributions:
    EXE-20261027T201410340Z-c35e9b80:
      operations: [created]
      at: 2026-10-27T20:16:25.260Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: claude-opus-5-5
        runtime: claude-code
      reason: "Identity derivation of the pinned Indy-init baseline into Praxis RQ form (WI-0002, tools/derive-requirements)"
      evidence: [research/requirements-preservation/derivation-record.json]
---

# RQ-CAUSA-2026-0136

Derived from baseline requirement `DEP-CURRENT-001` (Discover every run). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Acceptance criterion |
|---|---|
| DEP-CURRENT-001-O2 | This includes Forma, Forma foundations, Limen, Folio, Fides when used, Arca when used, Strata when used, Ordo, Praxis, Dokimos, Percepta, Aegis, Conditor, routing, .NET/F#, JavaScript/TypeScript, browser test tools, and transitive components that materially affect compatibility. |

| ID | Statement |
|---|---|
| DEP-CURRENT-001-O1 | Before every rehearsal, competition run, or new application build, inventory every required Echelon dependency and toolchain component from actual manifests, registries, release artifacts, and source repositories. |
| DEP-CURRENT-001-O3 | A system not in scope must be marked NOT_APPLICABLE, not assumed installed. |
