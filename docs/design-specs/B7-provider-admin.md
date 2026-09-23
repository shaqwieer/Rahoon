# B7 — Provider portal, Institution admin, Platform admin (Phase 1)

Source: `design-source/04 Phase 1 - B7 Provider & Admin.dc.html` (page title `04 Phase 1 — B7 Provider & admin — رهون`, h1 `المرحلة 1 — الدفعة B7: مقدم الخدمة وإدارة المنشأة وإدارة المنصة`). Cross-refs: `00 Brief & Assumptions` (matrix rows V01–PA13, assumptions A-08..A-12), `09 Handoff` (impl rules), `04 Phase 1 - B2` (help/support copy), child DCs `PlatformSidebar`, `SettingsNav`, `LenderSidebar`, `LenderTopbar`.
All pages: `dir="rtl" lang="ar"`, Western digits, IDs/dates/amounts/emails wrapped in `<bdi dir="ltr">`. Tones below use working-notes tokens: success `#1E6A45/#EAF4EE`, warning `#8A5300/#FBF2DE`, error `#B3261E/#FCECEA`, info `#1D5A8C/#EAF2F9`, neutral `#5E5D58`, charcoal `#22262A`, ink `#151513`, primary btn rust `#AA4528`, selected tint `#FDF0EB`, accent `#F4633A` (bar only).

Canvas section headings: `مقدم الخدمة / المقيّم — V01–V04` · `إدارة المنشأة — A01–A07` · `إدارة المنصة والامتثال والتدقيق — PA01–PA13` (page nav links: `00 الموجز`, B6, **B7**, B8).
Canvas footer: «صُمم في B7» — `V01–V04 (3 إطارات) · A01–A07 (5 إطارات) · PA01–PA13 (8 إطارات مجمّعة). مكونات جديدة: PlatformSidebar، SettingsNav. بهذا تكتمل شاشات المرحلة 1.` · «التالي: B8» — `المرحلة 2 — البيع الطوعي للجهة والمالك.`

---

## 0. Coverage map (artboard → screen)

| Screen | Drawn in artboard (label · width) | Coverage |
|---|---|---|
| V01 Inbox | `P1-Provider-AssignmentDetail-Desktop-Default · 1440` (left rail 380px) + `P1-Provider-Inbox-Mobile · 390` | drawn (list only) |
| V02 Detail | `P1-Provider-AssignmentDetail-Desktop-Default · 1440` | drawn |
| V03 Question/RFI | inline «الاستفسارات» thread inside V02 | no own frame |
| V04 Submission | `P1-Provider-Submission-Desktop-Returned · 1100` | only **Returned/resubmission** state drawn; first submission derived |
| A01 Org settings | card «A01 إعدادات المنشأة» inside `P1-InstAdmin-OrgSLA-Reports-Desktop · 1440` | read-only card; onboarding flow not drawn |
| A02 Users/roles | `P1-InstAdmin-UsersRoles-Desktop · 1440` | drawn |
| A03 Doc rules | `P1-InstAdmin-DocRules-Desktop · 1440` | drawn (table) |
| A04 Approval limits | `P1-InstAdmin-ApprovalLimits-Desktop · 1440` | drawn |
| A05 Templates | `P1-InstAdmin-Templates-Desktop-Editor · 1440` | editor drawn; list not drawn |
| A06 SLA & tasks | panel in `P1-InstAdmin-OrgSLA-Reports-Desktop` | drawn (stage deadlines) |
| A07 Reports | card in `P1-InstAdmin-OrgSLA-Reports-Desktop` | drawn (list) |
| PA01 Ops dashboard | `P1-Platform-OpsDashboard-Desktop · 1440 (PA01 + PA13)` | drawn |
| PA13 Service monitoring | panel «PA13 صحة الخدمة» inside PA01 | drawn as panel |
| PA02 Applications | `P1-Platform-Institutions-Desktop · 1440 (PA02 + PA03)` tab 1 | drawn (list); review detail not drawn |
| PA03 Institution list | same artboard, tab «المنشآت النشطة 11» | **not drawn: count only** |
| PA04 Platform users | `P1-Platform-PermissionCatalog-Desktop · 1440 (PA04 + PA05)` tab «مستخدمو المنصة 14» | **not drawn: count only** |
| PA05 Permission catalog | same artboard, selected tab | drawn (7 of 62 rows) |
| PA06 Case monitoring + temp access | `P1-Platform-CaseMonitoring-Desktop-TempAccess · 1440 (PA06)` | drawn |
| PA07 Approval thresholds | `P1-Platform-Defaults-Desktop · 1440 (PA07 + PA08 + PA09)` selected tab | drawn |
| PA08 Document types | same, tab «أنواع المستندات 38» | **not drawn: count only** |
| PA09 Base templates | same, tab «القوالب الأساسية 24» | **not drawn: count only** |
| PA10 Complaints | `P1-Platform-Complaints-Desktop · 1440 (PA10)` | drawn |
| PA11 Audit log | `P1-Platform-AuditLog-Desktop · 1440 (PA11)` | drawn |
| PA12 Retention & privacy | `P1-Platform-RetentionPrivacy-Desktop · 1440 (PA12)` | drawn |

Only one mobile frame exists (V01 390). All admin/platform screens are desktop-only in design; specInst: `التجاوب` = `سطح المكتب أولاً؛ على الجوال قراءة فقط.`

Brief matrix nav locations (column «موقع التنقل»): V01 `التكليفات` · V02 `التكليفات › تكليف` · V03 `التكليف › استفسار` · V04 `التكليف › تسليم` · A01 `الإعدادات › المنشأة` · A02 `الإعدادات › المستخدمون` · A03 `الإعدادات › المستندات` · A04 `الإعدادات › الموافقات` · A05 `الإعدادات › القوالب` · A06 `الإعدادات › مستوى الخدمة` · A07 `التقارير` · PA01 `المنصة › التشغيل` · PA02 `المنصة › الطلبات` · PA03 `المنصة › المنشآت` · PA04 `المنصة › المستخدمون` · PA05 `المنصة › الصلاحيات` · PA06 `المنصة › الحالات` · PA07 `المنصة › الحدود` · PA08 `المنصة › المستندات` · PA09 `المنصة › القوالب` · PA10 `المنصة › الشكاوى` · PA11 `المنصة › التدقيق` · PA12 `المنصة › الخصوصية` · PA13 `المنصة › الخدمة`.
Matrix roles: V* `مقدم خدمة / مقيّم` (Provider); A* `مسؤول المنشأة` (InstAdmin); PA01–05,07–09,13 `إدارة المنصة`; PA06 `الامتثال، الدعم`; PA10 `الامتثال`; PA11 `المدقق`; PA12 `الامتثال`.

---

## 1. Shells & navigation

### 1.1 Provider shell (NEW, inline — not a child DC)
Desktop header row: logo `rahoon-horizontal-full.svg` (172px) · org+role label `مكتب تقييم معتمد «ب» · مقيّم` · nav links `التكليفات` (aria-current=page) / `المساعدة` · user name `عمر العنزي`. No sidebar.
Mobile (390) header: symbol logo `rahoon-symbol-full.svg` (28px) + title `التكليفات` + sub `مكتب تقييم معتمد «ب»`.
Blueprint nav for provider: `التكليفات`, `تفاصيل التكليف`, `الاستفسارات`, `التسليم`, `(المرحلة 2: الملف، الترخيص، الفواتير، الأداء)`; scope `تكليفه فقط ولمدة التكليف`.

