---
id: RQ-CAUSA-2026-0356
title: "STATE-TRANSITION-TABLES§1-investigation-lifecycle 1. Investigation lifecycle"
status: accepted
version: 1.0.0
owners: [indy-init-rehearsal-005]
created: 2026-10-27
updated: 2026-10-27
priority: medium
tags: [rehearsal-005, identity-derivation]
source: "STATE-TRANSITION-TABLES§1-investigation-lifecycle"
provenance:
  contributions:
    EXE-20261027T201410340Z-c35e9b80:
      operations: [created]
      at: 2026-10-27T20:17:57.537Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: claude-opus-5-5
        runtime: claude-code
      reason: "Identity derivation of the pinned Indy-init baseline into Praxis RQ form (WI-0002, tools/derive-requirements)"
      evidence: [research/requirements-preservation/derivation-record.json]
---

# RQ-CAUSA-2026-0356

Derived from baseline requirement `STATE-TRANSITION-TABLES§1-investigation-lifecycle` (1. Investigation lifecycle). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Statement |
|---|---|
| STATE-TRANSITION-TABLES§1-investigation-lifecycle-O1 | Persisted states: |
| STATE-TRANSITION-TABLES§1-investigation-lifecycle-O2 | Draft |
| STATE-TRANSITION-TABLES§1-investigation-lifecycle-O3 | Active |
| STATE-TRANSITION-TABLES§1-investigation-lifecycle-O4 | AwaitingEvidence |
| STATE-TRANSITION-TABLES§1-investigation-lifecycle-O5 | AwaitingVerification |
| STATE-TRANSITION-TABLES§1-investigation-lifecycle-O6 | Resolved |
| STATE-TRANSITION-TABLES§1-investigation-lifecycle-O7 | Closed |
| STATE-TRANSITION-TABLES§1-investigation-lifecycle-O8 | Archived |
| STATE-TRANSITION-TABLES§1-investigation-lifecycle-O9 | Reopened is an event that normally returns Resolved/Closed to Active. |
| STATE-TRANSITION-TABLES§1-investigation-lifecycle-O10 | \| From \| Command \| To \| Minimum structural condition \| |
| STATE-TRANSITION-TABLES§1-investigation-lifecycle-O11 | \| none \| StartInvestigation \| Draft \| valid identity/source/manifest \| |
| STATE-TRANSITION-TABLES§1-investigation-lifecycle-O12 | \| Draft \| ActivateInvestigation \| Active \| activation policy satisfied \| |
| STATE-TRANSITION-TABLES§1-investigation-lifecycle-O13 | \| Active \| MarkAwaitingEvidence \| AwaitingEvidence \| open evidence dependency/reason \| |
| STATE-TRANSITION-TABLES§1-investigation-lifecycle-O14 | \| AwaitingEvidence \| ActivateInvestigation \| Active \| wait cleared/work resumed \| |
| STATE-TRANSITION-TABLES§1-investigation-lifecycle-O15 | \| Active \| MarkAwaitingVerification \| AwaitingVerification \| verification work exists \| |
| STATE-TRANSITION-TABLES§1-investigation-lifecycle-O16 | \| AwaitingVerification \| ActivateInvestigation \| Active \| verification failed/new work \| |
| STATE-TRANSITION-TABLES§1-investigation-lifecycle-O17 | \| AwaitingVerification \| ResolveInvestigation \| Resolved \| resolution gate \| |
| STATE-TRANSITION-TABLES§1-investigation-lifecycle-O18 | \| Active \| ResolveInvestigation \| Resolved \| policy permits and resolution gate \| |
| STATE-TRANSITION-TABLES§1-investigation-lifecycle-O19 | \| Resolved \| CloseInvestigation \| Closed \| closure policy \| |
| STATE-TRANSITION-TABLES§1-investigation-lifecycle-O20 | \| Resolved/Closed \| ReopenInvestigation \| Active \| explicit reopening basis \| |
| STATE-TRANSITION-TABLES§1-investigation-lifecycle-O21 | \| any non-archived state \| ArchiveInvestigation \| Archived \| archive policy \| |
| STATE-TRANSITION-TABLES§1-investigation-lifecycle-O22 | \| Archived \| ActivateInvestigation \| Active \| explicit unarchive/reactivation policy \| |
