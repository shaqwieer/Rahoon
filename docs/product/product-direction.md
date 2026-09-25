# رهون — Product direction (source of truth)

_Last updated: 2026-09-25 · Owner of this file: product owner. Engineering records changes here; it does not decide them._

This file is the **product** source of truth. The visual source of truth stays the Claude Design project (mirrored in `design-source/`, specified in `docs/design-specs/`). If a design file, spec, phase file or code assumption disagrees with this file, **this file wins**, and the disagreement is listed in [§7](#7-superseded-assumptions).

Every statement carries one of these labels:

| Label | Meaning |
|---|---|
| **CONFIRMED** | Decided by the product owner. Build on it. |
| **DESIGN** | Drawn in Claude Design (B13 or earlier batches) and consistent with the confirmed direction, but not separately approved. Build it, and flag it in reviews. |
| **PROPOSED** | Drawn in the design as «نمط مقترح» (a pattern for discussion). Do **not** build it as a final workflow until the linked question is answered. |
| **ASSUMPTION** | Engineering or design default used to keep moving. It's visible in the UI as an assumption and can be reversed. |
| **OPEN** | Unanswered. See [§6](#6-open-product-decisions). |

---

## 1. Who Rahoon is for (CONFIRMED, 2026-09-25)

1. Rahoon **primarily serves individuals who are struggling to repay an existing mortgage** (an existing home-finance contract secured on their property).
2. **The individual initiates** a request to address the default. **The financing institution becomes involved later** in the process.
3. Rahoon is **not primarily a bank-facing product**, and it **does not originate new mortgages** or offer new finance.
4. Every screen and feature is tested against the product owner's central question:
   > «إن كنت عميلاً متعثراً، ماذا أستفيد من رهون، وكيف تساعدني على حل مشكلتي؟»
   > "If I am a customer in default, what do I gain from Rahoon, and how does it help solve my problem?"
5. **Aqar Exit is only a partial reference.** It's not a product specification to copy. Don't import its flows, wording, pricing or solution list without an explicit product decision.

Unchanged from the original brief (still valid): Rahoon coordinates and documents. It is **not a court, not an auction operator, not a lender and not a public property marketplace**. Settlement, voluntary sale and judicial referral stay distinct. Declining an offer never leads to referral automatically. Not every case ends in foreclosure.

## 2. What the individual gains

Engineering must not promise outcomes. The table separates the benefits the design shows from those still awaiting approval.

| Benefit shown to the individual | Where it appears | Status |
|---|---|---|
| One place for the request, the data, the documents and the messages with the lender («مكان واحد لطلبك») | B13 landing promises | DESIGN |
| Understand before deciding: any offer is explained in plain language, with its terms, consequences and response options («تفهم قبل أن تقرر») | B13 landing, B6 D07 | DESIGN |
| The individual chooses who sees their data: it goes only to the chosen lender, with explicit consent («تختار مع من تشارك») | B13 OA04, landing | DESIGN. The scope of the commitment is **OPEN (Q12)** |
| The decision is the individual's: an offer isn't final until they accept it | B13 landing step 4, B6 D07 note | DESIGN |
| Tracking with clear states and the next step; autosave; can return later | B13 OA06, B6 D02 | DESIGN |
| Right to object, and a complaint channel reviewed independently of the case team | B6 D13, B13 OA08 | DESIGN. The legal basis of any promise is **OPEN (Q12)** |
| Response deadline from the lender | B13 OA06 shows it «بعد تأكيدها» | **OPEN (Q6)**. Don't display a deadline as a commitment |
| Free of charge | Removed from the landing page by the designer | **OPEN (Q9)** |
| Specific solutions (rescheduling, grace period, voluntary sale, …) | B6 D06 list is marked «قائمة مؤقتة — الحلول المتاحة لم تُعتمد بعد (Q11)» | **OPEN (Q11)**. Never present them as available services or promises |

## 3. The primary journey

| # | Step (individual's view) | Screens | Status |
|---|---|---|---|
| 1 | **Understand the service:** what Rahoon is and isn't, how it works, rights and privacy | Owner-first landing `P1-Public-Landing-*-OwnerFirst` (replaces S01) | DESIGN |
| 2 | **Register or sign in:** national ID or iqama + mobile → SMS code → accept terms. National digital identity is a reserved slot | OR01, OR02 | DESIGN. Identity provider **OPEN (Q10)** |
| 3 | **Start a request about an existing mortgage default:** choose the lender, then give finance and property details, the situation and preference, documents and scoped consent, then review | OA01–OA05 (autosave; «ليس طلب تمويل جديد») | DESIGN. Eligibility **OPEN (Q4)**; required data **OPEN (Q5)**; non-participating lender **OPEN (Q2)**; several lenders **OPEN (Q7)** |
| 4 | **Submit and track the request:** submitted → with the lender → possibly returned for completion → accepted (a case opens) or declined with a reason | OA06, OA07, OA08 | DESIGN. How the lender is involved is **OPEN (Q1)**; deadline **OPEN (Q6)**; Rahoon pre-screening **OPEN (Q3)** |
| 5 | **Provide required information and documents** after acceptance | D02–D05, D04, D12 | DESIGN (B6, owner-first framing) |
| 6 | **Review an approved proposal, when one exists:** accept with OTP consent, counter-propose or decline | D07, D08, D09 | DESIGN. **Which solutions exist is OPEN (Q11)**; who leads each step is **OPEN (Q8)** |
| 7 | **Document the outcome:** the decision is recorded with reference and time; the agreement or closure documents are available to the individual | D09 success, D14, agreement view | DESIGN. The legal effect of in-platform consent is still **ASSUMPTION A-05** (a consent record, not a licensed signature) |

Secondary routes (DESIGN, kept but not primary):
- **Lender invites the owner first** (D01). The design marks it «مسار ثانوي».
- **Lender creates a case manually** (L03). The design marks it «استثناء بسبب إلزامي», e.g. a customer who came to a branch.
- **Bulk import** (L04). «للترحيل فقط؛ لا تواصل مع المالك قبل دعوته».

## 4. Request stage before the case (DESIGN from B13; guarded by Q1/Q6)

A **request** (`REQ-YYYY-NNNNN`) exists before any case. A case (`RH-YYYY-NNNNNN`) is created only when the chosen lender accepts. It is linked to the request and starts in «تحقق» (verification), not «مسودة» (draft), because the individual already entered their data and verified their identity.

| State | Arabic | Who acts | Notes |
|---|---|---|---|
| `REQ.draft` | مسودة الطلب | individual | autosaved |
| `REQ.submitted` | مقدَّم | individual | after identity verification and consent to share |
| `REQ.lender_review` | بانتظار الجهة الممولة | lender | **PROPOSED**: mechanism and deadline need confirmation (Q1, Q6) |
| `REQ.info_requested` | مُعاد للاستكمال | individual | the lender asks for a correction or a document; the deadline pauses |
| `REQ.accepted` | مقبول ← حالة | lender | creates the case «تحقق» and assigns a case manager |
| `REQ.declined` | اعتذار مسبب | lender | no case; the reason is shown to the individual in plain text; they can object or apply to another lender |
| withdrawn | سحب المالك | individual | before the lender responds (B13 permissions table) |

Permissions by stage (DESIGN, B13 «الصلاحيات حسب المرحلة»):
- **Before submission:** the lender sees nothing.
- **During review:** only the chosen lender sees the request and the shared documents. Other lenders see nothing and don't know it exists.
- **After acceptance:** the full owner portal (D02–D14) and the full case workspace by role. Providers only when assigned, within scope.
- **After a decline:** the lender keeps the request record for audit only.

Decline reasons the design allows (PROPOSED): «ليس عميلاً لدينا · العقد غير عقاري · العقد مسدد أو مغلق · حالة قائمة لدينا بالفعل (تُربط بدل الاعتذار)».

Assumptions drawn in the design (**ASSUMPTION**, requires confirmation): a lender response deadline of 5 business days; non-participating lenders don't receive requests, and the individual is offered a waitlist or notification instead.

## 5. How the financing institution is involved

**OPEN (Q1).** The design draws one **PROPOSED** pattern: in-platform intake.
- **L00a:** «الطلبات الواردة» queue.
- **L00b:** review with a side-by-side match against the lender's records, and a decision: «قبول وفتح حالة / طلب استكمال من المالك / الاعتذار مع سبب».

The LenderSidebar now shows «الطلبات الواردة» as the first item and the default entry point, with «المحفظة» kept for reports.

B13 lists the alternatives that would change the lender screens completely:
- in-platform intake
- referral by Rahoon over another channel
- the individual authorizing Rahoon to contact the lender on their behalf

Until Q1 is answered, nothing lender-side for intake is final.

## 6. Open product decisions

Q1–Q12 are verbatim from B13 «نقاط تحتاج تأكيداً من صاحب المشروع»; Q13–Q15 were added by engineering while re-planning. **Blocks** says what can't be built as final until it's answered.

| # | Question | Why it matters | Blocks |
|---|---|---|---|
| **Q1** | How is the financing institution involved? (in-platform intake / referral by Rahoon over another channel / owner authorizes Rahoon to contact the lender) | Each option changes the lender screens completely | L00a/L00b, OA06, request → case conversion |
| Q2 | What if the lender is not on the platform? | Accept, park, or have Rahoon contact the lender | OA01 «جهتي غير موجودة» |
| Q3 | Does Rahoon screen the request before sending it to the lender? | May need a «فريق رهون» role, pre-review and eligibility criteria | OA05, OA06, platform roles |
| Q4 | Eligibility criteria (e.g. home finance only, mortgaged property, minimum delay, residents) | An eligibility screen before OA01 (not designed) | OA01 |
| Q5 | Data required at submission | Minimum data reduces drop-off; matching may need the contract number mandatory | OA02–OA04 |
| **Q6** | Lender response deadline and what happens after it | «حتى تاريخ…» is shown only if the deadline is binding | OA06, L00a |
| Q7 | Can an individual apply to more than one lender? | An individual may have defaulted finance with several lenders | OA01, permissions; "owner = exactly one case" |
| **Q8** | Steps after acceptance: who leads each step when the individual started the request? | The specs define solutions but not who leads each step | D02–D14, L05, solution workflow |
| Q9 | Is the service free for the individual? | «مجاناً» removed until pricing is confirmed | landing, terms |
| Q10 | Identity provider: national digital identity or mobile OTP only | Registration design and legal assurance level | OR01, OR02 |
| **Q11** | **Which solutions does Rahoon actually offer?** (rescheduling, grace period, voluntary sale… are examples in the spec, not approved services) | Don't show them to the individual as a promise | landing, D06, D07, L13–L17, B8 |
| **Q12** | What can Rahoon promise the individual? (response deadline, privacy, right to object, no action without notice) | Every promise needs a legal basis | landing, OA06 |
| Q13 | Does the MVP still include agreement activation, the payment schedule and closure, or does it end once the outcome is recorded? | Decides whether the old settlement-execution work stays in the MVP | Phase 1A-2 scope |
| Q14 | Can the individual have several requests or cases at once, and do they share one account? | Today's code pins an owner session to exactly one case | owner auth, D02 |
| Q15 | Does a returning individual sign in with ID + OTP (no password) as today's owners do, or with a password + OTP like staff? | Identity model for self-registered individuals | OR01/OR02, S03 |

**The solution workflow is blocked by Q1, Q8 and Q11**, with Q6 and Q12 needed for any deadline or promise shown to the individual. The intake journey (landing → registration → request draft → submit → tracking) can be built with the labelled assumptions above.

## 7. Superseded assumptions

These earlier assumptions are corrected by §1. Everything already built stays in the repository and in its verified status. It is re-reviewed against this file, not deleted.

| # | Earlier assumption | Where it lives | Correction |
|---|---|---|---|
| X1 | The financing institution is the primary customer; the public page sells to institutions (S01 «أدِر حالات التعثر في محفظتك», action «طلب عرض») | B2 spec S01, `docs/phases/*` (S01/S02 in 1B) | The public page addresses the individual in default; the institution section and S02 become secondary (B13) |
| X2 | Every case starts with the lender: wizard L03 (Draft → Awaiting data) or import L04 | Phase 0 anchors, `case-transition-matrix.md` (`finalize_intake`), `CaseDraftEndpoints`, seed | The primary start is the individual's request. L03 becomes an exception with a mandatory reason; L04 is for migration only. A case created from an accepted request starts in «تحقق» |
| X3 | The owner enters only through a lender invitation (D01) and signs in with invitation link + last 4 digits of ID + OTP | `architecture.md` §Security, `design-conflicts.md` #11, owner auth code, B6 spec D01 | Self-registration (OR01/OR02) is primary; D01 is secondary. The returning-owner sign-in is superseded, see Q15 |
| X4 | An owner session is pinned to exactly one case | `architecture.md`, `permission-matrix.md`, `RequestContext.OwnerCaseId` | An individual account exists before any case and owns requests; cardinality **OPEN (Q7, Q14)** |
| X5 | The lender's default landing is the portfolio dashboard (L01) | Phase 0, `HomeFor` in auth, LenderSidebar | Design moves the default to «الطلبات الواردة» (L00a). **PROPOSED** until Q1 |
| X6 | D06 lists available solutions as if they were offered | B6 spec D06 | The list is provisional (Q11). Offers appear only after the lender's review and approval (D07 note «عرض راجعته واعتمدته جهتك الممولة بعد دراسة طلبك. ليس نهائياً حتى توافق عليه.») |
| X7 | The first prototype flow is "the lender opens a case" | `08-flows-prototypes.md` | PROTO-00 «الفرد يقدّم طلب معالجة» is first; the lender's manual case opening (PROTO-01) is «استثناء» |
| X8 | The MVP is the lender's settlement path (create → approve → owner accepts → payments → closure) | `docs/phases/phase-1a-mvp-settlement.md` (before 2026-09-25) | The MVP starts with the individual (§3). Settlement execution moves to Phase 1A-2 (see Q13) |
| X9 | Demo scripts start with سارة creating a case | phase files, README demo logins, `web/e2e/lender-flow.spec.ts` | Demo scripts start with an individual registering and submitting a request; lender steps follow |

## 8. Change log

| Date | Change | By |
|---|---|---|
| 2026-09-25 | Individual-first direction confirmed (§1). Claude Design adds B13 (owner-initiated journey) and updates B6, Brief, Flows (PROTO-00), Handoff and LenderSidebar. The plan is corrected accordingly. | Product owner; recorded by engineering |
