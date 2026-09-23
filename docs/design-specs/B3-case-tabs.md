# B3 — Case Tabs (L06–L12) · Implementation spec

Source: `design-source/04 Phase 1 - B3 Case Tabs.dc.html` (+ `_condensed/…txt`), `CaseHeader.dc.html`, `09 Handoff/working-notes.md`, `00 Brief & Assumptions` (inventory rows L06–L12, assumptions A-xx).
Page title: `04 Phase 1 — B3 Case tabs — رهون`. Page H1: `المرحلة 1 — الدفعة B3: تبويبات الحالة`.
All screens: Lender app, Arabic RTL (`dir="rtl" lang="ar"`), amounts/refs/dates wrapped in `<bdi dir="ltr">` (never mirrored, A-02). Gregorian = system of record; Hijri (Umm al-Qura) shown beside deadlines (A-03).

Design summary block (verbatim): `صُمم في B3` — `L06–L12: 6 إطارات سطح مكتب (الأطراف، التمويل، العقار والرهن، المستندات مع لوحة الطلب، معاينة الإصدارات، التقييم والتحليل) + جوال التمويل والمستندات. مكوّن مشترك جديد: CaseHeader.`

---

## 0. Shared shell for every desktop case-tab artboard

Artboard 1440 wide, bg warm `#FAF9F6`. Horizontal flex:
- `LenderSidebar` 264px, props `active="cases"` (default user = سارة القحطاني, case manager).
- Column: `LenderTopbar` (64px) props `crumb1="الحالات" crumb2="RH-2026-004172"` → `CaseHeader` (~210px) → `<main>`.
- Main padding ≈ 24px 40px 40px.

### CaseHeader (used in all B3 screens with defaults except `tab`)
Props (kebab in design): `tab`, `state-key`, `sla-tone`, `sla-text`, `stage`, `stage-names`, `extra-tab`, `case-ref`, `title`.
B3 uses only `tab` → so effective values: `stateKey=proposed` («حل مقترح»), `stage=3` («إعداد الحل»), `slaTone=warn`, `slaText="مهلة المرحلة: يومان · 2026-09-25"`, `caseRef="RH-2026-004172"`, `title="عبدالله م. — فيلا سكنية، حي النرجس، الرياض"`.

Structure:
1. Meta line: `{caseRef}` (mono, LTR) · `تمويل سكني · مرابحة` · `مصرف الأفق` (these two are hard-coded in the component → must become props/data).
2. `<h2>{title}</h2>`.
3. Chips row: state badge (`{icon} {label}` with tone colors), SLA chip (`{sla.icon} {slaText}`), owner chip (`person` icon) `المسؤولة: سارة القحطاني` (hard-coded → data).
4. Button `إجراءات أخرى` + `expand_more`, `aria-haspopup="menu"` (inline-end of title block).
5. Stage stepper `<ol aria-label="مراحل الحالة">`, 7 equal columns; each `li` has bar + label, `aria-current="step"` on current. Bar color: done (i<stage) `#22262A`, current `#F4633A`, todo `#E4E3DF`; label weight 700 on current; todo label `#5E5D58`.
   Default stage names (0–6): `الاستلام | التحقق | التقييم | إعداد الحل | الموافقة الداخلية | رد المالك | التنفيذ والإغلاق` (overridable via `stageNames` "a|b|…").
6. Tabs `role="tablist" aria-label="أقسام الحالة"`, buttons `role="tab"`, min-height 44, selected = weight 700, ink, `inset 0 -3px 0 #F4633A` underline; others `#5E5D58`.

Tab keys → labels (fixed order):
| key | label | route segment |
|---|---|---|
| overview | نظرة عامة | `/cases/[ref]` |
| parties | الأطراف | `/parties` |
| finance | التمويل والمديونية | `/finance` |
| property | العقار والرهن | `/property` |
| documents | المستندات | `/documents` |
| valuation | التقييم والتحليل | `/valuation` |
| solutions | الحلول | `/solutions` |
| payments | المدفوعات | `/payments` |
| comms | التواصل والمهام | `/comms` |
| audit | السجل | `/audit` |
| extraTab `"key:label"` | e.g. sale / referral / closure | appended last, only when that path is opened |

Tab rule (from anchor spec): fixed order; «البيع» and «الإحالة» appear only when their path is opened; numeric badges on tabs = items needing attention and must be announced as text (anchor overview shows badges `المستندات 1`, `الحلول v2`, `التواصل والمهام 2`; the CaseHeader component itself has no badge prop → add `badges`).

State map (`stateKey` → label, icon, fg/bg/border):
| key | label | icon | tone |
|---|---|---|---|
| awaiting | بانتظار البيانات | hourglass_top | info |
| verification | تحقق | fact_check | info |
| valuation | تقييم | query_stats | info |
| proposed | حل مقترح | tips_and_updates | rust `#8E3920/#FDF0EB/#F0B8A6` |
| approval | موافقة داخلية | approval | warning |
| customer | بانتظار العميل | hourglass_empty | info |
| negotiation | تفاوض | forum | rust |
| settlement | تسوية معتمدة / نشطة | handshake | success |
| sale | بيع طوعي | sell | success |
| referral | إحالة قضائية | outbound | neutral outline (`#22262A` on white, border `#85847F`) |
| external | بيع قضائي خارجي | open_in_new | neutral outline |
| reconciliation | بانتظار التسوية المالية | calculate | warning |
| closed | مغلقة | check_circle | inverse (white on `#22262A`) |
| paused | موقوفة | pause_circle | neutral (`#22262A` on `#F2F1ED`) |