### 1.2 Institution admin shell = LenderSidebar + LenderTopbar + SettingsNav
- `LenderSidebar` props on every A-artboard: `active="none"`, `user-name="ليلى الغامدي"`, `user-role="مسؤولة المنشأة"`, `initials="ل غ"`. (Sidebar items: المحفظة, الحالات, مهامي `7`, الموافقات, الشكاوى `2`, التقارير; org switcher `مصرف الأفق`; footer `المساعدة والدعم`, `جلسة آمنة · MFA`.)
- `LenderTopbar`: `crumb1="الإعدادات"`, crumb2 per screen: A02 `المستخدمون والأدوار` · A04 `حدود الموافقة` · A03 `قواعد المستندات` · A05 `قوالب التواصل` · A06/A01/A07 artboard `مستوى الخدمة`. Topbar also has search `ابحث بالمرجع أو رقم العقد أو المهمة` (Ctrl K), badge `محتوى خاص`, `EN`, notifications `4`.
- `SettingsNav` (NEW, 220px, `aria-label="إعدادات المنشأة"`): heading `إعدادات مصرف الأفق`; items key→label: `org` المنشأة · `users` المستخدمون والأدوار · `docs` قواعد المستندات · `limits` حدود الموافقة · `templates` قوالب التواصل · `sla` مستوى الخدمة والمهام · `providers` مقدمو الخدمة · `reports` التقارير. Active style: bold, text `#8E3920`, bg `#FDF0EB`. Used: A02 `users`, A04 `limits`, A03 `docs`, A05 `templates`, OrgSLA-Reports `sla`.
- Main layout: `padding:28px 40px 40px; display:flex; gap:28px` → [SettingsNav 220px][content flex:1].

### 1.3 Platform shell = PlatformSidebar (NEW, 264px, dark)
`aria-label="إدارة المنصة"`; logo `rahoon-horizontal-dark.svg`; header `إدارة المنصة` + `{role} · وصول مقيد`; footer notice (icon `shield`) `لا وصول لبيانات الحالات دون إذن مؤقت`.
Items (key · label · icon): `ops` التشغيل monitoring · `inst` المنشآت والطلبات domain · `users` المستخدمون والصلاحيات manage_accounts · `cases` مراقبة الحالات visibility_lock · `defaults` الإعدادات الافتراضية tune · `providers` مقدمو الخدمة assignment_ind · `complaints` الشكاوى والتصعيد support_agent · `audit` التدقيق العام history · `privacy` الخصوصية والاحتفاظ policy · `workflow` سير العمل account_tree · `billing` التسعير والفوترة receipt_long · `integrations` التكاملات hub. Active: bold, bg `#22262A`, inset start bar `#F4633A`.
Props per artboard: PA01 `active=ops` (role default `مسؤول عمليات`) · PA02/03 `inst` · PA06 `cases`, `role="دعم تقني"` · PA04/05 `users` · PA07–09 `defaults` · PA10 `complaints`, `role="الامتثال"` · PA11 `audit`, `role="مدقق"` · PA12 `privacy`, `role="الامتثال"`. No topbar on platform screens. Main: `padding:32px 40px`, flex column.
Platform roles seen: `مسؤول عمليات`, `دعم تقني`, `الامتثال`, `مدقق` (also «مدقق المنصة» in copy).

---

## 2. Provider portal (V01–V04)

Spec aside «V01–V04 بوابة مقدم الخدمة» (verbatim, specProv):
- هدف المستخدم — إنجاز التكليف في وقته بأقل احتكاك، دون الاطلاع على ما لا يلزم.
- العزل — التكليف فقط: نطاق العمل، الموعد، مستندات مختارة. لا مديونية ولا أطراف أخرى.
- العمر — الوصول ينتهي بعد 7 أيام من التسليم آلياً ويُعرض ذلك صراحة.
- التسليم — قائمة تحقق قبل الإرسال؛ الإعادة تعرض الملاحظات بنقاط محددة ومهلة جديدة.
- الاستفسار — سلسلة داخل التكليف؛ يجيب فريق الحالة.
- التجاوب — 390: قائمة التكليفات، التفاصيل صفحة كاملة؛ الرفع من الجوال مدعوم.

### V01 — صندوق التكليفات / Assignment inbox
- Role: provider user (valuer). Route: `/provider/assignments` (desktop: list rail + selected detail at `/provider/assignments/[assignmentId]`).
- Layout desktop: `main` grid `380px | minmax(0,1fr)`; rail = h2 `التكليفات` + filter chips `نشطة 3` (selected: ink bg, white) / `مُسلَّمة` (outline); list of assignment rows. Mobile 390: header (1.1) + stacked `<article>` cards (white, border `#CBCAC6`, radius 10, padding 14); tap → full-page detail.
- Row/card content: `{id}` (Mono, ltr) + SLA text (tone-coloured, 12px 600) on first line; `{title}` bold; `{meta}`. Selected row: bg `#FDF0EB` + start-edge 3px `#F4633A` bar.
- Sample data:

| id | title | meta | SLA | tone | selected |
|---|---|---|---|---|---|
| ASG-2026-0871 | تقييم فيلا — حي الملقا، الرياض | مصرف الأفق · معاينة 2026-09-24 | 3 أيام | warning | yes |
| ASG-2026-0864 | إعادة تسليم: شقة — حي الروضة، جدة | مصرف الأفق · أُعيد للتعديل | 4 أيام | error | |
| ASG-2026-0880 | تقييم أرض سكنية — الخبر | شركة السنبلة للتمويل · جديد | 10 أيام | success | |

- Note: one provider sees assignments from several lenders (مصرف الأفق, شركة السنبلة للتمويل) — inbox is cross-tenant for the provider org but each assignment isolated.
- States drawn: default. Not drawn (derive): empty active list, delivered tab list, loading, expired access.
- Actions: select row → V02; chip filter active/delivered.

### V02 — تفاصيل التكليف / Assignment detail
- Route `/provider/assignments/[assignmentId]`. Detail area grid `minmax(0,1fr) | 340px` (content | aside).
- Header card: eyebrow `تكليف من مصرف الأفق · ASG-2026-0871`; h3 `تقييم فيلا سكنية — حي الملقا، الرياض`; badges: info `hourglass_top` `قيد التنفيذ`; warning `alarm` `التسليم خلال 3 أيام · 2026-09-26`.
- Card «نطاق العمل» (key/value rows):
  - نوع التقييم — قيمة سوقية · للعقار السكني
  - المنهجية المطلوبة — المقارنة + التكلفة
  - موعد المعاينة — 2026-09-24 10:00 · مؤكد
  - جهة الاتصال للمعاينة — سلطان ح. (المالك) عبر المنصة
  - الأتعاب المتفق عليها — حسب الاتفاقية الإطارية
- Card «الاستفسارات» = V03 (below).
- Aside:
  1. «تسليم التقرير»: `معاينة مؤكدة 2026-09-24 10:00 بحضور المالك.` + primary full-width button `بدء التسليم` → V04.
  2. «مستندات التكليف (3)»: `صك الملكية (مخفي الأرقام)` · `مخطط البناء` · `نموذج التقرير المطلوب` (lender-selected docs only; masked where rule says).
  3. Notice (subtle bg `#F2F1ED`, icon `lock_clock`): `وصولك لهذا التكليف فقط، وينتهي بعد 7 أيام من التسليم. لا ترى المديونية أو بيانات المالك الأخرى.`
- Owner contact is via platform only (masked name `سلطان ح.`); no phone/ID shown.
- States: default (in progress). Derive: new/not-accepted, returned (→ V04 returned), submitted (read-only with expiry countdown), expired (access revoked → S12 access denied).

