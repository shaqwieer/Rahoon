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
| **Seeded «فريق رهون» users** (2026-09-26, password `Rahoon-Demo-2026!`, SMS code on screen) | `/login` → `/team` | لمى الحربي `l.alharbi@team.rahoon.example` (قائدة الفريق) · نايف اليامي `n.alyami@team.rahoon.example` and تركي الشهري `t.alshehri@team.rahoon.example` (منسقا حالات) · عبير القحطاني `a.alqahtani@team.rahoon.example` (مراجِعة العروض) | Steps 6–8 |
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
| Landing | Owner-first public landing | 🟩 built 2026-09-25; browser ✓ at 390 and 1440 | New + **Needs design (D-1)** | B13 frames + proposed D-1 text (help paths, the Rahoon team step), labelled «بانتظار اعتماد التصميم» until D-1 is approved. No «مجاني», no deadlines, consent described as a mechanism (Q12) |
| OR01, OR02 | Registration, code, terms | 🟩 built 2026-09-25; browser ✓ at 390 | New | `/start` (register) and `/start?mode=signin`; Q10 (digital ID «غير متاح») and Q15 (ID + mobile code) interim rules |
| — | Individual account with **several requests** | ✅ 2026-09-26: `/my` lists every request (reference, lender, status, «ننتظر»); API test with 3 requests on one account; Playwright ✓ at 390 | New | Session scope `Individual`, not tied to any case (X4 superseded in code) |
| OA01–OA05 | Request wizard | ✅ 2026-09-26: `/my/requests/[ref]/apply?step=1…5`, autosave per step, documents, consent confirmed by SMS code, review, submit; Playwright ✓ at 390 (768/1440 in step 9) | New + **Needs design (D-2)** «بانتظار اعتماد التصميم» | Built with the existing design system and the proposed D-2 text. Lender list + «جهة أخرى» (V6); consent text `request-consent-draft-2026-09` labelled «صيغة مبدئية — تتطلب مراجعة» (V4); duplicate warned and linked (V7); name collected in OA02 (Q5 interim) |
| OA06 successor | «ماذا ستفعل رهون لك» + tracking (status, waiting-on, next step) | ✅ 2026-09-26: submission confirmation + `/my/requests/[ref]` (status, «ننتظر», next step, likely paths «مبدئي — يتأكد بعد الدراسة», what we're doing now, past timeline, documents, consent with withdraw/renew, add info, withdraw); Playwright ✓ at 390 | New + **Needs design (D-3)** «بانتظار اعتماد التصميم» | No deadlines (Q6): the E2E asserts no «حتى تاريخ/مجاني/خلال n» on the tracker. This is the D02 rework for the MVP |
| OA07 | Completion requested by the Rahoon team | ✅ 2026-09-26: T03 on the team side; the individual sees «نحتاج معلومة منك» with the team's message as the next step and answers from «إضافة معلومة أو مستند» (returns the request to the team); Playwright ✓ | Rework (actor = Rahoon team) — done | |
| OA08 | Declined / not suitable | 🟩 variant 1 (not suitable, decided by the team, reason shown) 2026-09-26, API-tested; variant 2 (lender response relayed) with closing in step 7; variant 3 (withdrawn) ✅ step 5 | Rework + **Needs design (D-6)** | The reason comes from the team, or is the lender's response relayed by the team. The objection option arrives in step 8 |
| — | **Rahoon team workspace** (queue, review, completion, consent evidence, coordination log, updates, offer recording and verification, response relay) | ✅ 2026-09-26 for T01–T04 (`/team`, `/team/requests/[ref]`): queue tabs, review, assignment, identity check, completion request, consent evidence, coordination log, updates, messages and internal notes, not-eligible, documents; Playwright ✓ at 1440. T05–T07 in step 7, T08 in step 8 | New + **Needs design (D-4)** «بانتظار اعتماد التصميم» | Built on the lender-shell layout with a «فريق رهون» sidebar; internal timers labelled «داخلي — لا يظهر للعميل» |
| L00a, L00b | Lender intake queue and review | ⬜ | **Deferred** (X10) | Lender-on-platform mode, later |
| D01 | Lender invitation | 🟩 | Secondary / Deferred | Not used in the MVP |
| D02–D05 | Owner home, journey, documents, debt | 🟩 on `master` (merged 2026-09-25); browser smoke ✓ at 390 | Rework → **done for the MVP by the request tracker** (step 5); the case-bound D02 stays for the lender mode | Home = the request tracker. **Found:** D02 shows «لديك حتى 2026-10-05 (10 أيام)», a deadline that Q6 rules out. Debt figures from the individual carry the source «العميل» |
| D06 | Options | 🟩 same; smoke ✓ | Rework | Becomes the **help paths explainer** (P1–P4). **Found:** titled «الخيارات المتاحة لك» (X6); lists «تأجيل مؤقت» (V8) and «البيع الطوعي» (V8 wording) |
| D07 | Offer | 🟩 same; smoke ✓ | Rework + **Needs design (D-5)** | **Found:** «صالح حتى 2026-10-05» in the header (Q6); no approval note; «نتواصل معك أولاً… نمنحك 15 يوماً» is a commitment (Q12/V4); «بيتك يبقى ملكك» wording needs review (V4) |
| D08, D09 | Counter / ask; accept with OTP consent | 🟩 same; smoke ✓ | OK (relay by the team) | A-05: a consent record, not a signature |
| D12 successor | Request messages with the Rahoon team | ✅ 2026-09-26: `/my/requests/[ref]/messages`; team internal notes never projected (API test) | New | |
| D11–D13 | Help, messages, complaints | 🟩 same; smoke ✓; complaint filed as owner and opened by the reviewer | OK, one rework | **Found:** the complaint confirmation promises «نرد عليك كتابياً خلال 5 أيام عمل» (from `ComplaintService`; Q6/Q12) → fix in step 8 |
| L01–L26 lender workspace | — | ✅ (Phase 0 screens) / 🟩 (L06–L12, L18–L24, L04, C01 merged 2026-09-25; browser smoke ✓ at 1440) | **Secondary** (lender-on-platform mode) | Kept; not in the MVP demo. **Found:** seed case RH-2026-003870 is «تسوية معتمدة / نشطة» but has no agreement or schedule (Phase 1A-2 seed fix) |
| L13–L17 solution builder / approvals | — | ✅ | **Secondary** (X11) | The mechanics may be reused for offer recording and verification in the team workspace (decided in step 2) |

## Steps (in order; one or more sessions each, all inside this phase)

### Step 0: Individual-first re-plan ✅ 2026-09-25
- [x] `product-direction.md`, B13 spec, design mirror refresh, phase files re-planned.

### Step 1: Product decisions recorded ✅ 2026-09-25
- [x] Answers for Q1, Q6, Q7, Q8, Q11, Q12, Q13 and Q14 recorded against each question's original text.
- [x] Q2 and Q3 resolved by derivation from Q1.
- [x] Q4, Q5, Q9, Q10 and Q15 remain open; verifications V1–V10 listed.
- [x] Plan, acceptance criteria and demo script updated. Pushed to the Rahoon repository.

### Step 2: Design and architecture alignment (no product code) ✅ 2026-09-25
- [x] **Design requests D-1 to D-6** written in [`docs/design-requests/1a-step2-design-requests.md`](../design-requests/1a-step2-design-requests.md). Each has proposed Arabic text marked «يحتاج اعتماد صاحب المشروع» or «يحتاج مراجعة نظامية», the states to draw, and design acceptance criteria.
  - **Open:** the product owner or designer adds them to the design project. Status is tracked in that file.
  - D-4 (the Rahoon team workspace, T01–T08) is the largest, and it's needed from step 6.
- [x] **ADR [`docs/adr/0001-request-ownership.md`](../adr/0001-request-ownership.md)** accepted. Decisions:
  - **New organization kind `Operator` («فريق رهون»)** owns requests; the Rahoon team are its members. The Platform org is rejected as the owner, to keep the rule that platform admins see tenant data only under a dual-approved grant.
  - Requests are **also** `IApplicantOwned`. The individual is a new `Individual` session scope with an independent account (`individual_profiles`: encrypted ID and phone with HMAC lookup, unique per ID), and holds several requests.
  - The **lender is a counterparty** (`financing_institutions` directory, optional future link to a Lender tenant) with no access in the MVP. Moving a request into a lender tenant later needs new consent.
  - New `requests` schema:
    - Request (`REQ-YYYY-NNNNN`)
    - RequestConsent (versioned text, OTP-confirmed, withdrawable)
    - RequestDocument/Version
    - CoordinationEntry (append-only manual channel log, hidden from the individual by default)
    - RequestUpdate, RequestMessage
    - RequestOffer (recorded ≠ verified, lender letter required)
    - RequestResponse
    - RequestObjection
  - `RequestWorkflow`: 12 explicit transitions with guards (§4.5). No future dates in the UI.
  - Reuse: sessions/CSRF/OTP/PII/documents/notifier/audit. The audit chain gets subject columns with hash-format versioning. **L13–L17 aren't reused for MVP offers** (they model the lender's internal decision); the D07/D09 components are reused with a request data adapter.
  - Required isolation and leakage tests listed (§6).
