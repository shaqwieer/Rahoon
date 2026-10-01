# Rahoon — prompt to prepare the detailed repository roadmap

Act as the lead product engineer, solution architect and UX specialist for the EXISTING Rahoon repository. The owner has completed Phase 0 and Phase 1 and now wants detailed subsequent phases that can be implemented in separate new sessions, sequentially. Your task in THIS session is to inspect the actual project and create the executable roadmap documents and session handoffs. Do not implement the subsequent features in this planning session.

## Current business and decisions

Rahoon operates in Saudi Arabia. It connects owners exiting developer contracts or selling properties connected to bank/finance-provider commitments with suitable buyers. Staff review evidence and numbers, prepare and publish opportunities, match buyers, and coordinate approval and completion. The old mortgage-help/judicial-agent/debt-settlement model was removed; keep its modules, roles, schemas and flags removed.

The Phase 1 baseline is seller/buyer intake, conditional property/developer/bank fields, drafts/account linking, private documents, staff review/resubmission, owner confirmation, opportunity publication, basic search/maps/calculators and expressions of interest. Verify the relevant baseline in code and tests; do not repeat it just because the new session lacks chat history.

Reuse the existing organization directory and admin page; official-source coverage may be partial and must be stated accurately. Preserve database-backed file bytes behind a future storage abstraction. A new object-storage migration and malware-scanner integration are out of scope. Retain the existing stack and any actual company/tenant boundaries. Use Arabic-first RTL, Saudi locations, SAR and Riyadh display time.

Internal review, owner acceptance, developer/bank approval, buyer financing approval and official completion are separate. Do not copy the seller's bank installment as the buyer's financing or use historic bank payments as seller equity. Unknown monetary values remain unknown. Commission rate, base, payer, tax treatment and timing come from an approved versioned policy, not an invented or copied percentage. Do not add lending, debt collection, a wallet or escrow.

## Roadmap numbering and order

Keep the existing meaning of Phase 2. Split the old broad Phase 3 into two manageable sessions. Add team/access administration as Phase 1.5, before transactions, then finish operating administration and readiness.

| Order | Phase | Main outcome |
|---|---|---|
| 1 | 1.5 | Rahoon administration foundation, staff membership, custom roles, permission catalog, effective access and resource scopes |
| 2 | 2 | Advanced search and map discovery, explained matching, comparison, saved searches/alerts and trustworthy calculators |
| 3 | 3A | Versioned offers, negotiation, staff presentation, actual owner acceptance and exclusive provisional reservations |
| 4 | 3B | Path-specific completion cases, external approvals, financial/payment evidence, transfer confirmation and participant file |
| 5 | 4 | Operating dashboards, assignments/reminders, versioned settings, directory administration/import, support and reports |
| 6 | 5 | Authorization/workflow/concurrency hardening, full journeys, real worker/configuration checks and launch-readiness evidence |

Phases 1.5 and 4 are complementary: the first establishes team/security capability, the second completes business administration and reporting. There is one administration application, not two duplicate consoles. Phases 3A and 3B together cover the old Phase 3.

## Inspect before documenting

Read repository instructions, architecture, actual entities/endpoints/screens/jobs, migrations, current permissions and existing tests. Record current branch/commit, relevant features as done/partial/missing, and known operating dependencies. Do not infer completion from an earlier assistant report alone, and do not turn planning into a full rewrite.

Map each proposed feature onto existing models and code entry points. Reuse rather than duplicate users, requests, organizations, documents, opportunities, audit and notifications. If the phase pack attached to this prompt is available, use its detailed semantics as the product brief, then adapt file names and implementation plans to the real repository. If only this prompt is attached, fully expand the phase scopes below yourself; do not require the user to paste earlier conversation history.

## Detail each phase

Every phase document must be independently usable by a new session and contain:

1. The stable business context and latest decisions above, phase ID, objective, prerequisites and explicit scope exclusions.
2. Current implementation evidence and the missing behavior, with real code references. Separate verified facts from assumptions.
3. End-user journeys and required screens/actions/states, including mobile RTL, empty/loading/error/conflict behavior.
4. Existing/new entities, relationships, immutable snapshots, database constraints, migrations and seed/configuration changes.
5. Endpoints/commands/queries, permission and resource-scope checks, transaction boundaries, concurrency and retry behavior.
6. A state-transition table where relevant: actor, permission, source/target state, prerequisites, side effects, failure/retry outcome.
7. Worker/events/notifications and actual channel/configuration prerequisites, with deduplication and failure handling.
8. Financial definitions and accepted policy sources where relevant; never invent a legal/financial default just to finish the plan.
9. Ordered implementation tasks within the phase, meaningful acceptance cases and appropriate verification commands discovered from the project.
10. Decisions needing the owner only if they are material and cannot be inferred; all independent work and routine decisions remain actionable.
11. A mandatory completion handoff and exact starting instruction for the next phase; stop after the named phase.

### Mandatory details for Phase 1.5

