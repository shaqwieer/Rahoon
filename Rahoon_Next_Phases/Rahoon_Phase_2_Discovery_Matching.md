# Rahoon — Phase 2: Advanced discovery, maps, explained matching and calculators

**Implement this phase only.** Prerequisites: Completed Phase 1.5 access foundation and the Phase 1 published opportunity/search/calculation baseline.

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

Give the buyer a credible answer to: "Which available opportunities fit what I can pay now and what I can afford later?" Make list, map, comparison, calculator and saved search agree on the same reviewed opportunity data. Preserve the distinction between an expression of interest and an offer; do not implement offers or reservations here.

## Search and property presentation

- Keep the professional property-specific card and details page. The primary monetary label is the buyer's required cash now, with a separate total purchase/commitment label, verification status and relevant payment schedule. Do not market a developer project launch card as a specific exit opportunity.
- Filters: city, district, project/developer, property type multi-select, area range, applicable bedrooms/bathrooms, ready/off-plan, delivery range, required cash now, total purchase/commitment ceiling, installment amount and frequency, remaining term, relevant amenities and negotiability when recorded.
- Distinguish contract original price, owner's proposed proceeds, buyer cash now and total future commitment in both API names and UI labels. Do not use one overloaded `price` field for every meaning.
- Start with useful primary filters and put the rest under a mobile-friendly advanced panel. Provide removable active-filter chips, result count, explicit reset, loading/error/empty states and server pagination.
- Serialize validated search state in the URL. Back/forward navigation, shared links and reload must preserve the same filters. Debounce expensive interactions and ignore stale responses.
- Sort by explained relevance, lowest required cash, total purchase amount and newest publication. Use a deterministic tie-breaker. Unknown financial values stay unknown and cannot pass a strict affordability filter as zero.
- Apply visibility and availability on the server to results, counts, facets and map endpoints. A stale public cache must not continue exposing a withdrawn opportunity or private evidence.

## Map discovery

Reuse the current OpenStreetMap-based implementation and its production-appropriate provider. Add list/map synchronization, marker selection, clustering where useful, "search this area", and validated geographic bounds. Map counts and cards must use the same filters and visibility policy.

Use only the coordinates permitted for public display. A public bounding-box query, cluster, marker payload or Google Maps link must not reveal the precise private location when only an approximate location is allowed. Label approximate locations. Do not return private coordinates and merely hide them in the UI.

## Explained matching

Reuse buyer preferences and capacity, with an explicit revision/version when they change. Separate mandatory eligibility filters from ranking preferences. A relevant score is a ranking aid, not financing approval or a recommendation guaranteeing affordability.

For each match explain actual reasons, such as city match, cash requirement below the stated limit, acceptable property type, or delivery fit. Record caveats for unverified/estimated values and unavailable future financing. Do not invent a percentage match that cannot be explained from the data.

Compare actual installment frequency and due dates. A quarterly payment divided by three is useful as a labelled monthly equivalent, but a separate annual or balloon payment may still make the opportunity unsuitable. Include the next material payments and known lump sums. If critical schedule or fee information is missing, classify affordability as incomplete rather than confirmed.

For a bank-connected property, never use the seller's current installment as the buyer's future installment. Buyer financing remains a separate estimate or externally approved arrangement. Offer meaningful cash-purchase search even when no buyer financing terms are available.

## Calculators and comparison

Extend the existing three calculators: owner exit, buyer capacity, and opportunity cost. Maintain one trusted financial calculation contract. Use appropriate decimal precision, explicit fee allocation and input provenance/date. Financial examples are not approved real fees.

Developer example: contract 1,000,000 SAR, credited payments 300,000, outstanding 700,000 including arrears 20,000, seller discount 10,000, and buyer fees of 15,000 payable now. If the buyer pays the arrears, seller amount is 290,000, cash now 325,000, future outstanding 680,000 and total commitment 1,005,000. Do not add arrears twice. Different payer allocations must change the breakdown coherently.

Bank example: proposed sale price 1,100,000, valid bank payoff amount 800,000, and seller costs 10,000 yield estimated seller net 290,000. This does not make buyer cash 290,000 or transfer the existing installment. Missing official figures produce an incomplete estimate.

The known financing/fee policy must be preserved. Show assumptions and unknown components. A comparison with market price requires a suitable dated reference and comparable all-in purchase values; it is not a guaranteed saving or return.

Allow comparison of up to four opportunities with aligned cash-now, total commitment, installments/frequency, extra payments, verification, location, delivery and external approval status. Different bank and developer paths must display "not applicable" or "not yet determined" rather than false comparable zeros. If a saved opportunity becomes unavailable, display that honestly without revealing removed private information.

## Favorites, saved searches and alerts

- Favorites persist for signed-in users. Guest favorites may be stored locally and merged idempotently on sign-in if supported by current UX.
- Store normalized saved search definitions, meaningful names and alert preferences. Users can update, pause and delete their own searches. Never permit cross-user access by altering an ID.
- Send alerts only for newly eligible public opportunities or meaningful qualifying revisions, with the configured channel and user consent. Deduplicate by user/search/opportunity/revision and suppress retries from sending duplicates.
- Use the real existing worker/queue. Respect visibility at send time, opt-out and channel failures. Unconfigured/disabled delivery must be visible in the handoff and UI as applicable; do not claim alerts are operational through a front-end demo.

## Acceptance and verification

1. URL-restored filters produce the same list and map set, with stable pagination and no stale-response overwrite.
2. Unknown cash/fees do not pass a strict budget filter. A large annual payment can invalidate otherwise comfortable monthly averages.
3. The developer and bank examples above calculate correctly, and buyer-relevant bank installments never come from the seller's old schedule.
4. Public map bounds and payloads do not expose exact private coordinates or unpublished opportunities.
5. Preference edits update matching and explanations; shared search URLs do not expose another buyer's profile.
6. Favorites, comparison and saved searches handle availability changes, duplicate operations and unauthorized IDs.
7. A real alert job is retry-safe, observes opt-out and does not send for withdrawn opportunities. Test delivery with the configured test mechanism, not real customer spam.

Measure the affected database queries and use justified indexes/query changes rather than introducing a separate search platform by default. Preserve existing public SEO behavior; public search URLs must not make account dashboards or private calculator inputs indexable.

## Phase-specific handoff and next session

Write `docs/rahoon/roadmap/handoffs/phase-2-handoff.md` and update this phase's actual status in the roadmap. The tailored implementation specification belongs at `docs/rahoon/roadmap/phase-2-discovery-matching.md`. Next: **Phase 3A — offers, negotiation and provisional reservations**. Do not execute it in this session.
