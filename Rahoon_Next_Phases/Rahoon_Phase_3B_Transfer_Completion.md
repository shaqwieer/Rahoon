# Rahoon — Phase 3B: External approvals, transfer coordination, payment evidence and completion

**Implement this phase only.** Prerequisites: Completed Phase 3A with accepted immutable offer revision, valid reservation and exclusivity rules.

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

Move a reserved opportunity through a documented completion case: participant documents, relevant developer/finance-provider approvals, required amounts and their evidence, transfer confirmation and a final participant file. Rahoon coordinates this transaction; it does not become a judicial agent, lender or debt-collection service.

## Completion case and process templates

Create at most one active completion case for the accepted reservation and opportunity. Convert the reservation atomically and maintain exclusivity throughout the transition. Capture accepted offer, listing numbers and policy revisions rather than referencing mutable live totals alone.

Assign a permitted case manager and use path-specific task templates for developer contracts, bank/finance-provider properties and reviewed mixed commitments. Tasks have responsible actor/team, due date, prerequisite, blocking status, evidence requirements and completion/reopen history.

Suggested case meanings: preparation, waiting for documents, waiting for external approval, ready to complete, completion in progress, completed, blocked and canceled. Define exact permitted transitions; tasks, external approvals and money evidence are not all compressed into one status.

The buyer/owner timeline shows what is done, what is needed from them and who is handling the next step. Internal notes remain internal. An off-plan contract transfer can complete before physical delivery of the property; do not require or claim physical handover when it is not the completion event for that path.

## External approvals and current financial evidence

Track each required external decision independently: not requested, requested, conditional, approved, rejected and expired, with approving organization, evidence, conditions, effective/expiry date and reviewer. Manual recording is acceptable where no real integration exists, but label its provenance and reviewer; do not claim a connected bank/REGA service.

For developer cases, use the relevant reviewed transfer conditions, outstanding balance and arrears allocation. For bank cases, require current verified payoff/settlement figures when that route depends on them, with valid dates and confirmation of what they include. Do not compute a bank's official amount as remaining installments multiplied by count.

The buyer's new finance approval is separate from the seller's existing finance and from approval to transfer the property. A party must not be allowed to set all these approvals through one "approved" checkbox. Record unsupported external conditions as unresolved blockers, not fictitious approvals.

Material changes to price, payoff, fee allocation, funding or transfer requirements require a reviewed amendment with renewed participant confirmation as appropriate. Preserve earlier accepted snapshots and record the effect on the case.

## Payment evidence without holding funds

Use a transaction-specific breakdown: payer, beneficiary, amount, purpose, due stage, source, whether included in another balance, and evidence. Distinguish planned, reported, verified and reversed/corrected money records. Use Finance Officer permission to verify money evidence and a separate authorized operation to confirm transfer.

Record external payments through their actual receipts/reference and authorized verification. The platform does not make a bank transfer simply because an employee clicks "paid". Avoid duplicate totals from retried evidence submissions or assigning the same recorded payment twice. Allow a single external transaction to be explicitly allocated across line items where appropriate rather than treating its receipt hash as independent money each time.

Developer example: credited paid 300,000, total remaining 700,000 including arrears 20,000, seller discount 10,000 and buyer fees 15,000 payable now. When buyer pays arrears, seller amount 290,000, buyer cash now 325,000, future obligation 680,000 and total commitment 1,005,000. Completion does not require paying future installments that the approved transfer leaves outstanding.

Bank example: agreed sale 1,100,000, valid payoff amount 800,000 and seller costs 10,000 produce seller net 290,000 under that allocation. Verify the actual paid amounts and accepted funding path; historical installments and the old loan schedule are not seller equity or buyer financing terms.

Store expected commission from the accepted policy snapshot, actual recorded amount and collection/evidence state. Do not silently recompute an existing case when an administrator later changes the commission rate. Taxes and obligations follow approved configuration and applicable reviewed requirements, not invented fixed rates.

## Completion gates and final file

Completion requires the path's mandatory tasks, valid required external approvals, reviewed accepted numbers, evidence of required closing-stage payments or explicitly approved conditions, and confirmation/evidence of the actual transfer event. Staff cannot bypass this by editing a generic status field.

Keep official transfer confirmation, payment confirmation and any later physical-delivery milestone separate. Mark the opportunity completed/unavailable only through the supported final transition. Produce a participant-appropriate completion summary with amounts, relevant schedule/remaining obligations, dates, references and the documents they may access. Do not expose the other party's private documents in the downloadable bundle.

If incomplete or canceled, retain history and state-specific reasons, notify the parties, and route any return to market through owner intent and data/publication review. Refund, forfeiture or fee treatment comes from approved terms; do not make a legal determination through a default arithmetic rule.

Completed facts are not casually editable. Support a privileged documented correction or reopening flow when genuinely necessary, with reason, audit, valid exclusivity state and required re-confirmation. A correction does not automatically make a sold opportunity available to a second buyer.

## Acceptance and verification

1. Reservation-to-case conversion cannot produce two active cases or release exclusivity during a race.
2. Developer and bank paths have distinct evidence and approval gates; a document reviewer alone cannot verify payment or declare official transfer.
3. Expired/conditional external approvals block completion where the missing condition is required. Renewed figures require the relevant amendment/confirmation.
4. Payment evidence retries and balance allocations cannot double-count arrears, fees, principal or a recorded external transfer.
5. A financing installment remaining after an approved developer transfer is disclosed rather than wrongly treated as a failure to complete.
6. Completed cases preserve offer/policy snapshots and produce correctly scoped participant files. Policy edits affect new proposals only unless an explicit accepted amendment says otherwise.
7. Cancellation and privileged correction preserve evidence, reasons and availability invariants. Unrelated accounts cannot read the case, receipts or final file by changing an ID.

Verify at least one developer scenario and one bank scenario, a blocked completion, a financial amendment, a duplicate-evidence retry and a concurrent case-conversion attempt. No real money movement is needed for these tests.

## Phase-specific handoff and next session

Write `docs/rahoon/roadmap/handoffs/phase-3b-handoff.md` and update this phase's actual status in the roadmap. The tailored implementation specification belongs at `docs/rahoon/roadmap/phase-3b-transfer-completion.md`. Next: **Phase 4 — operational administration, configuration and reporting**. Do not execute it in this session.