### V03 — سؤال أو طلب معلومات / Question-RFI (inline thread in V02)
- Card «الاستفسارات»: chat bubbles max-width 80%.
  - Own msg: `هل يتوفر مخطط البناء المعتمد؟ نحتاجه لمطابقة المساحة.` meta `أنت · 2026-09-21 10:30`
  - Lender reply: `أُضيف المخطط إلى مستندات التكليف.` meta `فهد العتيبي · مصرف الأفق · 2026-09-21 13:05`
  - Composer: placeholder `اكتب استفساراً للجهة…` (drawn as div, implement textarea) + secondary button `إرسال`.
- Rules: thread scoped to assignment; answered by case team (`يجيب فريق الحالة`); a reply may attach a doc to assignment documents. Route anchor `/provider/assignments/[id]#questions` (or `/questions`).
- Derive: empty thread, send error, disabled send when empty; read-only after submission-expiry.

### V04 — التسليم وإعادة التسليم / Submission & resubmission (Returned state drawn, 1100)
- Route `/provider/assignments/[assignmentId]/submit`.
- Header: `ASG-2026-0864 · مصرف الأفق · تقييم شقة — حي الروضة، جدة`; h2 `إعادة التسليم`.
- Alert (`role="alert"`, error tone): title (icon `undo`) `أُعيد التقرير v1 للتعديل`; bullets `• صفقات المقارنة أقدم من 12 شهراً (2 من 5). يرجى تحديثها.` / `• ينقص ذكر حالة الإشغال.`; meta `فهد العتيبي · 2026-09-22 · مهلة إعادة التسليم 2026-09-27`.
- Form grid 2 cols:
  - `القيمة السوقية *` — money input ltr, value `742,000.00`, suffix `ر.س`; required, decimal(2), >0.
  - `تاريخ المعاينة *` — date (ltr) `2026-09-15`; required, ≤ today.
  - File row (icon `upload_file`): `تقرير_التقييم_v2.pdf` · status (success `check_circle`) `رُفع · 4.1 م.ب · فُحص` · button `استبدال`. File must pass scan (فُحص) before submit. (Size limit elsewhere in design: 20 م.ب per B2 support ticket — assumption.)
  - «قائمة التحقق قبل التسليم» checkboxes (checked = ink filled box w/ `check`; unchecked = white box, border `#85847F`):
    - ☑ صفقات مقارنة خلال 12 شهراً (5)
    - ☑ ذكر حالة الإشغال
    - ☑ الصور بلا وجوه أشخاص أو لوحات سيارات
    - ☐ إقرار الاستقلالية وعدم تعارض المصالح
- Footer: secondary `حفظ مسودة`; primary `تسليم الإصدار v2`.
- Rules: versioned submissions (v1 returned → v2); return carries itemised notes + new deadline; submit requires all required fields + scanned file + all checklist items (spec: `قائمة تحقق قبل الإرسال`) — see conflict C2. Submission starts the 7-day read-only expiry clock.
- Derive first-submission state: same form without alert, h2 e.g. «التسليم», button `تسليم` v1. Other states: draft saved, uploading/scanning, scan failed, submitted confirmation, past deadline.

---

## 3. Institution admin (A01–A07)

Spec aside «A02 المستخدمون · A04 الحدود» (verbatim, specInst):
- المستخدمون — دعوة، تعليق، تغيير دور (موافقة ثانية). حالة MFA ظاهرة.
- المصفوفة — قراءة سريعة بثلاث قيم مع نص بديل لكل خلية.
- الحدود — إصدارات مع تاريخ نفاذ؛ التعديل مقترح يُعتمد من مستوى أعلى.
- القواعد الثابتة — لا يمكن تعطيل فصل المهام والموافقات المزدوجة.
- التجاوب — سطح المكتب أولاً؛ على الجوال قراءة فقط.

Tenant: all A-screens scoped to the admin's current institution (`مصرف الأفق`, email domain `alufuq.example`). Blueprint: `ضمن منشأته فقط`.

### A01 — تسجيل المنشأة وإعداداتها / Organization onboarding & settings
- Route `/settings/organization` (SettingsNav `org`). Drawn only as card «A01 إعدادات المنشأة» in the OrgSLA-Reports artboard (right column, top).
- Fields (k → sample v): `الاسم` مصرف الأفق · `اللغة الافتراضية للمالك` العربية · `مهلة الخمول` 30 دقيقة · `التحقق بخطوتين` إلزامي لكل الأدوار · `نطاق البريد المسموح` alufuq.example.
- Implement as form: name (text, req), default owner language (select ar/en), idle timeout (minutes select), MFA policy (fixed «mandatory for all roles» — B2: MFA mandatory for institutions), allowed email domains (list; invitations restricted to them). Onboarding (application) happens in PA02; post-approval setup = «إعداد المنشأة» stage.

### A02 — المستخدمون والأدوار والصلاحيات / Users, roles & permissions
- Route `/settings/users` (+ `/settings/users/[userId]`, `/settings/roles`). Crumb `الإعدادات › المستخدمون والأدوار`.
- Header: h2 `المستخدمون والأدوار` + primary `دعوة مستخدم`.
- Users table (cols `1.6fr 1.3fr 1fr 1fr 1fr`): `المستخدم` (name bold + email ltr 12px) · `الدور` · `الفريق` · `MFA` (tone text) · `الحالة` (badge).

| المستخدم | email | الدور | الفريق | MFA | الحالة |
|---|---|---|---|---|---|
| سارة القحطاني | s.alqahtani@alufuq.example | مديرة حالات | التحصيل — الرياض | مفعّل (success) | نشط (success badge) |
| فهد العتيبي | f.alotaibi@alufuq.example | محلل ائتمان | التحصيل — الرياض | مفعّل | نشط |
| نورة الشهري | n.alshehri@alufuq.example | معتمدة (≤ 2M) | المخاطر | مفعّل | نشط |
| ماجد الحربي | m.alharbi@alufuq.example | القانونية | القانونية | مفعّل | نشط |
| ريم الدوسري | r.aldosari@alufuq.example | المالية | المالية | مفعّل | نشط |
| بدر السالم | b.alsalem@alufuq.example | مدير حالات | التحصيل — جدة | لم يُفعّل (error) | دعوة معلقة (warning badge) |

  (Admin ليلى الغامدي herself is not listed — sample only.)
- Card «مصفوفة الصلاحيات (مقتطف)» + link `كتالوج الصلاحيات الكامل` (→ full catalog; at inst level; PA05 at platform). Grid `1.8fr repeat(6,1fr)`; col header `الصلاحية` + role columns.
- **Role columns**: `مدير حالات` · `محلل` · `معتمد` · `قانونية` · `مالية` · `مسؤول`.
- **Matrix** (y=✓ `check` success, c=◐ `contrast` warning, n=— `remove` `#85847F`):

| الصلاحية | مدير حالات | محلل | معتمد | قانونية | مالية | مسؤول | catalog key |
|---|---|---|---|---|---|---|---|
| إنشاء حالة | y | n | n | n | n | y | `case.create` |
| إعداد حل | y | y | n | n | n | n | key TBD |
| اعتماد حل | n | n | c | n | n | n | `solution.approve` |
| كشف الهوية كاملة | c | c | c | c | n | n | `pii.reveal` (catalog name «كشف بيانات شخصية») |
| تسجيل دفعة | n | n | n | n | y | n | `payment.record` |
| بدء الإحالة | n | n | n | y | n | n | `referral.initiate` |
| إدارة المستخدمين | n | n | n | n | n | c | key TBD |

  Each cell `aria-label` = `${role}: مسموح|بشرط|غير مسموح`. Legend (verbatim): `✓ مسموح · ◐ بشرط (حد أو ليس المُعِدّ) · — غير مسموح. تغيير الأدوار يتطلب موافقة ثانية ويُسجل.`
