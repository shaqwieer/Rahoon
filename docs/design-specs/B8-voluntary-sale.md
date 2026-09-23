# B8 — Voluntary Sale (البيع الطوعي) · Phase 2

Source: `design-source/05 Phase 2 - B8 Voluntary Sale.dc.html` (condensed: `_condensed/05 Phase 2 - B8 Voluntary Sale.txt`). Cross-refs: `00 Brief & Assumptions` (matrix rows L27–L33, D15–D16), `03 Phase 0 - Blueprint` (state/transition table), `08 Flows & Prototypes` (flow f7), `09 Handoff/working-notes.md`.

Page intro copy (verbatim, product principle):
> البيع الطوعي يبدأ بطلب أو موافقة صريحة من المالك، ويمكنه الانسحاب حتى قبول عرض شراء. لا سوق عقاري عام: العرض مضبوط لوسيط مرخّص مكلّف ومشترين مؤهلين، وبيانات المالك مخفية عنهم. نقل الملكية والتمويل يتمّان خارج المنصة.

Design summary copy: «L27–L33: 5 إطارات سطح مكتب (القرار، ملف البيع مع الموافقة، العرض المضبوط والوسيط، مقارنة العروض، التتبع) · D15–D16: 3 إطارات جوال.»

## 0. Shared context

**Sample case (keep consistent):** `RH-2026-004012` · عائشة ف. — فيلا سكنية، حي الياسمين، الرياض · مصرف الأفق · تمويل سكني · مرابحة. Pre-sale case state = تفاوض (negotiation). Buyer-facing reference `VS-2026-0031`.
People: سارة القحطاني (مديرة حالات, the logged-in lender user in all L frames; CaseHeader shows «المسؤولة: سارة القحطاني»), نورة الشهري (معتمدة), ماجد الحربي (القانونية), broker «دار الوسطاء «أ»».

**Lender shell (all L frames, 1440 desktop):** `LenderSidebar active="cases"` (264px) + `LenderTopbar crumb1="RH-2026-004012" crumb2=<per screen>` (64px) + `CaseHeader` (210px, except L30/L31 which has none).

**CaseHeader props per frame** (all use `tab="sale"`, `extraTab="sale:البيع الطوعي"`, `caseRef="RH-2026-004012"`, `title="عائشة ف. — فيلا سكنية، حي الياسمين، الرياض"`, and a custom stage stepper):
`stageNames="القرار|موافقة المالكة|التجهيز|العرض المضبوط|العروض|الاعتماد|نقل الملكية والتسوية"` (7 steps replacing the default case stages while on the sale tab).

| Frame | crumb2 | stateKey (badge) | slaTone | slaText | stage (0-based current) |
|---|---|---|---|---|---|
| L27 | قرار البيع الطوعي | negotiation → «تفاوض» | ok | طلب المالكة منذ 3 أيام | 0 القرار |
| L28/L29 | ملف البيع | sale → «بيع طوعي» | ok | التجهيز: 6 أيام متبقية | 2 التجهيز |
| L30/L31 | العرض المضبوط والوسيط | — (no CaseHeader in frame) | — | — | — |
| L32 | عروض الشراء | sale | warn | أفضل عرض صالح حتى 2026-11-12 | 4 العروض |
| L33 | تتبع البيع | sale | info | الإفراغ المتوقع 2026-11-26 | 6 نقل الملكية والتسوية |

CaseHeader behaviour: `extraTab` "key:label" appends a tab «البيع الطوعي» after the 10 standard tabs (نظرة عامة … السجل) — the sale tab only exists once the case has a sale track. Stepper: steps before `stage` = done (charcoal bar), current = orange bar + bold + `aria-current="step"`, after = grey. Also shows «إجراءات أخرى» menu button.

**Debtor shell (D frames, 390 mobile):** `DebtorTop` (60px, props title/sub/back/unread) + optional `DebtorNav` (68px, 5 tabs الرئيسية/المستندات/الخيارات/المدفوعات/المساعدة).

**Spec-aside convention:** every section has a 380px "spec" aside (`{k,v}` rows) — these are the designer's acceptance notes and are quoted verbatim below under each screen as "Designer spec".

**Status colour tokens used:** done `check_circle` #1E6A45 (success); pending/scheduled `schedule` #8A5300 (warning); not started `radio_button_unchecked` #5E5D58; current `radio_button_checked` #F4633A; error/low #B3261E.

**Suggested route family** (matrix nav path «الحالة › البيع › …»): `/cases/[caseId]/sale` (tab root → redirects to current sub-step) with sub-routes `decision`, `file` (consent+prep), `listing` (disclosure+broker), `offers`, `tracking`. Debtor: `/portal/options/voluntary-sale` (D15 explain), `/portal/options/voluntary-sale/consent` (D15 consent), home `/portal` shows D16 card, `/portal/sale` (D16 detail). Adjust prefixes to the app's existing debtor route base.

---

## L27 — قرار البيع الطوعي · Voluntary-sale decision

