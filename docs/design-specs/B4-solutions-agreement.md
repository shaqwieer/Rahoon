# B4 — Solutions, Approval, Agreement & Payments (L14–L21) · Implementation spec

Source: `design-source/04 Phase 1 - B4 Solutions & Agreement.dc.html` (+ `_condensed`), L15 from `03 Phase 0 - Anchor Screens B.dc.html` (frame `P0-Lender-CaseWorkspace-Desktop-SubmitApprovalReview`), L13 builder in same file (formula source), `CaseHeader`, `09 Handoff/working-notes.md`, `00 Brief & Assumptions` (A-04, A-05, A-08, A-09).
Page title: `04 Phase 1 — B4 Solutions & agreement — رهون`. H1: `المرحلة 1 — الدفعة B4: الحلول والموافقة والاتفاق والسداد`.
Shell/CaseHeader/state map/tab map/masking: see `B3-case-tabs.md §0` (same components). All amounts LTR `<bdi>`; currency `ر.س` (lender UI) / `ريال` (owner UI).

Design summary (verbatim): `صُمم في B4` — `L14–L21: 7 إطارات سطح مكتب (المقارنة، صندوق الموافقة مع القرار، معاينة العرض، التفاوض، الاتفاق، السداد مع تسجيل دفعة، الإخلال) + جوال الموافقات والسداد. L15 (شاشة الإرسال) في الشاشات المرجعية (ب).`

## Story canon (keep consistent in seed data)
- Case `RH-2026-004172` عبدالله م.; outstanding 1,284,560.00; late fees 18,300.00; net income 32,400.00 (salary stmt v2 verified 2026-09-20).
- v1: reschedule 60 m, 21,409.33, DSR 66.1% ✕, returned 2026-09-15 by سارة القحطاني.
- v2: reschedule 84 m, 15,074.52, waiver 18,300.00 (1.42%), rescheduled 1,266,260.00, first 2026-11-01 (10 جمادى الأولى 1448هـ), last 2033-10-01; prepared by فهد العتيبي 2026-09-22 16:05; reviewed & sent by سارة القحطاني 2026-09-23 10:12; approver نورة الشهري (≤ 2,000,000, waiver ≤ 5%), due 2026-09-26.
- Offer v2 sent 2026-09-24 09:00, valid until 2026-10-03. Owner counter 2026-09-27 20:41 (due day 10, start December). v3: day 10, start 2026-12-10, end 2033-11-10; accepted 2026-10-02 14:21 → `AGR-2026-004172-01`.
- Inst 1 paid `TRX-88392214` matched; inst 2 `TRX-88410027` pending match; recording inst 3 `TRX-88457310`.
- Breach example: `RH-2026-003870` ماجد ت. — شقة سكنية، حي العزيزية، المدينة المنورة (active settlement 3/60, 612,900.00): inst 3 & 4 missed, cure until 2026-10-04.

## Calculations (inferred, all verified against sample numbers)
| Name | Formula | Example |
|---|---|---|
| Waiver % | waiver ÷ total outstanding | 18,300 / 1,284,560 = 1.4246% → `1.42%` |
| Rescheduled amount | outstanding − waiver − down payment | 1,284,560 − 18,300 − 0 = `1,266,260.00` (builder label `محسوب: القائم − التنازل`) |
| Installment | rescheduled ÷ term (no profit/rate component in samples) | 1,266,260/84 = `15,074.52`; v1 1,284,560/60 = `21,409.33` |
| DSR (نسبة الاستقطاع) | installment ÷ verified net monthly income | 15,074.52/32,400 = `46.5%`; v1 `66.1%`; current 13,774.29 → `42.5%` |
| DSR check | ✓ if ≤ policy limit (55%, assumption, configurable) else ✕ | |
| LTV | outstanding ÷ market value | `77.9%` |
| Estimated recovery (reschedule) | rescheduled amount, undiscounted, excl. collection costs | v1 1,284,560.00 / v2 1,266,260.00 |
| Estimated recovery (voluntary sale) | est. sale price; lender gets outstanding, owner gets surplus | `≈ 1,540,000 − 1,284,560 للمالك` (≈255,440 to owner) |
| Last installment date | first due + (n − 1) months on due-day | 2026-12-10 + 83 m = 2033-11-10 |
| Offer expiry | send/approval date + 10 days (configurable) | 2026-10-03 |
| Paid / remaining | Σ matched payments; rescheduled − paid (pending excluded) | paid 15,074.52; remaining `1,251,185.48` |
| Cure deadline | breach review opened + 15 days | 2026-09-19 → 2026-10-04 |
| Approver in-limit | amount ≤ approver.maxAmount AND waiver% ≤ approver.maxWaiverPct (and solution type allowed) | 1,266,260 ≤ 2,000,000; 1.42% ≤ 5% |
| Compliance notice | created when waiver% > 1% (assumption) | |
Rounding: 84 × 15,074.52 = 1,266,259.68 → 0.32 residual; rule for final-installment adjustment not designed (see conflicts).

---

## L14 — مقارنة الحلول · Solution comparison

- Artboard `P1-Lender-SolutionCompare-Desktop-Default · 1440` (min-h 980).
- Roles: المحلل، المعتمد (read). Route `/cases/[ref]/solutions` (compare view, or `/solutions/compare`).
- Shell: `LenderSidebar active="cases"`; `LenderTopbar crumb1="RH-2026-004172" crumb2="مقارنة الحلول"`; `CaseHeader tab="solutions" state-key="approval" sla-text="بانتظار نورة الشهري · حتى 2026-09-26" stage="4"` (sla-tone default warn).

### Layout / content
Toolbar: `<h3>مقارنة الحلول</h3>` (flex:1) · checkbox (checked, ink box with white check) `إبراز الفروق فقط` · secondary button `تصدير PDF` (icon download).
Table `role="table" aria-label="مقارنة الحلول"`, grid `220px repeat(3,minmax(0,1fr))`. Header row: `البند` + one column header per option: version tag (LTR) + name (strong) + status badge (icon + text).
| col | v | name | status | icon/tone | highlight |
|---|---|---|---|---|---|
| 1 | v1 | إعادة جدولة 60 | أُعيد | undo / error | – |
| 2 | v2 | إعادة جدولة 84 | بانتظار الاعتماد | hourglass_top / warning | selected: header bg `#FDF0EB` + top `inset 0 3px 0 #F4633A`; cells bg `#FFFBF9`, weight 700 |
| 3 | بديل | بيع طوعي (تقديري) | للمقارنة فقط | visibility / neutral | cells muted `#5E5D58` |