Separate staff membership from public customer profiles. Support identity-bound invitations/addition, role assignment, scope, suspension and immediate revocation of stale session authority. Default responsibilities: platform owner, operations manager, case manager, document reviewer, publisher, finance officer, support agent and read-only auditor; map them to the existing catalog.

Specify custom role editor, effective permissions, per-action grants and assigned/all-resource scopes. Scope on one permission cannot widen another permission. Protect the last active owner under concurrency. Prevent self-escalation and unauthorized grants. Apply checks to detail/mutation/download/export/count endpoints. Include controlled bootstrap, audit, staff reassignment and negative tests with non-owner actors.

### Mandatory details for Phase 2

Specify shareable URL search state, server filtering/pagination, coherent list/map results, public-coordinate privacy, multiple property types and path-specific budget/term filters. Distinguish cash now, total commitment and original contract price. Matching must explain real reasons and unknown/estimated components. Include extra annual/balloon payments rather than relying only on a monthly average.

Detail comparison, favorites, saved searches and consented deduplicated alerts. Use real configured workers; no fake delivery. Extend existing seller/buyer/opportunity calculators consistently. Test unknown values, developer arrears double-counting and buyer bank-financing separation. Do not implement offers yet.

### Mandatory details for Phase 3A

Separate interest, offer and reservation. Specify immutable offer revisions, reviewed counterproposals, quote/policy snapshots, owner decision, stale-data conflicts and staff visibility. Staff approval is presentation, not owner acceptance. Enforce one active reservation with database-backed concurrency and retry-safe commands. Check expiry server-side even if a worker is late, and define cancellation/reopening without silently reviving withdrawn opportunities.

### Mandatory details for Phase 3B

Specify atomic reservation conversion and one active closing case; developer/bank/mixed task templates; separate external approval records, expiry and conditions; reviewed official financial evidence and material amendments. Record external payment evidence and fee-policy snapshots without moving funds. Separate reported/verified payments, transfer confirmation and any later physical delivery. Complete only with required evidence and gates. Define cancellation, privileged corrections and correctly scoped final files.

### Mandatory details for Phase 4

Extend the existing console with scoped real dashboards/queues, team workload/assignment, due reminders, approved versioned policies, notification configuration/templates, directory provenance and staged duplicate-safe import, support threads/internal notes, searchable audit and defined reports/exports. Preserve manually reviewed directory fields and historical references. Distinguish expected commission from verified receipts. Exports enforce scope and safe spreadsheet values.

### Mandatory details for Phase 5

Specify full journeys using separate actors, access/resource/download/caching checks, concurrency and retry verification, financial regressions, worker startup/failure handling, measured performance and safe recovery checks on a disposable environment. Deliver actual readiness and configuration evidence, not a blanket launch claim. Preserve the agreed storage direction and phase exclusions.

## Repository outputs

Create or update these documentation files:

- `docs/rahoon/roadmap/README.md`: ordered roadmap, prerequisites, current verified status and links.
- `docs/rahoon/roadmap/current-state.md`: relevant implementation evidence and actual gaps.
- `docs/rahoon/roadmap/decisions.md`: approved decisions, adopted technical choices and unresolved material choices.
- `docs/rahoon/roadmap/phase-1.5-admin-access.md`.
- `docs/rahoon/roadmap/phase-2-discovery-matching.md`.
- `docs/rahoon/roadmap/phase-3a-offers-reservations.md`.
- `docs/rahoon/roadmap/phase-3b-transfer-completion.md`.
- `docs/rahoon/roadmap/phase-4-operations-reports.md`.
- `docs/rahoon/roadmap/phase-5-launch-readiness.md`.
- `docs/rahoon/roadmap/session-prompts.md`: exact short starting instructions, one per new session.
- `docs/rahoon/roadmap/handoffs/README.md`: completion record template and how the next session reads it. Do not fabricate completed-phase handoffs.

Session starting instruction form: "Read the repository instructions, roadmap, decisions and prerequisite handoffs. Read `docs/rahoon/roadmap/<named-phase-file>`. Verify prerequisites from actual code, implement ONLY that phase, complete its acceptance checks, write its handoff and update roadmap status. Do not start the next phase."

The completion record includes implemented behavior, code entry points, migrations and actual application state, permission changes, actual tests, reproducible acceptance scenario, remaining limitations, commit and actual push/deployment state, and next-phase instruction. It carries no credentials or customer evidence.

Follow the existing authorized Git workflow, preserving other changes and actual branch names. Do not publish/deploy code or implement future phases merely to prepare this documentation. Do not label a phase completed until it was implemented and verified in its execution session.

## Completion of this planning session

Validate numbering/dependencies, detect overlapping responsibility and ensure each phase is self-contained. Provide links to the generated repository files, the ordered phase table, material missing decisions and the precise instruction to start Phase 1.5 in a new session. STOP after preparing and reviewing this roadmap. The next session will implement Phase 1.5.
