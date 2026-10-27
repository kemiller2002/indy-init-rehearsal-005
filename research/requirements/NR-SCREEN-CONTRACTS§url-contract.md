---
id: NR-SCREEN-CONTRACTS§url-contract
status: accepted
version: 1.0.0
priority: unstated
source: SCREEN-CONTRACTS§url-contract
---

# NR-SCREEN-CONTRACTS§url-contract

Derived from baseline requirement `SCREEN-CONTRACTS§url-contract` (URL contract). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Statement |
|---|---|
| SCREEN-CONTRACTS§url-contract-O1 | Every screen and every addressable entity has a canonical URL (R9.6–R9.10, P34.15–P34.20; the portfolio standard is Praxis SAF-URL-1..10). |
| SCREEN-CONTRACTS§url-contract-O2 | Pasting a copied URL into another browser opens the same view, after GitHub sign-in when needed. |
| SCREEN-CONTRACTS§url-contract-O3 | The rules: |
| SCREEN-CONTRACTS§url-contract-O4 | **Hash routes.** The route follows `#/` in `index.html#/...`, with relative links and no `<base href>`, so a reloaded deep link never 404s on GitHub Pages (D-203, Limen DF-LIMEN-2026-0006). |
| SCREEN-CONTRACTS§url-contract-O5 | **IDs.** Every `{...Id}` is the entity's GUID (`FSHARP-TYPE-SHAPES.md`); `{branchId}` is the counterfactual BranchId and `{version}` is an aggregate version (`int`). |
| SCREEN-CONTRACTS§url-contract-O6 | No other identifier, and no evidence content, appears in a URL. |
| SCREEN-CONTRACTS§url-contract-O7 | **Repository.** Investigation routes carry the persistence repository as the `repo` query parameter (`owner/name`), so the link resolves in another browser (OQ-048). |
| SCREEN-CONTRACTS§url-contract-O8 | **Version.** `at` (`int`) on any investigation-scoped route opens that view as of an aggregate version, visibly read-only (S14). |
| SCREEN-CONTRACTS§url-contract-O9 | Without it the view shows the current version. |
| SCREEN-CONTRACTS§url-contract-O10 | **Canonical form.** The canonical URL holds declared parameters only, in the order listed, with defaults omitted, sets sorted and de-duplicated, and `%20` with upper-case hex. |
| SCREEN-CONTRACTS§url-contract-O11 | **History.** Moving between rows of the table, or to another entity, pushes history. |
| SCREEN-CONTRACTS§url-contract-O12 | Changing a query parameter of the current row replaces it. |
| SCREEN-CONTRACTS§url-contract-O13 | **Missing targets.** An unknown ID, an unreadable repository, or data not available offline shows not found, not permitted or unavailable offline with a way back, never another record (P34.19). |
| SCREEN-CONTRACTS§url-contract-O14 | A superseded entity shows as superseded. |
| SCREEN-CONTRACTS§url-contract-O15 | **Inventory.** `.echelon/routes.json` (`echelon.routes/v1`) lists exactly these routes and is regenerated from the route table (Limen `Inventory.render`). |
| SCREEN-CONTRACTS§url-contract-O16 | **Copy link.** Screens marked ✓ offer a Copy link action that copies the canonical URL. |
| SCREEN-CONTRACTS§url-contract-O17 | Every investigation-scoped pattern below is under `/investigations/{investigationId}`, written `…`. |
| SCREEN-CONTRACTS§url-contract-O18 | \| Screen \| Route pattern \| Query parameters (default) \| Copy link \| |
| SCREEN-CONTRACTS§url-contract-O19 | \| Home (investigation list) \| `/` \| `q` string; `status` set; `sort` enum `updated`/`title` (`updated`); `page` int (1) \| \| |
| SCREEN-CONTRACTS§url-contract-O20 | \| S01 Investigation Start \| `/investigations/new` \| `profile` string \| \| |
| SCREEN-CONTRACTS§url-contract-O21 | \| S02 Investigation Workspace \| `/investigations/{investigationId}` \| `repo`; `at`; `tab` enum (`overview`) \| ✓ \| |
| SCREEN-CONTRACTS§url-contract-O22 | \| S03 Hypothesis Battlefield \| `…/hypotheses` \| `repo`; `at`; `status` set; `sort` enum (`support`) \| ✓ \| |
| SCREEN-CONTRACTS§url-contract-O23 | \| S04 Hypothesis Detail \| `…/hypotheses/{hypothesisId}` \| `repo`; `at`; `tab` enum (`predictions`) \| ✓ \| |
| SCREEN-CONTRACTS§url-contract-O24 | \| S05 Evidence Capture \| `…/evidence/new` \| `repo`; `kind` enum \| \| |
| SCREEN-CONTRACTS§url-contract-O25 | \| S06 Evidence Detail \| `…/evidence/{evidenceId}` \| `repo`; `at` \| ✓ \| |
| SCREEN-CONTRACTS§url-contract-O26 | \| S07 Evidence Dependency \| `…/evidence/{evidenceId}/dependencies` \| `repo`; `at`; `depth` int (1) \| ✓ \| |
| SCREEN-CONTRACTS§url-contract-O27 | \| S08 Unknown / Assumption / Contradiction \| `…/unknowns/{unknownId}`, `…/assumptions/{assumptionId}`, `…/contradictions/{contradictionId}` \| `repo`; `at` \| ✓ \| |
| SCREEN-CONTRACTS§url-contract-O28 | \| S09 Action / Experiment Designer \| `…/actions/new`, `…/actions/{actionId}` \| `repo`; `at` \| ✓ \| |
| SCREEN-CONTRACTS§url-contract-O29 | \| S10 Finding / Result Capture \| `…/actions/{actionId}/findings/new`, `…/findings/{findingId}` \| `repo`; `at` \| ✓ \| |
| SCREEN-CONTRACTS§url-contract-O30 | \| S11 Cause Review \| `…/cause-review` \| `repo`; `at` \| ✓ \| |
| SCREEN-CONTRACTS§url-contract-O31 | \| S12 Corrective Action \| `…/corrective-actions/{correctiveActionId}` \| `repo`; `at` \| ✓ \| |
| SCREEN-CONTRACTS§url-contract-O32 | \| S13 Verification \| `…/verifications/{verificationId}` \| `repo`; `at` \| ✓ \| |
| SCREEN-CONTRACTS§url-contract-O33 | \| S14 History / Time Travel \| `…/history` \| `repo`; `at` \| ✓ \| |
| SCREEN-CONTRACTS§url-contract-O34 | \| S15 Semantic Diff \| `…/diff` \| `repo`; `from` int; `to` int \| ✓ \| |
| SCREEN-CONTRACTS§url-contract-O35 | \| S16 Counterfactual Sandbox \| `…/branches/{branchId}` \| `repo`; `tab` enum (`changes`) \| ✓ \| |
| SCREEN-CONTRACTS§url-contract-O36 | \| S17 Multi-Agent Panel \| `…/agents` \| `repo` \| \| |
| SCREEN-CONTRACTS§url-contract-O37 | \| S18 Research Tournament \| `…/tournament` \| `repo` \| ✓ \| |
| SCREEN-CONTRACTS§url-contract-O38 | \| S19 Replay / Spectator \| `…/replay` \| `repo`; `at` \| ✓ \| |
| SCREEN-CONTRACTS§url-contract-O39 | \| S20 Factory View \| `/factory` \| `tab` enum (`work`) \| ✓ \| |
| SCREEN-CONTRACTS§url-contract-O40 | \| S21 Requirements Proof \| `/factory/requirements/{requirementId}` \| \| ✓ \| |
| SCREEN-CONTRACTS§url-contract-O41 | \| S22 Report Preview \| `…/report` \| `repo`; `at` \| ✓ \| |
| SCREEN-CONTRACTS§url-contract-O42 | \| S23 Persistence / Conflict Recovery \| `…/sync` \| `repo` \| \| |
| SCREEN-CONTRACTS§url-contract-O43 | \| S24 Settings / Semantic Manifest \| `/settings` \| `tab` enum (`general`) \| \| |
| SCREEN-CONTRACTS§url-contract-O44 | \| S25 Demo Health \| `/demo-health` \| \| \| |
| SCREEN-CONTRACTS§url-contract-O45 | \| Not found \| `/{*rest}` \| \| \| |
| SCREEN-CONTRACTS§url-contract-O46 | Sign-in is not a route a user shares: it keeps the requested route as its return target and resumes it (R9.7). |
