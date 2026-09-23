# B9 — Service Ecosystem, Configuration & Conditional Integrations (منظومة الخدمات والتهيئة والتكاملات المشروطة) · Phase 2

Source: `design-source/05 Phase 2 - B9 Service Ecosystem.dc.html` (condensed: `_condensed/05 Phase 2 - B9 Service Ecosystem.txt`). Cross-refs: `00 Brief & Assumptions` (matrix V05–V07, PA14–PA17, X01–X02; assumptions A-04 manual payments, A-05 in-platform acceptance ≠ binding signature, A-11 provider access lifetime), `09 Handoff/working-notes.md`, B7 (provider portal & admin shells).

Design summary copy: «V05–V07، PA14–PA17، X01–X02: 7 إطارات سطح مكتب + 4 إطارات جوال لأنماط التكامل المشروط وجدول حالاتها. بهذا تكتمل المرحلة 2.»

## 0. Shared context

**Fictional actors:** institution مصرف الأفق (also شركة السنبلة، مصرف الواحة، شركة المدى); inst admin ليلى الغامدي (مسؤولة المنشأة); provider «مكتب تقييم معتمد «ب»» (provider portal user); applicant «مكتب التقييم «هـ»» (`PRV-APP-0044`); debtor on X frames (agreement «اتفاق إعادة الجدولة v3», installment 15,074.52).

**Shells:**
- Institution admin: `LenderSidebar` (props `userName="ليلى الغامدي" userRole="مسؤولة المنشأة" initials="ل غ"`, `active` per screen) + `LenderTopbar` + (settings) `SettingsNav` (220px; items المنشأة / المستخدمون والأدوار / قواعد المستندات / حدود الموافقة / قوالب التواصل / مستوى الخدمة والمهام / مقدمو الخدمة / التقارير; header «إعدادات مصرف الأفق»).
- Platform: `PlatformSidebar` (dark, 264px; header «إدارة المنصة» + «<role> · وصول مقيد»; items التشغيل، المنشآت والطلبات، المستخدمون والصلاحيات، مراقبة الحالات، الإعدادات الافتراضية، مقدمو الخدمة، الشكاوى والتصعيد، التدقيق العام، الخصوصية والاحتفاظ، سير العمل، التسعير والفوترة، التكاملات; footer «لا وصول لبيانات الحالات دون إذن مؤقت»). No topbar in platform frames.
- Provider portal: custom header (logo + provider name + tab nav «التكليفات / الفواتير والأداء / الملف»).
- Provider onboarding: minimal header (logo + autosave status).
- Debtor mobile: `DebtorTop` (back) — X frames.

**Spec asides** quoted verbatim as "Designer spec".

**Status colours:** success #1E6A45/#EAF4EE · warning #8A5300/#FBF2DE · error #B3261E/#FCECEA · info #1D5A8C/#EAF2F9 · neutral #5E5D58/#F2F1ED.

---

## V05 — دليل مقدمي الخدمة · Provider directory (institution)

- **Artboard:** `P2-InstAdmin-ProviderDirectory-Desktop` · 1440 (label notes «V05 + V07 performance» — directory also shows per-institution performance).
- **Role:** مسؤول المنشأة. Matrix path «الإعدادات › مقدمو الخدمة». Route: `/settings/providers`.
- **Shell:** `LenderSidebar active="none"` (inst admin identity) · `LenderTopbar crumb1="الإعدادات" crumb2="مقدمو الخدمة"` · `SettingsNav active="providers"`.
- **Layout:** main = SettingsNav (220px) + content column.

### Content
- H2 «دليل مقدمي الخدمة» + secondary button **«دعوة مقدم خدمة»**.
- Filter chips (single-select; active = filled dark): «الكل 14» (active) · «مقيّمون 6» · «وسطاء 5» · «فحص فني 3» · «ترخيص ينتهي قريباً 2» (amber warning chip).
- Table cols `1.6fr .9fr 1.3fr 1fr 1fr 1fr .9fr`: «مقدم الخدمة» · «النوع» · «الترخيص» · «في الموعد» · «إعادة العمل» · «نشطة» · «الحالة».

| مقدم الخدمة | النوع | الترخيص (icon, colour) | في الموعد | إعادة العمل | نشطة | الحالة |
|---|---|---|---|---|---|---|
| مكتب تقييم معتمد «ب» | مقيّم | verified · ساري حتى 2027-06 (green) | 96% | 4% | 3 | معتمد (green) |
| مكتب التقييم «و» | مقيّم | event_upcoming · ساري حتى 2026-10 (amber) | 88% | 9% | 2 | معتمد |
| دار الوسطاء «أ» | وسيط | verified · ساري حتى 2027-03 | 92% | — | 1 | معتمد |
| شركة الوساطة «د» | وسيط | event_upcoming · ينتهي خلال 20 يوماً (amber) | 81% | — | 0 | تحذير (amber) |
| فحص «ز» الهندسي | فحص فني | verified · ساري حتى 2027-01 | 99% | 2% | 0 | معتمد |
| مكتب التقييم «ح» | مقيّم | event_busy · منتهٍ 2026-08 (red) | 74% | 15% | 0 | موقوف (red) |

