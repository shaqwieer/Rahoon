# Phase 2: Voluntary sale (B8) + service ecosystem (B9) ⬜

**Start only after Phase 1C is done.**

**Goal:**
- A controlled voluntary sale: owner consent, compliance-reviewed listing, broker with scoped access, offers compared, a maker-checker approval, and tracking through to reconciliation.
- The provider directory, onboarding and licences, invoices, performance, workflow designer, advanced reports and billing.
- **No public marketplace.** Licensed signing and payment (X01/X02) stay *conditional*, showing «غير مفعّل» until a real, authorized integration exists.

The backend is ✅ on `master` (merged 2026-09-24, `be3b7e3`; see `docs/progress/backend-B8-B9.md`).

## Definition of done

- [ ] Every row below is ✅.
- [ ] Playwright: owner requests a sale → approval → listing reviewed → broker offer → approval → completion reaches «بانتظار التسوية المالية».
- [ ] Brokers and buyers never see owner, case, lender, minimum-price or debt data (the tests already assert this on the API; add a UI check).

## Scope and status

| ID | Screen | Status |
|---|---|---|
| L27–L33 | Sale decision, owner consent, file prep, listing and disclosure, broker assignment, offer comparison, approval and tracking | ⬜ UI · 🟩 API |
| D15, D16 | Owner: sale explanation and consent, sale progress | ⬜ UI · 🟩 API |
| — | Broker view `/api/broker/sales/{VS-ref}` | ⬜ UI · 🟩 API |
| V05–V07 | Provider directory, onboarding and licence, delivery/invoice/performance | ⬜ UI · 🟩 API |
| PA14–PA17 | Workflow designer, advanced reports, provider registry, billing | ⬜ UI · 🟩 API |
| X01, X02 | Licensed signing and payment journeys, conditional | ⬜ UI · 🟩 API (returns `integration_unavailable`) |

## Steps

1. ⬜ L27 + D15 (owner consent with OTP) + L28.
2. ⬜ L29–L31 (file, listing with compliance review, broker assignment) + the broker view.
3. ⬜ L32–L33 + D16 (offers, maker-checker approval, tracking → reconciliation).
4. ⬜ V05–V07.
5. ⬜ PA14–PA17 + the X01/X02 «غير مفعّل» states.
6. ⬜ Playwright + responsive QA; update the implementation map.

## Seeded data for this phase

- RH-2026-004012: at the sale-decision step.
- RH-2026-003944: offers stage.
- Sale references VS-2026-0031 and VS-2026-0027; broker assignment ASG-2026-0873.
