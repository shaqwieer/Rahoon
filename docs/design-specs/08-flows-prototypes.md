# 08 — Flows & Prototypes (9 clickable flows)

> **Updated design (2026-09-25):** a new first flow **PROTO-00 «الفرد يقدّم طلب معالجة»** (المالك ← الجهة الممولة ·
> مختلط; goal «المسار الأساسي: الفرد يسجل ويقدّم طلبه، ثم تراجعه الجهة المختارة وتفتح الحالة.») precedes the flows below,
> and PROTO-01 is renamed «فتح حالة يدوياً (استثناء)» · «مدير الحالات · مسار ثانوي». PROTO-00 steps are summarised in
> `B13-owner-initiated-journey.md` §8. The flows below are unchanged otherwise.

Source: `design-source/08 Flows & Prototypes.dc.html` (+ `_condensed/…txt`). All flow data lives in `static F` of the `<script type="text/x-dc">` class.

**Tagging convention:** statements come from the design unless marked **(inferred)**. The screen-ID mapping per step is **(inferred)** from each step's title, role and the Brief matrix. The CaseState enum names come from `09 Handoff`.

Sidebar header: «08 النماذج التفاعلية» · «9 تدفقات قابلة للنقر · بيانات خيالية».

---

## A. Prototype harness (review tooling — NOT product behaviour)

These parts are review scaffolding and are not built into the app. They are useful as an E2E/Storybook harness pattern.

- **Layout:** grid `320px 1fr`.
  - Sticky left aside: logo; flow nav (9 buttons: number `n` in LTR, title `t`, roles `r`; current one gets `aria-current="page"` and a `#FDF0EB` background); state-simulator radiogroup; links «00 الموجز», «09 التسليم».
  - Main: the header, then the step dots, then the device frame, then 3 info cards.
- **Header:**
  - `protoId` = `PROTO-{n} · الخطوة {si+1} من {steps.length}`, with H1 = flow title and a subtitle = flow goal.
  - Buttons:
    - «رجوع» (icon arrow_forward): pops the history. Disabled when `hist.length <= 1`.
    - «من البداية» (icon restart_alt): `hist=[0]`.
- **Step dots** (`<ol aria-label="خطوات التدفق">`):
  - One pill per step, `"{i+1}. {t}"`. The current one has `aria-current="step"` and a black fill.
  - Visited steps show white; unvisited are transparent.
  - The dots list **every** step in the array, including alternative-branch steps, so «n من N» counts branch steps too.
- **Device frame:**
  - `d:'m'` → mobile 390×760, rounded 28, 8px bezel.
  - `d:'d'` → desktop, max 1000, min-height 600; content max-width 680, centred.
  - `data-screen-label` = `PROTO-{n}-{Mobile|Desktop}-Step{si+1}-{mode}`.
  - Frame top bar: symbol logo, `scr.t` (bold), `scr.who`, `scr.ref` (case ref, LTR).
- **Case ref per flow:**

| flow | case ref |
|---|---|
| f6 | RH-2026-003988 |
| f7 | RH-2026-004012 |
| f8 | RH-2026-003511 |
| f9 | RH-2026-003702 |
| all others | RH-2026-004172 |

- **Step render order inside the frame:**
  1. error alert (mode)
  2. tag chip
  3. H2 `h`
  4. `sub`
  5. `rows` (key/value table)
  6. `fields`
  7. `list` (icon + text)
  8. `callout`
  9. success status (mode)
  10. actions
- **Tones** (`TONE`):
  - ok `#1E6A45/#EAF4EE/#9CCBB0`
  - warn `#8A5300/#FBF2DE/#E2C27A`
  - err `#B3261E/#FCECEA/#EFA59C`
  - info `#1D5A8C/#EAF2F9/#9DC0DE`
  - neutral `#22262A/#F2F1ED/#CBCAC6`
- **Fields:** label + value box.
  - A field with an error text gets a 2px `#B3261E` border and an error line (icon `error` + text).
  - Box min-height is 50 on mobile and 44 on desktop. Font is 16 on mobile and 15 on desktop.
- **Actions:** `[label, target, kind]`.
  - `kind 'p'` = primary: rust `#AA4528`, white text, height 52 on mobile / 44 on desktop.
  - `kind 's'` = secondary: white with a `#85847F` border, height 48 / 40.
- **Transition semantics** (`go(to)`):
  - `'next'` → current index + 1.
  - `'done'` → reset history to `[0]` (end of flow).
  - `'exit:fX'` → switch to flow fX, step 0, with a fresh history.
  - `'<id>'` → jump to the step whose `id` matches.
  - Any out-of-range index falls back to 0.
  - History (`hist`) is an index stack. The «المسار المتّبع» card shows `hist` titles joined by « ← ».
- **Persistence:** `localStorage['rahoon-proto'] = {flow, hist}`, wrapped in try/catch. The **mode is not persisted**, and it resets to `default` on every navigation, flow switch, back or restart.
- **Info cards under the frame:**
  - «الحالة التجارية» = step `biz`. This is the business/case-state assertion.
  - «الإطار المرجعي» = a link to the source design file + frame label.
  - «المسار المتّبع» = the trail.

### Simulated system states («محاكاة الحالة»)