«في الموعد» cell = mini progress bar (max 60px, charcoal fill = %) + value; `aria-label="في الموعد 96%"`. «إعادة العمل» «—» for brokers (not applicable).
- Footnote: «الأداء محسوب من التكليفات المنجزة خلال 12 شهراً لدى مصرف الأفق فقط. لا تُشارك تقييمات منشأة مع أخرى.»

### Licence status → directory status
`valid` (verified, green) → معتمد · `expiring_soon` (event_upcoming, amber) → تحذير (or still معتمد, see ambiguity) · `expired` (event_busy, red) → موقوف — **cannot be assigned**.

### Actions
- «دعوة مقدم خدمة» → invite flow (either invite a new provider to onboard V06, or add from platform directory PA16 — not drawn).
- Row click → provider profile (not drawn). Filter chips.

### Designer spec (specDir — V05 + PA16)
- هدف المستخدم: اختيار مقدم خدمة مرخّص وموثوق بسرعة.
- البيانات: الترخيص وحالته، الأداء لدى المنشأة، التكليفات النشطة.
- التحذيرات: ترخيص قريب الانتهاء: تحذير؛ منتهٍ: موقوف ولا يُكلّف.
- PA16: دليل المنصة يضم المقبولين بعد مراجعة الامتثال؛ كل منشأة تختار من تضيف.
- العزل: تقييم الأداء لا يُشارك بين المنشآت.

---

## PA16 — دليل مقدمي الخدمة (منصة) · Platform provider directory

**No dedicated artboard.** Defined only by the V05 spec aside title «V05 الدليل · PA16 دليل المنصة», the specDir PA16 row, and the V06 review note «بعد القبول يظهر في دليل المنصة، وتختار كل منشأة إضافته لدليلها.»
- Role: إدارة المنصة (compliance). Route: `/platform/providers` (PlatformSidebar `active="providers"`).
- Implied content: platform-wide list of accepted providers (reuse V05 table minus institution-specific performance — performance is per institution and not shared), application queue linking to V06 review, licence status, suspension.
- Rule: institutions curate their own directory by adding from the platform directory; institutions do not see registration applications (specOnb).

---

## V06 — تسجيل مقدم الخدمة والملف ومراجعة الترخيص · Provider onboarding, profile & licence review

Two frames (section «V06 Onboarding review»).

### V06a — Onboarding, step 3 · `P2-Provider-Onboarding-Desktop-Step3` · 1100
- Role: مقدم خدمة (applicant). Route: `/provider/onboarding` (matrix) → `/provider/onboarding/[step]` e.g. `/licenses`.
- Header: logo + «تسجيل مقدم خدمة · محفوظ `14:02`» (autosave indicator with last-saved time).
- Layout: grid `220px minmax(0,1fr)`: stepper (ol, aria «خطوات التسجيل») | step content.
- Stepper (6): 1 بيانات المنشأة (done ✓) · 2 الممثل النظامي (done ✓) · 3 التراخيص والتأمين (current, `aria-current="step"`, orange dot) · 4 الفريق · 5 بيانات الفوترة · 6 الاتفاقية والإقرارات.
- Step header: «الخطوة 3 من 6» · H2 «التراخيص والتأمين» · «يراجعها فريق الامتثال يدوياً. لا نتحقق آلياً من سجلات رسمية في هذه المرحلة.»
- Document cards (border colour by state; grid `1fr auto`: info | action button):

| document | meta | status line (icon, colour) | button |
|---|---|---|---|
| ترخيص مزاولة التقييم العقاري | رقم •••4471 · ينتهي 2028-02-01 | check_circle · رُفع · بانتظار المراجعة (green; neutral border) | استبدال |
| وثيقة التأمين ضد الأخطاء المهنية | تنتهي 2026-10-07 | warning · تنتهي خلال 14 يوماً — ارفع التجديد (amber border) | رفع التجديد |
| السجل التجاري | لم يُرفع | upload_file · مطلوب (red border) | رفع |

Licence number masked (•••4471). Fields implied per document: number, expiry date, file.
- Footer: secondary «السابق» · primary «التالي: الفريق» (label names next step).

### V06b — Licence review · `P2-Platform-ProviderLicenseReview-Desktop` · 1440
- Role: الامتثال (platform). Shell `PlatformSidebar active="providers" role="الامتثال"`. Route: `/platform/providers/applications/[applicationId]`.
- Layout: grid `minmax(0,1fr) 400px`: checklist | decision aside.
- Header: «`PRV-APP-0044` · مقيّم عقاري»; H2 «مراجعة تسجيل: مكتب التقييم «هـ»».
- **«قائمة المراجعة»** rows `24px | 1fr | auto` (icon · title+memo · evidence link):

