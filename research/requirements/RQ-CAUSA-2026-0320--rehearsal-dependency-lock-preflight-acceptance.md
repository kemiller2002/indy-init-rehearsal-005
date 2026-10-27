---
id: RQ-CAUSA-2026-0320
title: "REHEARSAL-DEPENDENCY-LOCK§preflight-acceptance Preflight acceptance"
status: accepted
version: 1.0.0
owners: [indy-init-rehearsal-005]
created: 2026-10-27
updated: 2026-10-27
priority: medium
tags: [rehearsal-005, identity-derivation]
source: "REHEARSAL-DEPENDENCY-LOCK§preflight-acceptance"
provenance:
  contributions:
    EXE-20261027T201410340Z-c35e9b80:
      operations: [created]
      at: 2026-10-27T20:17:42.374Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: claude-opus-5-5
        runtime: claude-code
      reason: "Identity derivation of the pinned Indy-init baseline into Praxis RQ form (WI-0002, tools/derive-requirements)"
      evidence: [research/requirements-preservation/derivation-record.json]
---

# RQ-CAUSA-2026-0320

Derived from baseline requirement `REHEARSAL-DEPENDENCY-LOCK§preflight-acceptance` (Preflight acceptance). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Acceptance criterion |
|---|---|
| REHEARSAL-DEPENDENCY-LOCK§preflight-acceptance-O1 | Discover the newest approved stable versions from actual release sources. |
| REHEARSAL-DEPENDENCY-LOCK§preflight-acceptance-O2 | Resolve the dependency graph, including Forma foundations and Echelon integrations. |
| REHEARSAL-DEPENDENCY-LOCK§preflight-acceptance-O3 | Execute compatibility and security gates. |
| REHEARSAL-DEPENDENCY-LOCK§preflight-acceptance-O4 | Produce a versioned stack manifest and verification report. |
| REHEARSAL-DEPENDENCY-LOCK§preflight-acceptance-O5 | Freeze only after PASS; otherwise mark BLOCKED and investigate. |
| REHEARSAL-DEPENDENCY-LOCK§preflight-acceptance-O6 | Re-run discovery at the next kickoff. |
| REHEARSAL-DEPENDENCY-LOCK§preflight-acceptance-O7 | No live registry discovery or build verification is claimed by this document. |