Rows (rowheader + 3 cells, verbatim):
| البند | v1 | v2 | بديل |
|---|---|---|---|
| المدة | 60 شهراً | 84 شهراً | — |
| القسط الشهري | 21,409.33 | 15,074.52 | — |
| التنازل | 0.00 | 18,300.00 (1.42%) | — |
| نسبة الاستقطاع | 66.1% ✕ (red) | 46.5% ✓ (green) | — |
| إجمالي المسترد (تقديري) | 1,284,560.00 | 1,266,260.00 | ≈ 1,540,000 − 1,284,560 للمالك |
| يبقى المالك في منزله | نعم | نعم | لا |
| يتطلب موافقة المالك | نعم (قبول العرض) | نعم (قبول العرض) | نعم (موافقة صريحة مسبقة) |
| مسار الموافقة | نورة الشهري | نورة الشهري | معتمد + القانونية (المرحلة 2) |
| المُعِدّ | فهد العتيبي | فهد العتيبي | — |
✓/✕ always accompanied by text colour + value (a11y).

Footnote (info icon): `«إجمالي المسترد» تقدير غير مخصوم ولا يشمل تكاليف التحصيل. البيع الطوعي يظهر للمقارنة فقط ولا يُعرض على المالك دون طلبه.`

### Actions
- `إبراز الفروق فقط` toggle: hide/dim rows where all option values are equal (or highlight differing cells).
- `تصدير PDF`: server-rendered comparison PDF (watermark + audit like document downloads).
- **No decision action on this screen.**

### Rules / states
- All versions retained (v1 returned stays visible). Version under approval is marked with marker + background + status text.
- Voluntary-sale column = estimate, comparison only; never offered to owner without his request (Phase 2 path).
- Responsive: 768 = two switchable columns; 390 = one version at a time with switcher.

Spec aside (verbatim):
- هدف المستخدم: اختيار الحل الأنسب بمقارنة الأرقام والأثر على المالك ومسار الموافقة.
- البيانات: كل الإصدارات تبقى. الإصدار قيد الاعتماد مميز بعلامة وخلفية ونص حالته.
- الإجراءات: تصدير PDF، إبراز الفروق. لا إجراء قرار هنا.
- الوصول: جدول بعناوين صفوف وأعمدة؛ ✓ و✕ مصحوبة بنص.
- التجاوب: 768: عمودان قابلان للتبديل. 390: إصدار واحد في كل مرة مع مبدّل.

---

## L15 — طلب الموافقة · Approval request (submit review screen)

- Artboard (in Anchor Screens B): `P0-Lender-CaseWorkspace-Desktop-SubmitApprovalReview · 1440`.
- Role: reviewer = مديرة الحالات (سارة القحطاني) — the builder's primary button is `تسليم لمديرة الحالة للمراجعة`; Brief lists L15 role as المحلل (see conflicts).
- Route `/cases/[ref]/solutions/[version]/submit`. Shell: `LenderSidebar active="cases"`, `LenderTopbar crumb1="RH-2026-004172" crumb2="مراجعة قبل الإرسال"`; **no CaseHeader** (focused review page).

### Layout
Main grid `minmax(0,1fr) 380px`; content column max-width 760. Sticky bottom action bar (76px, white, top border) spanning the content area.

### Content
Eyebrow `إجراء عالي الأثر · يُسجَّل في السجل`; `<h2>مراجعة قبل إرسال الحل v2 للموافقة الداخلية</h2>`.
Numbered sections (`h3` with number circle):
1. `ما سيحدث`:
   - (swap_horiz) `تنتقل الحالة من **حل مقترح** إلى **موافقة داخلية**.`
   - (edit_off) `يُقفل الإصدار v2 للتعديل. أي تغيير لاحق ينشئ v3 ويعيد السلسلة.`
   - (person) `يُسند إلى **نورة الشهري** (حدها 2,000,000 ر.س، التنازل حتى 5%) · المهلة 3 أيام: 2026-09-26.`
   - (visibility_off) `لن يرى المالك أي عرض قبل الاعتماد. لا يُرسل له إشعار الآن.`
2. `الأدلة المرفقة تلقائياً` — rows (check_circle + name + meta + link `معاينة`):
   - الحل v2 (نسخة مقفلة) — 16:05 · فهد العتيبي
   - تحليل القدرة على السداد — 46.5% استقطاع
   - تقرير التقييم v1 — صالح حتى 2026-12-09
   - كشف الراتب v2 — متحقق 2026-09-20
3. `ملاحظتك للمعتمد` `(إلزامية)` — textarea (`aria-label="ملاحظة للمعتمد"`), sample: `راجعت الحل مع كشف الراتب v2. القسط ضمن حد الاستقطاع، والتنازل محصور في غرامات التأخير فقط.` + checkbox (checked) `أؤكد أنني راجعت شروط الحل والأدلة، وأنني لست مُعِدّة هذا الإصدار.`

Aside `ملخص الحل v2` + `RH-2026-004172`: النوع: إعادة جدولة · المبلغ المعاد جدولته: 1,266,260.00 ر.س · المدة: 84 شهراً · القسط الشهري: **15,074.52 ر.س** · التنازل: 18,300.00 (1.42%) · نسبة الاستقطاع: ✓ 46.5%.
Info note: `القيم أعلاه نسخة مقفلة ستُرسل كما هي. قاعدة إشعار الامتثال عند التنازل > 1% — **افتراض يتطلب تأكيد المنتج**.`

Bottom bar: secondary `رجوع للحالة` · caption `سيُسجل: الفاعل، الوقت، الملاحظة، نسخة v2` · primary `إرسال للموافقة`.