SLA tone → (fg, bg, icon): ok (`#1E6A45`,`#EAF4EE`,schedule) · warn (`#8A5300`,`#FBF2DE`,alarm) · err (`#B3261E`,`#FCECEA`,alarm_off) · info (`#1D5A8C`,`#EAF2F9`,hourglass_top) · none (`#5E5D58`,`#F2F1ED`,pause_circle).

Mobile (390×844) case screens do NOT use CaseHeader: compact top bar = back button (44×44, icon `arrow_forward` = RTL back, `aria-label="رجوع"`) + case ref (mono LTR) or page title; optional horizontally scrollable short tablist (`نظرة عامة | الأطراف | التمويل | المستندات`).

Common case-level rules (anchor spec, apply to all tabs): loading = skeleton for header & figures; forbidden = «لا تملك صلاحية» page revealing no data; offline = read-only. 768: side column moves below content, tabs scroll horizontally. All history (solution & document versions) is kept and visible — no deletion.

Masking (A-10): ID = first digit + last two (`1•••••••42`); name = first name + initial (`عبدالله م.`); phone `+966 5• ••• ••81`; DOB `19••`; deed `3••••••18`; contract `MF-88-3317•••`. Full reveal requires permission + reason, logged, visible to auditor, 60-second window.

---

## L06 — الأطراف · Parties

- Artboard: `P1-Lender-CaseParties-Desktop-Default · 1440` (min-height 980). No mobile artboard (spec only).
- Roles (Brief): فريق الحالة (case team). Provider sees no parties except owner's short name and inspection appointment.
- Route: `/cases/[ref]/parties`. CaseHeader `tab="parties"` (defaults otherwise).

### Layout
`main` grid: `minmax(0,1fr) 340px` (content | side panel). Content: header row + stacked party cards.

### Content
Header row: `<h3>الأطراف (3)</h3>` (count = parties.length) + secondary button `إضافة طرف` (icon `person_add`).

Party card `<article>` grid `48px minmax(0,1fr) auto`:
- Avatar circle 48px with initials (primary owner: ink `#151513` bg / white text; others `#F2F1ED` / ink).
- Name (strong) + role pill.
- Facts grid `repeat(3,minmax(0,1fr))`, each = key (muted) / value.
- Note line: icon + text colored by tone.
- Actions column: secondary small button `{action}` + text-link button `كشف…` (icon `visibility`, rust `#AA4528`, `aria-label="كشف البيانات كاملة (يتطلب سبباً ويُسجَّل)"`).

Sample data (verbatim):
| av | name | role | facts | note (icon, tone) | action |
|---|---|---|---|---|---|
| ع م | عبدالله م. | المالك والمقترض | الهوية: 1•••••••42 · الجوال: +966 5• ••• ••81 · تاريخ الميلاد: 19•• · اللغة المفضلة: العربية · الحالة الوظيفية: موظف · قطاع خاص · العنوان: الرياض، النرجس | متحقق من الهوية عبر الدعوة 2026-08-20 (verified_user, success `#1E6A45`) | مراسلة |
| ن م | نورة ع. | زوجة المالك · ساكنة | العلاقة: ساكنة في العقار · طرف في العقد: لا · التواصل: غير مسموح | لا تُشارك معها بيانات مالية (block, muted `#5E5D58`) | تعديل |
| س ع | سلمان ع. | ممثل غير نظامي | العلاقة: ابن المالك · تفويض موثق: غير مرفوع · يحضر المكالمات: نعم، بطلب المالك | طلب وكالة موثقة لمنح صلاحية الاطلاع (pending, warning `#8A5300`) | طلب وكالة |

Note: avatar initials for نورة ع. are `ن م` in data (inconsistent with name) — see conflicts.

Side panel (340px):
1. Card `التواصل مع المالك` rows (k / v):
   - `الدعوة` → `مقبولة 2026-08-20`
   - `التحقق من الهوية` → `مكتمل`
   - `القنوات المسموحة` → `المنصة، رسائل نصية`
   - `أوقات التواصل` → `9 ص – 5 م، أيام العمل`
   - `آخر تواصل` → `2026-09-20`
2. Warning note (bg `#FBF2DE`, border `#E2C27A`, icon `diversity_1`): `**احتياج تواصل:** يطلب المالك أن تكون المكالمات بحضور ابنه (ممثل غير نظامي). لا تُشارك بيانات مالية معه دون تفويض موثق.`
3. Neutral note (warm bg, icon `shield_person`): `كل كشف للبيانات الكاملة يتطلب سبباً ويُسجَّل ويظهر للمدقق. مدة الكشف 60 ثانية.`

### Actions
| Action | Effect |
|---|---|
| إضافة طرف | Opens add-party form (not designed): role, relation to contract, contact permission, verification. |
| مراسلة | Opens message composer to that party via allowed channels (comms tab). Only for parties with contact allowed. |
| تعديل | Edit party record. |
| طلب وكالة | Creates document request for a notarized POA (وكالة موثقة) — reuse L10 request drawer with type preset. |
| كشف… | Reveal full PII: modal asking reason (required) → logs audit event (actor, reason, fields, time) → shows unmasked for 60 s then re-masks. |

### Business rules
- All party data masked by default.
- Contact permission per party (`التواصل: غير مسموح` blocks messaging; financial data never shared with non-party occupant).
- Informal representative gets financial visibility only after documented POA uploaded + verified.
- Communication needs (e.g., calls with son present) captured as a party/case note shown prominently.
- Provider visibility: only owner short name + inspection appointment.

