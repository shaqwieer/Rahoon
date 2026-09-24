# Phase 1A: MVP (settlement path end to end) ▶ CURRENT

**Goal:** a lender and a property owner can take **one real case** all the way through the platform: creation → documents and valuation → solution → internal approval (maker-checker) → owner accepts the offer → agreement activated → installments recorded and matched → reconciliation → **documented closure**. Every step uses real persisted data, server-side rules and audit.

**Out of MVP** (later phases): public landing/demo form, staff invitations, search/help pages, bulk import, voluntary sale, judicial referral, provider/admin/platform portals, analytics.

## Definition of done

- [ ] Every screen marked *must* below is ✅ (built, merged into `master`, browser-checked at 1440, 768 and 390).
- [ ] Playwright covers the full path, and a separate owner (debtor) flow: `cd web && E2E_RESET=1 npx playwright test` is green.
- [ ] `dotnet test` and the web typecheck, lint and build are all green on `master`.
- [ ] README has the "MVP demo script": who logs in, in what order, and what they click.
- [ ] Tag `mvp-1` on `master`.

## Demo cast for the MVP (password `Rahoon-Demo-2026!`, SMS code shown on screen)

| Who | Login | Does |
|---|---|---|
| سارة القحطاني (case manager) | s.alqahtani@alufuq.example | creates the case, reviews and submits solutions, talks to the owner |
| فهد العتيبي (analyst) | f.alotaibi@alufuq.example | prepares solution versions, valuation and analysis |
| نورة الشهري (approver) | n.alshehri@alufuq.example | approves or returns solutions (step-up) |
| ماجد الحربي (legal) | m.alharbi@alufuq.example | agreement legal review and activation |
| ريم الدوسري (finance, maker) | r.aldosari@alufuq.example | records payments |
| عبدالعزيز الشمري (finance, checker) | a.alshammari@alufuq.example | matches payments (≠ recorder) |
| Owner عبدالله محمد السبيعي | `/invite/demo-RH-2026-004172`, ID `1098734542` | views the offer, accepts it with OTP, follows payments |

## Scope and status

| ID | Screen | Must/Should | Status | Where |
|---|---|---|---|---|
| L01, L02, L03, L05 | Portfolio, case list, wizard, workspace | must | ✅ | master |
| L06–L09 | Parties, finance, property and mortgage tabs | must | 🟨 | branch `…a3939fc4…` |
| L10 | Documents and requests | must | 🟨 | branch `…a3939fc4…` |
| L11/L12 | Valuation and analysis | must | 🟨 | branch `…a3939fc4…` |
| L13–L17 | Solutions, compare, submit, approvals, owner preview | must | ✅ | master |
| L18 | Negotiation (counteroffer handling) | must | 🟨 | branch `…a1008744…` @ `36d316a` |
| L19 | Agreement (legal review → schedule → activate) | must | 🟨 | branch `…a1008744…` @ `36d316a` |
| L20 | Payment schedule, record (maker), match (checker) | must | 🟨 | branch `…a1008744…` @ `36d316a` |
| L21 | Breach handling | should | 🟨 | branch `…a1008744…` @ `36d316a` |
| L22 | Case comms and tasks | should | 🟨 | branch `…a3939fc4…` |
| L24 | Case audit timeline + chain verification | should | 🟨 | branch `…a3939fc4…` |
| L26 | Reconciliation + closure (settlement path only) | must | ⬜ UI · 🟨 API on branch `…ae86d4e3…` | — |
| D01 | Owner invitation + identity check | must | 🟩 | master |
| D02, D07, D09, D10 | Owner home, offer, accept (OTP consent), payments | must | 🟨 | branch `…a1bd0510…` |
| D04, D12 | Owner documents, messages | must | 🟨 | branch `…a1bd0510…` |
| D03, D05, D06, D08, D11, D13, D14 | Journey, debt, options, counteroffer, help, complaints, closure docs | should | 🟨 | branch `…a1bd0510…` |
| S08, S09 | Notifications, tasks | should | 🟧 WIP | branch `…a1008744…` @ `05c715b` |
| L23 | Complaints (reviewer) | should | 🟨 | branch `…a3939fc4…` |

## Steps (one session each, in order)