- Actions: `دعوة مستخدم` (email must match allowed domain; select role + team; creates invitation → S05 `/invite/:token`); suspend user (`تعليق`, audited — PA11 sample `تعليق مستخدم … مغادرة الموظف` implies reason); change role = maker-checker (second admin approval, audited); resend/cancel invite (derive). Admin's own `إدارة المستخدمين` is conditional (◐) → cannot approve own change.
- States: user status `نشط`, `دعوة معلقة`; derive `معلّق`. MFA `مفعّل` / `لم يُفعّل`.

### A03 — قواعد المستندات / Document rules
- Route `/settings/documents` (SettingsNav `docs`). h2 `قواعد المستندات` + primary `إضافة قاعدة`.
- Table cols `1.5fr 1.2fr 1fr 0.9fr 1.5fr`: `نوع المستند` · `مطلوب قبل` (stage gate) · `يرفعه` (uploader party) · `الصلاحية` (validity) · `يراه` (visibility).

| نوع المستند | مطلوب قبل | يرفعه | الصلاحية | يراه |
|---|---|---|---|---|
| صك الملكية | تحقق | المصرف | — | فريق الحالة، القانونية |
| صورة الهوية | تحقق | المالك | 90 يوماً قبل الانتهاء | فريق الحالة فقط |
| كشف الراتب 3 أشهر | حل مقترح | المالك | 90 يوماً | فريق الحالة، المعتمد |
| تقرير التقييم | حل مقترح | مقدم الخدمة | 90 يوماً | فريق الحالة، المعتمد، المالك (ملخص) |
| وكالة موثقة | حسب الحاجة | المالك | حسب الوكالة | فريق الحالة، القانونية |
| إثبات السداد | تسوية نشطة | المالك أو المالية | — | فريق الحالة، المالية، المالك |

- Rule structure: `{documentTypeId (from PA08 platform catalog), requiredBeforeStage (CaseState|'as_needed'), uploaderParties[], validity {kind: none|days|days_before_expiry|per_document, days?}, visibleTo[] (+ summaryOnly flag e.g. «المالك (ملخص)»)}`. Validity must not exceed platform max (valuation ≤ 90 days). Providers see only docs explicitly shared to an assignment (V02).
- Actions: add/edit rule (form not drawn). Derive: whether edits need maker-checker (not stated).

### A04 — حدود الموافقة / Approval limits
- Route `/settings/approval-limits` (SettingsNav `limits`). Header: h2 `حدود الموافقة`; sub `الإصدار النافذ v4 · منذ 2026-07-01 · القيم أمثلة (افتراض)`; buttons secondary `سجل الإصدارات`, primary `اقتراح تعديل`.
- Table cols `1.4fr 1.4fr 1.2fr 1fr 1.6fr`: `المستوى` · `نوع الحل` · `المبلغ حتى` (ltr) · `التنازل حتى` · `فوق الحد`.

| المستوى | نوع الحل | المبلغ حتى | التنازل حتى | فوق الحد |
|---|---|---|---|---|
| محلل / مدير حالات | إعادة جدولة | — | — | إعداد فقط، لا اعتماد |
| معتمد | إعادة جدولة، سماح | 2,000,000 | 5% | معتمد أعلى |
| معتمد أول | كل الحلول الودية | 5,000,000 | 10% | لجنة المخاطر |
| لجنة المخاطر | كل الحلول الودية | بلا حد | حسب القرار | — |
| القانونية + معتمد | إحالة قضائية | — | — | موافقتان دائماً |
| المالية + معتمد | الإغلاق والتوزيع | — | — | موافقتان دائماً |

- Card «قواعد ثابتة لا تُعطَّل»: `• المُعِدّ والمراجِع لا يعتمدان طلبهما.` / `• الإحالة والإلغاء والإغلاق: موافقتان دائماً.` / `• أي تعديل على الحدود يتطلب موافقة المسؤول الثاني.`
- Card (pending) «تعديل مقترح v5 · بانتظار موافقة»: `رفع حد «المعتمد» من 2,000,000 إلى 2,500,000 ر.س.` / `اقترحته ليلى الغامدي · يعتمده: المدير التنفيذي للمخاطر`.
- Structure: `ApprovalLimitPolicy {version, status: draft|pending_approval|effective|superseded, effectiveFrom, proposedBy, approverRole/approvedBy, changeSummary}` → `tiers[] {level (role or role combo), solutionTypes[], maxAmount|null(unlimited)|n/a, maxWaiverPct|'by_decision'|n/a, escalateTo, requiresDualApproval}`. Guard (Handoff): `approver != preparer && amount <= limit`, requires `reason`, `mfa`.
- Actions: `اقتراح تعديل` → creates vN+1 draft → submit → second admin/higher level approves → becomes effective at date; `سجل الإصدارات` → version history. Proposer cannot approve. Validation vs PA07 platform minima (waiver without committee ≤ 10%, dual approvals cannot be removed).

### A05 — قوالب التواصل / Communication templates (editor)
- Routes `/settings/templates` (list, not drawn) and `/settings/templates/[templateId]`. Content grid `minmax(0,1fr) | 360px`.
- Header: `«عرض إعادة جدولة» · للمالك` + ltr code `TPL-OFFER-01 · v3`.
- Variant chips: `العربية` (selected) · `English` · `رسالة نصية` (channel/locale variants: ar body, en body, SMS).
- Body (verbatim, variables as chips): `أرسلنا لك عرضاً جديداً بقسط شهري {القسط} ريال لمدة {المدة}. يمكنك مراجعته والرد حتى {تاريخ_الصلاحية}، وإن كان لديك سؤال فاكتب لنا.`
- Variables row `متغيرات:` `{القسط}` `{المدة}` `{تاريخ_الصلاحية}` `{اسم_المسؤول}` (insertable).
- Buttons: secondary `حفظ مسودة`; primary `إرسال للاعتماد (الامتثال)` (publish requires compliance approval = `template.publish`).
- Aside «فحص النبرة» (auto checks): success `check_circle` `لا كلمات تهديد أو إلزام` · `يذكر المهلة وطريقة السؤال` · `أقل من 60 كلمة`; info `info` `النسخة الإنجليزية محدثة؟ نعم`.
- Aside «الاستخدام»: `أُرسل 312 مرة خلال 90 يوماً` · `آخر تعديل: الامتثال · 2026-08-02`.
- Rules: templates versioned; statuses draft → pending_compliance → published; inst templates derive from PA09 base templates. Validation: unknown variables rejected; tone-check failures should block/ warn (not specified which).

### A06 — مستوى الخدمة والمهام الأساسية / Basic SLA & tasks
- Route `/settings/sla` (SettingsNav `sla`). Artboard heading (full row) `مستوى الخدمة والمهام · المنشأة · التقارير`; layout 2 equal cols: left A06 panel, right A01 + A07 cards.
- Panel header `A06 مهل المراحل (أيام عمل)`; rows (cols `1.4fr 0.6fr 1.4fr`: stage · business days · escalation/rule):

