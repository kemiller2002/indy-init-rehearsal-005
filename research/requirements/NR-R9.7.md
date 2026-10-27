---
id: NR-R9.7
status: accepted
version: 1.0.0
priority: unstated
source: R9.7
---

# NR-R9.7

Derived from baseline requirement `R9.7` (Deep links survive reload, sign-in and offline). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Statement |
|---|---|
| R9.7-O1 | Routes are hash-based (`index.html#/...`), with relative links and no `<base href>`. |
| R9.7-O2 | A reloaded or pasted deep link therefore never 404s on GitHub Pages (D-203; Limen DF-LIMEN-2026-0006). |
| R9.7-O3 | When GitHub sign-in is needed, the application keeps the target, returns to it afterwards, and never returns outside the application. |
| R9.7-O4 | A deep link to state that is already held locally opens offline, and its persistence state is shown truthfully. |
