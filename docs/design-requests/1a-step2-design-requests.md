# Design requests D-1 to D-6 (Phase 1A, step 2)

_Prepared 2026-09-25 for the product owner or designer, to be added to the design project. Basis: `docs/product/product-direction.md` (decisions Q1–Q15) and `docs/adr/0001-request-ownership.md`._

**How to read this file**
- Each request says **why** (the decision it follows), **what exists** (design frame IDs), **what to change or add**, the **states** to draw, and **acceptance** for the design.
- Arabic text is marked either «نص مقترح — يحتاج اعتماد صاحب المشروع» (proposed text, needs the product owner's approval) or «يحتاج مراجعة نظامية» (needs legal/regulatory review, V4/Q12).
- **Nothing here is approved copy.**
- Reuse the existing design system throughout:
  - tokens and components C01–C15
  - `DebtorTop`/`DebtorNav`
  - C04 NextAction, C07 Document, C10 AuditTimeline, C11 SystemState
  - `ReviewScreen`, `IntegrationState`
- Mobile-first 390 for the individual, desktop-first 1440 for the Rahoon team; 768 rules stated in text.

**Global rules for every frame (from the decisions)**
- No deadline, «حتى تاريخ…», «مجاني», guaranteed discount, rescheduling approval or sale completion (Q6, Q9, Q11, Q12).
- Each request status shows three things: **the status label**, **«ننتظر»** (فريق رهون / أنت / جهتك الممولة) and **the next step** (Q6).
- The lender decides its offers, and the individual decides the response (Q8).
- Nothing implies that Rahoon represents the individual or acts on their behalf (V1).
- The product is named «منصة رهون» or «رهون» only.

---

## D-1 Owner-first landing: copy and the help-paths section

- **Why:** Q1/Q8 (the Rahoon team reviews and coordinates), Q11 (four help paths), Q6/Q9/Q12 (no deadlines, «مجاني» or promises).
- **Exists:** `P1-Public-Landing-Desktop-OwnerFirst` (1440) and `P1-Public-Landing-Mobile-OwnerFirst` (390), in B13.
- **Change: «كيف تعمل»** (نص مقترح — يحتاج اعتماد صاحب المشروع)
  1. «أنشئ حسابك» — «تحقق من هويتك برقم الهوية والجوال.»
  2. «قدّم طلبك» — «عرّفنا بتمويلك ووضعك وما يناسبك.»
  3. «يدرس فريق رهون طلبك» — «ويتواصل مع جهتك الممولة بعد موافقتك.» *(replaces «تراجع جهتك الطلب»)*
  4. «تابع ورد» — «أي عرض تعتمده جهتك الممولة يُشرح لك هنا، والقرار لك.»
- **Add: «كيف يمكن أن تساعدك رهون»**, four cards, one per help path, with this footnote (نص مقترح):
  > «هذه مسارات مساعدة وليست نتائج مضمونة. شروط التمويل والعروض تقررها جهتك الممولة، وقرار القبول أو الرفض لك.»

  | Path | Card text (نص مقترح) |
  |---|---|
  | P1 «الاحتفاظ بالعقار» | «ندرس ظروفك ونجهّز ونتابع طلب إعادة جدولة المديونية أو تخفيف ضغط الأقساط.» |
  | P2 «تسوية المديونية» | «نجهّز مقترح تسوية وننسق التواصل بشأنه، ونوضح لك شروط أي عرض معتمد وأثره عليك.» |
  | P3 «البيع الرضائي عند تعذر الاستمرار» | «ننسق التقييم والتعامل مع الجهة الممولة وأصحاب الحقوق وإجراءات البيع، ونوضح لك حصيلة البيع وما قد يتبقى من مديونية.» Label wording V8: «الرضائي» vs «الطوعي» |
  | P4 «معالجة العقبات» | «يمكنك الاعتراض على بيانات أو مبالغ غير صحيحة، واستكمال مستنداتك، وتقديم شكوى، ونحيلك لمختص مناسب عند الحاجة.» |

- **Change: promises.**
  - «تختار مع من تشارك» becomes «لا نشارك بياناتك مع جهتك الممولة إلا بموافقتك الموثقة» (يحتاج مراجعة نظامية — V4).
  - Keep «مكان واحد لطلبك» and «تفهم قبل أن تقرر».
  - Remove the visible «(Q12)» remark only once V4 wording is approved.
- **Remove:** any «مجاني» (Q9 open).
- **Acceptance:** desktop and mobile frames updated; the paths section has a mobile layout (cards stack); no future date appears anywhere.

## D-2 Request wizard OA01 and OA04 (+ OA03 choices)

- **Why:** Q1/Q2 (any lender, no participation required), V6 (how the lender is identified), V4 (consent wording), Q11 (paths).
- **Exists:** `P1-Owner-Apply-Mobile-Step1`…`Step4` in B13.
- **OA01 «من جهتك الممولة؟»:**
  - Remove «مشاركة في رهون» and «جهتي غير موجودة — نحفظ طلبك ونبلغك عند انضمامها».
  - Draw a searchable list of institutions + «جهة أخرى» with a free-text name (interim V6).
  - Keep «هذا الطلب لمعالجة تعثر في تمويل عقاري قائم … وليس طلب تمويل جديد».
  - Add a hint: «يمكنك تقديم طلب منفصل لكل تمويل متعثر» (Q7).
- **OA03 choices, aligned with the paths** (نص مقترح):
  - «البقاء في منزلي وتخفيف الأقساط أو إعادة الجدولة» (P1)
  - «تسوية المديونية» (P2)
  - «أفكر في بيع العقار بنفسي» (P3; the individual raises it)
  - «لست متأكداً — أحتاج نصيحة»
  - The B13 option «فترة مؤقتة دون أقساط» waits for V8.
  - Add the note: «اختيارك يساعدنا في الدراسة، والمسار المناسب يتحدد بعد دراسة طلبك.»
- **OA04 consent** (يحتاج مراجعة نظامية — V4):
  - Checkbox: «أوافق على أن تشارك منصة رهون مع [الجهة] البيانات والمستندات اللازمة لدراسة طلبي والتنسيق بشأنه.»
  - Description: «تُسجَّل موافقتك بتاريخها ونصها. يمكنك سحبها، ويتوقف عندها التنسيق مع جهتك.» The exact effect of withdrawal is V4.
  - Confirmed with an OTP (as in OR02). Show the recorded consent on OA05.
- **States:** institution selected / «جهة أخرى» typed; a duplicate warning «لديك طلب قائم لنفس التمويل» (V7 interim: warn and link); consent missing → «إرسال» disabled with the reason.
- **Acceptance:** OA01, OA03, OA04 and OA05 redrawn at 390; the duplicate-warning variant.

## D-3 «ماذا ستفعل رهون لك» + request tracking (OA06 successor)

- **Why:** the demo journey step «يعرف ما ستفعله رهون له», Q6 (no deadlines), Q8 (who acts), Q7/Q14 (several requests).
- **Exists:** `P1-Owner-Request-Mobile-Submitted` (OA06) and `InfoRequested` (OA07), in B13. The timeline pattern is from B6 D03.
- **New frames:**
  1. **Submission confirmation**, 390.
     - «وصل طلبك إلى فريق رهون» *(replaces «وصل طلبك إلى [الجهة]»)*
     - reference `REQ-…`
     - what happens next, in 3 bullets
  2. **Request home**, 390 + 1440 variant:
     - status chip
     - «ننتظر: فريق رهون / أنت / جهتك الممولة»
     - «الخطوة التالية» (NextAction card, no date)
     - **«ماذا ستفعل رهون لك»** card: the likely path(s) from the preference, labelled «مبدئي — يتأكد بعد الدراسة»; «ما نقوم به الآن»
     - a timeline of past events only
     - messages entry; «إضافة معلومة»; «سحب الطلب»
  3. **«طلباتي»** list, 390: several requests, each with reference, lender, status and «ننتظر».
  4. **State variants**, each showing the label, «ننتظر» and the next step:

     | State | Label (نص مقترح) | ننتظر |
     |---|---|---|
     | submitted | «مقدَّم» | فريق رهون |
     | team_review | «قيد دراسة فريق رهون» | فريق رهون |
     | info_requested | «نحتاج معلومة منك» | أنت |
     | lender_coordination | «قيد التنسيق مع جهتك الممولة» | جهتك الممولة |
     | offer_available | «وصل عرض من جهتك الممولة» | أنت |
     | response_recorded | «سجّلنا ردك» | فريق رهون |
     | closed | «مغلق» + outcome summary | — |
     | not_eligible | → D-6 | — |
     | withdrawn | «مسحوب» | — |

- **Acceptance:** all variants at 390; home at 1440; no future dates; each variant answers «ماذا أستفيد؟» in one sentence.

## D-4 Rahoon team workspace (new; no frames exist)

- **Why:** Q1/Q3/Q8. The Rahoon team reviews, obtains consent, coordinates with the lender manually, informs the individual, records the lender's offer (a second member verifies it) and relays the response. See the ADR §4.2–§4.5.
- **Shell:** desktop 1440, based on the `LenderShell` layout, with a new **«فريق رهون» sidebar**. Items: «الطلبات» (queue) · «مهامي» · «بحاجة إلى تحقق» (offers to verify) · «الاعتراضات والشكاوى» · «التقارير الداخلية». Header: «فريق رهون». 768: icon rail; 390: read-only list and detail.
- **Frames:**

  | Frame | Content |
  |---|---|
  | **T01 الطلبات** | Tabs: «مسندة إليّ» / «غير مسندة» / «الكل» (lead only). Columns: المرجع · مقدم الطلب (masked) · الجهة الممولة · الحالة · ننتظر · آخر تحديث · **مؤشر داخلي للمدة** (internal only, labelled «داخلي — لا يظهر للعميل», V5). Assign / reassign (lead) |
  | **T02 مراجعة الطلب** | **Header:** reference, status, «ننتظر», assigned coordinator. **Main column:** what the individual declared (source «العميل»); preference or paths; «بكلمات العميل»; documents with scan state; **consent evidence** (text version, time, recipient, withdrawn?). **Aside actions:** «طلب استكمال…», «بدء التنسيق مع الجهة» (disabled with the reason when there's no active consent), «غير مناسب للخدمة…» (reason, Q4 interim), «رسالة للعميل». **«ما سيراه العميل»** preview of the status, «ننتظر» and the next step being set |
  | **T03 طلب استكمال** (drawer) | items requested, message to the individual, preview |
  | **T04 سجل التنسيق مع الجهة الممولة** | Append-only list + «إضافة قيد». Fields: القناة (هاتف / بريد إلكتروني / خطاب رسمي / زيارة / أخرى) · التاريخ والوقت · الطرف لدى الجهة (الاسم/الصفة كما ذُكر) · الملخص · المرفقات · «يظهر للعميل؟» (off by default) + the text shown if on. Corrections are new entries. Wording is provisional (V2); a banner reads «التنسيق لا يعني تمثيلاً رسمياً للعميل» (V1) |
  | **T05 تسجيل عرض الجهة** | Upload or select the lender letter (required). Path P1/P2/P3. Typed terms per path (P1: new installment, term, start, conditions; P2: settlement amount, payment conditions, what is released or remains; P3: sale coordination terms). Lender reference and date; any validity **as stated by the lender** («بحسب خطاب الجهة»). Plain-language «أثره عليك» text. Preview as the individual sees it. «إرسال للتحقق» |
  | **T06 التحقق من العرض** | Shown to another team member (the recorder can't verify). Source document beside the recorded values, with a match checklist; «اعتماد ونشر للعميل» / «إعادة مع سبب». ReviewScreen pattern with step-up |
  | **T07 رد العميل ونقله للجهة** | the individual's response (accept / decline / question / counter) with reference and time; «تسجيل نقل الرد للجهة» creates a T04 entry; then «متابعة التنسيق» or «إغلاق الطلب» (outcome code + summary shown to the individual) |
  | **T08 الاعتراضات والشكاوى** (step 8) | P4 queue: objection to data or amounts, complaint, «إحالة لمختص» (manual record, V9) |

- **States:** empty queue; forbidden (not assigned); consent withdrawn (banner, coordination actions disabled with the reason); the offer returned by the verifier.
- **Acceptance:** T01–T07 at 1440, the T02 and T04 variants at 768, T08 as a list and detail.

## D-5 D06 help-paths explainer and D07 offer from the lender

- **Why:** Q11 (help paths, no guarantees), Q8 (the lender decides the offer, the individual decides), Q1 (the offer arrives over the manual channel and is recorded by the team).
- **Exists:**
  - B6 `P1-Debtor-Options-Mobile · D06` (tag «قائمة مؤقتة (Q11)»)
  - D07 `P1-Debtor-Offer-Mobile` (note «عرض راجعته واعتمدته جهتك الممولة…»)
  - D08/D09
- **D06 becomes «المسارات التي يمكن أن تساعدك بها رهون»:**
  - the four paths (text as in D-1)
  - which paths the team is studying for this request (from T02)
  - the footnote on no guarantees
  - no «متاح لك» wording
- **D07:**
  - eyebrow «عرض من جهتك الممولة»
  - note (نص مقترح): «سجّل فريق رهون هذا العرض من خطاب [الجهة] بتاريخ …، وتحقق منه عضو آخر من الفريق.»
  - link «عرض خطاب الجهة», if the letter is shared with the individual (propose yes)
  - keep «ليس نهائياً حتى توافق عليه»
  - per-path sections P1, P2 and P3 with the typed terms and «أثره عليك»
  - any lender validity shown as «بحسب خطاب الجهة»
  - actions: «أوافق» (→ D09 with OTP) · «لدي سؤال أو اقتراح» (D08) · «لا يناسبني»
- **Decline text:** B6's «لن يُتخذ أي إجراء قانوني بسبب رفضك» is a commitment → **يحتاج مراجعة نظامية (Q12, V4)**. The proposed neutral text is «سننقل ردك لجهتك الممولة ونبلغك بالخطوة التالية.»
- **Acceptance:** D06, and D07 in three variants (P1, P2, P3), plus D08/D09 adjusted for relaying through the team, all at 390.

## D-6 OA08: not suitable, lender response and withdrawal

- **Why:** the Rahoon team reviews (Q3); eligibility is open (Q4); a lender's response is relayed, not issued by Rahoon.
- **Exists:** `P1-Owner-Request-Mobile-Declined` (OA08).
- **Variants:**
  1. **«غير مناسب للخدمة حالياً»**, decided by the Rahoon team:
     - the reason in plain language, category «خارج نطاق الخدمة» (Q4 interim)
     - options: «تصحيح البيانات وإعادة الإرسال», «طلب لتمويل آخر», «الاعتراض على القرار» (P4), «تواصل مع فريق رهون»
  2. **«ردّت جهتك الممولة»**, e.g. no matching finance found:
     - the lender's answer quoted **as relayed by the team**, with the date the team received it
     - the same options
  3. **«سُحب الطلب»**: an archived view.
- **Tone:** no blame; «لم يُتخذ أي إجراء بخصوص تمويلك بسبب هذا الطلب» (نص مقترح — يحتاج مراجعة نظامية).
- **Acceptance:** 3 variants at 390.

---

## Status

| Request | Status | Needed by |
|---|---|---|
| D-1 | ⬜ sent to the design project? | Phase 1A step 4 |
| D-2 | ⬜ | step 5 |
| D-3 | ⬜ | step 5 |
| D-4 | ⬜ | step 6 (T01–T04), step 7 (T05–T07), step 8 (T08) |
| D-5 | ⬜ | step 7 |
| D-6 | ⬜ | step 5 (variant 1), step 6 (variant 2) |

If a request isn't designed by the step that needs it, engineering builds it with the existing design system and records «بانتظار اعتماد التصميم» in the phase findings (phase-1a step 2 rule).
