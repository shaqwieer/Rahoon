# منصة رهون — Product direction (source of truth)

_Last updated: 2026-09-25 (second revision: product-owner answers to Q1–Q15) · Owner: product owner. Engineering records decisions here; it doesn't make them._

This file is the **product** source of truth for منصة رهون. The visual source of truth is the design project (mirrored in `design-source/`, specified in `docs/design-specs/`). If a design file, spec, phase file or code assumption disagrees with this file, **this file wins**, and the disagreement is listed in [§8](#8-superseded-assumptions).

**Naming rule (CONFIRMED):** documents and interfaces say **«منصة رهون»** (or «رهون») only. They never name the software development company.

| Label | Meaning |
|---|---|
| **CONFIRMED** | Decided by the product owner. Build on it. |
| **DESIGN** | Drawn in the design project and consistent with the confirmed direction. Build it, and flag it in reviews. |
| **PROPOSED** | Drawn as «نمط مقترح», or proposed by engineering. Not final. |
| **ASSUMPTION** | An engineering default to keep moving. It's visible as an assumption and reversible. |
| **VERIFY** | Decided in principle, but needs operational, legal or regulatory verification before it's activated. See [§7](#7-verifications-required-before-activation). |
| **OPEN** | Unanswered. |

---

## 1. Product definition (CONFIRMED 2026-09-25)

1. منصة رهون is a **SaaS platform operating in Saudi Arabia**. Its **primary customer is the individual facing a default on an existing real-estate finance (mortgage)**.
2. **The individual starts the request themselves.**
3. Rahoon's purpose is to **actually help the individual**: understand their situation, reach a suitable solution, and follow its execution. It isn't just a request form. It isn't a bank-facing system with banks as the primary segment. It isn't a platform for getting new real-estate finance.
4. Every journey and screen is tested against the product owner's question:
   > **«أنا لو عميل ومتعثر، ماذا أستفيد من المنصة؟ وماذا تقدم حلًا لمشكلتي؟»**
5. **عقار إكزت** is only a partial reference for the idea. Don't copy its model or assume it describes Rahoon's services.

Still valid from the original brief: Rahoon coordinates and documents. It isn't a court, an auction operator, a lender or a public property marketplace. **It doesn't hold customer funds or execute payments itself.** Declining an offer never leads to any automatic action against the individual.

## 2. Service paths: what Rahoon does for the individual (CONFIRMED, answer to Q11)

Rahoon helps the individual through the paths that suit their situation:

| # | Path | What Rahoon does (verbatim from the product owner) |
|---|---|---|
| P1 | **الاحتفاظ بالعقار** (keeping the property) | «دراسة ظروفه وتجهيز ومتابعة طلب إعادة جدولة المديونية أو تخفيف ضغط الأقساط.» |
| P2 | **تسوية المديونية** (debt settlement) | «تجهيز مقترح تسوية، وتنسيق التواصل بشأنه، وتوضيح شروط أي عرض معتمد وأثره على العميل.» |
| P3 | **البيع الرضائي عند تعذر الاستمرار** (consensual sale when continuing isn't possible) | «تنسيق التقييم، والتعامل مع الجهة الممولة وأصحاب الحقوق، وإجراءات البيع، وتوضيح حصيلة البيع وما قد يتبقى من مديونية.» |
| P4 | **معالجة العقبات** (removing obstacles) | «تمكين العميل من الاعتراض على بيانات أو مبالغ غير صحيحة، واستكمال المستندات، وتقديم شكوى، والإحالة لمختص مناسب عند الحاجة.» |

**Guardrails (CONFIRMED):**
- These are **help paths Rahoon provides**, not promises that every solution is available to every individual.
- Never present a debt discount, a rescheduling approval or a completed sale as a **guaranteed outcome**.
- The **lender** decides the finance terms and any offer it issues. The **individual** decides whether to accept or decline an offer.
- Before building any action that lets Rahoon **formally represent** the individual or **act on their behalf**, verify the approval, authorization and regulatory requirements (VERIFY V1).
- Terminology: the product owner says **«البيع الرضائي»**; the design batch B8 says «البيع الطوعي». Align the UI wording at Phase 2 (VERIFY V8).

## 3. Who leads and who decides (CONFIRMED, answer to Q8)

| Actor | Leads or decides | In the MVP |
|---|---|---|
| **Rahoon team** (فريق رهون, e.g. a case coordinator) | **Leads** the request's follow-up and coordination and **informs the individual of the next step**. Reviews the request, obtains the individual's **documented consent** to share the necessary data, and coordinates with the lender over a **documented manual channel** (Q1). | Platform staff role (new) |
| **The financing institution** (lender) | **Decides** everything about the finance contract and the terms of any offer it issues. | **Doesn't need an account or technical integration.** It's reached through the documented manual channel |
| **The individual** | Starts the request, provides information and documents, **decides the response** to any offer. | Self-registered account |

Every step in the plan and in the demo script names its owner from this table.

## 4. The primary journey (MVP demo, CONFIRMED order)

**فرد متعثر يدخل رهون ← يقدم طلبه ← يعرف ما ستفعله رهون له ← يتابع دراسة الحالة والتواصل ← يرى عرضًا معتمدًا إذا توفر ← يرد عليه.**

The lender dashboard is **not** the main entry point.

| # | Step | Owner | Screens | Status |
|---|---|---|---|---|
| 1 | Enters Rahoon: understands what Rahoon is, what it does and doesn't do, and the four help paths | individual | owner-first landing (B13 §2) | DESIGN. Copy needs updating for Q1/Q11 (design request D-1) |
| 2 | Registers or signs in | individual | OR01/OR02 | DESIGN. Identity provider OPEN (Q10); returning sign-in OPEN (Q15) |
| 3 | Submits a request about the existing default: lender named, finance and property, situation and preference, documents, **documented consent for Rahoon to share the necessary data with the lender** | individual | OA01–OA05 | DESIGN with copy changes (D-2). Eligibility OPEN (Q4); required data OPEN (Q5) |
| 4 | **Knows what Rahoon will do for them:** the relevant help path(s), who acts next, and what Rahoon is waiting for. No deadline or promise of a solution | Rahoon team (content), individual (reads) | OA06 successor | Screen change needed (D-3) |
| 5 | Follows the case study and the communication: Rahoon team review → completion requests → coordination with the lender (manual, documented) → updates and messages | Rahoon team leads; the individual responds | OA06/OA07 successors + D02, D04, D12; **Rahoon team workspace** | Individual screens: DESIGN with changes. **Team workspace: not designed (D-4)** |
| 6 | Sees an **approved offer from the lender, if one exists**, explained: terms and their effect on them. Never "guaranteed" | lender decides the offer; Rahoon team records and explains it | D07 (+ D06 as path explainer) | DESIGN with changes (D-5) |
| 7 | **Responds** (accept / decline / ask or counter). The response is documented and relayed to the lender over the manual channel | individual decides; Rahoon team relays | D08, D09 | DESIGN. In-platform acceptance is a consent record, not a licensed signature (A-05) |

After the MVP (Phase 1A-2), the outcome's execution is **tracked**: agreement, installments, closure. Rahoon never holds funds or executes payments.

## 5. Requests, accounts and statuses (CONFIRMED where marked)

- **Account (CONFIRMED, Q14):** the individual has an **independent account** and can create **more than one request** to address different cases. Each request has its own reference, status, documents and access permissions. A user session is **not tied to a single case**.
- **Several requests (CONFIRMED, Q7):** the individual can submit separate requests, for example for finance with different lenders. A second request for the **same** finance is VERIFY V7.
- **Lender participation (CONFIRMED, Q1/Q2):** the individual names their lender, whether or not that lender uses Rahoon. No lender account or integration is needed to start. The design's «مشاركة في رهون» label and the «جهتي غير موجودة» waitlist are superseded (X14). How the lender is identified (a list of licensed institutions or free text) is VERIFY V6.
- **What the individual sees (CONFIRMED, Q6/Q12):** the request's **status**, **what the team or the individual is waiting for**, and **the next step**. **No response time or deadline, and no promise of a solution**, until a clear operating policy is approved. Processing times may be **tracked internally** and never shown as a commitment.
- **Request states (engineering design accepted in `docs/adr/0001-request-ownership.md` §4.5; the labels shown to the individual are proposed in design request D-3):**

  | State | Waiting on |
  |---|---|
  | `draft` | individual |
  | `submitted` | Rahoon team |
  | `team_review` | Rahoon team |
  | `info_requested` | individual |
  | `lender_coordination` | lender, over the manual channel |
  | `offer_available` | individual |
  | `response_recorded` | Rahoon team relays it |
  | `closed` | — |

  Plus the side exits `withdrawn` (individual) and `not_eligible` (with a plain-language reason once Q4 is decided).

- **Lender-on-platform mode (DESIGN, deferred):** B13's in-platform lender intake (L00a/L00b) and the existing lender workspace (L01–L26) remain for a later mode where a lender joins Rahoon. They're **not** the MVP entry and not required for the MVP (X10).

## 6. Q1–Q15: original text, decision, status, impact

Q1–Q12 are quoted **verbatim** from the design (B13 «نقاط تحتاج تأكيداً من صاحب المشروع»). Q13–Q15 are quoted verbatim from the engineering re-plan of 2026-09-25. Status is **محسوم** (decided) or **يحتاج تحققًا/قرارًا** (needs verification or a decision).

| # | النص الأصلي | الإجابة / القرار | الحالة | الأثر على المراحل |
|---|---|---|---|---|
| Q1 | «كيف تُشرَك الجهة الممولة؟» — «استقبال داخل المنصة، أو إحالة من رهون عبر قناة أخرى، أو تفويض من المالك لرهون بالتواصل نيابة عنه — كل خيار يغيّر شاشات الجهة كلياً.» | يبدأ الطلب لدى رهون. في الـMVP يراجع فريق رهون الطلب، ويحصل على موافقة موثقة من الفرد لمشاركة البيانات اللازمة، ثم ينسق مع الجهة الممولة عبر **مسار يدوي موثق**. لا يُشترط حساب للجهة ولا تكامل تقني كي يبدأ الفرد طلبه. | **محسوم** للـMVP. التمثيل القانوني وقناة التواصل الرسمية وأي تكامل لاحق: **يحتاج تحققًا** (V1–V3) | 1A: مساحة عمل فريق رهون + سجل تنسيق يدوي، بدل واجهة استلام الجهة (L00a/b تؤجَّل). بنية البيانات: الطلب ملك الفرد ومنصة رهون، والجهة طرف مسجَّل لا مستأجر (ADR في 1A الخطوة 2) |
| Q2 | «ماذا لو لم تكن الجهة مشاركة في المنصة؟» — «يحدد هل يُقبل الطلب أصلاً، أو يُحفظ، أو تتواصل رهون مع الجهة.» | يُستنتج مباشرة من إجابة Q1: يُقبل الطلب، وتتواصل رهون مع الجهة عبر المسار اليدوي الموثق. لا قائمة انتظار. | **محسوم** (مشتق من Q1). طريقة تحديد الجهة (قائمة أو نص حر): **يحتاج قرارًا** (V6) | 1A الخطوة 5: OA01 بلا وسم «مشاركة في رهون» وبلا «جهتي غير موجودة» |
| Q3 | «هل تفحص رهون الطلب قبل إرساله للجهة؟» — «قد يلزم دور «فريق رهون» ومراجعة أولية ومعايير أهلية.» | نعم: فريق رهون يراجع الطلب أولًا (من نص إجابة Q1). | **محسوم** للمراجعة. معايير الأهلية تبقى في Q4 | 1A: دور منصة جديد «فريق رهون» وصلاحياته ومساحة عمله |
| Q4 | «معايير الأهلية» — «مثلاً: تمويل عقاري فقط، عقار مرهون، حد أدنى للتأخر، أفراد مقيمون.» | لم يُحدَّد بعد. تعريف المنتج يحصر الخدمة في فرد لديه تعثر في تمويل عقاري قائم في السعودية، أما المعايير التفصيلية (حد التأخر، الإقامة، حالة الرهن) فلم تُحسم. | **يحتاج قرارًا** | 1A: لا شاشة أهلية ولا حالة `not_eligible` نهائية قبل القرار. يراجع الفريق يدويًا |
| Q5 | «ما البيانات المطلوبة عند التقديم؟» — «الحد الأدنى يقلل التخلي؛ المطابقة قد تحتاج رقم العقد إلزامياً.» | لم يُحدَّد. يُبنى الحد الأدنى من تصميم B13 (رقم العقد اختياري) كافتراض ظاهر. | **يحتاج قرارًا** | 1A الخطوة 5: الحقول الإلزامية افتراض قابل للتغيير |
| Q6 | «مهلة رد الجهة وما بعد انقضائها» — «المالك يرى «حتى تاريخ…» فقط إذا كانت المهلة ملزمة.» | لا تُعرض على الواجهات مدة استجابة قبل اعتماد سياسة تشغيل واضحة. تُعرض حالة الطلب وما ينتظره الفريق أو العميل والخطوة التالية. تُتتبع أوقات المعالجة داخليًا دون تقديمها كتعهد. | **محسوم** للواجهة. سياسة المدد الداخلية والتصعيد بعد انقضائها: **يحتاج قرارًا** (V5) | 1A: مؤشرات داخلية فقط. لا «حتى تاريخ…» للفرد |
| Q7 | «هل يمكن التقديم لأكثر من جهة؟» — «للمالك أكثر من تمويل متعثر لدى جهات مختلفة.» | للفرد أن ينشئ أكثر من طلب لمعالجة حالات مختلفة (مثل تمويلات لدى جهات مختلفة)، لكل طلب مرجعه وحالته ومستنداته وصلاحياته. | **محسوم**. تكرار طلب لنفس التمويل: **يحتاج تحققًا** (V7) | 1A: نموذج بيانات متعدد الطلبات؛ الموافقة على المشاركة لكل طلب على حدة |
| Q8 | «خطوات المعالجة بعد القبول» — «المواصفات تحدد الحلول لكن ليس من يقود كل خطوة عند بدء المالك للطلب.» | فريق رهون يقود المتابعة والتنسيق وإبلاغ الفرد بالخطوة التالية. الجهة الممولة تقرر ما يتعلق بعقد التمويل وشروط عروضها. الفرد يقرر الرد على العرض. | **محسوم** | كل خطوة في 1A والسيناريو التجريبي تسمّي صاحبها (§3) |
| Q9 | «هل الخدمة مجانية للفرد؟» — «أزلنا «مجاناً» من الصفحة حتى يتأكد نموذج التسعير.» | لم يُحدَّد (المنصة SaaS، ونموذج التسعير للفرد غير مذكور). | **يحتاج قرارًا** | لا «مجاني» على أي واجهة |
| Q10 | «مزود التحقق من الهوية» — «التحقق الوطني الرقمي أو رمز الجوال فقط.» | لم يُحدَّد. تبقى خانة التحقق الوطني الرقمي «غير متاح» ورمز الجوال كتجريبي. | **يحتاج قرارًا** | 1A الخطوة 4 |
| Q11 | «ما الحلول التي تقدّمها رهون فعلياً؟» — «إعادة الجدولة، السماح، البيع الطوعي… أمثلة من المواصفات لم تُعتمد كخدمات؛ لا نعرضها للفرد كوعد.» | أربعة مسارات مساعدة (§2): الاحتفاظ بالعقار؛ تسوية المديونية؛ البيع الرضائي عند تعذر الاستمرار؛ معالجة العقبات. هي مسارات مساعدة وليست نتائج مضمونة. | **محسوم**. «السماح» غير مذكور صراحة، ويُعامَل كجزء من «تخفيف ضغط الأقساط» فقط إن أكده صاحب المشروع (V8) | 1A: الصفحة العامة وD06 تشرح المسارات؛ D07 يعرض عرض الجهة المعتمد دون ضمان. 2: البيع الرضائي. 1B: أجزاء من معالجة العقبات |
| Q12 | «ما الذي يمكن أن تعد به رهون الفرد؟» — «مثلاً: مهلة رد، خصوصية، حق الاعتراض، عدم اتخاذ إجراء دون إشعار — كلها تحتاج أساساً قانونياً.» | لا وعد بحل ولا مدة استجابة قبل اعتماد سياسة تشغيل. يمكن **وصف** ما تقدمه المنصة (الموافقة الموثقة قبل المشاركة، إمكانية الاعتراض والشكوى كمسار خدمة) دون صياغتها كتعهد قانوني. | **محسوم** لقاعدة الواجهة. صياغة أي التزام (الخصوصية، «عدم اتخاذ إجراء دون إشعار»): **يحتاج تحققًا** قانونيًا (V4) | 1A: مراجعة نصوص الصفحة العامة وOA06 |
| Q13 | «Does the MVP still include agreement activation, the payment schedule and closure, or does it end once the outcome is recorded?» | يبقى تفعيل الاتفاق وتنفيذ المدفوعات والإغلاق المالي في `phase-1a2-settlement-execution.md`. الـMVP يركز على بدء الطلب، ومراجعته، وتنسيق الوصول إلى عرض معتمد عند توفره، وتوثيق رد الفرد. لا افتراض بأن رهون تحتفظ بأموال أو تنفذ دفعات. | **محسوم** | 1A ينتهي بتوثيق الرد؛ 1A-2 للتنفيذ والإغلاق (تتبع فقط) |
| Q14 | «Can the individual have several requests or cases at once, and do they share one account?» | حساب مستقل للفرد، أكثر من طلب، لكل طلب مرجعه وحالته ومستنداته وصلاحياته؛ الجلسة غير مربوطة بحالة واحدة. | **محسوم** | 1A الخطوة 4: نموذج الحساب يحل محل «owner = exactly one case» |
| Q15 | «Does a returning individual sign in with ID + OTP (no password) as today's owners do, or with a password + OTP like staff?» | لم يُحدَّد. | **يحتاج قرارًا** | 1A الخطوة 4: يُبنى الدخول بالهوية + رمز الجوال افتراضًا ظاهرًا حتى القرار |

## 7. Verifications required before activation

These items were decided in principle; verify each before its feature is activated.

| # | Item | Blocks |
|---|---|---|
| V1 | Approvals, authorization and regulatory requirements for Rahoon to **formally represent** the individual or **act on their behalf** | Any power-of-attorney or on-behalf action. Not built until verified |
| V2 | The **official communication channel** with each lender, and how the manual coordination is documented as evidence | Final wording of the coordination log; lender contact records |
| V3 | Any **technical integration** with lenders later | Lender-on-platform mode, integrations |
| V4 | **Consent text and the scope of data shared**, plus the wording of any privacy or notice commitment (data-protection and regulatory review) | Final text of OA04/consent and the landing promises |
| V5 | **Internal processing-time policy** (SLA and escalation). Never shown as a commitment until approved | Internal alerts only |
| V6 | How the individual **identifies their lender** (list of licensed institutions vs free text) | OA01 field type |
| V7 | A **second request for the same finance**: allowed, linked or blocked? | Duplicate check on submission |
| V8 | Wording: «البيع الرضائي» vs «البيع الطوعي» in the UI; whether «السماح/فترة مؤقتة دون أقساط» belongs under P1 | Path copy, Phase 2 |
| V9 | «الإحالة لمختص مناسب» (P4): who the specialists are and how a referral is made and documented | P4 in 1B |
| V10 | **Judicial referral** coordination isn't among the confirmed service paths. Confirm whether it stays in any scope | Phase 3 UI (the backend exists and is kept) |
| V11 | Final **terms of use and privacy policy** text (published as labelled drafts; each account records the accepted version `terms-draft-2026-09`) | Replacing the drafts on `/terms` and `/privacy`; re-acceptance when the version changes |
| V12 | **Self-declared identity** until Q10: anyone can register an ID number with their own mobile. The Rahoon team must verify identity before any coordination | Steps 5–6 review checklist; Q10 removes it |

## 8. Superseded assumptions

Everything already built stays in the repository with its verified status. It's re-reviewed against this file, not deleted.

| # | Earlier assumption | Where it lives | Correction |
|---|---|---|---|
| X1 | The financing institution is the primary customer; the public page sells to institutions | B2 spec S01, phase files | The public page addresses the individual in default; institutions are secondary |
| X2 | Every case starts with the lender (wizard L03 or import L04) | Phase 0 anchors, `case-transition-matrix.md`, `CaseDraftEndpoints`, seed | The individual's request is the start. L03 is an exception with a reason; L04 is for migration |
| X3 | The owner enters only via a lender invitation + last-4 + OTP | `architecture.md`, `design-conflicts.md` #11, owner auth code, B6 D01 | Self-registration is primary; the invitation is secondary; returning sign-in is Q15 |
| X4 | An owner session is pinned to exactly one case | `architecture.md`, `permission-matrix.md`, `RequestContext.OwnerCaseId` | Independent account, several requests (Q7, Q14) |
| X5 | The lender's default landing is the portfolio (L01) | Phase 0, `HomeFor`, LenderSidebar | Lender screens aren't the MVP entry at all (X10) |
| X6 | D06 lists solutions as if available | B6 D06 | D06 explains the four **help paths** (§2) without promising outcomes |
| X7 | The first prototype is "the lender opens a case" | `08-flows-prototypes.md` | PROTO-00 (the individual's request) is first |
| X8 | The MVP is the lender's settlement path | phase 1A before 2026-09-25 | The MVP is §4; execution moves to Phase 1A-2 (Q13) |
| X9 | Demo scripts start with سارة creating a case | phase files, README, `web/e2e/lender-flow.spec.ts` | The demo starts with the individual (§4) |
| X10 | **B13's lender intake (L00a/L00b) is how the lender joins** (design «نمط مقترح») | B13 spec §4, LenderSidebar design | MVP: the **Rahoon team** reviews and coordinates with the lender over a documented manual channel (Q1). L00a/L00b are deferred to a later lender-on-platform mode |
| X11 | Offers come out of the lender's internal workflow inside Rahoon (L13–L17) | Phase 0/1A lender screens | MVP: the lender's offer arrives over the manual channel, and the Rahoon team **records it with the lender's source document**. L13–L17 stay for the lender-on-platform mode |
| X12 | Consent means "share with the chosen lender only" (OA04 «أوافق على مشاركة طلبي ومستنداتي مع [الجهة] فقط») | B13 OA04 | Consent is the individual's documented approval for **Rahoon** to share the necessary data with the lender for coordination (text: V4) |
| X13 | The submitted request "reached the lender" (OA06 «وصل طلبك إلى [الجهة]»), and a decline comes from the lender (OA08) | B13 OA06/OA08 | The request reaches the **Rahoon team** first. Any lender response is relayed by the team |
| X14 | Only lenders participating in Rahoon receive requests; others go to a waitlist (OA01) | B13 OA01, §1 assumption | Any lender can be named; no participation needed (Q1/Q2) |

## 9. Change log

| Date | Change | By |
|---|---|---|
| 2026-09-25 | Individual-first direction confirmed. The design adds B13 and updates B6, Brief, Flows (PROTO-00), Handoff and LenderSidebar. Plan corrected. | Product owner; recorded by engineering |
| 2026-09-25 | Product definition refined (SaaS in Saudi Arabia, help beyond a request form); four confirmed service paths (Q11); Rahoon team leads and coordinates manually with lenders (Q1, Q8); no deadlines or promises in the UI (Q6, Q12); several requests per account (Q7, Q14); settlement execution stays in 1A-2 (Q13). Q2 and Q3 resolved by derivation from Q1. Q4, Q5, Q9, Q10 and Q15 still open; verifications V1–V10 recorded. | Product owner; recorded by engineering |