### Rules (review-screen pattern spec, verbatim essentials)
- Used for: الاعتماد، الإرسال للموافقة، الإلغاء، الإحالة، قبول عرض شراء، الإغلاق. لا حوار «نعم/لا».
- Structure: 1) ما سيحدث (الانتقال، القفل، الإسناد، المهلة، ما يراه المالك) 2) الأدلة 3) السبب وإقرار التفويض.
- `الزر الأساسي معطّل حتى يُكتب السبب ويُحدد الإقرار. للإلغاء والإحالة: إعادة إدخال رمز MFA.`
- Separation of duties: `إن كان المستخدم مُعِدّ الإصدار، يُمنع ويظهر السبب بدلاً من الإقرار.` (builder shows `أنت المُعِدّ: لا يمكنك المراجعة أو الاعتماد.`)
- After submit: success toast + return to workspace; next-action card becomes «بانتظار المعتمد».
- A11y: page title announces action; sections labelled & numbered; submit error moves focus to first missing field.
- Approver auto-selected by limits (amount, waiver %, solution type — A-08 configurable); if over limit, route shows higher approver instead of blocking input.
- Snapshot: submitted values are a locked copy; any change → new version (v3) and restart of chain.

---

## L16 — صندوق الموافقات · Approval inbox (with decision)

- Artboards: `P1-Lender-ApprovalInbox-Desktop-Decision · 1440` (min-h 1000); `P1-Lender-ApprovalInbox-Mobile · 390`.
- Role: المعتمد (نورة الشهري). Routes: `/approvals` (list) and `/approvals/[requestId]` (decision pane; mobile = separate full page).
- Shell: `LenderSidebar active="approvals" user-name="نورة الشهري" user-role="معتمدة · حتى 2,000,000" initials="ن ش" approvals="3"`; `LenderTopbar crumb1="الموافقات" crumb2="RH-2026-004172 · v2"`; **no CaseHeader**.

### Layout (desktop)
Main grid `400px minmax(0,1fr)`: list pane (white, inline-end border) | decision pane (padding 24/32).

### List pane
`<h2>الموافقات</h2>`; filter pills: `بانتظاري 3` (selected, ink) · `قرارات سابقة`.
Items (ref mono LTR · SLA chip coloured · title · meta); selected item: bg `#FDF0EB` + 3px orange bar on start edge:
| ref | title | meta | SLA | tone/icon |
|---|---|---|---|---|
| RH-2026-004172 (selected) | إعادة جدولة 84 شهراً · v2 | سارة القحطاني · 1,266,260.00 ر.س · تنازل 1.42% | متبقٍ 3 أيام | warning / alarm |
| RH-2026-004090 | فترة سماح 3 أشهر · v1 | سارة القحطاني · 1,120,450.00 ر.س | اليوم | error / alarm_off |
| RH-2026-003944 | سداد مخفض دفعة واحدة · v2 | خالد الزهراني · خصم 6% — فوق حدك، مُصعّد | 4 أيام | success / schedule |

### Decision pane
- Eyebrow `طلب اعتماد · أرسلته سارة القحطاني 2026-09-23 10:12`; `<h3>إعادة جدولة 84 شهراً — عبدالله م.</h3>`.
- 4 figure tiles (`repeat(4,…)`): label / value / sub-caption (tone):
  - القسط · 15,074.52 · `الاستقطاع 46.5% ✓` (success)
  - المدة · 84 شهراً · `كان 60 في v1` (muted)
  - التنازل · 18,300.00 · `حدك 5% · هنا 1.42%` (success)
  - المبلغ · 1,266,260.00 · `حدك 2,000,000` (success)
  (sub-caption turns warning/error when over limit.)
- Card `ملاحظة المراجِع`: `«راجعت الحل مع كشف الراتب v2. القسط ضمن حد الاستقطاع، والتنازل محصور في غرامات التأخير فقط.»` + evidence links: `الحل v2 (مقفل)` · `الفروق عن v1` · `تحليل القدرة` · `تقرير التقييم` · `معاينة ما سيراه المالك` (→ L17).
- Card `قرارك`:
  - `role="radiogroup" aria-label="القرار"` 3 radio cards (52px): `اعتماد وإرسال للمالك` (selected) · `إعادة للتعديل` · `رفض الحل`.
  - Effects box (changes per selected decision; approve copy verbatim): `عند الاعتماد` — `• تنتقل الحالة إلى **بانتظار العميل**، ويُرسل العرض للمالك بقالب «عرض إعادة جدولة».` / `• صلاحية العرض 10 أيام: حتى 2026-10-03.` / `• يُنشأ إشعار امتثال لأن التنازل > 1% (افتراض).`
  - `سبب القرار *` textarea; sample `الحل قابل للسداد وضمن حدودي، والتنازل مبرر بظرف موثق.`
  - Footer: (passkey) `سيُطلب رمز التحقق لتأكيد القرار` + primary button label per decision — approve: `تأكيد الاعتماد` (others e.g. «تأكيد الإعادة» / «تأكيد الرفض» — not drawn).

### Mobile (390×844)
Header: symbol logo (28px) + `الموافقات · 3`. Cards: ref · SLA text · title · meta · secondary full-width button `مراجعة`. Footnote: `القرار متاح على الجوال بنفس شاشة المراجعة ورمز التحقق؛ لا اعتماد بالسحب أو بلمسة واحدة.`

### Rules
- Sort by deadline (SLA). Items above the approver's limit display «مُصعّد» and cannot be approved by her (read-only / routed to higher approver).
- Decision: approve / return for edit / reject; **reason mandatory**; effect shown before confirming; **OTP (رمز التحقق) step-up** to confirm.
- Separation of duties: request is **not visible** to whoever prepared or reviewed it.
- States: empty (no requests), decided (read-only), request changed after opening (alert + reload; must re-open latest version).
- Effects: approve → case `customer` («بانتظار العميل»), offer generated from locked version and sent via template «عرض إعادة جدولة», expiry +10 days, compliance notice if waiver > 1%. Return → version status `أُعيد`, case back to `proposed`, preparer edits → new version. Reject → version `مرفوض` (case state after reject not specified).

Spec aside (verbatim):
- هدف المستخدم: قرار سريع ومبرر ضمن الحدود مع كل الأدلة في مكان واحد.
- الترتيب: حسب المهلة. العناصر فوق حد المعتمد تظهر «مُصعّد» ولا يمكنه اعتمادها.
- القرار: اعتماد / إعادة للتعديل / رفض. السبب إلزامي. أثر القرار مكتوب قبل التأكيد. رمز تحقق.
- فصل المهام: لا يظهر الطلب لمن أعدّه أو راجعه.
- الحالات: لا طلبات، تم القرار (قراءة فقط)، تغيّر الطلب بعد الفتح (تنبيه وإعادة تحميل).
- التجاوب: 390: قائمة ثم شاشة مراجعة كاملة؛ لا اعتماد بالسحب.