### Spec aside (verbatim)
- هدف المستخدم: معرفة من هم أطراف الحالة، وما المسموح مشاركته مع كل منهم.
- البيانات: الدور، العلاقة بالعقد، التحقق، القنوات، احتياجات التواصل. كلها مخفية افتراضياً.
- الإجراءات: إضافة طرف، طلب وكالة، مراسلة، كشف مسجل بسبب.
- الصلاحية: فريق الحالة يرى؛ مقدم الخدمة لا يرى الأطراف إلا اسم المالك المختصر وموعد المعاينة.
- التجاوب: 390: بطاقة لكل طرف، الحقائق في قائمة عمودية.

### Entities / API
- `CaseParty { id, caseId, displayNameMasked, fullName(enc), role: owner_borrower|occupant|informal_rep|guarantor|co_borrower…, roleLabel, isContractParty, relation, nationalId(enc), phone(enc), dob(enc), preferredLanguage, employmentStatus, address, contactAllowed:bool, identityVerifiedAt, identityVerifiedVia, poaStatus: none|requested|uploaded|verified, attendsCalls, notes[] {icon, tone, text} }`
- `CaseContactPreferences { caseId, invitationStatus, invitationAcceptedAt, idVerificationStatus, allowedChannels[] (platform, sms…), contactHours, lastContactAt, communicationNeeds(text) }`
- `PiiRevealLog { id, userId, caseId, partyId, fields[], reason, revealedAt, expiresAt(+60s) }`
- API: `GET /cases/{ref}/parties`, `POST /cases/{ref}/parties`, `PATCH /cases/{ref}/parties/{id}`, `POST /cases/{ref}/parties/{id}/reveal {reason}` → returns unmasked payload + expiresAt, `GET /cases/{ref}/contact-preferences`, `POST /cases/{ref}/document-requests` (POA).
- Integrations: identity verification via invitation + OTP + national ID provider placeholder (A-07) — "متحقق من الهوية عبر الدعوة".

---

## L07 — التمويل والمديونية · Financing & debt

- Artboards: `P1-Lender-CaseFinance-Desktop-Default · 1440` (min-h 1000); `P1-Lender-CaseFinance-Mobile · 390` (390×844).
- Roles: المحلل، المالية (read for case team).
- Route: `/cases/[ref]/finance`. CaseHeader `tab="finance"`.

### Layout (desktop)
1. Full-width info banner `role="note"` (info tone `#EAF2F9`/`#9DC0DE`, icon `database`).
2. Grid `minmax(0,1.3fr) minmax(0,1fr)`: debt breakdown table | contract data card.
3. Full-width installment history card with 12-column `<ol>`.

### Content
Banner: `المبالغ مستوردة من نظام التمويل الأساسي وللقراءة فقط. آخر مزامنة 2026-09-22 18:40.` + secondary small button `طلب تصحيح بيانات`.

Debt table: title `تفصيل المديونية القائمة`, `role="table" aria-label="تفصيل المديونية"` (div grid, cols `minmax(0,1.4fr) 1fr 1.2fr`). Columns: `البند` | `المبلغ (ر.س)` (LTR) | `المصدر`.
| البند | المبلغ | المصدر |
|---|---|---|
| أصل التمويل المتبقي | 1,121,840.00 | نظام التمويل · 2026-09-22 |
| الأرباح المستحقة | 144,420.00 | نظام التمويل · 2026-09-22 |
| غرامات التأخير | 18,300.00 | نظام التمويل · 2026-09-22 |
| الرسوم | 0.00 | — |
| **إجمالي القائم** (weight 700, bg `#FAF9F6`) | **1,284,560.00** | مجموع البنود |
Rule: total = sum of items, auto-verified (1,121,840 + 144,420 + 18,300 + 0 = 1,284,560).

Contract card `بيانات العقد` (k/v):
رقم العقد: MF-88-3317••• · تاريخ العقد: 2021-05-10 · مبلغ التمويل الأصلي: 1,450,000.00 ر.س · المدة الأصلية: 240 شهراً (متبقٍ 176) · القسط الأصلي: 13,774.29 ر.س · هامش الربح: ثابت · حسب العقد · أول قسط متأخر: 2026-02-01

Installment history: title `سجل الأقساط — آخر 12 شهراً` + meta `القسط الأصلي 13,774.29 ر.س`. `<ol aria-label="سجل الأقساط">` grid `repeat(12,…)`; each `li aria-label="{YYYY-MM}: {label}"` shows icon, month `YY-MM` (e.g. `25-10`), status label. Cells tinted by status:
| code | label | icon | fg / bg / border |
|---|---|---|---|
| p | مسدد | check_circle | `#1E6A45 / #EAF4EE / #9CCBB0` |
| m | غير مسدد | cancel | `#B3261E / #FCECEA / #EFA59C` |
| x | جزئي | contrast | `#8A5300 / #FBF2DE / #E2C27A` |
| u | مستحق | schedule | `#5E5D58 / #FFFFFF / #CBCAC6` |
Data: 2025-10 p, 2025-11 p, 2025-12 p, 2026-01 p, 2026-02 m, 2026-03 m, 2026-04 m, 2026-05 x, 2026-06 m, 2026-07 m, 2026-08 m, 2026-09 u.
Summary line: `7 أقساط غير مسددة منذ 2026-02 · مجموعها 96,420.00 ر.س · دفعة جزئية واحدة في 2026-05 لم تغطِّ القسط.`
Arrears ≈ unpaid count × original installment (7 × 13,774.29 = 96,420.03 → shown 96,420.00; see conflicts).

