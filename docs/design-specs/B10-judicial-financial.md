# B10 — Judicial & Financial Integration (Phase 3)

Source: `design-source/06 Phase 3 - B10 Judicial & Financial Integration.dc.html` (+ `_condensed/…txt`). Page title: `06 Phase 3 — B10 Judicial & financial integration — رهون`. Page H1: «المرحلة 3 — الدفعة B10: التكامل القضائي والمالي المعتمد».

**Tagging convention:** statements are taken from the design unless marked **(inferred)**. All routes, entity names, field names and API operations are **(inferred)**. Enum names come from `09 Handoff` (`CaseState`, integration states, decision-support tags).

## 0. Global banner and rules for the batch

- Page-level note (role=note, warning icon): **«افتراض — يتطلب تأكيداً قانونياً ومنتجياً:»** «تُفعَّل هذه الشاشات فقط عند اعتماد قناة تكامل رسمية. رهون لا تدير البيع القضائي ولا تحل محل الأنظمة الرسمية؛ تعرض ما تبلغه الجهة المختصة بمصدره ووقته، منفصلاً دائماً عن حالة المنصة.»
- Handoff implementation rules that apply here:
  - Every integration has a state `enabled | simulated | pending | unavailable | failed`.
  - The external status is stored **verbatim** with `source` and `syncedAt`, and is **never converted** into the platform state.
  - Simulation tags every related record and produces no effect.
  - No automatic transition into `judicial_referral` from any state. Guard: `no_open_complaint && notice_sent && objection_period_elapsed`.
- CaseState enum (Handoff): `draft, awaiting_data, verification, valuation, proposed_solution, internal_approval, awaiting_customer, negotiation, active_settlement, voluntary_sale, judicial_referral, external_judicial_sale, awaiting_reconciliation, closed, paused, cancelled`.
  - CaseHeader `stateKey` → enum: `external`=«بيع قضائي خارجي»=`external_judicial_sale`; `reconciliation`=«بانتظار التسوية المالية»=`awaiting_reconciliation`; `referral`=«إحالة قضائية»=`judicial_referral`; `closed`=«مغلقة».
- Frame summary at the end of the page: «صُمم في B10 — J01–J07، F01–F04، PA18: 8 إطارات سطح مكتب + إطار جوال للمالك (الحالة الرسمية وحقوقه).» Next: «التالي: B11 — المرحلة 4 — التحليل والاختناقات والعمليات القابلة للتهيئة ودعم القرار.»
- Brief matrix (00):

| ID | AR | EN | Role | Area | Nav |
|---|---|---|---|---|---|
| J01 | الجاهزية القانونية المتكاملة | Legal readiness (integrated) | القانونية | Legal | الحالة › الإحالة |
| J02 | تصدير الحالة ومطابقتها | Case export & reconciliation | القانونية | Legal | الإحالة › التصدير |
| J03 | المرجع والحالة الرسمية الخارجية | External reference & official status | القانونية | Legal | الإحالة › الحالة الخارجية |
| J04 | الاستثناءات | Exceptions | القانونية | Legal | الإحالة › الاستثناءات |
| J05 | بوابة الوكيل — الحالات المكلفة | Agent portal — assigned cases | وكيل البيع القضائي | Agent | الوكيل › الحالات |
| J06 | بوابة الوكيل — الجاهزية والخطة | Agent — readiness & plan | وكيل البيع القضائي | Agent | الوكيل › الحالة › الخطة |
| J07 | بوابة الوكيل — التحديثات والنتيجة والأدلة | Agent — updates, result & evidence | وكيل البيع القضائي | Agent | الوكيل › الحالة › النتيجة |
| F01 | التسوية المالية المتكاملة | Financial reconciliation | المالية | Finance | الحالة › الإغلاق › التسوية |
| F02 | التوزيعات المعتمدة | Authorized distributions | المالية، المعتمد | Finance | الإغلاق › التوزيعات |
| F03 | مستندات الفك والمخالصة | Release & clearance documents | القانونية | Finance | الإغلاق › المستندات |
| F04 | الإغلاق بمصادر قابلة للتتبع | Traceable closure | المعتمد | Finance | الإغلاق › مراجعة |
| PA18 | التكاملات وحالاتها | Integrations & states | إدارة المنصة | Platform | المنصة › التكاملات |

- Phase-1 precedents that these screens extend (from B5):
  - L25 «حزمة جاهزية الإحالة القضائية اليدوية والمرجع الخارجي» (P1-Legal-ReferralReadiness) is the **manual** variant. J01/J03 are the **integrated** variant.
  - L26 «التسوية المالية اليدوية والإغلاق» is the manual variant. F01–F04 are the integrated variant.
- Shared sample case (working notes): **RH-2026-003511**, فيصل ر., «شقة سكنية، حي الصفا، جدة», lender مصرف الأفق, product «تمويل سكني · مرابحة».
  - Amounts: sale 760,000; costs 22,800; lender 684,200; surplus 53,000.
  - People: ماجد الحربي (legal), ريم الدوسري (finance, preparer), عبدالرحمن ش. (finance checker), نورة الشهري (approver).
  - Agent office: مكتب الوكالة «ط». External reference: `EXT-JD-2026-•••8841`.

### Colour semantics used throughout (tokens)
- success `#1E6A45/#EAF4EE/#9CCBB0`; warning `#8A5300/#FBF2DE/#E2C27A`; error `#B3261E/#FCECEA/#EFA59C`; info `#1D5A8C/#EAF2F9/#9DC0DE`; neutral `#22262A/#F2F1ED/#CBCAC6`.
- Accent orange `#F4633A`: current item, selected-row inset mark (`inset -3px 0 0 #F4633A`). Primary button: rust `#AA4528`. Disabled button: `#E4E3DF` background with `#6B6A65` text.
- Status is always icon + text; colour is only supportive.

---

## J01 + J03 — Legal readiness · External reference & official status

- **Artboard:** `P3-Legal-ReferralIntegrated-Desktop-ExternalStatus · 1440`. Section label «J01-J03 Legal readiness external status». Desktop only is drawn.
- **Role:** القانونية (user ماجد الحربي, role «القانونية», initials «م ح»).
  - Access rule (specJ1 «الصلاحية»): «القانونية تدير؛ فريق الحالة يقرأ؛ المالك يرى ملخصاً.»
