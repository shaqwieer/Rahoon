# Phase 1A: MVP (the individual starts the request, the Rahoon team leads) ▶ CURRENT

> **Re-planned twice on 2026-09-25**, following `docs/product/product-direction.md`:
> 1. Individual-first direction. The MVP starts with the individual; settlement execution moved to [`phase-1a2-settlement-execution.md`](phase-1a2-settlement-execution.md).
> 2. Product-owner answers:
>    - **Q1/Q8:** the Rahoon team reviews, obtains documented consent and coordinates with the lender over a **documented manual channel**; the lender needs no account.
>    - **Q11:** four help paths.
>    - **Q6/Q12:** no deadlines or promises in the UI.
>    - **Q7/Q14:** several requests per account.
>    - **Q13:** execution stays in 1A-2.
>
> The file name is kept for continuity.

**Goal:** a struggling individual gets real help from منصة رهون, not just a form:
**enters Rahoon → submits a request → knows what Rahoon will do for them → follows the case study and the communication → sees an approved offer from their lender, if one exists → responds.**
- The Rahoon team leads the follow-up and the coordination.
- The lender decides its offer.
- The individual decides the response.
- The lender dashboard is **not** an entry point to the MVP.

**Product question for every screen:** «أنا لو عميل ومتعثر، ماذا أستفيد من المنصة؟ وماذا تقدم حلًا لمشكلتي؟»

## Product gate

**Decided** (`product-direction.md` §6): Q1, Q2, Q3, Q6 (UI), Q7, Q8, Q11, Q12 (UI rule), Q13, Q14.

**Still open.** Build with a visible, reversible assumption, or stop and ask:

| Open item | Affects | Interim rule |
|---|---|---|
| Q4 eligibility | Steps 5–6 | No eligibility screen; the Rahoon team reviews manually; no final `not_eligible` wording |
| Q5 required data | Step 5 | B13's minimum set, as a labelled assumption (contract number optional) |
| Q9 pricing | Step 4 | No «مجاني» anywhere |
| Q10 identity provider | Step 4 | Mobile OTP (sandbox); national digital ID slot «غير متاح» |
| Q15 returning sign-in | Step 4 | ID + mobile OTP, as an assumption |
| V1 representation / acting on behalf | Steps 6–7 | **Don't build** any power-of-attorney or on-behalf action. Coordination = sharing consented data + relaying communications, documented |
| V2 official lender channel | Step 6 | Coordination log records channel, date, counterpart, summary and evidence file. Wording is provisional |
| V4 consent text and data scope | Step 5 | Consent text is marked «صيغة مبدئية — تتطلب مراجعة» (provisional wording, needs review) |
| V5 internal SLA | Step 6 | Internal timers only, never shown to the individual |
| V6 lender identification | Step 5 | Assumption: a list of seeded lenders + «أخرى» free text |
| V7 duplicate request for the same finance | Step 5 | Warn and link; don't block (assumption) |

## Definition of done (acceptance criteria)

- [ ] **Primary journey in the browser on a 390 phone:** landing → registration → request (with documented consent for Rahoon to share the necessary data) → submitted, with autosave and return-later working.
- [ ] **«ماذا ستفعل رهون لك»:** after submission, the individual sees:
  - the relevant help path(s) (P1–P4) in plain language
  - the current status
  - **who we're waiting for** (فريق رهون / أنت / الجهة الممولة)
  - the next step
  - no deadline, no promised outcome
- [ ] **The Rahoon team workspace** lets a team member:
  - review the request
  - ask the individual for completion
  - see the consent evidence
  - log each manual coordination with the lender (channel, date, counterpart, summary, evidence)
  - post updates and next steps the individual sees
- [ ] **Approved offer:** the team records the lender's offer **with the lender's source document**, and a second team member verifies it before the individual sees it (ASSUMPTION control, reversible). The individual then sees:
  - the terms and their effect in plain language, per path
  - «ليس نهائياً حتى توافق عليه»
  - nothing presented as guaranteed
