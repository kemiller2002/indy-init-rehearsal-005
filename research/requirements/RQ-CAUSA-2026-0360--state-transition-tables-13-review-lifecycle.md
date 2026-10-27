---
id: RQ-CAUSA-2026-0360
title: "STATE-TRANSITION-TABLES§13-review-lifecycle 13. Review lifecycle"
status: accepted
version: 1.0.0
owners: [indy-init-rehearsal-005]
created: 2026-10-27
updated: 2026-10-27
priority: medium
tags: [rehearsal-005, identity-derivation]
source: "STATE-TRANSITION-TABLES§13-review-lifecycle"
provenance:
  contributions:
    EXE-20261027T201410340Z-c35e9b80:
      operations: [created]
      at: 2026-10-27T20:17:59.309Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: claude-opus-5-5
        runtime: claude-code
      reason: "Identity derivation of the pinned Indy-init baseline into Praxis RQ form (WI-0002, tools/derive-requirements)"
      evidence: [research/requirements-preservation/derivation-record.json]
---

# RQ-CAUSA-2026-0360

Derived from baseline requirement `STATE-TRANSITION-TABLES§13-review-lifecycle` (13. Review lifecycle). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Statement |
|---|---|
| STATE-TRANSITION-TABLES§13-review-lifecycle-O1 | States: |
| STATE-TRANSITION-TABLES§13-review-lifecycle-O2 | Requested |
| STATE-TRANSITION-TABLES§13-review-lifecycle-O3 | Approved |
| STATE-TRANSITION-TABLES§13-review-lifecycle-O4 | Rejected |
| STATE-TRANSITION-TABLES§13-review-lifecycle-O5 | Withdrawn |
| STATE-TRANSITION-TABLES§13-review-lifecycle-O6 | Expired |
| STATE-TRANSITION-TABLES§13-review-lifecycle-O7 | \| From \| Command \| To \| |
| STATE-TRANSITION-TABLES§13-review-lifecycle-O8 | \| none \| RequestReview \| Requested \| |
| STATE-TRANSITION-TABLES§13-review-lifecycle-O9 | \| Requested \| ApproveReview \| Approved \| |
| STATE-TRANSITION-TABLES§13-review-lifecycle-O10 | \| Requested \| RejectReview \| Rejected \| |
| STATE-TRANSITION-TABLES§13-review-lifecycle-O11 | \| Requested \| WithdrawReview \| Withdrawn \| |
| STATE-TRANSITION-TABLES§13-review-lifecycle-O12 | \| Requested \| ExpireReview \| Expired \| |
| STATE-TRANSITION-TABLES§13-review-lifecycle-O13 | A material state change may make an Approved review stale for a later transition without rewriting the historical review. |
