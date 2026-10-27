---
id: RQ-CAUSA-2026-0383
title: "SYSTEM-VERSION-AND-COMPATIBILITY§acceptance Acceptance"
status: accepted
version: 1.0.0
owners: [indy-init-rehearsal-005]
created: 2026-10-27
updated: 2026-10-27
priority: medium
tags: [rehearsal-005, identity-derivation]
source: "SYSTEM-VERSION-AND-COMPATIBILITY§acceptance"
provenance:
  contributions:
    EXE-20261027T201410340Z-c35e9b80:
      operations: [created]
      at: 2026-10-27T20:18:09.617Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: claude-opus-5-5
        runtime: claude-code
      reason: "Identity derivation of the pinned Indy-init baseline into Praxis RQ form (WI-0002, tools/derive-requirements)"
      evidence: [research/requirements-preservation/derivation-record.json]
---

# RQ-CAUSA-2026-0383

Derived from baseline requirement `SYSTEM-VERSION-AND-COMPATIBILITY§acceptance` (Acceptance). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Acceptance criterion |
|---|---|
| SYSTEM-VERSION-AND-COMPATIBILITY§acceptance-O1 | Produce a version inventory from authoritative sources at baseline lock. |
| SYSTEM-VERSION-AND-COMPATIBILITY§acceptance-O2 | Select the newest stable compatible release of every applicable system, with explicit evidence. |
| SYSTEM-VERSION-AND-COMPATIBILITY§acceptance-O3 | Prove installed package and runtime versions match the immutable lock. |
| SYSTEM-VERSION-AND-COMPATIBILITY§acceptance-O4 | Exercise cross-system contract, fixture, integration, and deployed-browser tests. |
| SYSTEM-VERSION-AND-COMPATIBILITY§acceptance-O5 | Reject a newer incompatible release rather than silently downgrading. |
| SYSTEM-VERSION-AND-COMPATIBILITY§acceptance-O6 | Verify that Administration shows actual versions and investigation pages do not. |
| SYSTEM-VERSION-AND-COMPATIBILITY§acceptance-O7 | Preserve the full manifest and verification report alongside the rehearsal evidence. |
| SYSTEM-VERSION-AND-COMPATIBILITY§acceptance-O8 | This specification does not claim any current version is the latest; it requires the next run to establish that fact. |
