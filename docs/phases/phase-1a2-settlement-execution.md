# Phase 1A-2: After the outcome (agreement → payments → closure) ⬜

**Start only after Phase 1A (MVP) is done.** Whether this phase is part of the MVP release is **OPEN (Q13)** in `docs/product/product-direction.md`.

> **Origin:** this was steps 3–4 of the MVP before the 2026-09-25 re-plan, when the MVP was the lender's settlement path. The work and its branches are unchanged; only the order moved. Rahoon doesn't collect money. Payments stay recorded manually with maker-checker (A-04), and signing stays «غير مفعّل» (A-05).

**Goal:** once the individual has accepted an approved proposal, the outcome is carried through and documented for them:
- the lender's legal review and activation of the agreement
- the installment schedule the individual can follow
- payments recorded and matched by two different people
- breach handled without automatic referral
- reconciliation and a documented closure, with closure documents available to the individual

## Definition of done

- [ ] Every row below is ✅ with *Direction review* OK.
- [ ] Playwright extends the owner journey: accept → activation → a matched payment visible to the individual (D10) → closure → closure documents (D14).
- [ ] The individual sees every state change in plain language and never sees internal notes.
- [ ] `dotnet test` is green, with the B10 backend merged and migrated.

## Demo cast

| Who | Login | Does |
|---|---|---|
| ماجد الحربي (legal) | m.alharbi@alufuq.example | agreement legal review and activation |
| ريم الدوسري (finance, maker) | r.aldosari@alufuq.example | records payments |
| عبدالعزيز الشمري (finance, checker) | a.alshammari@alufuq.example | matches payments (≠ recorder) |
| نورة الشهري (approver) | n.alshehri@alufuq.example | reconciliation or closure approval (step-up) |
| The individual from Phase 1A | their own sign-in | follows payments (D10) and receives closure documents (D14) |

## Scope and status

| ID | Screen | Tech status | Direction review | Where |
|---|---|---|---|---|
| L19 | Agreement (legal review → schedule → activate) | 🟨 branch `…a1008744…` @ `36d316a` (merged in 1A step 2) | OK | |
| L20 | Payment schedule, record (maker), match (checker) | 🟨 same | OK | |
| L21 | Breach handling (no automatic referral) | 🟨 same | OK | Check the owner-facing wording: «نتواصل معك أولاً» |
| D10 | Owner payments and receipts | 🟨 branch `…a1bd0510…` | OK | «رهون لا تستلم أي مبالغ» |
| L26 | Reconciliation + closure (settlement path) | ⬜ UI · 🟨 API on branch `…ae86d4e3…` | OK | |
| D14 | Owner closure documents | 🟨 branch `…a1bd0510…` | OK | Owner access stays read-only for 90 days (assumption) |
| S08, S09 | Notifications, tasks | 🟧 WIP `…a1008744…` @ `05c715b` | OK | May move to 1B |

## Steps

1. ⬜ **Agreement → payments.**
   - ماجد: L19 legal review → schedule → activate; guard reasons are shown when blocked.
   - ريم records installment 1 and عبدالعزيز matches it; the same user can't match their own payment.
   - The individual sees it in D10.
   - Breach: seeded overdue case → L21 outcome.
2. ⬜ **Closure backend.** Merge `worktree-agent-ae86d4e3d70e6413a` (B10/B11 API), add the migration `ReferralClosureAnalytics`, run `dotnet test` and `bash scripts/dev-api.sh --reset`.
3. ⬜ **L26 settlement-path UI.**
   - Reconciliation: preparer, reviewer and approver are three different people; step-up.
   - Closure documents checklist; closure request → decision.
   - D14 for the individual.
   - Fast demo: seeded RH-2026-003702.
4. ⬜ **E2E + responsive QA.** Extend `web/e2e/owner-journey.spec.ts`, then the negative checks: other tenant → 404, forbidden transitions show reasons, double submit replays.

## Findings / open decisions

- The approvals inbox lists only solution approvals. Referral, reconciliation and closure approvals notify only. Decide in step 3 whether closure approvals belong in the inbox or on a case-level screen.
- The owner summary PDF generated at closure is Latin-only (no Arabic shaping); listed as outstanding.