### Mobile (390)
- Top bar: back + `RH-2026-004172`; tablist `نظرة عامة | الأطراف | التمويل (selected) | المستندات`.
- Hero card: `إجمالي القائم` / `1,284,560.00` `ر.س` / `نظام التمويل · 2026-09-22 18:40`.
- List of first 4 debt rows (k / LTR amount) — total not repeated.
- Card `آخر 6 أشهر`: 6-col grid (2026-04 … 2026-09) icon + month `MM`, aria `YYYY-MM: label`.

### Actions
- `طلب تصحيح بيانات` → creates task for Finance (ريم الدوسري role) with field + expected value + note; does NOT modify number. No other edits (read-only).

### States
- Sync delayed > 24 h: prominent warning banner (warning tone replaces info).
- Sync failed: show last known value with tag «غير محدث».
- History is text per month; icon/colour are supportive (a11y).

### Spec aside (verbatim)
- هدف المستخدم: فهم مكونات المديونية ومصدرها وتاريخ التعثر قبل بناء الحل.
- البيانات: كل مبلغ: القيمة + المصدر + وقت المزامنة. الإجمالي = مجموع البنود ويُتحقق آلياً.
- الإجراءات: للقراءة فقط؛ «طلب تصحيح» ينشئ مهمة للمالية ولا يعدّل الرقم مباشرة.
- الوصول: سجل الأقساط قائمة بنص لكل شهر؛ الأيقونة واللون داعمان.
- الحالات: مزامنة متأخرة > 24 ساعة: تحذير بارز. فشل المزامنة: آخر قيمة معروفة مع وسم «غير محدث».

### Entities / API
- `FinancingContract { id, caseId, contractNoMasked, contractDate, productType ('تمويل سكني · مرابحة'), originalAmount, originalTermMonths, remainingTermMonths, originalInstallment, profitType ('ثابت'), firstOverdueDate, lenderId }`
- `DebtSnapshot { id, caseId, source ('core_banking'), syncedAt, syncStatus: ok|delayed|failed, items[] { code: principal|profit|late_fees|fees, label, amount, sourceLabel, asOf }, total }` (server validates total = Σ items).
- `InstallmentHistory { caseId, month (YYYY-MM), status: paid|unpaid|partial|due, amountDue, amountPaid }`; derived `arrearsCount`, `arrearsAmount`, `arrearsSince`.
- `DataCorrectionRequest (Task) { caseId, field, currentValue, proposedValue, note, assigneeRole: finance, status }`.
- API: `GET /cases/{ref}/finance` (contract + snapshot + 12-month history), `POST /cases/{ref}/finance/correction-requests`.
- Integration: «نظام التمويل الأساسي» (core financing system) — import/sync with timestamp; labelled as source on every amount.

---

## L08 + L09 — العقار والرهن · Property & Mortgage/security

- Artboard: `P1-Lender-CasePropertyMortgage-Desktop-Default · 1440` (min-h 1000). One tab covers both L08 (الرهن والضمانات, role القانونية) and L09 (العقار, فريق الحالة).
- Route: `/cases/[ref]/property`. CaseHeader `tab="property"`.

### Layout
`main` grid `minmax(0,1fr) minmax(0,1fr)`.
- Start column: `<section aria-labelledby="prop-h">` property photo placeholder then property details.
- End column: stacked sections: mortgage, legal note, inspection card.
- 768 & 390: single column, photo first.

### Content
Photo: `role="img" aria-label="مكان صورة واجهة العقار من تقرير التقييم"` (placeholder text `property photo — from valuation report v1`). Source = valuation report v1 photos.

`<h3>العقار</h3>` k/v:
النوع: فيلا سكنية · دوران · الموقع: الرياض · حي النرجس · مساحة الأرض: 450 م² · مساحة البناء: 520 م² · سنة البناء: 2019 · رقم الصك: 3••••••18 · الإشغال: يسكنها المالك وأسرته · القيمة (التقييم): 1,650,000.00 ر.س
Warning note (icon `family_restroom`): `**عقار مسكون من المالك وأسرته.** أي زيارة تحتاج موعداً متفقاً عليه عبر المنصة، ولا تُنشر صوره خارج ملف الحالة.`

`<h3>الرهن والضمانات</h3>` + split badge `مراجعة قانونية` (neutral half) | `مكتملة` (success half). k/v:
الجهة المرتهنة: مصرف الأفق · درجة الرهن: الأولى · تاريخ التسجيل: 2021-05-12 · قيود أخرى: لا توجد · التأمين على العقار: ساري حتى 2027-05-11 · مطابقة الصك: متحقق 2026-08-29 · مصدر التحقق: مستند مرفوع + مراجعة القانونية

Card `ملاحظة القانونية`: `الرهن مسجل بالدرجة الأولى ولا توجد قيود لاحقة. لا يوجد ما يمنع التسوية أو البيع الطوعي بموافقة المالك.` — footer `ماجد الحربي · القانونية · 2026-08-28`.

Card (icon `event`): `معاينة المقيّم` / `تمت 2026-09-04 10:00 بحضور المالك` + badge `مكتملة`.

### Actions
None drawn. Implied: Legal role can edit legal review status + note (others read-only).

### States / rules
- Legal review status: pending | complete (| issues). **Pending legal review blocks transition to «حل مقترح»** (proposed).
- Family occupancy = protection info: visits only by appointment agreed via platform; photos never published outside the case file.

### Spec aside (verbatim)
- هدف المستخدم: التأكد من سلامة الضمان وفهم وضع الإشغال قبل أي خيار.
- النبرة: الإشغال الأسري معلومة حماية، تُعرض بلطف وتفرض قيوداً على الزيارات والصور.
- الصلاحية: القانونية تعدّل حالة المراجعة؛ الباقون قراءة.
- الحالات: مراجعة قانونية معلقة: تحجب الانتقال إلى «حل مقترح».
- التجاوب: 768 و390: عمود واحد، الصورة أولاً.

