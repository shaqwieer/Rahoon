# Rahoon — Phase 5: Workflow hardening, operational recovery and launch readiness

**Implement this phase only.** Prerequisites: All preceding phases complete or explicitly documented with material gaps; active policy and channel configuration known.

## Context carried into this new session

You are the product engineer and UX specialist working in the existing Rahoon repository. Rahoon operates in Saudi Arabia. It helps a property owner exit a developer contract or sell a property connected to a bank/finance provider commitment, and helps a buyer find a suitable opportunity. Rahoon staff review evidence and numbers, prepare and publish opportunities, match buyers, and coordinate completion with the relevant parties.

The owner reports Phase 0 and Phase 1 completed. Verify the relevant implementation in the repository; do not redo those phases. Their baseline is seller and buyer intake, conditional developer/bank/property-type fields, saved drafts, account linking, private documents, staff review and requests for more information, owner confirmation, publication, basic listings/search/maps/calculators, and expressions of interest. Record any material gap that affects this phase. Fix a narrow dependency when necessary; do not silently restart the entire project.

The old mortgage-help, judicial-agent, debt-settlement and related business modules were subsequently removed. Keep them removed; do not restore their schemas, roles, flags, routes or seed data. Existing developer/bank/finance-provider directory and administration must be reused rather than duplicated. Source coverage can be partial: do not present imported entries as the complete official directory without evidence.

The latest agreed file-storage direction is database-backed storage behind an abstraction, allowing a future move to object storage. Preserve it. Apply authentication, authorization and the upload validation already appropriate to the product. A new malware-scanning integration and an object-storage migration are outside these phases.

Arabic-first, proper RTL, SAR, Saudi locations and Asia/Riyadh display time remain the defaults. The same account may be both seller and buyer. A public account is not a staff account simply because its profile claims a role. Use the existing stack and architecture; no framework rewrite, new lending product, wallet, escrow, automatic collection, or multi-tenant SaaS redesign.

Internal approval, owner acceptance, buyer financing approval, developer/bank approval and official completion are distinct. A bank-financed property's old installment is not automatically available to its buyer. Historic bank payments do not determine seller proceeds. Financial values may be verified, declared, estimated or unknown; unknown is never zero. Keep commission rate, basis, payer, tax treatment and timing in the approved policy, not a copied or invented percentage.

## Session execution contract

1. Read applicable repository instructions, architecture, migrations and any `docs/rahoon/roadmap/README.md`, decisions and completed-phase handoffs. Inspect the actual relevant code before editing. Repository implementation and explicit owner decisions take precedence over an old plan.
2. Implement ONLY the named phase in this document. Reuse working behavior. Do not add later-phase functionality early or mark a later phase complete through placeholders.
3. Maintain a coherent implementation from database and server rules through UI, permissions, jobs and tests. Buttons, notifications, timers, maps and reports must describe actual behavior, not a mock. Validate trusted calculations and transitions on the server.
4. Treat existing schemas and permission names as authoritative. The model names and dotted permission codes below illustrate semantics; map them onto the existing architecture and document the mapping. Do not create duplicate request, listing, document or identity models.
5. Keep public property media separate from private evidence. Return purpose-specific DTOs. UI hiding alone does not authorize an endpoint, export or download. Preserve any existing company/tenant boundary; do not introduce cross-boundary reads.
6. Use transactions, database constraints, version checks and retry-safe commands where the phase requires them. Do not replace business invariants with a front-end check or an in-memory lock.
7. Make normal reversible implementation decisions and continue. If a material business decision is missing, finish independent work, identify the precise unresolved choice, and do not manufacture a financial policy or external approval. Record adopted defaults and their rationale.
8. Follow the owner's existing Git and deployment workflow and repository rules. Respect uncommitted work. Do not force-push, invent a `main` branch where the repository uses `master`, expose credentials, or claim a push/deployment you did not perform. This phase does not require opening a PR or rewriting history.
9. Run checks appropriate to the change, including meaningful authorization/workflow/financial tests. Use the project's existing test tools. Check important new screens in a real browser at desktop and phone widths when possible. Report unavailable checks honestly rather than marking them passed.

## Mandatory handoff at the end of this session

Create or update the phase handoff under `docs/rahoon/roadmap/handoffs/`. It must contain: phase ID and status (`Completed`, `Partial`, or `Blocked`), verified prerequisites, final implemented behavior, important code entry points, migrations and their actual application state, permissions, adopted product decisions, tests with actual results, how to reproduce the acceptance scenario, remaining limitations, commit reference if available, actual push/deployment state, and the next phase with its starting instruction. Never put credentials or customer documents in the handoff.

Update roadmap status only from evidence. A partial feature, disabled required worker, or untested material invariant must remain visible in the status. Finish the named phase, summarize what changed and how it was verified, and STOP. Do not start the next phase in the same session.

## Outcome and scope

Verify and fix the implemented Rahoon product as a complete operating system. This is targeted hardening of existing behavior, not a new business model, new payment product or an excuse to rewrite the architecture. Distinguish a working preview from a site whose configured operational dependencies are ready for real customers.

## Workflow and transaction integrity