Radiogroup `aria-label="حالة الشاشة"`: «افتراضي» `default` · «تحميل» `loading` · «خطأ» `error` · «دون اتصال» `offline` · «ممنوع» `forbidden` · «نجاح» `success`. Helper text: «تنطبق على الشاشة الحالية لعرض التحميل والخطأ ودون اتصال والمنع والنجاح.»

These map to C11 SystemState, which the product must implement on every screen.

| mode | behaviour | copy |
|---|---|---|
| default | content shown | — |
| loading | content **hidden**; skeleton of 5 bars, `aria-busy="true"` | — |
| error | content shown, with a `role="alert"` banner above it (error tone) and a retry button | «**تعذّر إكمال الإجراء.** لم يتغير شيء. المرجع ERR-2F71.» + button «إعادة المحاولة» (back to default) |
| offline | amber banner under the frame bar (icon `wifi_off`); content shown; **primary actions disabled** (grey `#E4E3DF/#6B6A65`); secondary actions stay enabled | «أنت غير متصل. تعرض آخر نسخة محفوظة؛ الإجراءات معلقة.» |
| forbidden | content **hidden**; `role="status"` block (icon `visibility_off`) | «لا يمكنك فتح هذه الصفحة» / «قد لا تكون موجودة أو أنها خارج صلاحياتك. لم نعرض أي بيانات عنها.» + button «طلب وصول» (no handler) |
| success | content shown, with a dark `role="status"` banner (icon `check_circle`) after the callout | «تم الحفظ وسُجّل في سجل الحالة.» |

**Product-level acceptance for the system states** (applies to every flow; inferred):
- **Loading:** shows a skeleton and never partial data.
- **Error:** states that nothing changed, shows an error reference ID and offers a retry. The failed mutation leaves the state unchanged, and the attempt is audit-logged per the Handoff rule "كل انتقال … يُسجل حتى المرفوض".
- **Offline:** shows the cached last version. Mutating (primary) actions are disabled, or queued as pending.
- **Forbidden:** reveals no data about the resource, including its existence, and offers «طلب وصول».
- **Success:** confirms that the change was saved and logged in the case audit.

---

## B. Flows

Legend for step tables:
- **vp** = viewport (m = mobile 390, d = desktop).
- **Actions:** `label → target [p|s]`.
- **biz** = the case/business-state effect, which the E2E test asserts.

### PROTO-01 · f1 — «فتح حالة جديدة» (create a new case)
- **Roles:** «مدير الحالات · سطح المكتب» (سارة القحطاني, مديرة حالات).
- **Goal:** «إنشاء حالة صحيحة وغير مكررة، بحالة «مسودة» ثم «بانتظار البيانات».»
- **Reference:** `03 Phase 0 — Anchor Screens.dc.html` · `P0-Lender-CreateCase`. Case RH-2026-004172.

| # | id | screen · who · vp (screen ref, inferred) | content | biz | actions |
|---|---|---|---|---|---|
| 1 | | الحالات · سارة القحطاني · مديرة حالات · d (L02) | H «قائمة الحالات»; sub «38 حالة مسندة إليك.»; rows: تحتاج إجرائي=7, متأخرة=42 | لا تغيير. | حالة جديدة → next [p] |
| 2 | | حالة جديدة · 1 من 6 · العقد والمنتج · d (L03) | H «بيانات العقد»; fields: رقم عقد التمويل=MF-88-3317•••, المنتج=تمويل سكني · مرابحة; callout **warn** (content_copy): «تكرار محتمل: العقد مرتبط بحالة مغلقة RH-2026-003901 (2025-12-04).» | مسودة محفوظة تلقائياً. | المتابعة بحالة جديدة مع سبب → next [p]; فتح الحالة السابقة → exit:f1 [s] |
| 3 | | حالة جديدة · 2 من 6 · المالك والأطراف · d (L03) | H «المالك الأساسي»; fields: الاسم كما في الهوية=عبدالله محمد السـ…; رقم الهوية الوطنية=1098••• **err** «10 أرقام مطلوبة، أُدخل 7»; الجوال=+966 66 214 81 **err** «يجب أن يبدأ بـ 05»; callout **err** (error): «يوجد خطآن في هذه الخطوة.» | مسودة · التحقق يمنع المتابعة. | تصحيح الحقول → next [p] |
| 4 | | حالة جديدة · 2 من 6 · المالك والأطراف · d (L03) | same H; fields corrected: 1•••••••42, +966 5• ••• ••81; list ok: «لا أخطاء» | مسودة. | التالي (الخطوات 3–5) → next [p] |
| 5 | | حالة جديدة · 6 من 6 · المراجعة والإنشاء · d (L03 review) | H «مراجعة قبل الإنشاء»; rows: المنشأة=مصرف الأفق; المالك=عبدالله م. · 1•••••••42; العقار=فيلا · النرجس، الرياض; القائم=1,284,560.00 ر.س · نظام التمويل; callout **info**: «لن يصل أي تواصل للمالك قبل إرسال الدعوة يدوياً.» | مسودة ← بانتظار البيانات عند الإنشاء. | إنشاء الحالة → next [p] |
| 6 | | RH-2026-004172 · مساحة عمل الحالة · d (L05) | tag **info** «بانتظار البيانات»; H «أُنشئت الحالة»; sub «الإجراء التالي: طلب المستندات من المالك وإرسال الدعوة.»; list: ok «سُجّل الإنشاء في السجل مع سبب تجاوز التكرار»; neutral (person) «المسؤولة: سارة القحطاني» | بانتظار البيانات · مهلة 5 أيام عمل. | إنهاء التدفق → done [s] |