### Entities / API
- `Property { id, caseId, type ('فيلا سكنية · دوران'), city, district, landAreaM2, builtAreaM2, yearBuilt, deedNoMasked, occupancy: owner_family|tenant|vacant, occupancyLabel, valuationValue (from current valuation), photoDocumentId }`
- `Mortgage { id, caseId, mortgagee, rank (1), registeredAt, otherEncumbrances, insuranceValidUntil, deedMatchStatus, deedMatchedAt, verificationSource, legalReviewStatus: pending|complete, legalNote { text, authorId, date } }`
- `Inspection { id, caseId, providerAssignmentId, scheduledAt, attendedBy ('المالك'), status: scheduled|completed|cancelled }`
- API: `GET /cases/{ref}/property`, `PATCH /cases/{ref}/mortgage/legal-review` (legal only), guard in case state machine `proposeSolution` requires `legalReviewStatus=complete`.

---

## L10 — المستندات والطلبات · Documents & requests

Artboards:
- `P1-Lender-CaseDocuments-Desktop-RequestDrawer · 1440` (1440×1040, drawer open)
- `P1-Lender-CaseDocuments-Desktop-VersionPreview · 1440` (1440×1040, dark full-screen viewer)
- `P1-Lender-CaseDocuments-Mobile · 390`
- Roles: فريق الحالة. Route: `/cases/[ref]/documents`; drawer `/cases/[ref]/documents?request=new` (or intercepting route `@modal`); preview `/cases/[ref]/documents/[docId]?v=2` (full screen). CaseHeader `tab="documents"`.

### Layout — list
- Toolbar: filter chips (pill, 36px; selected = ink bg, white text): `الكل 9` (selected) · `مطلوبة 1` · `قيد المراجعة 0` · `تنتهي قريباً 1`; primary button `طلب مستند` (icon `add`) at inline-end.
- List rows grid `36px minmax(0,1fr) 150px 40px`: type icon | name + version tag (LTR) + meta line | status badge (icon + text, tone) | `more_vert` menu button (`aria-label="المزيد"`).

Rows (verbatim; icon → status icon, tone):
| icon | name | ver | meta | status | status icon / tone |
|---|---|---|---|---|---|
| badge | صورة الهوية الوطنية | v1 | المالك · 2026-08-20 | تنتهي خلال 12 يوماً | event_upcoming / warning |
| description | صك الملكية | v1 | المصرف · 2026-08-15 | متحقق | check_circle / success |
| receipt_long | عقد التمويل | v1 | نظام التمويل · 2026-08-14 | متحقق | check_circle / success |
| request_quote | كشف الراتب لآخر 3 أشهر | v2 | المالك · 2026-09-19 · v1 مرفوض | متحقق | check_circle / success |
| work | تعريف بالراتب | v1 | المالك · 2026-09-19 | متحقق | check_circle / success |
| analytics | تقرير التقييم | v1 | مكتب «ب» · 2026-09-10 | صالح حتى 2026-12-09 | event_available / success |
| policy | مذكرة المراجعة القانونية | v1 | ماجد الحربي · 2026-08-28 | داخلي | visibility_off / neutral (`#5E5D58`/`#F2F1ED`) |
(Design maps `gavel`→`policy`, `lock`→`visibility_off`: A-14 bans roof/key/hammer/lock icons.)

### Request drawer (dialog)
`<aside role="dialog" aria-labelledby="req-h">` 520px, anchored inline-end (left in RTL), full height, scrim `rgba(21,21,19,.32)` over page.
- Header: `<h3 id="req-h">طلب مستند</h3>` + close button (`aria-label="إغلاق"`).
- Field `نوع المستند *` — select; value `صورة الهوية الوطنية (محدثة)`; helper: `قاعدة المنشأة: PDF/JPG · صلاحية ≥ 90 يوماً · يراه فريق الحالة فقط` (rules come from institution document-type config: allowed formats, min validity, visibility).
- Fieldset `من يرفعه *` — radio cards (44px): `المالك — عبدالله م. (عبر بوابة المالك)` (selected: 2px ink border, `#FDF0EB` bg) | `فريق المصرف (داخلي)`.
- Field `المهلة *` — date picker showing `2026-10-03` + trailing `21 ربيع الآخر 1448هـ · 10 أيام` (Hijri + days-from-today).
- Preview block `معاينة ما سيراه المالك` (warm box): `نحتاج صورة حديثة من هويتك الوطنية لأن الصورة الحالية تنتهي صلاحيتها قريباً. يمكنك رفعها حتى 2026-10-03. إن واجهت صعوبة، اختر «أحتاج مساعدة».` Caption: `من قالب «تحديث مستند منتهٍ» · يُرسل إشعار نصي + داخل البوابة`.
- Footer: secondary `إلغاء`, primary (flex:1) `إرسال الطلب`.
Validation: type, uploader, deadline required (deadline > today). Template text is generated from template + type + deadline; owner-visible copy must be previewed before send. On mobile the request is a full page.

