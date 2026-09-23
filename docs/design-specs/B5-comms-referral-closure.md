# B5 — Comms, Complaints, Audit, Manual Referral, Reconciliation & Closure, Bulk Import

Source: `design-source/04 Phase 1 - B5 Comms, Referral & Closure.dc.html` (condensed: `_condensed/04 Phase 1 - B5 …txt`). Page title: `04 Phase 1 — B5 Comms, referral & closure — رهون`; page H1: «المرحلة 1 — الدفعة B5: التواصل والشكاوى والسجل والإحالة والإغلاق والاستيراد».
All artboards: desktop 1440, `dir="rtl" lang="ar"`, lender shell = `LenderSidebar` (264px) + `LenderTopbar` (64px) [+ `CaseHeader` (~210px) on case tabs]. Each artboard has a 380px spec aside (`{k,v}` rows) — quoted verbatim under "Spec notes".
Design-closing note: «صُمم في B5 — L22–L26 + L04: 6 إطارات سطح مكتب (التواصل، الشكوى قيد المراجعة، السجل، جاهزية الإحالة اليدوية، المطابقة والإغلاق، الاستيراد بأخطاء الصفوف).» Only desktop frames are drawn; mobile (390) behaviour is described in spec text only.

Global conventions seen here (from brief/handoff): amounts/refs/dates in `<bdi dir="ltr">`, Western digits, Gregorian primary + Hijri alongside (A-02/A-03); masking: ID first digit + last two, name = first name + initial (A-10); icons = Material Symbols Rounded (no roof/key/gavel/lock icons). Tokens: primary btn `#AA4528`, accent `#F4633A` (current step only), success `#1E6A45/#EAF4EE`, warning `#8A5300/#FBF2DE`, error `#B3261E/#FCECEA/#EFA59C`, info `#1D5A8C/#EAF2F9`, ink `#151513`, secondary text `#5E5D58`, border `#CBCAC6`, strong border `#85847F`, warm bg `#FAF9F6`, tint `#FDF0EB/#F0B8A6`.

Shared shell props reference:
- `LenderSidebar`: `active` (portfolio|cases|tasks|approvals|complaints|reports|none), `userName` (default سارة القحطاني), `userRole` (default مديرة حالات), `initials` (default س ق), `approvals` badge. Items: المحفظة, الحالات, مهامي (badge 7), الموافقات, الشكاوى (badge 2), التقارير; footer «المساعدة والدعم», user + «جلسة آمنة · MFA»; org switcher «مصرف الأفق».
- `LenderTopbar`: `crumb1`, `crumb2`; search «ابحث بالمرجع أو رقم العقد أو المهمة» + `Ctrl K`; chip «محتوى خاص» (shield_person); `EN` toggle; bell «الإشعارات، 4 غير مقروءة».
- `CaseHeader`: `caseRef`, `title`, `stateKey`, `slaTone` (ok|warn|err|info|none), `slaText`, `stage` (0–6), `stageNames`, `tab`, `extraTab` ("key:label"). Meta line «تمويل سكني · مرابحة · مصرف الأفق» (hard-coded in design → make props), owner chip «المسؤولة: سارة القحطاني» (hard-coded → prop), button «إجراءات أخرى ▾» (menu). Stages: الاستلام | التحقق | التقييم | إعداد الحل | الموافقة الداخلية | رد المالك | التنفيذ والإغلاق (past = charcoal bar, current = orange bar + bold + `aria-current="step"`, future = grey). Tabs: نظرة عامة, الأطراف, التمويل والمديونية, العقار والرهن, المستندات, التقييم والتحليل, الحلول, المدفوعات, التواصل والمهام, السجل (+ extraTab). State chip map: awaiting بانتظار البيانات · verification تحقق · valuation تقييم · proposed حل مقترح · approval موافقة داخلية · customer بانتظار العميل · negotiation تفاوض · settlement تسوية معتمدة / نشطة · sale بيع طوعي · referral إحالة قضائية · external بيع قضائي خارجي · reconciliation بانتظار التسوية المالية · closed مغلقة · paused موقوفة. SLA tone icons: ok schedule, warn alarm, err alarm_off, info hourglass_top, none pause_circle.

---

## L22 — التواصل والمهام / Communication & tasks

1. **ID/labels**: L22; section label `L22 Comms`; artboard `P1-Lender-CaseComms-Desktop-Thread · 1440`. Brief nav path «الحالة › التواصل».
2. **Roles**: فريق الحالة (case manager, analyst, case officer). Route: `/cases/[ref]/comms`.
   Shell: `LenderSidebar active="cases"` (default user سارة القحطاني / مديرة حالات); `LenderTopbar crumb1="RH-2026-004172" crumb2="التواصل والمهام"`; `CaseHeader tab="comms" stateKey="negotiation" slaText="الرد على المالك خلال 3 أيام · 2026-10-01" slaTone="ok" stage=5` (defaults: caseRef RH-2026-004172, title «عبدالله م. — فيلا سكنية، حي النرجس، الرياض»).
