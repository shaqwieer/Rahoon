# Phase 2: Voluntary sale (B8) + service ecosystem (B9) ⬜

**Start only after Phase 1C is done.**

**Goal:**
- A controlled voluntary sale: owner consent, compliance-reviewed listing, broker with scoped access, offers compared, a maker-checker approval, and tracking through to reconciliation.
- The provider directory, onboarding and licences, invoices, performance, workflow designer, advanced reports and billing.
- **No public marketplace.** Licensed signing and payment (X01/X02) stay *conditional*, showing «غير مفعّل» until a real, authorized integration exists.

The backend is ✅ on `master` (merged 2026-09-24, `be3b7e3`; see `docs/progress/backend-B8-B9.md`).

## Direction review (added 2026-09-25, `docs/product/product-direction.md`)

- **Q11 decided (2026-09-25):** «البيع الرضائي عند تعذر الاستمرار» is confirmed help path P3. Rahoon coordinates the valuation, deals with the lender and rights holders, handles the sale procedures, and explains the proceeds and any remaining debt. It's a help path, **not a guaranteed sale**. Remaining checks before building L27–L33 and D15–D16:
  - B8 was designed with the lender driving the sale inside Rahoon. Re-plan it for the Rahoon team leading with manual lender coordination (Q1/Q8); lender-driven screens go to the lender-on-platform mode.
  - Terminology: «البيع الرضائي» (product owner) vs «البيع الطوعي» (design), V8.
  - Any action on the individual's behalf (listing, broker mandate) needs V1 first.
- It starts only from the individual's own preference or request (B13: «البيع الطوعي خيار يطرحه المالك فقط»), never as pressure.
- **What the individual gains.** D15 must answer the product question: what they gain, the risks, and that they can withdraw until an offer is accepted.
- **Ecosystem screens (V05–V07, PA14–PA17)** are institution- and platform-facing. Keep them, but after the MVP. Reconsider billing (PA17) once Q9 (is the service free for the individual?) is answered.

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
