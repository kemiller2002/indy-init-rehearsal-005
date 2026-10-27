---
id: NR-KICKOFF-EXECUTION-PACKET§preamble
status: accepted
version: 1.0.0
priority: unstated
source: KICKOFF-EXECUTION-PACKET§preamble
---

# NR-KICKOFF-EXECUTION-PACKET§preamble

Derived from baseline requirement `KICKOFF-EXECUTION-PACKET§preamble` (Kickoff Execution Packet). Each obligation is carried verbatim as the criterion that bears its id.

| ID | Acceptance criterion |
|---|---|
| KICKOFF-EXECUTION-PACKET§preamble-O4 | verify target implementation repository is empty/organizer-compliant; |
| KICKOFF-EXECUTION-PACKET§preamble-O11 | run Conditor verify/doctor; |
| KICKOFF-EXECUTION-PACKET§preamble-O37 | state-space/property tests. |
| KICKOFF-EXECUTION-PACKET§preamble-O44 | URL routes: every screen, investigation, evidence item, branch and version addressable by a canonical hash URL, `.echelon/routes.json`, and pure round-trip-tested route parsing and formatting (R9.6–R9.10, SCREEN-CONTRACTS URL contract, Praxis SAF-URL-1..10); |
| KICKOFF-EXECUTION-PACKET§preamble-O71 | relevant acceptance tests pass; |
| KICKOFF-EXECUTION-PACKET§preamble-O96 | Domain kernel and protected-transition test working. |
| KICKOFF-EXECUTION-PACKET§preamble-O117 | test/proof data being fabricated or hard-coded. |
| KICKOFF-EXECUTION-PACKET§preamble-O120 | `requirement -> work item -> implementation -> test -> runtime/verification evidence -> proof trace` |
| KICKOFF-EXECUTION-PACKET§preamble-O124 | run full build/tests; |