---

## L17 — معاينة العرض كما يراه المالك · Debtor-offer preview

- Artboard `P1-Lender-DebtorOfferPreview-Desktop · 1440` (1440×960).
- Role: مدير الحالات (also reachable by approver, builder). Route `/cases/[ref]/solutions/[version]/preview`.
- Shell: `LenderSidebar active="cases"`, `LenderTopbar crumb1="RH-2026-004172" crumb2="معاينة العرض كما يراه المالك"`; no CaseHeader.

### Layout
Main grid `minmax(0,1fr) 440px`: checks column | 390px phone mock of debtor portal (D07 offer screen).

### Left column
`<h2>معاينة العرض كما يراه المالك</h2>`; warning note `role="note"` (icon visibility): `معاينة فقط. لم يُرسل شيء للمالك بعد.`
Card `فحص الوضوح قبل الإرسال` (icon + text):
- ✓ `الرقم الأهم (القسط) أولاً وبخط كبير`
- ✓ `لا مصطلحات: «تنازل عن الغرامات» ← «نلغي غرامات التأخير»`
- ✓ `ما يحدث عند التأخر مكتوب بلا تهديد`
- ✓ `خيار «أحتاج مساعدة» ظاهر بنفس الأهمية تقريباً`
- (info, blue) `مستوى القراءة: مناسب للمرحلة المتوسطة (فحص آلي — افتراض)`
Buttons (secondary): `معاينة بالإنجليزية` · `معاينة الرسالة النصية`.

### Phone mock (owner view, verbatim)
- Top: symbol logo + `حالتك مع مصرف الأفق`.
- Eyebrow `عرض من مصرف الأفق · صالح حتى 2026-10-03`; `<h3>قسط شهري أقل لمدة أطول</h3>`.
- Hero: `قسطك الجديد` / `15,074.52` `ريال` / `كل شهر لمدة 84 شهراً، يبدأ 2026-11-01`.
- List: ✓ `نلغي غرامات التأخير: 18,300 ريال` · ✓ `تبقى في منزلك وتستمر ملكيتك` · (info) `إذا تأخرت قسطين متتاليين، نتواصل معك أولاً ونمنحك 15 يوماً للتصحيح`.
- Buttons (52px, 17px): primary `مراجعة وقبول العرض` · secondary `اقتراح بديل`; link `لا يناسبني · أحتاج مساعدة`. (Inert in preview.)

### Rules
- Shows exactly what owner sees on a 390 phone, with clarity check and alternate language (EN) + SMS preview.
- Tone: no threatening words; «إذا تأخرت» explained with supportive action first.
- Offer copy generated from locked version + template; owner-facing labels differ from internal (waiver → «نلغي غرامات التأخير»).

---

## L18 — التفاوض · Negotiation (counteroffer)

- Artboard `P1-Lender-Negotiation-Desktop-Counteroffer · 1440` (min-h 960).
- Role: مدير الحالات. Route `/cases/[ref]/solutions/negotiation` (or `/cases/[ref]/offers/[offerId]`).
- Shell: `LenderTopbar crumb1="RH-2026-004172" crumb2="التفاوض"`; `CaseHeader tab="solutions" state-key="negotiation" sla-text="الرد على المالك خلال 3 أيام · 2026-10-01" sla-tone="ok" stage="5"`.

### Layout
Main grid `minmax(0,1fr) 400px`: thread | aside (comparison + next action).

### Thread `سلسلة العروض`
Articles (icon · who · time LTR · body · terms line). Owner entries indented 48px (inline-start) with rust tint (`#FDF0EB`/`#F0B8A6`); internal notes warm `#FAF9F6`; official offers white.
1. send — `مصرف الأفق — العرض v2` — 2026-09-24 09:00 — `قسط 15,074.52 ريال لمدة 84 شهراً، يبدأ 2026-11-01، مع إلغاء غرامات التأخير.` — `أُرسل بعد اعتماد نورة الشهري · صالح حتى 2026-10-03`
2. person — `عبدالله م. — اقتراح بديل` — 2026-09-27 20:41 — `«أوافق على المبدأ، لكن راتبي يتأخر أحياناً إلى يوم 5. أرجو أن يكون موعد القسط يوم 10، وأن يبدأ في ديسمبر.»` — `طلب: تاريخ الاستحقاق يوم 10 · البدء 2026-12-10`
3. forum — `سارة القحطاني — ملاحظة داخلية` — 2026-09-28 10:05 — `الطلب معقول ولا يغيّر المبلغ. تأجيل شهر واحد يحتاج اعتماداً جديداً لأنه يغيّر تاريخ الانتهاء.` — `مرئية لفريق الحالة فقط`

### Aside
Table `المقابل مقارنة بـ v2` (cols `1.2fr 1fr 1fr`): `البند` | `v2` | `طلب المالك`; changed values in rust `#8E3920`:
| البند | v2 | طلب المالك |
|---|---|---|
| القسط | 15,074.52 | 15,074.52 |
| يوم الاستحقاق | 1 | 10 |
| البدء | 2026-11-01 | 2026-12-10 |
| الانتهاء | 2033-10-01 | 2033-11-10 |
Card `الإجراء التالي`: primary `إعداد v3 بناءً على الطلب` · secondary `الرد بتوضيح دون تغيير` · secondary `الاعتذار عن الطلب مع السبب…` · caption `أي v3 يمر بالمراجعة والاعتماد من جديد. الاعتذار يعيد الحالة إلى «حل مقترح»، لا إلى الإحالة.`

### Actions / rules
- `إعداد v3…` → opens builder prefilled from v2 + counter terms → new version → review (L15) → approval (L16) → new offer.
- `الرد بتوضيح…` → message to owner, no terms change (offer stays open).
- `الاعتذار…` → dialog requiring reason; owner notified; case → `proposed` (never auto-referral).
- Any change to end date / term / amount requires new approval.
- Owner reply has its own service deadline (here 3 days).
- Internal notes hidden from owner; 390: full-width thread, collapsible comparison.