- **Not designed:** «فتح الحالة السابقة» (`exit:f1`) just restarts the flow. It should navigate to RH-2026-003901.
- **Acceptance criteria:**
  - The wizard autosaves the draft (state `draft`).
  - A duplicate contract linked to an existing (closed) case shows the duplicate warning. Continuing requires a reason, and that reason is written to the audit.
  - National ID must be 10 digits; the mobile must start with 05. Invalid fields show inline errors plus an error summary, and block «التالي».
  - The review step shows the org, the masked ID, the property and the outstanding amount with its source (نظام التمويل).
  - No owner communication is sent on creation.
  - Creating the case gives `draft → awaiting_data`, a new ref `RH-YYYY-NNNNNN`, the assignee set to the creator, and an SLA of 5 business days.
  - The audit contains the creation and the duplicate-override reason.

### PROTO-02 · f2 — «طلب مستند ورفعه» (request and upload a document)
- **Roles:** «الجهة ↔ المالك · مختلط». Desktop: سارة القحطاني. Mobile: عبدالله م.
- **Goal:** «طلب مستند بمهلة واضحة، ورفعه، ومراجعته، وإعادة الطلب بسبب مفهوم.»
- **Reference:** `04 Phase 1 — B3 Case Tabs.dc.html` · `P1-Lender-CaseDocuments`. Case RH-2026-004172.

| # | id | screen · who · vp (ref) | content | biz | actions |
|---|---|---|---|---|---|
| 1 | | طلب مستند · سارة القحطاني · الجهة · d (L10) | H «طلب: كشف الراتب لآخر 3 أشهر»; rows: من يرفعه=المالك عبر البوابة; المهلة=2026-09-21 · 9 ربيع الآخر 1448هـ; callout **neutral** (visibility): «معاينة ما سيراه المالك: «نحتاج كشف راتبك لآخر 3 أشهر لنقترح قسطاً يناسب دخلك.»» | مهمة للمالك + إشعار. | إرسال الطلب → next [p] |
| 2 | | المستندات المطلوبة · عبدالله م. · المالك · جوال · m (D04) | H «مستند جديد مطلوب»; sub «كشف الراتب لآخر 3 أشهر · حتى 2026-09-21» | — | رفع ملف → next [p]; أحتاج مساعدة → exit:f6 [s] |
| 3 | | رفع المستند · عبدالله م. · جوال · m (D04) | H «تأكيد الرفع»; rows: الملف=كشف_الراتب.jpg · 2.1 م.ب; الفحص=سليم | مرفوع · قيد المراجعة. | إرسال → next [p] |
| 4 | | مراجعة المستند · سارة القحطاني · الجهة · d (L10) | H «كشف الراتب v1»; sub «رفعه المالك 2026-09-12.» | — | قبول → ok [p]; رفض مع سبب… → next [s] |
| 5 | | رفض المستند · سارة القحطاني · d (L10) | H «سبب الرفض (يُرسل للمالك)»; field السبب=«الصفحة الثانية غير واضحة. جرّب PDF من تطبيق البنك.» | v1 مرفوض ويُحفظ · الطلب يُعاد. | رفض وإبلاغ المالك → next [p] |
| 6 | | المستندات المطلوبة · عبدالله م. · جوال · m (D04) | tag **err** «يحتاج رفعاً من جديد»; H «لماذا نحتاجه مرة أخرى؟»; sub «الصفحة الثانية غير واضحة. جرّب ملف PDF من تطبيق البنك.» | — | رفع v2 → ok [p] |
| 7 | ok | المستندات · فريق الحالة · d (L10) | tag **ok** «متحقق»; H «كشف الراتب v2 مقبول»; list: ok «قبلته سارة القحطاني 2026-09-20»; neutral (history) «الإصدار v1 محفوظ في السجل» | يرفع حاجز الانتقال إلى «حل مقترح». | إنهاء التدفق → done [s] |

- **Acceptance criteria:**
  - The request sets who uploads it and a dual-calendar due date. It shows the exact owner-facing wording, and creates an owner task plus a notification.
  - The owner sees the requested document with its due date. «أحتاج مساعدة» routes to help/complaint.
  - The upload is virus-scanned («الفحص: سليم»), then the document status becomes `uploaded/in review`.
  - The reviewer can accept, or reject with a mandatory reason that is sent to the owner.
  - A rejected v1 is kept, and the request reopens.
  - The owner sees «يحتاج رفعاً من جديد» plus the reason. v2 uploads as a new version.
  - Accepting sets `verified`, records the acceptor and date, and keeps v1 in history.
  - A verified document satisfies the `proposed_solution` gate.
  - Accept after v1 (step 4 → ok) skips the rejection path.

### PROTO-03 · f3 — «مراجعة الحل واعتماده» (solution review and approval)
- **Roles:** «المحلل ← المراجِع ← المعتمد»:
  - فهد العتيبي (محلل)
  - سارة القحطاني (مراجِعة)
  - نورة الشهري (معتمدة)
- **Goal:** «فصل المهام: المُعِدّ لا يعتمد، والقرار بسبب وأثر ورمز تحقق.»
- **Reference:** `03 Phase 0 — Anchor Screens B.dc.html` · `P0-Lender-SolutionBuilder`. Case RH-2026-004172.

