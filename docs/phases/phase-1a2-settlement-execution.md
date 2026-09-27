# Phase 1A-2: After the outcome (agreement → payments → closure) ▶ CURRENT (steps 0–1 done 2026-09-27)

**Start only after Phase 1A (MVP) is done.** **Q13 decided (2026-09-25):** this phase is **not** part of the first MVP.

> **Origin:** this was steps 3–4 of the MVP before the 2026-09-25 re-plan, when the MVP was the lender's settlement path. Rahoon doesn't collect money. Payments happen outside the platform (A-04), and in-platform acceptance is a consent record, not a signature (A-05).
>
> **Re-planned 2026-09-27 (step 0).** The earlier steps 1–4 were all lender-on-platform work (L19–L21, the B10 merge, L26), which contradicted this file's own direction review (manual-coordination mode first). The manual-mode steps now come first ([ADR 0002](../adr/0002-request-execution-tracking.md)). The lender-mode steps follow, unchanged in content.

**Goal:** once the individual has accepted an approved offer, the outcome is **tracked** and documented for them:
- the agreement
- the installment schedule they can follow
- payment confirmations
- breach handled without any automatic action against them
- closure, with closure documents available to them

The lender executes all of this; Rahoon tracks and explains.

## Direction review

- **Q13 decided:** agreement activation, payments and financial closure stay in this phase, not in the MVP. Rahoon **doesn't hold customer funds or execute payments**.
- **Q1/Q8 decided:** in the MVP the lender isn't on the platform, and the Rahoon team coordinates over a documented manual channel. This phase therefore has two modes:
  1. **Manual-coordination mode (first, steps 1–4).** The Rahoon team records the lender's agreement, installment schedule, payment confirmations, notices and closure letters **as evidence from the lender** (source document + date), and a second member verifies each record. The individual can also report a payment with proof. Rahoon only tracks and explains; it never marks money as received on its own authority.
  2. **Lender-on-platform mode (later, steps 5–8).** The existing L19–L21 and L26 screens and backend (maker-checker payments, reconciliation, closure) are used when a lender works inside Rahoon.
- **Product questions (added 2026-09-27, answered the same day; `product-direction.md` §6, ADR 0002 §7):**

  | Open item | Affects | Interim rule |
  |---|---|---|
  | **Q16** tracking horizon: **decided 2026-09-27** | Steps 1–3 | Track the agreement, the lender schedule, payments actually reported or confirmed, and closure. No duty to monitor every installment. Outcome `tracking_ended` ends tracking with a summary |
  | **Q17** after a breach: **decided 2026-09-27** | Steps 1–2 | The breach stays on the same request, with the reason recorded; coordination continues there |
  | **Q18** closure documents: **rule decided 2026-09-27**; exact P1/P2 list open (**V13**) | Steps 1–3 | Completion needs a verified closure document relevant to the path. Interim mapping: P1 → rescheduling/agreement confirmation letter; P2 → clearance or mortgage release; «أخرى» never completes |
  | V4 wording | Steps 2–3 | Withdrawal during tracking, «لم تتخذ رهون أي إجراء», payment-report confirmation: proposed text, labelled |
  | P3 sale execution | Step 1 | Out of this phase (Phase 2). An accepted P3 offer is closed with `offer_accepted` as today |

### Reuse decisions (step 0)