| status | item | memo | link |
|---|---|---|---|
| ✓ | بيانات المنشأة والممثل | مطابقة للمستندات | عرض |
| ✓ | ترخيص المزاولة | ساري · الرقم مطابق للشهادة المرفوعة | الشهادة |
| ⚠ | التأمين المهني | ينتهي خلال 14 يوماً | الوثيقة |
| ✓ | إقرار الاستقلالية وتعارض المصالح | موقّع | الإقرار |
| ✓ | أعضاء الفريق (3) | كل عضو مرخّص فردياً | الفريق |
| ○ | اتفاقية حماية البيانات | بانتظار توقيع مقدم الخدمة | — |

- Aside **«القرار»**: radio cards «قبول» · «طلب استكمال» (selected) · «رفض»; field **«الرسالة لمقدم الطلب \*»** (required textarea; sample «وثيقة التأمين المهني تنتهي خلال 14 يوماً؛ يرجى رفع التجديد.»); note «بعد القبول يظهر في دليل المنصة، وتختار كل منشأة إضافته لدليلها.»; primary **«إرسال القرار»**.
- Guards (implied): message required for every decision; «قبول» should be blocked while required items are incomplete (e.g., data-protection agreement unsigned, expiring insurance) — design picks «طلب استكمال».

### Registration states (specOnb)
مسودة · مقدَّم · يحتاج استكمال · مقبول · مرفوض · موقوف لانتهاء الترخيص.

### Designer spec (specOnb)
- التسجيل: 6 خطوات بحفظ تلقائي. المستندات تُفحص وتُراجع يدوياً.
- المراجعة: قائمة بنود مع رابط لكل دليل؛ قرار قبول / استكمال / رفض برسالة.
- الصلاحية: الامتثال في المنصة؛ المنشأة لا ترى طلبات التسجيل.
- الحالات: مسودة، مقدَّم، يحتاج استكمال، مقبول، مرفوض، موقوف لانتهاء الترخيص.
- التجاوب: التسجيل ممكن على الجوال؛ المراجعة سطح مكتب.

---

## V07 — التسليم والفاتورة والأداء · Delivery, invoice & performance (provider portal)

- **Artboard:** `P2-Provider-InvoicesPerformance-Desktop` · 1440.
- **Roles:** مقدم خدمة (creates invoices); المالية at institution (reviews/approves — not drawn). Matrix path «التكليف › الفاتورة». Route: `/provider/invoices` (portal nav: `/provider/assignments` التكليفات · `/provider/invoices` الفواتير والأداء (current) · `/provider/profile` الملف).
- **Header:** logo · «مكتب تقييم معتمد «ب»» · nav.
- **Layout:** grid `minmax(0,1fr) 380px`: invoices | performance aside.

### Invoices
- H2 «الفواتير» + primary **«إنشاء فاتورة من تكليف مُسلَّم»**.
- Table cols `1.2fr 1.4fr 1fr 1fr 1.2fr`: «الفاتورة» · «التكليف» · «المبلغ» · «التاريخ» · «الحالة».

| الفاتورة | التكليف | المبلغ | التاريخ | الحالة (colour) |
|---|---|---|---|---|
| INV-B-2026-114 | ASG-2026-0842 · مصرف الأفق | 3,500.00 | 2026-09-12 | مدفوعة · مرجع مسجل (green) |
| INV-B-2026-118 | ASG-2026-0851 · مصرف الأفق | 3,500.00 | 2026-09-15 | معتمدة للسداد (info blue) |
| INV-B-2026-121 | ASG-2026-0859 · شركة السنبلة | 4,200.00 | 2026-09-19 | قيد المراجعة (amber) |
| INV-B-2026-109 | ASG-2026-0830 · مصرف الأفق | 3,500.00 | 2026-08-30 | مرفوضة: مبلغ مختلف عن الاتفاقية (red) |

- Note: «الفاتورة تُرسل للمنشأة المكلِّفة؛ السداد خارج المنصة ويُسجل مرجعه.»

### Performance aside — «أداؤك · 12 شهراً»
Each metric: label + value + bar + note (`aria-label="<k>: <v>"`):
- التسليم في الموعد — 96% — bar 96% — «46 من 48 تكليفاً»
- التقارير المقبولة من أول مرة — 92% — bar 92% — «4 إعادات»
- متوسط الرد على الاستفسار — 3 ساعات — bar 80% — «الهدف ≤ 24 ساعة»
- شكاوى مرتبطة — 0 — bar 0% — «—»
Info: «ترى المنشآت أداءك لديها فقط. يمكنك الاعتراض على أي تقييم خلال 14 يوماً.»