| # | id | screen · who · vp (ref) | content | biz | actions |
|---|---|---|---|---|---|
| 1 | | منشئ الحل v2 · فهد العتيبي · محلل · d (L13) | H «قسط 15,074.52 ر.س · 84 شهراً»; rows: الاستقطاع=46.5% ✓ (الحد 55%); التنازل=18,300.00 (1.42%) | حل مقترح. | تسليم لمديرة الحالة → next [p] |
| 2 | | مراجعة قبل الإرسال · سارة القحطاني · مراجِعة · d (L15 / ReviewScreen) | H «ما سيحدث»; list neutral: (swap_horiz) «حل مقترح ← موافقة داخلية»; (edit_off) «يُقفل v2 للتعديل»; (person) «يُسند إلى نورة الشهري · 3 أيام»; field ملاحظة للمعتمد=«القسط ضمن الحد والتنازل محصور في الغرامات.» | حل مقترح → موافقة داخلية. | إرسال للموافقة → next [p] |
| 3 | | الموافقات · نورة الشهري · معتمدة · d (L16) | tag **warn** «بانتظار قرارك»; H «إعادة جدولة 84 شهراً — عبدالله م.»; rows: المبلغ=1,266,260.00 · ضمن حدك; التنازل=1.42% · حدك 5% | موافقة داخلية. | اعتماد وإرسال للمالك… → next [p]; إعادة للتعديل… → ret [s] |
| 4 | | تأكيد الاعتماد · نورة الشهري · d (ReviewScreen + MFA) | H «أدخل رمز التحقق»; fields: السبب=«قابل للسداد وضمن حدودي.»; الرمز=«• • • • • •»; callout **info**: «عند الاعتماد: بانتظار العميل، ويُرسل العرض صالحاً 10 أيام.» | — | تأكيد → app [p] |
| 5 | app | RH-2026-004172 · مساحة العمل · d (L05) | tag **info** «بانتظار العميل»; H «اعتُمد الحل v2 وأُرسل للمالك»; list: ok «القرار مسجّل بسببه ووقته»; warn (schedule) «صلاحية العرض حتى 2026-10-03» | بانتظار العميل. | إنهاء التدفق → done [s] |
| 6 | ret | إعادة للتعديل · نورة الشهري · d (L16 return) | H «سبب الإعادة»; field السبب=«أرجو إرفاق تعريف الراتب بجانب الكشف.» | موافقة داخلية ← حل مقترح (v2 يبقى محفوظاً). | إعادة للمحلل → retd [p] |
| 7 | retd | منشئ الحل · فهد العتيبي · d (L13) | tag **err** «أُعيد للتعديل»; H «ملاحظة المعتمد»; sub «أرجو إرفاق تعريف الراتب بجانب الكشف.» — أي تعديل ينشئ v3. | حل مقترح. | إنهاء التدفق → done [s] |

- **Acceptance criteria:**
  - Submitting for approval gives `proposed_solution → internal_approval`, locks solution v2 and assigns the approver with a 3-day SLA.
  - Handoff guard: `solution_locked, analysis, valuation_valid`. The actor is preparer or reviewer.
  - The approver sees the amount and waiver against their personal limits (≤2,000,000; waiver ≤5%).
  - Guard: `approver != preparer && amount <= limit`.
  - Approval requires a reason plus MFA. It gives `internal_approval → awaiting_customer`, the offer is sent with 10-day validity (until 2026-10-03), and the decision is logged with its reason and time.
  - Return requires a reason. It gives `internal_approval → proposed_solution`, keeps v2 stored, and shows the note to the analyst. Any edit creates v3.
  - Negative test: the preparer (فهد) cannot approve their own solution.

### PROTO-04 · f4 — «رد المالك: قبول أو رفض أو بديل» (owner response)
- **Roles:** «المالك · جوال» (عبدالله م.). All steps are mobile.
- **Goal:** «قرار واعٍ ومسجّل؛ الرفض لا يؤدي إلى إحالة.»
- **Reference:** `04 Phase 1 — B6 Debtor Journey.dc.html` · `P1-Debtor-Offer-Mobile`. Case RH-2026-004172.

| # | id | screen · vp (ref) | content | biz | actions |
|---|---|---|---|---|---|
| 1 | | العرض المقدم لك · عبدالله م. · m (D07) | H «قسطك الجديد 15,074.52 ريال»; sub «يوم 10 من كل شهر · 84 شهراً · تُلغى غرامات التأخير 18,300 ريال.»; callout **neutral**: «إذا تأخرت قسطين متتاليين نتواصل معك أولاً ونمنحك 15 يوماً.» | بانتظار العميل. | مراجعة وقبول → acc [p]; اقتراح بديل → cnt [s]; لا يناسبني → dec [s] |
| 2 | acc | قبل أن توافق · m (D09) | H «تأكيد الموافقة»; list (check_box): «قرأت الشروط وفهمت ما يحدث إذا تأخرت»; «أوافق باختياري»; field رمز التأكيد=«7 2 0 5 1 •» | — | أوافق على العرض → accd [p] |
| 3 | accd | تمت الموافقة · m (D09) | tag **ok** «مسجّلة»; H «شكراً، سجّلنا موافقتك»; rows: المرجع=AGR-2026-004172-01; الوقت=2026-10-02 14:21 | بانتظار العميل ← تسوية معتمدة (بعد مراجعة القانونية). | إنهاء التدفق → done [s] |
| 4 | cnt | اقتراح بديل · m (D08) | H «ما الذي تود تغييره؟»; fields: يوم القسط=10; البدء من=ديسمبر; السبب (اختياري)=«راتبي يتأخر أحياناً إلى يوم 5.» | — | إرسال الاقتراح → cntd [p] |
| 5 | cntd | أُرسل اقتراحك · m (D08) | tag **info** «قيد المراجعة»; H «سنرد خلال 3 أيام عمل»; sub «اقتراحك طلب وليس اتفاقاً. قد يُقبل أو يُعدَّل.» | بانتظار العميل ← تفاوض. | إنهاء التدفق → done [s] |
| 6 | dec | لا يناسبني · m (D07 decline) | H «ساعدنا نفهم»; field السبب (اختياري)=«القسط ما زال مرتفعاً.»; callout **info**: «لن يُتخذ أي إجراء قانوني بسبب رفضك. سنتواصل لنبحث خياراً آخر.» | — | إرسال → decd [p]; أحتاج مساعدة → exit:f6 [s] |
| 7 | decd | وصلنا ردك · m | tag **info** «قيد المتابعة»; H «سيتواصل معك فريق حالتك»; sub «خلال يومي عمل لبحث خيار آخر.» | بانتظار العميل ← حل مقترح (لا إحالة). | إنهاء التدفق → done [s] |