- **Route (inferred):** `/cases/[caseRef]/referral`. J03 is embedded in the same page. An optional deep link `/cases/[caseRef]/referral/external-status` (inferred).
- **Shell props:**
  - `LenderSidebar active="cases" user-name="ماجد الحربي" user-role="القانونية" initials="م ح"`
  - `LenderTopbar crumb1="RH-2026-003511" crumb2="الإحالة"`
  - `CaseHeader tab="referral" extra-tab="referral:الإحالة" case-ref="RH-2026-003511" title="فيصل ر. — شقة سكنية، حي الصفا، جدة" state-key="external" sla-text="تُحدَّث من القناة المعتمدة" sla-tone="info" stage="4" stage-names="الجاهزية|الاعتماد|إشعار المالك|التقديم|الإجراء الخارجي|النتيجة|التسوية والإغلاق"`
  - This gives a referral-specific 7-stage stepper whose current stage is index 4, «الإجراء الخارجي».
- **Layout:** `main` is a grid `minmax(0,1fr) 420px`.
  - Left column: two status cards side by side (`1fr 1fr`), then the J03 log card.
  - Right `aside`: J01 readiness card, then the external reference card, then the integration-state legend.
  - Responsive (specJ1): «768: البطاقتان مكدستان؛ السجل كامل العرض.» Mobile for legal is not specified.

### Content

**Status separation cards.** specJ1: «بطاقتان متجاورتان: داخلية (فاتحة) ورسمية (داكنة، نص حرفي، مصدر ووقت).»

1. Internal card (white, border `#CBCAC6`):
   - Label «حالة المنصة (داخلية)».
   - Value: icon `open_in_new` + «بيع قضائي خارجي».
   - Meta: «منذ `2026-11-18` · قرار داخلي».
2. Official card (dark `#151513`, white text, orange icon):
   - Label «الحالة الرسمية (خارجية)».
   - Value: icon `account_balance` + ««قيد التنفيذ لدى الجهة المختصة»». The guillemets are part of the display, marking verbatim text.
   - Meta: «المصدر: القناة المعتمدة · مُزامن `2026-12-02 09:00`».

**J03 official status log.**
- Title «J03 سجل الحالة الرسمية». Subtitle «نص الحالة كما ورد حرفياً · لا يُعدَّل».
- Rows use a grid `24px 150px 1fr 150px`: icon | datetime (LTR) | title (bold) + message | source tag.
- Source tags (SRC):

| key | label | bg | fg |
|---|---|---|---|
| ext | القناة المعتمدة | #FFFFFF | #151513 |
| int | داخلي | #F2F1ED | #22262A |
| agt | أبلغ بها الوكيل | #FBF2DE | #8A5300 |

- Rows, in design order (not chronological):

