# Phase M0 + M1 — Redefinition and the request → review → opportunity slice

> **Product source of truth:** [`docs/product/product-definition.md`](../product/product-definition.md) (2026-10-01).
> **Inventory of the old model:** [`docs/redefinition/legacy-inventory.md`](../redefinition/legacy-inventory.md).
> Branch `redefine/exit-marketplace`; rollback tag `legacy-mortgage-final`.

**Scope:** this phase covers M0 (correct the model) and M1 (the full request / review / opportunity slice). M2 (advanced search and matching) and M3 (offers, reservation, completion) start **only when the product owner asks**.

**Definition of done:** the review journey works end to end.
1. An owner sends a request.
2. The team asks for completion, then approves it and prepares an opportunity.
3. The owner confirms the summary, and the team publishes it.
4. A buyer finds it, registers interest, and the team follows the interest.

It also meets the 11 acceptance criteria of the brief (§13), each checked below.

## Steps

| # | Step | Tech status |
|---|---|---|
| 0 | Inventory, product definition, phase plan; legacy flag on API and web (old endpoints, jobs, seed and routes off; non-team staff login refused) | ✅ |
| 1 | Public site: new home, navigation (الرئيسية، بيع عقارك، الفرص المتاحة، كيف نعمل، الحاسبات، تواصل معنا، حسابي), how-it-works, contact, legal drafts, SEO | ✅ |
| 2 | Mobile-first sign-in (reuses the account by mobile) and `/account` | ✅ |
| 3 | Market backend: field catalog, sale requests and obligations, draft autosave, submit, private documents and listing photos | ✅ |
| 4 | Calculator (developer / financier / buyer capacity / opportunity cost) + public endpoints + verbatim tests of the 3 examples | ✅ |
| 5 | Seller wizard (3 steps, device draft before sign-in) and completion file (4 groups, map picker, photos, documents) | ✅ |
| 6 | Team review of sale requests (assign, completion request, approve/reject with reason, figure verification with source and date, external approval log) | ✅ |
| 7 | Opportunity preparation, terms versions, owner confirmation, publish / pause / withdraw | ✅ |
| 8 | Public listing: cards, server-side search and pagination (city, type, due now, installment), details page, map with approximate location | ✅ |
| 9 | Buyer request (3 steps), team review, status and suggested opportunities with reasons, edit preferences | ✅ |
| 10 | Interest linked to the opportunity, the buyer profile and the terms version; team follow-up; saved opportunities | ✅ |
| 11 | Calculators page (3 calculators) | ✅ |
| 12 | E2E journey + mobile 390 checks, docs, handoff | ✅ |

## Acceptance criteria (brief §13)

| # | Criterion | Where verified |
|---|---|---|
| 1 | No old services, roles or texts. Saudi market, currency and cities | Legacy-off test; web proxy 404; content review |
| 2 | A developer request asks no bank fields. A bank request promises no continued installments. Land asks no rooms | `FieldCatalogTests` |
| 3 | First request not blocked by photos or documents. No duplicate requests or accounts | `SaleRequestTests`, `PhoneAuthTests` |
| 4 | Seller and buyer review: approve, reject, completion, log, server permissions | `SaleReviewTests`, `BuyerRequestTests` |
| 5 | Approve ≠ prepare ≠ owner confirm ≠ publish. The owner can't publish or change approved data | `OpportunityTests` |
| 6 | Private documents hidden from visitors and buyers. Photos and allowed location work | `MarketAccessTests` |
| 7 | Search separates due-now / price / total. Unknown ≠ 0 | `SearchTests` |
| 8 | Calculation examples correct. Wording differs when incomplete | `MarketCalculatorTests` |
| 9 | The map stores the real location. Google Maps opens the allowed coordinates without leaking precision | `OpportunityTests` (public DTO) |
| 10 | Interest saved, linked, visible to the team, and doesn't reserve | `InterestTests` |
| 11 | Mobile, RTL, loading, empty and failure states | Playwright `market-journey.spec.ts` + manual 390 check |

## Findings

Verified on 2026-10-01:
- `dotnet test`: 254 passed. This covers the archived suite with the legacy flag on, the calculator examples, the catalog rules, the `MarketTests` end-to-end API journeys and `LegacyGateTests`. It also covers `CurrentModelTests`: the default flag-off configuration on a fresh database, photo metadata stripping, and exact location only with the owner's explicit choice.
- `tsc`, `eslint` and `next build` pass.
- Playwright `market-journey.spec.ts`: 14 passed. It covers the full UI journey, old routes answering 404, and no horizontal scroll at 390px on 10 public pages, 7 owner/buyer pages and 7 team pages. The 13 archived specs were skipped.
- A manual browser pass covered the wizard, the follow-up file, the map, team review and the opportunity editor.

- **Design decisions taken under the brief:**
  - Old model gated, not deleted (reversible, no data loss).
  - Mobile-only sign-in reuses the account of an existing mobile.
  - Field rules served by the API.
  - One server calculator called by the UI.
  - The opportunity terms are versioned, and a published version never changes in place.
  - Public location is the centre of a ~1 km cell unless the owner agreed to exact.
- **Things the app refuses on purpose** (seen while testing):
  - Sending a summary without a description.
  - Publishing without a location, owner confirmation, the checklist, or accepted photos.
  - Publishing with a negative gap.
  - Saving two edits of the same opportunity at the same instant (409, "refresh").
- **Photos:** EXIF/XMP/IPTC metadata (e.g. phone GPS) is stripped on upload, so a photo can't reveal an exact location the listing shows as approximate.
- **Roles:** the role-permission sync runs with every migration (`dotnet run -- migrate` and migrate-on-startup), not only with the demo seed, so existing tenants get the `market.*` permissions in any environment.
- **Map:**
  - OpenStreetMap tiles need a Referer; the site's `same-origin` policy sends none, so the tile layer sets `strict-origin-when-cross-origin`.
  - Nominatim search found nothing for some Arabic district queries; the UI then says so and the pin is placed by hand (D3: production geocoder/tiles provider).
- **Demo data:** fictional and labelled «تجريبي». Photos are drawn illustrations. Parties end with «(تجريبي)».
- **Open decisions:** D1 SMS provider (blocking for real use), D2 commission policy, D3 map provider, D4 REGA/FAL advertising requirements per opportunity, D5 real party directory, D6 retention of the archived data, D7 object storage and malware scanning.
- **Deferred to M2/M3 (not built):**
  - **M2:** map-area search and clustering, explained ranking beyond the fit reasons, comparison, saved searches and alerts.
  - **M3:** offers and negotiation, reservation, transfer checklists, closing and fees.
- **Deferred inside M1 for review:**
  - English UI: the current model is Arabic-only.
  - Editing an obligation's party after submission.
  - Team messaging to the owner beyond completion requests, notes and the visible log.