| Existing piece | Built for | Manual mode | Lender mode |
|---|---|---|---|
| Offer record/verify pattern (T05/T06, `/team/verify`, step-up, verifier ≠ recorder) | 1A manual | **Reuse** the pattern and the verify screen for execution records | — |
| D10 owner payments (`/owner/payments`) | owner-case session (X4) | **Components only** (`InstallmentList`, «رهون لا تستلم أي مبالغ», the payment-notice form) behind a request adapter | Route kept as is |
| D14 owner closure documents (`/owner/documents/closure`) | owner-case session | **Components only**. The «read-only for 90 days» assumption doesn't apply to the individual account (Q14); documents stay | Route kept |
| L19 agreement, L20 payments (maker-checker), L21 breach | lender tenant | Not reused (they model the lender's internal work) | Steps 5 and 8 |
| L26 reconciliation + closure, B10 closure API | lender tenant | Not reused | Steps 6–7 |
| S08 notifications, S09 tasks (WIP `05c715b`) | both | Not needed | Stay with Phase 1B |

## Definition of done

- [ ] **Manual mode:** Playwright extends `owner-journey.spec.ts`: accept → relay → start tracking → agreement recorded and verified → schedule visible to the individual → the individual reports a payment → the lender confirmation is recorded, verified and linked → a lender notice is explained → a closure letter is verified → closed with `executed_closed` → the individual downloads the closure documents.
- [ ] The individual sees every state change in plain language, with the source of each fact. They never see internal notes, unverified records or a Rahoon deadline.
- [ ] **Lender mode:** L19–L21 re-verified; the B10 backend merged and migrated; L26 built; the lender-mode Playwright path (activation → matched payment in D10 → closure → D14) passes.
- [ ] `dotnet test` is green.
- [ ] Every row below is ✅, with *Direction review* OK or Secondary (lender mode). A row still awaiting design (D-7, D-8) counts when «بانتظار اعتماد التصميم» is recorded in *Findings*, as in Phase 1A.

## Demo cast

**Mode 1, manual coordination (steps 1–4):**

| Who | Login | Does |
|---|---|---|
| Rahoon coordinator | `n.alyami@team.rahoon.example` (or `t.alshehri`) | starts tracking, records the lender's agreement, schedule, confirmations, notices and closure letters; answers payment reports; closes |
| Rahoon verifier | `a.alqahtani@team.rahoon.example` | verifies each execution record (≠ recorder, step-up) |
| Rahoon lead | `l.alharbi@team.rahoon.example` | sees every request; holds both permissions but still can't verify their own record |
| The individual | seeded in step 1 (an accepted, relayed P1 offer), or the live demo request | follows the agreement and schedule, reports a payment with proof, receives the closure documents |

**Mode 2, lender on platform (steps 5–8), existing cast:**

| Who | Login | Does |
|---|---|---|
| ماجد الحربي (legal) | m.alharbi@alufuq.example | agreement legal review and activation |
| ريم الدوسري (finance, maker) | r.aldosari@alufuq.example | records payments |
| عبدالعزيز الشمري (finance, checker) | a.alshammari@alufuq.example | matches payments (≠ recorder) |
| نورة الشهري (approver) | n.alshehri@alufuq.example | reconciliation or closure approval (step-up) |
| The invited owner | owner sign-in | follows payments (D10) and receives closure documents (D14) |

## Scope and status

| ID | Screen / capability | Mode | Tech status | Direction review | Notes |
|---|---|---|---|---|---|
| — | `execution_tracking` state, execution records, payment reports, new close outcomes (ADR 0002) | manual | 🟩 2026-09-27: API + seed + tests (`ExecutionTests` 12), migration applied on Postgres; seeded requests render on `/my` (Playwright at 390/1440) and the team detail (Chrome at 1440); 768 not checked | New | Step 1. No dedicated screens yet (steps 2–3) |
| T02 «التنفيذ», T09, T10, T11, T06 extension | Team execution screens | manual | ⬜ | New + **Needs design (D-7)** | Step 2 |
| E01–E06 | The individual's execution view under `/my/requests/[ref]` (D10/D14 successors) | manual | ⬜ | New + **Needs design (D-8)** | Step 3 |
| L19 | Agreement (legal review → schedule → activate) | lender | 🟩 on `master` (`ca4aefb`, 1A step 3); browser smoke only | Secondary | Step 5 |
| L20 | Payment schedule, record (maker), match (checker) | lender | 🟩 same | Secondary | Step 5 |
| L21 | Breach handling (no automatic referral) | lender | 🟩 same | Secondary | Check the owner-facing wording «نتواصل معك أولاً»: a commitment (V4); make it descriptive or mark it provisional |
| D10 | Owner payments and receipts | lender | 🟩 on `master` (1A step 3) | Secondary | «رهون لا تستلم أي مبالغ». Components reused in E01/E02 |
| L26 | Reconciliation + closure (settlement path) | lender | ⬜ UI · 🟨 API on branch `…ae86d4e3…` | Secondary | Steps 6–7 |
| D14 | Owner closure documents | lender | 🟩 on `master` (1A step 3) | Secondary | Owner access read-only for 90 days (assumption, case session only). Components reused in E05 |
| S08, S09 | Notifications, tasks | both | 🟧 WIP `…a1008744…` @ `05c715b` | — | Stay in Phase 1B (not needed here) |

## Steps

0. ✅ **Re-plan for manual-coordination mode first** (2026-09-27, docs only).
   - [x] Reuse decisions (table above): D10/D14 are bound to the owner-case session, so only their components carry over.
   - [x] [ADR 0002](../adr/0002-request-execution-tracking.md): `execution_tracking` state, lender-evidenced execution records with second-member verification, the individual's payment reports, new close outcomes. ADR 0001 amendment row.
   - [x] Design requests D-7 (team) and D-8 (individual): [`docs/design-requests/1a2-step0-design-requests.md`](../design-requests/1a2-step0-design-requests.md).
   - [x] Q16–Q18 recorded as OPEN in `product-direction.md` §6, with interim rules.
1. ✅ **Execution tracking backend (manual mode)** (2026-09-27).
   - [x] `RequestStatus.ExecutionTracking` («قيد متابعة التنفيذ», «ننتظر» = lender by default); transition `start_execution_tracking` (guards: consent, relayed, `accepted_trackable_offer`); `close` from tracking with `executed_closed` / `tracking_ended` / `agreement_ended_by_lender` / `other` only; `offer_accepted` refused whenever the latest response accepts a P1/P2 offer (audited guard failure); `continue_coordination` from tracking with a required reason shown to the individual (Q17); withdrawal wording during tracking.
   - [x] `RequestExecutionRecord` (agreement · payment_confirmation · lender_notice · closure_document), `RequestScheduleItem`, `RequestPaymentReport`; applicant document kind `payment_proof`; permissions `request.execution_record` (coordinator, lead) / `request.execution_verify` (verifier, lead); DB check verifier ≠ recorder; every id in a body is checked against the same request.
   - [x] Endpoints: `POST /api/team/requests/{ref}/execution/start`, `…/execution/records`, `…/execution/records/{id}/verify` (step-up on publish), `…/payment-reports/{id}/note`, `GET /api/team/verify/execution`; `POST /api/my/requests/{ref}/payment-reports`. Team update (`/updates`) takes an optional «ننتظر» (lender/applicant) during tracking. Team and individual detail DTOs gain `execution`; the individual's has published records only, the current agreement's schedule with a state only from a lender confirmation or their own report, no team identities and no date-derived overdue state.
   - [x] Seed: REQ-2026-00308 (accepted, relayed P1; ready to start), REQ-2026-00309 (tracking: agreement + 12-row schedule, installment 1 confirmed, installment 2 reported with its confirmation awaiting عبير), REQ-2026-00310 (P2 closed `executed_closed` with clearance and release letters). REQ-2026-00306 stays `offer_accepted` (historical). The live demo continues from REQ-2026-00311. README logins updated.
   - [x] Tests: `ExecutionTests` (12, ADR 0002 §6 items 1–8, including idempotent record/verify replay and the audit chain after a blocked verify) and the step-7 accept test now expects tracking. Backend **177/177**. Migration `RequestExecutionTracking`. Web: `execution_tracking` added to the status maps (the individual chip indexed the map with no fallback and would have crashed) and three outcome labels; tsc, eslint, build ✓. E2E 7 passed, 1 skipped (known `fixme`); `owner-journey.spec.ts` now asserts that closing an accepted P1 offer is refused with the reason.
2. ⬜ **Team execution screens (D-7).** T02 «التنفيذ» tab, T09, T10, T06 extension (verify queue lists execution records), T11; the close dialog gets the new outcomes. «بانتظار اعتماد التصميم» if D-7 isn't designed.
3. ⬜ **The individual's execution view (D-8).** E01–E06 on the tracker, reusing the D10/D14 components; report a payment with proof; lender notice with messages/objection; closure documents with no expiry; withdrawal wording. «بانتظار اعتماد التصميم» if D-8 isn't designed.
4. ⬜ **Manual-mode E2E + responsive QA.** Extend `owner-journey.spec.ts` (the DoD path above; the current accept → close test becomes accept → tracking → closure), add the execution screens to `responsive-qa.spec.ts`, negative checks (another individual → 404, verifier = recorder refused, close without closure document refused, double submit replays). README demo script and logins.
5. ⬜ **Lender mode: agreement → payments** (was step 1).
   - ماجد: L19 legal review → schedule → activate; guard reasons are shown when blocked.
   - ريم records installment 1 and عبدالعزيز matches it; the same user can't match their own payment.
   - The owner sees it in D10.
   - Breach: seeded overdue case → L21 outcome.
   - Seed fixes: RH-2026-003870 («تسوية معتمدة / نشطة» with no agreement or schedule); RH-2026-004172 v2 awaiting review, so the `fixme` maker-checker test in `lender-flow.spec.ts` runs again.
6. ⬜ **Lender mode: closure backend** (was step 2). Merge `worktree-agent-ae86d4e3d70e6413a` (B10/B11 API), add the migration `ReferralClosureAnalytics`, run `dotnet test` and `bash scripts/dev-api.sh --reset`.
7. ⬜ **Lender mode: L26 settlement-path UI** (was step 3).
   - Reconciliation: preparer, reviewer and approver are three different people; step-up.
   - Closure documents checklist; closure request → decision.
   - D14 for the owner.
   - Fast demo: seeded RH-2026-003702.
8. ⬜ **Lender mode: E2E + wrap-up** (was step 4). Lender-mode path in `lender-flow.spec.ts`, negative checks (other tenant → 404, forbidden transitions show reasons, double submit replays). Tick the Definition of done and move ▶ CURRENT to Phase 1B.

## Findings / open decisions

- **Step 0 (2026-09-27), the close path changes:** today the team closes an accepted offer straight away (`offer_accepted`, E2E `owner-journey.spec.ts` «T07: relay, then close», seed REQ-2026-00306). From step 1, tracking is **mandatory** for an accepted P1/P2 offer: `close` with `offer_accepted` is refused for it (ADR 0002 §4.1), and `tracking_ended` ends tracking early. The E2E test is updated in step 4, and `offer_accepted` stays valid for existing closed requests and for P3.
- **Step 0, dates vs Q6/Q12:** installment due dates are shown only as the lender's stated schedule with its source («بحسب جدول جهتك الممولة»), with no countdown, reminder or Rahoon-driven «متأخر». Precedent: 1A step 9 showed lender-mode deadlines as the lender's condition.
- **Step 1 (2026-09-27), for step 2:**
  - The team screen offers «متابعة التنسيق» from tracking (it is in `TeamRequestView`'s handled actions) but its dialog has no reason field; from tracking the API answers 400 until step 2 adds the field.
  - The close dialog lists the pre-tracking outcomes for every state; step 2 shows the tracking outcomes only in `execution_tracking` (the API already refuses mismatches).
  - The execution verify queue is a separate endpoint (`/api/team/verify/execution`) so the step-7 offer queue keeps its shape; step 2 merges both into «بحاجة إلى تحقق».
  - «بدء متابعة التنفيذ» has no button yet; the E2E asserts the refusal of the old close instead and is extended in step 4.
- **Step 1, document kinds vs ADR 0002 §4.6:** every lender document (agreement letter, confirmation, notice, closure letter) uses the existing team kind `lender_letter` («خطاب الجهة الممولة»); only the applicant kind `payment_proof` («إثبات سداد») is new. Step 2's upload UI follows this, not the §4.6 wording.
- **Step 1, README demo script:** step 10 now says an accepted P1/P2 offer is tracked (the old close is refused); the script is rewritten with the tracking path in step 4.
- **Step 1, scope kept to ADR 0002:** `request_info` is **not** available during tracking (not in the ADR table); the team uses messages or a team update with «ننتظر: العميل».
- **Step 1, local dev:** the API and web dev servers from the previous session were stopped for the build and restarted (`dev-api.sh --reset`, `npm run dev`); `dev-api.sh` prints `pkill: command not found` on Windows (harmless).
- **Step 0, D14's 90-day window** came from the owner-case session. For the individual account (Q14) closure documents stay available; the lender-mode D14 keeps its assumption until step 7.
- The approvals inbox lists only solution approvals. Referral, reconciliation and closure approvals notify only. Decide in step 7 whether closure approvals belong in the inbox or on a case-level screen.
- The owner summary PDF generated at closure is Latin-only (no Arabic shaping); listed as outstanding.