| icon (colour) | datetime | title | message | source |
|---|---|---|---|---|
| check_circle (#1E6A45) | 2026-11-20 10:14 | تم استلام الطلب | «تم استلام طلب التنفيذ» | ext |
| check_circle | 2026-11-24 12:30 | تكليف وكيل البيع | مكتب الوكالة «ط» | ext |
| check_circle | 2026-11-26 08:00 | إشعار المالك من الجهة | وفق إجراءات الجهة | ext |
| radio_button_checked (#F4633A) | 2026-12-02 09:00 | قيد التنفيذ لدى الجهة المختصة | آخر حالة رسمية | ext |
| description (#8A5300) | 2026-12-01 17:40 | محضر البيع (غير مؤكد رسمياً بعد) | 760,000.00 ر.س · أبلغ به الوكيل | agt |
| sync_problem (#B3261E) | 2026-11-29 03:00 | فشل مزامنة | انتهت المهلة · أُعيدت المحاولة تلقائياً ونجحت 03:15 | int |

**J01 legal readiness.**
- Title «J01 الجاهزية القانونية». Each item shows icon + text + date (LTR).

| icon | item | date / meta |
|---|---|---|
| check_circle green | قرار الإحالة معتمد (موافقتان) | 2026-11-10 |
| check_circle | إشعار المالك المسبق | 2026-10-20 |
| check_circle | انقضاء مهلة الاعتراض دون اعتراض | 2026-11-04 |
| check_circle | الحزمة PKG-3511-02 مقبولة | 2026-11-20 |
| check_circle | لا شكاوى مفتوحة | — |
| info (#1D5A8C) | التفويض القانوني للقناة | ساري حتى 2027-06 |

- Check: 2026-10-20 + 15 days = 2026-11-04, matching the 15-day objection period (assumption) in flow f8.

**External reference card.**
- Title «المرجع الخارجي». Key/value rows:
  - «رقم الطلب» `EXT-JD-2026-•••8841` (masked, LTR)
  - «وكيل البيع المكلّف» مكتب الوكالة «ط»
  - «آخر مزامنة ناجحة» `2026-12-02 09:00`
- Button (secondary, icon `sync`): «طلب تحديث الآن».

**Integration-state legend.** Title «وسوم حالة التكامل». Chips show label + description:

| enum | label | description | bg/fg/border |
|---|---|---|---|
| enabled | مفعّل | مزامنة حية من قناة معتمدة | #EAF4EE/#1E6A45/#9CCBB0 |
| simulated | محاكاة | بيانات اختبار؛ لا أثر | #EAF2F9/#1D5A8C/#9DC0DE |
| pending | قيد الانتظار | أُرسل ولم يصل رد | #FBF2DE/#8A5300/#E2C27A |
| unavailable | غير متاح | القناة متوقفة؛ يدوي | #F2F1ED/#22262A/#CBCAC6 |
| failed | فشل | رفض أو خطأ؛ استثناء مفتوح | #FCECEA/#B3261E/#EFA59C |

### Actions
- «طلب تحديث الآن»: requests an on-demand sync of the official status (inferred). No disabled state is drawn.
  - (inferred) Disable it with a reason when the integration is `unavailable`, or while a sync is in flight.
  - (inferred) When the channel is `unavailable`, the J03 reference is entered manually, as in flow f8 («إدخال يدوي»).
- CaseHeader «إجراءات أخرى» menu (from the shared component). Its contents are not specified.

### States and copy
- Sync failure is logged in J03 as an `int` event, and **does not change** either status (specJ1 «الفشل»: «فشل المزامنة يظهر في السجل ولا يغيّر الحالة.»).
- An agent-reported event appears with the `agt` tag and the suffix «(غير مؤكد رسمياً بعد)» until the channel confirms it.
- specJ1 «هدف المستخدم»: «متابعة الإجراء الخارجي بدقة دون خلطه بحالة المنصة.» specJ1 «المصادر»: «كل حدث موسوم: القناة المعتمدة، أبلغ بها الوكيل، داخلي.»

### Business rules
- Official status text is stored and displayed verbatim, never edited, and always shows its source and `syncedAt`.
- Platform state and official state are separate fields. The official status never drives a platform transition.
- Readiness items (J01) must all be met before export and submission (inferred from L25/f8):
  - referral decision approved by two approvals;
  - prior owner notice sent;
  - objection period elapsed with no objection;
  - export package accepted;
  - no open complaints;
  - legal mandate of the channel is valid (informational, with its expiry).
- The platform does not operate the court or the auction. Sale decisions are outside Rahoon.

### Data and API (inferred)
- `Case.state` (`external_judicial_sale`), `Case.stateChangedAt`, `Case.stateChangeKind` («قرار داخلي»).
- `ExternalReference { id, caseId, channelIntegrationId, requestNo (masked in UI), assignedAgentOrgId, assignedAgentName, lastSuccessfulSyncAt, entryMode: integrated|manual }`.
- `ExternalStatusEvent { id, caseId, occurredAt, title, text (verbatim), source: channel|agent_reported|internal, officiallyConfirmed: bool, amount?, currency?, syncedAt, rawPayload, kind: status|agent_report|sync_failure, retryInfo? }`.
  - The current official status is the latest `source=channel, kind=status` event.
- `ReadinessItem { key, label, status: met|unmet|info, evidenceDate, evidenceRef, validUntil? }`.
- Endpoints:
  - `GET /cases/{ref}/referral` (state, external status, readiness, reference)
  - `GET /cases/{ref}/external-status/events`
  - `POST /cases/{ref}/external-status/sync` (returns a job; audit-logged)
  - `GET /integrations/states` (legend)

### Integration dependency
- قناة الإحالة القضائية المعتمدة (PA18 row 1). While it is `unavailable`, the reference and status are entered manually.

---

## J02 — Case export & reconciliation (validation)

- **Artboard:** `P3-Legal-CaseExport-Desktop-Validation · 1440`. Section «J02-J04 Export exceptions». specJ2: «التجاوب: سطح المكتب فقط.»
- **Role:** القانونية (ماجد الحربي).
- **Route (inferred):** `/cases/[caseRef]/referral/export` (or `/referral/export/[packageId]`).
- **Shell:** `LenderSidebar active="cases"` (ماجد الحربي) + `LenderTopbar crumb1="RH-2026-003511" crumb2="تصدير الملف"`. **No CaseHeader.**
- **Layout:** `main` is a grid `1fr 1fr`.
  - The header row spans both columns: H2 + subtitle, with the primary button at the end.
  - Below it, the disabled-reason line.
  - Left: the package content card. Right: the field-mapping table.

### Content
- H2 «J02 تصدير الملف ومطابقته». Subtitle: «الحزمة `PKG-3511-02` · مطابقة الحقول مع مخطط القناة المعتمدة `v1.3` (افتراض)».
- **«محتوى الحزمة»** (manifest). Rows use `36px 1fr 140px`: number | title + hash (LTR) | status (check_circle + text).

| # | document | hash | status |
|---|---|---|---|
| 01 | ملخص الحالة ومسارها | sha256: 7f3a…c91e | متحقق |
| 02 | عقد التمويل وملاحقه | sha256: 19b2…04aa | متحقق |
| 03 | الصك وشهادة الرهن | sha256: a0e1…77d2 | متحقق |
| 04 | كشف المديونية المعتمد 2026-11-15 | sha256: 55d9…12f0 | متحقق |
| 05 | سجل العروض والردود | sha256: 8c47…3fb1 | متحقق |
| 06 | إثبات الإشعار المسبق | sha256: 2e6b…a803 | متحقق |

- **«مطابقة الحقول»** table (role=table, grid `1.2fr 1fr 1fr 0.8fr`). Columns: «الحقل» | «في المنصة» | «المتوقع» | «النتيجة».

| field | platform | expected | result | style |
|---|---|---|---|---|
| رقم العقد | MF-77-1205••• | MF-77-1205••• | مطابق | green text |
| هوية المالك | 1•••••••07 | 1•••••••07 | مطابق | green |
| المديونية | 684,200.00 | 684,200.00 | مطابق | green |
| رقم الصك | 4••••••31 | 4••••••13 | غير مطابق | red text, row bg #FCECEA |
| تاريخ الإشعار | 2026-10-20 | 1448-04-28 | صيغة مختلفة | red, row bg #FCECEA |
| المدينة | جدة | JED | محوّل آلياً | info blue (not blocking) |

- Result enum (inferred): `match | mismatch | format_diff | auto_converted`. Blocking results: `mismatch` and `format_diff`.

### Actions
- **«إرسال عبر القناة»** (primary): `disabled`, with `aria-describedby="exp-why"`.
  - Reason line (icon `block`): «معطّل: حقلان غير متطابقين يجب حلّهما.»
  - Enabled only when there are zero blocking mapping rows, every manifest item is «متحقق», and J01 readiness is met (the last condition is inferred).
  - Effect (inferred): send the package through the channel, set the package to `pending`, and create an ExternalStatusEvent on the response.
- No resolve UI is drawn per mismatched row (inferred need: fix the source data, or record an explicit conversion).

### Rules (specJ2)
- «التصدير: حزمة ببصمات لكل ملف ومطابقة حقول مع مخطط القناة.»
- «الحجب: الإرسال معطّل مع سبب مرئي حتى حل عدم التطابق.»

### Data and API (inferred)
- `ExportPackage { id: "PKG-3511-02", caseId, seq, channelSchemaVersion: "v1.3", status: draft|blocked|sent|pending|accepted|rejected, rejectionCode?, sentAt, acceptedAt, items[], mappings[] }`.
- `PackageItem { seq, title, documentVersionId, sha256, verification: verified|failed }`.
- `FieldMapping { field, platformValue, expectedValue, result, blocking: bool }`.
- Endpoints:
  - `POST /cases/{ref}/referral/packages` (build)
  - `GET /packages/{id}` (manifest + mappings)
  - `POST /packages/{id}/validate`
  - `POST /packages/{id}/send` (server re-checks guards; returns 409 with a reason if blocked)

---

## J04 — Integration exceptions

- **Artboard:** `P3-Legal-IntegrationExceptions-Desktop · 1440`. Desktop only.
- **Role:** القانونية (ماجد الحربي).
- **Route (inferred):** `/referrals/exceptions`, with `?selected=RH-2026-003511`. It is a cross-case queue, not a case tab.
- **Shell:** `LenderSidebar active="cases"` + `LenderTopbar crumb1="الإحالات" crumb2="الاستثناءات"`. No CaseHeader.
- **Layout:** `main` is a grid `1fr 400px`. Left: the exception list (articles). Right: the detail and resolution panel for the selected exception.

### Content
- H2 «J04 استثناءات التكامل» + «· 4 مفتوحة».
- Each exception is an article (grid `28px 1fr auto`): icon | title (bold) + ref (LTR) + description + meta (time · owner) | status tag.
  - The selected row (first) carries the orange inset mark.

| icon | type (title) | ref | description | meta | tag |
|---|---|---|---|---|---|
| sync_problem red | عدم تطابق الحالة | RH-2026-003511 | الجهة: «صدر محضر البيع» · المنصة لم تستلم التحويل | منذ يومين · ماجد الحربي | مفتوح (red) |
| error red | رفض الحزمة | RH-2026-003244 | «مستند الإشعار غير مقروء» — رمز الرفض DOC-04 | 2026-12-01 | مفتوح |
| hourglass_top amber | لا رد منذ 72 ساعة | RH-2026-003390 | الطلب مُرسل 2026-11-28 · إعادة المحاولة 3 مرات | آلي | قيد الانتظار |
| cloud_off neutral | القناة غير متاحة | — | صيانة مجدولة من الجهة 2026-12-05 00:00–04:00 | إشعار الجهة | معلومة |

- Type enum (inferred): `status_mismatch | package_rejected | no_response | channel_unavailable`. Status enum (inferred): `open | pending | info | resolved`.
- **Detail panel** (selected: status mismatch):
  - Title «عدم تطابق الحالة · RH-2026-003511».
  - Two-column compare:
    - «المنصة» → **بيع قضائي خارجي**
    - «الجهة» → **«صدر محضر البيع»**
  - Explanation: «الجهة أبلغت بنتيجة البيع، لكن المبلغ لم يصل بعد. لا تنتقل الحالة إلى «بانتظار التسوية المالية» تلقائياً.»
  - Resolution radio group. The first option is selected by default in the design.
    - «تسجيل النتيجة وانتظار التحويل» (selected)
    - «التصعيد للجهة عبر القناة»
  - Textarea (aria-label «ملاحظة»), prefilled «النتيجة مسجلة وفق المحضر؛ بانتظار إشعار التحويل.»
  - Primary button «حل الاستثناء».

### Actions and rules
- «حل الاستثناء» requires a chosen option and a note (specJ2 «القرار»: «حل الاستثناء يتطلب ملاحظة ويُسجل.»). It is audit-logged.
  - (inferred) Disable it with a reason until the note is non-empty.
- specJ2 «الاستثناءات»: «طابور بنوع الاستثناء ومرجعه ومسؤوله؛ لا انتقال تلقائي عند التعارض.»
- Conflicting official and platform states never auto-transition the case. Moving to `awaiting_reconciliation` is a separate human action after the transfer is received (inferred).
- Option semantics (inferred):
  - «تسجيل النتيجة…» records the official result (ExternalStatusEvent/SaleResult confirmed) and keeps the case waiting for the transfer.
  - «التصعيد…» sends an escalation through the channel and moves the exception to `pending`.

### Data and API (inferred)
- `IntegrationException { id, type, caseRef?, integrationId, description, detectedAt, ownerUserId | "system" | "authority", status, platformState, externalStateText, rejectionCode?, retryCount?, resolution: { action: record_and_wait|escalate, note, resolvedBy, resolvedAt } }`.
- Endpoints:
  - `GET /integration-exceptions?status=open`
  - `GET /integration-exceptions/{id}`
  - `POST /integration-exceptions/{id}/resolve { action, note }`
  - `POST /integration-exceptions/{id}/escalate`

---

## J05 + J06 — Agent portal: assigned cases · readiness & plan

- **Artboard:** `P3-Agent-CaseDetail-Desktop-Plan · 1440 (J05 + J06)`. Section «J05-J07 Agent portal». specAgent: «التجاوب: الوكيل: سطح مكتب؛ المالك: جوال أولاً.»
- **Role:** judicial sale agent («وكيل بيع قضائي»), org «مكتب الوكالة «ط»». This is an external role with its own portal shell.
- **Route (inferred):** `/agent/cases` (J05 list) and `/agent/cases/[externalRef]` (J06 detail). The list and detail render as a master-detail pair on desktop.
- **Shell:** a custom agent header, with no LenderSidebar:
  - logo `rahoon-horizontal-full.svg` (172px);
  - org label «مكتب الوكالة «ط» · وكيل بيع قضائي»;
  - nav: «الحالات المكلفة» (aria-current=page) and «المساعدة»;
  - access badge «وصول بتكليف من الجهة المختصة».
- **Layout:** `main` is a grid `360px 1fr`.
  - Left: the J05 list.
  - Right: a nested grid `1fr 340px`, holding the case detail (J06) and an access-scope aside.

### Content
- **J05 list.** H2 «J05 الحالات المكلفة». Items show external ref (LTR), title (bold) and meta. The selected item has bg `#FDF0EB` and the orange inset mark.

| ext ref | title | meta | selected |
|---|---|---|---|
| EXT-JD-2026-•••8841 | شقة — حي الصفا، جدة | تكليف 2026-11-24 · مصرف الأفق | yes |
| EXT-JD-2026-•••8790 | أرض سكنية — الخبر | تكليف 2026-11-10 · شركة السنبلة | |
| EXT-JD-2026-•••8702 | فيلا — الطائف | مكتمل · بانتظار الإغلاق | |

- Notes on the list:
  - The agent sees cases from multiple lenders (مصرف الأفق, شركة السنبلة).
  - The list is keyed by the **authority reference**, not by RH refs.
- **Detail header.** «`EXT-JD-2026-•••8841` · مرجع الجهة». H3 «شقة سكنية — حي الصفا، جدة».
- **J06 sale readiness.** Title «J06 جاهزية البيع». Items show icon + text + meta.

| icon | item | meta |
|---|---|---|
| check_circle | الحزمة المصدّرة مستلمة | 6 مستندات |
| check_circle | المعاينة | 2026-11-27 |
| check_circle | التقييم المعتمد لدى الجهة | — |
| schedule (amber) | ترتيبات إخلاء إنسانية | بالتنسيق مع الجهة |
| check_circle | الإعلان وفق إجراءات الجهة | خارج رهون |

- **«الخطة المقدمة للجهة»** (date LTR `130px` + text):
  - 2026-11-27 معاينة العقار وتوثيق حالته
  - 2026-11-28 رفع خطة البيع للجهة المختصة
  - 2026-12-01 تنفيذ البيع وفق إجراءات الجهة
  - 2026-12-10 تسليم محضر البيع والأدلة
  - Footer note: «اعتماد الخطة وقرارات البيع لدى الجهة المختصة، لا في رهون.»
- **Access aside:**
  - «ما تراه»: «العقار، المستندات المصدّرة، مرجع الجهة.»
  - «ما لا تراه»: «سجل التفاوض، الشكاوى الداخلية، بيانات حالات أخرى للمصرف.»
  - Lock notice (icon `lock_clock`): «ينتهي وصولك بانتهاء التكليف أو بإشعار من الجهة.»

### Actions
- Select a case in the list to open its detail.
- No plan-edit or approve buttons are drawn, because the plan is submitted to the authority outside Rahoon (specAgent «الخطة»: «تُرفع للجهة؛ رهون لا تعتمد قرارات البيع.»).
- (inferred) The plan is a read-only mirror, or the agent enters milestones as updates (see J07).

### Rules
- specAgent «العزل»: «الوكيل يرى الحالات المكلف بها من الجهة فقط، ومستندات الحزمة المصدّرة.»
- Server-side scoping (inferred):
  - An agent can only query cases with an active `AgentAssignment` for their org.
  - Response DTOs exclude negotiation history, internal complaints and other lender cases.
  - Access expires at the end of the assignment or when the authority revokes it.

### Data and API (inferred)
- `AgentOrg { id, name }`.
- `AgentAssignment { id, agentOrgId, caseId, externalRef, lenderOrgName, assignedAt, source: authority, status: active|completed_awaiting_close|ended, accessEndsAt? }`.
- `AgentReadinessItem { key, label, status: done|pending, meta }`.
- `SalePlanMilestone { date, text }`.
- Endpoints:
  - `GET /agent/assignments`
  - `GET /agent/assignments/{extRef}` (property, exported docs, readiness, plan)
  - `GET /agent/assignments/{extRef}/documents` (package docs only)

---

## J07 — Agent: updates, result & evidence

- **Artboard:** `P3-Agent-ResultEvidence-Desktop · 1100 (J07)`. No shell drawn; it is a content panel at 1100.
- **Role:** agent (مكتب الوكالة «ط»).
- **Route (inferred):** `/agent/cases/[externalRef]/result`.
- **Content:**
  - Header «`EXT-JD-2026-•••8841` · مكتب الوكالة «ط»», H2 «J07 التحديثات والنتيجة والأدلة».
  - **«التحديثات المرسلة»** (date LTR + text):
    - 2026-11-27 14:10 المعاينة مكتملة؛ العقار مشغول ويحتاج ترتيب إخلاء بالتنسيق.
    - 2026-11-28 09:30 رُفعت خطة البيع للجهة.
    - 2026-12-01 17:40 صدر محضر البيع — مرفق.
  - **«تسجيل النتيجة»** form (2-column grid):
    - «ثمن البيع الرسمي *»: amount input, LTR, value `760,000.00`, suffix «ر.س».
    - «تاريخ محضر البيع *»: date `2026-12-01`.
    - «تكاليف الإجراء المعلنة»: amount `22,800.00` «ر.س» (optional).
    - «الأدلة *»: attachment chip (icon `attach_file`) «محضر_البيع.pdf · إشعار_الجهة.pdf».
    - Info note (role=note): «تُعرض النتيجة للمصرف موسومة «أبلغ بها الوكيل» حتى تؤكدها القناة الرسمية. لا تؤدي إلى توزيع تلقائي.»
- **Actions:**
  - «حفظ» (secondary): saves a draft.
  - «إرسال النتيجة» (primary): submits the result.
    - Requires the sale price, minutes date and at least one evidence file (inferred from the `*` marks).
    - Creates an `agent_reported` ExternalStatusEvent that shows in J03 as «محضر البيع (غير مؤكد رسمياً بعد)».
    - Never triggers a distribution or a state change.
  - (inferred) An «add update» action is implied by «التحديثات المرسلة», but it is not drawn.
- **Rules:** specAgent «النتيجة»: «تُسجّل بأدلة وتُوسم «أبلغ بها الوكيل» حتى التأكيد الرسمي.»
- **Data and API (inferred):**
  - `AgentUpdate { id, assignmentId, at, text, attachments[] }`.
  - `SaleResult { id, caseId, assignmentId, officialSalePrice, currency: SAR, saleMinutesDate, declaredCosts?, evidence[] (documentIds), status: draft|submitted, source: agent_reported|channel, officiallyConfirmedAt? }`.
  - Endpoints:
    - `POST /agent/assignments/{extRef}/updates`
    - `PUT /agent/assignments/{extRef}/result` (draft)
    - `POST /agent/assignments/{extRef}/result/submit`
    - `POST /agent/assignments/{extRef}/evidence` (upload with virus scan)

---

## Owner mobile — Referral official status (no J-ID)

- **Artboard:** `P3-Debtor-ReferralStatus-Mobile · 390`. This is a variant of the D02 home for an owner whose case is in `external_judicial_sale`.
- **Role:** owner/debtor (فيصل ر.).
- **Route (inferred):** the owner home `/(debtor)/home`, which renders this card when the case state is external.
- **Shell:** `DebtorTop title="أهلاً فيصل" sub="حالتك مع مصرف الأفق"` + `DebtorNav active="home"`.
- **Content:**
  - Card:
    - Label «حالة الإجراء لدى الجهة المختصة».
    - Value **«قيد التنفيذ لدى الجهة المختصة»** (verbatim, in guillemets).
    - Meta «كما أبلغتنا الجهة · `2026-12-02`».
  - Paragraph: «يتابع الإجراءَ جهةٌ رسمية مختصة، ونعرض لك هنا ما تبلغنا به. سيُسدَّد التمويل من ثمن البيع، ويعود لك أي فائض.»
  - «حقوقك»:
    - «• الاطلاع على ملخص المبالغ والتسوية»
    - «• تقديم اعتراض للجهة المختصة مباشرة»
    - «• التواصل مع مسؤول حالتك في المصرف»
  - Button (secondary, 52px): «مراسلة مسؤول الحالة». It opens owner messages (D12) (inferred).
- **Rules:** specAgent «المالك»: «يرى الحالة الرسمية كما أبلغت بها الجهة، وحقوقه، بلغة هادئة.»
  - The owner sees only the official status text and its date. The owner sees no internal log, sync failures or agent-reported results (inferred from «يرى ملخصاً»).
- **API (inferred):** `GET /me/case` includes `externalStatus { text, reportedAt }` only when the state is `external_judicial_sale`.

---

## F01 + F02 — Financial reconciliation · Authorized distribution (maker-checker)

- **Artboard:** `P3-Finance-Distribution-Desktop-MakerChecker · 1440 (F01 + F02)`. Section «F01-F04 Financial».
- **Roles:**
  - Preparer: finance (ريم الدوسري, «المالية», initials «ر د»).
  - Checker: finance auditor (عبدالرحمن ش.).
  - Approver: نورة الشهري.
- **Route (inferred):** `/cases/[caseRef]/closure` (tab «الإغلاق»). Sub-views `/closure/reconciliation` and `/closure/distribution`, or a single page.
- **Shell:**
  - `LenderSidebar active="cases" user-name="ريم الدوسري" user-role="المالية" initials="ر د"`
  - `LenderTopbar crumb1="RH-2026-003511" crumb2="التسوية والتوزيع"`
  - `CaseHeader tab="closure" extra-tab="closure:الإغلاق" case-ref="RH-2026-003511" title="فيصل ر. — شقة سكنية، حي الصفا، جدة" state-key="reconciliation" sla-text="التوزيع خلال 5 أيام · 2026-12-15" sla-tone="warn" stage="6" stage-names="الجاهزية|الاعتماد|إشعار المالك|التقديم|الإجراء الخارجي|النتيجة|التسوية والإغلاق"`
- **Layout:** `main` is a grid `1fr 420px`. Left: the F01 card, then the F02 card. Right aside: the approval chain.

### F01 reconciliation
- Header «F01 المطابقة» + a badge (check_circle, green) «الفرق 0.00».
- Rows use `1.6fr 1fr 1.6fr`: label | amount (LTR) | source.

| label | amount | source | row style |
|---|---|---|---|
| ثمن البيع الرسمي | 760,000.00 | القناة المعتمدة · محضر 2026-12-01 | normal |
| تكاليف الإجراء | −22,800.00 | إشعار الجهة | normal |
| المستلم فعلياً | 737,200.00 | TRX-88601944 · 2026-12-08 | normal |
| الصافي المتوقع | 737,200.00 | الثمن − التكاليف | bg #FAF9F6, bold |
| الفرق | 0.00 | — | bg #EAF4EE, bold |

- **Formulas** (specFin «المطابقة»: «ثمن − تكاليف = صافٍ مقابل المستلم الفعلي؛ الفرق يجب أن يكون صفراً.»):
  - `expectedNet = officialSalePrice − procedureCosts` → 760,000.00 − 22,800.00 = **737,200.00**
  - `difference = actualReceived − expectedNet` → 737,200.00 − 737,200.00 = **0.00**
  - Guard: `difference == 0.00`. It is required before the distribution can be sent for review, and before closure.
  - A non-zero difference renders an error badge («فرق 1,000.00» style, as in flow f9 step 1) and blocks progress.
- Inputs and their sources:
  - Price and costs must come from **channel-confirmed** data (the `ext` source), not from agent-reported values (inferred from the J07 note).
  - Received comes from the bank transfer (TRX ref + date).

### F02 proposed distribution (waterfall)
- Title «F02 التوزيع المقترح».
- Stacked bar (role=img, aria-label «التوزيع: 684,200 للمصرف، 53,000 للمالك، من صافي 737,200»). Segments: charcoal `#22262A` at 92.8% and orange `#F4633A` at 7.2%.
- Rows use `16px 1fr 140px 1.4fr`: swatch | label | amount | basis.

| swatch | line | amount | basis |
|---|---|---|---|
| #22262A | سداد المديونية لمصرف الأفق | 684,200.00 | كشف المديونية المعتمد 2026-11-15 |
| #F4633A | الفائض للمالك | 53,000.00 | إلى الحساب المتحقق IBAN ••••4410 |
| #CBCAC6 | رسوم أخرى | 0.00 | — |

- **Waterfall** (specFin «التوزيع»: «المديونية أولاً ثم الفائض للمالك؛ رسم بنسب مع بديل نصي.»):
  - `net = actualReceived` (737,200.00; must equal expectedNet)
  - `lenderShare = min(net, approvedDebtStatementAmount)` → 684,200.00. The debt statement 2026-11-15 is the same value as J02's «المديونية» field.
  - `otherFees = Σ approved other fees` → 0.00 (ordering relative to the debt is not specified; see conflicts)
  - `ownerSurplus = net − lenderShare − otherFees` → 737,200 − 684,200 − 0 = **53,000.00**
  - Check: 684,200 + 53,000 + 0 = 737,200. Bar percentages: 684,200/737,200 = 92.81%, and 53,000/737,200 = 7.19%.
  - (inferred) If `net < debt`, the surplus is 0 and a shortfall remains (not designed).

### Approval chain (aside)
- Title «سلسلة الاعتماد». Items show icon + role: name + status/time.

| icon | role · person | status |
|---|---|---|
| check_circle green | المُعِدّة: ريم الدوسري | 2026-12-09 11:20 |
| hourglass_top amber | المدقق: عبدالرحمن ش. | بانتظار |
| radio_button_unchecked grey | المعتمد: نورة الشهري | — |

- Rule box: «الفائض للمالك يُحوَّل إلى حسابه المتحقق منه فقط، عبر القنوات النظامية، ويُسجّل مرجعه.»
- Primary button «إرسال للتدقيق».

### Actions and rules
- «إرسال للتدقيق» sends the distribution from the preparer to the checker.
  - Enabled only when the F01 difference is 0.00, and the surplus destination is a **verified** owner IBAN (inferred).
- Maker-checker (specFin «الاعتماد»): «مُعِدّة ≠ مدقق ≠ معتمد؛ رمز تحقق عند الإغلاق.»
  - The server enforces three distinct users.
  - The checker and approver each approve or return with a reason (inferred from the ApprovalChain component states: بانتظار، معتمد، مُعاد، مرفوض، مُصعّد).
- Execution records the transfer references. Example: the surplus is later executed as TRX-88622015 on 2026-12-11 (F04).
- Distribution is **never** automatic from the agent result or the official status (J07 note).

### Data and API (inferred)
- `Reconciliation { caseId, officialSalePrice, salePriceSource, procedureCosts, costsSource, actualReceived, receivedTxnRef, receivedAt, expectedNet (computed), difference (computed), status }`.
- `Distribution { id, caseId, lines[{ type: debt_repayment|owner_surplus|other_fee, amount, basisRef, destinationIbanMasked?, executedTxnRef?, executedAt? }], status: draft|in_review|in_approval|approved|executed|returned }`.
- `ApprovalStep { distributionId, role: preparer|checker|approver, userId, status: done|pending|not_started|returned|rejected, at, reason }`.
- Endpoints:
  - `GET /cases/{ref}/reconciliation`
  - `PUT /cases/{ref}/reconciliation/received` (link a bank transaction)
  - `POST /cases/{ref}/distributions` (compute the waterfall on the server)
  - `POST /distributions/{id}/submit-review`
  - `POST /distributions/{id}/check { decision, reason }`
  - `POST /distributions/{id}/approve { reason, mfaCode }`
  - `POST /distributions/{id}/execute { lines[txnRef] }`

---

## F03 + F04 — Release & clearance documents · Traceable closure

- **Artboard:** `P3-Finance-TraceableClosure-Desktop · 1440 (F03 + F04)`.
- **Role:** approver (نورة الشهري, «معتمدة», initials «ن ش»). F03 is assigned to «القانونية» in the Brief (see conflicts).
- **Route (inferred):** `/cases/[caseRef]/closure/review`.
- **Shell:** `LenderSidebar active="cases" user-name="نورة الشهري" user-role="معتمدة" initials="ن ش"` + `LenderTopbar crumb1="RH-2026-003511" crumb2="مراجعة الإغلاق"`. **No CaseHeader** (a ReviewScreen-style page).
- **Layout:** `main` is a grid `1fr 420px`. Left: H2, the trace table, the F03 docs. Right aside: the closure approval.

### F04 trace table
- H2 «F04 الإغلاق بمصادر قابلة للتتبع». Card title «تتبع كل رقم إلى مصدره».
- Rows use `1.3fr 130px 1.6fr 110px`: label | amount | source | source-type tag.

| label | amount | source | tag |
|---|---|---|---|
| ثمن البيع | 760,000.00 | محضر البيع · القناة المعتمدة 2026-12-02 | رسمي |
| تكاليف الإجراء | 22,800.00 | إشعار الجهة 2026-12-02 | رسمي |
| المستلم | 737,200.00 | TRX-88601944 · كشف 2026-12-08 | بنكي |
| المديونية المسددة | 684,200.00 | نظام التمويل · إقفال 2026-12-10 | نظام التمويل |
| الفائض المحوّل للمالك | 53,000.00 | TRX-88622015 · 2026-12-11 | بنكي |
| قرار الإحالة | — | اعتماد 2026-11-10 · موافقتان | داخلي |

- Source-type tags:
  - `official`: رسمي, bg #151513, fg #FFFFFF
  - `bank`: بنكي, #EAF4EE / #1E6A45
  - `core_banking`: نظام التمويل, #EAF2F9 / #1D5A8C
  - `internal`: داخلي, #F2F1ED / #22262A

### F03 release and clearance documents
- Title «F03 مستندات الفك والمخالصة». Items show icon + title + meta.

| icon | document | meta |
|---|---|---|
| check_circle | خطاب المخالصة النهائية | المالية · 2026-12-11 |
| check_circle | طلب فك الرهن | عبر القناة · مقبول 2026-12-12 |
| schedule amber | تأكيد فك الرهن الرسمي | قيد الانتظار — لا يحجب الإغلاق (افتراض) |
| check_circle | ملخص التوزيع للمالك | مولَّد · بالعربية |

### Closure approval (aside)
- Title «اعتماد الإغلاق». Consequences listed:
  - «• تصبح الحالة **مغلقة** نهائياً.»
  - «• يستلم المالك ملخص التوزيع والمخالصة.»
  - «• تبقى الحالة الرسمية الخارجية ظاهرة للقراءة مع آخر مزامنة.»
- Checkbox, **checked** in the design: «راجعت تتبع المصادر ولا يوجد رقم دون مصدر.»
- Textarea (aria-label «سبب القرار»), prefilled «مطابقة صفرية الفرق، والتوزيع منفذ بمراجع.»
- Primary button «اعتماد الإغلاق (رمز تحقق)», which opens the MFA/OTP step.

### Rules
- Closure is enabled only when all of these hold (inferred from the design and specFin):
  - reconciliation difference = 0.00;
  - distribution approved and executed with transfer refs;
  - every trace row has a source (the acknowledgement checkbox is ticked);
  - a reason is entered;
  - all required release documents are done, except `official_release_confirmation`, which is non-blocking (assumption);
  - the approver is distinct from the preparer and the checker;
  - an MFA code is supplied.
- specFin «التتبع»: «كل رقم بوسم مصدره: رسمي، بنكي، نظام التمويل، داخلي.»
- specFin «المستندات»: «المخالصة وطلب فك الرهن؛ التأكيد الرسمي المتأخر لا يحجب الإغلاق (افتراض).»
- After closure:
  - state is `closed` (final);
  - the owner receives the distribution summary and the clearance letter (see flow f9, D14);
  - the external status remains read-only, with its last sync.

### Data and API (inferred)
- `TraceItem { caseId, label, amount?, sourceType: official|bank|core_banking|internal, sourceDescription, sourceRef, sourceDate }`. Generated server-side from Reconciliation, Distribution and decisions. Every item must have a `sourceRef`.
- `ReleaseDocument { caseId, type: final_clearance_letter|release_request|official_release_confirmation|owner_distribution_summary, status: done|pending|rejected, issuedBy, channelRef?, date, blocksClosure: bool, language: ar }`.
- `ClosureDecision { caseId, approverId, traceAcknowledged: bool, reason, mfaVerifiedAt, closedAt }`.
- Endpoints:
  - `GET /cases/{ref}/closure/trace`
  - `GET /cases/{ref}/release-documents`
  - `POST /cases/{ref}/release-documents/{type}/generate`
  - `POST /cases/{ref}/release-request/send` (channel)
  - `POST /cases/{ref}/close { traceAcknowledged, reason, mfaCode }` (returns 409 with reasons)

---

## PA18 — Integrations & states (platform admin)

- **Artboard:** `P3-Platform-Integrations-Desktop · 1440 (PA18)`. Section «PA18 Integrations».
- **Role:** platform admin, role label «مسؤول التكاملات». The sidebar footer reads «لا وصول لبيانات الحالات دون إذن مؤقت».
- **Route (inferred):** `/platform/integrations`.
- **Shell:** `PlatformSidebar active="integrations" role="مسؤول التكاملات"` (dark sidebar; the header reads «إدارة المنصة», «{role} · وصول مقيد»). No topbar drawn.
- **Layout:** single `main`, with H2 «التكاملات», then a table, then a footnote.
- **Table** (role=table, grid `1.8fr 1fr 1.2fr 1fr 1fr 1fr`). Columns: «التكامل» | «الوضع» | «آخر نجاح» | «نسبة الفشل 7 أيام» | «المنشآت» | «المفاتيح».

| integration (name / purpose) | state | last success | fail 7d (colour) | orgs | keys (colour) |
|---|---|---|---|---|---|
| قناة الإحالة القضائية المعتمدة / تصدير الحزم + الحالة الرسمية | مفعّل | 2026-12-02 09:00 | 0.8% (green) | 1 | تُدوَّر خلال 12 يوماً (amber) |
| نظام التمويل — مصرف الأفق / استيراد المديونية يومياً | مفعّل | 2026-12-02 06:00 | 0.1% (green) | 1 | سارية (green) |
| مزود التوقيع المرخّص / اتفاقيات المالك | محاكاة | — | — | 0 | بيئة اختبار (grey) |
| بوابة الدفع المرخّصة / أقساط المالك | قيد الانتظار | — | — | 0 | بانتظار الاعتماد (amber) |
| مزود الهوية الوطني / تحقق المالك | غير متاح | — | — | 0 | — |
| مزود الرسائل النصية / الإشعارات | مفعّل | 2026-12-02 09:14 | 2.4% (amber) | 11 | سارية (green) |
| استيراد كشوف البنك / مطابقة الدفعات | فشل | 2026-11-30 22:00 | 100% منذ 2026-12-01 (red) | 2 | منتهية (red) |

- The state chip uses the legend colours (see J01 legend).
- The failure-rate colour is chosen per row by the data. A suggested rule is green < 1%, amber otherwise, red for a total failure (inferred).
- The keys cell holds a key/credential status text with a colour.
- Footnote: «محاكاة» تُستخدم للاختبار فقط ولا تنتج أي أثر قانوني أو مالي، وتوسم بها كل السجلات المرتبطة.
- **Actions:** none drawn (read-only table). (inferred) Row actions are needed for rotating keys, switching mode and viewing logs.
- **Rules:**
  - Simulated integrations tag every record they create (for example `isSimulated=true`). They never produce legal or financial effects.
  - A `failed` integration opens an exception (legend: «رفض أو خطأ؛ استثناء مفتوح»).
- **Data and API (inferred):**
  - `Integration { id, name, purpose, kind: judicial_channel|core_banking|e_signature|payment_gateway|national_id|sms|bank_statement_import, state: enabled|simulated|pending|unavailable|failed, lastSuccessAt, failureRate7d, failingSince?, orgCount, credentialStatus: valid|rotate_soon|expired|test_env|awaiting_approval|none, credentialExpiresAt? }`.
  - Endpoints:
    - `GET /platform/integrations`
    - `GET /platform/integrations/{id}/health`
    - (inferred) `PATCH /platform/integrations/{id} { state }` (audited)

---

## Integration dependencies (labels as shown)
- «قناة الإحالة القضائية المعتمدة»: J01–J04 and F03 (release request). Handles package export, official status and the release request.
- «نظام التمويل — مصرف الأفق»: the debt amount (J02 «المديونية», F02 basis, F04 «نظام التمويل · إقفال»).
- «استيراد كشوف البنك»: F01 «المستلم فعلياً», and F04 bank tags. It is currently `failed` (the manual link fallback is implied).
- «مزود الرسائل النصية»: notifications to the owner.
- «مزود الهوية الوطني»: owner verification (unavailable).
- «مزود التوقيع المرخّص» (simulated) and «بوابة الدفع المرخّصة» (pending) belong to B9 X01/X02.

## Reusable components
- **Used:** `LenderSidebar`, `LenderTopbar`, `CaseHeader` (extraTab, custom stageNames), `DebtorTop`, `DebtorNav`, `PlatformSidebar`, C01 Button (primary/secondary/disabled + `aria-describedby` reason), C06 Field (amount with «ر.س» suffix, LTR), C08 ApprovalChain, C10 AuditTimeline (J03 log), ReviewScreen (F04), C11 SystemState.
- **Introduced or specialised in B10:**
  - `DualStatusCards`: internal (light) vs official (dark, verbatim, source + synced).
  - `SourceTag`: `ext|agt|int` in J03, and `official|bank|core_banking|internal` in F04.
  - `IntegrationStateChip` + legend (`IntegrationState` in Handoff).
  - `ReadinessChecklist` (icon + text + date).
  - `PackageManifest` (seq, title, sha256, verification).
  - `FieldMappingTable` (match/mismatch/format_diff/auto_converted).
  - `ExceptionQueueItem` + `ExceptionResolvePanel` (radio + required note).
  - `AgentPortalHeader` + `AccessScopeCard` («ما تراه / ما لا تراه» + expiry).
  - `ReconciliationTable` (with computed rows and a difference badge).
  - `DistributionBar` (stacked proportional bar + text alternative) + `DistributionLines`.
  - `TraceTable`.
  - `ClosureApprovalPanel` (consequences, acknowledgement checkbox, reason, MFA button).
  - `SpecAside` (design-only annotation, not for the product).

## Conflicts and ambiguities
1. J04 header says «· 4 مفتوحة», but the tags are 2× «مفتوح», 1× «قيد الانتظار» and 1× «معلومة». Decide whether the count means "all non-resolved" or only "open".
2. The J03 log is not chronological. Its order is 11-20, 11-24, 11-26, **12-02**, 12-01, 11-29. Sort by `occurredAt` desc (or asc) in implementation.
3. The platform state is «بيع قضائي خارجي» «منذ 2026-11-18 · قرار داخلي», but the package was accepted and the request received on 2026-11-20. Confirm that the `judicial_referral → external_judicial_sale` transition is a manual internal decision, and what gates it.
4. F01/F02 (checker pending on 12-09, state `awaiting_reconciliation`) and F04 (surplus already transferred 12-11, closure pending) are different moments in time. Treat them as sequential snapshots.
5. The Brief assigns F03 to «القانونية», but it is drawn on the approver's (نورة الشهري) page. Legal likely produces the release request, and finance the clearance letter («المالية · 2026-12-11»).
6. The non-blocking «تأكيد فك الرهن الرسمي» is an assumption (افتراض). It needs legal confirmation.
7. J02 «تاريخ الإشعار» shows `2026-10-20` against `1448-04-28` with result «صيغة مختلفة». Under Umm al-Qura, 2026-10-20 ≈ 8–9 جمادى الأولى 1448 (1448-05-0x), not 1448-04-28. Anchor: 2026-09-23 = 11 ربيع الآخر 1448 per the working notes.
   - The working notes contain two anchors that disagree:
     - `2026-09-23 = 11 ربيع الآخر` (also consistent with f2's «2026-09-21 · 9 ربيع الآخر»);
     - `2026-11-01 = 10 جمادى الأولى`. Under this anchor, 1448-04-28 ≈ 2026-10-20, so the designer probably meant a format-only difference.
   - The fixture Hijri strings are internally inconsistent. Compute Hijri with an Umm al-Qura library (`islamic-umalqura`) and don't trust the fixtures.
   - The row blocks sending either way; the reason text counts two rows.
8. F02 «رسوم أخرى» is ordered after the owner surplus, yet any fees would normally be deducted before the surplus. The waterfall order for non-zero fees is unspecified. Assumed order: costs (already netted) → debt → other fees → surplus.
9. The procedure costs (22,800) are deducted before the lender, but whether the platform or the authority pays them is not stated. They are shown as already deducted from the received amount.
10. Whether F01 may use agent-reported values before channel confirmation is not stated. F01 labels the price source «القناة المعتمدة», so require confirmation (inferred).
11. There is no disabled state for «إرسال للتدقيق», «حل الاستثناء», «إرسال النتيجة» or «اعتماد الإغلاق». The guards are inferred from the spec text.
12. PA18 has no actions or edit UI. There is no detail view for keys, logs or mode switching.
13. The agent portal has no mobile design. There is also no "add update" composer, and no evidence upload flow beyond the chip.
14. The J01 readiness in B10 differs from L25 (B5), which listed «استنفاد الحلول الودية» and «عرض البيع الطوعي على المالك». Decide whether the integrated J01 extends L25's list or replaces it.
15. The F04 trace dates the sale price source «القناة المعتمدة 2026-12-02», while F01 says «محضر 2026-12-01». The minutes date and the confirmation date are different fields; store both.
