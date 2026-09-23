# B11 — Optimization & Decision Support (Phase 4)

Source: `design-source/07 Phase 4 - B11 Optimization.dc.html` (+ `_condensed/…txt`). Page title: `07 Phase 4 — B11 Optimization — رهون`. H1: «المرحلة 4 — الدفعة B11: التحسين ودعم القرار».

**Tagging convention:** statements come from the design unless marked **(inferred)**. All routes, entity and field names, and API operations are **(inferred)**.

## 0. Global principle (page intro, verbatim)

«كل مخرج آلي هنا **دعم قرار**: يعرض تفسيره ومصدر بياناته ودرجة ثقته، ويتطلب مراجعة بشرية، ويمكن تجاوزه بسبب، ويُسجّل. لا قرار ائتماني أو قانوني ملزم يصدر آلياً.»

- Handoff rules ("دعم القرار"):
  - «كل مخرج آلي يحمل modelVersion، inputs، confidence، limitations.»
  - «الرأي البشري (agree | override | not_used) إلزامي قبل الإرفاق بقرار.»
  - «لا يظهر للمالك ولا يغيّر حالة.»
- Handoff component `DecisionSupportTag`:
  - Anatomy: «وسم، نطاق، ثقة، عوامل، مصدر، قيود، تجاوز».
  - States: «متفق، متجاوز بسبب، غير مستخدم».
  - A11y: «الرسم له بديل نصي».
- Page footer: «صُمم في B11 — O01–O05: 5 إطارات سطح مكتب. نمط موحد لدعم القرار: وسم «دعم قرار»، نطاق بدل رقم، العوامل، المصدر، القيود، التجاوز بسبب، الحوكمة.»
- Charts follow C14 AccessibleChart:
  - RTL bars; chart/table toggle («عرض كجدول»); text values on every bar.
  - Orange is used only to highlight a single category.
  - Mobile at 390 defaults to the table.
- **Shared AI-output contract (inferred from the Handoff; apply to every automated output in O02, O03, O04 and O05):**
  - `AiOutputMeta { modelVersion, inputs[{name, version, asOf}], confidence, limitations, dataAsOf, humanReview { value: agree|override|not_used, reason, by, at } }`
  - Mapping to the Handoff tags (inferred):
    - O05 uses the tags explicitly.
    - O04: approve unedited ≈ `agree`; edit then approve ≈ `override`; reject ≈ `not_used`.
    - O02: «غير مفيدة…» ≈ `not_used`; «عرض الحالات» is not an opinion.
    - O03 experimental rule: a suggestion accepted ≈ `agree`, rejected ≈ `not_used`.
  - None of this metadata or output is ever exposed to the owner, and none of it changes case state. For O04, only the final approved agreement text reaches the owner, never the draft or its flags.
- Button suffix «…» (Handoff C01): «…» = شاشة مراجعة. Such a button opens a review or reason step instead of acting immediately. It applies to «اعتماد بعد المراجعة…» and «غير مفيدة…».
- Brief matrix:

| ID | AR | EN | Role | Area | Nav |
|---|---|---|---|---|---|
| O01 | تحليل المحفظة | Portfolio analysis | مسؤول المنشأة، المحلل | Lender | التحليلات |
| O02 | الاتجاهات والاختناقات | Trends & bottlenecks | مسؤول المنشأة | Lender | التحليلات › الاختناقات |
| O03 | العمليات القابلة للتهيئة | Configurable operations | مسؤول المنشأة | InstAdmin | الإعدادات › العمليات |
| O04 | مساعدة صياغة المستندات (دعم قرار) | Document drafting assist | القانونية | Lender | المستند › مسودة مقترحة |
| O05 | رؤى تنبؤية مع التفسير والمصدر والثقة | Predictive insights (decision support) | المحلل | Lender | الحالة › التحليل › رؤى |

---

## O01 — Portfolio analysis

- **Artboard:** `P4-Lender-PortfolioAnalysis-Desktop · 1440`. Section «O01 Portfolio analysis».
- **Role:** institution admin (ليلى الغامدي, «مسؤولة المنشأة», initials «ل غ»). The analyst also has access, per the Brief.
- **Route (inferred):** `/analytics/portfolio`.
- **Shell:** `LenderSidebar active="reports" user-name="ليلى الغامدي" user-role="مسؤولة المنشأة" initials="ل غ"` + `LenderTopbar crumb1="التحليلات" crumb2="تحليل المحفظة"`. No CaseHeader.
- **Layout** (single `main`, top to bottom):
  1. Header row.
  2. KPI grid `repeat(4, 1fr)`.
  3. Two charts `1fr 1fr`.
  4. Cohort table.