| المرحلة | أيام عمل | الإجراء/القاعدة | CaseState |
|---|---|---|---|
| بانتظار البيانات | 5 | تذكير يوم 3 | awaiting_data |
| تحقق | 5 | تصعيد لمدير الفريق | verification |
| تقييم | 10 | يُحسب من إسناد المقيّم | valuation |
| موافقة داخلية | 3 | تذكير المعتمد يوم 2 | internal_approval |
| بانتظار العميل | 10 | توقف عند شكوى مفتوحة | awaiting_customer |
| بانتظار التسوية المالية | 5 | تصعيد للمالية | awaiting_reconciliation |

- Structure `SlaRule {stage, businessDays, clockStart ('stage_entry'|'valuer_assigned'), reminders[{dayOffset, target}], escalation{target}, pauseConditions ['open_complaint']}`. Brief A-09: values are examples, configurable per institution. Owner-response SLA must be ≥ platform min 7 days. Editing UI not drawn.

### A07 — التقارير التشغيلية الأساسية / Essential operational reports
- Route `/settings/reports` or `/reports` (see conflict C9). Card «A07 التقارير التشغيلية»; rows: icon `description` · title · meta (cadence · format) · icon button `download` `aria-label="تنزيل"` (36px).

| التقرير | meta |
|---|---|
| الحالات حسب الحالة والمرحلة | أسبوعي · CSV |
| الالتزام بمستوى الخدمة | شهري · PDF |
| الشكاوى ونتائجها | شهري · PDF |
| نشاط المستخدمين الحساس | عند الطلب · للمدقق |

- Rules: last report restricted to auditor role; downloads audited. Export pattern (02 Components Set 2): job idle/running/ready, link valid 24h.

---

## 4. Platform admin (PA01–PA13)

Global rule (Blueprint): `لا وصول لبيانات الحالة إلا مخفية أو بوصول مؤقت مدقق`. B2: platform admin re-verifies (MFA) on every login.

### PA01 — لوحة التشغيل / Operational dashboard (+PA13)
- Route `/platform` (or `/platform/ops`). h2 `التشغيل`; sub `أرقام مجمعة ومجهولة · لا بيانات حالات`.
- KPI row (4 cols; label · value ltr · sub):
  - منشآت نشطة · 11 · +1 هذا الربع
  - حالات نشطة (كل المنشآت) · 8,412 · مجمعة
  - مستخدمون نشطون اليوم · 624 · —
  - وصول مؤقت مفتوح · 1 · ينتهي 13:10
- Grid `1.3fr | 1fr`: left = PA13 panel; right = card «يحتاج انتباهاً»:
  - warning `warning` تأخر الرسائل النصية — 212 رسالة في الطابور
  - info `support_agent` 3 شكاوى مصعّدة للمنصة
  - charcoal `domain` طلبا انضمام بانتظار التحقق من الترخيص
  - error `bug_report` SUP-2026-1201: قالب الرفض لا يعرض السبب
- Only aggregate/anonymous numbers; no case rows.

### PA13 — مراقبة الخدمة / Service monitoring (panel «PA13 صحة الخدمة»)
- Route option `/platform/services` (panel in PA01). Rows (cols `1.6fr 1fr 1.4fr`: service · icon+status · metric):

| الخدمة | الحالة | icon/tone | المؤشر | Handoff enum |
|---|---|---|---|---|
| البوابات والواجهات | يعمل | check_circle success | 99.98% خلال 30 يوماً | enabled |
| تخزين المستندات وفحصها | يعمل | check_circle success | زمن الفحص 4.2 ث | enabled |
| الرسائل النصية | متقطع | warning warning | تأخر 6 د · المزوّد | (degraded — not in enum) |
| استيراد نظام التمويل (مصرف الأفق) | يعمل | check_circle success | آخر مزامنة 06:00 | enabled |
| مزود الهوية الوطني | غير مفعّل | cloud_off neutral | نمط محجوز · افتراض | pending/simulated |
| التكامل القضائي | غير متاح | cloud_off neutral | المرحلة 3 عند اعتماد قناة | unavailable |

### PA02 — طلبات المنشآت / Institution applications
- Route `/platform/institutions?tab=applications` (+ review `/platform/institutions/applications/[appId]`, not drawn). h2 `المنشآت والطلبات`; tabs `طلبات الانضمام 4` (selected, orange underline) / `المنشآت النشطة 11`.
- Table cols `1.6fr 1fr 1.6fr 1.2fr 1fr`: `المنشأة` (name + ltr id) · `النوع` · `التحقق` (icon + tone text) · `المرحلة` · (action) button `مراجعة`.

| المنشأة | id | النوع | التحقق | tone | المرحلة |
|---|---|---|---|---|---|
| شركة المدى للتمويل العقاري | APP-2026-0019 | تمويل عقاري | مستندات الترخيص مرفوعة | check_circle success | مراجعة الامتثال |
| بنك الساحل | APP-2026-0021 | بنك | ينقص خطاب التفويض | error error | بانتظار المنشأة |
| شركة ركيزة للتمويل | APP-2026-0022 | تمويل | جديد | schedule neutral | استلام |
| مصرف الواحة | APP-2026-0017 | بنك | مكتمل | check_circle success | إعداد المنشأة |

- Info note (icon `info`): `التحقق من الترخيص يدوي في المرحلة 1: يرفع مقدم الطلب المستندات ويراجعها الامتثال. لا يُفترض تكامل مع سجلات رسمية.`
- Application stages (ordered): `استلام` → `مراجعة الامتثال` ⇄ `بانتظار المنشأة` → `إعداد المنشأة` → active institution (derive: rejected). Verification statuses: `جديد`, `ينقص خطاب التفويض` (missing doc), `مستندات الترخيص مرفوعة`, `مكتمل`. Institution types: `بنك`, `تمويل`, `تمويل عقاري`.

### PA03 — قائمة المنشآت / Institution list — **not drawn: count only** (`المنشآت النشطة 11`). Route `/platform/institutions?tab=active`.

### PA04 — المستخدمون والأدوار (منصة) / Platform users & roles — **not drawn: count only** (`مستخدمو المنصة 14`). Route `/platform/users?tab=users`. Known platform roles: مسؤول عمليات, دعم تقني, الامتثال, مدقق.

### PA05 — كتالوج الصلاحيات / Permission catalog
- Route `/platform/users?tab=permissions` (or `/platform/permissions`). h2 `المستخدمون والصلاحيات`; tabs `مستخدمو المنصة 14` / `كتالوج الصلاحيات 62` (selected).
- Table cols `1.6fr 1fr 1fr 2fr`: `الصلاحية` (Arabic name + ltr key Mono) · `الحساسية` (tone text) · `نطاق` · `شرط`. Drawn 7 of 62:

| key | الصلاحية | الحساسية | نطاق | شرط |
|---|---|---|---|---|
| case.create | إنشاء حالة | عادية (charcoal) | المنشأة | — |
| solution.approve | اعتماد حل | عالية (error) | المنشأة | ضمن الحد · ليس المُعِدّ · MFA |
| pii.reveal | كشف بيانات شخصية | عالية | الحالة | سبب إلزامي · 60 ثانية · مسجل |
| payment.record | تسجيل دفعة | متوسطة (warning) | الحالة | مدقق مختلف للمطابقة |
| referral.initiate | بدء الإحالة | عالية | الحالة | القانونية فقط · موافقتان |
| platform.temp_access | وصول دعم مؤقت | عالية | الحالة | موافقة المنشأة + المدقق · مدة محددة |
| template.publish | نشر قالب | متوسطة | المنشأة | اعتماد الامتثال |