### Invoice states (specInv)
مسودة → قيد المراجعة → معتمدة (للسداد) → مدفوعة (مرجع) | مرفوضة بسبب.

### Rules
- Invoice only from an assignment that is delivered **and accepted**; amount comes from the framework agreement (institution-specific, e.g. 3,500 vs 4,200) — mismatch → rejection reason.
- Payment outside platform; only reference recorded.
- Performance visible to each institution only for its own assignments; provider may dispute any rating within 14 days.

### Designer spec (specInv)
- الفاتورة: تُنشأ من تكليف مُسلَّم ومقبول فقط؛ المبلغ من الاتفاقية الإطارية.
- الحالات: مسودة، قيد المراجعة، معتمدة، مدفوعة (مرجع)، مرفوضة بسبب.
- الأداء: مؤشرات مفهومة مع العدد الفعلي، وحق الاعتراض.
- السداد: خارج المنصة؛ تُسجل المراجع فقط.

Delivery (upload of deliverable) is not drawn in B9 — see B7 provider assignment screens.

---

## PA14 — مصمم سير العمل وقواعد الموافقة · Workflow designer & approval rules

- **Artboard:** `P2-Platform-WorkflowDesigner-Desktop` · 1440.
- **Roles:** إدارة المنصة، مسؤول المنشأة (matrix); frame uses `PlatformSidebar active="workflow"` (default role «مسؤول عمليات»). Route: `/platform/workflows/[institutionId]` (draft editing: `/versions/[v]`).
- **Layout:** grid `minmax(0,1fr) 380px`; header row full width; stage list | rule editor aside.

### Header
H2 «مصمم سير العمل · مصرف الأفق»; meta «مسودة `v6` · النافذ `v5` منذ `2026-07-01`». Buttons: secondary (icon `science`) **«محاكاة على 50 حالة سابقة»**, primary **«إرسال للاعتماد»**.

### Stage list (vertical `ol`, aria «مراحل سير العمل»; not a free-form canvas)
Each stage: icon tile · title · SLA · optional badges (lock «قاعدة منصة ثابتة»; «معدّل في v6») · rule chips.

| icon | stage | SLA | locked | changed | rules |
|---|---|---|---|---|---|
| edit_note | الإنشاء والاستلام | 5 أيام | – | – | فحص التكرار إلزامي · الاستيراد يبدأ مسودة |
| fact_check | التحقق | 5 أيام | – | – | صك + هوية + عقد · تصعيد لمدير الفريق يوم 4 |
| query_stats | التقييم | 10 أيام | – | – | مقيّم من الدليل · صلاحية ≤ 90 يوماً |
| approval (selected, tinted) | الموافقة الداخلية | 3 أيام | 🔒 | معدّل في v6 | المُعِدّ ≠ المعتمد · حسب جدول الحدود v4 · جديد: التنازل > 3% ← مراجعة الامتثال |
| hourglass_empty | بانتظار العميل | 10 أيام | 🔒 | – | تتوقف المهلة عند شكوى · الرفض ← حل مقترح |
| outbound | الإحالة القضائية | — | 🔒 | – | القانونية + معتمد · إشعار مسبق ومهلة اعتراض |
| calculate | التسوية والإغلاق | 5 أيام | 🔒 | – | مطابقة صفرية الفرق · المالية + معتمد |

### Aside — rule editor
- «القاعدة المحددة: الموافقة الداخلية»
- Condition builder: «إذا» [التنازل] [>] [3%] → «فأضف» [مراجعة الامتثال قبل المعتمد] (field / operator / value → action select).
- Simulation result: «المحاكاة: كانت ستضيف مراجعة لـ 7 من 50 حالة، ومتوسط تأخير +1.2 يوم.»
- Lock note: «لا يمكن إزالة فصل المهام، أو الموافقتين للإحالة والإغلاق، أو حق المالك في الاعتراض. التعديلات تنطبق على الحالات الجديدة فقط.»

### Rules / guards
- Platform rules locked and labelled; cannot remove: segregation of duties (preparer ≠ approver), dual approval for referral and closure, owner's right to object. Institution may add rules and change SLAs.
- Every edit creates a draft version; publish requires approval («إرسال للاعتماد»). Versions: draft / pending approval / active (effective date) / superseded.
- New version applies to **new cases only**; existing cases stay pinned to their version.
- Simulation against past cases before publishing (count affected + avg delay).

### Designer spec (specWf)
- هدف المستخدم: تهيئة المراحل والمهل والموافقات دون كسر الضوابط الأساسية.
- التصميم: قائمة مراحل رأسية (ليست رسماً حراً) لسهولة الوصول ولوحة المفاتيح.
- الضوابط: قواعد المنصة مقفلة وموسومة. التعديل يُنشئ إصداراً ويُعتمد.
- المحاكاة: أثر القاعدة على حالات سابقة قبل النشر.
- التطبيق: على الحالات الجديدة فقط؛ الحالات القائمة تبقى على إصدارها.