- **Responsive** (specO1): «768: الرسمان مكدسان؛ 390: المؤشرات ثم «عرض كجدول».»

### Content
- H2 «تحليل المحفظة». Data-provenance line (icon `database`): «1,395 حالة (نشطة + مغلقة خلال 12 شهراً) · بيانات حتى `2026-09-23 06:00`».
- Button (secondary) «المقارنة: آخر 12 شهراً ← السابقة». It is the comparison-period selector (inferred).
- **KPIs** (label, value, delta; the delta is green `#1E6A45` for all four):

| label | value | delta |
|---|---|---|
| حالات أُغلقت بحل ودي | 71% | +6 نقاط |
| نسبة الاسترداد | 93.4% | +1.1 نقطة |
| متوسط أيام الحل | 49 | −15 يوماً |
| إحالات | 2.3% | −0.8 نقطة |

- The delta colour means "good direction", not the sign. Days going down and referrals going down are both green.
- **Figure «نتائج الحالات المغلقة»** + link «عرض كجدول».
  - A single stacked bar (role=img, aria-label = `'نتائج الحالات المغلقة: ' + "{t} {v}"` joined by «، »).
  - A legend in 2 columns: swatch, label, value.

| outcome | count | colour | width (n/total) |
|---|---|---|---|
| تسوية ودية | 104 | #22262A | 70.7% |
| سداد كامل | 21 | #5E5D58 | 14.3% |
| بيع طوعي | 12 | #F4633A | 8.2% |
| إحالة قضائية | 6 | #85847F | 4.1% |
| أُلغيت | 4 | #CBCAC6 | 2.7% |

- Formula: `w = n / Σn × 100` (toFixed 1); `Σn = 147`.
- **Figure «نسبة الاسترداد حسب المنطقة»** + «عرض كجدول».
  - Rows use `110px 1fr 60px 70px`: name | bar | value | «{n} حالة».
  - Each row has aria-label `"{n}: {v}% من {c} حالة"`.

| region | recovery | cases | bar width |
|---|---|---|---|
| الرياض | 95.1% | 512 | 75.5% |
| جدة | 92.8% | 301 | 64% |
| الدمام | 91.9% | 188 | 59.5% |
| مكة | 90.4% | 122 | 52% |
| أخرى | 89.7% | 272 | 48.5% |

- Bar width: `(v − 80) × 5 %`, a truncated axis starting at 80%.
- Footnote: «الاسترداد = المسترد ÷ المديونية عند الفتح · مناطق بأقل من 10 حالات مدمجة في «أخرى».»
- **Cohort table «المجموعات حسب سنة التمويل»** (role=table, `1fr repeat(5,1fr)`). Columns: «السنة» | «الحالات» | «حل ودي» | «بيع طوعي» | «إحالة» | «متوسط أيام الحل».

| year | cases | amicable | voluntary sale | referral | avg days |
|---|---|---|---|---|---|
| 2019 | 214 | 68% | 9% | 4% | 61 |
| 2020 | 288 | 70% | 8% | 3% | 55 |
| 2021 | 402 | 73% | 7% | 2% | 47 |
| 2022 | 491 | 72% | 6% | 1% | 44 |

### Actions
- «المقارنة…» changes the comparison period (a menu; options not drawn).
- «عرض كجدول» toggles each chart to its table.

### Rules (specO1)
- «هدف المستخدم: فهم أداء المحفظة ونتائجها عبر الزمن والمناطق والمجموعات.»
- «الرسوم: كل رسم بقيمه النصية وبديل جدولي؛ اللون البرتقالي لإبراز فئة واحدة فقط.»
- «الخصوصية: مجمّع؛ الفئات الصغيرة مدمجة.» Groups with fewer than 10 cases merge into «أخرى»; apply this server-side (inferred).
- «المصدر: عدد الحالات ووقت البيانات ظاهران أعلى الصفحة.»

### Data and API (inferred)
- `GET /analytics/portfolio?period=last12m&compare=prev12m` returns:
  - `{ caseCount, dataAsOf, kpis[{key, value, unit, delta, deltaUnit, goodDirection}], outcomes[{key,label,count}], regions[{name, recoveryRate, caseCount}], cohorts[{year, cases, amicablePct, voluntarySalePct, referralPct, avgDaysToResolve}] }`.