- Structure `Permission {key, nameAr, nameEn?, sensitivity: normal|medium|high, scope: institution|case (platform?), conditions[] (within_limit, not_preparer, mfa, reason_required, reveal_ttl_seconds=60, audited, different_checker, legal_only, dual_approval, institution_plus_auditor_approval, time_boxed, compliance_approval)}`. Institutions assign catalog permissions to roles (A02 matrix).

### PA06 — مراقبة الحالات مع الإخفاء ووصول دعم مؤقت مدقق / Case monitoring, masking & audited temp access
- Route `/platform/cases` (+ `/platform/cases/temp-access`). Role shown: `دعم تقني`. Grid `minmax(0,1fr) | 420px`.
- h2 `مراقبة الحالات`. Table cols `1.4fr 1.2fr 1.2fr 1fr`: `المعرّف المخفي` (ltr Mono) · `المنشأة` · `الحالة` · `عمر المرحلة`. Selected row bg `#FDF0EB`.

| المعرّف المخفي | المنشأة | الحالة | عمر المرحلة |
|---|---|---|---|
| C-7F3A…91 (selected) | مصرف الأفق | بانتظار العميل | 14 يوماً |
| C-19B2…04 | مصرف الأفق | تحقق | 3 أيام |
| C-A0E1…77 | شركة السنبلة | تقييم | 9 أيام |
| C-55D9…12 | مصرف الواحة | موافقة داخلية | 1 يوم |
| C-8C47…3F | شركة السنبلة | موقوفة | 41 يوماً |
| C-2E6B…A8 | مصرف الأفق | تسوية نشطة | — |

  Footnote: `الأسماء والهويات والمبالغ غير معروضة. المرجع الحقيقي يظهر فقط أثناء وصول مؤقت معتمد.` Masked id = opaque hash-derived id (`C-` + 4 hex + `…` + 2 hex), not the RH- reference.
- Aside form «طلب وصول مؤقت مدقق»: target `للحالة C-7F3A…91 · مصرف الأفق`.
  - `السبب وطلب الدعم المرتبط *` textarea, sample `SUP-2026-1201 — قالب رسالة الرفض لا يعرض السبب.` (required; must reference a support ticket SUP-…).
  - `النطاق` (fixed text) `قراءة فقط · تبويب المستندات والقوالب · بلا تنزيل`.
  - `المدة` select (`expand_more`), value `ساعتان` (other options not drawn).
  - `يوافق عليه:` `مسؤول منشأة مصرف الأفق + مدقق المنصة`; `كل شاشة تُفتح تُسجل، وتُشعر المنشأة بعد انتهاء الوصول.`
  - Primary `إرسال الطلب`.
- Temp-access flow: request (support, reason+ticket, scope, duration) → approval by institution admin of that case's org AND platform auditor (dual) → grant active (real reference visible, read-only, scoped tabs, no download) → auto-expire at start+duration → institution notified post-expiry; every screen view logged. Audit sample: `2026-09-23 11:10 · منح وصول مؤقت · دعم تقني + مدقق · مصرف الأفق · C-7F3A…91 · ساعتان · قراءة`; KPI `وصول مؤقت مفتوح 1 · ينتهي 13:10` (consistent with 2h). States derive: pending approvals, approved/active (countdown), rejected, expired, revoked.
- B2 help copy (institution-facing): `فريق دعم رهون لا يرى بيانات الحالة. إن احتاج ذلك، سيطلب وصولاً مؤقتاً مدققاً يوافق عليه مسؤول منشأتك، وينتهي تلقائياً.`

### PA07 — حدود الموافقة (منصة) / Approval thresholds (platform defaults)
- Route `/platform/defaults?tab=approval-limits`. h2 `الإعدادات الافتراضية للمنصة`; sub `قيم أساسية تُنسخ للمنشأة الجديدة، ويمكن للمنشأة تشديدها لا تخفيفها`. Tabs `حدود الموافقة` (selected) · `أنواع المستندات 38` · `القوالب الأساسية 24`.
- Table cols `1.8fr 1.4fr 1.6fr`: `القاعدة` · `الحد الأدنى للمنصة` · `ملاحظة`.

| القاعدة | الحد الأدنى للمنصة | ملاحظة | validation on institution |
|---|---|---|---|
| فصل المُعِدّ والمعتمد | إلزامي | لا يمكن للمنشأة تعطيله | non-editable true |
| الإحالة القضائية | موافقتان + القانونية | يمكن إضافة مستوى | approvals ≥ 2 incl. legal |
| الإغلاق | المالية + معتمد | يمكن إضافة مستوى | approvals ⊇ {finance, approver} |
| حد التنازل الأقصى بلا لجنة | 10% | المنشأة تحدد أقل | inst waiver (non-committee) ≤ 10% |
| صلاحية تقرير التقييم | ≤ 90 يوماً | المنشأة تحدد أقل | validity days ≤ 90 |
| مهلة رد المالك الدنيا | 7 أيام | لا تقل عن ذلك (افتراض) | owner response ≥ 7 days |

- Rule: defaults copied to new institution at onboarding; institution may only tighten.

### PA08 — أنواع المستندات / Document types — **not drawn: count only** (`38`). Route `/platform/defaults?tab=document-types`. Master list referenced by A03.
### PA09 — قوالب التواصل (منصة) / Communication templates — **not drawn: count only** (`القوالب الأساسية 24`). Route `/platform/defaults?tab=templates`. Audit sample shows platform publishing `TPL-REJECT-02 v4` by `الامتثال`.

### PA10 — الشكاوى والتصعيد / Complaints & escalations
- Route `/platform/complaints`. Role `الامتثال`. h2 `الشكاوى والتصعيد`.
- KPIs (4): `مفتوحة (كل المنشآت)` 27 (ink) · `مصعّدة للمنصة` 3 (warning) · `متأخرة` 1 (error) · `متوسط الحل` 3.8 أيام (ink).
- Table cols `1.2fr 1.2fr 2fr 1fr 1fr`: `الرقم` (ltr Mono) · `المنشأة` · `الموضوع` · `المهلة` (tone) · `المستوى`.

| الرقم | المنشأة | الموضوع | المهلة | tone | المستوى |
|---|---|---|---|---|---|
| CMP-2026-0142 | مصرف الأفق | رفض مستند دون سبب واضح | يومان | warning | المنشأة |
| CMP-2026-0131 | شركة السنبلة | تواصل خارج الأوقات المفضلة | متأخر يوم | error | المنصة |
| CMP-2026-0128 | مصرف الواحة | اعتراض على مبلغ غرامات | 4 أيام | success | المنصة |
| CMP-2026-0119 | مصرف الأفق | طلب نسخة من السجل | 5 أيام | success | المنشأة |
| CMP-2026-0101 | شركة السنبلة | زيارة دون موعد مؤكد | مغلقة | neutral | المنصة |

- Level = handling tier (institution vs escalated to platform). Complaint subjects visible but no owner PII. Open complaint pauses `بانتظار العميل` SLA and blocks referral (`no_open_complaint`). Detail view not drawn.

### PA11 — سجل التدقيق العام / General audit log
- Route `/platform/audit`. Role `مدقق`. h2 `التدقيق العام` + secondary `تصدير موقّع`.
- Table cols `170px 1.6fr 1.3fr 1.2fr 1.4fr`: `الوقت` (ltr) · `الحدث` (bold) · `الفاعل` · `المنشأة` · `التفاصيل`.

