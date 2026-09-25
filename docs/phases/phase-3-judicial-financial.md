# Phase 3: Judicial referral, agent portal, full financial closure (B10) ⬜

**Start only after Phase 2 is done.**

**Goal:**
- A separately approved judicial referral with an evidence pack.
- The external reference and official status are stored **exactly as entered** (no invented court API).
- A sales-agent portal limited to the agent's own assignments.
- Full reconciliation, distribution waterfall and closure with traceable sources.
- A case never goes to referral automatically, and not every case ends in foreclosure.

The backend is 🟨 on branch `worktree-agent-ae86d4e3d70e6413a`. It gets merged with a migration in Phase 1A-2 step 2, because the settlement-path closure needs it (see `docs/progress/backend-B10-B11.md` on that branch).

## Direction review (added 2026-09-25, `docs/product/product-direction.md`)

- **On hold (V10), 2026-09-25:** judicial referral isn't among the four confirmed help paths (Q11). P4 «الإحالة لمختص مناسب» means referral to a suitable specialist, not judicial referral. Nothing in this phase is built until the product owner confirms scope.
- **Scope check first (Q11, Q8).** In an individual-first product, confirm with the product owner whether Rahoon still coordinates judicial referral and sale-agent work at all. If it does, confirm it stays strictly a separately approved lender action, triggered only after documented notice and the objection period, and never automatic.
  - The backend exists and stays; building its UI waits for this confirmation.
- **The individual's view.** Whatever remains must tell the individual, in plain language, what is happening, their rights and how to object. It must show the official status exactly as entered, and nothing before the notice.
- **Full financial closure (F01–F04)** extends the settlement-path closure built in Phase 1A-2.

## Definition of done

- [ ] Every row below is ✅.
- [ ] Playwright: legal requests referral → a different approver decides (step-up) → external reference recorded → agent submits a result → legal confirms → reconciliation → distribution → closure.
- [ ] The owner sees only what the rules allow: nothing before the notice, then the official status verbatim and their rights.

## Scope and status

| ID | Screen | Status |
|---|---|---|
| J01–J03 | Legal readiness, export and matching, external reference and official status | ⬜ UI · 🟨 API |
| J04 | Exceptions queue | ⬜ UI · 🟨 API |
| J05–J07 | Agent portal: assigned cases, plan, updates, result and evidence | ⬜ UI · 🟨 API |
| F01–F04 | Full reconciliation, approved distributions, release/clearance docs, traceable closure | ⬜ UI · 🟨 API (the settlement-path subset is built in 1A-2 step 3) |
| PA18 | Integrations and their states | ⬜ UI · 🟨 API |

## Steps

1. ⬜ J01–J03 (building on L25 from Phase 1B).
2. ⬜ J05–J07 agent portal (ياسر الحمدان, y.alhamdan@agent-j.example).
3. ⬜ F01–F04 (extends the Phase 1A-2 closure UI with the distribution waterfall).
4. ⬜ J04 + PA18.
5. ⬜ Playwright + responsive QA; update the implementation map.

## Seeded data

- RH-2026-003511: in judicial referral; the agent reported 760,000 − 22,800 and it isn't confirmed yet.
- RH-2026-003702: reconciliation at 455,210.75, difference 0.

## Findings / open decisions (from the backend build)

- Fee order: sale costs → debt → other approved fees → owner surplus; any shortfall stays with the lender.
- Money figures come only from sale results legal has confirmed.
- The evidence-pack export is an audited GET with side effects, like document downloads.
- There's no "request update now": the judicial channel is unavailable.
