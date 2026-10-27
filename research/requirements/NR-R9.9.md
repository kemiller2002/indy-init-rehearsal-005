---
id: NR-R9.9
status: accepted
version: 1.0.0
priority: unstated
source: R9.9
---

# NR-R9.9

Derived from baseline requirement `R9.9` (No secrets or evidence content in URLs). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Statement |
|---|---|
| R9.9-O1 | URLs never contain a GitHub token, an OAuth code, an AI provider key, evidence text or attachment content, or personal data typed about anyone. |
| R9.9-O2 | Entities are addressed by their GUID identifiers. |
| R9.9-O3 | Limen's reserved parameter names (`token`, `code`, `key`, `session` and the rest) are refused. |
