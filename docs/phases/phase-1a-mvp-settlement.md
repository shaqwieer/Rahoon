# Phase 1A: MVP (the individual starts the request) ▶ CURRENT

> **Re-planned on 2026-09-25** after the product owner confirmed that Rahoon primarily serves individuals in default, and that **the individual initiates** the request while the lender joins later. See `docs/product/product-direction.md`; the design is `docs/design-specs/B13-owner-initiated-journey.md`.
> The file name is kept for continuity. The lender settlement-execution work that used to be the MVP (agreement activation → payments → closure) moved to [`phase-1a2-settlement-execution.md`](phase-1a2-settlement-execution.md).

**Goal:** an individual struggling with an existing mortgage can get from first visit to a documented outcome, with real persisted data:
understand the service → register or sign in → start and submit a request about the existing default → provide information and documents → track progress → review an approved proposal when one exists → have the outcome documented.

**The product question every screen must pass:** «إن كنت عميلاً متعثراً، ماذا أستفيد من رهون، وكيف تساعدني على حل مشكلتي؟»

## Product gate: read before any step

Steps below are marked with the open decisions they depend on (`docs/product/product-direction.md` §6). **Don't turn a guess into an approved requirement.**
- A step marked ⛔ can't be built as final until its question is answered.
- If you proceed on an assumption, the UI must label it (as the design does, e.g. «نمط مقترح», «قائمة مؤقتة», «افتراض»), and the assumption goes into *Findings*.

| Decision | Blocks |
|---|---|
| **Q1** How the lender is involved | Step 5 (lender side), request → case |
| **Q11** Which solutions Rahoon offers | Step 7 (proposal content shown to the individual) |
| **Q8** Who leads steps after acceptance | Steps 6–7 |
| Q6 Response deadline | Any deadline shown in OA06/L00a |
| Q12 What Rahoon may promise | Landing promises, OA06 text |
| Q9 Pricing (free?) | Landing, OR01 sub-title |
| Q10 / Q15 Identity and returning sign-in | Step 3 |
| Q2, Q4, Q5, Q7 | Step 4 details (non-participating lender, eligibility, required fields, several lenders) |
| Q13 Does the MVP include settlement execution? | Whether Phase 1A-2 is part of the MVP |

## Definition of done (acceptance criteria)

- [ ] **Primary journey:** a new individual completes it in the browser on a 390 phone, landing → registration → request (OA01–OA05) → submitted → tracked, with autosave and return-later working.
- [ ] **Request outcomes** work end to end with persisted data and audit, **recorded by whoever Q1 designates** (lender staff in-platform, Rahoon staff, or another mechanism). The outcome set below is the design's, and it's also gated by Q1:
  - (a) accepted → a case opens in «تحقق» linked to the `REQ-…` reference
  - (b) returned for completion → the individual completes it
  - (c) declined with a reason → no case; the individual sees the reason and next options
- [ ] **After acceptance** (⛔ Q8: who leads each step): the individual sees the next step, required documents and messages (D02, D04, D12). When an approved proposal exists, they can review it (D07), accept with OTP consent (D09), counter-propose (D08) or decline. The decision is recorded with reference and time and stays visible to them.
- [ ] **Privacy rules are enforced on the server** and covered by tests:
  - the lender sees nothing before submission
  - only the chosen lender sees the request and shared documents
  - other lenders get 404
  - consent is scoped to one lender and can be withdrawn before acceptance
  - amounts from the individual keep the source «المالك»
- [ ] **No unapproved promise:** no solution is shown as available, and no deadline or "free" is presented as a commitment, unless the related question (Q6, Q9, Q11, Q12) is answered.
- [ ] **Every screen in scope passed a product review.** The review is recorded in the table below (column *Direction review*).
- [ ] **Tests and checks:**
  - Playwright owner-first E2E is green: `web/e2e/owner-journey.spec.ts`, covering the primary path + decline + info request.
  - `dotnet test` is green.
  - Web typecheck, lint and build are green.
- [ ] **Wrap-up:** README has the owner-first demo script, and `mvp-1` is tagged.