Spec aside (verbatim):
- المعاينة: تعرض ما سيراه المالك حرفياً على جوال 390، مع فحص الوضوح واللغة البديلة.
- النبرة: لا كلمات تهديد. «إذا تأخرت» تُشرح بإجراء داعم أولاً.
- التفاوض: سلسلة زمنية: عروض رسمية، طلبات المالك، ملاحظات داخلية (مميزة ومخفية عن المالك).
- الإجراءات: إعداد vN، توضيح، اعتذار مسبب. الاعتذار ≠ إحالة.
- المهلة: الرد على المالك له مهلة خدمة مستقلة.
- التجاوب: 390: السلسلة كاملة العرض والمقارنة قابلة للطي.

---

## L19 — الاتفاق · Agreement (accepted)

- Artboard `P1-Lender-Agreement-Desktop-Accepted · 1440` (min-h 960).
- Roles: القانونية، مدير الحالات. Route `/cases/[ref]/agreement` (Brief: `الحالة › الاتفاق`; header tab stays `solutions`).
- Shell: `LenderTopbar crumb1="RH-2026-004172" crumb2="الاتفاق"`; `CaseHeader tab="solutions" state-key="settlement" sla-text="أول قسط 2026-11-01" sla-tone="ok" stage="6"`.

### Layout
Main grid `minmax(0,1fr) 380px`.

### Content
Header `<h3>اتفاق إعادة الجدولة</h3>` + mono tag `AGR-2026-004172-01 · v3`.
Terms rows (grid `200px 1fr`, verbatim):
- الأطراف: مصرف الأفق · عبدالله م. (الهوية 1•••••••42)
- العقد الأصلي: MF-88-3317••• بتاريخ 2021-05-10
- المبلغ المعاد جدولته: 1,266,260.00 ر.س (بعد إلغاء غرامات 18,300.00)
- الأقساط: 84 قسطاً × 15,074.52 ر.س · يوم 10 من كل شهر
- الفترة: 2026-12-10 إلى 2033-11-10 · 19 جمادى الآخرة 1448هـ (Hijri of start date)
- شرط التأخر: قسطان متتاليان ← مراجعة ومهلة تصحيح 15 يوماً
- طريقة السداد: تحويل بنكي إلى حساب المصرف (خارج المنصة)
Buttons: `النص الكامل PDF` (icon description) · `مقارنة مع العرض المعتمد`.

Aside:
1. Card (success title, icon task_alt) `سجل موافقة المالك`: `قبل عبدالله م. العرض v3 داخل البوابة` / `2026-10-02 14:21 · رمز تحقق إلى +966 5• ••• ••81 · جهاز: iPhone` / `أقرّ بقراءة الشروط، واطّلع على «ماذا لو تأخرت؟»`.
2. Card `خطوات التفعيل`: ✓ `مراجعة القانونية · ماجد الحربي` · ✓ `إنشاء جدول السداد (84 قسطاً)` · (schedule, pending) `تحديث نظام التمويل الأساسي · مهمة للمالية`.
3. Dashed neutral note (icon draw): `**التوقيع الإلكتروني المرخّص:** غير مفعّل في هذه المرحلة. الأثر الملزم لسجل الموافقة — **افتراض يتطلب تأكيداً قانونياً**.`

### Rules
- In-platform acceptance ≠ licensed signature (A-05): store consent record (timestamp, OTP destination masked, device, acknowledgements). Licensed e-sign = conditional pattern later (X01, Phase 2).
- Agreement version must equal approved offer version (compare action).
- Activation steps: legal review → schedule generation → core-system update task for Finance (manual, outside platform).
- Payment happens outside platform (bank transfer).

---

## L20 — جدول السداد وتسجيل الدفعات · Payment schedule & recording

- Artboards: `P1-Lender-PaymentSchedule-Desktop-RecordPayment · 1440` (1440×1000, drawer open); `P1-Lender-PaymentSchedule-Mobile · 390`.
- Role: المالية (ريم الدوسري). Route `/cases/[ref]/payments`; drawer `?record=1` / intercepting route; mobile record = full page `/cases/[ref]/payments/record`.
- Shell: `LenderSidebar active="cases" user-name="ريم الدوسري" user-role="المالية" initials="ر د"`; `LenderTopbar crumb1="RH-2026-004172" crumb2="المدفوعات"`; `CaseHeader tab="payments" state-key="settlement" sla-text="القسط 3 مستحق 2027-02-10" sla-tone="ok" stage="6"`.

### Layout / content
KPI tiles (`repeat(4,…)`): المُعاد جدولته `1,266,260.00` ر.س · المسدد `15,074.52` `1 من 84 قسطاً` · المتبقي `1,251,185.48` ر.س · بانتظار المطابقة `1` `دفعة مسجلة`.
Toolbar: `جدول السداد` + secondary `استيراد مراجع بنكية` (icon upload) + primary `تسجيل دفعة`.
Table (`role="table"`, cols `60px 1fr 1fr 1.2fr 1.6fr 1.3fr`): `#` | `الاستحقاق` | `المبلغ` | `المدفوع` | `المرجع` | `الحالة` (badge icon + text):
| # | due | amount | paid | ref | status | icon / tone |
|---|---|---|---|---|---|---|
| 1 | 2026-12-10 | 15,074.52 | 15,074.52 | TRX-88392214 | مطابقة | check_circle / success |
| 2 | 2027-01-10 | 15,074.52 | 15,074.52 | TRX-88410027 | مسجلة — بانتظار المطابقة | pending / warning |
| 3 | 2027-02-10 | 15,074.52 | — | — | مستحقة | schedule / `#22262A` on `#F2F1ED` |
| 4–6 | 2027-03-10 … 2027-05-10 | 15,074.52 | — | — | قادمة | radio_button_unchecked / muted on white |

