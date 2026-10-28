---
id: DF-CAUSA-2026-0002
title: "R5-DEC-002 Owner scope decision: Percepta contract S16 (counterfactual sandbox) is NOT-APPLICABLE to Causa in Rehearsal 5"
status: accepted
version: 1.0.0
owners: [indy-init-rehearsal-005]
created: 2026-10-27
updated: 2026-10-27
tags: [rehearsal-005, scope, percepta, owner-decision]
provenance:
  contributions:
    EXE-20261027T204350795Z-d44bb1b9:
      operations: [created]
      at: 2026-10-27T23:49:23.007Z
      actor:
        kind: agent
        id: claude
        provider: anthropic
        model: unknown
        runtime: claude-code
      reason: "Owner scope decision transcribed by the coordinating agent"
---

# R5-DEC-002: Percepta contract S16 is NOT-APPLICABLE

- **Decision:** the screen contract `S16-counterfactual-sandbox` (`.percepta/contracts/s16-counterfactual-sandbox.json`) is NOT-APPLICABLE to Causa for Rehearsal 5. It may be bound as not-applicable, citing this record.
- **Decided by:** the owner, kemiller2002, on 2026-10-27 (about 23:50Z), during the rehearsal. The coordinator asked a multi-select question listing S16, S17, S18, and S20+S21 as candidates; the owner selected **only S16**.
- **Recorded by:** the coordinating agent. The owner did not write this file. The Rehearsal 5 report must list this transcription.

## Not decided (stay in scope)

S17 (multi-agent panel), S18 (research tournament), S20 (factory view) and S21 (requirements proof) were offered and **not** approved. They stay in scope. Unless they are implemented and bound, they remain NOT-BOUND, and gate G5 cannot pass. S09, S10 and S19 were never offered as candidates and stay in scope.

## Basis

S16 is a generic Indy-Init screen contract. The Causa engine has no counterfactual capability; the stream core review on 2026-10-27 found no supporting requirement beyond the contract itself. The decision is the owner's scope call, not an agent's.