---

## PA15 — التقارير المتقدمة · Advanced reporting

- **Artboard:** `P2-InstAdmin-AdvancedReports-Desktop` · 1440 (PA15).
- **Role:** مسؤول المنشأة. Matrix «التقارير › متقدم». Shell `LenderSidebar active="reports"` (ليلى الغامدي) · `LenderTopbar crumb1="التقارير" crumb2="تقرير مخصص"`. Route: `/reports/custom`.
- **Layout:** grid `300px minmax(0,1fr)`: builder panel | chart + KPIs.

### Builder «بناء التقرير»
- «المقياس» select — value «متوسط أيام الحل»
- «التقسيم» select — value «نوع الحل × الربع»
- «الفترة» — «2025-10 → 2026-09» (month range)
- Privacy note: «بيانات مجمعة فقط. الخلايا الأقل من 10 حالات تُخفى لحماية الخصوصية.»
- Secondary **«جدولة إرسال شهري»**.

### Chart (figure)
Caption «متوسط أيام الوصول لحل حسب النوع» + link **«عرض كجدول»** (table alternative). Rows (role=list), each 4 quarter bars (scale: width = v/90; latest quarter orange #F4633A, others charcoal); `aria-label="<type>: v1، v2، v3، v4 يوماً"`:

| نوع الحل | Q4-2025 | Q1-2026 | Q2-2026 | Q3-2026 |
|---|---|---|---|---|
| إعادة جدولة | 62 | 58 | 51 | 47 |
| فترة سماح | 41 | 39 | 36 | 34 |
| سداد مخفض | 78 | 72 | 70 | 66 |
| بيع طوعي | — | — | 88 | 81 |

Legend: «أعمدة كل صف: الربع 4 · 2025 ← الربع 3 · 2026 (من اليمين)» · ««—» = أقل من 10 حالات».

### KPI tiles (3)
- حالات أُغلقت بحل ودي — 71% — آخر 12 شهراً
- متوسط أيام الحل — 49 — −15 عن العام السابق
- شكاوى لكل 100 حالة — 1.8 — ضمن الهدف

### Rules
- Aggregates only; suppress cells with n < 10 (render «—»), server-side.
- Scheduled sends only to authorised users.

### Designer spec (specRep — PA15 + PA17)
- التقارير: منشئ بسيط: مقياس × تقسيم × فترة؛ رسم مع بديل جدولي.
- الخصوصية: تجميع فقط وإخفاء الخلايا الصغيرة (< 10).
- الجدولة: إرسال دوري للمستخدمين المخوّلين فقط.
- الفوترة: خطط افتراضية؛ الاستخدام بعدد الحالات النشطة؛ حالة كل فاتورة.
- الصلاحية: مالية المنصة للفوترة؛ مسؤول المنشأة للتقارير.

---

## PA17 — التسعير والفوترة · Pricing & billing

- **Artboard:** `P2-Platform-Billing-Desktop` · 1440 (PA17).
- **Role:** مالية المنصة (`PlatformSidebar active="billing" role="المالية — المنصة"`). Route: `/platform/billing`.
- **Layout:** single column: header, 3 plan cards (grid 3), billing table.

### Content
- H2 «التسعير والفوترة» + badge «نموذج التسعير افتراض — يتطلب تأكيد المنتج».
- Plan cards (name · price «ر.س / شهر» · limit · institutions on plan):
  - الأساسية — 18,000 — حتى 500 حالة نشطة — 3 منشآت
  - المؤسسية — 42,000 — حتى 3,000 حالة نشطة — 6 منشآت
  - المخصصة — حسب الاتفاق — أكثر من 3,000 حالة — 2 منشأة
- Table cols `1.6fr 1fr 1fr 1fr 1.2fr 1fr`: «المنشأة» · «الخطة» · «حالات نشطة» · «الفاتورة» · «المبلغ» · «الحالة»:

| المنشأة | الخطة | حالات نشطة | الفاتورة | المبلغ | الحالة |
|---|---|---|---|---|---|
| مصرف الأفق | المؤسسية | 1,248 | BIL-2026-09-003 | 42,000.00 | صادرة (info) |
| شركة السنبلة | الأساسية | 412 | BIL-2026-09-006 | 18,000.00 | مدفوعة (green) |
| مصرف الواحة | المؤسسية | 2,104 | BIL-2026-09-004 | 42,000.00 | متأخرة 5 أيام (red) |
| شركة المدى | — | — | — | — | تجربة (neutral) |

### Billing rules
- Monthly flat fee per plan tier; tier by active-case count (usage metric = active cases). Custom = contract.
- Invoice statuses: صادرة · مدفوعة · متأخرة N أيام · تجربة (trial, no invoice).
- Invoice ref `BIL-YYYY-MM-NNN`. Pricing is an assumption pending product confirmation — make plans data-driven.
- No actions drawn (implied: issue invoice, record payment reference, change plan, overage when active cases exceed tier).

---

## X01 / X02 — التوقيع المرخّص / الدفع المرخّص · Licensed signing / Licensed payment (conditional integrations)

Section heading: «X01 التوقيع المرخّص · X02 الدفع المرخّص — أنماط تكامل مشروطة». Intro (verbatim):
> تظهر فقط إذا فعّلت المنشأة مزوداً مرخّصاً معتمداً. في غير ذلك يبقى المسار اليدوي (سجل الموافقة، التحويل البنكي). كل حالة تكامل موسومة صراحة: مفعّل، محاكاة، قيد المعالجة، غير متاح، فشل.

- **Roles:** debtor (both); القانونية (X01), المالية (X02) see provider status. Matrix paths «الاتفاق › توقيع», «المدفوعات › دفع». Routes: `/portal/agreement/sign`, `/portal/payments/[installmentId]/pay`.
- **Frame template (390 mobile):** `DebtorTop title=<x.title> back` · status tag pill (icon + text, coloured) · H3 · body · facts list (k/v) · primary button (bottom) · alt link.

| Frame | title | tag (icon, colour) | H3 | body | facts | primary | alt link |
|---|---|---|---|---|---|---|---|
| `P2-Debtor-LicensedSign-Mobile-Enabled · X01` | توقيع الاتفاق | verified · «مفعّل · مزود توقيع مرخّص» (green) | وقّع الاتفاق إلكترونياً | سننقلك إلى مزود التوقيع المرخّص للتحقق من هويتك والتوقيع، ثم تعود هنا تلقائياً. | المستند: اتفاق إعادة الجدولة v3 · المزود: مزود توقيع مرخّص (مكان محجوز) · المدة المتوقعة: دقيقتان | المتابعة إلى التوقيع | أفضّل الموافقة داخل المنصة |
| `P2-Debtor-LicensedSign-Mobile-Unavailable · X01` | توقيع الاتفاق | cloud_off · «غير متاح مؤقتاً» (neutral) | خدمة التوقيع غير متاحة الآن | لم يتغير شيء في عرضك. يمكنك الموافقة داخل المنصة برمز تحقق، أو المحاولة لاحقاً. | صلاحية العرض: حتى 2026-10-11 · آخر محاولة: 2026-10-02 14:05 | الموافقة برمز تحقق | إعادة المحاولة |
| `P2-Debtor-LicensedPay-Mobile-Enabled · X02` | سداد القسط | verified · «مفعّل · بوابة دفع مرخّصة» (green) | ادفع القسط 3 | الدفع عبر بوابة مرخّصة لصالح حساب مصرف الأفق مباشرة. رهون لا تحتفظ بالمبلغ. | المبلغ: 15,074.52 ريال · المستفيد: مصرف الأفق · الاستحقاق: 2027-02-10 | المتابعة إلى الدفع | أفضّل التحويل البنكي |
| `P2-Debtor-LicensedPay-Mobile-Failed · X02` | سداد القسط | error · «فشل · لم يُخصم أي مبلغ» (red) | لم تكتمل عملية الدفع | رفض البنك المُصدر العملية. لم يُخصم أي مبلغ. يمكنك المحاولة ببطاقة أخرى أو بالتحويل البنكي. | مرجع المحاولة: PAY-TRY-88213 · الوقت: 2027-02-08 21:14 | المحاولة مرة أخرى | تعليمات التحويل البنكي |

### Integration state table (verbatim)
Cols: «حالة التكامل» · «ما يراه المالك» · «ما يراه فريق الجهة» · «البديل».

| حالة التكامل | ما يراه المالك | ما يراه فريق الجهة | البديل |
|---|---|---|---|
| مفعّل (green) | زر المسار المرخّص + بديل يدوي ظاهر | الحالة من المزود مع وقت آخر تحديث | المسار اليدوي دائماً |
| محاكاة (blue) | لا يظهر للمالك | وسم «محاكاة» على كل سجل؛ لا أثر مالي أو قانوني | — |
| قيد المعالجة (amber) | «نتحقق من العملية» بلا تكرار الدفع | انتظار رد المزود؛ لا تُعلَّم الدفعة مطابقة | إشعار عند الاكتمال |
| غير متاح (charcoal) | رسالة هادئة + بديل | تنبيه تشغيلي | المسار اليدوي |
| فشل (red) | «لم يُخصم أي مبلغ» عند التأكد فقط | سجل الفشل ورمزه | إعادة المحاولة أو يدوي |

### Rules / guards
- Show licensed path only when the institution has contracted & enabled an approved licensed provider (per institution, per capability: signing / payment). Otherwise manual path only: in-platform consent record + OTP; bank transfer with manually recorded/matched reference (Brief A-04, A-05).
- Manual alternative always visible when enabled.
- Simulated mode never visible to owner; every simulated record tagged «محاكاة»; no financial/legal effect.
- Pending: block duplicate payment; do not mark installment matched until provider confirms.
- Failed: show «لم يُخصم أي مبلغ» only when confirmed; store failure code; allow retry or manual.
- Unavailable: offer remains unchanged; show offer validity and last attempt; ops alert.
- Payment goes directly to lender account; Rahoon never holds funds. Provider name is a placeholder («مزود توقيع مرخّص (مكان محجوز)»).
- Redirect-and-return flow for signing («ثم تعود هنا تلقائياً»).

---

## Implied data entities

- **Provider**: id, legalName, type (valuer/broker/inspection), crNumber, status (مسودة/مقدَّم/يحتاج استكمال/مقبول/مرفوض/موقوف), representative, team members (each licensed individually), billing details, agreementsSigned (independence/COI declaration, data-protection agreement).
- **ProviderApplication**: id (`PRV-APP-0044`), providerId, currentStep (1–6), lastSavedAt, submittedAt, reviewChecklist[{item, status, evidenceDocId}], decision (accept/request_info/reject), message (required), decidedBy/at.
- **ProviderDocument/Licence**: providerId, kind (practice_licence/professional_insurance/commercial_register/...), number (masked in UI), expiryDate, fileId, reviewStatus (uploaded_pending/approved/needs_renewal/missing), derived licenceState (valid/expiring_soon/expired).
- **InstitutionProvider** (institution directory membership): institutionId, providerId, addedBy/at, active; perf metrics computed per institution (onTimeRate, reworkRate, activeAssignments).
- **Assignment** (`ASG-2026-0842`): institutionId, providerId, caseId, status (… delivered, accepted), deliveredAt, acceptedAt.
- **FrameworkAgreement**: institutionId, providerId, serviceType, fee.
- **ProviderInvoice** (`INV-B-2026-114`): assignmentId, amount, date, status (draft/under_review/approved/paid/rejected), rejectionReason, paymentReference.
- **PerformanceRating / Dispute**: assignmentId, metrics, disputeDeadline (+14 days), disputeText, status.
- **WorkflowDefinition / WorkflowVersion**: institutionId, version (v5 active since 2026-07-01, v6 draft), status (draft/pending_approval/active/superseded), stages[{key, icon, title, slaDays, locked, rules[]}], approvedBy, effectiveFrom; Case.workflowVersionId pinned at creation.
- **WorkflowRule**: stageKey, text/label, isPlatformLocked, condition {field, operator, value}, action, changedInVersion.
- **SimulationRun**: versionId, sampleSize 50, affectedCount 7, avgDelayDays 1.2.
- **ReportDefinition**: metric, breakdown, period, schedule (monthly), recipients (authorised); result cells with suppression flag.
- **Plan**: name, monthlyPrice (nullable «حسب الاتفاق»), activeCaseLimit; **InstitutionSubscription**: plan, trial flag, activeCaseCount; **PlatformInvoice** (`BIL-2026-09-003`): amount, status (issued/paid/overdue/trial), dueDate.
- **IntegrationConfig**: institutionId, capability (signing/payment), providerId, mode (enabled/simulated/disabled), health (available/unavailable), lastUpdateAt.
- **SignatureSession**: agreementVersionId, provider, status (pending/signed/failed/unavailable), redirectUrl, completedAt; **PaymentAttempt** (`PAY-TRY-88213`): installmentId, amount, beneficiary, status (pending/succeeded/failed), failureCode, debited (confirmed bool), at, simulated flag.

## API operations (suggested)

- Directory: `GET /institution/providers?type=&licence=expiring` · `POST /institution/providers/invitations` · `POST /institution/providers/{id}` (add from platform directory) · `GET /platform/providers` · `PATCH /platform/providers/{id}/status` (suspend)
- Onboarding: `GET/PUT /provider/onboarding/steps/{step}` (autosave) · `POST /provider/onboarding/documents` · `POST /provider/onboarding/submit` · `GET /platform/provider-applications` · `GET /platform/provider-applications/{id}` · `POST /platform/provider-applications/{id}/decision` {decision, message}
- Licence expiry job: flag expiring (≤ 30 days?) and auto-suspend on expiry.
- Invoices: `GET /provider/assignments?invoiceable=true` · `POST /provider/invoices` {assignmentId} (amount from agreement) · `GET /provider/invoices` · `POST /institution/provider-invoices/{id}/review` (approve/reject reason) · `POST .../payment-reference` · `GET /provider/performance` · `POST /provider/ratings/{id}/disputes`
- Workflow: `GET /platform/workflows/{institutionId}` · `POST .../versions` (draft) · `PATCH .../versions/{v}/stages/{key}` · `POST .../versions/{v}/rules` · `POST .../versions/{v}/simulate` {sampleSize} · `POST .../versions/{v}/submit` · `POST .../versions/{v}/approve`
- Reports: `POST /reports/query` {metric, breakdown, period} (server-side suppression) · `POST /reports/schedules`
- Billing: `GET /platform/billing/plans` · `GET /platform/billing/invoices` · `POST /platform/billing/invoices/{id}/payments`
- Integrations: `GET /portal/capabilities` (which licensed paths are enabled) · `POST /portal/agreements/{id}/signature-sessions` · callback `POST /integrations/signing/webhook` · `POST /portal/installments/{id}/payment-attempts` · `GET /portal/payment-attempts/{id}` · `POST /integrations/payments/webhook` · `GET /platform/integrations` (status/health, PA18 in B10).

## Integration dependencies & design labelling

- Licence verification: **manual** — «يراجعها فريق الامتثال يدوياً. لا نتحقق آلياً من سجلات رسمية في هذه المرحلة.»
- Provider invoice payment & platform billing payment: **outside platform**, reference recorded.
- Licensed e-signature and payment gateway: **conditional** providers, labelled by explicit state tags (مفعّل / محاكاة / قيد المعالجة / غير متاح / فشل); provider names placeholders («مكان محجوز»). Manual fallback always.
- Report scheduling needs email/notification service.

## Components

Used: LenderSidebar, LenderTopbar, SettingsNav, PlatformSidebar, DebtorTop.
Introduced/reusable: FilterChips (with warning variant); ProviderTable (licence status icon/colour, inline mini progress bar, status badge); WizardStepper (done/current/todo, autosave header); DocumentUploadCard (status-coloured border + action); ReviewChecklist (status · item · evidence link); DecisionRadioCards + required message; InvoiceTable with status badges; MetricBar list (label/value/bar/note); WorkflowStageList (vertical, lock badge, "changed in vN" badge, rule chips); RuleConditionBuilder (field/op/value → action) + SimulationResult; ReportBuilderPanel (selects + period + privacy note); GroupedBarChart with «عرض كجدول» table toggle and suppressed-cell «—»; KpiTile; PlanCard; IntegrationStatusTag (5 states); IntegrationActionScreen (mobile template: tag, heading, body, facts, primary, alt link).

## Conflicts / ambiguities

1. **PA16 has no artboard** — only rules in asides; UI must be derived (reuse V05 table, without cross-institution performance).
2. **V07 "delivery"** part not drawn in B9 (only invoices + performance) — rely on B7 assignment/delivery screens.
3. **Expiring-licence threshold**: «مكتب التقييم «و»» (expires 2026-10, ~1 month) is amber but status «معتمد», while «شركة الوساطة «د»» (20 days) is «تحذير». The filter chip «ترخيص ينتهي قريباً 2» counts both. Define threshold(s) (e.g., amber icon ≤ 60 days, status تحذير ≤ 30 days).
4. **V05 vs V07 performance**: V05 shows «ب» rework 4% at مصرف الأفق; V07 shows 92% first-time acceptance (4 reworks of 48 ≈ 8%) — V07 is provider's aggregate across institutions, yet V07 note says institutions only see their own. Confirm metric definitions and scope.
5. **Onboarding «التالي: الفريق»** appears enabled though السجل التجاري is missing — assume steps can be saved incomplete and validation happens on final submit.
6. **PA14 role/scope**: matrix lists إدارة المنصة + مسؤول المنشأة, frame uses platform shell editing «مصرف الأفق»'s workflow; B11 O03 «العمليات القابلة للتهيئة» (inst admin) may overlap. Who approves the new version (platform vs institution) is unspecified. Locked-stage badge is per stage, but institution still adds a rule to a locked stage → implement lock per rule (platform rules non-removable), not per stage.
7. **Rule references**: «حسب جدول الحدود v4» — approval-limits table is versioned separately (SettingsNav «حدود الموافقة»).
8. **PA15 chart scale** fixed at 90 days max in design; use dynamic max. Chart rendered as horizontal bar groups, quarter order right→left.
9. **PA17** has no actions, plan counts (3+6+2 = 11 institutions) don't match 4 table rows (sample only); overage rules and proration undefined; pricing explicitly an assumption.
10. **X01 context**: sample agreement «اتفاق إعادة الجدولة v3» — applies to settlement agreements (B4) and possibly sale consent (B8) — confirm which documents route to licensed signing.
11. **X states drawn**: only enabled + unavailable (X01) and enabled + failed (X02); pending and simulated owner-facing states are defined only in the table.
12. Integration admin/config UI (enable provider, simulation mode) is not in B9 — see PA18 in B10.
