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
| 1 | Public site: new home, navigation (الرئيسية، بيع عقارك، الفرص المتاحة، كيف نعمل، الحاسبات، تواصل معنا، حسابي), how-it-works, contact, legal drafts, SEO | 🟩 (contact form needs the step 3 API) |
| 2 | Mobile-first sign-in (reuses the account by mobile) and `/account` | ⬜ |
| 3 | Market backend: field catalog, sale requests and obligations, draft autosave, submit, private documents and listing photos | ⬜ |
| 4 | Calculator (developer / financier / buyer capacity / opportunity cost) + public endpoints + verbatim tests of the 3 examples | ⬜ |
| 5 | Seller wizard (3 steps, device draft before sign-in) and completion file (4 groups, map picker, photos, documents) | ⬜ |
| 6 | Team review of sale requests (assign, completion request, approve/reject with reason, figure verification with source and date, external approval log) | ⬜ |
| 7 | Opportunity preparation, terms versions, owner confirmation, publish / pause / withdraw | ⬜ |
| 8 | Public listing: cards, server-side search and pagination (city, type, due now, installment), details page, map with approximate location | ⬜ |
| 9 | Buyer request (3 steps), team review, status and suggested opportunities with reasons, edit preferences | ⬜ |
| 10 | Interest linked to the opportunity, the buyer profile and the terms version; team follow-up; saved opportunities | ⬜ |
| 11 | Calculators page (3 calculators) | ⬜ |
| 12 | E2E journey + mobile 390 checks, docs, handoff | ⬜ |

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

(Filled in as the steps are done.)
