---
id: RQ-CAUSA-2026-0052
title: "CAUSA-STORAGE-RELEASE§identities Identities"
status: accepted
version: 1.0.0
owners: [indy-init-rehearsal-005]
created: 2026-10-27
updated: 2026-10-27
priority: medium
tags: [rehearsal-005, identity-derivation]
source: "CAUSA-STORAGE-RELEASE§identities"
provenance:
  contributions:
    EXE-20261027T201410340Z-c35e9b80:
      operations: [created]
      at: 2026-10-27T20:15:45.873Z
      actor:
        kind: agent
        id: anthropic/claude-code
        provider: anthropic
        model: claude-opus-5-5
        runtime: claude-code
      reason: "Identity derivation of the pinned Indy-init baseline into Praxis RQ form (WI-0002, tools/derive-requirements)"
      evidence: [research/requirements-preservation/derivation-record.json]
---

# RQ-CAUSA-2026-0052

Derived from baseline requirement `CAUSA-STORAGE-RELEASE§identities` (Identities). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Statement |
|---|---|
| CAUSA-STORAGE-RELEASE§identities-O1 | \| identity \| value \| |
| CAUSA-STORAGE-RELEASE§identities-O2 | \| contract version \| `1.0.0` \| |
| CAUSA-STORAGE-RELEASE§identities-O3 | \| release tag \| `causa-storage-v1.0.0` (tag object `fd2991a3…`) → `c947c7e354be80766573a782546c8186336774cc` \| |
| CAUSA-STORAGE-RELEASE§identities-O4 | \| schema identity: SHA-256 of `contract/v1.0.0/schemas/bundle.json` \| `0a68af8c93190d2e4d6c27beb2f8cb67c3aebdda586d01beb7122b71af179152` \| |
| CAUSA-STORAGE-RELEASE§identities-O5 | \| dataset identity: SHA-256 of `fixtures/v1.0.0/manifest.json` \| `f650a2e509a420872b46177b21f57ef6ebd7625ae4c69dd92198d3ca472c2b86` \| |
| CAUSA-STORAGE-RELEASE§identities-O6 | \| release identity: SHA-256 of `compatibility/v1.0.0/lock.json` \| `6a7e7802ff2a3ff500ab6065662514a96e09e6c15eb30212ba1377fcc5bf8fc5` \| |
| CAUSA-STORAGE-RELEASE§identities-O7 | \| domain engine \| `indy-init-rehearsal-001@55f49f61c939a3a780cee592f36f732a1328f981` \| |
| CAUSA-STORAGE-RELEASE§identities-O8 | \| engine assembly: SHA-256 of `IndyInitRootCauseInvestigator.Engine.dll`, sterile build \| `b3904277e72390667a35ccaeb7986f5d6c4848ffd5b8c28fc13f9673b5e13a44` \| |
| CAUSA-STORAGE-RELEASE§identities-O9 | \| .NET SDK \| `10.0.402` (`rollForward: disable`) \| |