- **Artboard:** `P2-Lender-VoluntarySaleDecision-Desktop-Review` · 1440 (section label «L27 Decision»). No mobile frame.
- **Roles:** مدير الحالات requests; المعتمد + القانونية approve (matrix: «مدير الحالات، المعتمد»). Route: `/cases/[caseId]/sale/decision`.
- **Layout:** shell + CaseHeader; `main` grid `minmax(0,1fr) 400px` (RTL: content right, 400px review aside left).

### Content (main column)
1. **Owner request card** (icon `person`): title «طلب المالكة»; quote «بعد التفكير، أفضّل بيع الفيلا بنفسي بسعر السوق وسداد التمويل، والانتقال لمسكن أصغر.»; meta «عبر البوابة · `2026-09-20 19:12` · موثق في السجل».
2. **«الشروط المسبقة»** checklist, rows grid `24px | 1fr | auto` = icon · (title + memo) · link:

| icon | title | memo | link |
|---|---|---|---|
| check_circle | طلب صريح من المالكة | 2026-09-20 · عبر البوابة | عرض |
| check_circle | تقييم صالح | 2,850,000 ر.س · مكتب «ب» · 2026-09-05 | التقرير |
| check_circle | مراجعة الرهن والقيود | لا قيود لاحقة · ماجد الحربي | المذكرة |
| check_circle | لا شكاوى مفتوحة | — | السجل |
| schedule | موافقة المالكة المكتوبة بنطاق البيع | تُطلب بعد اعتماد القرار | — (no link) |

3. **«التقدير المالي (قبل العروض)»** table, cols `1.6fr | 1fr | 1.4fr` = item · amount (LTR) · source:

