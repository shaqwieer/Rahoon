# Phase 2 handoff — Discovery, maps, explained matching, comparison, saved searches and calculators

**Status:** Completed · **Date:** 2026-10-02 · **Commits on `master`:** `9a04fdd` (server spine, migration), `b4d3a35` (API
tests), `73ebee3` (web, e2e), `bf66899` (docs, line endings), and a final commit (public developer name restricted to the
directory, its test, an e2e hydration wait, these results).
**Push / deployment:** **not pushed, not deployed.** origin/master and staging are still at `95e2f5c` (Phase 1.5).

## Verified prerequisites

Checked in code before building: the Phase 1.5 handoff's entry points (`Permissions.cs`, `TeamScope.cs`, `RequestContext`),
published opportunities with immutable terms versions and typed snapshots (`OpportunityTerms`), `MarketCalculator` with the
brief's developer and bank examples (tests green), public search (`MarketPublicEndpoints.Search`), `OpportunityProjection.PublicPoint`
(exact point only with the owner's agreement, else a ~1 km cell centre), buyer requests and suggestions, saved opportunities,
interests. The API suite passed before any change (101 tests).

Gaps found and handled in this phase:
- Search ignored extra payments, and `Fit` treated an **unknown installment as fine**. Both now go through one classifier.
- There was **no search map**, only the map on the detail page.
- `/my/saved` returned **full figures for withdrawn or paused** opportunities. It now returns only the reference, the title and «لم تعد متاحة».
- Public listing photos were cached `max-age=600`. They are now `no-cache` with an ETag, so a withdrawn listing's photo is a 404 on the next use.
- **No background worker existed.** Phase 2 adds the first one, a hosted service for alerts.
- The model has **no next-installment date** and **no negotiability field**.

## Implemented behaviour

- **One spine** (`Modules/Market/Discovery`):
  - `SearchCriteria` is the only parser and validator of search state. It produces the canonical query and its hash, and keeps the Phase 1 keys (`sort=fit` → relevance, `price` → total).
  - `DiscoveryQuery` is the only filter builder: published only, public points only.
  - Results, counts, facets, map markers, comparison, matching, the capacity calculator and alerts all read through these two.
- **Strict affordability.** `Affordability.Classify` (C#) and `DiscoveryQuery.Passes/Fails` (SQL) are twins, and a test runs both over the same rows. Rules:
  - The amount due now must be within the cash available.
  - The total must be within the ceiling.
  - The monthly equivalent must be within the comfortable installment. With an annual extra payment, a year of installments plus that payment must be within 12 months of comfort.
  - The largest extra payment must fit in the cash left after paying now.
  - The remaining term must be within the limit.
  - Any unknown figure makes the result **incomplete**, never a pass. Incomplete results are excluded from strict filters and counted.
  - "Schedule known" means one of two things: there is no future balance, or the installment, its frequency and any extra payment's recurrence are all known.
- **Search UI** (`/opportunities`):
  - The first page is server-rendered. After that the URL is the state, through native `history.pushState`, which Next keeps in step with `useSearchParams`.
  - The list and the map are fetched from the same API filters.
  - Every request is aborted when a newer one starts, and a sequence check drops late answers.
  - Filter edits apply after a 600 ms pause. Filters are removable chips with a reset.
  - Primary filters stay visible, and the rest sit in an advanced panel: district, project or developer, area range, bathrooms, delivery range, ceiling, remaining term, amenities (all of them must match) and track.
  - Sorts: relevance (with its explanation), cash now, total and newest. Every sort ends with the id.
  - Pagination happens on the server.
  - Filtered or personal URLs are `noindex, follow` with canonical `/opportunities`.
- **Map**:
  - It shows public points only, clustered on a client-side pixel grid. Approximate points are drawn dashed and labelled «تقريبي».
  - Hovering a card highlights its marker, and clicking a marker scrolls to its card. A marker whose card is on another page gets a notice.
  - «ابحث في هذه المنطقة» puts `bbox` in the URL, and the list applies it too. The API validates bounds: correct ranges, south below north, west below east, a span of at most 40°.
  - A capped map (500 markers) and opportunities without a point are reported, not hidden.
  - On phones there is a list/map switch.
- **Explained matching**:
  - Mandatory eligibility is the buyer's cities and property types.
  - Ranking preferences only order the list: area, bedrooms, readiness, delivery, and districts the buyer named.
  - Each match lists its reasons, the preferences met and not met, and caveats: estimated figures, new financing subject to approval, and «قسط صاحب العقار لا ينتقل إليك» on bank or mixed tracks.
  - Each match also shows the next payments as recorded: amount due now, then the installment with its cadence («موعد القسط القادم غير مسجل»), then the extra payment with its date and recurrence.
  - No percentage is produced anywhere.
  - `BuyerRequest.PreferencesRevision` goes up on any capacity or preference change (decimal scale is ignored), and matches report the revision they used. `/account/buy` shows what fits, what is incomplete and what doesn't fit.
  - Public `match=me` applies the **session's own** profile only. A reference in the URL is ignored, and the echoed URL state never contains the buyer's numbers.
- **Calculators**:
  - One engine still. Optional `Fees` take a payer (buyer, seller or split %) and a timing; `AsOf` is optional too. Both result in `FeeLines` and `Assumptions` in the response.
  - Stored terms are untouched: these inputs are absent on every stored version, and published terms are never recomputed.
  - An optional dated, sourced market reference is compared with the buyer's all-in total, only when that total is complete. It is worded as a difference, never as a saving or a return, and the comparison notes a reference older than one year and the unapproved commission.
  - The capacity calculator counts fitting and incomplete published opportunities with the same rules as search.
  - The per-opportunity calculator on the detail page shows the classifier, the cash left and the annual commitment.
- **Comparison** (`/compare?refs=`, noindex):
  - At most 4 opportunities, picked from cards or the detail page and kept in browser-local storage across tabs.
  - Cells read `value`, «غير معروف بعد» or «لا ينطبق» (bank vs developer paths), never a comparable zero.
  - It shows the approvals per party and, for a signed-in buyer, their fit.
  - An unavailable reference returns only `{reference, available:false}`.
- **Developer name**: shown on cards and searchable through the «project or developer» filter **only when the developer was
  chosen from the organization directory** (`DeveloperPartyId` set). A name the owner typed stays internal; a financier's
  identity is never copied to the opportunity.
- **Favorites**: signed-in only (see decisions). Saving is idempotent, unpublished opportunities answer 404, and unavailable ones are shown honestly.
- **Saved searches** (`/api/market/my/searches`, `/account/searches`):
  - The criteria are normalized and the page is dropped. Saving the same criteria twice returns the first search.
  - Each search can be renamed (with a version check, 409 when stale), have alerts turned on or off (on requires consent), and be paused, resumed or deleted (soft delete).
  - Another person's id answers 404, and staff get 403. A person can keep 20 live searches.
- **Alerts** (`SearchAlertJob` + `SearchAlertWorker`):
  - Saving a search with alerts, turning alerts on and resuming all record the current matches as **baseline**, which is never sent.
  - Each run queues only (search, opportunity) pairs it hasn't seen, plus a new terms version whose cash due now **dropped**. Other versions are recorded and not sent.
  - The unique `(search, opportunity, terms)` key with `INSERT … ON CONFLICT DO NOTHING` makes runs, retries and parallel workers duplicate-safe.
  - Delivery claims rows with `FOR UPDATE SKIP LOCKED`. At send time it re-checks that the opportunity is still published on that version and that the search is still active, alerting and consented; otherwise the row is `skipped` with the reason.
  - One in-app notification goes out per search per run. The event and the status change are written in one transaction.
  - SMS rows are marked `sending` before the gateway is called, so a crash can miss an SMS but never send it twice. Rows stuck in `sending` for 15 minutes are closed as `failed` and never resent. The gateway is the **sandbox**, so SMS rows read `simulated`.
  - The worker runs on `Alerts:Enabled` (Development and Staging: on; base and Testing: off) every `Alerts:IntervalSeconds` (Development 60, Staging 300). The CLI `run-alerts` does one run.
  - Notifications link to `/account/searches`.

## Code entry points

API (`server/src/Rahoon.Api`):
- `Modules/Market/Discovery/`: `SearchCriteria.cs`, `DiscoveryQuery.cs`, `Affordability.cs`, `Matching.cs`, `DiscoveryEndpoints.cs` (`GET /api/market/opportunities`, `/opportunities/map`, `/compare`), `SavedSearchEndpoints.cs`, `SearchAlertJob.cs` (job and worker), `TermsSnapshot.cs` (typed snapshot, shared by team, seed and tests), `DiscoveryEntities.cs`
- Changed: `MarketPublicEndpoints.cs` (calculators, detail with `schedule`, per-opportunity fit, photo caching; search moved out), `MarketCalculator.cs` (`Fees`, `AsOf`, `FeeLines`, `Assumptions`), `BuyerEndpoints.cs` (revision, matches through the spine, honest saved list, alert links), `OpportunityProjection.cs` (developer name, recurrence, remaining months on cards), `TeamOpportunityEndpoints.cs` (snapshot helper, developer recorded on creation), `MarketService.NotifyBySmsAsync` (returns the result), `ModuleRegistry.cs`, `Program.cs` (`run-alerts`), appsettings (`Alerts`)

Web (`web/src`): `components/market/discovery/*` (DiscoveryView, SearchPanel, ResultsMap, FitSummary, CompareControls, CompareView,
SaveSearch, SavedSearchList), `lib/market/search.ts` (URL state), `lib/market/compare.ts`, `app/(public)/opportunities/page.tsx`,
`app/(public)/compare/page.tsx`, `app/account/searches/page.tsx`. Changed: `OpportunityCard`, `opportunities/[ref]/page.tsx`,
`OpportunityActions` (calculator), `Calculators`, `TermsBreakdown`, `account/buy`, `account/saved`, `AccountNav`, `lib/hooks.ts`
(`useMediaQuery`). Removed: `components/market/SearchFilters.tsx`.

## Migrations and their application state

`20261001200520_DiscoveryMatching` makes these changes:
- `opportunity_terms`: adds `quality`, `extra_payment_recurrence`, `annual_extra_payment`, `one_off_extra_payment`, `next_extra_payment_date`, `schedule_known`.
- `opportunities`: adds `developer_party_id` and `developer_name`, plus the partial index `ix_opportunities_published_point`.
- `buyer_requests`: adds `preferences_revision`, default 1.
- New tables `market.saved_searches` and `market.search_alerts`.
- **Backfill reads the stored `input_json` and `result_json` only.** No terms version is recomputed.

Where it has been applied:
- Test databases: applied on every run.
- Local development database: applied on startup on 2026-10-01. The backfill was checked: OP-2026-00005 has an annual extra payment of 50,000 and its schedule is known; developer names are filled from the obligations.
- Staging: **not applied** (not deployed). The first startup after deployment migrates.
- Production: none.

## Permissions

No new or changed keys. Everything in this phase is public, or belongs to the signed-in individual's own rows (`IApplicantOwned`
plus explicit owner checks). Staff calling `/api/market/my/*` get 403.

## Adopted decisions

See `../decisions.md` («Adopted in Phase 2»). The main ones:
- Guest favorites are not stored locally (existing UX sends guests to sign in).
- There is no negotiability filter (not recorded anywhere).
- The map uses OSM tiles via env (D3 is open).
- Re-alerts happen only when cash now drops.
- SMS alerts are sandboxed and labelled as such (D1).
- A one-off extra payment must fit in the cash left after paying now.
- Relevance is SQL tiers, and no percentage is shown.

**Behaviour change**: the seeded Jeddah townhouse (7,000/month equivalent plus a 50,000 annual payment) no longer passes a
7,000 monthly comfort. The year costs 134,000, which is more than 84,000. The Phase 1 test that asserted the old result was updated, as acceptance check 2 requires.

## Tests and checks run (actual results)

- `dotnet test server/tests/Rahoon.Api.Tests`: **123 passed, 0 failed** on the final tree. Before the developer-name fix, the 122-test version ran three times in a row, all green. That is 101 earlier tests (one updated, see above) plus 22 new:
  - `AffordabilityTests`: fee allocation, stored JSON unchanged, bank rule, annual payment, incomplete cases.
  - `DiscoveryTests`: SQL and C# agreement on 8 schedule shapes × 7 profiles; list vs map vs pages vs bounds; public points and hidden statuses; matching revisions; `match=me` privacy; favorites and comparison availability; saved-search ownership; a typed developer name never public; photo revalidation.
  - `SearchAlertTests`: baseline and once-only delivery, 4 parallel runs, send-time skips (withdrawn, paused, deleted, resume baseline), revision rule, sandbox SMS once, stuck rows.
- `MarketCalculatorTests` is unchanged and green: the developer and bank examples.
- Web: `tsc --noEmit` ✓, `eslint src e2e` ✓, `next build` ✓.
- Playwright (local stack): **22 passed** on the final tree. That is the existing 16 plus 6 new in `e2e/discovery.spec.ts`. One run on the final tree failed once: the test changed a filter right after a reload, before hydration. The test now waits for the client-only map, and `discovery.spec.ts` then passed 3 times in a row. The new tests check:
  - URL reload and Back restore the same list and map set.
  - A villa response delayed 3.5 s never overwrites the newer apartment search.
  - The comparison shows «لا ينطبق», «غير معروف بعد» and «لم تعد متاحة», and the tray opens it.
  - A signed-in buyer saves a search with alerts, pauses it and reopens it.
  - No horizontal scroll at 390 px on `/opportunities` (list and map), `/compare`, `/calculators`, `/opportunities?match=me`, `/account/buy`, `/account/searches` and `/account/saved`. Screenshots are in `web/test-results/p2-390-*.png`.
- Live alert run on the local stack: a saved search was resumed and a new opportunity was published through the UI journey. Within 60 s the worker logged «1 searches, 2 queued, 2 delivered» and one in-app event «2 فرص جديدة تطابق بحثك …».
- Calculator in a real browser: with the brief's developer example and the 15,000 fee carried by the seller, the result was **310,000 due now**, **275,000 seller net**, **990,000 total** and the fee lines. A 1,100,000 reference gave «أقل من المرجع بفرق 110,000».
- Query measurement: a throwaway database (dropped after) held **20,003 published opportunities**.
  - Endpoint times: list 0.11–0.17 s (count, incomplete count, page, two facets), map 0.085 s, bounded map 0.01 s.
  - `EXPLAIN ANALYZE`: filtered relevance page 29 ms (`ix_opportunities_status_city_property_type`), full count 18 ms, map window 0.6 ms (`ix_opportunities_published_point`).
  - No other index is justified at this scale.
- Browser: Chrome via the extension rendered the page (list, map, clusters, approximate marker). Its screenshots then timed out repeatedly, so the interactive checks ran in Playwright Chromium (desktop 1440 px and 390 px). Desktop screenshots: `web/test-results/p2-desktop-*.png`.

## How to reproduce the acceptance scenario (local)

1. `bash scripts/dev-api.sh` (or `--reset`), `cd web && npm run dev`.
2. `/opportunities?maxNow=400000&maxInstallment=7000`: the Jeddah townhouse is excluded (annual payment), and the counter reports the excluded incomplete ones. Raise the installment to `11200` and it appears with «الأقساط مع الدفعة السنوية … ضمن ما يريحك».
3. Move the map and click «ابحث في هذه المنطقة». The URL gets `bbox` and the list follows. Reload, and press Back and Forward.
4. Click «قارن» on two or three cards and then «قارن الآن». The bank villa shows «لا ينطبق» for installments.
5. Sign in as لينا (`0561110012`, buyer with a request): `/opportunities?match=me` and `/account/buy` (explained matches, revision). Edit the preferences and the revision goes up.
6. «احفظ هذا البحث» with alerts and consent, then `/account/searches`. Publish a matching opportunity as the team. Within a minute an in-app notification «فرصة جديدة تطابق بحثك …» appears, or run `dotnet run -- run-alerts`.
7. `/calculators`: add a fee, switch its payer between buyer, seller and split, and add a dated reference.

## Remaining limitations and known issues

- **SMS is sandboxed (D1).** Alerts by SMS are recorded as `simulated`. In-app alerts are real. There is no e-mail channel (no provider).
- Guest favorites are not supported: guests sign in first. This is a product choice that can be revisited.
- The model has no next-installment date, so the next payments show the cadence and the extra payment's recorded date only.
- No negotiability filter: the field doesn't exist. Adding it means a catalog field and team input first.
- The map uses public OSM tiles (D3). The marker cap is 500 per view, with a notice.
- Map clustering is a simple client-side pixel grid. Many opportunities sharing one approximate cell open as a list popup.
- The filters need JavaScript. A change made before the page hydrates (slow phone, first dev compile) is lost; there is no plain-form fallback.
- The Phase 1 `MarketCalculator.Fit` is kept only for its existing tests. Every endpoint uses `Affordability`.
- Relevance tiers are deliberately simple: preferences met, then figure quality, then cash now, then newest.
- No market reference data is stored per opportunity. The comparison with a market price exists only in the calculator, with a reference the person enters.

## Next phase and its starting instruction

**Phase 3A — offers, negotiation and provisional reservations.** Starting instruction:

> Read the repository instructions, `docs/rahoon/roadmap/README.md`, `decisions.md`, `handoffs/phase-1.5-handoff.md` and
> `handoffs/phase-2-handoff.md`. Read the Phase 3A brief `Rahoon_Next_Phases/Rahoon_Phase_3A_Offers_Reservations.md` and write
> its plan as `docs/rahoon/roadmap/phase-3a-offers-reservations.md`. Verify prerequisites from actual code, implement ONLY
> Phase 3A, complete its acceptance checks, write its handoff and update roadmap status. Do not start the next phase.
