---
id: NR-UI-006
status: accepted
version: 1.0.0
priority: P0
source: UI-006
---

# NR-UI-006

Derived from baseline requirement `UI-006` (Save, sync and conflict status). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Statement |
|---|---|
| UI-006-O1 | Expose distinct states: clean, unsaved, saving locally, saved locally, queued offline, syncing remotely, remotely synced, conflict, failed. |
| UI-006-O2 | Only claim remote synchronization after confirmed provider acknowledgement. |
| UI-006-O3 | Show last successful sync time when known. |
| UI-006-O4 | On expected-version conflict, preserve local edits and provide a recoverable compare/retry flow; do not overwrite remote history. |
| UI-006-O5 | Frozen fixtures MUST remain read-only. |
| UI-006-O6 | Never expose tokens. |
