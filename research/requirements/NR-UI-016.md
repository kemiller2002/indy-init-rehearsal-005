---
id: NR-UI-016
status: accepted
version: 1.0.0
priority: P0
source: UI-016
---

# NR-UI-016

Derived from baseline requirement `UI-016` (Causa semantic color system). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Acceptance criterion |
|---|---|
| UI-016-O15 | Acceptance MUST include actual screenshots from the deployed exact SHA at desktop, iPad portrait/landscape and mobile sizes, plus automated token presence and contrast checks, keyboard focus checks, reduced-motion checks and independent visual review. |
| UI-016-O16 | Verify that navy, teal, amber, coral and ivory appear in appropriate populated states; absence of data must not force a misleading status color. |
| UI-016-O17 | A grayscale-only interface is a failed UI-016 acceptance even if functional tests pass. |

| ID | Statement |
|---|---|
| UI-016-O1 | The interface MUST have a deliberate, visibly colored Causa identity, not an effectively monochrome black/gray/white presentation. |
| UI-016-O2 | Use these initial canonical palette values: |
| UI-016-O3 | \| Deep navy \| Deep navy \| `#14283F` \| Primary navigation, masthead, structural emphasis \| |
| UI-016-O4 | \| Teal \| Teal \| `#087F8C` \| Primary investigation actions, active selection and positive workflow emphasis \| |
| UI-016-O5 | \| Amber \| Amber \| `#E9A23B` \| Unresolved unknowns, pending review and attention \| |
| UI-016-O6 | \| Coral \| Coral \| `#D66B58` \| Contradictions, material conflicts and critical investigation alerts \| |
| UI-016-O7 | \| Soft ivory \| Soft ivory \| `#F5F3ED` \| Main workspace surface and light backgrounds \| |
| UI-016-O8 | All colors MUST be mapped to named, semantic Forma theme tokens, not scattered hardcoded CSS declarations. |
| UI-016-O9 | Use Forma components for surfaces, controls, states and focus treatment. |
| UI-016-O10 | Define derived foreground, hover, disabled, border and dark-mode tokens only after measuring contrast; the five hex values are starting identity colors, not a guarantee of accessible foreground/background combinations. |
| UI-016-O11 | Ensure WCAG AA contrast for normal text (4.5:1), large text (3:1), and relevant non-text indicators (3:1). |
| UI-016-O12 | Do not convey lifecycle, confidence, danger, verification or sync status through color alone: pair color with text and/or icons, and distinguish causal semantics from decorative accents. |
| UI-016-O13 | Use the same token semantics across dashboard, investigation views, timeline, causal graph, evidence inspector, report and startup shell. |
| UI-016-O14 | Print/Folio output MUST remain legible in grayscale. |