## Demo cast (owner-first)

Password for staff: `Rahoon-Demo-2026!`. SMS codes are shown on screen (sandbox).

| Who | How they enter | Does | Status |
|---|---|---|---|
| **A new individual** (fictional, registers live) | `/` → «ابدأ طلب المعالجة» → OR01 (ID or iqama + 05 mobile) → OR02 code | Registers, fills OA01–OA05, submits, tracks, later reviews the proposal | Built in steps 3–7 |
| عبدالله م. (seeded individual, design sample) | Sign in (method per Q15) | Has REQ-2026-00318 → accepted → RH-2026-004172, with an approved proposal to review | Seed in steps 4–5 (today he exists only as a party on RH-2026-004172, entering by invitation) |
| Declined sample | seeded | REQ-2026-00341, declined «لا يوجد عقد تمويل عقاري باسمك لدينا.» | Seed in step 5 |
| سارة القحطاني, case manager, s.alqahtani@alufuq.example | staff login | Reviews incoming requests (L00a/L00b **PROPOSED**, Q1), then runs the case | Step 5 depends on Q1 |
| فهد العتيبي, analyst, f.alotaibi@alufuq.example | staff login | Prepares a proposal on the case | Reuses existing screens |
| نورة الشهري, approver, n.alshehri@alufuq.example | staff login | Approves the proposal with step-up, so the individual sees it | Reuses existing screens |

## Who clicks what (target demo script; filled in as steps complete)

