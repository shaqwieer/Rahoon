# Phase 2 — Discovery, maps, explained matching, comparison, saved searches and calculators

**Status:** 🟡 In progress (started 2026-10-01). The status section at the end is kept current during the session.
**Brief:** `Rahoon_Next_Phases/Rahoon_Phase_2_Discovery_Matching.md` (owner's phase pack). This file maps that brief onto
the code. **Prerequisite:** Phase 1.5 ([handoff](handoffs/phase-1.5-handoff.md)).

## Business context (stable)

The buyer's question: *"Which available opportunities fit what I can pay now and what I can afford later?"* List, map,
comparison, calculators, matching and saved searches must answer it from **the same reviewed, published data and the same
rules**. An expression of interest stays an interest (no offers, no reservations: Phase 3A). Unknown is never zero. A
bank-connected property's current installment never becomes the buyer's installment. A score is a ranking aid, never a
financing approval.

## Verified baseline (read in code before building)

| Area | Where | State found |
|---|---|---|
| Public search | `MarketPublicEndpoints.Search` | City/type/readiness/min area/bedrooms/track; budget filters by due now, total, monthly-equivalent installment; unknown never passes; page size ≤ 24. **Gaps:** no area max, bathrooms, district/project/developer, delivery range, remaining term, amenities, bounds; no deterministic tie-breaker; extra payments ignored by the filter; `Fit` treats an unknown installment as fine. |
| Map | `LocationMap` (detail page only), `OpportunityProjection.PublicPoint` | Public point = exact only with the owner's agreement, else the centre of a ~1 km cell. **No search map.** |
| Matching | `BuyerEndpoints.Mine` | Buyer request filtered by cities/types, `MarketCalculator.Fit`, up to 12. No revision, no ranking preferences, no caveats. |
| Calculators | `MarketCalculator.Compute`, `/calc/terms`, `/calc/capacity`, `/calc/opportunities/{ref}/fit` | Developer and bank examples correct (tests). No fee allocation, provenance/date, market reference. |
| Favorites | `SavedOpportunity`, `/opportunities/{ref}/save|unsave`, `/my/saved` | Signed-in only. `/my/saved` returns full figures of withdrawn opportunities. |
| Workers | — | **None exists.** No hosted service or queue in the API. |
| Caching | `Program.cs` sets `no-store` on every API response; `PublicPhoto` sets `public, max-age=600` | A withdrawn listing's photo could stay cached 10 minutes. |

## Model changes (migration `DiscoveryMatching`)

| Table | Change | Why |
|---|---|---|
| `market.opportunity_terms` | `quality`, `extra_payment_recurrence`, `annual_extra_payment`, `one_off_extra_payment`, `next_extra_payment_date`, `schedule_known` | Typed schedule snapshot for strict affordability in SQL. **Backfilled by reading `input_json`/`result_json` only**; published terms are never recomputed (they are immutable once sent). |
| `market.opportunities` | `developer_party_id`, `developer_name` | Developer filter/label. Backfilled from the live developer obligation. The **financier's identity is never public** and never filterable. |
| `market.buyer_requests` | `preferences_revision` | Incremented on any capacity or preference change; matches report the revision they used. |
| `market.saved_searches` | new | Owner, name, canonical query + hash, alerts on/off, channel, consent time, paused/deleted, baseline time. Unique live (owner, hash). `IApplicantOwned`. |
| `market.search_alerts` | new | (search, opportunity, terms version) unique; kind `baseline/new/revision`; status `baseline/pending/sending/sent/simulated/failed/skipped`; attempts; event id. `IApplicantOwned`. |

## One spine for list, map, facets, saved searches and alerts

- `Discovery/SearchCriteria.cs` — the only parser/validator of search state (from a query string or a saved search),
  with a canonical query string (sorted keys) used for URLs, saved searches and their hash. Existing keys stay (`city`,
  `types`, `maxNow`, `maxInstallment`, `freq`, `maxTotal`, `readiness`, `minArea`, `bedrooms`, `track`, `sort`, `page`);
  `sort=fit` → `relevance`, `sort=price` → `total`. New keys: `district`, `project`, `developer` (directory id), `maxArea`,
  `bathrooms`, `deliveryFrom`, `deliveryTo`, `maxTerm`, `features`, `bbox` (`s,w,n,e`), `match=me`.
- `Discovery/DiscoveryQuery.cs` — the only `IQueryable` filter builder over *published opportunity + its published terms*.
  Visibility (Published only, public coordinates only) lives here, so results, counts, facets, map markers, saved-search
  evaluation and alerts cannot drift apart. Sorts: `relevance` (complete and verified figures first, then lowest cash
  now when a budget is given, then newest), `now`, `total`, `newest`; every sort ends with `Id`.
- `Discovery/Affordability.cs` — the only affordability classifier: `fits` / `does_not_fit` / `incomplete`, with reasons,
  limits, unknowns and the next material payments. Its SQL twin is in `DiscoveryQuery`; a test runs both over the same rows.

### Strict affordability rules (C# and SQL)

| Capacity given | Rule | Unknown → |
|---|---|---|
| Cash now `A` | due now ≤ `A` | incomplete (excluded, counted) |
| Max total `T` | buyer total ≤ `T` | incomplete |
| Comfortable installment `C` (+ frequency) | no future balance → passes; else monthly equivalent ≤ `C`/month **and** (installments per year + annual extra payments) ≤ 12 × `C`/month | installment, frequency or extra-payment recurrence unknown → incomplete |
| Cash now `A` and an extra payment | the largest extra payment ≤ `A` − due now (kept from Phase 1) | — |
| Max remaining term | remaining months ≤ max; no future balance passes | incomplete |

A bank-track terms snapshot has no installment (the seller's financing ends at payoff), so the buyer's installment is never
taken from the seller's schedule; buyer financing is shown as "determined by your financier" (not estimated by Rahoon).
The schema holds no next-installment date: the next material payments are the amount due now, the installment with its
cadence ("next due date not recorded"), and the extra payment with its recorded date and recurrence.

## Map

`GET /api/market/opportunities/map` takes the same criteria and returns public points only (`lat/lng` = the public point,
`precision`), capped (500) with `capped`, `total`, and `withoutLocation`. `bbox` is validated (ranges, south < north,
west < east, span ≤ 40°). With `bbox` in the URL the list applies it too. Client-side grid clustering, list ↔ marker
selection, "search this area". Approximate markers are drawn and labelled as approximate. Tiles stay OSM via
`NEXT_PUBLIC_MAP_TILES` (decision D3 still open).

## Matching (buyer)

`Discovery/Matching.cs`: **mandatory eligibility** = published + buyer's cities + property types; **affordability** =
the classifier on the buyer's declared capacity; **ranking preferences** (area range, bedrooms, readiness, delivery by,
district mentioned in the buyer's areas) only order results. Each match lists the actual reasons, caveats (estimated or
unverified figures, new financing needed, seller's installment not transferable, external approval status) and the next
payments. No percentage. `GET /api/market/buyer-requests/mine` returns matches with `revision`. Public search accepts
`match=me` only: the profile comes from the session, never from a reference in the URL.

## Calculators and comparison

- One engine stays `MarketCalculator.Compute`. New optional inputs (`Fees` with payer buyer/seller/split and timing,
  `AsOf`) default to absent, so stored terms deserialize unchanged and existing results are untouched.
- `/calc/terms` adds an optional dated market reference (value, date, source): compared with the buyer's all-in total only
  when that is complete; worded as a difference, never a saving or a return.
- `/calc/capacity` counts fitting and incomplete published opportunities through the classifier.
- `/calc/opportunities/{ref}/fit` returns the classifier's result (cash left, annual burden, next payments).
- `GET /api/market/compare?refs=` — up to 4; cells carry `value` / `unknown` / `not_applicable`; an unavailable reference
  returns only `{reference, available:false}`.

## Saved searches and alerts

- `/api/market/my/searches` (individual only): list, create (idempotent per canonical query), rename / alerts / channel
  (version check), pause, resume, delete. Another person's id → 404. Max 20 live searches.
- Creation (and resume) records current matches as `baseline` rows, never sent. Each run inserts only pairs not seen
  before (`new`), or a new terms version whose cash now dropped (`revision`); `INSERT … ON CONFLICT DO NOTHING`.
- Delivery: rows claimed with `FOR UPDATE SKIP LOCKED`; at send time the opportunity must still be published on the same
  terms version and the search active, alerting and consented, else `skipped` with the reason. One in-app notification per
  search per run (event + status change in one transaction). SMS: rows marked `sending` before the gateway call, so a crash
  leaves a missed alert, never a duplicate. The SMS gateway is the sandbox (D1): results are recorded as `simulated` and the
  UI says no SMS is sent yet.
- `SearchAlertWorker` (`BackgroundService`, `Alerts:Enabled`, `Alerts:IntervalSeconds`): on in Development and Staging,
  off in Testing (tests call `SearchAlertJob.RunOnceAsync`).

## Web

- `/opportunities`: server-rendered first page; then the URL is the state (`history.pushState` through Next's router
  integration), the client fetches list and map with abort + sequence guard (a stale response never overwrites a newer
  one), debounced filter changes, removable chips, reset, advanced panel, server pagination, list/map split on desktop and a
  list/map switch on phones. Filtered URLs are `noindex, follow` with canonical `/opportunities`.
- Compare tray on cards (≤ 4, local to the browser) → `/compare?refs=` (noindex).
- "Save this search" (signed in) → `/account/searches` (rename, pause/resume alerts, delete, open).
- `/account/buy`: explained matches. Opportunity page calculator shows the classifier result and the next payments.
- `/calculators`: fee allocation rows, as-of date, market reference; capacity shows fitting and incomplete counts.

## Permissions

No new permission keys: everything in this phase is public or belongs to the signed-in individual's own rows. Team
screens are unchanged.

## Adopted decisions (reversible)

| Topic | Choice | Why |
|---|---|---|
| Guest favorites | Not stored locally; guests are sent to sign in (existing UX) | Brief: "if supported by current UX" |
| Negotiability filter | Not offered | Not recorded anywhere in the model |
| Map provider | OSM tiles via env | D3 open |
| Relevance | Quality tier → cash now (when budgeted) → newest → id | Explainable, SQL-expressible, paginated in the database |
| One-off extra payment | Must fit in cash left after the amount due now (Phase 1 rule kept) | Conservative; shown as a limit with its date |
| Re-alert on revision | Only when cash now drops (or becomes known) | "Meaningful qualifying revision" |
| Alert channel | In-app always; SMS optional, sandboxed and labelled | D1 open |
| Search platform | PostgreSQL with indexes; no separate engine | Brief |

## Acceptance checks → tests

| # | Check | Test |
|---|---|---|
| 1 | URL-restored filters give the same list and map set; stable pagination; no stale overwrite | `DiscoveryTests` (list vs map set, ordering, pages) + Playwright `discovery.spec.ts` (reload/back, delayed first response) |
| 2 | Unknown cash/fees excluded; annual payment invalidates a comfortable monthly average | `AffordabilityTests` + `DiscoveryTests` (SQL vs C# agreement) |
| 3 | Developer and bank examples; bank installment never from seller | `MarketCalculatorTests` (unchanged) + `AffordabilityTests` + `DiscoveryTests` |
| 4 | Map bounds/payloads expose no exact private point and no unpublished opportunity | `DiscoveryTests` |
| 5 | Preference edits update matching and revision; shared URLs don't expose another buyer's profile | `DiscoveryTests` |
| 6 | Favorites, comparison and saved searches handle unavailability, duplicates, foreign ids | `DiscoveryTests` |
| 7 | Alert job retry-safe, opt-out, no send for withdrawn; tested with the sandbox | `SearchAlertTests` |

## Status (kept current during the session)

- [ ] Plan (this file)
- [ ] Server: criteria, query, affordability, map, compare, matching, calculators, migration
- [ ] Server: saved searches, alerts job and worker
- [ ] Web: discovery page, map, compare, saved searches, matches, calculators
- [ ] Tests: API, Playwright, browser desktop/390 px
- [ ] Query measurement
- [ ] Handoff and roadmap status