3. **Layout**: `main` grid `minmax(0,1fr) 360px`. Left = thread panel (channel chips bar → message list → composer pinned at bottom of panel). Right aside = appointments card, tasks card, scheduling note. Mobile (spec only): «390: الرسائل ملء الشاشة، المهام والمواعيد في تبويب.»
4. **Content**
   - Channel chip bar (single-select pills): «مع المالك» (selected: filled ink bg, white text), «ملاحظات داخلية», «مع مقدمي الخدمة» (outlined). Trailing hint: «كل رسالة تُحفظ في سجل الحالة».
   - Message bubble (max-width 78%): header `who` (bold) + `time` (LTR bdi) + `channel` label; body `text`; optional `meta` footnote. Staff messages align start (right in RTL), bg `#FAF9F6`, border `#CBCAC6`; owner messages align end (left), bg `#FDF0EB`, border `#F0B8A6`.
   - Sample thread:
     | who | time | channel | text | meta |
     |---|---|---|---|---|
     | سارة القحطاني | 2026-09-24 09:02 | البوابة + نصية | أرسلنا لك عرضاً جديداً بقسط شهري أقل. يمكنك مراجعته والرد حتى 2026-10-03، وإن كان لديك سؤال فاكتب لنا هنا. | قالب «عرض إعادة جدولة» · قُرئت 2026-09-24 12:40 |
     | عبدالله م. | 2026-09-27 20:41 | البوابة | شكراً. أرسلت اقتراحاً بتغيير يوم القسط. هل يمكن أن يحضر ابني المكالمة؟ | — |
     | سارة القحطاني | 2026-09-28 10:12 | البوابة | بالتأكيد. اقترحنا موعد مكالمة يوم الثلاثاء 30 سبتمبر الساعة 4:30 مساءً. نحتاج فقط وكالة موثقة إن أردت أن يطّلع ابنك على التفاصيل المالية. | موعد مقترح · أكّده المالك |
     | عبدالله م. | 2026-09-28 11:03 | البوابة | الموعد مناسب. | — |
   - Composer: toolbar buttons «قالب» (icon `text_snippet`), «اقتراح موعد» (icon `event`); trailing «يُرسل عبر: البوابة + إشعار نصي»; text field placeholder «اكتب رسالة للمالك…» (min-height 52); primary «إرسال».
   - Aside card «المواعيد»: date tile (30 / سبتمبر), title «مكالمة لمناقشة العرض v3», meta «16:30 · بحضور ابن المالك · أكّدها المالك».
   - Aside card «مهام الحالة» + button «+ مهمة». Task rows (icon, title, meta; done = `task_alt` green + line-through):
     - ✓ «الرد على اقتراح المالك» — «سارة ق. · مكتملة 2026-09-28»
     - ○ «إعداد v3 (يوم 10، بدء ديسمبر)» — «فهد ع. · 2026-09-30»
     - ○ «طلب وكالة موثقة لسلمان ع.» — «خالد ز. · 2026-10-05»
   - Note (icon `schedule_send`): «الرسائل خارج أوقات التواصل المفضلة للمالك تُجدول تلقائياً لصباح يوم العمل التالي.»
5. **Actions**: switch channel (filters thread; composer target changes accordingly — internal notes never visible to owner); «قالب» opens template picker (inserts supportive-language template, records template name in meta); «اقتراح موعد» opens appointment proposal (date/time/attendees) → creates Appointment in `proposed` state; owner confirms from portal (meta «موعد مقترح · أكّده المالك»); «إرسال» sends via portal + SMS notification; «+ مهمة» creates CaseTask (title, assignee, due); toggle task done. No disabled states drawn; «إرسال» should be disabled when empty.
6. **States**: message read receipt («قُرئت <ts>»); appointment proposed/confirmed; task open/done; scheduled-send (outside preferred hours). Empty/other states not drawn.
7. **Rules**:
   - Channels are separate lanes: «المالك / داخلي / مقدمو الخدمة في مسارات منفصلة؛ لا يُمزج الداخلي بما يراه المالك.»
   - Every message is persisted to the case record/audit.
   - Owner preferred contact hours enforced automatically; out-of-hours messages queued to next business-day morning.
   - Templates must use supportive language.
   - Third party (son) access to financial details requires a notarized POA (وكالة موثقة) → task created.
   - Tasks linked to messages & appointments.
8. **Entities / API**
   - `Conversation`/`Message { id, caseId, channel: owner|internal|provider, providerId?, senderUserId|ownerId, senderRole, body, templateKey?, deliveryChannels: [portal, sms], scheduledFor?, sentAt, readAt?, appointmentId? }`
   - `Appointment { id, caseId, type: call|visit, startsAt, attendees[] (incl. third party + relation), status: proposed|confirmed|rescheduled|cancelled|done, proposedBy, confirmedAt }`
   - `CaseTask { id, caseId, title, assigneeUserId, dueDate, status: open|done, completedAt, linkedMessageId?, linkedAppointmentId? }`
   - `Owner.contactPreferences { preferredHoursFrom/To, channels }`; `MessageTemplate { key, name, body, locale }`
   - API: `GET /cases/{ref}/messages?channel=`, `POST /cases/{ref}/messages`, `GET /message-templates`, `POST /cases/{ref}/appointments`, `PATCH /appointments/{id}` (confirm/reschedule), `GET/POST /cases/{ref}/tasks`, `PATCH /tasks/{id}`.
9. **Integrations**: SMS notification («إشعار نصي») — provider not specified; treat as notification service (sandbox in dev). No labels of "manual/unavailable" on this screen.
- **Spec notes (verbatim)**: هدف المستخدم: «تواصل موثق ومحترم مع المالك، وتنسيق داخلي منفصل عنه.» · القنوات: «المالك / داخلي / مقدمو الخدمة في مسارات منفصلة؛ لا يُمزج الداخلي بما يراه المالك.» · الاحترام: «أوقات التواصل المفضلة تُحترم آلياً؛ القوالب بلغة داعمة.» · المهام: «مهام الحالة مرتبطة بالرسائل والمواعيد.» · التجاوب: «390: الرسائل ملء الشاشة، المهام والمواعيد في تبويب.»

---

## L23 — الشكاوى والاعتراضات / Complaints & appeals