- Aggregates are computed from a nightly snapshot (`dataAsOf`). They are tenant-scoped with small-cell suppression (k ≥ 10).
- KPI definitions to lock:
  - `recoveryRate = recovered / debtAtOpening`
  - `avgDaysToResolve` = open → closed
  - `amicableRate`
  - `referralRate`

---

## O02 — Trends & bottlenecks

- **Artboard:** `P4-Lender-Bottlenecks-Desktop · 1440`. Section «O02 Bottlenecks».
- **Role:** institution admin (ليلى الغامدي).
- **Route (inferred):** `/analytics/bottlenecks`.
- **Shell:** `LenderSidebar active="reports"` (ليلى الغامدي) + `LenderTopbar crumb1="التحليلات" crumb2="الاتجاهات والاختناقات"`.
- **Layout:** `main` is a grid `1fr 400px`.
  - Left: the stage-wait list, then the monthly trend chart.
  - Right aside: the analytic note (decision support), then «اتجاهات أخرى».
- **Responsive** (specO2): «سطح المكتب أولاً.»

### Content
- H2 «أين تنتظر الحالات؟».
- **Stage list** (role=list, aria-label «المراحل ومدة الانتظار"). Each listitem uses `170px 1fr 90px 80px`:
  - stage name, which is bold if hot;
  - a two-segment bar: work `w×6%` in charcoal, wait `wt×6%` in `#CBCAC6`, or orange `#F4633A` if hot;
  - median «{w+wt} يوماً»;
  - «{q} حالة».
  - aria-label: `"{n}: {w} أيام عمل و{wt} أيام انتظار، {q} حالة" + (hot ? "، انتظار فوق المعتاد" : "")`.

| stage | work d | wait d | median | cases | hot |
|---|---|---|---|---|---|
| التحقق | 3 | 3 | 6 يوماً | 188 | |
| التقييم | 5 | 9 | 14 يوماً | 141 | ✓ |
| إعداد الحل | 4 | 2 | 6 يوماً | 122 | |
| الموافقة الداخلية | 1 | 1 | 2 يوماً | 37 | |
| بانتظار العميل | 0 | 7 | 7 يوماً | 164 | |
| التفاوض | 3 | 2 | 5 يوماً | 58 | |
| التسوية المالية | 2 | 2 | 4 يوماً | 61 | |

- Legend: «وقت عمل» (charcoal), «وقت انتظار» (grey), «انتظار فوق المعتاد» (orange).
- **Figure «وسيط أيام التقييم شهرياً»** + «عرض كجدول».
  - 12 vertical columns, each with a value label; height `v/16×100%`. Colour is orange if `v ≥ 13`, else charcoal.
  - Month labels (LTR): 10 11 12 01 02 03 04 05 06 07 08 09.
  - Values: 8, 8, 9, 8, 9, 9, 10, 9, 10, **13, 14, 14**.
  - aria: «وسيط أيام التقييم من أكتوبر 2025 إلى سبتمبر 2026: 8، 8، 9، 8، 9، 9، 10، 9، 10، 13، 14، 14».
- **Analytic note card:**
  - Eyebrow «ملاحظة تحليلية · دعم قرار».
  - Title «التقييم هو أكبر اختناق منذ يوليو».
  - Body «9 من 14 يوماً وسيطاً في «انتظار إسناد المقيّم»، بعد توقف مكتب التقييم «ح» لانتهاء ترخيصه.»
  - Meta:
    - «**المصدر:** سجل انتقالات 141 حالة تقييم»
    - «**الثقة:** عالية (نمط متكرر 3 أشهر)»
    - «**ليست سبباً مؤكداً:** ارتباط زمني، يحتاج تأكيداً»
  - Buttons:
    - «عرض الحالات» (primary): opens the case list filtered to the affected valuation-stage cases.
    - «غير مفيدة…» (secondary): opens a reason prompt and records the feedback (inferred).
- **«اتجاهات أخرى»** (icon + text):
  - `trending_down` (green): الردود على العروض أسرع بـ 2 يوم بعد تبسيط نص العرض (يوليو).
  - `trending_up` (amber): ارتفاع الشكاوى المتعلقة برفض المستندات — مرتبط بخلل القالب SUP-2026-1201.
  - `trending_flat` (grey): نسبة الإحالة مستقرة عند 2–3%.

### Rules (specO2)
- «التمثيل: عمل مقابل انتظار لكل مرحلة؛ الانتظار غير المعتاد بالبرتقالي مع نص في الوصف.»
- «الملاحظات الآلية: تفرّق بين الارتباط والسببية، مع المصدر والثقة، وقابلة للرفض كـ «غير مفيدة».»
- «الإجراءات: فتح الحالات المتأثرة؛ لا تغيير آلي في العمليات.»
- The "hot" threshold compares the stage's wait with its usual wait. The threshold is not defined (e.g. above a historical p75; inferred).

### Data and API (inferred)
- `GET /analytics/bottlenecks` returns `{ stages[{key, name, medianWorkDays, medianWaitDays, openCount, isAboveNormal}], monthlyTrend[{stageKey:"valuation", month, medianDays}] }`.
- `AnalyticInsight { id, kind: bottleneck|trend, title, body, source, confidence: high|medium|low, confidenceNote, causalityNote, affectedCaseQuery, ai: AiOutputMeta (modelVersion + limitations required; not shown in the design), feedback: useful|not_useful, feedbackReason, createdAt }`.
- `POST /insights/{id}/feedback { value: not_useful, reason }`.
- `GET /cases?stage=valuation&waitReason=awaiting_valuer_assignment` («عرض الحالات»).

---

## O03 — Configurable operations

- **Artboard:** `P4-InstAdmin-ConfigurableOps-Desktop · 1440`. Section «O03 Operations».
- **Role:** institution admin (ليلى الغامدي).
- **Route (inferred):** `/settings/operations`. SettingsNav highlights `sla` («مستوى الخدمة والمهام»), so this is either a sub-page of `/settings/sla` or an extension of it.
- **Shell:**
  - `LenderSidebar active="none"` (ليلى الغامدي)
  - `LenderTopbar crumb1="الإعدادات" crumb2="العمليات"`
  - `SettingsNav active="sla"` (220px). The SettingsNav items are: المنشأة، المستخدمون والأدوار، قواعد المستندات، حدود الموافقة، قوالب التواصل، مستوى الخدمة والمهام، مقدمو الخدمة، التقارير.
- **Layout:** `main` holds SettingsNav plus a content area.
  - Content: H2 + subtitle, then a 2-column grid (routing rules | team capacity), then the reminder-cadence card.

### Content
- H2 «العمليات القابلة للتهيئة». Subtitle «تعديلات تُعتمد وتُسجّل».
- **«قواعد الإسناد»** (title, description, status chip):

| rule | description | status |
|---|---|---|
| حالات جديدة ← مدير حالات بأقل عبء | توزيع آلي ضمن المنطقة؛ يمكن للمدير إعادة الإسناد بسبب. | مفعّل (green) |
| تقييم ← مقيّم من الدليل بالتناوب | يستبعد المنتهية تراخيصهم تلقائياً. | مفعّل |
| حالات فوق 3M ← محلل أول | إلزامي. | مفعّل |
| اقتراح أولوية بناءً على الاختناق | يقترح فقط؛ لا يعيد الترتيب دون موافقة. | تجريبي (info blue) |

- Status enum (inferred): `enabled | experimental | disabled`. «تجريبي» suggests only and never executes (specO3).
- **«سعة الفرق»** (label, «n / max», progress bar; aria `"{t}: {n} من {max}"`):

| team | n | max | % | colour |
|---|---|---|---|---|
| التحصيل — الرياض | 112 | 140 | 80% | charcoal |
| التحصيل — جدة | 96 | 100 | 96% | red #B3261E |
| التحليل | 74 | 90 | 82% | charcoal |
| الموافقات | 11 | 30 | 37% | charcoal |

- Formula: `w = round(n/max×100)%`; red when `n/max > 0.9`.
- **«إيقاع التذكيرات للمالك»**: a horizontal sequence of 150px chips (day, action):
  - اليوم 0 → إرسال العرض
  - اليوم 3 → تذكير لطيف
  - اليوم 7 → عرض مكالمة
  - اليوم 9 → تذكير أخير بالمهلة
  - Constraint note: «حد أقصى 2 تذكير أسبوعياً، وضمن أوقات التواصل المفضلة. لا تذكير أثناء شكوى مفتوحة.»

### Actions
- No edit controls are drawn. The subtitle says every change is approved and logged.
- (inferred) Edit rule, toggle status, edit capacity max and edit cadence each go through a change request, then second-person approval, then an audit entry. The details are not designed.

### Rules (specO3)
- «هدف المستخدم: ضبط الإسناد والسعة والتذكيرات دون فقدان السيطرة البشرية.»
- «القواعد: كل قاعدة بحالتها؛ «تجريبي» يقترح ولا ينفذ.»
- «الحدود: إيقاع التذكير محدود ومحترم لأوقات المالك وللشكاوى.»
- «التسجيل: كل تعديل مُعتمد ومسجل.»
- Server guards (inferred):
  - reminder scheduler: at most 2 per rolling week per owner;
  - reminders only within the owner's preferred contact windows;
  - reminders suppressed while any complaint on the case is open;
  - the valuer rotation excludes expired licences;
  - cases above 3M SAR are assigned to a senior analyst (mandatory);
  - manual reassignment needs a reason.

### Data and API (inferred)
- `RoutingRule { id, orgId, trigger, target, strategy: least_load|round_robin|threshold|ai_suggest, params (region, amountThreshold=3,000,000), description, status: enabled|experimental|disabled, version, approvedBy, approvedAt }`.
- `PrioritySuggestion { id, ruleId, caseIds[], suggestedOrder, ai: AiOutputMeta, decision: accepted|rejected|pending }`. This covers the «تجريبي» rule; the queue order changes only after a human accepts.
- `TeamCapacity { teamId, name, currentLoad, maxLoad }`.
- `ReminderCadence { orgId, steps[{dayOffset, action}], maxPerWeek: 2, respectContactWindows: true, suppressDuringOpenComplaint: true, version }`.
- `ConfigChangeRequest { id, entityType, entityId, diff, requestedBy, approvedBy, status, reason }`.
- Endpoints:
  - `GET /settings/operations`
  - `POST /settings/operations/change-requests`
  - `POST /change-requests/{id}/approve`

---

## O04 — Document drafting assist (review)

- **Artboard:** `P4-Legal-DraftingAssist-Desktop-Review · 1440`. Section «O04 Drafting».
- **Role:** القانونية (ماجد الحربي, initials «م ح»).
- **Route (inferred):** `/cases/[caseRef]/agreement/drafts/[draftId]` (crumb «مسودة الاتفاق»).
- **Shell:** `LenderSidebar active="cases" user-name="ماجد الحربي" user-role="القانونية" initials="م ح"` + `LenderTopbar crumb1="RH-2026-004172" crumb2="مسودة الاتفاق"`. No CaseHeader.
- **Layout:** `main` is a grid `1fr 400px`. Left: header, document body card, action row. Right aside: value sources, confidence and limitations, audit note.

### Content
- H2 «مسودة مقترحة: اتفاق إعادة الجدولة». Tag (info blue, icon `auto_awesome`): «مسودة آلية · لم تُراجع».
- **Draft body** (auto-filled values rendered as `<mark>`, blue `#EAF2F9`):
  - «**البند 3 — الأقساط:** يلتزم الطرف الثاني بسداد [84 قسطاً شهرياً] قيمة كل منها [15,074.52 ريال]، تستحق في اليوم [العاشر] من كل شهر ابتداءً من [2026-12-10].»
  - «**البند 5 — التأخر:** في حال تأخر الطرف الثاني عن سداد قسطين متتاليين، يتواصل الطرف الأول معه ويمنحه مهلة تصحيح مدتها خمسة عشر يوماً قبل مراجعة الاتفاق.»
  - «**البند 7 — الإنهاء:**» [flagged text, wavy amber underline] «يحق للطرف الأول إنهاء الاتفاق فوراً عند أي إخلال.»
    - Warning (amber, 13px, bold): «⚠ يتعارض مع البند 5 ومع سياسة المنشأة — مقترح حذفه».
    - The paragraph background is `#FBF2DE`.
- **Actions row:**
  - «رفض المسودة» (secondary)
  - «تحرير يدوي» (secondary)
  - spacer
  - «اعتماد بعد المراجعة…» (primary; the «…» means it opens a review screen)
- **«مصادر كل قيمة»** (value → source):
  - 84 قسطاً · 15,074.52 → الحل v3 المعتمد
  - اليوم العاشر → طلب المالك 2026-09-27
  - 2026-12-10 → الحل v3
  - مهلة 15 يوماً → سياسة المنشأة + البند 5
- **«الثقة والقيود»:**
  - «مطابقة القالب المعتمد: **98%** · اختلاف واحد مُعلَّم»
  - «المساعد لا يضيف شروطاً قانونية جديدة؛ يملأ القالب ويعلّم التعارضات.»
  - «النموذج: مساعد الصياغة `v0.3` (افتراض) · القالب `TPL-AGR-02 v5`»
- Audit note (icon `history`): «يُسجَّل: المسودة الآلية، كل تعديل بشري، ومن اعتمد النص النهائي.»

### Actions and effects (inferred where not stated)
- «رفض المسودة»: discards the AI draft and records the rejection (a reason is optional or required; not specified).
- «تحرير يدوي»: opens an editor. Each human edit is versioned and logged.
- «اعتماد بعد المراجعة…»: opens a ReviewScreen (what will happen, reason, MFA per the Handoff ReviewScreen), then approves the final text.
  - It must be blocked while unresolved flagged conflicts remain (inferred).
  - There is no automatic sending (specO4 «القرار»: «رفض، تحرير، أو اعتماد عبر شاشة مراجعة؛ لا إرسال آلي.»).

### Rules (specO4)
- «هدف المستخدم: تسريع الصياغة مع بقاء المسؤولية القانونية بشرية.»
- «الشفافية: كل قيمة مُعبأة مميزة ومرتبطة بمصدرها؛ التعارضات مُعلّمة بنص.»
- «الحدود: لا شروط جديدة خارج القالب المعتمد.»
- «التسجيل: المسودة الآلية والتعديلات والاعتماد النهائي.»
- AI output metadata:
  - `modelVersion` = «مساعد الصياغة v0.3»
  - `templateId` = TPL-AGR-02 v5
  - `inputs` = solution v3, owner request 2026-09-27, org policy
  - `confidence` = template match 98%
  - `limitations` = the text above

### Data and API (inferred)
- `DocumentDraft { id, caseId, templateId, templateVersion, origin: ai|human, ai: AiOutputMeta (modelVersion "مساعد الصياغة v0.3", confidence = templateMatchPct 98%, limitations text, humanReview), status: unreviewed|rejected|editing|approved, filledValues[{placeholder, value, sourceType, sourceRef}], flags[{clauseNo, text, message, severity, suggestedAction: delete, resolution}], body, versions[{by, at, diff}], approvedBy, approvedAt, rejectionReason }`.
- Endpoints:
  - `POST /cases/{ref}/drafts { templateId }` (AI generate)
  - `GET /drafts/{id}`
  - `POST /drafts/{id}/reject`
  - `PUT /drafts/{id}` (human edit creates a version)
  - `POST /drafts/{id}/approve { reason, mfaCode }`

---

## O05 — Predictive insight with override

- **Artboard:** `P4-Lender-PredictiveInsight-Desktop-Override · 1440`. Section «O05 Predictive».
- **Role:** credit analyst (فهد العتيبي, «محلل ائتمان», initials «ف ع»).
- **Route (inferred):** `/cases/[caseRef]/analysis/insights` (crumb «التحليل · رؤى»; CaseHeader tab `valuation` = «التقييم والتحليل»).
- **Shell:**
  - `LenderSidebar active="cases" user-name="فهد العتيبي" user-role="محلل ائتمان" initials="ف ع"`
  - `LenderTopbar crumb1="RH-2026-004172" crumb2="التحليل · رؤى"`
  - `CaseHeader tab="valuation"`, with **only** that prop. It therefore falls back to the component defaults:
    - caseRef RH-2026-004172
    - title «عبدالله م. — فيلا سكنية، حي النرجس، الرياض»
    - state `proposed` («حل مقترح»)
    - sla-tone warn, «مهلة المرحلة: يومان · 2026-09-25»
    - stage 3, with the default stage names
- **Layout:** `main` is a grid `1fr 400px`. Left: the insight card. Right aside: the analyst-opinion card, then the governance card.

### Content (insight card)
- Title «تقدير الالتزام بالحل v2». «v2» is the **solution** version, not the model version.
- Tag (icon `psychology`): «دعم قرار · غير ملزم».
- Question: «احتمال سداد 12 قسطاً الأولى دون تأخر قسطين متتاليين».
- **Range bar** (role=img, aria «التقدير بين 68 و82 بالمئة، الوسط 76"):
  - Grey track.
  - Band from `inset-inline-start:68%`, width 14%, in info blue `#9DC0DE`.
  - Marker at 76%: 3px wide, black.
  - Axis labels in LTR order «100% · 50% · 0%», so 0% sits at the RTL start (right).
- Summary: «**بين 68% و82%** (الأرجح 76%) · الثقة: **متوسطة**».
- **«لماذا؟ أهم العوامل»** (icon | factor | effect):

| icon (colour) | factor | effect |
|---|---|---|
| arrow_upward green | الاستقطاع 46.5% (أقل من 50%) | يرفع التقدير |
| arrow_upward green | تجاوب المالك خلال يومين في المتوسط | يرفع التقدير |
| arrow_upward green | التزام كامل لمدة 4 سنوات قبل التعثر | يرفع التقدير |
| arrow_downward red | انخفاض الدخل 28% منذ 2026-01 | يخفض التقدير |
| remove grey | دفعة جزئية واحدة 2026-05 | أثر محدود |

- Data and limits block:
  - «**البيانات:** كشف الراتب v2 (متحقق)، سجل الأقساط 24 شهراً، سجل تواصل الحالة. آخر تحديث `2026-09-22`.»
  - «**لا يُستخدم:** الجنس، الجنسية، العمر، المنطقة، الحالة الاجتماعية.»
  - «**القيود:** تدرّب على 1,120 حالة مغلقة لدى المنشأة؛ أقل دقة للحالات فوق 3M.»

### Content (aside)
- **«رأيك كمحلل»**: a radio group. **Override is selected** in the design.

| enum | label |
|---|---|
| agree | أتفق مع التقدير |
| override | أتجاوزه — التقدير أعلى من الواقع ← selected |
| not_used | لا أستخدمه في هذه الحالة |

- «سبب التجاوز *» textarea (required when override is chosen), prefilled «جهة العمل الجديدة في فترة تجربة حتى 2026-12؛ لا يظهر ذلك في البيانات.»
- Primary «حفظ الرأي».
- Note: «التقدير لا يظهر للمالك، ولا يُرفق تلقائياً بطلب الموافقة إلا مع رأيك.»
- **«الحوكمة»:**
  - «النموذج `ADH-v1.2` · راجعته لجنة المخاطر `2026-08-15`»
  - «نسبة التجاوز البشري هذا الشهر: 18%»
  - link «بطاقة النموذج»

### Actions and rules
- «حفظ الرأي» saves the opinion. It is disabled until an option is chosen; with `override`, until the reason is non-empty (inferred from `*`).
- specO5:
  - «هدف المستخدم: الاستفادة من تقدير إحصائي دون أن يحل محل الحكم المهني.»
  - «العرض: نطاق وليس رقماً واحداً، مع درجة الثقة.»
  - «التفسير: أهم العوامل واتجاه أثرها، والبيانات المستخدمة وغير المستخدمة.»
  - «التجاوز: ثلاثة خيارات؛ التجاوز يتطلب سبباً ويُسجل.»
  - «الحدود: لا يظهر للمالك، لا يحجب ولا يفتح انتقالاً، لا يُرفق دون رأي المحلل.»
  - «الحوكمة: إصدار النموذج، تاريخ المراجعة، نسبة التجاوز، بطاقة النموذج.»
- Hard guards:
  1. Never include the prediction in owner (debtor) APIs or DTOs.
  2. Predictions never block or unlock a state transition.
  3. A prediction can be attached to an approval request (L15) only if an opinion exists.
  4. Protected attributes are excluded from the inputs.
  5. Every prediction stores `modelVersion`, `inputs` (with versions and dates), `confidence`, `range`, `limitations`.
  6. The override rate is aggregated monthly for governance.

### Data and API (inferred)
- `Prediction { id, caseId, subjectRef: "solution:v2", modelId: "ADH", modelVersion: "ADH-v1.2", question, low: 0.68, high: 0.82, point: 0.76, confidence: high|medium|low, factors[{text, direction: up|down|neutral, effectLabel}], inputs[{name, version, verified, asOf}], excludedAttributes[], limitations, dataAsOf, createdAt }`.
- `PredictionOpinion { predictionId, userId, value: agree|override|not_used, overrideDirection?: overestimates|underestimates, reason (required if override), createdAt }`.
- `ModelCard { modelVersion, reviewedBy: "لجنة المخاطر", reviewedAt, trainingSetSize: 1120, knownLimitations, overrideRateMonth }`.
- Endpoints:
  - `GET /cases/{ref}/predictions?subject=solution:v2`
  - `POST /predictions/{id}/opinion { value, reason }`
  - `GET /models/{version}/card`
  - `GET /models/{version}/governance`

---

## Integration dependencies
- O01/O02: the internal analytics snapshot (nightly; «بيانات حتى 2026-09-23 06:00»). There is no external integration.
- O04: the AI drafting model (مساعد الصياغة v0.3, an assumption) plus the template store (TPL-AGR-02 v5).
- O05: the predictive model ADH-v1.2. Its inputs are the verified salary statement (L10/L12), the installment history (core banking, 24 months) and the case comms log.
- O03: affects the assignment engine, the valuer directory (licence status, from B9 V05/V06) and the notification scheduler (SMS provider, PA18).

## Reusable components
- **Used:** `LenderSidebar` (active reports/cases/none), `LenderTopbar`, `CaseHeader` (defaults), `SettingsNav` (active sla), C14 AccessibleChart (bar, stacked bar, column trend, table toggle), C01 Button («…» convention), C06 Field (textarea, required).
- **Introduced in B11:**
  - `KpiTile` (label, value, delta with good-direction colour).
  - `StackedShareBar` + legend.
  - `HBarRow` (label, bar, value, count).
  - `WorkWaitBar` (two segments + highlight).
  - `DecisionSupportTag` / `InsightCard` (eyebrow «دعم قرار», title, body, source, confidence, causality caveat, dismiss «غير مفيدة…»).
  - `RoutingRuleRow` (status chip enabled/experimental).
  - `CapacityBar` (red above 90%).
  - `CadenceTimeline`.
  - `DraftDocumentViewer` (marked values + flagged clause).
  - `ValueSourceList`.
  - `ConfidenceLimitsCard`.
  - `PredictionRangeBar`.
  - `FactorList` (direction icons).
  - `AnalystOpinionForm` (agree/override/not_used + reason).
  - `ModelGovernanceCard`.

## Conflicts and ambiguities
1. **O01 denominators.** The outcomes total 147 closed cases, but the regions and cohorts total 1,395. The KPIs don't reconcile with one base:
   - 71% ≈ 104/147 (amicable of closed).
   - «إحالات 2.3%» fits 32/1,395, but not 6/147, which is 4.1%.
   - Each cohort row's percentages sum to about 79–82%, so the remaining outcomes (full payment, cancelled, still open) are unlisted.
   - Define each KPI's population explicitly.
2. **O01 region bars** use a truncated axis starting at 80% (`(v−80)×5`). This exaggerates differences and conflicts with honest-chart norms. Either label the axis start, or use a zero-based scale.
3. **O02 "hot" rule:** the "above normal" threshold is not defined. Only التقييم is flagged.
4. **O02 trend threshold:** the analytic note says the bottleneck is «منذ يوليو», and the trend turns orange from 07 (v ≥ 13). This is consistent, but the ≥ 13 threshold is hard-coded in the design and needs a data-driven definition.
5. **O03 has no edit or approval UI**, although it says «تعديلات تُعتمد وتُسجّل». The approver role and flow are unspecified. It is also unclear whether O03 is its own SettingsNav item or part of «مستوى الخدمة والمهام»; the nav shows `sla` active.
6. **O04 vs O05 solution version.** O04 fills from «الحل v3», but O05 estimates for «الحل v2». Per the working notes, v3 came from the owner counteroffer (day 10, start 2026-12-10).
   - Also: 84 × 15,074.52 = 1,266,259.68, which is not the 1,266,260.00 rescheduled amount (a rounding remainder in the last installment is implied).
7. **O05 offers only one override direction** («التقدير أعلى من الواقع»). An "underestimates" option is missing. Assume a direction field, or two override options.
8. The O05 prediction subject is solution v2 while the case is at `proposed` on the default header, which matches the pre-approval moment. Confirm whether predictions are recomputed per solution version.
9. «مساعد الصياغة v0.3 (افتراض)» is an assumed model; the AI provider is unspecified.
10. O04's «رفض المسودة» has no reason field drawn, and it is unspecified whether one is required.
11. O02's «غير مفيدة…» follow-up screen is not designed.
12. **The AI-output contract is fully rendered only on O05** (range, factors, data used and excluded, limits, opinion, governance).
    - O02's insight shows source, confidence and a causality caveat, but no model version or limitations.
    - O04 shows the model, confidence and limits, but uses reject/edit/approve instead of agree/override/not_used.
    - O03's experimental priority suggestion has no AI metadata UI.
    - The Handoff requires `modelVersion/inputs/confidence/limitations` and a human opinion for every automated output. Store `AiOutputMeta` everywhere, and decide how much of it each screen displays.
13. None of B11 has mobile frames. The only responsive guidance is specO1 (768/390).
