# Phase 4: Optimization and decision support (B11) ⬜

**Start only after Phase 3 is done.**

**Goal:**
- Portfolio analytics and bottleneck views with explicit numerators and denominators.
- Configurable operations with maker-checker.
- Drafting help: deterministic templates, approved by a person before use.
- Predictive insights, each with its provenance, limitations and a human review.
- Nothing binding is decided automatically, and no accuracy figures are fabricated.

The backend is 🟨 on branch `worktree-agent-ae86d4e3d70e6413a` and is merged in Phase 1A-2 step 2.

## Direction review (added 2026-09-25, `docs/product/product-direction.md`)

- **Q6 decided:** processing times may be tracked **internally** and are never shown to the individual as a commitment. The analytics here are for the Rahoon team and, later, lenders.
- **O01/O02 were designed as institution-facing.** Add Rahoon-team request metrics (the intake mechanism is the Rahoon team, Q1):
  - requests received
  - response time
  - accepted / declined, with reasons
  - time from request to first proposal
- **Individual-facing value.** Consider what, if anything, analytics should show the individual (e.g. typical steps). Nothing predictive ever reaches the owner; that rule is unchanged.
- **O05 predictive insights** stay decision support for staff only, with provenance, limitations and human review.

## Definition of done

- [ ] Every row below is ✅.
- [ ] Every insight shows its source, method (the ADH-v1.2 heuristic, a 0–100 score, *not* a probability), limitations and the override-with-reason path.
- [ ] Insights never reach owner screens and never change case state (there's an API test; add a UI check).

## Scope and status

| ID | Screen | Status |
|---|---|---|
| O01 | Portfolio analytics | ⬜ UI · 🟨 API `/api/analytics/portfolio` |
| O02 | Trends and bottlenecks + insight feedback | ⬜ UI · 🟨 API |
| O03 | Configurable operations (change requests) | ⬜ UI · 🟨 API `/api/settings/operations` |
| O04 | Drafting help (template filling, human approval) | ⬜ UI · 🟨 API `/api/cases/{ref}/drafts` |
| O05 | Predictive insights + model governance page | ⬜ UI · 🟨 API |

## Steps

1. ⬜ O01 + O02 (charts follow the dataviz rules: accessible, RTL, with a text alternative).
2. ⬜ O03.
3. ⬜ O04 + O05 with governance and limitations.
4. ⬜ Playwright + responsive QA; final pass over the implementation map, README and deviations.

## Known gaps (from the backend build)

- There's no comparison with the previous period in O01, and no work-versus-wait split in O02; both need historical data.
