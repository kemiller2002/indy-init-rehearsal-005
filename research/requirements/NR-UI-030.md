---
id: NR-UI-030
status: accepted
version: 1.0.0
priority: P0
source: UI-030
---

# NR-UI-030

Derived from baseline requirement `UI-030` (Presentation semantics verification and adversarial tests). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Acceptance criterion |
|---|---|
| UI-030-O1 | Add deterministic fixture and deployed-browser assertions covering UI-022..UI-029: absence of technical metadata from header/footer/cards; truthful actual status versus expected status; technical validation never becoming an investigation-level "Verified" badge; mapped user-facing terminology; overview backed by the domain snapshot; causal graph invoking the real workflow layout engine; draft actions respecting engine authority; failure-state copy; and two-level navigation/accessibility. |
| UI-030-O2 | Include negative controls for fabricated conclusions, unknown-as-zero, technical-language leakage, and misleading saved/verified states. |
| UI-030-O3 | Record source requirement IDs, tests, exact deployed SHA, and independent review. |

| ID | Statement |
|---|---|
| UI-030-O4 | Do not mark unsupported features PASS. |