- [x] Rule kept: if a design request isn't ready when its step starts, build with the existing design system and record «بانتظار اعتماد التصميم» in *Findings*.

### Step 3: Bring in finished work and review it ✅ 2026-09-25
- [x] **Safety fix first** (`97cb644`): `seed`/`reset-demo` are refused outside Development/Testing **before** any database access. A single `DemoDataGuard` is used by the CLI and `DevSeeder`. Tests `DemoDataGuardTests` (6) include running the real CLI in Production against an unreachable database; I confirmed they fail with the guard disabled.
- [x] Merged `worktree-agent-a3939fc4d7a8b5a97` (case tabs L06–L12, comms L22, complaints L23, case audit L24, import L04, cancellation C01) and `worktree-agent-a1bd05103bd12a217` (owner portal D02–D14, agreement view, notifications). Cherry-picked `36d316a` (L18–L21) as `ca4aefb`. No textual conflicts.
- [x] Checks:
  - web: `next typegen` + `tsc --noEmit` clean (after clearing stale generated `.next/dev/types`), `eslint .` clean, `npm run build` passes
  - backend: `dotnet test` **122/122**
- [x] Browser smoke check (Playwright sweep, demo data reseeded; screenshots reviewed for D02, D06, D07, L20, L23 detail and the breadcrumb):
  - **36 routes** load with HTTP 200, no console errors and no error boundary: 21 lender routes as سارة at 1440, and 15 owner routes through the demo invitation at 390, including D07–D09 on an offer created through the real submit → approve (step-up) path
  - complaint filed by the owner and opened by هند
  - the full visual comparison with the design at 390/768/1440 remains for step 9