1. **Visitor (phone):** opens `/`, reads «كيف تعمل» and the privacy notes, taps «ابدأ طلب المعالجة».
2. **Individual:** OR01 enters ID and mobile → OR02 code + terms → account created.
3. **Individual:** OA01 chooses «مصرف الأفق» → OA02 finance and property → OA03 situation and preference → OA04 uploads a salary letter and ticks the consent for «مصرف الأفق فقط» → OA05 review → «إرسال الطلب».
4. **Individual:** OA06 shows «بانتظار الجهة الممولة», with no deadline shown until Q6 is answered. They can add information or withdraw.
5. **Lender:** mechanism per Q1. Proposed pattern: سارة opens «الطلبات الواردة» → REQ-… → match table → «قبول وفتح الحالة». The alternatives are «طلب استكمال» → the individual gets OA07, or «الاعتذار مع سبب» → OA08.
6. **Individual:** «قُبل طلبك وفُتحت حالتك», with the name of the case manager, the next step, required documents (D04) and messages (D12).
7. **Proposal** (who does it is ⛔ Q8; shown here with today's lender mechanics): فهد prepares a proposal, سارة submits it, نورة approves it with step-up. The solution types are provisional until Q11.
8. **Individual:** D07 reviews the approved proposal («عرض راجعته واعتمدته جهتك الممولة…») → D09 accepts with OTP (or D08 counter / decline) → sees the recorded outcome (reference and time) and the agreement view.

## Scope and status

*Tech status* uses the legend in `README.md` and records what was built and verified. *Direction review* says whether the screen fits the individual-first direction: **OK**, **Rework** (listed), **Secondary** (kept, not in the primary path), **New**, **Proposed** (blocked by a question) or **Provisional** (labelled until answered).

| ID | Screen | Tech status | Direction review | Where / notes |
|---|---|---|---|---|
| Landing | Owner-first public landing (replaces S01) | ⬜ | New | B13 §2; moved into the MVP from Phase 1B |
| OR01, OR02 | Individual registration + code + terms | ⬜ | New | B13 §3; backend: an individual account independent of any case (Q10, Q15) |
| S03/S04 | Sign-in + MFA | ✅ (staff) | Rework | Split the entry: individual sign-in (Q15) vs staff «للجهات الممولة» |
| OA01–OA05 | Request wizard (autosave, consent scoped to one lender) | ⬜ | New | B13 §3; new backend request module (Q2, Q4, Q5, Q7) |
| OA06–OA08 | Request tracking, completion, decline | ⬜ | New | B13 §3; deadline text waits for Q6 |
| L00a, L00b | Lender incoming requests + review | ⬜ | **Proposed (Q1)** | Build only the pattern Q1 selects |
| — | Request → case (starts in «تحقق», linked to `REQ-…`) | ⬜ | New | A new creation path beside the wizard |
| D01 | Invitation + identity check | 🟩 | Secondary | Kept as «مسار ثانوي» (design label) |
| D02–D05 | Owner home, journey, documents, debt | 🟨 branch `…a1bd0510…` | Rework | Before acceptance, home = OA06; the header «مع [الجهة]» appears only once a case exists |
| D06 | Options | 🟨 same | **Provisional (Q11)** | Add the design tag «قائمة مؤقتة — الحلول المتاحة لم تُعتمد بعد (Q11)» |
| D07 | Offer | 🟨 same | Rework | Add the design note «عرض راجعته واعتمدته جهتك الممولة بعد دراسة طلبك. ليس نهائياً حتى توافق عليه.» |
| D08, D09 | Counteroffer; accept with OTP consent | 🟨 same | OK | The consent record is the documented outcome (A-05: not a licensed signature) |
| D11–D13 | Help, messages, complaints | 🟨 same | OK | D13 also covers «الاعتراض على الرد» after a decline (Q3: who reviews) |
| L01 | Portfolio | ✅ | Secondary | No longer the lender's default landing, if Q1 confirms the intake queue |
| L02, L05 | Case list, workspace | ✅ | Rework (small) | Show the case source «طلب من الفرد» + the `REQ-…` link |
| L03 | Create-case wizard | ✅ | Secondary | Becomes an exception with a mandatory reason; add the reason field in Phase 1B |
| L06–L12 | Case tabs | 🟨 branch `…a3939fc4…` | OK | Needed after acceptance |
| L13–L17 | Solutions, compare, submit, approvals, owner preview | ✅ | **Provisional (Q11, Q8)** | The mechanics work; solution types stay provisional |
| L18 | Negotiation (counteroffer handling) | 🟨 branch `…a1008744…` @ `36d316a` | OK | |
| L22, L24 | Comms, case audit | 🟨 branch `…a3939fc4…` | OK | |

## Steps (in order; each step is one or more sessions, all inside this phase)

### Step 0: Correct the plan to the individual-first direction ✅ 2026-09-25
- [x] Product source of truth `docs/product/product-direction.md`
- [x] B13 spec `docs/design-specs/B13-owner-initiated-journey.md`
- [x] Design mirror refreshed (B13 added; B6 and batch files updated)
- [x] Phase files re-planned; superseded assumptions listed; open decisions recorded

### Step 1: Product decision checkpoint (no code) ⬜
- [ ] The product owner answers or defers Q1, Q8, Q11 (these block the solution workflow), plus Q6, Q9, Q10, Q12, Q13, Q14 and Q15.
- [ ] Engineering records each answer in `product-direction.md` §6, then updates this file's ⛔ marks.
- Steps 2–4 can start without these answers, using the labelled assumptions. Steps 5 and 7 can't be finished without them.

### Step 2: Bring the finished work in, and review it against the direction ⬜
- [ ] Merge `worktree-agent-a3939fc4d7a8b5a97` (case tabs, comms, complaints, audit, import, cancel UI).
- [ ] Merge `worktree-agent-a1bd05103bd12a217` (owner portal).
- [ ] Cherry-pick `36d316a` (L18–L21). L19–L21 are used in Phase 1A-2.
- [ ] Run the web typecheck, lint and build; fix merge conflicts.
- [ ] Do the design-text reworks that need no product decision:
  - D01 «مسار ثانوي»
  - D06 provisional tag
  - D07 approval note
  - breadcrumb raw `new`
- [ ] Record each merged screen's *Direction review* result in the table above.

### Step 3: Owner-first landing + individual registration ⬜ (Q9, Q10, Q12, Q15 labelled)
- [ ] Backend:
  - an individual account that isn't tied to a case (supersedes "owner = exactly one case"; see X4)
  - registration by ID + mobile + OTP, and terms consent recorded
  - the national-digital-ID slot as `unavailable`
  - sign-in for a returning individual (assumption until Q15)
  - rate limits and lockout
- [ ] Web:
  - owner-first landing (desktop and mobile), with the institution section secondary
  - OR01/OR02
  - separate entry points for the individual and for staff
- [ ] Tests: registration, duplicate identity → sign-in, OTP lockout; no bearer tokens, cookies only (same session model).

### Step 4: Request wizard OA01–OA05 ⬜ (Q2, Q4, Q5, Q7 labelled)
- [ ] Backend request module:
  - `REQ-YYYY-NNNNN` reference
  - draft with autosave
  - participating-lender list
  - «جهتي غير موجودة» waitlist (assumption, Q2)
  - pre-case document upload with the same scanning and storage rules
  - consent scoped to one lender, withdrawable before acceptance
  - submit locks the request (add-info only)
  - audit on every step
- [ ] Web: OA01–OA05, mobile-first, with autosave and «محفوظ».
- [ ] Tests: another lender can't see the request; the lender sees nothing before submission; consent scope; idempotent submit.

### Step 5: Tracking + lender involvement ⬜ (⛔ Q1 for the lender side; Q6 for deadlines)
- [ ] Individual: OA06 tracking, add information, withdraw; OA07 completion; OA08 decline with next options.
- [ ] Lender side, **exactly as decided in Q1**. If Q1 selects in-platform intake:
  - L00a/L00b
  - the match table
  - accept → case created in «تحقق» and linked to REQ, with an assignment rule
  - request completion, decline with a listed reason, link to an existing case
  - LenderSidebar «الطلبات الواردة» as the lender's home
- [ ] Seed the B13 samples: REQ-2026-00318 → RH-2026-004172, and REQ-2026-00341 declined.

### Step 6: After acceptance, the individual follows the case ⬜ (Q8)
- [ ] D02 home continues from OA06 (same account, the case now visible); D03–D05, D04 documents, D12 messages.
- [ ] The lender workspace shows the case source + REQ link; document requests reach the individual.

### Step 7: Proposal review and documented outcome ⬜ (⛔ Q11 for proposal content; ⛔ Q8 for who prepares and approves it)
- [ ] The proposal is prepared and approved by whoever Q8 designates. If that's the lender, reuse the existing L13–L17 mechanics (preparer ≠ approver, step-up). The solution types shown stay labelled provisional until Q11.
- [ ] Individual:
  - D07 (with the approval note) → D09 accept with OTP consent, or D08 counter → L18, or decline (never leads to referral)
  - the recorded outcome (reference, time, terms) stays visible and printable

### Step 8: E2E, responsive QA, wrap-up ⬜
- [ ] `web/e2e/owner-journey.spec.ts` covers the primary path, the decline branch and the info-request branch. Adapt `web/e2e/lender-flow.spec.ts`: it still starts with the lender creating a case, which is now the exception path.
- [ ] Check at 390, 768 and 1440; bidi, contrast and 200% text.
- [ ] README owner-first demo script; update `docs/design-implementation-map.md`; tag `mvp-1`.

## Already verified (kept from before the re-plan)

- 2026-09-24:
  - سارة submitted RH-2026-004172 v2 and نورة approved it in the browser with the step-up OTP. The case moved to «بانتظار العميل». This proves the proposal-approval mechanics reused in step 7.
  - Backend: 116/116 tests green on `master` (`be3b7e3`).

## Findings / open decisions

- **Request deadline:** the design uses two values, 5 business days (state-model assumption) and «3 أيام عمل» (L00b sample). Both are pending Q6.
- **OR01 sub-title:** it says «مجاني», but the landing page removed «مجاناً» pending Q9. Don't render it until Q9 is answered.
- **OTP attempts:** OR02 allows 5 before a temporary lock; today's staff policy is 3 wrong codes → 15-minute lock. Choose one when implementing and record it in `design-conflicts.md`.
- **Cancellation wording** (from the B3/B5 branch): the backend doesn't revoke the owner's portal access on cancellation. Product decision.
- **Approvals inbox:** it lists only solution approvals; other approval types notify only. Revisit in Phase 1A-2.