| الوقت | الحدث | الفاعل | المنشأة | التفاصيل |
|---|---|---|---|---|
| 2026-09-23 11:10 | منح وصول مؤقت | دعم تقني + مدقق | مصرف الأفق | C-7F3A…91 · ساعتان · قراءة |
| 2026-09-23 09:42 | تعديل حد موافقة (مقترح) | ليلى الغامدي | مصرف الأفق | v5 بانتظار الموافقة |
| 2026-09-22 17:05 | نشر قالب | الامتثال | المنصة | TPL-REJECT-02 v4 |
| 2026-09-22 15:30 | تعليق مستخدم | مسؤول المنشأة | شركة السنبلة | مغادرة الموظف |
| 2026-09-22 10:14 | فشل دخول متكرر | — | مصرف الواحة | 5 محاولات · قفل 15 د |
| 2026-09-21 16:00 | تصدير سجل موقّع | مدقق المنصة | مصرف الأفق | RH-… (مخفي) · طلب قانوني |

- Rules: append-only, hash-chained (Handoff audit JSON: `hash`, `prevHash` sha256; `actor{id,role,org}`, `at`, `reason`, `evidence[]`, `blocked`). Signed export is itself audited (with reason, e.g. `طلب قانوني`). Case refs masked in details. Filters not drawn.

### PA12 — الاحتفاظ والخصوصية / Retention & privacy
- Route `/platform/privacy`. Role `الامتثال`. h2 `الخصوصية والاحتفاظ`; sub `كل المدد أدناه افتراضات — تتطلب تأكيداً قانونياً قبل التفعيل`.
- Table cols `1.6fr 1.2fr 1.6fr 1fr`: `فئة البيانات` · `مدة الاحتفاظ` · `بعد الانتهاء` · `الحالة` (badge, warning style).

| فئة البيانات | مدة الاحتفاظ | بعد الانتهاء | الحالة |
|---|---|---|---|
| ملفات الحالات المغلقة | 10 سنوات (افتراض) | أرشفة مشفرة ثم حذف | مسودة |
| مستندات الهوية | حتى الإغلاق + 5 سنوات (افتراض) | حذف آمن مع سجل | مسودة |
| سجلات التدقيق | 10 سنوات (افتراض) | لا تُحذف قبل المدة | مسودة |
| رسائل التواصل | حتى الإغلاق + 5 سنوات (افتراض) | حذف | مسودة |
| وصول المالك بعد الإغلاق | 90 يوماً (افتراض) | سحب الوصول وإبقاء النسخ | مسودة |
| وصول مقدم الخدمة | التكليف + 7 أيام | سحب آلي | نافذ |

- Card «طلبات أصحاب البيانات»: `2 طلب اطلاع · 0 طلب تصحيح مفتوح` + link `عرض الطلبات`.
- Card «قواعد الإخفاء»: `الهوية: أول رقم وآخر رقمين · الاسم: الأول + حرف · الجوال: آخر رقمين`.
- Status: `مسودة` (inactive until legal confirmation) → `نافذ`. Brief A-12: configurable, legal confirmation required, never shown as final.

---

## 5. Business & security rules (consolidated)
1. **Tenant isolation**: each institution sees only its data; provider sees only its assignment; owner only own case (Brief). Switching org recreates session & clears query cache.
2. **Masking server-side** (Handoff): UI never receives full data except on an audited «كشف». Rules: ID first digit + last two (`1•••••••42`), name first name + initial (`سلطان ح.`), phone last two digits. `pii.reveal`: reason mandatory, 60 s, logged.
3. **Action exposure** (Handoff): no permission → action not sent (hidden); has permission but ineligible → sent with `reason` (rendered disabled + reason).
4. **Provider access** (A-11, PA12 `التكليف + 7 أيام` = `نافذ`): assignment only (scope, appointment, selected docs; no debt, no other parties). Write access until delivery → read-only for 7 days after delivery → automatic revocation (`سحب آلي`); expiry shown explicitly in UI. Masked deed on shared docs.
5. **Platform admins**: aggregate/anonymous data only; case rows via masked ids; real data only under approved temp access (dual approval institution admin + platform auditor, time-boxed, read-only, scoped, no download, every screen logged, institution notified after expiry).
6. **Maker-checker**: preparer/reviewer never approve own request; role changes need second approval; limit changes need second admin/higher-level approval; referral, cancellation, closure always two approvals; payment matching by different checker; template publish needs compliance approval.
7. **Platform minima** (PA07) enforced on institution config; institution may tighten only.
8. **MFA**: mandatory for all institution roles (A01); `solution.approve` requires MFA; platform users re-verify each login. Lockout after 5 failures, 15 min (PA11 sample, B2).
9. **SLA**: business days; configurable; pauses on open complaint; no automatic transition to referral.
10. **Audit**: immutable, hash-chained, every transition/decision/reveal/temp-access/config change logged including blocked attempts.

## 6. Implied entities & API

Entities (key fields):
- `ProviderOrg {id, name, type}`; `ProviderUser`; `Assignment {id 'ASG-YYYY-NNNN', lenderOrgId, caseId (hidden), providerOrgId, assigneeUserId, type, title, propertyLabel, status: new|in_progress|returned|submitted|closed, dueAt, inspection {at, confirmed, contactLabel}, scope[{k,v}], feesLabel, accessExpiresAt}`
- `AssignmentDocument {assignmentId, documentId, label, masked}`; `AssignmentMessage {assignmentId, authorId, authorOrgLabel, body, at, attachments[]}`
- `AssignmentSubmission {assignmentId, version, status: draft|submitted|returned|accepted, marketValue decimal(18,2) SAR, inspectionDate, reportFileId, fileScanStatus, checklist[{key,checked}], submittedAt}`; `SubmissionReturn {submissionId, notes[], returnedBy, returnedAt, resubmitDueAt}`
- `Institution {id, name, type, status, defaultOwnerLanguage, idleTimeoutMin, mfaPolicy, allowedEmailDomains[]}`; `InstitutionApplication {id 'APP-YYYY-NNNN', orgName, type, verificationStatus, stage, documents[], reviewer}`
- `User`, `Membership {userId, institutionId, roleId, teamId, status: active|invited|suspended, mfaEnrolled}`, `Invitation {email, roleId, teamId, expiresAt, token}`, `Team`
- `Role {id, institutionId|null(platform), name}`, `Permission {key, nameAr, sensitivity, scope, conditions[]}`, `RolePermission {roleId, permissionKey, grant: allow|conditional|deny, condition}`, `RoleChangeRequest {userId, fromRole, toRole, requestedBy, approvedBy, status}`
- `ApprovalLimitPolicy`/`ApprovalLimitTier` (§A04); `PlatformDefaultRule {key, minValue, note, editable}`
- `DocumentType` (platform, 38), `DocumentRule` (§A03)
- `CommunicationTemplate {code 'TPL-XXX-NN', audience, version, status, variants{ar,en,sms}, variables[], lastEditedBy/At, usage90d}`, `ToneCheckResult`
- `SlaRule` (§A06); `ReportDefinition {title, cadence, format, restrictedToRole}`, `ReportExport`
- `TempAccessRequest {id, caseId, institutionId, requesterId, reason, supportTicketRef, scope{readOnly, tabs[documents,templates], download:false}, durationMinutes, approvals[{role: institution_admin|platform_auditor, userId, decision, at}], status: pending|active|rejected|expired|revoked, startsAt, expiresAt}`; `TempAccessViewLog {requestId, screen, at}`
- `SupportTicket 'SUP-YYYY-NNNN'`; `Complaint 'CMP-YYYY-NNNN' {institutionId, subject, dueAt, level: institution|platform, status}`
- `AuditEvent {event, actor, org, at, details, reason, hash, prevHash}`; `SignedExport`
- `RetentionPolicy {category, period, afterAction, status: draft|effective}`, `DataSubjectRequest {type: access|correction, status}`, `MaskingRule`
- `ServiceHealth {service, status, metric, checkedAt}`, `OpsAlert`