| البند | المبلغ | المصدر |
|---|---|---|
| القيمة السوقية (التقييم) | 2,850,000.00 | تقييم 2026-09-05 |
| المديونية القائمة | 2,240,000.00 | نظام التمويل · 2026-09-22 |
| عمولة وساطة تقديرية 2% | 57,000.00 | اتفاقية الوسيط (افتراض) |
| رسوم أخرى تقديرية | 8,000.00 | افتراض |
| **الفائض المتوقع للمالكة** (highlight row: green text, bold, bg #EAF4EE) | 545,000.00 | تقديري قبل العروض |
| الحد الأدنى المقترح من المالكة | 2,700,000.00 | يغطي المديونية والتكاليف |

Calculation: `surplus = valuation − outstandingDebt − (valuation × brokerRate) − otherFees` → 2,850,000 − 2,240,000 − 57,000 − 8,000 = 545,000. Each line carries its source + as-of date.

### Aside — review panel
- Title «مراجعة قرار فتح مسار البيع»; «ما سيحدث:» bullets (verbatim):
  - «• تنتقل الحالة إلى **بيع طوعي**؛ تُعلَّق العروض الودية القائمة.»
  - «• يُطلب من المالكة توقيع موافقة صريحة بنطاق البيع وحدّها الأدنى.»
  - «• لا يُكلّف وسيط ولا يُعرض العقار قبل الموافقة.»
  - «• يمكن للمالكة الانسحاب حتى قبول عرض شراء.»
- Field **«السبب \*»** textarea, required. Sample: «بطلب المالكة؛ صافي متوقع يغطي المديونية مع فائض لها.»
- Approver line: «يعتمده: نورة الشهري + مراجعة القانونية»
- Primary button **«إرسال للاعتماد»** (full width).
- Info note (icon `info`): «التقدير مبني على تقييم `2026-09-05`. الأرقام النهائية تعتمد على العرض المقبول.»

### Actions
| Action | Effect | Enable condition (implied) |
|---|---|---|
| Prereq links (عرض/التقرير/المذكرة/السجل) | open request record, valuation report, legal memo, audit log | — |
| إرسال للاعتماد | creates approval request (approver + legal) for opening sale track | reason non-empty; prereqs 1–4 satisfied; requester ≠ approver |
| (Approver side, not drawn; flow f7 shows button «اعتماد») | on approval: case state تفاوض → بيع طوعي; existing amicable offers suspended; consent request sent to owner | approver within limits; legal review done |

### Designer spec (specDecision)
- هدف المستخدم: فتح مسار بيع طوعي فقط عند طلب المالك، مع وضوح الأثر المالي عليه.
- الشروط: طلب صريح، تقييم صالح، مراجعة قانونية، لا شكاوى مفتوحة.
- التقدير: يُظهر الفائض المتوقع للمالك صراحة، لا مصلحة المصرف فقط.
- القرار: شاشة مراجعة بسبب واعتماد؛ لا زر مباشر.
- الصلاحية: مدير الحالات يطلب؛ المعتمد + القانونية يعتمدان.

### Guards
- No sale track without owner's explicit request (Blueprint: «أي حالة حل ← بيع طوعي | مدير الحالات + معتمد | موافقة المالك الصريحة، تقييم صالح | guard: لا موافقة موثقة من المالك»).
- Valuation must be valid (≤ 90 days per platform assumption — valuation 2026-09-05).
- Open complaint blocks.
- No direct "open sale" button — always via approval with reason.

---

## L28 + L29 — موافقة المالك على البيع / ملف البيع وتجهيز العقار · Sale consent + Sale file & property preparation

- **Artboard (shared):** `P2-Lender-SaleFile-Desktop-Preparation` · 1440 (section «L28-L29 Consent file»).
- **Roles:** مدير الحالات (matrix). Routes: `/cases/[caseId]/sale/file` (L28 consent panel lives in the same page; matrix path «الحالة › البيع › الموافقة» / «› الملف»).
- **Layout:** shell + CaseHeader (state بيع طوعي, stage 2); `main` grid `minmax(0,1fr) 400px`: main = preparation checklist; aside = consent card + occupancy card + photo note.

### L29 main — «تجهيز العقار» checklist
Header right counter «5 من 8» (done count / total). Rows grid `24px | 1fr | 150px` = status icon · (title + memo) · responsible party:

| status | title | memo | responsible |
|---|---|---|---|
| done | موافقة المالكة الموقعة | 2026-09-25 | المالكة |
| done | صك الملكية ومطابقة البيانات | متحقق | القانونية |
| done | خطاب المديونية للمشتري | يُصدر عند قبول عرض | المالية |
| done | تحديد أوقات الزيارة | الخميس والسبت 4–7 م | المالكة |
| done | خطة الإخلاء | 60 يوماً بعد نقل الملكية | المالكة + مدير الحالة |
| scheduled | التصوير المعتمد | موعد 2026-09-30 | الوسيط |
| scheduled | فحص فني اختياري | بطلب المشتري لاحقاً | — |
| not started | مراجعة ملخص العرض | قبل المشاركة مع المشترين | الامتثال |

Item statuses: `done` (check_circle green) · `scheduled/pending` (schedule amber) · `not_started` (radio_button_unchecked grey).

### L28 aside — consent card
- Title (icon `task_alt`) «L28 موافقة المالكة الصريحة»
- «وقّعت عائشة ف. داخل البوابة `2026-09-25 20:04` · رمز تحقق»
- Scope box:
  - «**الحد الأدنى للسعر:** 2,700,000 ر.س»
  - «**مدة التفويض:** 90 يوماً حتى 2026-12-24» (start = consent date 2026-09-25 + 90 days)
  - «**الزيارات:** بموعد مسبق، الخميس والسبت»
  - «**الانسحاب:** متاح حتى قبول عرض»
- Link «عرض نص الموافقة ونسختها» (opens consent text version + signed copy).
- Card «الإشغال والانتقال»: «تسكن المالكة وأبناؤها العقار. اتُفق على مهلة إخلاء 60 يوماً بعد نقل الملكية، مذكورة في شروط العرض للمشترين.»
- Note (icon `photo_camera`): «التصوير بموافقة المالكة فقط، بلا أشخاص أو متعلقات شخصية أو أرقام الوحدة.»

### States (designer spec)
- **بانتظار الموافقة** — blocks preparation («يحجب التجهيز»): checklist locked until consent signed.
- **موقّعة** — shown.
- **انسحاب المالك** — returns case to «حل مقترح» (proposed solution).
- Mandate expiry (90 days) — implied: listing/broker access ends (see L31).

### Designer spec (specFile)
- الموافقة: نطاق موثق: الحد الأدنى، مدة التفويض، أوقات الزيارة، حق الانسحاب.
- التجهيز: قائمة بمسؤول لكل بند؛ الإشغال والإخلاء بترتيب إنساني متفق عليه.
- الصور: بموافقة، بلا أشخاص أو متعلقات.
- الحالات: بانتظار الموافقة (يحجب التجهيز)، انسحاب المالك (يعيد إلى حل مقترح).
- التجاوب: سطح المكتب؛ المالك يشارك من الجوال.

### Actions (implied; none drawn as buttons besides link)
Mark prep item done / schedule date / assign responsible; upload approved photos; view consent. Lender cannot edit consent scope — it is owner-signed (changes require new owner consent version).

---

## L30 + L31 — الإدراج المضبوط والإفصاح / تكليف الوسيط · Controlled listing & disclosure + Broker assignment

- **Artboard (shared):** `P2-Lender-ControlledListing-Desktop` · 1440 (section «L30-L31 Listing broker»). **No CaseHeader** in this frame (only sidebar + topbar crumbs «RH-2026-004012 › العرض المضبوط والوسيط»).
- **Roles:** L30 مدير الحالات، الامتثال (compliance reviews summary); L31 مدير الحالات assigns. Routes: `/cases/[caseId]/sale/listing` (L30) and `/cases/[caseId]/sale/broker` or same page section (L31).
- **Layout:** `main` grid `minmax(0,1fr) minmax(0,1fr)`: row 1 page heading (full width, `grid-column:1/-1`); row 2 disclosure matrix (right in RTL) | photos + buyer preview (left); row 3 broker assignment card (full width).

### Heading
H2 «العرض المضبوط والإفصاح»; sub «ليس إعلاناً عاماً. يُشارك الملخص مع الوسيط المكلّف، ويعرضه على مشترين مؤهلين بعد إقرار السرية.»

### L30 — «L30 مصفوفة الإفصاح» (role=table)
Columns `1.6fr 1fr 1fr 1fr`: «البيان» · «الوسيط» · «مشترٍ مؤهل» · «العامة». Cell values: نعم (green) / لا (grey) / conditional text (amber).

| البيان | الوسيط | مشترٍ مؤهل | العامة |
|---|---|---|---|
| اسم المالكة وهويتها | لا | لا | لا |
| الحي والمدينة | نعم | نعم | لا |
| الموقع الدقيق | نعم | بعد الزيارة | لا |
| الصور المعتمدة | نعم | نعم | لا |
| السعر المطلوب | نعم | نعم | لا |
| الحد الأدنى للمالكة | لا | لا | لا |
| المديونية والتعثر | لا | لا | لا |
| مهلة الإخلاء | نعم | نعم | لا |

**«العامة» is always «لا» — no public listing exists.** Implement the matrix as a server-enforced field-level policy (not just UI).

### Photos + buyer preview
- Photo placeholder (role=img, aria «مكان صور العقار المعتمدة»): "approved property photos (6) — no people".
- Card «معاينة ما يراه المشتري المؤهل»:
  - **فيلا سكنية · شمال الرياض** (area generalized — not «حي الياسمين»)
  - «أرض 600 م² · بناء 680 م² · 2017»
  - «السعر المطلوب: 2,850,000 ر.س»
  - «الإخلاء: 60 يوماً بعد نقل الملكية · الزيارة بموعد»
  - «المرجع للمشتري: `VS-2026-0031` — لا يكشف رقم الحالة»

### L31 — «L31 تكليف وسيط مرخّص»
Sub: «من دليل مقدمي الخدمة المعتمدين لدى المصرف». Radio-select table, cols `40px 1.6fr 1.3fr 1fr 1fr 1fr`: (radio) · «الوسيط» · «الترخيص» · «صفقات منجزة» · «متوسط المدة» · «العمولة».

| sel | الوسيط | الترخيص (colour) | صفقات منجزة | متوسط المدة | العمولة |
|---|---|---|---|---|---|
| ● (row bg #FDF0EB) | دار الوسطاء «أ» | ساري حتى 2027-03 (green) | 38 | 46 يوماً | 2% |
| ○ | مكتب الوساطة «ج» | ساري حتى 2026-12 (green) | 21 | 58 يوماً | 2.5% |
| ○ | شركة الوساطة «د» | ينتهي خلال 20 يوماً (amber warning) | 12 | — | 2% |

Footer: note «الوسيط يرى ملف البيع فقط، ويُسحب وصوله عند انتهاء التفويض أو قبول عرض.» + primary button **«تكليف دار الوسطاء «أ»»** (label includes selected broker name).

### Actions
| Action | Effect | Condition |
|---|---|---|
| Select broker (radio) | updates button label | only brokers in the lender's approved directory with a valid licence; expired licence = «موقوف ولا يُكلّف» (B9 V05) — hidden or disabled; expiring shows warning but selectable |
| تكليف <broker> | creates broker assignment with scoped access (sale file only) | owner consent signed; mandate not expired; compliance summary review (prep item «مراجعة ملخص العرض») before sharing with buyers |
| Share summary with buyers (implied) | broker shows summary to qualified buyers after NDA («إقرار السرية») | compliance reviewed |

### Designer spec (specListing)
- الإفصاح: مصفوفة صريحة لكل جمهور؛ «العامة» دائماً لا — لا سوق عامة.
- المرجع: VS-… للمشترين؛ لا يكشف رقم الحالة أو المصرف.
- الوسيط: من دليل معتمد مع حالة الترخيص؛ ترخيص قريب الانتهاء يظهر تحذيراً.
- الوصول: الوسيط يرى ملف البيع فقط، ويُسحب وصوله آلياً.
- الصلاحية: مدير الحالات يكلّف؛ الامتثال يراجع الملخص.

### Guards
- Broker access scope = sale file only (no case, debt, owner identity). Auto-revoked at `min(mandateEnd, offerAcceptedAt)` (also on owner withdrawal — implied).
- Buyer reference `VS-YYYY-NNNN` must not reveal case number or bank.

---

## L32 — مقارنة عروض الشراء · Buyer-offer comparison

- **Artboard:** `P2-Lender-BuyerOffers-Desktop-Compare` · 1440 (section «L32-L33 Offers tracking»).
- **Roles:** المحلل (prepares), المعتمد (approves). Route: `/cases/[caseId]/sale/offers`.
- **Layout:** shell + CaseHeader (stage 4 العروض, sla warn «أفضل عرض صالح حتى 2026-11-12»); `main` single column: comparison table + footer action bar.

### Comparison table (role=table, aria «مقارنة عروض الشراء»)
Cols `220px repeat(3, 1fr)`; header «البند» then per offer: id (LTR) + «مشترٍ مؤهل · عبر الوسيط». Selected/recommended column OF-02: bg #FDF0EB, orange top bar, bold cells.

| البند | OF-01 | OF-02 (selected) | OF-03 |
|---|---|---|---|
| السعر | 2,780,000 | 2,800,000 | 2,820,000 |
| طريقة الدفع | تمويل معتمد مبدئياً | نقداً | تمويل — لم يُعتمد بعد |
| إثبات القدرة | خطاب موافقة مبدئية | شيك مصدق (نسخة) | — |
| الشروط | فحص فني | بلا شروط | الحصول على التمويل خلال 30 يوماً |
| مدة الإفراغ المقترحة | 30 يوماً | 21 يوماً | 45 يوماً |
| صافي تقديري بعد العمولة | 2,724,400 | 2,744,000 | 2,763,600 |
| ≥ الحد الأدنى للمالكة | ✓ (green) | ✓ | ✓ |
| درجة اليقين | متوسطة | عالية (green) | منخفضة (red) |

Calculations: `netAfterCommission = price × (1 − brokerRate)` with brokerRate 2% (2,780,000×0.98 = 2,724,400 etc.). `meetsMinimum = price ≥ consent.minPrice (2,700,000)`. Certainty enum: عالية / متوسطة / منخفضة (derived from payment method + proof of funds + conditions — rule not specified; treat as analyst-entered).

### Footer
- Info (icon `info`): «أسماء المشترين مخفية عن المالكة؛ تعرض لها الأسعار والشروط فقط. القرار النهائي يتطلب موافقتها واعتماد المصرف.»
- Secondary button **«إرسال المقارنة للمالكة»**.
- Primary button **«طلب اعتماد OF-02…»** (ellipsis → opens approval review; label uses selected offer id).

### Actions
| Action | Effect | Condition |
|---|---|---|
| Select offer column | changes highlighted column & approval button label | — |
| إرسال المقارنة للمالكة | publishes anonymised comparison to owner portal (D16 «مقارنة العروض») | ≥1 valid offer |
| طلب اعتماد OF-xx… | opens review screen (not drawn): «الأثر على المديونية والفائض، والموافقتان»; submits bank approval | owner has accepted that offer (owner approves before bank); offer ≥ min price; offer still valid; maker ≠ checker |

### Designer spec (specOffers — shared with L33)
- المقارنة: السعر والصافي والشروط واليقين جنباً إلى جنب؛ الأعلى سعراً ليس بالضرورة الأنسب.
- المالك: يرى العروض بلا أسماء المشترين، ويوافق قبل اعتماد المصرف.
- الاعتماد: شاشة مراجعة: الأثر على المديونية والفائض، والموافقتان.
- التتبع: يميز الداخلي عن الخارجي اليدوي؛ لا تُعرض حالة رسمية كأنها مؤكدة.
- الإغلاق: ينتقل لـ «بانتظار التسوية المالية» بعد استلام الثمن وفك الرهن.
- التجاوب: المقارنة على الجوال: عرض واحد مع مبدّل.

Mobile: comparison collapses to one offer at a time with an offer switcher (not drawn).

---

## L33 — اعتماد البيع وتتبعه · Sale approval & tracking

- **Artboard:** `P2-Lender-SaleTracking-Desktop` · 1440.
- **Roles:** المعتمد (matrix); case team records external steps. Route: `/cases/[caseId]/sale/tracking`.
- **Layout:** shell + CaseHeader (stage 6, sla info «الإفراغ المتوقع 2026-11-26»); `main` grid `minmax(0,1fr) 380px`.

### Main — «L33 تتبع التنفيذ» timeline
Rows `28px | 1fr | 150px | 120px` = icon · (title + memo) · source chip · date (LTR). Source chip «داخلي» = filled grey bg (#F2F1ED, #22262A); «خارجي — يدوي» = white/outlined grey text.

| status | step | memo | source | date |
|---|---|---|---|---|
| done | موافقة المالكة على OF-02 | عبر البوابة برمز تحقق | داخلي | 2026-11-02 |
| done | اعتماد المصرف | نورة الشهري + القانونية | داخلي | 2026-11-03 |
| done | إصدار خطاب المديونية للمشتري | صالح 30 يوماً | داخلي | 2026-11-04 |
| done | توقيع اتفاقية البيع | خارج المنصة · نسخة مرفوعة | خارجي — يدوي | 2026-11-06 |
| pending | استلام الثمن وسداد المديونية | مرجع التحويل يُدخل عند الاستلام | خارجي — يدوي | متوقع 2026-11-24 |
| not started | فك الرهن ونقل الملكية | لدى الجهة المختصة | خارجي — يدوي | متوقع 2026-11-26 |
| not started | تحويل الفائض للمالكة | 504,000 تقديرياً | خارجي — يدوي | — |
| not started | التسوية المالية والإغلاق | ينتقل إلى «بانتظار التسوية المالية» | داخلي | — |

Dates: actual date for done; «متوقع <date>» for expected; «—» unknown.

### Aside
1. **«العرض المعتمد OF-02»** — «2,800,000 ر.س · نقداً»; «اعتمدته نورة الشهري `2026-11-03` · وافقت المالكة `2026-11-02`».
2. **«التوزيع المتوقع (تقديري)»**:
   - سداد المديونية — 2,240,000.00
   - عمولة الوسيط 2% — 56,000.00
   - المتبقي للمالكة — 504,000.00
   - note «يُنفَّذ عبر القنوات النظامية خارج المنصة؛ رهون تسجّل المراجع فقط.»
   Calc: `ownerSurplus = acceptedPrice − debtPayoff − brokerCommission` (2,800,000 − 2,240,000 − 56,000 = 504,000).
3. Note (icon `sync_disabled`): «الحالات الخارجية (نقل الملكية، فك الرهن) تُدخل يدوياً بمصدرها ووقتها — لا تكامل مع أنظمة رسمية في هذه المرحلة.»

### Actions (implied)
Record external step (source, timestamp, reference, uploaded copy); issue debt letter (valid 30 days); on price received + mortgage released → case state «بانتظار التسوية المالية» (reconciliation). No external status may be displayed as officially confirmed unless manually entered with source.

---

## D15 — شرح البيع الطوعي والموافقة · Voluntary-sale explanation & consent (debtor)

Two mobile frames (390). Role: المدين / المالك. Matrix path «الخيارات › البيع الطوعي».

### D15a — Explain · `P2-Debtor-VoluntarySaleExplain-Mobile`
- Top: `DebtorTop title="البيع الطوعي" sub="ما يعنيه لك" back`. No bottom nav.
- Intro: «تبيعين العقار بنفسك بسعر السوق عبر وسيط مرخّص، ويُسدَّد التمويل من ثمن البيع، ويعود لك ما يتبقى.»
- 4 explainer rows (icon + title + desc):
  - `payments` **يُسدَّد تمويلك من ثمن البيع** — ويعود لك الفائض بعد العمولة والرسوم.
  - `person_check` **أنت من يقرر** — تحددين أقل سعر تقبلينه، وتُعرض عليك كل العروض.
  - `visibility_off` **خصوصيتك محفوظة** — المشترون لا يرون اسمك أو وضع التمويل.
  - `undo` **يمكنك الانسحاب** — في أي وقت قبل قبول عرض شراء.
- Primary (bottom, 54px) **«مراجعة الموافقة»** → D15b. Link **«أريد التحدث مع مسؤولة حالتي أولاً»** → message/contact case manager.

### D15b — Consent · `P2-Debtor-VoluntarySaleConsent-Mobile`
- Top: `DebtorTop title="موافقتك على البيع" back`.
- Field **«أقل سعر تقبلينه»** — numeric input (LTR, thousands separators, 19px bold, 2px border) with suffix «ريال»; sample 2,700,000. Helper: «التقييم المستقل: 2,850,000 ريال». Validation (implied): required, > 0; warn if below debt + estimated costs (not specified).
- **«أوقات الزيارة المناسبة»** — multi-select chips: الخميس (selected), السبت (selected), الأحد (unselected). ≥1 required (implied). (Time window «4–7 م» appears only in L29 — not captured here.)
- Terms summary: «• التفويض 90 يوماً» · «• يمكنك الانسحاب حتى قبول عرض شراء» · «• لا يرى المشترون اسمك أو بياناتك» · «• تُعرض عليك العروض قبل أي قبول».
- Checkbox (required, shown checked): «أوافق باختياري على عرض عقاري للبيع بهذه الشروط.»
- Primary **«تأكيد برمز التحقق»** → OTP step → records consent (L28 shows «وقّعت … داخل البوابة · رمز تحقق»). Disabled until checkbox checked + min price + ≥1 visit day.

### Designer spec (specDebtor — D15+D16)
- الشرح: لغة مبسطة: ما يعنيه البيع، القرار لك، الخصوصية، الانسحاب.
- الموافقة: الحد الأدنى بجانب التقييم المستقل؛ إقرار واحد + رمز تحقق.
- التقدم: الخطوة التالية أولاً ثم المراحل؛ الانسحاب متاح ظاهر.
- النبرة: لا ضغط زمني مصطنع، ولا صور مزادات أو لافتات بيع.
- الصلاحية: المالكة ترى ملخص العروض فقط.

---

## D16 — تقدم البيع · Sale progress (debtor)

- **Artboard:** `P2-Debtor-SaleProgress-Mobile-Offers` · 390. Matrix path «الرئيسية › البيع». Top `DebtorTop title="أهلاً عائشة" sub="البيع الطوعي" unread=1` (bell label «الإشعارات، 1 جديدة»); bottom `DebtorNav active="home"`.
- **Next-step card** (article): eyebrow «خطوتك التالية»; title **«وصلت 3 عروض شراء»**; body «أعلاها 2,820,000 ريال (بشرط تمويل)، وأعلى عرض نقدي 2,800,000.»; primary **«مقارنة العروض»** → owner comparison (anonymised; mobile single-offer switcher; not drawn).
- **«مراحل البيع»** stepper:
  - ✓ وافقتِ على البيع
  - ✓ تجهيز العقار وتصويره
  - ✓ عرضه على مشترين مؤهلين
  - ◉ (orange, bold) مراجعة العروض — أنتِ هنا
  - ○ نقل الملكية واستلام الفائض
- Link **«أريد الانسحاب من البيع»** (always visible until an offer is accepted) → withdrawal confirmation (not drawn) → sale withdrawn, case → حل مقترح, broker access revoked.
- Flow f7 adds the owner action «أوافق على العرض النقدي» (accept offer) and «أريد الانسحاب» on this step.

Card copy is dynamic: "N offers", highest price with financing condition flagged, highest cash offer.

---

## Cross-cutting: states & business rules

**Case state transition:** تفاوض/any solution state → **بيع طوعي** (`sale`, icon `sell`) on L27 approval → **بانتظار التسوية المالية** (`reconciliation`) after price received + mortgage release → مغلقة. Owner withdrawal → **حل مقترح** (`proposed`).

**Sale sub-stages (stepper):** القرار → موافقة المالكة → التجهيز → العرض المضبوط → العروض → الاعتماد → نقل الملكية والتسوية.

**Consent states:** requested (بانتظار الموافقة — blocks prep/listing/broker) → signed (OTP, versioned text + copy) → withdrawn (allowed until offer accepted) / expired (mandate end) / fulfilled (offer accepted — withdrawal no longer available).

**Listing states (implied):** draft summary → compliance review («مراجعة ملخص العرض») → shared with broker → shown to qualified buyers (after NDA) → closed (offer accepted / mandate expired / withdrawn).

**Offer states (implied):** received (via broker) → shared with owner (anonymised) → owner accepted/declined → bank approval requested → approved (L33 «العرض المعتمد») / rejected; expired by validity date.

**Tracking step source:** `internal` («داخلي») vs `external_manual` («خارجي — يدوي»); status done/pending/not started; actual vs expected date.

**Rules:**
1. Explicit owner request + separate scoped owner consent (min price, 90-day mandate, visit times, withdrawal right) — OTP-confirmed; no broker/listing before consent.
2. Owner can withdraw until offer acceptance; withdrawal visible on D16.
3. Controlled disclosure matrix enforced server-side per audience; public = none. Owner identity, owner's minimum, debt/default never disclosed to broker or buyers. Exact location to buyers only after visit.
4. Buyer reference `VS-…` separate from case ref.
5. Broker from lender's approved provider directory with licence status; expiring licence warning; expired = cannot assign. Broker sees sale file only; access auto-revoked at mandate expiry or offer acceptance.
6. Buyer names hidden from owner; owner sees prices & terms only.
7. Offer approval: owner approval first, then bank approval (approver + legal) — maker-checker (analyst prepares, approver approves).
8. Photos only with owner consent; no people, personal items or unit numbers.
9. Transfer of ownership, payment, mortgage release happen outside platform; platform records references manually with source + time; no official-system integration in Phase 2.
10. All estimates labelled «تقديري» with source/as-of date.
11. Opening sale suspends existing amicable offers.

## Implied data entities

- **VoluntarySale**: id, caseId, buyerRef (`VS-2026-0031`), status, currentStage (0–6), openedAt, closedAt, closeReason (completed/withdrawn/expired).
- **SaleRequest**: saleId, source (portal/…), text, requestedAt, auditRef.
- **SaleDecision**: saleId, reason (required), requestedBy, prerequisitesSnapshot[], estimateSnapshot, approvals[{role: approver|legal, userId, decision, at}], status.
- **SaleEstimate lines**: key, amount, source, asOf, isAssumption.
- **SaleConsent**: saleId, version, minPrice, mandateDays, mandateStart, mandateEnd, visitDays[], visitWindow, withdrawalUntil (=offerAccepted), acknowledgement bool, otpVerifiedAt, signedAt, channel, consentTextVersion, documentId, status (requested/signed/withdrawn/expired/fulfilled), withdrawnAt, withdrawReason.
- **SalePrepItem**: saleId, key, title, status (done/scheduled/not_started), memo, dueDate/scheduledAt, responsibleParty (owner/legal/finance/broker/compliance/case_manager).
- **OccupancyPlan**: occupants text, evacuationDaysAfterTransfer (60).
- **SalePhoto**: fileId, approved, ownerConsentRef.
- **DisclosurePolicy**: field × audience (broker/qualifiedBuyer/public) → yes/no/conditional("after_visit").
- **ListingSummary**: saleId, title, areaLabel (generalized), landArea, builtArea, yearBuilt, askingPrice, evacuationTerms, visitTerms, complianceReviewStatus/by/at.
- **BrokerAssignment**: saleId, providerId, licenceStatus/expiry snapshot, commissionRate, assignedBy/at, accessScope = sale_file, accessExpiresAt, revokedAt, revokeReason.
- **QualifiedBuyer / NDA**: brokerId, buyer (hidden), ndaAcceptedAt.
- **BuyerOffer**: id (`OF-01`), saleId, brokerId, buyerIdentity (hidden from owner), price, paymentMethod (cash/financing_preapproved/financing_pending), proofOfFunds (doc), conditions, proposedTransferDays, validUntil, netAfterCommission (computed), meetsMinimum (computed), certainty (high/medium/low), status, ownerDecision {decision, otpAt}, bankApproval {approverId, legalId, at}.
- **SaleTrackingStep**: saleId, key, title, memo, source (internal/external_manual), status, actualDate, expectedDate, reference, documentId, enteredBy/at.
- **DebtLetter**: issuedAt, validDays 30, amount.

## API operations (suggested)

- `POST /cases/{id}/sale-requests` (owner portal) · `GET /cases/{id}/sale` (aggregate)
- `GET /cases/{id}/sale/decision-prereqs` · `POST /cases/{id}/sale/decision` (reason) · `POST /approvals/{id}/decide`
- `POST /sales/{id}/consent-requests` · `GET /portal/sale/consent` · `POST /portal/sale/consent` (minPrice, visitDays, ack) → `POST .../consent/otp/verify` · `POST /portal/sale/withdraw` · `GET /sales/{id}/consent/document`
- `GET/PATCH /sales/{id}/prep-items/{key}` · `POST /sales/{id}/photos`
- `GET /sales/{id}/disclosure-matrix` · `GET /sales/{id}/listing-preview?audience=buyer` · `POST /sales/{id}/listing/compliance-review`
- `GET /sales/{id}/broker-candidates` (from institution directory with licence status) · `POST /sales/{id}/broker-assignment` · auto job: revoke broker access at expiry/offer acceptance
- `POST /sales/{id}/offers` (broker) · `GET /sales/{id}/offers/comparison` · `POST /sales/{id}/offers/share-with-owner` · `GET /portal/sale/offers` (anonymised) · `POST /portal/sale/offers/{offerId}/accept` (OTP) · `POST /sales/{id}/offers/{offerId}/approval-requests`
- `GET /sales/{id}/tracking` · `POST /sales/{id}/tracking/{stepKey}` (manual external entry with source/ref/doc) · `POST /sales/{id}/debt-letter`
- `GET /portal/sale` (D16 progress + next step)

## Integration dependencies

- None live. Design explicitly labels: «خارجي — يدوي» source chips, «لا تكامل مع أنظمة رسمية في هذه المرحلة», «يُنفَّذ عبر القنوات النظامية خارج المنصة؛ رهون تسجّل المراجع فقط».
- Consent/offer acceptance = in-platform OTP («رمز تحقق»), not licensed e-signature (see B9 X01 conditional pattern; Brief A-05).
- Debt balance from «نظام التمويل» (core banking) with as-of timestamp.
- Broker/provider directory from B9 V05.

## Components

Used: LenderSidebar, LenderTopbar, CaseHeader (with `extraTab`, custom `stageNames`), DebtorTop, DebtorNav.
Introduced/reusable: PrerequisiteChecklist (icon/title/memo/link rows); EstimateTable (item/amount/source with highlighted result row); ReviewDecisionPanel («ما سيحدث» bullets + required reason + approvers + submit); ChecklistWithOwner (status/title/memo/responsible + "x من y" counter); ConsentSummaryCard; AudienceDisclosureMatrix (yes/no/conditional cells); BuyerPreviewCard; SelectableProviderTable (radio rows + licence status colouring); OfferComparisonTable (columns = offers, selectable column, coloured cells); ExecutionTimeline (status icon + source chip internal/external-manual + actual/expected date); DistributionSummary; Mobile ExplainerList (icon rows); ChipMultiSelect; ConsentCheckbox + OTP button; NextStepCard; VerticalStepper (done/current/todo).

## Conflicts / ambiguities

1. **Consent vs decision order:** L27 says owner's written scoped consent «تُطلب بعد اعتماد القرار»; Flow f7 orders owner consent (D15) *before* lender decision approval; Blueprint guard for → بيع طوعي is «لا موافقة موثقة من المالك». Recommend: owner portal *request* (documented) satisfies the guard; formal scoped consent follows approval and gates prep/listing/broker (matches L27–L28 dates 09-20 request, 09-25 consent).
2. **Fees inconsistency:** L27 deducts «رسوم أخرى تقديرية 8,000» and commission on valuation (57,000); L32 net and L33 distribution deduct only 2% commission on price (no other fees). Decide whether other fees apply in L32/L33.
3. **Debt figure static:** 2,240,000 as of 2026-09-22 reused in L33 (Nov). Payoff should be refreshed at approval/transfer.
4. **«≥ الحد الأدنى للمالكة»** — compared against price or net? All pass either way; define (recommend price vs minPrice, as owner consent is on sale price).
5. **Prep item «خطاب المديونية للمشتري»** is marked done (check) while memo says «يُصدر عند قبول عرض» — likely means "prepared/ready"; L33 shows it actually issued 2026-11-04.
6. **Approver limit:** نورة الشهري's limit in fictional data is ≤2,000,000; sale ~2.8M is approved by her — clarify whether amount-based limits apply to sale approvals.
7. **L30/L31 frame has no CaseHeader** unlike other L frames; assume it should render within the case sale tab (stage 3 العرض المضبوط).
8. **Title mismatch:** matrix L30 «الإدراج المضبوط والإفصاح» vs frame «العرض المضبوط والإفصاح»; L31 matrix «تكليف الوسيط» vs frame «تكليف وسيط مرخّص».
9. **Not drawn:** approver-side decision screen (L27 approval), offer approval review screen («طلب اعتماد OF-02…»), owner mobile offer comparison/accept screen, withdrawal confirmation, mandate expiry handling, lender mobile views, compliance summary review UI, buyer NDA flow.
10. **Visit time window:** D15 captures days only; L29 shows «الخميس والسبت 4–7 م» and D15 offers الأحد (unselected). Decide if time window is owner-entered.
11. **Expiring-licence broker** («شركة الوساطة «د»», 20 days) is still selectable in L31 — confirm whether assignment should be blocked if licence expires before mandate end (2026-12-24).
12. **Offer certainty** derivation rule unspecified (manual vs computed).
