---
id: NR-STATE-TRANSITION-TABLES§1-investigation-lifecycle
status: accepted
version: 1.0.0
priority: unstated
source: STATE-TRANSITION-TABLES§1-investigation-lifecycle
---

# NR-STATE-TRANSITION-TABLES§1-investigation-lifecycle

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