- [x] Phase 0 leftover fixed: the breadcrumb for `/cases/new/…` now reads «الحالات › حالة جديدة › RH-…» (and `/cases/import` reads «استيراد»).
- [x] Direction review recorded in the table above. Rework items are assigned to the steps that rebuild those screens: D02 → step 5 (tracker); D06/D07 → step 7 (D-5); complaint confirmation → step 8.

### Step 4: Landing + registration + account with several requests ✅ 2026-09-25 (Q9, Q10, Q15 interim rules)
- [x] **Backend** (ADR 0001 §4.1):
  - `AccountKind` (Staff/Owner/Individual) and `UserStatus.Pending`
  - session scope `Individual`, with no organization and no tenant data
  - `identity.individual_profiles`: encrypted ID/iqama and mobile, HMAC lookup (unique per ID), masked values, `IdentityAssurance = self_declared`
  - `identity.terms_acceptances` (version + time + masked IP on every acceptance)
  - endpoints `POST /api/auth/individual/start|resend|verify` and `GET /api/individual/account`
  - `RequireIndividual` guard; `/api/auth/me` returns `scope=individual`, the masked identity and `home=/my`
  - an individual's logout returns to `/`
  - migration `IndividualAccounts`, which also tags existing invited-owner accounts as `Owner`
