---
id: NR-WASM-LOAD-001
status: accepted
version: 1.0.0
priority: unstated
source: WASM-LOAD-001
---

# NR-WASM-LOAD-001

Derived from baseline requirement `WASM-LOAD-001` (Initial loading screen). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Statement |
|---|---|
| WASM-LOAD-001-O1 | The application MUST render an accessible, branded loading screen immediately, before the F#/Limen WebAssembly runtime is initialized and interactive. |
| WASM-LOAD-001-O2 | The screen MUST be implemented in the initial HTML/CSS shell so that it does not depend on successful WebAssembly startup. |
| WASM-LOAD-001-O3 | Do not display a blank page while the runtime loads. |
