---
id: RQ-CAUSA-2026-0367
title: "STATE-TRANSITION-TABLES§4-evidence-lifecycle 4. Evidence lifecycle"
status: accepted
version: 1.0.0
owners: [indy-init-rehearsal-005]
created: 2026-10-27
updated: 2026-10-27
priority: medium
tags: [rehearsal-005, identity-derivation]
source: "STATE-TRANSITION-TABLES§4-evidence-lifecycle"
provenance:
  contributions:
    EXE-20261027T201410340Z-c35e9b80:
      operations: [created]
      at: 2026-10-27T20:18:02.269Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: claude-opus-5-5
        runtime: claude-code
      reason: "Identity derivation of the pinned Indy-init baseline into Praxis RQ form (WI-0002, tools/derive-requirements)"
      evidence: [research/requirements-preservation/derivation-record.json]
---

# RQ-CAUSA-2026-0367

Derived from baseline requirement `STATE-TRANSITION-TABLES§4-evidence-lifecycle` (4. Evidence lifecycle). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Statement |
|---|---|
| STATE-TRANSITION-TABLES§4-evidence-lifecycle-O1 | Evidence is append/supersede rather than a broad workflow. |
| STATE-TRANSITION-TABLES§4-evidence-lifecycle-O2 | \| State \| Command \| Result \| |
| STATE-TRANSITION-TABLES§4-evidence-lifecycle-O3 | \| none \| AdmitEvidence \| Active \| |
| STATE-TRANSITION-TABLES§4-evidence-lifecycle-O4 | \| Active \| VerifyEvidence \| Active + verification assessment \| |
| STATE-TRANSITION-TABLES§4-evidence-lifecycle-O5 | \| Active \| CorrectEvidenceMetadata \| Active + corrective event \| |
| STATE-TRANSITION-TABLES§4-evidence-lifecycle-O6 | \| Active \| SupersedeEvidence \| Superseded \| |
| STATE-TRANSITION-TABLES§4-evidence-lifecycle-O7 | \| Active/Superseded \| MarkEvidenceArtifactUnavailable \| availability changes \| |
| STATE-TRANSITION-TABLES§4-evidence-lifecycle-O8 | \| Active/Superseded \| RecordEvidenceIntegrityMismatch \| integrity status changes \| |
| STATE-TRANSITION-TABLES§4-evidence-lifecycle-O9 | Original evidence content is never overwritten. |