- [x] **Security:**
  - **anti-enumeration:** `/start` answers the same way whether or not the ID is registered. If the ID is registered with another mobile, a decoy challenge is issued: nothing is sent and the account isn't locked.
  - a pending registration from another mobile can only be replaced after its code expires
  - 3 wrong codes on the real mobile → 15-minute lock (same as staff)
  - terms are checked before the code, so a missing tick never costs an attempt
  - cookie-only session (HttpOnly), CSRF-exempt pre-session endpoints guarded by the Origin check and rate limit
- [x] **Web:**
  - individual-first landing (B13 + proposed D-1): header «كيف تعمل · كيف نساعدك · حقوقك وخصوصيتك · للجهات الممولة», «تسجيل الدخول», primary «ابدأ طلب المعالجة»; mobile «دخول» + menu
  - OR01/OR02 at `/start` (register and sign-in in one flow, terms + optional awareness opt-in on the code screen; field errors clear as you type)
  - `/my` («حسابي»: «طلباتي» empty state + account card with masked ID/phone, «مُعلَنة… لم تُوثَّق بعد», digital ID «غير متاح», accepted terms version)
  - draft `/terms` and `/privacy` pages, labelled «مسودة»
  - the staff `/login` note now sends individuals to `/start`; the footer has «دخول الموظفين»
  - `/my` is protected (no session → `/start?mode=signin`); an individual reaching staff pages is sent to `/my`
- [x] **Tests:**
  - `IndividualAccountTests` (9): registration, session not bound to a case, terms-before-code, field errors, same-ID sign-in (one account), decoy (no code, no lock), real lock, no staff/owner/tenant data, staff and anonymous refused, HttpOnly cookie and no token in the body
  - backend total **131/131**
  - web typecheck, lint and build clean
  - browser journey (Playwright, 20 checks at 390/1440, run three times): landing → validation → register → terms required → `/my` → staff page refused → sign out → protected redirect → sign in again; no web-storage token, no horizontal overflow, no console errors
- [x] *Moved to step 5:* "the account can list several requests" — requests don't exist before step 5. Step 4 proves the session isn't tied to any case.