### Version preview (full-screen, dark `#151513`, white text)
- Header: close (`aria-label="إغلاق المعاينة"`), title `كشف الراتب لآخر 3 أشهر`, tag `v2`, `RH-2026-004172`, outline button `تنزيل بعلامة مائية` (icon download).
- Body grid `minmax(0,1fr) 380px`: document viewer (560px page; `document preview — PDF page 1/3`, `role="img" aria-label="معاينة المستند"`) | aside.
- Aside: `الإصدارات` + note `لا يُحذف أي إصدار`; version cards:
  - `v2 · الحالي` — status `متحقق` (check_circle success) — `رفعه المالك 2026-09-19 21:14 · PDF 3 صفحات · 1.2 م.ب` — `قبلته سارة القحطاني 2026-09-20 · «مطابق لتعريف الراتب»`
  - `v1` — status `مرفوض` (undo, error) — `رفعه المالك 2026-09-12 · JPG · صورة ملتقطة بالجوال` — `رفضته سارة القحطاني 2026-09-14 · «الصفحة الثانية غير مقروءة» · أُبلغ المالك بلغة واضحة`
- Footer: `يراه: فريق الحالة، المحلل، المعتمد. مخفي عن مقدمي الخدمة.` / `الصلاحية: صالح حتى 2026-12-20 (90 يوماً).`
- Accept/reject actions for a version under review are implied (not drawn): reject requires reason, sent to owner in plain language.

### Mobile (390)
Top bar: back · `المستندات · 9` · icon button `add_circle` (`aria-label="طلب مستند"`, rust). List of cards (first 5): icon, name, status text, `chevron_left`. Preview = full-screen; request = full page.

### States
Document statuses: `مطلوب` (requested) · `مرفوع قيد المراجعة` (uploaded, under review) · `متحقق` (verified) · `مرفوض` (rejected) · `منتهٍ` (expired) · `تنتهي قريباً` (expiring soon) · plus `داخلي` (internal/hidden) and `صالح حتى {date}`.
Filter counts: all / requested / under review / expiring soon.

### Business rules
- No version deletion; each upload = new version; reject needs reason (sent to owner in clear language).
- Download always watermarked with user name + time, and logged.
- Visibility per document (case team, analyst, approver; hidden from providers; internal legal memo hidden from owner).
- Validity: per type (e.g. salary statement 90 days; valuation 90 days).
- Expiring soon triggers a renewal request using template «تحديث مستند منتهٍ».

### Spec aside (verbatim)
- هدف المستخدم: طلب المستند الصحيح من الشخص الصحيح بمهلة واضحة، ومراجعة الإصدارات.
- طلب مستند: لوحة جانبية: النوع (قواعد المنشأة)، المسؤول عن الرفع، المهلة بالهجري والميلادي، ومعاينة نص المالك قبل الإرسال.
- الإصدارات: لا حذف. الرفض يتطلب سبباً يُرسل للمالك بلغة واضحة.
- الأمان: التنزيل بعلامة مائية باسم المستخدم والوقت، ويُسجل.
- الحالات: مطلوب، مرفوع قيد المراجعة، متحقق، مرفوض، منتهٍ، تنتهي قريباً.
- التجاوب: 390: قائمة بطاقات؛ المعاينة ملء الشاشة؛ الطلب صفحة كاملة.

### Entities / API
- `DocumentType { id, institutionId, name, allowedFormats[], minValidityDays, validityDays, defaultVisibility[], ownerTemplateId }`
- `CaseDocument { id, caseId, typeId, name, icon, source: owner|lender|core_system|provider|legal, visibility[] (case_team, analyst, approver, provider, owner), internal:bool, currentVersionId, status, validUntil }`
- `DocumentVersion { id, documentId, versionNo, uploadedBy, uploadedAt, format, pages, sizeBytes, storageKey, reviewStatus: pending|verified|rejected, reviewedBy, reviewedAt, reviewNote, ownerFacingReason }`
- `DocumentRequest { id, caseId, typeId, uploaderKind: owner|internal, uploaderPartyId, dueDate, templateId, renderedOwnerMessage, channels[] (sms, portal), status: open|fulfilled|overdue|cancelled, createdBy }`
- `DownloadLog { userId, versionId, at, watermarkText }`
- API: `GET /cases/{ref}/documents?filter=all|requested|in_review|expiring`, `GET /documents/{id}/versions`, `GET /documents/versions/{vid}/preview`, `POST /documents/versions/{vid}/download` (returns watermarked file, logs), `POST /documents/versions/{vid}/review {decision, note, ownerReason}`, `POST /cases/{ref}/document-requests`, `POST /document-requests/preview` (render template).
- Integrations: SMS + portal notification (template «تحديث مستند منتهٍ»); Hijri conversion (Umm al-Qura, approximate in UI).

---

## L11 + L12 — التقييم والتحليل · Valuation & Analysis

- Artboard: `P1-Lender-CaseValuation-Desktop-Default · 1440` (min-h 1000). One frame shows both sub-tabs (annotation `↓ same frame, "التحليل" sub-tab (L12 Analysis)`).
- Roles: L11 المحلل; L12 المحلل الائتماني.
- Route: `/cases/[ref]/valuation` (sub-tab `التقييم`) and `/cases/[ref]/valuation/analysis` (sub-tab `التحليل`). CaseHeader `tab="valuation"`.

### Layout
Segmented control `role="tablist"`: `التقييم` (selected: white pill + shadow, 700) | `التحليل`.
- L11: grid `minmax(0,1.2fr) minmax(0,1fr)`: valuation summary card | assignment timeline.
- L12: grid 3 equal columns.
- 390: the two sub-tabs are separate; cards stacked.