Review actual state transitions and enforce them in commands, including review resubmission, publication revisions, offer acceptance, reservation expiry/conversion, external approvals, money evidence, completion and cancellation. Eliminate generic status setters that bypass prerequisites.

Exercise concurrent owner acceptance, reservation expiry versus closing conversion, duplicate offer/payment submissions and role changes during active sessions. Confirm the database invariants, version conflicts and retry-safe event processing hold beyond a single browser process.

Ensure notifications occur after successful commits through the real delivery mechanism. Jobs for saved searches, reminders and expiry use bounded batches, retry/backoff and idempotency. A delayed or disabled worker must not undermine server-side exclusivity or create false delivery status. Make worker failure observable to permitted operators without exposing secrets.

## Access, files and caching

Recheck the role/action/resource-scope matrix for every sensitive read, mutation, count, map payload, export and document download. Test public users, assigned employees, revoked sessions, read-only auditors and owners of unrelated resources.

Preserve database-backed private file storage and its migration abstraction. Verify size/type handling and authorization on the existing upload/download paths, thumbnail/public-media separation, scoped completion bundles and deletion/deactivation behavior. Do not add malware-scanning integration or migrate to object storage in this phase.

Inspect response caching and public search projections so unpublished/withdrawn records, precise private coordinates and user-specific capacity inputs cannot leak between sessions. Review routine logs and audit payloads for accidental secrets or full private document contents.

## Financial regression checks

Test developer versus bank paths, mixed obligations, arrears included versus additional, different payer allocations, annual extra installments, unknown values, expired bank figures, accepted policy revisions and material amendments.

Known developer example remains: 300,000 credited paid, 700,000 remaining including 20,000 arrears, 10,000 seller discount and 15,000 buyer fees now gives 290,000 seller amount, 325,000 cash now, 680,000 future commitment and 1,005,000 total. Known bank example remains: sale 1,100,000 less verified payoff 800,000 less seller costs 10,000 gives 290,000 seller net, without implying any buyer installment or cash amount.

Unknown values cannot become zero, negative net cannot be silently clamped, and a market comparison cannot use cash-now alone as the full purchase price. Reconcile reports with the same authoritative case/payment records used by the product.

## End-to-end and UX verification

Run complete controlled scenarios with separate genuine actors:

1. Seller sends a developer request, staff request more information, owner confirms the prepared listing, publisher publishes, buyer finds/compares it, submits an offer, reviewer presents it, owner accepts, case manager obtains recorded approvals, finance officer verifies necessary evidence, and the authorized closer confirms the relevant transfer event.
2. A bank-connected request takes its distinct review/financial path; seller's old installment is never represented as buyer financing. Missing or expired payoff/approval evidence blocks the required final step.
3. A provisional reservation expires or is canceled while another buyer is pending, and the opportunity reopens only under the defined confirmation/publication conditions.
4. A staff role is revoked during the workflow and private downloads and mutations fail on the next request; another permitted manager can take over assigned work.

Use isolated test fixtures and appropriate environments; do not send test notifications to real customer recipients or fabricate official documents. Check Arabic RTL, desktop/phone layouts, keyboard use, error/retry states, drafts and numeric/date formatting in a real browser when available. State precisely what could not be tested.

## Performance and operational recovery

Measure search/map queries, dashboards, large exports and file retrieval using controlled realistic data. Optimize demonstrated bottlenecks and justified indexes. Record the measured environment and result; do not invent load-test capacity or a Lighthouse score.

Review migration execution and job startup for safe retry and bounded work. Verify backup/restore instructions for the active database and file bytes. Exercise restore/migration recovery on a disposable authorized environment, not by deleting or restoring the live database. Provide a short runbook for job failures, document delivery, stuck requests/reservations and supported rollback of the current release.

List actual required configuration: initial owner, active roles/policies, directory source coverage, map provider, notification channels/workers and relevant external-processing procedures. Existing integration status must be verified from configuration and tests, not inferred from a visible button.

## Acceptance and delivery

- Run the repository's appropriate build, type, unit/integration and selected end-to-end checks; fix phase-introduced failures and document unrelated existing failures with evidence.
- Deliver a readiness matrix: implemented, verified, configured, unavailable/blocked. A feature requiring a real worker or external configuration is not ready simply because its UI renders.
- Deliver the operations runbook, focused permission/workflow evidence, tested financial examples, measured performance notes and recovery result or precise limitation.
- Do not declare legal compliance, external bank approval or production deployment merely from automated tests. Report the actual product/configuration evidence and material remaining decisions.
- If deployment is part of the owner's current authorized workflow, follow it and verify the result; otherwise deliver the prepared, reviewable release and its actual local/remote state.

Completion of this phase ends the roadmap. Do not automatically add more features, begin a redesign or move to an unrelated integration.

## Phase-specific handoff and next session

Write `docs/rahoon/roadmap/handoffs/phase-5-handoff.md` and update this phase's actual status in the roadmap. The tailored implementation specification belongs at `docs/rahoon/roadmap/phase-5-launch-readiness.md`. Next: **No automatic next phase; deliver evidence-based readiness and outstanding decisions.**. Do not execute it in this session.