1. **ID/labels**: L23; section `L23 Complaints`; artboard `P1-Lender-Complaint-Desktop-InReview · 1440`. Brief path «الحالة › الشكاوى».
2. **Roles**: مراجِع شكاوى (الامتثال) — independent of the case team; مدير الحالات sees tag only. Route: `/complaints/[cmpRef]` (sidebar «الشكاوى» section; list at `/complaints`). Case link to `/cases/[ref]`.
   Shell: `LenderSidebar active="complaints" userName="هند المطيري" userRole="الامتثال · مراجِعة شكاوى" initials="هـ م"`; `LenderTopbar crumb1="الشكاوى" crumb2="CMP-2026-0142"`. **No CaseHeader** (reviewer isn't on the case team).
3. **Layout**: `main` grid `minmax(0,1fr) 380px`. Left: header block → complaint text card → findings card → decision & response card. Right aside: impact alert (error tone), path/stepper card, other-open-complaints card. Mobile (spec): «390: النص والقرار أولاً، الأثر والمسار قابلان للطي.»
4. **Content**
   - Header meta: `CMP-2026-0142` · «الحالة» link `RH-2026-003988` · «قُدمت عبر بوابة المالك».
   - H2 (complaint subject as quote): «رُفض مستندي دون أن أفهم السبب، وأخشى أن تنتهي المهلة»
   - Chips: «قيد المراجعة» (icon manage_search); «الرد خلال يومين · 2026-09-25» (alarm, warning tone); «المراجِعة: هند المطيري (مستقلة عن فريق الحالة)» (person).
   - Card «نص الشكوى»: «أرسلت كشف الحساب مرتين، وفي كل مرة يظهر «مرفوض» فقط. لا أعرف ما المطلوب بالضبط، ومهلة الرد على العرض تقترب. أرجو التوضيح وتمديد المهلة.» Footer «منيرة ع. · 2026-09-20 22:10 · مرفقات: لا يوجد».
   - Card «ما وجدته المراجعة» (icon + text list):
     - ✓ green: «رُفض كشف الحساب مرتين بسبب صحيح (الصفحة الأولى ناقصة).»
     - ⚠ error: «رسالة الرفض للمالكة كانت «مرفوض» فقط دون السبب — خلل في القالب، أُبلغت إدارة المنصة (SUP-2026-1201).»
     - ℹ info: «مهلة العرض تنتهي بعد 5 أيام؛ التمديد مبرر.»
   - Card «القرار والرد»:
     - Decision segmented single-select: «مقبولة جزئياً» (selected: 2px ink border, tint bg, bold), «مقبولة», «غير مقبولة».
     - Checkbox (checked): «تمديد مهلة العرض 7 أيام (يتطلب موافقة مدير الحالة — أُرسلت)».
     - Response text (editable, rich/plain textarea): «نعتذر عن عدم وضوح سبب الرفض. المطلوب كشف حساب يظهر فيه اسمك ورقم الحساب في الصفحة الأولى. مددنا مهلة العرض حتى 2026-10-10. إن لم يكن الرد مرضياً، يمكنك طلب إعادة النظر أو تقديم شكوى للجهات المختصة.»
     - Buttons: primary «إرسال الرد وإغلاق المراجعة», secondary «حفظ مسودة».
   - Aside alert «أثر الشكوى المفتوحة على الحالة» (icon block, error bg): «• تحجب الإحالة القضائية والإلغاء.» «• تُوقف احتساب مهلة رد المالك.» «• تظهر لفريق الحالة كوسم دون نص الشكوى.»
   - Aside «المسار» stepper (icon, label, date): ✓ «الاستلام وإشعار المالكة» 2026-09-20 · ✓ «الإسناد لمراجِعة مستقلة» 2026-09-21 · ● (orange, current) «المراجعة والقرار» «الآن» · ○ «الرد المكتوب للمالكة» — · ○ «إغلاق أو تصعيد» —.
   - Aside «شكاوى مفتوحة أخرى»: `CMP-2026-0139` «اعتراض على مبلغ · 4 أيام».
5. **Actions**: choose decision (required); toggle offer-deadline extension (creates an approval request to case manager — «أُرسلت»); edit response; «حفظ مسودة» saves draft; «إرسال الرد وإغلاق المراجعة» sends written response to owner (portal) and moves complaint to closed (or «بانتظار المالك» if awaiting owner). Case link opens case. Other complaint link opens it. Enable «إرسال…» only when decision selected + response non-empty (implied).
6. **States** (spec): «مستلمة، قيد المراجعة، بانتظار المالك، مغلقة، مصعّدة، متأخرة.» Drawn: قيد المراجعة.
7. **Rules**:
   - Reviewer from Compliance, not case team; case team sees a tag only (no complaint text).
   - Open complaint → blocks judicial referral and cancellation; pauses owner-response SLA clock.
   - Decision ∈ {مقبولة, مقبولة جزئياً, غير مقبولة} with corrective actions and escalation path included in the response.
   - Response must mention right to reconsideration/regulator complaint.
   - Offer-deadline extension requires case-manager approval (maker-checker).
   - Written response SLA: shown «الرد خلال يومين · 2026-09-25» (filed 2026-09-20); debtor side promises 5 business days (D13).
   - Findings may spawn platform support ticket (SUP-xxxx).
8. **Entities / API**
   - `Complaint { id, ref "CMP-YYYY-NNNN", caseId, type: complaint|objection, subject, body, submittedBy (ownerId), submittedVia: owner_portal|…, submittedAt, attachments[], status: received|in_review|awaiting_owner|closed|escalated|overdue, reviewerUserId, dueAt, decision: accepted|partially_accepted|rejected, responseText, responseSentAt, draft, slaPausedCaseClock: bool }`
   - `ComplaintFinding { complaintId, severity: ok|issue|info, text, linkedTicketRef? }`; `ComplaintEvent` (path steps with dates).
   - `DeadlineExtensionRequest { caseId, offerVersionId, days: 7, newDeadline, requestedBy, approverRole: case_manager, status }`.
   - API: `GET /complaints?status=`, `GET /complaints/{ref}`, `POST /complaints/{ref}/findings`, `PUT /complaints/{ref}/draft`, `POST /complaints/{ref}/decision` (decision, response, extension), `POST /complaints/{ref}/escalate`; case-level flag `GET /cases/{ref}` → `openComplaintsCount` (tag only).
9. **Integrations**: none; regulator complaints are external (text only).
- **Spec notes (verbatim)**: هدف المستخدم: «مراجعة مستقلة للشكوى برد مكتوب ومهلة واضحة.» · الاستقلال: «المراجِع من الامتثال وليس من فريق الحالة. فريق الحالة يرى وسماً فقط.» · الأثر: «الشكوى المفتوحة تحجب الإحالة والإلغاء وتوقف مهلة رد المالك.» · القرار: «مقبولة / جزئياً / غير مقبولة، مع إجراءات تصحيحية ومسار التصعيد في الرد.» · الحالات: «مستلمة، قيد المراجعة، بانتظار المالك، مغلقة، مصعّدة، متأخرة.» · التجاوب: «390: النص والقرار أولاً، الأثر والمسار قابلان للطي.»

---

## L24 — سجل التدقيق للحالة / Case audit log

1. **ID/labels**: L24; section `L24 Audit`; artboard `P1-Lender-CaseAudit-Desktop-Filtered · 1440`. Path «الحالة › السجل».
2. **Roles**: everyone on case team (read); المدقق (auditor) exports. Route `/cases/[ref]/audit`.
   Shell: `LenderSidebar active="cases"`; `LenderTopbar crumb1="RH-2026-004172" crumb2="السجل"`; `CaseHeader tab="audit" stateKey="settlement" slaText="القسط 3 مستحق 2027-02-10" slaTone="ok" stage=6`.
3. **Layout**: full-width `main`: filter bar → table (div grid `role=table`, columns `160px 1.6fr 1.2fr 1.8fr 1fr`) → integrity footer. Mobile (spec): «390: بطاقة لكل حدث؛ المرشحات في لوحة سفلية.»
4. **Content**
   - Filter bar: applied chip «النوع: انتقالات، موافقات، كشف بيانات» with remove ✕ (`aria-label="إزالة"`); dropdown chips «الفاعل ▾», «الفترة ▾»; button «تصدير موقّع (CSV + بصمة)» (icon download).
   - Table `aria-label="سجل التدقيق"`, columns: «الوقت» (Gregorian `YYYY-MM-DD HH:mm` LTR + Hijri line) · «الحدث» (icon colored by type + label) · «الفاعل» · «التفاصيل والسبب» · «الأدلة» (link).
     | الوقت | Hijri | icon/tone | الحدث | الفاعل | التفاصيل والسبب | الأدلة |
     |---|---|---|---|---|---|---|
     | 2026-10-02 14:21 | 20 ربيع الآخر | task_alt green | قبول المالك للعرض v3 | عبدالله م. · المالك | رمز تحقق · iPhone · IP مخفي | سجل الموافقة |
     | 2026-10-01 11:40 | 19 ربيع الآخر | approval green | اعتماد الحل v3 | نورة الشهري · معتمدة | «طلب معقول لا يغيّر المبلغ» | v3 مقفل |
     | 2026-09-28 10:05 | 16 ربيع الآخر | swap_horiz info | بانتظار العميل ← تفاوض | النظام (اقتراح من المالك) | طلب تغيير يوم القسط وتاريخ البدء | الطلب |
     | 2026-09-24 09:00 | 12 ربيع الآخر | approval green | اعتماد الحل v2 وإرساله | نورة الشهري · معتمدة | «قابل للسداد وضمن حدودي» | v2 مقفل |
     | 2026-09-23 10:12 | 11 ربيع الآخر | swap_horiz info | حل مقترح ← موافقة داخلية | سارة القحطاني · مديرة حالات | «مكتمل وفق القائمة» | 4 مرفقات |
     | 2026-09-18 09:30 | 6 ربيع الآخر | block error | انتقال محجوب: تقييم ← حل مقترح | فهد العتيبي | المانع: كشف الراتب v1 مرفوض | — |
     | 2026-09-02 11:20 | 20 ربيع الأول | visibility warning | كشف رقم الهوية كاملاً | سارة القحطاني | «مطابقة مع الصك» · 60 ثانية | — |
   - Footer (icon verified): «السجل للإضافة فقط. سلسلة البصمة سليمة حتى 2026-10-02 14:21 · الحدث #218».
5. **Actions**: filter by type (multi: انتقالات، موافقات، كشف بيانات, …), actor, period; remove filter chip; evidence link opens artifact (consent record, locked version, request, attachments); «تصدير موقّع (CSV + بصمة)» — auditor role only (hide/disable for others).
6. **States**: filtered (drawn); empty/loading not drawn. Event types imply tones: success (approval/acceptance), info (transition), error (blocked), warning (sensitive data reveal).
7. **Rules**: append-only; hash chain with integrity status (last verified event # and timestamp); export is signed (CSV + hash); records blocked attempts with blocker reason; data reveal events include reason + duration («60 ثانية»); consent events record OTP method, device, masked IP; timestamps Gregorian + Hijri; actor + role.
8. **Entities / API**
   - `AuditEvent { seq (#218), caseId, occurredAt, type: transition|transition_blocked|approval|consent|data_reveal|message|document|…, fromState?, toState?, actorType: user|owner|system, actorId, actorRole, detail, reason, evidenceRefs[], metadata {device, ipMasked, otp, revealDurationSec}, prevHash, hash }`
   - API: `GET /cases/{ref}/audit?types=&actor=&from=&to=`, `GET /cases/{ref}/audit/integrity`, `POST /cases/{ref}/audit/export` (auditor; returns CSV + signature/hash manifest).
9. **Integrations**: none external. Hash chain + signing are internal.
- **Spec notes (verbatim)**: هدف المستخدم: «إثبات من فعل ماذا ومتى ولماذا، بما فيها المحاولات المحجوبة.» · البيانات: «وقت ميلادي + هجري، الفاعل ودوره، السبب، الأدلة، والمحجوب مع مانعه.» · السلامة: «للإضافة فقط مع سلسلة بصمة؛ التصدير موقّع.» · الصلاحية: «الجميع في فريق الحالة يقرأ؛ المدقق يصدّر.» · التجاوب: «390: بطاقة لكل حدث؛ المرشحات في لوحة سفلية.»

---

## L25 — حزمة جاهزية الإحالة القضائية اليدوية والمرجع الخارجي / Manual judicial referral pack & external reference

1. **ID/labels**: L25; section `L25 Referral`; artboard `P1-Legal-ReferralReadiness-Desktop-Default · 1440`. Path «الحالة › الإحالة».
2. **Roles**: القانونية (legal). Approver for the referral decision (role not named on screen; «طلب اعتماد»). Route `/cases/[ref]/referral`.
   Shell: `LenderSidebar active="cases" userName="ماجد الحربي" userRole="القانونية" initials="م ح"`; `LenderTopbar crumb1="RH-2026-003511" crumb2="جاهزية الإحالة"`; `CaseHeader tab="referral" extraTab="referral:الإحالة" caseRef="RH-2026-003511" title="فيصل ر. — شقة سكنية، حي الصفا، جدة" stateKey="proposed" slaText="لا مهلة خدمة — قرار قانوني" slaTone="none" stage=3`.
3. **Layout**: `main` grid `minmax(0,1fr) 400px`. Left: info note → readiness checklist card → evidence pack index card. Right aside: next-action card (blocked) → external reference card. Desktop only for preparation; mobile read-only (spec).
4. **Content**
   - Note (`role="note"`, icon info): «رهون لا تُحيل ولا تدير البيع القضائي. هذه الحزمة تجهّز ملفاً موثقاً يُقدَّم يدوياً للجهة المختصة، ثم يُسجَّل مرجعها هنا. **المرحلة 1: يدوي بالكامل.**»
   - Card «قائمة الجاهزية» + counter «6 من 8 مكتملة». Rows (grid `24px 1fr auto`: status icon, title + meta, action link):
     | ✓/✗ | البند | التفاصيل | الإجراء |
     |---|---|---|---|
     | ✓ | استنفاد الحلول الودية | 3 عروض (v1–v3) بين 2025-11 و2026-06 · رُفض اثنان، ولم يُرد على الثالث | عرض |
     | ✓ | عرض البيع الطوعي على المالك | عُرض 2026-07-02 · رفضه المالك كتابياً | عرض |
     | ✓ | لا شكاوى أو اعتراضات مفتوحة | آخر شكوى أُغلقت 2026-05-14 | السجل |
     | ✓ | المستندات الأساسية صالحة | العقد، الصك، الرهن، كشف المديونية (2026-09-22) | الفهرس |
     | ✓ | مراجعة الرهن والقيود | ماجد الحربي · 2026-09-15 | المذكرة |
     | ✓ | مطابقة المديونية مع نظام التمويل | الفرق 0.00 ر.س | المطابقة |
     | ✗ (red) | إشعار المالك المسبق بالقرار وحقوقه | لم يُرسل · قالب «إشعار قبل الإحالة» جاهز | إرسال… |
     | ✗ (red) | انقضاء مهلة الاعتراض | تبدأ بعد الإشعار · 15 يوماً (افتراض) | — |
   - Card «فهرس حزمة الأدلة» + disabled button «تصدير الحزمة (بعد الاعتماد)». Index rows (`n` LTR 36px, title, meta):
     01 «ملخص الحالة ومسارها الزمني» — «مولَّد · PDF»; 02 «عقد التمويل وملاحقه» — v1; 03 «صك الملكية وشهادة الرهن» — v1; 04 «كشف المديونية المعتمد» — 2026-09-22; 05 «سجل العروض والردود» — «3 عروض»; 06 «سجل التواصل والإشعارات» — «41 رسالة».
   - Aside next-action card: eyebrow «الإجراء التالي · لك»; title «طلب اعتماد قرار الإحالة»; «محجوب حتى يكتمل بندان:» + two ✗ items «إشعار المالك المسبق بالقرار وحقوقه», «انقضاء مهلة الاعتراض (15 يوماً — افتراض)»; disabled button «طلب اعتماد الإحالة…» (grey `#E4E3DF`).
   - Aside card «المرجع الخارجي»: «يُدخل يدوياً بعد التقديم للجهة المختصة. غير متاح قبل اعتماد القرار.»; field «رقم الطلب لدى الجهة المختصة» (text, LTR, disabled, placeholder «—»); field «الحالة الرسمية (كما أبلغتها الجهة)» — value «غير متاحة · إدخال يدوي»; note «الحالة الرسمية الخارجية تُعرض منفصلة دائماً عن حالة المنصة، مع مصدرها ووقت إدخالها.»
5. **Actions**:
   - Checklist links: «عرض» (offers / voluntary-sale offer), «السجل» (audit/complaints), «الفهرس» (docs index), «المذكرة» (legal memo), «المطابقة» (debt reconciliation), «إرسال…» (opens send pre-referral notice using template «إشعار قبل الإحالة» → review → send; starts objection-period timer).
   - «طلب اعتماد الإحالة…» disabled until all 8 items ✓; when enabled opens request dialog (reason, sends to approver).
   - «تصدير الحزمة (بعد الاعتماد)» disabled until referral decision approved; exports numbered pack (PDF bundle).
   - External reference inputs disabled until approval; then manual entry of request number + official status (free text as reported) — stored verbatim with source + entered-at.
6. **States**: drawn = «Default» (6/8, blocked). Implied: all met → CTA enabled; pending approval; approved → export + external-ref enabled; reference entered (shows source + entry time, separate from platform state chip); blocked by open complaint (item «لا شكاوى أو اعتراضات مفتوحة» ✗).
7. **Rules / guards**:
   - Referral is a separate decision; never auto-opened after rejection or breach (also brief: «رفض التسوية لا يعني الإحالة القضائية تلقائياً»).
   - Blockers: open complaint/objection; active offer; incomplete pack; owner not notified; objection period not elapsed (15 days after notice — assumption, configurable).
   - Readiness items (8) as above; count shown "N من 8 مكتملة".
   - External reference: manual in Phase 1, stored verbatim, with source and entry time, always displayed separately from platform state.
   - Tone: no gavel/auction imagery; procedural language.
   - Maker-checker: legal prepares, separate approver approves decision.
8. **Entities / API**
   - `ReferralReadiness { caseId, items[{ key: amicable_exhausted|voluntary_sale_offered|no_open_complaints|core_docs_valid|lien_review|debt_reconciled|owner_notified|objection_period_elapsed, status: met|unmet, detail, evidenceLink, computedAt }], metCount }` (mostly computed server-side).
   - `PreReferralNotice { caseId, templateKey, sentAt, channels, objectionPeriodDays (15), objectionEndsAt }`
   - `ReferralDecision { caseId, requestedBy, reason, status: draft|pending_approval|approved|rejected, approverUserId, decidedAt }`
   - `EvidencePack { caseId, items[{ seq "01", title, sourceType, version/meta }], exportedAt, fileHash }`
   - `ExternalReference { caseId, authority?, requestNumber (verbatim string), officialStatusText (verbatim), source, enteredBy, enteredAt, history[] }`
   - API: `GET /cases/{ref}/referral/readiness`, `POST /cases/{ref}/referral/notice`, `POST /cases/{ref}/referral/decision`, `POST /referral-decisions/{id}/approve|reject`, `GET /cases/{ref}/referral/pack`, `POST /cases/{ref}/referral/pack/export`, `POST/GET /cases/{ref}/referral/external-reference`.
9. **Integrations**: Judicial/competent authority — **manual** («المرحلة 1: يدوي بالكامل», «إدخال يدوي»); Phase 3 J01–J04 integrate later. Core financing system — used for debt reconciliation («مطابقة المديونية مع نظام التمويل»).
- **Spec notes (verbatim)**: هدف المستخدم: «تجهيز ملف إحالة مكتمل وعادل، دون أن تبدو المنصة كجهة قضائية.» · القاعدة: «الإحالة قرار منفصل: لا تُفتح تلقائياً بعد الرفض أو الإخلال.» · الحواجز: «شكوى مفتوحة، عرض قائم، نقص في الحزمة، عدم إشعار المالك أو عدم انقضاء مهلة الاعتراض.» · المرجع الخارجي: «يدوي في المرحلة 1، مع المصدر ووقت الإدخال، ومنفصل عن حالة المنصة.» · النبرة: «لا صور مطارق أو مزادات؛ لغة إجرائية.» · التجاوب: «سطح المكتب فقط للإعداد؛ الجوال قراءة.»

---

## L26 — التسوية المالية اليدوية والإغلاق / Manual reconciliation & closure

1. **ID/labels**: L26; section `L26 Closure`; artboard `P1-Finance-ReconcileClose-Desktop-Review · 1440`. Path «الحالة › الإغلاق».
2. **Roles**: المالية (preparer), مدقق مالي (reviewer), المعتمد (approver). Route `/cases/[ref]/closure`.
   Shell: `LenderSidebar active="cases" userName="ريم الدوسري" userRole="المالية" initials="ر د"`; `LenderTopbar crumb1="RH-2026-003702" crumb2="التسوية والإغلاق"`; `CaseHeader tab="closure" extraTab="closure:الإغلاق" caseRef="RH-2026-003702" title="تركي ب. — منزل، حي المنسك، أبها" stateKey="reconciliation" slaText="متأخر يوم · 2026-09-22" slaTone="err" stage=6`.
3. **Layout**: `main` grid `minmax(0,1fr) 400px`. Left: reconciliation card (table) → closure documents card. Right aside: «مراجعة قبل الإغلاق» review panel with note + approvals + submit. Desktop for preparation; approver can review on mobile (spec).
4. **Content**
   - Card header «المطابقة المالية» + sub «تسوية نقدية مخفضة · اتفاق AGR-2026-003702-01».
   - Table columns (`minmax(0,1.6fr) 1fr 1.4fr`): «البند» · «المبلغ (ر.س)» (LTR) · «المصدر».
     | البند | المبلغ | المصدر | style |
     |---|---|---|---|
     | المبلغ المتفق عليه في التسوية | 455,210.75 | الاتفاق AGR-2026-003702-01 | normal |
     | المستلم — تحويل 1 | 300,000.00 | TRX-88201744 · مطابق | normal |
     | المستلم — تحويل 2 | 155,210.75 | TRX-88355102 · مطابق | normal |
     | إجمالي المستلم | 455,210.75 | مجموع المراجع | bold, warm bg |
     | الفرق | 0.00 | — | bold, green text, success bg |
     | المبلغ المتنازل عنه (معتمد) | 38,204.10 | قرار 2026-06-11 · نورة الشهري | secondary text |
     | تحديث نظام التمويل | مغلق | مزامنة 2026-09-21 16:00 | green text |
     Calculations: `إجمالي المستلم = Σ transfers`; `الفرق = المتفق عليه − إجمالي المستلم` (must be 0.00 or explained).
   - Card «مستندات الإغلاق» (icon, title, meta):
     ✓ «خطاب المخالصة النهائية» — «أعدّته المالية · 2026-09-21»; ✓ «خطاب فك الرهن» — «القانونية · 2026-09-22»; ⏱ (warning) «إثبات تقديم طلب فك الرهن للجهة المختصة» — «مرجع خارجي يُدخل يدوياً — لا يحجب الإغلاق (افتراض)»; ✓ «ملخص الحالة النهائي للمالك» — «مولَّد · بالعربية».
   - Aside «مراجعة قبل الإغلاق» / «ما سيحدث:»: «• تنتقل الحالة إلى **مغلقة** ولا يمكن تعديلها.» «• يستلم المالك المخالصة وخطاب فك الرهن في بوابته.» «• يبقى وصول المالك للقراءة 90 يوماً ثم يُسحب (افتراض).» «• تُغلق كل المهام المفتوحة (0 حالياً).»
   - Field «ملاحظة الإغلاق *» (textarea, required), sample «المطابقة صفرية الفرق. المستندات مكتملة.»
   - «الموافقات المطلوبة»: ✓ «المطابقة: ريم الدوسري (مُعِدّة)» · ⏳ «التدقيق: عبدالرحمن ش. (المالية)» · ○ «الاعتماد: نورة الشهري».
   - Primary «إرسال للتدقيق والاعتماد».
5. **Actions**: add/match received transfers (implied; each amount must carry a source ref); attach/generate closure docs; enter external release-submission reference (manual); write closure note (required); «إرسال للتدقيق والاعتماد» → review chain (reviewer then approver); final approval executes closure (state → closed, docs published to owner portal, owner read-only access window starts, open tasks closed). Disabled when: difference ≠ 0 without explanation, required docs missing, note empty (implied).
6. **States**: reconciliation in-progress / zero-diff / non-zero diff (needs explanation, blocks closure) / submitted for review / approved / closed. Drawn: zero-diff, preparer done, review pending.
7. **Rules**: every amount has a traceable source; any difference blocks closure and requires explanation; approvals: «مُعِدّة ≠ مدقق ≠ معتمد» (three distinct users); waived amount only if previously approved (shows decision date + approver); closed case is immutable; owner gets clearance + lien release + final summary in portal; owner read access 90 days then revoked (assumption); proof of release submission to authority is manual external ref and does not block closure (assumption); payments recorded manually (A-04).
8. **Entities / API**
   - `Reconciliation { caseId, agreementRef, solutionType: discounted_cash_settlement|…, agreedAmount, receipts[{ label, amount, bankRef "TRX-…", matchStatus }], totalReceived, difference, differenceExplanation?, waivedAmount, waiverDecisionRef, coreSystemStatus, coreSyncedAt }`
   - `ClosureDocument { caseId, type: final_clearance|lien_release_letter|lien_release_submission_proof|owner_final_summary, status: ready|pending, preparedByDept, date, fileId, externalRef?, blocksClosure: bool, visibleToOwner: bool }`
   - `ClosureRequest { caseId, note, preparerId, reviewerId, approverId, steps[{role, userId, status: done|pending|waiting, at}], status }`; `OwnerAccessGrant { ownerId, caseId, mode: read_only, expiresAt }`
   - API: `GET /cases/{ref}/reconciliation`, `POST /cases/{ref}/reconciliation/receipts`, `PUT /cases/{ref}/reconciliation/explanation`, `GET/POST /cases/{ref}/closure/documents`, `POST /cases/{ref}/closure/submit`, `POST /closure-requests/{id}/review|approve|reject`.
9. **Integrations**: core financing system sync («تحديث نظام التمويل · مغلق · مزامنة …») — label implies an automated sync, but Phase 1 financial integration is manual (A-04) → treat as manual status entry or sandbox connector; authority lien-release submission — **manual** external ref.
- **Spec notes (verbatim)**: هدف المستخدم: «إغلاق بمطابقة صفرية الفرق ومستندات مكتملة ومصادر قابلة للتتبع.» · المطابقة: «كل مبلغ بمصدره؛ أي فرق يمنع الإغلاق ويتطلب تفسيراً.» · الموافقات: «مُعِدّة ≠ مدقق ≠ معتمد.» · ما يستلمه المالك: «المخالصة وفك الرهن وملخص نهائي في بوابته.» · التجاوب: «سطح المكتب للإعداد؛ المعتمد يستطيع المراجعة على الجوال.»

---

## L04 — الاستيراد الجماعي وأخطاء الصفوف / Bulk import with row errors

1. **ID/labels**: L04; section `L04 Bulk import`; artboard `P1-Lender-BulkImport-Desktop-RowErrors · 1440`. Path «الحالات › استيراد».
2. **Roles**: مدير الحالات. Route `/cases/import` (wizard steps 1 upload / 2 validate / 3 import-result).
   Shell: `LenderSidebar active="cases"` (default user سارة القحطاني); `LenderTopbar crumb1="الحالات" crumb2="استيراد ملف"`. No CaseHeader.
3. **Layout**: single-column `main`: title row → file card → 3 KPI tiles (grid `repeat(3,minmax(0,1fr))`) → attention table (`70px 1.3fr 1.3fr 2.2fr 1.2fr`) → footer action bar. Desktop only; mobile shows import status only.
4. **Content**
   - H2 «استيراد الحالات» + step «الخطوة 2 من 3 · التحقق».
   - File card (icon table_view): «محفظة_سبتمبر_2026.xlsx»; «250 صفاً · قالب v3 · رفعته سارة القحطاني 11:20»; button «استبدال الملف».
   - Tiles: ✓ green «231» «صف جاهز»; content_copy warning «12» «تكرار محتمل · يحتاج قراراً»; error red «7» «أخطاء · لن تُستورد».
   - Table `aria-label="الصفوف التي تحتاج انتباهاً"`, columns «الصف» · «رقم العقد» (masked, LTR) · «الحقل» · «المشكلة وطريقة الإصلاح» (icon + text) · «الإجراء» (link):
     | الصف | رقم العقد | الحقل | type | المشكلة وطريقة الإصلاح | الإجراء |
     |---|---|---|---|---|---|
     | 17 | MF-91-0042••• | رقم الهوية | error | 9 أرقام؛ يجب 10. صحّح في الملف | تفاصيل |
     | 38 | MF-87-2215••• | المبلغ القائم | error | نص «1.2 مليون» بدل رقم؛ استخدم 1200000.00 | تفاصيل |
     | 52 | MF-88-3317••• | رقم العقد | duplicate | مرتبط بحالة مفتوحة RH-2026-004172 | تخطي / ربط |
     | 61 | MF-90-1180••• | تاريخ أول تأخر | error | تاريخ مستقبلي 2027-01-01 | تفاصيل |
     | 88 | MF-86-7730••• | رقم الهوية | duplicate | نفس المالك في حالة مغلقة 2025-10 | إنشاء مع سبب |
     | 102 | MF-92-0019••• | المنطقة | error | قيمة غير معروفة «Riyad»؛ اختر من القائمة | تفاصيل |
     | 144 | MF-89-4471••• | الجوال | error | صيغة غير صحيحة؛ يبدأ بـ 05 | تفاصيل |
     (Design shows 7 of 19 attention rows — list needs paging/filter.)
   - Footer: button «تنزيل ملف الأخطاء» (icon download); note «الحالات المستوردة تبدأ «مسودة» ولا يُرسل أي تواصل للمالك»; primary «استيراد 231 صفاً».
5. **Actions**: replace file (back to validation); «تفاصيل» opens row error detail; duplicate decisions: «تخطي» / «ربط» (link to existing open case) or «إنشاء مع سبب» (create anyway, reason required); download error file (original rows + error column); «استيراد N صفاً» imports ready rows only (N = ready count, updates as duplicates are resolved to create/link — implied) → step 3.
6. **States**: drawn = step 2 «RowErrors». Implied: step 1 upload (template download, file pick), validating/progress, all-clean (no attention table), importing, step 3 result; mobile = status only. Row types: ready (not listed), duplicate (warning `content_copy`), error (red `error`).
7. **Rules (validation)**: national ID exactly 10 digits; outstanding amount numeric decimal (no text like «1.2 مليون»); contract number matching an open case = duplicate needing decision (skip/link); first-delinquency date not in future; region from controlled list (no free text e.g. «Riyad»); mobile must start with `05` (Saudi format); same owner ID in closed case = possible duplicate → create with reason; template version tracked (v3); error rows never imported; valid rows not lost; imported cases start in «مسودة» (draft) and **no owner communication is sent**.
8. **Entities / API**
   - `ImportBatch { id, fileName, templateVersion, uploadedBy, uploadedAt, totalRows, readyCount, duplicateCount, errorCount, status: uploaded|validated|importing|completed|failed, step }`
   - `ImportRow { batchId, rowNumber, contractNumberMasked, rawData, status: ready|duplicate|error, issues[{ field, code, message, fix }], duplicateOfCaseRef?, decision: skip|link|create_with_reason, decisionReason? }`
   - API: `POST /imports` (multipart upload), `GET /imports/{id}` (summary), `GET /imports/{id}/rows?status=`, `PATCH /imports/{id}/rows/{n}/decision`, `GET /imports/{id}/errors.xlsx`, `PUT /imports/{id}/file` (replace), `POST /imports/{id}/commit` → creates cases in `draft`.
9. **Integrations**: none (file upload XLSX, template v3).
- **Spec notes (verbatim)**: هدف المستخدم: «استيراد دفعة كبيرة بثقة مع إصلاح الأخطاء دون فقدان الصفوف السليمة.» · التصنيف: «جاهز / تكرار محتمل (قرار) / خطأ (لا يُستورد). كل خطأ بطريقة إصلاح.» · الإجراءات: «تنزيل ملف الأخطاء، استيراد الجاهز فقط، قرار لكل تكرار.» · الأمان: «الحالات المستوردة «مسودة» ولا تواصل مع المالك.» · التجاوب: «سطح المكتب فقط؛ الجوال يعرض حالة الاستيراد.»

---

## Reusable components (B5)

| Component | Props / variants | Used in |
|---|---|---|
| `LenderSidebar`, `LenderTopbar`, `CaseHeader` | see top | all |
| `ChannelChips` (pill single-select) | options, value; selected = filled ink | L22 |
| `MessageBubble` | who, time, channel, text, meta, side: staff\|owner (align + tint) | L22 (same pattern in D12) |
| `Composer` | placeholder, deliveryHint, tools [template, appointment], onSend | L22, D12 |
| `AppointmentCard` / date tile | day, month, title, time, attendees, status | L22, D12 |
| `TaskList` + `TaskRow` | icon/status open\|done (line-through), title, meta (assignee · date), add button | L22 |
| `InfoNote` | icon, text, tone neutral\|info\|error | L22, L25, L23 |
| `StatusChip` (C02) | icon, label, tone ok\|warn\|err\|info\|none | L23, CaseHeader |
| `SegmentedChoice` (card radio) | options, value | L23 (and D13) |
| `Checkbox` custom 20px | checked, label | L23 |
| `FindingList` | items {icon, tone, text} | L23 |
| `ImpactAlert` | title, bullet lines, tone error | L23 |
| `Stepper` vertical (path) | steps {status done\|current\|todo, label, date} | L23 (D03 variant) |
| `FilterBar` (C12) | applied chips w/ remove, dropdown chips, action button | L24 |
| `DataGrid` (div role=table) | columns w/ widths, rows, cell renderers (bdi LTR, icon+text, link) | L24, L25, L26, L04 |
| `AuditRow` (C10) | time + hijri, type icon/tone, event, actor, detail, evidence link | L24 |
| `IntegrityFooter` | lastVerifiedAt, eventSeq | L24 |
| `ChecklistCard` | title, counter "N من M مكتملة", items {met, title, meta, actionLabel} | L25 |
| `PackIndex` | items {n, title, meta}, export button (disabled w/ reason label) | L25 |
| `NextActionCard` (C04) blocked variant | eyebrow, title, blockers[], disabled CTA | L25 |
| `ExternalReferenceCard` | disabled until approved; requestNumber, officialStatus (verbatim), source, enteredAt | L25 |
| `ReconciliationTable` | rows {label, amount, source, emphasis: normal\|total\|diff\|muted\|status} | L26 |
| `DocStatusList` | {icon/status ready\|pending, title, meta} | L26, D04, D14 |
| `ApprovalChain` (C08) | steps {role, user, status done\|in_progress\|pending} | L26 |
| `ReviewBeforeCommitPanel` | "ما سيحدث" bullets, required note, approvals, CTA | L26 |
| `KpiTile` | icon, tone, value, label | L04 |
| `WizardStepLabel` | "الخطوة N من M · label" | L04 |
| `FileCard` | name, rows, template, uploader, time, replace action | L04 |

## Conflicts / ambiguities (B5)

1. **Weekday mismatch**: L22 message «يوم الثلاثاء 30 سبتمبر» — 2026-09-30 is a Wednesday (Tuesday = 09-29). D02/D12 also say «الثلاثاء 4:30 م» / 30 سبتمبر. Fix date or weekday in seed data.
2. **L23 SLA**: complaint filed Sun 2026-09-20 22:10, due 2026-09-25 (a Friday, Saudi weekend); debtor copy (D13) promises «خلال 5 أيام عمل». Define whether SLA is calendar or business days and weekend handling.
3. **L23 placement**: brief path «الحالة › الشكاوى» vs design as standalone complaints module (sidebar «الشكاوى», no CaseHeader, reviewer outside case team). Recommend `/complaints/[ref]` + case-level tag only; no «الشكاوى» case tab exists in CaseHeader.
4. **L25 CaseHeader state** `stateKey="proposed"` («حل مقترح», stage 3) on a referral-readiness case where offers are exhausted; unclear which platform state precedes `referral`. Also referral/closure are `extraTab`s, not in the standard tab list — decide if they appear only when applicable.
5. **L25 "active offer" blocker** listed in spec but not a checklist row (8 rows); third offer «لم يُرد على الثالث» — need rule for expired/unanswered offers counting as "no active offer".
6. **L25 objection period 15 days** and **L26 owner read access 90 days** are marked «افتراض» → make configurable per institution.
7. **L26 button vs chain**: chain already shows التدقيق «hourglass» (in progress) while «إرسال للتدقيق والاعتماد» is still enabled; clarify if this is the pre-submit preview (chain = planned approvers) or post-submit (button should be hidden/disabled).
8. **L26 core-system sync** («تحديث نظام التمويل · مغلق · مزامنة …») implies integration; Phase 1 financial ops are manual (A-04). Decide manual status entry vs connector.
9. **L26 vs D14 dates**: L26 case is overdue since 2026-09-22 and not closed on 2026-09-23, while D14 says «اكتملت التسوية في 2026-09-24» (consistent only if closed next day). Seed accordingly.
10. **L24 timeline**: CaseHeader SLA «القسط 3 مستحق 2027-02-10» (future snapshot) while latest audit event is 2026-10-02 and footer chain verified to that event; snapshot date is ambiguous.
11. **L04 counts**: «استيراد 231 صفاً» — unclear whether resolved duplicates (link/create-with-reason) add to the count; «ربط» semantics (attach row data to existing case? update?) undefined. Step 1 and step 3 not drawn.
12. **L22 composer** is only drawn for «مع المالك»; internal-notes and provider channel UIs (recipient selection, provider scoping) not drawn. «قالب»/«اقتراح موعد» dialogs not drawn.
13. **L23 decision options** «مقبولة / مقبولة جزئياً / غير مقبولة» — order in UI starts with «مقبولة جزئياً» (selected) — keep order as drawn or normalize? Escalation action («إغلاق أو تصعيد») has no button on screen.
14. **Export permissions**: L24 export is auditor-only; the design shows the button to a case-team default user — apply the Components rule «مخفي أم معطّل؟»: «يُخفى الإجراء إذا لم يملك الدور صلاحيته أصلاً؛ ويُعطّل مع سبب مرئي إذا كان الدور مخوّلاً لكن الحالة غير مؤهلة بعد.» → hide export for non-auditors. Same rule governs L25 disabled buttons (legal is authorized, case not eligible → disabled with visible reason).