### L11 content
Card header `ملخص تقرير التقييم v1` + badge (event_available, success) `صالح · 77 يوماً` (days until validUntil: 2026-09-23 → 2026-12-09 = 77).
3 KPI tiles:
- `القيمة السوقية` / `1,650,000` / `ر.س`
- `النطاق` / `1.58M – 1.72M` / `حسب المقارنات`
- `التمويل إلى القيمة` / `77.9%` / `محسوب` → **LTV = إجمالي القائم ÷ القيمة السوقية = 1,284,560 / 1,650,000 = 77.85% → 77.9%**
k/v: المقيّم: مكتب تقييم معتمد «ب» · ترخيص سارٍ · تاريخ المعاينة: 2026-09-04 · تاريخ التقرير: 2026-09-10 · المنهجية: المقارنة (5 صفقات) + التكلفة · الصلاحية: حتى 2026-12-09 (90 يوماً)
Buttons: `فتح التقرير` (secondary) · `طلب إعادة تقييم` **disabled** (`aria-describedby="reval-why"`), reason text: `معطّل: التقرير الحالي صالح. يُتاح قبل 14 يوماً من الانتهاء أو بسبب موثق.`
→ Enabled when `today ≥ validUntil − 14 days` (here from 2026-11-25) OR user supplies documented reason (hence also an override path).

Card `تكليف التقييم` — vertical timeline (`<ol>`, rows `24px 1fr`: icon + title/meta):
| icon (color) | title | meta |
|---|---|---|
| assignment (`#22262A`) | إنشاء التكليف | فهد العتيبي · 2026-08-30 · مهلة 10 أيام |
| forum (info `#1D5A8C`) | سؤال من المقيّم: موعد المعاينة | 2026-08-31 · رد خالد ز. خلال 3 ساعات |
| event (`#22262A`) | المعاينة بحضور المالك | 2026-09-04 10:00 |
| upload_file (success) | تسليم التقرير v1 | 2026-09-10 · قبل المهلة بيومين |
| lock_clock (muted) | انتهاء وصول المقيّم | 2026-09-17 · آلي (7 أيام بعد التسليم) |
Rule A-11: provider access = assignment duration + 7 days read-only, then auto-revoked; shown in timeline.

### L12 content (3 columns)
1. `القدرة على السداد` (k / LTR v):
   - صافي الدخل الشهري المتحقق: 32,400.00
   - التزامات أخرى: 2,100.00
   - القسط الحالي / الدخل: 42.5% → 13,774.29 / 32,400
   - **القسط المقترح v2 / الدخل: 46.5%** (weight 700) → 15,074.52 / 32,400
   - حد السياسة (افتراض): 55%
   Formula: **DSR = monthly installment ÷ verified net monthly income** (the sample numbers exclude «التزامات أخرى»; see conflicts). Income must come from verified documents (salary statement v2 verified 2026-09-20).
2. `مؤشرات الظرف` list:
   - (work_history) `انخفاض الدخل 28% منذ 2026-01 (تغيير جهة العمل)`
   - (home) `العقار سكن رئيسي للأسرة`
   - (handshake) `المالك متعاون ويرد خلال يومين في المتوسط`
   Caption: `مدخلة من المحلل · تُستخدم لاختيار الحل، لا لتقييم الشخص`
3. `الخيارات الممكنة` (icon + **title** — desc):
   - check_circle success — **إعادة جدولة** — ممكنة ضمن حد الاستقطاع بمدة ≥ 80 شهراً
   - check_circle success — **تنازل عن الغرامات** — ضمن صلاحية المعتمد (1.42%)
   - remove muted — **سداد مخفض** — لا سيولة مصرّح بها
   - remove muted — **بيع طوعي** — لم يطلبه المالك؛ لا يُعرض إلا بموافقته
   Caption: `قائمة مرجعية للمحلل؛ القرار بشري ويمر بالموافقات.`
   (≥ 80 months: minimum term n with 1,266,260/n ≤ 55% × 32,400 = 17,820 → n ≥ 71.06 on the simple division model; design says 80 — treat as analyst-entered text, not computed.)

### Spec aside (verbatim)
- هدف المستخدم: الاعتماد على تقييم صالح ومستقل، وتحليل قدرة موثق.
- التقييم: القيمة + النطاق + المنهجية + الصلاحية. إعادة التقييم معطّلة مع السبب حتى الأهلية.
- التحليل: أرقام من مستندات متحقق منها فقط، مع مصدرها. مؤشرات الظرف وصفية لا تصنيفية.
- دعم القرار: «الخيارات الممكنة» قائمة مرجعية وليست توصية آلية.
- مقدم الخدمة: وصوله ينتهي آلياً ويظهر ذلك في الخط الزمني.
- التجاوب: 390: التبويبان منفصلان، البطاقات مكدسة.

### Entities / API
- `ValuationAssignment { id, caseId, providerId, createdBy, createdAt, dueDays, status, accessExpiresAt, events[] {type, at, actor, text} }`
- `ValuationReport { id, assignmentId, version, marketValue, rangeLow, rangeHigh, methodology, comparablesCount, inspectionDate, reportDate, validUntil, documentVersionId, photos[] }`
- `AffordabilityAnalysis { caseId, netMonthlyIncome, incomeSourceDocVersionId, otherObligations, currentInstallment, currentDsr, proposedSolutionVersionId, proposedDsr, policyDsrLimit (config, 0.55), circumstanceIndicators[] {icon, text, enteredBy}, options[] {kind, feasible, note} }`
- API: `GET /cases/{ref}/valuation`, `POST /cases/{ref}/valuation/revaluation-requests {reason?}` (server: 409 unless within 14 days of expiry or reason documented), `GET /cases/{ref}/analysis`, `PUT /cases/{ref}/analysis/indicators`, `PUT /cases/{ref}/analysis/options`.
- Integrations: valuation provider (مكتب تقييم معتمد «ب») via provider portal; auto access revocation job.

