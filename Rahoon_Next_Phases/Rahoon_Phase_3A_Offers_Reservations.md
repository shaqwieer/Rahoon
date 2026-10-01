# Rahoon — Phase 3A: Offers, negotiation, owner acceptance and provisional reservations

**Implement this phase only.** Prerequisites: Completed Phases 1.5 and 2, including effective staff grants, reviewed opportunities and trusted financial snapshots.

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

Turn an interested buyer into a reviewed, versioned offer, allow the real owner to accept or negotiate, and maintain at most one active provisional reservation per opportunity. This phase ends with a conditional reservation ready for transfer coordination. It does not declare official transfer, receipt of funds or completed purchase.

## Offer model and authoritative states

Reuse the existing interest record where useful, but keep interest and offer semantics distinct. An offer links the buyer, opportunity, actual owner, quoted opportunity revision, reviewed financial breakdown, proposed terms, validity deadline and immutable submission revisions. Keep the commission/policy version applicable to that revision when available.

Define a transition table including actor, permission, prerequisites, side effects and allowed retries. Suggested meanings are draft, submitted, under staff review, needs buyer changes, presented to owner, accepted, rejected, withdrawn, expired and superseded. Map them into the existing model; avoid two separate booleans that can make an offer accepted and rejected simultaneously.

The buyer may edit a draft; submitted revisions remain historical evidence. A negotiated counterproposal is a new attributed revision with its own exact terms, not an in-place price edit. Preserve who proposed and who accepted each revision. An accepted old revision does not accept a later counterproposal.

## Buyer experience

Offer entry shows the reviewed opportunity and financial summary, required cash, applicable installment assumptions, proposed owner amount/price, buyer payment method and declared funding status, proposed timing, validity and relevant conditions. Ask only path-specific fields.

An expression of interest must never reserve the property. The buyer can submit, respond to a request for changes, withdraw when allowed, and view their own offer timeline. They cannot act on another buyer's offer, offer on their own property, or manufacture an externally approved financing state.

Use the actual Phase 1 buyer review status. If information is incomplete, explain what is needed. Offerability checks must use current visibility, current approved revision and available status on the server, including for a direct API call bypassing the UI.

Avoid duplicate active offer chains for one buyer/opportunity; a retry must return the same operation outcome or update the intended negotiation chain according to documented rules.

## Staff review and owner decisions

Use Phase 1.5 permissions and resource scopes for reading offers, requesting changes, checking evidence, presenting an offer and managing negotiations. Staff approval means permission to present to the owner, not owner acceptance. Keep an internal review note separate from a note visible to buyer/owner.

The owner sees the financial proposal and the buyer information needed for the decision, not unrestricted personal financial documents. The owner may reject, propose an alternative revision, or accept the exact reviewed version. An employee cannot impersonate owner acceptance because they can edit the listing.

Check whether the opportunity numbers or requirements changed since the offer's quoted revision. Resolve material changes through a reviewed amendment and renewed participant acceptance; never silently reprice the offer. A stale decision produces a clear conflict with the latest state.

## Provisional reservation and exclusivity

Create a reservation only from the required reviewed-and-accepted offer revision and the applicable agreed reservation conditions. An explicit provisional status must remain distinct from any external approval.

Store owner/buyer/offer revision, creation and expiry time, the configured term, conditions, actor and status. Suggested meanings: held, expired, canceled and converted to closing. If the term/policy is not approved, surface the precise missing decision and finish independent work; do not invent a legal deposit condition.

At most one active reservation may hold an opportunity. Enforce this through a transaction and a database-backed invariant appropriate to the stack. Concurrent acceptances must yield one committed winner, without two accepted-active deals or a listing left reserved by a failed transaction.

Make submission, acceptance, cancellation and expiry commands retry-safe. Expiry is checked by authoritative server time on relevant actions as well as by the worker; a delayed worker cannot let an expired reservation block the opportunity indefinitely. Deal conversion in Phase 3B must transfer the exclusivity lock, not briefly release it.

Other pending offers may be held while reserved rather than irreversibly rejected by default. On expiration or cancellation, re-evaluate data freshness, owner intent and publication rules before reopening availability. Record why it returned to published or remains paused. Do not silently revive withdrawn or out-of-date listings.

No automatic collection of a deposit, platform wallet, payout or escrow is required. If a deposit is mentioned in an approved policy, record its conditions/evidence only within the agreed flow; do not invent money movement.

## Screens, notifications and audit

Provide buyer offer form/detail/timeline; owner review/counterproposal; staff review and negotiation queue; and reservation summary with next actions. Use Arabic labels that distinguish "مهتم", "عرض مقدم", "مقبول من المالك" and "حجز مبدئي مشروط".

Notify the relevant participant about actual committed decisions using existing configured channels. Event delivery is retry-safe; failed delivery cannot roll back an already valid offer decision or falsely claim delivery. Audit revisions, decisions, reasons, expiry and actor authority at the time.

## Acceptance and verification

1. Buyer submission enters staff review; presentation to the owner requires the appropriate staff permission.
2. Only the actual owner can accept the exact current reviewed revision. Staff approval alone never reserves a property.
3. Two buyers accepted concurrently produce exactly one active reservation and a clear conflict for the losing operation.
4. Counterproposal acceptance preserves immutable history; stale versions and changed financial terms require renewed review/acceptance.
5. Repeated submit/accept commands do not create duplicate offers or reservations. Revoked staff roles and changed ownership cannot use stale sessions to decide.
6. Expired holds are respected even when the background worker is delayed; cancellation/reopening obeys visibility and owner confirmation rules.
7. A buyer/owner sees only their allowed records and redacted counterparty data, including through direct detail/download APIs.

Include focused concurrency and transition tests and a browser scenario with buyer, staff reviewer and owner as separate actors. Do not test the entire workflow only as the platform owner.

## Phase-specific handoff and next session

Write `docs/rahoon/roadmap/handoffs/phase-3a-handoff.md` and update this phase's actual status in the roadmap. The tailored implementation specification belongs at `docs/rahoon/roadmap/phase-3a-offers-reservations.md`. Next: **Phase 3B — external approvals, transfer coordination and completion**. Do not execute it in this session.