- [ ] **Response:** the individual accepts (OTP consent record), declines, or asks a question / counter-proposes. The response is documented with reference and time, and the team records relaying it to the lender over the manual channel.
- [ ] **Obstacles, minimum (P4):** the individual can object to incorrect data or amounts, complete documents and file a complaint. Referral to a specialist is recorded manually (V9).
- [ ] **Several requests per account:** each request has its own reference, status, documents and access. The session isn't tied to one case.
- [ ] **Privacy and access are enforced on the server and tested:**
  - no lender sees anything; lenders have no MVP access
  - only assigned Rahoon team members see a request
  - the individual sees only their own requests
  - consent is recorded per request, before any sharing is logged
  - amounts from the individual keep the source «العميل»
  - no internal notes reach the individual
- [ ] **No unapproved promise:** no deadline, "free", guaranteed discount, rescheduling approval or sale completion on any screen.
- [ ] **Every screen in scope passed a direction review** (table below).
- [ ] **Tests and checks:**
  - Playwright `web/e2e/owner-journey.spec.ts`: primary path + completion request + decline response + objection.
  - `dotnet test` is green.
  - Web typecheck, lint and build are green.
- [ ] **Wrap-up:** README demo script (owner-first) and tag `mvp-1`.

## Demo cast

| Who | Enters as | Owns (Q8) | Status |
|---|---|---|---|
| **An individual in default** (fictional, registers live on a phone) | `/` → «ابدأ طلب المعالجة» → registration | submits, provides information, **decides the response** | Steps 4–7 |
| **Rahoon team coordinator** (fictional platform user, seeded in step 6) | staff login → team workspace | **leads**: reviews, requests completion, coordinates with the lender manually, informs the individual, records the offer and the response | Step 6 |
| **Second Rahoon team member** (fictional, seeded) | staff login | verifies a recorded offer before release (ASSUMPTION control) | Step 7 |
| **The lender** (e.g. «مصرف الأفق», fictional) | **no login in the MVP**; represented by documents and messages over the manual channel | **decides** its offer and the finance terms | Evidence files in the seed |
| Existing lender staff (سارة, فهد, نورة …) | staff login | not part of the MVP demo; kept for the lender-on-platform mode | Secondary |

## Who clicks what (target demo script)

