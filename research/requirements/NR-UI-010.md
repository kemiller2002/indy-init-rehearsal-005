---
id: NR-UI-010
status: accepted
version: 1.0.0
priority: P0
source: UI-010
---

# NR-UI-010

Derived from baseline requirement `UI-010` (Empty, error and recovery states). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Statement |
|---|---|
| UI-010-O1 | Design specific recoverable states for no investigations, no evidence, unavailable projection, schema mismatch, invalid event history, offline network, expired authentication, failed sync, and conflict. |
| UI-010-O2 | Every error MUST describe what happened, what data was preserved, and a safe next step. |
| UI-010-O3 | Provide Retry only when it performs a real action. |
| UI-010-O4 | Invalid histories MUST fail closed rather than silently rendering partial authoritative state. |
| UI-010-O5 | Do not expose internal secrets. |
