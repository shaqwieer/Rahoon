# Rahoon — Phase 4: Rahoon operating console, settings, organization directory and reports

**Implement this phase only.** Prerequisites: Phases 1.5, 2, 3A and 3B for access, discovery and actual transaction records.

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

Make Rahoon manageable day to day: who is handling each customer, what is waiting and why, which external approvals are overdue, what completed, what money/commission has been recorded, and which configuration governs new cases. Extend the Phase 1.5 console and permissions; do not build an unrelated second admin app.

## Dashboard and operating queues

Show real, scoped counts and trends for new seller/buyer requests, incomplete files, staff review, publication-ready opportunities, published/paused/reserved opportunities, offers, closing cases, external approval waits, overdue tasks, completed/canceled transactions and recorded commission.

Define each metric and its date basis. Creation date, acceptance date and completion date are different. Use Riyadh calendar boundaries while storing/comparing appropriate timestamps consistently. Data scopes apply to counters and exports as well as detail lists.

Provide actionable queues, filters and search by request/opportunity/case reference, assignee, stage, city, organization, elapsed time and due date. Include unassigned work, my tasks, team workload and explicit reassignment. Bulk actions still validate each record and return per-item outcomes; they cannot bypass review or permission gates.

Create follow-up reminders and supervisor escalation using configurable working rules and the existing real worker. Internal reminders and customer-visible messages are distinct. Do not invent a guaranteed response SLA in public copy or overload workers with unlimited startup replay.

## Platform settings and policy versions

Provide authorized administration of relevant property types/features, supported locations, business organization options, approved commission/fee policy, reservation term, review/follow-up thresholds, display defaults and active notification channels/templates. Reuse existing schemas and validators; avoid an arbitrary form-builder redesign.

Commission policy includes rate or amount, basis, payer, timing, configured tax treatment, effective date and active version. Reservation and cancellation policies carry their own reviewed terms. Draft configuration is not an approved policy. Require the proper grant, validation and audit when activating material changes; keep existing offer/case snapshots unchanged.

Manage notification templates in Arabic/English where currently supported, with valid placeholders, preview and test mechanisms. Show real channel availability/failure state. Never expose API keys, database credentials or SMTP secrets in general operational screens or exports.

Public FAQs and explanatory content may be managed if aligned with existing architecture. Do not turn generated copy into purported legal terms or add guarantees inconsistent with actual processing and external approvals.

## Developer, bank and finance-provider directory

Reuse the existing directory and its admin page. Add only missing operating capabilities: search/type/status filters, aliases, source URL/name, retrieved/verified dates, official identifiers when actually available, reviewed qualification status, duplicate resolution and deactivation history.

Use staged CSV/JSON imports with field mapping, validation, preview, duplicate candidates and explicit apply. Preserve manually reviewed fields unless an authorized user intentionally replaces them. A rerun must not create a duplicate organization or change existing references to another legal entity because names are similar.

Do not physically remove an organization referenced by requests or accepted cases; deactivate it while retaining the historical snapshot. Keep the "not listed" customer option and staff review route for a genuinely missing organization.

List source coverage honestly: entries imported from a partial REGA page are not evidence that all qualified developers were imported. Use official qualification labels only when the corresponding source and date support them. The directory is a selection aid, not proof of external approval for a particular transfer.

## Support, communications and audit

Provide customer inquiry/support records linked to the relevant request or case, assignment, status, participant-visible reply and explicitly internal notes. A support agent receives only the information and files their permission/scope allow. Avoid a full independent messaging system when the existing thread/timeline model can serve the need.

Make the audit trail searchable by actor, resource, action and time, with correct scope and redaction. Retain review, permission, policy, publication, offer, approval and financial-evidence decisions. Routine audit rows must not contain entire private file bytes or secret credentials.

## Reports and exports

Provide defined reports for request funnel, conversion between stages, stage aging, employee workload, completion/cancellation reasons, opportunities by location/type/provider and commission expected versus recorded/verified.

Match report totals to underlying records and avoid double-counting repeated revisions or canceled/reopened cases. Separate expected commission from verified money and from an accounting claim of recognized revenue. Report definitions and filters must be visible.

Allow appropriately scoped CSV export using the existing reporting tools. Respect export permissions, redact unnecessary personal data and neutralize spreadsheet formula injection from user-entered text. Large exports must use bounded/background generation when needed and protected downloads, rather than an unbounded request that blocks the API.

## Acceptance and verification

1. A manager can trace every queue count to matching records and reassign work; an assigned-scope employee never sees organization-wide private data through a counter/export.
2. Policy changes are versioned/audited and do not silently change already accepted offers or closing numbers.
3. Directory imports validate and preview before applying, are retry-safe, retain reviewed fields and do not merge distinct entities by name alone.
4. Directory deactivation keeps historical request/case references intact; partial official-source coverage is represented honestly.
5. Support internal notes do not leak to customer timelines. An auditor has no write powers and no automatic private-file access.
6. Reports reconcile to known cases, have defined date/stage semantics, and exports enforce scope and safe cell values.
7. Follow-up jobs and channel handling operate with actual queues/configuration and are documented if unavailable.

Test with several staff scopes and a realistic controlled dataset across pending, published, reserved, completed and canceled cases. Do not manufacture production analytics to fill an empty dashboard.

## Phase-specific handoff and next session

Write `docs/rahoon/roadmap/handoffs/phase-4-handoff.md` and update this phase's actual status in the roadmap. The tailored implementation specification belongs at `docs/rahoon/roadmap/phase-4-operations-reports.md`. Next: **Phase 5 — hardening and launch-readiness verification**. Do not execute it in this session.
