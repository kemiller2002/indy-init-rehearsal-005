---
id: NR-PF-015
status: accepted
version: 1.0.0
priority: P0
source: PF-015
---

# NR-PF-015

Derived from baseline requirement `PF-015` (Credentials and security). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Acceptance criterion |
|---|---|
| PF-015-O1 | Public read-only catalog tests should not require a PAT when anonymous access is sufficient. |
| PF-015-O2 | Authenticated integration tests use an approved secret such as `CAUSA_GITHUB_TOKEN` when explicitly required, with least-privilege read-only permissions. |
| PF-015-O3 | Never embed or print credentials, put them in URLs, or include them in screenshots, traces, logs, or artifacts. |

| ID | Statement |
|---|---|
| PF-015-O4 | A missing secret yields BLOCKED, not PASS. |