---

## Reusable components (B3)

| Component | Props / variants | Notes |
|---|---|---|
| `CaseHeader` | `caseRef, title, productLabel, lenderName, ownerName, stateKey (14), slaTone (ok/warn/err/info/none), slaText, stage 0–6, stageNames[], tab, extraTabs[{key,label}], badges{tabKey:text}` | + `إجراءات أخرى` menu. Hard-coded strings to be props. |
| `LenderSidebar` | `active (portfolio|cases|tasks|approvals|complaints|reports|none), userName, userRole, initials, approvals` | From earlier batch. |
| `LenderTopbar` | `crumb1, crumb2` | Breadcrumb. |
| `MobileCaseTopBar` | `title|caseRef, onBack, trailingAction?, tabs?` | 44px touch targets; back icon `arrow_forward`. |
| `PartyCard` | `avatarInitials, avatarVariant(primary|subtle), name, roleLabel, facts[], note{icon,tone,text}, primaryAction, onReveal` | Mobile: facts vertical. |
| `RevealPiiButton` + `RevealReasonDialog` | `fields, onRevealed(expiresAt)` | 60 s countdown, audit. |
| `KeyValueList` / `KVRow` | `items[{k,v}]`, `ltr` values | Used everywhere. |
| `Callout/Note` | `tone (info|warning|success|error|neutral), icon, title?, body, action?` | `role="note"` / `role="alert"`. |
| `DataTable (div grid)` | `columns[], rows[], totalRow?` | role=table/row/cell (no `<table>` + loops per design rule). |
| `InstallmentHistoryStrip` | `months[{ym, status}]`, `count 12|6` | status map p/m/x/u. |
| `StatusBadge` | `tone, icon, label` | Documents, versions. |
| `SplitBadge` | `label, value, tone` | «مراجعة قانونية | مكتملة». |
| `FilterChips` | `items[{label,count,selected}]` | Pill 36px. |
| `DocumentRow` | `icon, name, version, meta, status, menu` | Grid 36/1fr/150/40. |
| `SideDrawer` | `title, width (480|520), onClose, footer` | Inline-end, scrim .32. |
| `RadioCardGroup` | `options, value` | Selected: 2px ink border + `#FDF0EB`. |
| `DualDateField` | `value, showHijri, showDaysFromToday` | «21 ربيع الآخر 1448هـ · 10 أيام». |
| `OwnerMessagePreview` | `text, templateName, channels` | |
| `DocumentViewer` (full-screen dark) | `document, versions[], onDownloadWatermarked` | |
| `VersionCard` | `version, status, meta, reviewNote` | |
| `SegmentedTabs` | `items, value` | التقييم / التحليل. |
| `KpiTile` | `label, value, unit/caption` | |
| `Timeline` | `items[{icon,color,title,meta}]` | Assignment timeline. |
| `ChecklistItem` | `icon, tone, title, desc` | Options list. |
| `DisabledWithReason` | button `disabled` + `aria-describedby` reason text | Pattern for all gated actions. |

## Conflicts / ambiguities (B3)

1. **CaseHeader hard-codes** `تمويل سكني · مرابحة`, `مصرف الأفق`, `المسؤولة: سارة القحطاني` — must be data; also gender of «المسؤولة/المسؤول».
2. **Tab label mismatch:** CaseHeader `التقييم والتحليل` vs anchor overview tabs `التقييم`; Brief routes L11 `الحالة › التقييم`, L12 `الحالة › التحليل`. Recommend one tab with sub-tabs as designed.
3. **Tab badges** exist in anchor overview but not in CaseHeader component → add prop.
4. **Mobile tab labels** shortened (`التمويل`) and only 4 tabs shown; full mobile tab set/overflow not designed.
5. **Parties count & avatar:** `نورة ع.` has avatar `ن م`; spec says "3 parties" — fine. Unclear whether lender itself is a party.
6. **Arrears:** 7 × 13,774.29 = 96,420.03, displayed 96,420.00; "7 unpaid" counts 6 unpaid + 1 partial (2026-05) and does not subtract the partial amount — define arrears formula with finance (likely from core system, not computed).
7. **Document counts:** chip `الكل 9` but only 7 rows in data; `مطلوبة 1` but no row shows «مطلوب» (the pending ID renewal request is presumably the 8th/9th item). Mobile title also `9`.
8. **Salary statement validity** `صالح حتى 2026-12-20 (90 يوماً)` from 2026-09-20 is 91 days; valuation `2026-12-09 (90 يوماً)` from 2026-09-10 = 90 ✓. Define validity start (upload vs review date).
9. **DSR definition:** 46.5% = installment / income, ignoring `التزامات أخرى 2,100.00` (with it: 53.0%). Clarify whether other obligations count.
10. **Option «≥ 80 شهراً»** inconsistent with 55% limit on simple math (≥ 72); treat as free text or recompute.
11. **Re-valuation eligibility:** "or documented reason" implies an alternate enabled path while the button is shown disabled — need UX for reason entry (e.g. menu item «بسبب موثق»).
12. **Legal-review gate** blocks «حل مقترح», yet the case is already in `proposed` state in the header; confirm ordering (legal review completed 2026-08-28, before proposal — consistent).
13. **L08 edit UI** for legal (status/note edit) not drawn.
14. **Add party / edit party / reveal-reason dialog / reject-version dialog** not drawn — build from shared patterns (review screen, drawer).
15. **Mobile artboards** only for finance & documents; parties/property/valuation mobile behaviours are described in spec text only.
