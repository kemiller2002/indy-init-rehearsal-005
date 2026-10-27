---
id: NR-WASM-LOAD-005
status: accepted
version: 1.0.0
priority: unstated
source: WASM-LOAD-005
---

# NR-WASM-LOAD-005

Derived from baseline requirement `WASM-LOAD-005` (Failure and recovery). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Statement |
|---|---|
| WASM-LOAD-005-O1 | On timeout or initialization failure, show a human-readable error and a functional Retry action that reattempts initialization or safely reloads. |
| WASM-LOAD-005-O2 | Never leave the spinner indefinitely. |
| WASM-LOAD-005-O3 | Avoid exposing sensitive implementation details in user-visible errors. |