- **Acceptance criteria:**
  - **Accept:** requires both acknowledgements plus an OTP. It records the consent (ref `AGR-{case}-NN`, timestamp). The state goes to `active_settlement` only after legal review.
  - **Counteroffer:** fields are day, start month and an optional reason. It is presented as a request, not an agreement, with a response SLA of 3 business days. The state goes `awaiting_customer → negotiation`.
  - **Decline:** the reason is optional. The copy guarantees no legal action. The state goes `awaiting_customer → proposed_solution`.
    - It must never produce `judicial_referral` (Handoff: «decline never leads to referral»). Contact is due within 2 business days.
  - Help from decline routes to the complaint/help flow (f6).

### PROTO-05 · f5 — «تسجيل دفعة» (record a payment)
- **Roles:** «المالية · صانع ومدقق»:
  - ريم الدوسري (maker)
  - عبدالرحمن ش. (مدقق مالي, checker)
  - the owner, on mobile
- **Goal:** «تسجيل يدوي بمرجع فريد، ومطابقة من شخص آخر قبل أن يراها المالك «مستلمة».»
- **Reference:** `04 Phase 1 — B4 Solutions & Agreement.dc.html` · `P1-Lender-PaymentSchedule`. Case RH-2026-004172.

| # | id | screen · who · vp (ref) | content | biz | actions |
|---|---|---|---|---|---|
| 1 | | جدول السداد · ريم الدوسري · المالية · d (L20) | H «القسط 3 · 2027-02-10»; rows: المبلغ=15,074.52 ر.س; الحالة=مستحق | تسوية نشطة. | تسجيل دفعة → next [p] |
| 2 | | تسجيل دفعة · ريم الدوسري · d (L20) | H «بيانات التحويل»; fields: المبلغ=15,074.52; مرجع التحويل=TRX-88410027 **err** «المرجع مستخدم في القسط 2 — تحقق منه» | — | تصحيح المرجع → next [p] |
| 3 | | تسجيل دفعة · ريم الدوسري · d (L20) | same H; fields: 15,074.52; TRX-88457310; list: ok «المرجع فريد»; neutral (attach_file) «كشف_حساب_فبراير.pdf» | — | تسجيل وإرسال للمطابقة → next [p] |
| 4 | | بانتظار المطابقة · عبدالرحمن ش. · مدقق مالي · d (L20 match) | tag **warn** «مسجلة — بانتظار المطابقة»; H «مطابقة الدفعة»; rows: سجّلتها=ريم الدوسري; الكشف البنكي=15,074.52 · 2027-02-08 | المالك يراها «قيد التأكيد». | مطابقة → next [p]; رفض مع سبب → exit:f5 [s] |
| 5 | | المدفوعات · عبدالله م. · جوال · m (D10) | tag **ok** «مستلم»; H «القسط 3 مستلم»; sub «المرجع TRX-88457310 · الإيصال متاح للتنزيل.» | مطابقة · المتبقي 1,236,110.96 ر.س. | إنهاء التدفق → done [s] |

- **Not designed:** «رفض مع سبب» (`exit:f5`) just restarts.
- **Balance check:** 1,266,260.00 − 2 × 15,074.52 = **1,236,110.96**. Per the working notes, installment 1 is matched (TRX-88392214), installment 2 (TRX-88410027) is recorded but pending match, and installment 3 is now matched.
  - Rule: **only matched payments reduce the remaining balance** (inferred; the numbers are consistent with it).
- **Acceptance criteria:**
  - The transfer reference must be unique per case/org. Reusing one used on another installment shows an inline error and blocks saving.
  - An evidence attachment is recorded.
  - Recording sets the payment to `recorded_pending_match`. The owner sees «قيد التأكيد», not «مستلم».
  - Matching must be done by a different user than the recorder (maker ≠ checker), against the bank-statement line (amount and date).
  - After matching, the owner sees «مستلم» with the reference and a downloadable receipt, and the remaining balance updates.