Drawer `role="dialog" aria-labelledby="pay-h"`, 480px inline-end, scrim .32: `<h3>تسجيل دفعة يدوياً</h3>` + close.
- `القسط` — select: `القسط 3 · 2027-02-10` (defaults to earliest due/unpaid).
- `المبلغ *` — number input LTR `15,074.52` + suffix `ر.س` (default = installment amount).
- `تاريخ الاستلام *` — date `2027-02-08`.
- `مرجع التحويل البنكي *` — text LTR `TRX-88457310`; inline async validation success (check_circle): `المرجع غير مستخدم في أي دفعة سابقة` (error variant: duplicate reference → block).
- `الإثبات` — file attachment `كشف_حساب_فبراير.pdf · 240 ك.ب` (optional per design; no asterisk).
- Info note (icon rule, blue): `تُحفظ الدفعة بحالة **مسجلة — بانتظار المطابقة**، ويطابقها موظف مالية آخر. لا تظهر للمالك كمطابقة قبل ذلك.`
- Footer: `إلغاء` · primary `تسجيل وإرسال للمطابقة`.

### Mobile (390)
Header: back + `المدفوعات`. Progress card: `سُدد 1 من 84` + progress bar + `المتبقي 1,251,185.48 ر.س`. Cards (first 4): `القسط {n} · {due}` + amount + status text. Recording = full page.

### Rules
- Payments outside platform (A-04): manual entry or bank-reference import, **maker-checker** (recorder ≠ matcher, both Finance).
- Reference checked for duplicates; amount ≠ installment requires reason (field appears conditionally).
- Payment statuses: `قادمة` (upcoming) · `مستحقة` (due) · `مسجلة — بانتظار المطابقة` (recorded, pending match) · `مطابقة` (matched) · `جزئية` (partial) · `متأخرة` (overdue) · `مرفوضة` (rejected).
- Owner sees only matched payments, labelled «مستلمة» with reference.
- Paid/remaining KPIs count only matched payments.

Spec aside (verbatim):
- الاتفاق: ملخص الشروط، سجل الموافقة (الوقت، الرمز، الجهاز)، خطوات التفعيل. التوقيع المرخص نمط مشروط لاحق.
- السداد: الدفع خارج المنصة. التسجيل يدوي أو باستيراد مراجع، بصانع ومدقق.
- التحقق: المرجع يُفحص ضد التكرار. المبلغ المختلف عن القسط يتطلب سبباً.
- الحالات: قادمة، مستحقة، مسجلة بانتظار المطابقة، مطابقة، جزئية، متأخرة، مرفوضة.
- ما يراه المالك: الدفعة المطابقة فقط تظهر «مستلمة» مع مرجعها.
- التجاوب: 390: بطاقات أقساط؛ التسجيل صفحة كاملة.

---

## L21 — معالجة الإخلال · Breach handling

- Artboard `P1-Lender-BreachReview-Desktop-Default · 1440` (min-h 960).
- Roles: مدير الحالات، المالية. Route `/cases/[ref]/payments/breach/[breachId]`.
- Shell: `LenderTopbar crumb1="RH-2026-003870" crumb2="مراجعة الإخلال"`; `CaseHeader tab="payments" case-ref="RH-2026-003870" title="ماجد ت. — شقة سكنية، حي العزيزية، المدينة المنورة" state-key="settlement" sla-text="مهلة التصحيح: 11 يوماً · 2026-10-04" sla-tone="warn" stage="6"`.

### Layout
Main grid `minmax(0,1fr) 380px`.

### Content
Alert `role="alert"` (warning tone `#FBF2DE`/`#E2C27A`, icon warning): **`لم يُسدَّد قسطان متتاليان (3 و4)`** / `حسب شرط الاتفاق، فُتحت مراجعة إخلال ومُنح المالك 15 يوماً للتصحيح منذ 2026-09-19. الحالة العامة تبقى «تسوية نشطة» أثناء المراجعة.`
Card `ما حدث` (rows `24px 140px 1fr`: icon · date LTR · text):
| icon (tone) | date | text |
|---|---|---|
| event_busy (error) | 2026-08-10 | لم يُسدد القسط 3 |
| sms (muted) | 2026-08-12 | تذكير لطيف عبر المنصة ورسالة نصية |
| event_busy (error) | 2026-09-10 | لم يُسدد القسط 4 |
| flag (warning) | 2026-09-19 | فتح مراجعة إخلال آلياً · إشعار المالك بمهلة 15 يوماً وخيار المساعدة |
| mark_email_unread (muted) | 2026-09-21 | رسالة ثانية دون رد |
Card `المسارات المتاحة بعد المهلة` (3 columns; title · description · owner):
- `تصحيح` — `يسدد المالك المتأخر ← تعود التسوية طبيعية` — `تلقائي بعد المطابقة` (success bg)
- `إعادة هيكلة` — `حل جديد vN يمر بالمراجعة والاعتماد` — `المحلل ← المعتمد`
- `تقييم خيارات أخرى` — `بيع طوعي بموافقة المالك، أو تقييم إحالة منفصل` — `مدير الحالات + القانونية`

Aside:
- Next-action card (top border 3px orange): eyebrow `الإجراء التالي · لكِ` (gendered to assignee) · `التواصل مع المالك قبل انتهاء المهلة` · `لم يرد على الرسالتين الأخيرتين. جرّب مكالمة في الوقت المفضل (بعد 4 م).` · primary `جدولة مكالمة` · secondary `إرسال رسالة «هل تحتاج مساعدة؟»`.
- Info note: `لا يوجد انتقال تلقائي إلى الإحالة. أي تصعيد يتطلب قراراً منفصلاً من القانونية ومعتمداً.`

### Rules / states
- Trigger: agreement clause — 2 consecutive missed installments → system auto-opens BreachReview, notifies owner (cure period 15 days + help option). Case public state unchanged (`settlement`); breach is a sub-state.
- Breach states: `open (in cure)` → `cured` (auto when overdue amounts are matched) | `restructuring` (new solution vN) | `other_options` (voluntary sale w/ consent or separate referral assessment) | `closed`.
- No automatic referral; escalation requires separate Legal + Approver decision (review-screen pattern with MFA).
- Owner messaging starts with help, not consequences.
- 390: alert + next action first, then timeline.

Spec aside (verbatim):
- هدف المستخدم: التعامل مع التأخر بإنصاف وتوثيق، والتركيز على التواصل أولاً.
- القاعدة: الإخلال لا يغيّر الحالة العامة تلقائياً، بل يفتح مراجعة بمهلة تصحيح.
- الإجراءات: التواصل، إعادة هيكلة، تقييم خيارات أخرى بقرار منفصل.
- النبرة: رسائل المالك تبدأ بالمساعدة، لا بالعواقب.
- التجاوب: 390: التنبيه والإجراء التالي أولاً، الخط الزمني بعدهما.