### Step 5: Request wizard + «ماذا ستفعل رهون لك» + tracking ✅ 2026-09-26 (Q4, Q5, V4, V6, V7 interim rules)
- [x] Backend (ADR 0001 §4.2–§4.6):
  - organization kind `Operator` + seeded tenant «فريق رهون» (`rahoon-team`); role templates «منسق حالات», «مراجِع العروض», «قائد الفريق» and the `request.*` permission keys (users arrive in step 6)
  - `IApplicantOwned`: one combined query filter (tenant **or** the individual's own row) and a write-guard branch (an individual writes only their own rows, never deletes, never changes the applicant). The individual's session keeps an empty tenant set
  - new schema `requests`: `financing_institutions` (directory, not tenant data; 6 fictional institutions seeded), `requests` (`REQ-YYYY-NNNNN`), `request_consents`, `request_documents` + versions, `request_updates` (timeline; internal entries never projected)
  - `RequestWorkflow` with the full ADR §4.5 table (submit, pick_up, request_info, info_provided, start_coordination, publish_offer, respond, continue_coordination, close, not_eligible, withdraw); guards; blocked attempts audited
  - individual API `/api/my/requests`: draft autosave per step (locked after submit, add-only), documents (existing storage, magic-byte check, scanner), consent confirmed by SMS code with the text snapshot and version, consent withdrawal (pauses the request; a new consent resumes it), submit (idempotent), add information, withdraw
  - several requests per account; a duplicate for the same lender (and the same contract number when both are given) is warned and linked, never blocked (V7)
  - audit: `SubjectType`/`SubjectReference` + `HashVersion` (existing events stay format 1 and still verify); request events chain under the operator tenant; individual actor type `individual`
  - the person's name is collected in OA02 (Q5 interim) and replaces the masked-ID display label on submit
  - step-up now works for individuals (the phone is on the individual profile), needed for step 7
  - migration `IndividualRequests`
- [x] Web: `/my` («طلباتي» + «ابدأ طلب معالجة جديد»), OA01–OA05 wizard, submission confirmation, tracker, add information, new consent, withdraw. Request copy lives in `components/individual/copy.ts` (ar/en).
- [x] Tests: `RequestTests` (12): full wizard → consent → submit; refused without consent (audited); a lender change voids the consent; idempotent submit + lock; another individual gets 404 and the blocked write is audited; staff/platform/anonymous refused; several requests + duplicate warned and linked; the DTO never carries team-only documents or internal entries; consent withdrawal pauses and a new consent resumes; withdraw; audit chain verifies (operator + older lender events); field errors. Backend **143/143**. Web typecheck, lint and build green. `web/e2e/owner-journey.spec.ts` (first part) ✓ at 390.

### Step 6: Rahoon team workspace ✅ 2026-09-26 (V1, V2, V5 interim rules)
- [x] Platform role «فريق رهون»: operator tenant, role templates (منسق حالات، مراجِع العروض، قائد الفريق), `request.*` permissions (`docs/permission-matrix.md`) and seeded users (see the demo cast). Two fictional requests are seeded (REQ-2026-00301 unassigned, REQ-2026-00302 in review with نايف) so the queue isn't empty; the live demo continues from REQ-2026-00303.
- [x] Queue (T01: assigned to me / unassigned / all for the lead / finished) and review (T02); assignment and self-take; completion requests (T03/OA07); consent evidence (all consent rows with text snapshot, version, SMS-confirmed time, withdrawal); V12 identity check recorded before coordination.
- [x] **Manual coordination log** (T04): channel, date and time, counterpart, summary, evidence documents, visible-to-individual flag + separate text. Append-only (corrections are new entries referencing the original) and audited. Refused without active consent (audited). Banner «التنسيق لا يعني تمثيلاً رسمياً للعميل» (V1); wording provisional (V2).
- [x] Updates and next steps pushed to the individual; messages both ways (D12 successor) and internal notes (never projected).
- [x] Internal processing timers (V5 interim: team-waiting 3 days «تجاوز المعتاد», 7 days «متأخر»; others 10 days), shown on team screens only, labelled «داخلي — لا يظهر للعميل»; asserted absent from the individual DTO.
- [x] Guards now live: `start_coordination` needs active consent, an identity check and at least one coordination entry; `not_eligible` with a plain reason (Q4 interim).
- [x] Tests: `TeamRequestTests` (9): queue tabs and lead view; drafts never reach the team and other tenants get 403; assignment-scoped access with audited refusal; start-coordination guards and projection of visible entries only; consent required for coordination; information request round trip; messages vs internal notes; not eligible; append-only corrections. Test helper `TestClient.Raw` added because `ToJsonString()` escapes Arabic (earlier Arabic `DoesNotContain` checks could pass vacuously; the step-5 DTO test now uses it). Backend **152/152**. E2E: the team part of `owner-journey.spec.ts` ✓ (team at 1440, individual at 390).
- Migration `RahoonTeamWorkspace`.

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

- **Identity is self-declared (Q10, important):** without a national identity provider, anyone can register someone else's ID number with their own mobile. Mitigation today:
  - the account is labelled «مُعلَنة… لم تُوثَّق بعد»
  - the Rahoon team verifies identity (documents, lender match) before any coordination (steps 5–6; V12)
  - the real owner of an ID can't be locked out by others' attempts

  A national identity provider (Q10) removes the gap.
- ~~**The person's name isn't collected** at registration.~~ **Resolved in step 5:** OA02 asks «اسمك الكامل كما في الهوية» (Q5 interim), and the name replaces the masked-ID label on submit.
- **SMS abuse:** codes are rate-limited per IP (`auth` policy) and per session (60-second resend cooldown). A per-mobile daily cap is still missing; add it before a real SMS gateway is enabled.
- **Placeholders shown to users until later steps:**
  - ~~«تقديم طلب المعالجة يُتاح في هذه الصفحة قريباً» on `/my`~~ removed in step 5
  - draft terms and privacy pages; the final legal text is **V11** (see `product-direction.md` §7)
- **D-1 is pending design approval:** the landing uses the proposed D-1 text. It's labelled in this table, not on the page.

- ✅ **Resolved in step 3 (`97cb644`).** Safety (found 2026-09-25): `dotnet run -- reset-demo` calls `EnsureDeletedAsync()` in `server/src/Rahoon.Api/Program.cs:86` **before** the seeder's Development/Testing guard (`DevSeeder.cs:39`). Run against a non-development environment, it would drop that database before failing. Fix: check the environment before deleting, and add a test.
- **Design gap:** the Rahoon team workspace (D-4) has no frames. B13 designed a lender intake instead, and that is now deferred. The requests D-1 to D-6 are written (`docs/design-requests/1a-step2-design-requests.md`) and **await the product owner or designer**; steps 4–8 will use «بانتظار اعتماد التصميم» for any frame still missing.
- **Step 5 (2026-09-26), design pending:** OA01–OA05 and the tracker are built with the existing design system and the proposed D-2/D-3 text, «بانتظار اعتماد التصميم».
- **Step 5, deployment:** the «فريق رهون» operator tenant is created by the demo seeder only. A real environment needs it provisioned (platform admin, Phase 1C) before individuals can start requests; without it the API answers «خدمة الطلبات غير مهيأة بعد» (503).
- **Step 5, consent effect (V4):** withdrawing consent after submission moves the request to «نحتاج معلومة منك» with the next step «توقف التنسيق… وافق من جديد، أو اسحب الطلب»; a new consent resumes it. The exact legal effect is still V4.
- **Step 5, dev note:** running `next build` while `next dev` is running breaks the dev server's `.next` («Jest worker encountered 2 child process exceptions»). Restart `npm run dev` (after deleting `.next`) following a build.
- **Step 6, design pending:** T01–T04 are built with the existing design system (lender-shell layout, «فريق رهون» sidebar), «بانتظار اعتماد التصميم» (D-4).
- **Step 6, platform temp access:** platform admins can't read operator data at all today (no grant path to the operator tenant). Extending the temporary-access grant to the operator tenant is Phase 1C (ADR §4.2).
- **Step 6, notifications:** team actions create in-app notifications for the individual, but `/my` has no notifications screen yet; the timeline and «ننتظر» carry the information. An SMS nudge («لديك تحديث على طلبك») needs a product decision on messaging.
- **Tenancy change:** the MVP request isn't owned by a lender tenant. Covered by the ADR in step 2; today's lender-tenant case model is kept for later.
- **Earlier design conflicts, still relevant:**
  - OR02 allows 5 OTP attempts; staff policy is 3.
  - The lender response deadline values (5 or 3 days) are moot for the UI (Q6: no deadlines).
- **Cancellation wording** (B3/B5 branch): the owner's access isn't revoked on cancellation. Product decision, lender-on-platform mode.