### PROTO-06 · f6 — «تقديم شكوى» (submit a complaint)
- **Roles:** «المالك ← الامتثال»: منيرة ع. (owner, mobile) and هند المطيري (الامتثال, desktop).
- **Goal:** «شكوى مستقلة برقم ومهلة ورد مكتوب، تحجب الإحالة أثناءها.»
- **Reference:** `04 Phase 1 — B5 Comms, Referral & Closure.dc.html` · `P1-Lender-Complaint`. Case **RH-2026-003988**.
- **Entry points:** it is also the target of `exit:f6` from f2 («أحتاج مساعدة»), f4 («أحتاج مساعدة») and f8 («تقديم اعتراض»).

| # | id | screen · who · vp (ref) | content | biz | actions |
|---|---|---|---|---|---|
| 1 | | شكوى أو اعتراض · منيرة ع. · جوال · m (D13) | H «اشرح لنا ما حدث»; fields: النوع=شكوى على طريقة التعامل; التفاصيل=«رُفض مستندي دون أن أفهم السبب.»; callout **neutral**: «تراجعها جهة مستقلة عن فريق حالتك خلال 5 أيام عمل.» | — | إرسال → next [p] |
| 2 | | وصلت شكواك · جوال · m (D13) | tag **info** «مستلمة»; H «رقم الشكوى CMP-2026-0142»; rows: الرد المتوقع=حتى 2026-09-25 | تحجب الإحالة والإلغاء · توقف مهلة رد المالك. | متابعة → next [p] |
| 3 | | CMP-2026-0142 · هند المطيري · الامتثال · d (L23 / PA10) | tag **info** «قيد المراجعة»; H «القرار والرد»; fields: القرار=مقبولة جزئياً; الرد=«نعتذر عن عدم الوضوح… مددنا المهلة حتى 2026-10-10.» | — | إرسال الرد → next [p] |
| 4 | | رد على شكواك · منيرة ع. · جوال · m (D13) | tag **ok** «مغلقة · مقبولة جزئياً»; H «قرأنا شكواك وتصرفنا»; sub «مددنا مهلة العرض حتى 2026-10-10، والمطلوب كشف يظهر فيه اسمك ورقم الحساب.» | مغلقة. | مقتنع بالرد → done [p]; طلب إعادة النظر → exit:f6 [s] |

- **Not designed:** «طلب إعادة النظر» (appeal) restarts the flow.
- **Acceptance criteria:**
  - The complaint gets a reference `CMP-YYYY-NNNN` and a 5-business-day response SLA. It is routed to an independent compliance reviewer, not the case team.
  - While it is open:
    - `judicial_referral` and cancellation are blocked (guard `no_open_complaint`);
    - the owner's response timer (offer validity) is paused;
    - O03: no reminders are sent.
  - The reviewer records a decision (e.g. «مقبولة جزئياً») and a written reply. The reply is delivered to the owner, and the complaint becomes `closed` with its outcome.
  - Remedial effects (the offer extension to 2026-10-10) are applied and logged.
  - The owner can accept the reply or request reconsideration.

### PROTO-07 · f7 — «البيع الطوعي» (voluntary sale)
- **Roles:** «المالك + الجهة · مختلط»: عائشة ف. (owner, mobile), نورة الشهري (معتمدة) and سارة القحطاني.
- **Goal:** «يبدأ بموافقة صريحة ويمكن الانسحاب منه حتى قبول عرض.»
- **Reference:** `05 Phase 2 — B8 Voluntary Sale.dc.html` · `P2-Lender-VoluntarySale`. Case **RH-2026-004012**.

| # | id | screen · who · vp (ref) | content | biz | actions |
|---|---|---|---|---|---|
| 1 | | البيع الطوعي · عائشة ف. · جوال · m (D15) | H «ما يعنيه لك»; list neutral: (payments) «يُسدَّد التمويل من الثمن ويعود لك الفائض»; (undo) «يمكنك الانسحاب حتى قبول عرض» | تفاوض. | مراجعة الموافقة → next [p] |
| 2 | | موافقتك على البيع · جوال · m (D15) | H «أقل سعر تقبلينه»; field الحد الأدنى=2,700,000 ريال | — | تأكيد برمز التحقق → next [p] |
| 3 | | قرار البيع الطوعي · نورة الشهري · معتمدة · d (L27) | H «فتح مسار البيع»; rows: الفائض المتوقع للمالكة=545,000 ر.س (تقديري) | تفاوض ← بيع طوعي. | اعتماد → next [p] |
| 4 | | عروض الشراء · عائشة ف. · جوال · m (D16) | H «وصلت 3 عروض»; rows: أعلى نقدي=2,800,000; أعلى بشرط تمويل=2,820,000 | بيع طوعي. | أوافق على العرض النقدي → next [p]; أريد الانسحاب → exit:f7 [s] |
| 5 | | تتبع البيع · سارة القحطاني · d (L33) | tag **ok** «بيع طوعي»; H «OF-02 معتمد»; list: ok «موافقة المالكة + اعتماد المصرف»; warn (schedule) «نقل الملكية — خارجي، يُدخل يدوياً» | ← بانتظار التسوية المالية بعد استلام الثمن. | إنهاء التدفق → done [s] |

