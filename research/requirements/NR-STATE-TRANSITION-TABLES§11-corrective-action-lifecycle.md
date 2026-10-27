---
id: NR-STATE-TRANSITION-TABLES§11-corrective-action-lifecycle
status: accepted
version: 1.0.0
priority: unstated
source: STATE-TRANSITION-TABLES§11-corrective-action-lifecycle
---

# NR-STATE-TRANSITION-TABLES§11-corrective-action-lifecycle

Derived from baseline requirement `STATE-TRANSITION-TABLES§11-corrective-action-lifecycle` (11. Corrective action lifecycle). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Statement |
|---|---|
| STATE-TRANSITION-TABLES§11-corrective-action-lifecycle-O1 | States: |
| STATE-TRANSITION-TABLES§11-corrective-action-lifecycle-O2 | Proposed |
| STATE-TRANSITION-TABLES§11-corrective-action-lifecycle-O3 | Approved |
| STATE-TRANSITION-TABLES§11-corrective-action-lifecycle-O4 | InProgress |
| STATE-TRANSITION-TABLES§11-corrective-action-lifecycle-O5 | Implemented |
| STATE-TRANSITION-TABLES§11-corrective-action-lifecycle-O6 | AwaitingVerification |
| STATE-TRANSITION-TABLES§11-corrective-action-lifecycle-O7 | VerifiedEffective |
| STATE-TRANSITION-TABLES§11-corrective-action-lifecycle-O8 | VerificationFailed |
| STATE-TRANSITION-TABLES§11-corrective-action-lifecycle-O9 | ReworkRequired |
| STATE-TRANSITION-TABLES§11-corrective-action-lifecycle-O10 | Cancelled |
| STATE-TRANSITION-TABLES§11-corrective-action-lifecycle-O11 | \| From \| Command \| To \| |
| STATE-TRANSITION-TABLES§11-corrective-action-lifecycle-O12 | \| none \| RecordCorrectiveAction \| Proposed \| |
| STATE-TRANSITION-TABLES§11-corrective-action-lifecycle-O13 | \| Proposed \| ApproveCorrectiveAction \| Approved \| |
| STATE-TRANSITION-TABLES§11-corrective-action-lifecycle-O14 | \| Proposed/Approved \| CancelCorrectiveAction \| Cancelled \| |
| STATE-TRANSITION-TABLES§11-corrective-action-lifecycle-O15 | \| Approved \| StartCorrectiveAction \| InProgress \| |
| STATE-TRANSITION-TABLES§11-corrective-action-lifecycle-O16 | \| InProgress \| MarkCorrectiveActionImplemented \| Implemented \| |
| STATE-TRANSITION-TABLES§11-corrective-action-lifecycle-O17 | \| Implemented \| StartCorrectiveActionVerification \| AwaitingVerification \| |
| STATE-TRANSITION-TABLES§11-corrective-action-lifecycle-O18 | \| AwaitingVerification \| MarkCorrectiveActionVerified \| VerifiedEffective \| |
| STATE-TRANSITION-TABLES§11-corrective-action-lifecycle-O19 | \| AwaitingVerification \| MarkCorrectiveActionVerificationFailed \| VerificationFailed \| |
| STATE-TRANSITION-TABLES§11-corrective-action-lifecycle-O20 | \| VerificationFailed \| RequireCorrectiveActionRework \| ReworkRequired \| |
| STATE-TRANSITION-TABLES§11-corrective-action-lifecycle-O21 | \| ReworkRequired \| StartCorrectiveAction \| InProgress \| |
