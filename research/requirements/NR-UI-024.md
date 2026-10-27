---
id: NR-UI-024
status: accepted
version: 1.0.0
priority: P0
source: UI-024
---

# NR-UI-024

Derived from baseline requirement `UI-024` (User-oriented terminology and presentation-language contract). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Statement |
|---|---|
| UI-024-O1 | Define a centralized, testable presentation-language mapping from typed F# domain/projection/error states to concise user-facing labels, explanations, and actions. |
| UI-024-O2 | Do not let view templates or agents invent meanings, confidence, permitted transitions, or success claims. |
| UI-024-O3 | Default labels: "Investigations" instead of "Historical investigations"; "Opening investigation" instead of "Fetching and validating the investigation"; "Status" instead of "Expected lifecycle"; "Investigation history" instead of "Replayed state"; "Explore changes" instead of "Open a sandbox copy"; "Apply change to draft" instead of "Ask the engine" when that is the actual action; "Change history" instead of "Engine decisions"; and "Discard draft" instead of "Discard the sandbox". |
| UI-024-O4 | The wording MUST remain accurate to read-only, ephemeral, unsaved, and persisted states: never imply a draft was saved or a change was applied to the authoritative investigation when it was not. |
| UI-024-O5 | Technical codes and provider details belong in Administration, except where needed in an explicitly requested technical detail view. |
| UI-024-O6 | Preserve precise provenance and domain-specific terms where material. |
