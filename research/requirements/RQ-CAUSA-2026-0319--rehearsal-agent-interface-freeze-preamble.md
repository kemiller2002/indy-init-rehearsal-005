---
id: RQ-CAUSA-2026-0319
title: "REHEARSAL-AGENT-INTERFACE-FREEZE§preamble Next Rehearsal Agent Handoff Contract"
status: accepted
version: 1.0.0
owners: [indy-init-rehearsal-005]
created: 2026-10-27
updated: 2026-10-27
priority: medium
tags: [rehearsal-005, identity-derivation]
source: "REHEARSAL-AGENT-INTERFACE-FREEZE§preamble"
provenance:
  contributions:
    EXE-20261027T201410340Z-c35e9b80:
      operations: [created]
      at: 2026-10-27T20:17:41.938Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: claude-opus-5-5
        runtime: claude-code
      reason: "Identity derivation of the pinned Indy-init baseline into Praxis RQ form (WI-0002, tools/derive-requirements)"
      evidence: [research/requirements-preservation/derivation-record.json]
---

# RQ-CAUSA-2026-0319

Derived from baseline requirement `REHEARSAL-AGENT-INTERFACE-FREEZE§preamble` (Next Rehearsal Agent Handoff Contract). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Acceptance criterion |
|---|---|
| REHEARSAL-AGENT-INTERFACE-FREEZE§preamble-O4 | Integration owner: dependency lock, contract tests, CI, review, deployed-SHA proof, release-candidate batching. |
| REHEARSAL-AGENT-INTERFACE-FREEZE§preamble-O7 | Output MUST distinguish accepted semantic events/new version from typed rejection/fault. |
| REHEARSAL-AGENT-INTERFACE-FREEZE§preamble-O22 | Extract actual types and exported interfaces from accepted R001 F# source and Limen adapter. |
| REHEARSAL-AGENT-INTERFACE-FREEZE§preamble-O23 | Publish one versioned interface manifest including module/type names, request/response examples, error variants and contract tests. |
| REHEARSAL-AGENT-INTERFACE-FREEZE§preamble-O30 | Interface manifest SHA, owner matrix, producer/consumer tests, negative-case results, and decision log. |

| ID | Statement |
|---|---|
| REHEARSAL-AGENT-INTERFACE-FREEZE§preamble-O1 | Domain owner: F# command/decision/event semantics, policy and capability derivation. |
| REHEARSAL-AGENT-INTERFACE-FREEZE§preamble-O2 | UI owner: Forma shell, Limen host, screen projections, accessibility and loading experience. |
| REHEARSAL-AGENT-INTERFACE-FREEZE§preamble-O3 | Persistence owner: provider-neutral adapter, frozen contract mapping, local queue, optimistic concurrency and remote acknowledgment. |
| REHEARSAL-AGENT-INTERFACE-FREEZE§preamble-O5 | Coordinator: interface change control, escalations and requirements ledger. |
| REHEARSAL-AGENT-INTERFACE-FREEZE§preamble-O6 | Input MUST include investigation ID, actor/provenance, typed command, expected aggregate version, and command identity/idempotency context where supported. |
| REHEARSAL-AGENT-INTERFACE-FREEZE§preamble-O8 | UI MUST NOT generate authoritative events directly. |
| REHEARSAL-AGENT-INTERFACE-FREEZE§preamble-O9 | Domain owner publishes concrete F# signatures and JSON/WASM boundary shape from source. |
| REHEARSAL-AGENT-INTERFACE-FREEZE§preamble-O10 | A single immutable projection identifies investigation, aggregate version, lifecycle, hypotheses, evidence, contradictions, obligations, causal assertions, permitted capabilities and reasons for blocked actions. |
| REHEARSAL-AGENT-INTERFACE-FREEZE§preamble-O11 | Fields MUST map to existing domain types; do not add synthetic confidence, inferred cause, or made-up severity/owner. |
| REHEARSAL-AGENT-INTERFACE-FREEZE§preamble-O12 | Every screen must consume the same versioned projection or explicitly label stale state. |
| REHEARSAL-AGENT-INTERFACE-FREEZE§preamble-O13 | Given an immutable canonical ordered history and semantic/policy version, replay validates every event through the authoritative engine and either yields a projection or a typed invalid-history fault. |
| REHEARSAL-AGENT-INTERFACE-FREEZE§preamble-O14 | No alternative browser-only validator. |
| REHEARSAL-AGENT-INTERFACE-FREEZE§preamble-O15 | Frozen fixtures are read-only. |
| REHEARSAL-AGENT-INTERFACE-FREEZE§preamble-O16 | Provider-neutral operations: load, append with expected version, acknowledge remote persistence, surface conflict, and recover local edits. |
| REHEARSAL-AGENT-INTERFACE-FREEZE§preamble-O17 | Separate local-durable from remotely-synced status. |
| REHEARSAL-AGENT-INTERFACE-FREEZE§preamble-O18 | Storage adapter alone maps to causa-data v1.0.0 paths/serialization. |
| REHEARSAL-AGENT-INTERFACE-FREEZE§preamble-O19 | No secrets in browser bundles. |
| REHEARSAL-AGENT-INTERFACE-FREEZE§preamble-O20 | Folio receives a read-only, versioned report projection with provenance, unresolved uncertainties, conclusions and verification status as present. |
| REHEARSAL-AGENT-INTERFACE-FREEZE§preamble-O21 | Report rendering cannot elevate an unverified hypothesis to a confirmed cause. |
| REHEARSAL-AGENT-INTERFACE-FREEZE§preamble-O24 | Run golden replay of all five frozen scenarios and negative-history cases. |
| REHEARSAL-AGENT-INTERFACE-FREEZE§preamble-O25 | Each owner signs off against the exact interface-manifest SHA before parallel work. |
| REHEARSAL-AGENT-INTERFACE-FREEZE§preamble-O26 | Breaking change requires coordinator decision, manifest revision, consumer notification and compatibility proof. |
| REHEARSAL-AGENT-INTERFACE-FREEZE§preamble-O27 | No agent edits another owner's shared interface independently. |
| REHEARSAL-AGENT-INTERFACE-FREEZE§preamble-O28 | An interface not extracted and validated is BLOCKED, not frozen. |
| REHEARSAL-AGENT-INTERFACE-FREEZE§preamble-O29 | Stale aggregate version; forged/illegal history; unknown required semantic extension; unauthorized command; duplicate submission; conflicting remote write; offline queue; mismatched frozen schema; incomplete verification; incorrect deployed SHA. |
| REHEARSAL-AGENT-INTERFACE-FREEZE§preamble-O31 | Integrate defect-class closure from DEFECT-CLASS-CLOSURE-AND-RELEASE-GATES.md. |
