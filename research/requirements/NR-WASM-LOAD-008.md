---
id: NR-WASM-LOAD-008
status: accepted
version: 1.0.0
priority: unstated
source: WASM-LOAD-008
---

# NR-WASM-LOAD-008

Derived from baseline requirement `WASM-LOAD-008` (Acceptance). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Acceptance criterion |
|---|---|
| WASM-LOAD-008-O1 | Verify via automated browser checks: shell appears before delayed WebAssembly initialization; correct ready handoff; no false progress; simulated download/init failure shows recovery; retry works; reduced-motion, keyboard, accessibility and responsive checks pass. |
| WASM-LOAD-008-O2 | Include a deployment-level check of the actual published build. |
| WASM-LOAD-008-O3 | Preserve the exact verified SHA and acceptance-gate requirements in DEFECT-CLASS-CLOSURE-AND-RELEASE-GATES.md. |
