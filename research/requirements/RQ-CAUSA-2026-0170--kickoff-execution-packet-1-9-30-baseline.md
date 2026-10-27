---
id: RQ-CAUSA-2026-0170
title: "KICKOFF-EXECUTION-PACKET§1-9-30-baseline 1. 9:30 baseline"
status: accepted
version: 1.0.0
owners: [indy-init-rehearsal-005]
created: 2026-10-27
updated: 2026-10-27
priority: medium
tags: [rehearsal-005, identity-derivation]
source: "KICKOFF-EXECUTION-PACKET§1-9-30-baseline"
provenance:
  contributions:
    EXE-20261027T201410340Z-c35e9b80:
      operations: [created]
      at: 2026-10-27T20:16:38.705Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: claude-opus-5-5
        runtime: claude-code
      reason: "Identity derivation of the pinned Indy-init baseline into Praxis RQ form (WI-0002, tools/derive-requirements)"
      evidence: [research/requirements-preservation/derivation-record.json]
---

# RQ-CAUSA-2026-0170

Derived from baseline requirement `KICKOFF-EXECUTION-PACKET§1-9-30-baseline` (1. 9:30 baseline). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Acceptance criterion |
|---|---|
| KICKOFF-EXECUTION-PACKET§1-9-30-baseline-O2 | verify target implementation repository is empty/organizer-compliant; |
| KICKOFF-EXECUTION-PACKET§1-9-30-baseline-O9 | run Conditor verify/doctor; |

| ID | Statement |
|---|---|
| KICKOFF-EXECUTION-PACKET§1-9-30-baseline-O1 | Before application implementation: |
| KICKOFF-EXECUTION-PACKET§1-9-30-baseline-O3 | capture timestamp and repository HEAD/tree; |
| KICKOFF-EXECUTION-PACKET§1-9-30-baseline-O4 | capture workstation/runtime versions; |
| KICKOFF-EXECUTION-PACKET§1-9-30-baseline-O5 | pin the exact planning repository commit; |
| KICKOFF-EXECUTION-PACKET§1-9-30-baseline-O6 | run Conditor plan; |
| KICKOFF-EXECUTION-PACKET§1-9-30-baseline-O7 | inspect planned mutations; |
| KICKOFF-EXECUTION-PACKET§1-9-30-baseline-O8 | run Conditor init; |
| KICKOFF-EXECUTION-PACKET§1-9-30-baseline-O10 | record generated lock and component versions; |
| KICKOFF-EXECUTION-PACKET§1-9-30-baseline-O11 | create first post-bootstrap commit; |
| KICKOFF-EXECUTION-PACKET§1-9-30-baseline-O12 | create/start initial ROS work item; |
| KICKOFF-EXECUTION-PACKET§1-9-30-baseline-O13 | capture Praxis baseline. |
| KICKOFF-EXECUTION-PACKET§1-9-30-baseline-O14 | No application feature coding begins before this evidence exists. |