1. **Individual (phone):** opens `/`. Reads what منصة رهون does (four help paths, no guarantees, no new finance) and taps «ابدأ طلب المعالجة».
2. **Individual:** registers (ID + mobile + code + terms).
3. **Individual:** request steps:
   - names the lender and the finance
   - explains the situation and their preference (keep the home / settle / sell if continuing isn't possible / not sure)
   - uploads documents
   - gives **documented consent for Rahoon to share the necessary data** with the lender
   - reviews → «إرسال الطلب»
4. **Individual:** sees **«ماذا ستفعل رهون لك»**: the likely path(s), status «قيد مراجعة فريق رهون», waiting on «فريق رهون», next step.
5. **Rahoon coordinator:** opens the request.
   - Asks for a missing salary letter; the individual gets «بانتظارك» and uploads it.
   - Logs the coordination with «مصرف الأفق»: channel, date, summary, evidence.
   - Posts an update; the individual sees «قيد التنسيق مع الجهة الممولة» (waiting on the lender).
6. **Rahoon coordinator:** records the lender's approved offer with the lender's letter attached. The **second team member** verifies it.
7. **Individual:** sees the offer:
   - «عرض من جهتك الممولة»
   - terms, effect on them, «ليس نهائياً حتى توافق عليه»
   - no guarantee
8. **Individual:** accepts with OTP (or declines / asks). The response is recorded with reference and time. The coordinator logs relaying it to the lender. **Execution tracking continues in Phase 1A-2.**

## Scope and status

*Tech status* uses the legend in `README.md`. *Direction review* (after the 2026-09-25 decisions) is one of **OK**, **Rework**, **Secondary**, **New**, **Deferred** or **Needs design**.

| ID | Screen / capability | Tech status | Direction review | Notes |
|---|---|---|---|---|
| Landing | Owner-first public landing | ⬜ | New + **Needs design (D-1)** | Copy: the Rahoon team reviews and coordinates; four help paths; no «مجاني» or deadlines |
| OR01, OR02 | Registration, code, terms | ⬜ | New | Q10 and Q15 assumptions |
| — | Individual account with **several requests** | ⬜ | New | Replaces "owner = exactly one case" (X4) |
| OA01–OA05 | Request wizard | ⬜ | New + **Needs design (D-2)** | Lender named (V6); consent wording (V4); several requests (Q7) |
| OA06 successor | «ماذا ستفعل رهون لك» + tracking (status, waiting-on, next step) | ⬜ | New + **Needs design (D-3)** | No deadlines (Q6) |
| OA07 | Completion requested by the Rahoon team | ⬜ | Rework (actor = Rahoon team) | |
| OA08 | Declined / not suitable | ⬜ | Rework + **Needs design (D-6)** | The reason comes from the team, or is the lender's response relayed by the team |
| — | **Rahoon team workspace** (queue, review, completion, consent evidence, coordination log, updates, offer recording and verification, response relay) | ⬜ | New + **Needs design (D-4)** | Uses the existing design system; no frames exist |
| L00a, L00b | Lender intake queue and review | ⬜ | **Deferred** (X10) | Lender-on-platform mode, later |
| D01 | Lender invitation | 🟩 | Secondary / Deferred | Not used in the MVP |
| D02–D05 | Owner home, journey, documents, debt | 🟨 branch `…a1bd0510…` | Rework | Home = the request tracker; debt figures from the individual carry the source «العميل» until the lender confirms |
| D06 | Options | 🟨 same | Rework | Becomes the **help paths explainer** (P1–P4), with no promises |
| D07 | Offer | 🟨 same | Rework + **Needs design (D-5)** | «عرض من جهتك الممولة», recorded by Rahoon from the lender's document; per-path effects |
| D08, D09 | Counter / ask; accept with OTP consent | 🟨 same | OK (relay by the team) | A-05: a consent record, not a signature |
| D11–D13 | Help, messages, complaints | 🟨 same | OK | P4 minimum |
| L01–L26 lender workspace | — | ✅ / 🟨 | **Secondary** (lender-on-platform mode) | Kept; not in the MVP demo |
| L13–L17 solution builder / approvals | — | ✅ | **Secondary** (X11) | The mechanics may be reused for offer recording and verification in the team workspace (decided in step 2) |

## Steps (in order; one or more sessions each, all inside this phase)

### Step 0: Individual-first re-plan ✅ 2026-09-25
- [x] `product-direction.md`, B13 spec, design mirror refresh, phase files re-planned.

### Step 1: Product decisions recorded ✅ 2026-09-25
- [x] Answers for Q1, Q6, Q7, Q8, Q11, Q12, Q13 and Q14 recorded against each question's original text.
- [x] Q2 and Q3 resolved by derivation from Q1.
- [x] Q4, Q5, Q9, Q10 and Q15 remain open; verifications V1–V10 listed.
- [x] Plan, acceptance criteria and demo script updated. Pushed to the Rahoon repository.

### Step 2: Design and architecture alignment (no product code) ⬜
- [ ] **Design requests** for the product owner or designer to add to the design project:
  - **D-1** Landing copy: replace «تراجع جهتك الطلب» with the Rahoon team reviewing and coordinating; add a section on the four help paths; no «مجاني» or deadlines.
  - **D-2** OA01 (lender named, no «مشاركة في رهون» or waitlist) and OA04 (consent: Rahoon shares the necessary data with the lender; wording per V4).
  - **D-3** «ماذا ستفعل رهون لك» + tracking: path(s), status, waiting-on, next step.
  - **D-4** Rahoon team workspace: queue, review, completion, consent evidence, coordination log, updates, offer recording and verification, response relay.
  - **D-5** D07 as «عرض من جهتك الممولة», with per-path effects and without a guarantee; D06 as the help-paths explainer.
  - **D-6** OA08 reasons and wording.
- [ ] **ADR `docs/adr/0001-request-ownership.md`:**
  - the request is owned by the individual's account and handled in the Rahoon platform tenant
  - the lender is a **counterparty record**, not a tenant, in the MVP
  - how this coexists with today's lender-tenant case model, which is kept for the lender-on-platform mode
  - the Rahoon team role and permissions
  - the request state model (`product-direction.md` §5)
  - reuse of L13–L17 mechanics for offer recording and verification: yes or no
- [ ] If the design requests aren't ready when step 5 starts, build with the existing design system and mark each such screen «بانتظار اعتماد التصميم» in *Findings*.

### Step 3: Bring in finished work and review it ⬜
- [ ] **First:** fix the `reset-demo` safety finding (environment check before deleting the database; test).
- [ ] Merge `worktree-agent-a3939fc4d7a8b5a97` (case tabs, comms, complaints, audit, import, cancel) and `worktree-agent-a1bd05103bd12a217` (owner portal). Cherry-pick `36d316a` (L18–L21).
- [ ] Run the web typecheck, lint and build; fix conflicts. Record each merged screen's direction review above.
- [ ] Fix the Phase 0 leftover: breadcrumb shows the raw `new` segment.

### Step 4: Landing + registration + account with several requests ⬜ (Q9, Q10, Q15 interim rules)
- [ ] Backend: an individual account independent of any case; registration and sign-in (ID + mobile OTP); terms consent; national-digital-ID slot `unavailable`; lockout.
- [ ] Web: owner-first landing (D-1) and OR01/OR02; separate staff entry.
- [ ] Tests: duplicate identity → sign-in; OTP lockout; cookie-only session; the account can list several requests.

### Step 5: Request wizard + «ماذا ستفعل رهون لك» + tracking ⬜ (Q4, Q5, V4, V6, V7 interim rules)
- [ ] Backend:
  - request module: `REQ-YYYY-NNNNN`, draft autosave, lender named (list + «أخرى»)
  - document upload with the existing scanning and storage rules
  - **consent record per request** (text version stored)
  - submit lock (add-info only)
  - several requests per account; duplicate warning
  - audit
- [ ] Web: OA01–OA05, the submission result, and the «ماذا ستفعل رهون لك» tracker (status, waiting-on, next step; no deadline).
- [ ] Tests: the individual sees only their own requests; consent is required before submit; idempotent submit.

### Step 6: Rahoon team workspace ⬜ (V1, V2, V5 interim rules)
- [ ] New platform role «فريق رهون» (coordinator; verifier), with permissions and seeded users.
- [ ] Queue and request review; completion requests (OA07); consent evidence view.
- [ ] **Manual coordination log** with the lender: channel, date, counterpart, summary, evidence file; append-only and audited.
- [ ] Updates and next steps pushed to the individual; messages (D12).
- [ ] Internal processing timers (never shown to the individual).

### Step 7: Approved offer and the individual's response ⬜
- [ ] The team records the lender's offer with its source document; a second member verifies it (ASSUMPTION control).
- [ ] The individual sees D06 (paths explainer) and D07 (offer), then responds: D09 accept with OTP consent, D08 ask/counter, or decline, which never triggers any action against them.
- [ ] Response documented; the coordinator logs relaying it to the lender.

### Step 8: Obstacles, minimum (P4) ⬜
- [ ] Objection to incorrect data or amounts on a request, with a Rahoon team response.
- [ ] Document completion; complaint (reuse the complaints backend); manual record of a referral to a specialist (V9).

### Step 9: E2E, responsive QA, wrap-up ⬜
- [ ] `web/e2e/owner-journey.spec.ts`: primary path, completion request, decline response, objection.
- [ ] Adapt `web/e2e/lender-flow.spec.ts` to the lender-on-platform mode, or mark it secondary.
- [ ] Check at 390, 768 and 1440; bidi, contrast and 200% text.
- [ ] README demo script (individual → team → offer → response); update `docs/design-implementation-map.md`; tag `mvp-1`; push.

## Already verified (kept from earlier work)

- 2026-09-24:
  - سارة submitted RH-2026-004172 v2 and نورة approved it in the browser with the step-up OTP. The lender-side approval mechanics work; they're now secondary (X11) and may be reused in step 7.
  - Backend: 116/116 tests green on `master` (`be3b7e3`).

## Findings / open decisions

- **Safety (found 2026-09-25, fix first thing in step 3):** `dotnet run -- reset-demo` calls `EnsureDeletedAsync()` in `server/src/Rahoon.Api/Program.cs:86` **before** the seeder's Development/Testing guard (`DevSeeder.cs:39`). Run against a non-development environment, it would drop that database before failing. Fix: check the environment before deleting, and add a test.
- **Design gap:** the Rahoon team workspace (D-4) has no frames. B13 designed a lender intake instead, and that is now deferred.
- **Tenancy change:** the MVP request isn't owned by a lender tenant. Covered by the ADR in step 2; today's lender-tenant case model is kept for later.
- **Earlier design conflicts, still relevant:**
  - OR02 allows 5 OTP attempts; staff policy is 3.
  - The lender response deadline values (5 or 3 days) are moot for the UI (Q6: no deadlines).
- **Cancellation wording** (B3/B5 branch): the owner's access isn't revoked on cancellation. Product decision, lender-on-platform mode.