- **Not designed:** withdrawal («أريد الانسحاب») restarts the flow. Expected behaviour: withdraw before an offer is accepted and return to `negotiation`.
- **Acceptance criteria:**
  - The owner's explicit consent, with an OTP and a minimum acceptable price, is required before the approver can open the sale path. The state goes `negotiation → voluntary_sale`.
  - The expected surplus is shown and labelled «(تقديري)».
  - The owner can withdraw until they accept an offer. Once the owner accepts and the bank approves, OF-02 is `approved`.
  - The title transfer is external and entered manually.
  - `voluntary_sale → awaiting_reconciliation` happens only after the proceeds are received.

### PROTO-08 · f8 — «الإحالة القضائية اليدوية» (manual judicial referral)
- **Roles:** «القانونية + معتمد»: ماجد الحربي (القانونية), فيصل ر. (owner, mobile) and نورة الشهري (معتمدة).
- **Goal:** «قرار منفصل بحواجز واضحة، ومرجع خارجي يدوي منفصل عن حالة المنصة.»
- **Reference:** `04 Phase 1 — B5 Comms, Referral & Closure.dc.html` · `P1-Legal-ReferralReadiness`. Case **RH-2026-003511**, which continues in B10 J01–J04.

| # | id | screen · who · vp (ref) | content | biz | actions |
|---|---|---|---|---|---|
| 1 | | جاهزية الإحالة · ماجد الحربي · القانونية · d (L25 / J01) | tag **err** «محجوب»; H «بندان ناقصان»; list err (cancel): «إشعار المالك المسبق وحقوقه»; «انقضاء مهلة الاعتراض» | حل مقترح (لا تغيير). | إرسال الإشعار المسبق → next [p] |
| 2 | | إشعار قبل الإحالة · فيصل ر. · جوال · m (D02/D12 notice) | H «إشعار مهم بشأن حالتك»; sub «يدرس المصرف إحالة حالتك للجهة المختصة. يحق لك الاعتراض خلال 15 يوماً أو طلب حل آخر.» | مهلة اعتراض 15 يوماً (افتراض). | (بعد 15 يوماً دون اعتراض) → next [p]; تقديم اعتراض → exit:f6 [s] |
| 3 | | طلب اعتماد الإحالة · ماجد الحربي · d (L25 → ReviewScreen) | H «مراجعة الأثر»; list neutral: (swap_horiz) «← إحالة قضائية»; (block) «تُعلّق العروض القائمة»; (description) «تُقفل حزمة الأدلة»; field السبب=«استنفاد الحلول الودية ورفض البيع الطوعي كتابياً.» | — | إرسال للاعتماد → next [p] |
| 4 | | اعتماد الإحالة · نورة الشهري · معتمدة · d (L16 / ReviewScreen) | H «القرار»; callout **warn** (warning): «قرار عالي الأثر · موافقتان · رمز تحقق.» | — | اعتماد → next [p] |
| 5 | | المرجع الخارجي · ماجد الحربي · d (L25 / J03 manual) | tag **neutral** «إحالة قضائية»; H «تسجيل مرجع الجهة»; fields: رقم الطلب لدى الجهة المختصة=EXT-JD-2026-•••8841; الحالة الرسمية=«تم استلام الطلب» · إدخال يدوي; callout **neutral**: «الحالة الرسمية تُعرض منفصلة عن حالة المنصة، بمصدرها ووقتها.» | إحالة قضائية. | حفظ → done [p] |

- **Harness note:** «(بعد 15 يوماً دون اعتراض)» is a time-skip in the prototype. In the product the step becomes available only when the guard is satisfied.
- **Acceptance criteria:**
  - Readiness is blocked (the state stays `proposed_solution`) until the prior notice is sent **and** the objection period has elapsed. Blocked items are listed.
  - Referral guard (Handoff): `no_open_complaint && notice_sent && objection_period_elapsed`. There is **no automatic transition** to referral.
  - The owner receives the notice with the objection right (15 days, an assumption) and can object; objecting routes to f6 and blocks referral.
  - The request shows the impact:
    - state → `judicial_referral`
    - open offers suspended
    - evidence package locked
    - a mandatory reason
  - Approval is a high-impact decision: two approvals (legal + approver, which must be distinct users) plus MFA.
  - After approval, the state is `judicial_referral`. The external reference and official status are entered **manually** and stored verbatim, with source «إدخال يدوي» and a timestamp, separate from the platform state.
  - Negative: an attempt with an open complaint is rejected with a reason, and the rejected attempt is audit-logged.

### PROTO-09 · f9 — «التسوية المالية والإغلاق» (financial reconciliation and closure)
- **Roles:** «المالية ← المدقق ← المعتمد»: ريم الدوسري (المالية), نورة الشهري (معتمدة) and تركي ب. (owner, mobile).
- **Goal:** «إغلاق بفرق صفري ومصادر قابلة للتتبع ومستندات للمالك.»
- **Reference:** `04 Phase 1 — B5 Comms, Referral & Closure.dc.html` · `P1-Finance-ReconcileClose`. Case **RH-2026-003702**. The integrated variant is B10 F01–F04.