### Step 1: Merge the finished UI branches ⬜
- [ ] Merge `worktree-agent-a3939fc4d7a8b5a97` (case tabs, comms, complaints, audit, import, cancel).
- [ ] Merge `worktree-agent-a1bd05103bd12a217` (owner portal).
- [ ] Cherry-pick **only** `36d316a` from `worktree-agent-a10087449fcbd8672` (L18–L21). Leave the WIP commit `05c715b` for Phase 1B.
- [ ] Run the web typecheck, lint and build; fix conflicts (expect `lib/api/lender.ts`, `OverviewActions.tsx`, the i18n dictionaries).
- [ ] Fix the leftover: breadcrumb shows raw `new`.
- [ ] Browser: open every case tab for RH-2026-004172 as سارة; note defects in *Findings* below.

### Step 2: Owner journey in the browser ⬜
- [ ] Sara submits v2 and Noura approves (done once on 2026-09-24; reseed first with `bash scripts/dev-api.sh --reset`).
- [ ] Owner: `/invite/demo-RH-2026-004172` → identity → OTP → D02 home → D07 offer → D09 accept with OTP.
- [ ] Check the case moved to «بانتظار العميل» → agreement pending, and that the owner never sees internal notes.
- [ ] Owner counteroffer (D08) → lender negotiation (L18) → decline with reason → back to «حل مقترح».
- [ ] Mobile 390 pass on every owner screen (the owner portal is mobile-first).

### Step 3: Agreement → payments ⬜
- [ ] ماجد: L19 legal review → create schedule → activate (guard reasons shown when blocked).
- [ ] ريم records installment 1 (L20 drawer); عبدالعزيز matches it; the same user can't match their own payment.
- [ ] The owner sees the payment in D10.
- [ ] Breach: run the monitor (or use the seeded overdue case) → L21 outcome.

### Step 4: Reconciliation and closure (settlement path) ⬜
- [ ] Merge the backend branch `worktree-agent-ae86d4e3d70e6413a` (B10/B11 API). Then:
  - [ ] `dotnet ef migrations add ReferralClosureAnalytics`
  - [ ] `dotnet test` green
  - [ ] `bash scripts/dev-api.sh --reset`
- [ ] Build the L26 UI for the settlement path. Read `docs/progress/backend-B10-B11.md` (on that branch) for the routes.
  - [ ] Reconciliation: preparer, reviewer and approver are three different people; step-up.
  - [ ] Closure documents checklist.
  - [ ] Closure request → decision (`case.close`, step-up).
- [ ] Owner D14 shows the closure documents after closure. Owner access is read-only for 90 days.
- [ ] Seeded case RH-2026-003702 (already «بانتظار التسوية المالية») gives a fast demo path.

### Step 5: E2E + responsive QA ⬜
- [ ] Extend `web/e2e/lender-flow.spec.ts` with the full path: create → … → approve → owner accept → activate → payments → closure. Use the seeded cases where the path is long.
- [ ] Add `web/e2e/owner-flow.spec.ts` for the debtor flow: invite → identity → offer → accept; plus the counteroffer variant.
- [ ] Negative checks: another tenant gets 404, a forbidden transition shows reasons, a double submit replays the same response.
- [ ] Check all MVP screens at 1440, 768 and 390; fix layout, bidi and contrast issues.

### Step 6: MVP wrap-up ⬜
- [ ] README: MVP demo script and demo cast.
- [ ] Update `docs/design-implementation-map.md` statuses for the MVP screens.
- [ ] Deviations, blocked integrations (signing, payment and identity stay «غير مفعّل» or sandbox) and outstanding work → *Findings* below.
- [ ] Tag `mvp-1`.

## Already verified in this phase

- 2026-09-24: Sara submitted RH-2026-004172 v2 and Noura approved it in the browser with the step-up OTP. The case moved to «بانتظار العميل». Backend: 116/116 tests green on `master` (`be3b7e3`).

## Findings / open decisions

- **Cancellation wording** (from the B3/B5 branch): the backend does **not** revoke the owner's portal access on cancellation, so the cancel screen says so. If access should be revoked automatically, that's a product decision plus a backend change.
- **Approvals inbox** lists only solution approvals. Referral, reconciliation and closure approvals notify by task and notification only (B10 note). For the MVP, closure approvals need to be reachable. Decide in step 4 whether they go in the inbox or on a case-level screen.
- The owner summary PDF generated at closure is Latin-only (no Arabic shaping). Acceptable for the MVP; listed as outstanding.