---

## Data entities (B4)

- `Solution { id, caseId, kind: reschedule|reduced_payoff|grace_period|voluntary_sale, currentVersionId }`
- `SolutionVersion { id, solutionId, versionNo, status: draft|in_review|pending_approval|approved|returned|rejected|superseded|offered|accepted, termMonths, installmentAmount, firstDueDate, dueDay, lastDueDate, waiverAmount, waiverPct, downPayment, graceMonths, rescheduledAmount, dsr, dsrLimit, incomeDocVersionId, justification (internal), breachClause {missedConsecutive:2, cureDays:15}, offerValidityDays:10, preparedBy, preparedAt, lockedAt, lockedSnapshot(json), estRecovery, isComparisonOnly }`
- `ApprovalPolicy / ApproverLimit { institutionId, userId|role, maxAmount, maxWaiverPct, allowedKinds[] }` (A-08 configurable).
- `ApprovalRequest { id, caseId, solutionVersionId, submittedBy (reviewer), submittedAt, reviewerNote, reviewerAttestation:bool, assignedApproverId, dueAt, slaStatus, escalated:bool, escalatedToId, status: pending|approved|returned|rejected|superseded, evidence[] {kind, refId, label, meta}, versionHashAtOpen }`
- `ApprovalDecision { requestId, decidedBy, decision: approve|return|reject, reason, otpVerifiedAt, decidedAt, effects[] }`
- `ComplianceNotice { caseId, solutionVersionId, rule ('waiver>1%'), createdAt }`
- `Offer { id, caseId, solutionVersionId, templateId ('عرض إعادة جدولة'), sentAt, validUntil, status: sent|countered|accepted|expired|withdrawn|declined, ownerViewRendered{ar,en,sms} }`
- `NegotiationEntry { id, caseId, offerId?, kind: offer|owner_counter|owner_message|lender_clarification|internal_note|lender_decline, authorType: lender|owner, authorId, at, body, termsSummary, requestedTerms {dueDay, startDate, …}, visibility: all|case_team }`
- `Agreement { id ('AGR-2026-004172-01'), caseId, solutionVersionId, versionLabel ('v3'), parties, originalContractRef, rescheduledAmount, waiverAmount, installments, installmentAmount, dueDay, startDate, endDate, breachClause, paymentMethod ('bank_transfer_offplatform'), pdfDocumentId, status: pending_activation|active|breach_review|completed|terminated }`
- `ConsentRecord { agreementId|offerId, partyId, acceptedAt, channel ('portal'), otpDestinationMasked, device, acknowledgements[] ('terms_read','what_if_late_viewed') , ip? }`
- `ActivationStep { agreementId, key: legal_review|schedule_created|core_system_update, status, actorId, taskId, completedAt }`
- `Installment { id, agreementId, no, dueDate, amount, status: upcoming|due|recorded_pending_match|matched|partial|overdue|rejected, paidAmount }`
- `PaymentRecord { id, installmentId, amount, receivedDate, bankReference (unique per institution), proofDocumentId, varianceReason?, recordedBy, recordedAt, status: pending_match|matched|rejected, matchedBy (≠ recordedBy), matchedAt, rejectReason, source: manual|import }`
- `BankReferenceImport { id, fileId, uploadedBy, rows[], matchedCount, errors[] }`
- `BreachReview { id, caseId, agreementId, triggeredAt, trigger ('2_consecutive_missed'), missedInstallments[], cureDeadline, status: open|cured|restructuring|other_options|closed, events[] {icon, date, text}, nextAction {assigneeId, text}, outcomePath }`

## API operations (B4)
- `GET /cases/{ref}/solutions` (all versions + comparison rows); `GET /cases/{ref}/solutions/compare?versions=…&diffOnly=true`; `POST /cases/{ref}/solutions/compare/export` (PDF).
- `POST /solutions/{id}/versions` (new vN, e.g. from counteroffer `?fromVersion=v2&fromEntry=…`); `POST /solution-versions/{id}/calculate` (preview installment/DSR/approval route).
- `POST /solution-versions/{id}/submit-for-review` (preparer → reviewer); `POST /solution-versions/{id}/submit-for-approval {note, attestation}` (locks, 403 if actor = preparer).
- `GET /approvals?filter=pending|decided` (sorted by dueAt; excludes requests where user was preparer/reviewer); `GET /approvals/{id}` (with version hash; 409/alert if changed); `POST /approvals/{id}/decision {decision, reason, otp}` (403 if over limit → escalated).
- `GET /solution-versions/{id}/owner-preview?lang=ar|en&channel=portal|sms` (+ clarity checks).
- `POST /offers` (created on approval), `GET /cases/{ref}/negotiation`, `POST /cases/{ref}/negotiation/entries {kind: clarification|internal_note}`, `POST /offers/{id}/decline {reason}` (→ case proposed).
- Debtor side (B6): `POST /offers/{id}/accept {otp, acknowledgements}`, `POST /offers/{id}/counter {requestedTerms, message}`.
- `GET /cases/{ref}/agreement`, `GET /agreements/{id}/pdf`, `GET /agreements/{id}/diff-with-offer`, `POST /agreements/{id}/activation-steps/{key}/complete`.
- `GET /cases/{ref}/payments` (KPIs + schedule), `POST /installments/{id}/payments` (maker), `GET /payments/reference-check?ref=…`, `POST /payments/{id}/match` / `POST /payments/{id}/reject` (checker ≠ maker), `POST /cases/{ref}/payments/import-references` (file).
- `GET /breach-reviews/{id}`, job: nightly breach detection (2 consecutive unpaid after grace) → create review + owner notification; `POST /breach-reviews/{id}/actions {schedule_call|send_help_message}`, `POST /breach-reviews/{id}/outcome {path, reason}` (escalation = separate legal+approver decision).