API (ASP.NET, suggested):
- Provider: `GET /api/provider/assignments?status=active|delivered` · `GET /api/provider/assignments/{id}` · `GET/POST /api/provider/assignments/{id}/messages` · `GET /api/provider/assignments/{id}/documents/{docId}` (masked stream) · `PUT /api/provider/assignments/{id}/submission/draft` · `POST /api/provider/assignments/{id}/submission/files` (upload+scan) · `POST /api/provider/assignments/{id}/submission` (submit vN).
- Lender side (dependency, other batches): `POST /api/cases/{id}/assignments/{aid}/return` (notes, deadline), `POST .../messages`, `POST .../documents/share`.
- Inst admin: `GET/PUT /api/settings/organization` · `GET /api/settings/users` · `POST /api/settings/invitations` · `POST /api/settings/users/{id}/suspend` · `POST /api/settings/users/{id}/role-change-requests` + `POST /api/settings/role-change-requests/{id}/approve|reject` · `GET /api/settings/permission-matrix` · `GET/POST/PUT /api/settings/document-rules` · `GET /api/settings/approval-limits` (+`/versions`) · `POST /api/settings/approval-limits/proposals` + `/approve|reject` · `GET/PUT /api/settings/templates/{id}` · `POST /api/settings/templates/{id}/submit-for-approval` · `POST /api/settings/templates/{id}/tone-check` · `GET/PUT /api/settings/sla-rules` · `GET /api/settings/reports`, `POST /api/settings/reports/{id}/exports`.
- Platform: `GET /api/platform/ops/kpis`, `/alerts`, `/services` · `GET /api/platform/institution-applications` + `GET/POST .../{id}/review` · `GET /api/platform/institutions` · `GET /api/platform/users` · `GET /api/platform/permissions` · `GET /api/platform/cases/monitor` (masked) · `POST /api/platform/temp-access` + `POST /api/temp-access/{id}/approve|reject` (institution admin & auditor) + `GET /api/platform/temp-access/{id}` + auto-expiry job · `GET/PUT /api/platform/defaults/{approval-rules|document-types|templates}` · `GET /api/platform/complaints` · `GET /api/platform/audit` + `POST /api/platform/audit/exports` (signed) · `GET/PUT /api/platform/retention-policies` · `GET /api/platform/data-subject-requests`.
- Jobs: provider access expiry, temp-access expiry + institution notification, SLA reminders/escalations, retention (inactive until legally confirmed).

## 7. Integration dependencies & labels
- SMS provider (`الرسائل النصية`, template SMS variant; degraded state `متقطع`).
- Document storage + malware scan (`تخزين المستندات وفحصها`, `فُحص`).
- Core financing import per institution (`استيراد نظام التمويل (مصرف الأفق)`, `آخر مزامنة 06:00`).
- National identity provider — reserved placeholder (`نمط محجوز · افتراض`).
- Judicial integration — unavailable until Phase 3 (`المرحلة 3 عند اعتماد قناة`).
- License verification — manual in Phase 1, no official registry integration.
- Handoff integration state enum: `enabled | simulated | pending | unavailable | failed`.

## 8. Components
New in B7: `PlatformSidebar` (props `active`, `role`), `SettingsNav` (prop `active`), inline Provider header shell (to componentise), assignment list row/card, chat-thread bubble + composer, submission checklist, return-notes alert, permission matrix cell (tri-state icon + aria-label), versioned-policy header (effective version + history + propose), pending-proposal card, fixed-rules card, template editor (variant chips + variable chips + tone-check aside + usage), KPI tile, service-health row, attention list, temp-access request panel, masked-id cell, key/value spec card, report row with icon download button.
Reused: `LenderSidebar`, `LenderTopbar`, grid tables (`role=table/row/cell`, sticky header), tabs with count (`<bdi>` count, selected = inset bottom 3px `#F4633A`), status badges (radius 4, 12px 600), chips (pill 32px), primary/secondary buttons (min-height 40–44, radius 6), file upload row, money input with `ر.س` suffix, export job pattern.

## 9. Conflicts & ambiguities
- **C1 Provider access on return**: rule resolved as §5.4 (V02 copy, A-11, PA12 agree). Open: whether a return (V04) reopens write access and resets the 7-day clock — design implies write access until the resubmission deadline and the clock restarts at the accepted/final delivery.
- **C2 Submit enabled with unchecked checklist**: `تسليم الإصدار v2` is styled enabled while `إقرار الاستقلالية وعدم تعارض المصالح` is unchecked; spec says checklist before sending → recommend disabled with reason.
- **C3 Temp-access approvers**: B2 help says institution admin only; PA06 + catalog say institution admin + platform auditor. Duration options beyond `ساعتان` not specified; max duration not stated.
- **C4 Masking**: PA12 phone = last two digits; working-notes fictional phone `+966 5• ••• ••81` also leaks leading `5`. Name rule `الأول + حرف` matches.
- **C5 Unknown roles**: `المدير التنفيذي للمخاطر` (v5 approver), `معتمد أول`, `لجنة المخاطر` absent from A02 role columns; `مدقق` (institution auditor for reports) and institution-level `الامتثال` (template approval) not in matrix either. Full institution role list is not enumerated anywhere.
- **C6 Unused variable**: `{اسم_المسؤول}` listed but not in body.
- **C7 Retention badge** uses warning style for `نافذ` too (should likely be success).
- **C8 Permission key gaps**: `إعداد حل`, `إدارة المستخدمين` have no catalog key; matrix label `كشف الهوية كاملة` vs catalog `كشف بيانات شخصية`; 55 of 62 keys not shown.
- **C9 Reports placement**: SettingsNav has `reports` under settings, Brief nav puts A07 at top-level `التقارير` (LenderSidebar also has `التقارير`). A01/A06/A07 share one artboard with SettingsNav `sla` active.
- **C10 Future nav items**: SettingsNav `providers` and PlatformSidebar `providers`, `workflow`, `billing`, `integrations` are P2/P3 (B9/B10) — hide or show disabled in P1 (undecided).
- **C11 PA13 status `متقطع`** has no equivalent in Handoff integration enum (needs `degraded`).
- **C12 Provider inbox SLA** column shows remaining days only; whether counted in business days (A06) or calendar days is not stated. Returned item SLA `4 أيام` vs resubmission deadline `2026-09-27` (4 calendar days from 09-23) → calendar. Stronger: ASG-2026-0871 due 2026-09-26 (Saturday, Saudi weekend) shown as `3 أيام` from 2026-09-23 — a business-day count cannot land on Saturday, so inbox SLA is calendar days (while A06 stage deadlines are `أيام عمل`).
- **C13 Not drawn**: PA03, PA04, PA08, PA09 content; A05 template list; A03 add-rule form; A06 edit UI; A01 onboarding wizard; PA02 review detail; PA10 detail; PA11 filters; temp-access approval screens for institution admin/auditor; all empty/loading/error states (except V04 returned).
- **C14 Crumb vs matrix naming**: A02 crumb `المستخدمون والأدوار` vs matrix `الإعدادات › المستخدمون`; A04 crumb `حدود الموافقة` vs `الإعدادات › الموافقات`; SettingsNav `sla` label `مستوى الخدمة والمهام` vs crumb `مستوى الخدمة`.
- **C15 Whether document-rule and SLA edits require maker-checker** is not stated (limits and roles do).