| ID | Statement |
|---|---|
| KICKOFF-EXECUTION-PACKET§preamble-O1 | This packet is intentionally concise. |
| KICKOFF-EXECUTION-PACKET§preamble-O2 | The machine-readable source is `kickoff/indy-init.kickoff.json`. |
| KICKOFF-EXECUTION-PACKET§preamble-O3 | Before application implementation: |
| KICKOFF-EXECUTION-PACKET§preamble-O5 | capture timestamp and repository HEAD/tree; |
| KICKOFF-EXECUTION-PACKET§preamble-O6 | capture workstation/runtime versions; |
| KICKOFF-EXECUTION-PACKET§preamble-O7 | pin the exact planning repository commit; |
| KICKOFF-EXECUTION-PACKET§preamble-O8 | run Conditor plan; |
| KICKOFF-EXECUTION-PACKET§preamble-O9 | inspect planned mutations; |
| KICKOFF-EXECUTION-PACKET§preamble-O10 | run Conditor init; |
| KICKOFF-EXECUTION-PACKET§preamble-O12 | record generated lock and component versions; |
| KICKOFF-EXECUTION-PACKET§preamble-O13 | create first post-bootstrap commit; |
| KICKOFF-EXECUTION-PACKET§preamble-O14 | create/start initial ROS work item; |
| KICKOFF-EXECUTION-PACKET§preamble-O15 | capture Praxis baseline. |
| KICKOFF-EXECUTION-PACKET§preamble-O16 | No application feature coding begins before this evidence exists. |
| KICKOFF-EXECUTION-PACKET§preamble-O17 | Agents start with: |
| KICKOFF-EXECUTION-PACKET§preamble-O18 | `kickoff/indy-init.kickoff.json` |
| KICKOFF-EXECUTION-PACKET§preamble-O19 | Then required-before-coding documents in the manifest. |
| KICKOFF-EXECUTION-PACKET§preamble-O20 | Agents must not infer architecture from README summaries when a normative contract exists. |
| KICKOFF-EXECUTION-PACKET§preamble-O21 | semantic kernel; |
| KICKOFF-EXECUTION-PACKET§preamble-O22 | policy/capabilities; |
| KICKOFF-EXECUTION-PACKET§preamble-O23 | semantic compiler structural checks; |
| KICKOFF-EXECUTION-PACKET§preamble-O24 | workspace; |
| KICKOFF-EXECUTION-PACKET§preamble-O25 | local durability; |
| KICKOFF-EXECUTION-PACKET§preamble-O26 | GitHub provider; |
| KICKOFF-EXECUTION-PACKET§preamble-O27 | AI proposals; |
| KICKOFF-EXECUTION-PACKET§preamble-O28 | complete investigation/correction/verification loop; |
| KICKOFF-EXECUTION-PACKET§preamble-O29 | time travel + semantic diff; |
| KICKOFF-EXECUTION-PACKET§preamble-O30 | factory/proof view; |
| KICKOFF-EXECUTION-PACKET§preamble-O31 | stretch features. |
| KICKOFF-EXECUTION-PACKET§preamble-O32 | Owns: |
| KICKOFF-EXECUTION-PACKET§preamble-O33 | entities/types; |
| KICKOFF-EXECUTION-PACKET§preamble-O34 | commands/events; |
| KICKOFF-EXECUTION-PACKET§preamble-O35 | decide/evolve; |
| KICKOFF-EXECUTION-PACKET§preamble-O36 | policies/capabilities; |
| KICKOFF-EXECUTION-PACKET§preamble-O38 | May not: |
| KICKOFF-EXECUTION-PACKET§preamble-O39 | introduce provider/UI/AI dependencies. |
| KICKOFF-EXECUTION-PACKET§preamble-O40 | Owns: |
| KICKOFF-EXECUTION-PACKET§preamble-O41 | Limen boundary; |
| KICKOFF-EXECUTION-PACKET§preamble-O42 | Forma composition; |
| KICKOFF-EXECUTION-PACKET§preamble-O43 | screen contracts; |
| KICKOFF-EXECUTION-PACKET§preamble-O45 | accessibility/responsive evidence. |
| KICKOFF-EXECUTION-PACKET§preamble-O46 | May not: |
| KICKOFF-EXECUTION-PACKET§preamble-O47 | implement independent transition legality. |
| KICKOFF-EXECUTION-PACKET§preamble-O48 | Owns: |
| KICKOFF-EXECUTION-PACKET§preamble-O49 | local durable store; |
| KICKOFF-EXECUTION-PACKET§preamble-O50 | GitHub provider; |
| KICKOFF-EXECUTION-PACKET§preamble-O51 | synchronization/conflict; |
| KICKOFF-EXECUTION-PACKET§preamble-O52 | canonical serialization. |
| KICKOFF-EXECUTION-PACKET§preamble-O53 | May not: |
| KICKOFF-EXECUTION-PACKET§preamble-O54 | introduce GitHub identity into domain types. |
| KICKOFF-EXECUTION-PACKET§preamble-O55 | Owns: |
| KICKOFF-EXECUTION-PACKET§preamble-O56 | context builder; |
| KICKOFF-EXECUTION-PACKET§preamble-O57 | proposal schemas; |
| KICKOFF-EXECUTION-PACKET§preamble-O58 | provider adapters; |
| KICKOFF-EXECUTION-PACKET§preamble-O59 | challenge/planner; |
| KICKOFF-EXECUTION-PACKET§preamble-O60 | tool gateway. |
| KICKOFF-EXECUTION-PACKET§preamble-O61 | May not: |
| KICKOFF-EXECUTION-PACKET§preamble-O62 | write authoritative state directly. |
| KICKOFF-EXECUTION-PACKET§preamble-O63 | Owns: |
| KICKOFF-EXECUTION-PACKET§preamble-O64 | semantic compiler; |
| KICKOFF-EXECUTION-PACKET§preamble-O65 | generated validation; |
| KICKOFF-EXECUTION-PACKET§preamble-O66 | Praxis/Aegis/ROS aggregation; |
| KICKOFF-EXECUTION-PACKET§preamble-O67 | proof bundle; |
| KICKOFF-EXECUTION-PACKET§preamble-O68 | demo health. |
| KICKOFF-EXECUTION-PACKET§preamble-O69 | A branch/work item is integration-ready only when: |
| KICKOFF-EXECUTION-PACKET§preamble-O70 | target requirements identified; |
| KICKOFF-EXECUTION-PACKET§preamble-O72 | architecture boundary checks pass; |
| KICKOFF-EXECUTION-PACKET§preamble-O73 | no unresolved Aegis invariant fault; |
| KICKOFF-EXECUTION-PACKET§preamble-O74 | Praxis thresholds not silently exceeded; |
| KICKOFF-EXECUTION-PACKET§preamble-O75 | Percepta contract evidence exists for UI work; |
| KICKOFF-EXECUTION-PACKET§preamble-O76 | ROS attribution exists; |
| KICKOFF-EXECUTION-PACKET§preamble-O77 | known deviation explicitly recorded. |
| KICKOFF-EXECUTION-PACKET§preamble-O78 | If behind, cut: |
| KICKOFF-EXECUTION-PACKET§preamble-O79 | visual polish beyond accessibility; |
| KICKOFF-EXECUTION-PACKET§preamble-O80 | research tournament; |
| KICKOFF-EXECUTION-PACKET§preamble-O81 | multi-agent roles beyond Generator/Skeptic/Evidence Auditor; |
| KICKOFF-EXECUTION-PACKET§preamble-O82 | sensitivity variants; |
| KICKOFF-EXECUTION-PACKET§preamble-O83 | cross-investigation intelligence; |
| KICKOFF-EXECUTION-PACKET§preamble-O84 | dynamic graph polish; |
| KICKOFF-EXECUTION-PACKET§preamble-O85 | bounded autonomy. |
| KICKOFF-EXECUTION-PACKET§preamble-O86 | Do not cut: |
| KICKOFF-EXECUTION-PACKET§preamble-O87 | typed semantic kernel; |
| KICKOFF-EXECUTION-PACKET§preamble-O88 | capability/blocker integrity; |
| KICKOFF-EXECUTION-PACKET§preamble-O89 | original-source preservation; |
| KICKOFF-EXECUTION-PACKET§preamble-O90 | local persistence truth; |
| KICKOFF-EXECUTION-PACKET§preamble-O91 | AI proposal authority boundary; |
| KICKOFF-EXECUTION-PACKET§preamble-O92 | causal gate; |
| KICKOFF-EXECUTION-PACKET§preamble-O93 | corrective verification; |
| KICKOFF-EXECUTION-PACKET§preamble-O94 | security essentials; |
| KICKOFF-EXECUTION-PACKET§preamble-O95 | factory proof. |
| KICKOFF-EXECUTION-PACKET§preamble-O97 | Manual local investigation UI works. |
| KICKOFF-EXECUTION-PACKET§preamble-O98 | Durable local + GitHub persistence path established or explicit fallback active. |
| KICKOFF-EXECUTION-PACKET§preamble-O99 | Structured AI proposal loop works. |
| KICKOFF-EXECUTION-PACKET§preamble-O100 | End-to-end cause -> correction -> verification -> resolution path works. |
| KICKOFF-EXECUTION-PACKET§preamble-O101 | Time travel/semantic diff works. |
| KICKOFF-EXECUTION-PACKET§preamble-O102 | One ambitious stretch capability complete. |
| KICKOFF-EXECUTION-PACKET§preamble-O103 | Factory/proof view usable. |
| KICKOFF-EXECUTION-PACKET§preamble-O104 | Security/resilience smoke pass complete. |
| KICKOFF-EXECUTION-PACKET§preamble-O105 | Feature freeze except critical fixes. |
| KICKOFF-EXECUTION-PACKET§preamble-O106 | Final proof bundle and deployment identity captured. |
| KICKOFF-EXECUTION-PACKET§preamble-O107 | Immediately stop feature work for: |
| KICKOFF-EXECUTION-PACKET§preamble-O108 | false synced/persisted state; |
| KICKOFF-EXECUTION-PACKET§preamble-O109 | AI bypass of commands/policy; |
| KICKOFF-EXECUTION-PACKET§preamble-O110 | UI-owned legality; |
| KICKOFF-EXECUTION-PACKET§preamble-O111 | dropped unknown extension/history; |
| KICKOFF-EXECUTION-PACKET§preamble-O112 | hidden material blocker; |
| KICKOFF-EXECUTION-PACKET§preamble-O113 | verification collapsed into implementation; |
| KICKOFF-EXECUTION-PACKET§preamble-O114 | provider-specific generic semantics; |
| KICKOFF-EXECUTION-PACKET§preamble-O115 | secret exposure; |
| KICKOFF-EXECUTION-PACKET§preamble-O116 | screen contract accessibility failure; |
| KICKOFF-EXECUTION-PACKET§preamble-O118 | A feature is not complete because it renders. |
| KICKOFF-EXECUTION-PACKET§preamble-O119 | It is complete when: |
| KICKOFF-EXECUTION-PACKET§preamble-O121 | exists, or an explicit deviation says why it does not. |
| KICKOFF-EXECUTION-PACKET§preamble-O122 | At final freeze: |
| KICKOFF-EXECUTION-PACKET§preamble-O123 | stop feature branches; |
| KICKOFF-EXECUTION-PACKET§preamble-O125 | run semantic compiler/contract validation; |
| KICKOFF-EXECUTION-PACKET§preamble-O126 | run demo health; |
| KICKOFF-EXECUTION-PACKET§preamble-O127 | run critical accessibility/security checks; |
| KICKOFF-EXECUTION-PACKET§preamble-O128 | generate exact-build proof bundle; |
| KICKOFF-EXECUTION-PACKET§preamble-O129 | record deployed commit; |
| KICKOFF-EXECUTION-PACKET§preamble-O130 | tag/freeze submission; |
| KICKOFF-EXECUTION-PACKET§preamble-O131 | preserve local fallback/spectator state. |