## Integrations (as labelled)
- OTP / «رمز التحقق» for approver decisions and owner acceptance (SMS to masked phone).
- Notification templates: «عرض إعادة جدولة», SMS + portal; EN alternate language.
- «نظام التمويل الأساسي» update after agreement — manual task for Finance (no live integration).
- Bank transfers outside platform; «استيراد مراجع بنكية» file import only (no gateway, wallet or escrow — A-04).
- Licensed e-signature: «غير مفعّل في هذه المرحلة» — conditional Phase-2 pattern (X01).
- Readability auto-check «فحص آلي — افتراض».
- Compliance notice rule «التنازل > 1%» — assumption pending product confirmation.

---

## Reusable components (B4, in addition to B3 list)

| Component | Props / variants | Used in |
|---|---|---|
| `ComparisonTable` | `columns[{version, name, status, tone, selected}]`, `rows[{label, cells[]}]`, `diffOnly` | L14 (also builder v1/v2 mini table, L18 counter table `KVCompareTable`) |
| `VersionStatusBadge` | status → (أُعيد/undo/error, بانتظار الاعتماد/hourglass_top/warning, للمقارنة فقط/visibility/neutral, …) | L14, lists |
| `CheckMarkValue` | `value, ok:boolean` renders `46.5% ✓` / `66.1% ✕` with colour + text | L14, L15, L16 |
| `ReviewScreen` (high-impact action pattern) | `eyebrow, title, whatHappens[], evidence[], reasonLabel, attestationText, summaryAside, recordCaption, primaryLabel, requireMfa` | L15; reused for cancel, referral, accept purchase offer, close |
| `ApprovalInboxList` / `ApprovalListItem` | `ref, title, meta, slaText, slaTone, selected, escalated` | L16 desktop & mobile (card + `مراجعة`) |
| `FigureTile` | `label, value, caption, captionTone` | L16 (4), L20 KPIs |
| `DecisionPanel` | `options (approve|return|reject), effectsByOption, reason, onConfirm(otp)` | L16 |
| `OtpConfirm` | step-up dialog | L16, owner acceptance |
| `PhoneFrame` + `DebtorOfferCard` | `lender, validUntil, headline, installment, termText, benefits[], infoLine, actions` | L17 (and B6 D07) |
| `ClarityChecklist` | `items[{ok|info, text}]` | L17 |
| `NegotiationThread` / `ThreadEntry` | `kind (offer|owner|internal), who, time, body, terms` (owner indented 48px, internal tinted) | L18 |
| `NextActionCard` | `eyebrow, title, body, primary, secondary[] , caption` (top orange border) | L18, L21, workspace |
| `AgreementTerms` (`KVRow` 200px) | `rows` | L19 |
| `ConsentRecordCard` | `party, acceptedAt, otpTo, device, acknowledgements` | L19 |
| `StepChecklist` | `steps[{status done|pending, label}]` | L19 activation |
| `AssumptionNote` | dashed neutral callout with bold «افتراض يتطلب تأكيداً …» | L15, L19 |
| `PaymentScheduleTable` | cols #/due/amount/paid/ref/status; mobile `InstallmentCard` | L20 |
| `PaymentStatusBadge` | 7 statuses (see L20) | L20, debtor |
| `RecordPaymentDrawer` | `installments, defaultInstallment, onReferenceCheck` | L20 |
| `ProgressSummary` | `paidCount/total, remaining` | L20 mobile |
| `AlertBanner` | `role=alert`, tone, title, body | L21 |
| `EventTimeline` (date column 140px) | `items[{icon, tone, date, text}]` | L21 |
| `PathCards` | `items[{title, desc, owner, highlighted}]` | L21 |

## Conflicts / ambiguities (B4)

1. **L15 role**: Brief says المحلل; design has analyst submit to case manager (`تسليم لمديرة الحالة للمراجعة`) and case manager performs L15 with attestation «لست مُعِدّة هذا الإصدار». Chain = preparer → reviewer → approver. Implement the design's 3-step chain.
2. **L15 location**: designed only in Phase 0 anchor file, not in B4; frame names use `P0-` prefix.
3. **Offer validity**: approval screen says 10 days until 2026-10-03 (from 2026-09-23), but offer v2 sent 2026-09-24 09:00 still valid until 2026-10-03 (9 days). Decide: expiry from approval or from send.
4. **Agreement CaseHeader SLA** says `أول قسط 2026-11-01` though v3 starts 2026-12-10 (stale v2 value).
5. **Agreement version**: agreement `v3` reached via L18, but v3 approval/offer screens are not drawn; the thread stops at the internal note.
6. **Hijri in agreement period** `19 جمادى الآخرة 1448هـ` corresponds to start date only; end date Hijri missing.
7. **Installment rounding**: 84 × 15,074.52 ≠ 1,266,260.00 (0.32 short) — define last-installment adjustment. No profit/margin applied in the rescheduling math — confirm with finance (Murabaha restructuring may carry profit).
8. **Inbox sort**: spec says sort by deadline, but `اليوم` item appears 2nd after `متبقٍ 3 أيام`; and an escalated (over-limit) item appears in the approver's own "بانتظاري" list with green SLA — clarify whether escalated items belong in her inbox (read-only) or only the higher approver's.
9. **Return / reject effects & button labels** not drawn (only approve). Case state after reject undefined.
10. **SoD vs. inbox**: inbox hides requests from preparer/reviewer — ensure approver role cannot also be reviewer on same case.
11. **Payments timeline** jumps to 2027-02 (CaseHeader `القسط 3 مستحق 2027-02-10`) while the rest of the story is 2026-09/10 — seed data must pick one "today" per screen.
12. **Amount ≠ installment reason field** and duplicate-reference error state, `جزئية/متأخرة/مرفوضة` statuses, checker (match) UI, and import flow are specified in text only — not drawn.
13. **Proof attachment** not marked required; confirm.
14. **Breach trigger timing**: review auto-opened 2026-09-19, nine days after second missed due (2026-09-10) — implies an unspecified grace period (configurable).
15. **«لكِ»** (feminine) in breach next-action — must be gendered/neutral per assignee.
16. **Breach example case** `RH-2026-003870` sits in the same `settlement` stage 6 as 004172; case-ref/title passed as props — CaseHeader meta line still hard-codes `مصرف الأفق` / `تمويل سكني · مرابحة`.
17. **L19 route**: Brief `الحالة › الاتفاق` but CaseHeader has no `agreement` tab (uses `solutions`); decide whether agreement lives under solutions or payments.
