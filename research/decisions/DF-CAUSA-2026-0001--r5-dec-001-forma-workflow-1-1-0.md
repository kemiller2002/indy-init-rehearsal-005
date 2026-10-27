---
id: DF-CAUSA-2026-0001
title: "R5-DEC-001 Owner approves @echelon-foundry/forma-workflow 1.1.0 for Rehearsal 5"
status: accepted
version: 1.0.0
owners: [indy-init-rehearsal-005]
created: 2026-10-27
updated: 2026-10-27
tags: [rehearsal-005, dependency, owner-decision]
provenance:
  contributions:
    EXE-20261027T204350795Z-d44bb1b9:
      operations: [created]
      at: 2026-10-27T22:56:37.689Z
      actor:
        kind: agent
        id: claude
        provider: anthropic
        model: unknown
        runtime: claude-code
      reason: "Owner decision transcribed by the coordinating agent"
---

# R5-DEC-001: `@echelon-foundry/forma-workflow` 1.1.0

- **Decision:** approved. Rehearsal 5 uses `@echelon-foundry/forma-workflow` 1.1.0 as the causal graph's workflow layout engine (WDL-001, T23/T24).
- **Decided by:** the owner, kemiller2002, on 2026-10-27, during the rehearsal, in reply to the coordinator's question. The owner's answer was "Approved".
- **Recorded by:** the coordinating agent. The owner did not write this file. As with the RDP-003 deviations in Indy-init `kickoff/rehearsal-005/README.md`, the decision is the owner's and the transcription is the agent's. The Rehearsal 5 report must list it.

## Why a decision is needed

- The package is not in the echelon-current 1.29.0 release set, so the T11 stack lock gate does not cover it (VER-001, VER-004, VER-005-O2).
- R4-DEC-001 approved version 1.0.0 for Rehearsal 4 only. No approval covered 1.1.0.
- 1.1.0 is a stable, published npm release from the owning repository, `kemiller2002/forma` `packages/workflow`, at gitHead `e0b088dbbe98b05221c4bc5b5e2224e9bf470827`. It was published 2026-10-27T19:43:57Z with an npm SLSA provenance attestation. Its integrity is pinned in `package-lock.json` and `src/app/capabilities/workflow-layout-engine.json`.

## Scope and limits

- The decision covers this exact version for Rehearsal 5. It is not a standing approval for later versions.
- It does not weaken any requirement. WDL-001..WDL-015 still need their tests.
- The Registry gap stays open. Adding the package to an echelon-current release set belongs to echelon-registry, through its Praxis input after the rehearsal. This decision does not route it.