| # | id | screen · who · vp (ref) | content | biz | actions |
|---|---|---|---|---|---|
| 1 | | المطابقة · ريم الدوسري · المالية · d (L26 / F01) | tag **err** «فرق 1,000.00»; H «المطابقة غير متوازنة»; rows: المتفق عليه=455,210.75; المستلم=454,210.75 | بانتظار التسوية المالية. | ربط التحويل الناقص → next [p] |
| 2 | | المطابقة · ريم الدوسري · d (L26 / F01) | tag **ok** «الفرق 0.00»; H «المطابقة متوازنة»; list ok: «TRX-88201744 · 300,000.00»; «TRX-88355102 · 155,210.75» | — | إرسال للتدقيق والاعتماد → next [p] |
| 3 | | مراجعة الإغلاق · نورة الشهري · معتمدة · d (L26 review / F04) | H «ما سيحدث»; list neutral: (lock) «تصبح الحالة مغلقة نهائياً»; (description) «يستلم المالك المخالصة وفك الرهن»; (schedule) «وصول المالك قراءة 90 يوماً (افتراض)» | التدقيق مكتمل. | اعتماد الإغلاق → next [p] |
| 4 | | أُغلقت حالتك · تركي ب. · جوال · m (D14) | tag **ok** «مغلقة»; H «هذه مستنداتك»; list (description): «خطاب المخالصة النهائية»; «خطاب فك الرهن»; «ملخص حالتك» | مغلقة. | إنهاء التدفق → done [s] |

- **Formula:** `difference = agreed − received`. 455,210.75 − 454,210.75 = 1,000.00, which is unbalanced. After linking both transfers (300,000.00 + 155,210.75 = 455,210.75), the difference is 0.00.
- **Acceptance criteria:**
  - Reconciliation shows a non-zero difference as an error and blocks submission.
  - Linking the missing bank transfer(s) brings the difference to 0.00. Each transfer is listed with its TRX ref and amount.
  - Submission to audit and approval is allowed only at a zero difference. The checker completes the audit (the state label is «التدقيق مكتمل»).
  - The approver sees the consequences:
    - final closure;
    - the owner receives the clearance and release letters;
    - the owner has read-only access for 90 days (an assumption).
  - Approval (plus MFA, per B10 F04) gives `awaiting_reconciliation → closed`.
  - The owner (mobile) then sees «مغلقة» and can download three documents: «خطاب المخالصة النهائية», «خطاب فك الرهن» and «ملخص حالتك».
  - After 90 days, owner access expires (inferred E2E with clock control).

---

## C. Cross-flow state transitions exercised

The CaseState enum comes from the Handoff; the Arabic labels come from the CaseHeader.

| flow | from → to |
|---|---|
| f1 | `draft` (مسودة) → `awaiting_data` (بانتظار البيانات) |
| f2 | gate for → `proposed_solution` (حل مقترح) |
| f3 | `proposed_solution` → `internal_approval` → `awaiting_customer`; return: `internal_approval` → `proposed_solution` |
| f4 | `awaiting_customer` → `active_settlement` (after legal review) · → `negotiation` · → `proposed_solution` (never `judicial_referral`) |
| f5 | within `active_settlement` (payment sub-entity `recorded` → `matched`) |
| f6 | complaint sub-entity; blocks referral and cancellation; pauses the owner response timer |
| f7 | `negotiation` → `voluntary_sale` → `awaiting_reconciliation` |
| f8 | `proposed_solution` → `judicial_referral`, with an external reference (manual) |
| f9 | `awaiting_reconciliation` → `closed` |

## D. Reusable components exercised by the flows
- C11 SystemState (all 6 modes), C01 Button (primary/secondary, disabled when offline, «…» review convention), C06 Field (inline error, OTP code box), C04-like callouts (info/warn/err/neutral), status tag chips (C02), ReviewScreen («ما سيحدث» list + reason + MFA), key/value rows, icon lists.
- Harness-only: flow nav, mode radiogroup, step dots, trail, reference-frame card, device frame.

## E. Conflicts and ambiguities
1. The step counter «الخطوة n من N» and the step dots include alternative-branch steps (f3, f4 and f2's `ok`). This is a harness artefact. Real wizards should count only the path.
2. Undesigned branches, implemented as restarts in the prototype, are **not acceptance criteria**:
   - f1 «فتح الحالة السابقة»
   - f5 «رفض مع سبب» (payment match rejection)
   - f6 «طلب إعادة النظر»
   - f7 «أريد الانسحاب»
3. f3 step 2 is performed by «سارة القحطاني · مراجِعة», while the Handoff transition lists `actor:['preparer','reviewer']`. The step 1 button «تسليم لمديرة الحالة» implies an analyst → case manager hand-off before approval. Confirm whether this reviewer step is mandatory.
4. f4 «accept» leads to «تسوية معتمدة (بعد مراجعة القانونية)», but there is no legal-review step in any flow. Confirm who triggers `active_settlement`.
5. f8 approval says «موافقتان», but only one approver step is shown. It is implied that the legal requester counts as the first approval.
6. f9 shows no separate checker step, though the roles say «المالية ← المدقق ← المعتمد» and B10 F02 shows an explicit checker. The biz text «التدقيق مكتمل» implies it happened off-screen.
7. The 15-day objection period (f8) and the 90-day read-only owner access (f9) are both marked «افتراض».
8. The f2 due date «2026-09-21 · 9 ربيع الآخر 1448هـ» is two days before "today" (2026-09-23) in the working notes, and v2 is accepted on 2026-09-20. The fictional timeline is loose; don't derive test clocks from it.
9. In the error mode, «إعادة المحاولة» only clears the mode, and «طلب وصول» has no handler. Product behaviour needs definition: retry the same request; request access creates an access request to an admin.
10. The offline mode disables only primary actions. Secondary actions such as «أحتاج مساعدة» and «رفض مع سبب…» stay clickable. Decide whether secondary mutations should also be disabled offline.
