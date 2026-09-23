# B6 — Debtor / Owner Journey (mobile first)

Source: `design-source/04 Phase 1 - B6 Debtor Journey.dc.html` (condensed `_condensed/04 Phase 1 - B6 …txt`). Page title `04 Phase 1 — B6 Debtor journey — رهون`; H1 «المرحلة 1 — الدفعة B6: رحلة المالك / المدين (الجوال أولاً)».

**Global design principles (verbatim intro)**: «نص 18/30 للقراءة المريحة، أهداف لمس ≥ 48px، إجراء أساسي واحد لكل شاشة، و«أحتاج مساعدة» ظاهرة دائماً. لا صور منازل أو مفاتيح أو مطارق. المالك يرى حالته فقط وما سُمح له به.»
**Responsive (verbatim)**: «التجاوب: 390 أساسي؛ 768 عمود واحد بعرض 560 في المنتصف؛ 1440 بطاقة الخطوة التالية + عمود جانبي، بلا قائمة جانبية. تكبير النص حتى 200% دون قص: البطاقات تتمدد رأسياً والأزرار تلتف.»
Artboards: 390 px mobile (all D-screens), plus `P1-Debtor-Home-Mobile-EN-LTR` (390, English/LTR) and `P1-Debtor-Home-Desktop` (1440). Button heights: primary 54px (mobile), secondary 50–52px; fields 52–54px; body 16px+; primary colour `#AA4528`.
**Tone**: humane, non-threatening, second person, reassurance about rights («يمكنك…», «خذ وقتك»), never legal-threat vocabulary; payments never collected by Rahoon. Keep all Arabic strings verbatim.

**Shell components**
- `DebtorTop` props: `title` (default «حالتك»), `sub` (default «مع مصرف الأفق»), `back` bool (shows back button `aria-label="رجوع"`, icon arrow_forward; else Rahoon symbol logo 28px), `unread` int (bell `aria-label` «الإشعارات، N جديدة» / «الإشعارات», dot when N>0). Height 60.
- `DebtorNav` prop `active`: home|docs|options|payments|help. Items: الرئيسية (space_dashboard) · المستندات (folder) · الخيارات (tips_and_updates) · المدفوعات (payments) · المساعدة (support). 5-col bottom bar, 68px, active = bold rust text + top orange inset bar, `aria-current="page"`.
- Screens without DebtorNav are focused flows (D01, D07, D08, D09, D12, D13).

Suggested routes (Next.js): `/invite/[token]` (D01a) → `/owner/verify` (D01b + OTP step) → `/owner` … listed per screen. Brief paths in «».

---

## D01 — الدعوة والتحقق من الهوية / Invitation & identity verification
Brief path «/invite → تحقق». Role: owner/debtor (unauthenticated).

### D01a Invitation — `P1-Debtor-Invitation-Mobile · D01a` (390)
- Route `/invite/[token]`. No DebtorTop — centered logo header (horizontal logo 172px). No nav.
- Content: eyebrow «دعوة من جهتك المموّلة»; H2 «مصرف الأفق يدعوك لمتابعة حالتك عبر رهون»; p «هنا سترى الخطوة التالية، والمستندات المطلوبة، والخيارات المتاحة لك، ويمكنك طرح أسئلتك في أي وقت.»
- Trust card (icon verified) «كيف تتأكد أن الدعوة حقيقية»: «• الرابط يبدأ بـ rahoon.sa» · «• لن نطلب منك كلمة مرور بنكية أو أي دفعة هنا» · «• رمز الدعوة: AF-7Q2K — يطابق رسالة المصرف».
- Actions: primary «متابعة» → D01b; link «لا أعرف هذه الدعوة» (report/decline invitation — flow not drawn; should record report and show safe guidance).
- States (spec): «منتهية، مستخدمة، رقم غير صحيح ← تواصل مع المصرف» (expired, already used, wrong number → contact the bank). Not drawn.

### D01b Identity verify — `P1-Debtor-IdentityVerify-Mobile · D01b` (390)
- Route `/owner/verify`. `DebtorTop title="التحقق من هويتك" sub="خطوة 1 من 2" back=true`. No nav.
- Content: p «نتحقق من هويتك لنعرض لك حالتك فقط، ولا يراها غيرك.»
- Option A: button (icon badge) «الدخول عبر الهوية الوطنية الرقمية» + caption «مزوّد هوية وطني — نمط محجوز، يتطلب تأكيد التكامل».
- Divider «أو».
- Option B: field «آخر 4 أرقام من هويتك الوطنية» (numeric, `inputmode="numeric"`, LTR, mono, letter-spaced, masked display sample «••42»; validation: exactly 4 digits, must match last 4 of registered ID). Field «سنرسل رمزاً إلى جوالك المسجل» showing read-only masked phone `+966 5• ••• ••81` + «الرقم غير صحيح؟ تواصل مع المصرف» (link).
- Primary «إرسال الرمز» → step 2 (OTP entry — **not drawn**, «خطوة 1 من 2» implies it; reuse D09 6-box OTP).
- Rules: A-07 «نمط عام: رابط دعوة + رمز جوال + مزوّد هوية وطني (مكان محجوز). لا يُفترض تكامل حي.»; OTP sent only to phone registered by lender (owner cannot change it here); rate limit/lockout implied.
- Entities/API: `Invitation { token, code "AF-7Q2K", caseId, ownerId, lenderId, status: active|used|expired|reported, expiresAt }`; `OtpChallenge { id, purpose: login|consent, phoneMasked, attempts, expiresAt }`. `GET /public/invitations/{token}`, `POST /public/invitations/{token}/report`, `POST /owner/auth/verify-id` (last4) → `POST /owner/auth/otp/send`, `POST /owner/auth/otp/verify` → session; `GET /owner/auth/national-id/start` (placeholder).
- Integration: National digital ID provider — **placeholder/unavailable** («نمط محجوز، يتطلب تأكيد التكامل»); SMS OTP — sandbox.
- Spec row: «الوثوق بالدعوة والدخول بأمان» · «متابعة؛ تحقق بمزود وطني أو آخر 4 أرقام + رمز» · «منتهية، مستخدمة، رقم غير صحيح ← تواصل مع المصرف».

---

## D02 — الرئيسية وملخص الحالة / Home & case summary
Artboards: `P1-Debtor-Home-Mobile-OfferWaiting · D02` (390), `P1-Debtor-Home-Mobile-EN-LTR · D02` (390), `P1-Debtor-Home-Desktop · 1440`. Route `/owner`. Brief «الرئيسية».
- Mobile shell: `DebtorTop title="أهلاً عبدالله" sub="حالتك مع مصرف الأفق" unread=1`; `DebtorNav active="home"`.
- Content (mobile):
  - Next-step card (article, primary emphasis): eyebrow «خطوتك التالية»; title «راجع العرض الجديد»; «قسط شهري 15,074.52 ريال يوم 10 من كل شهر، كما طلبت.»; deadline (icon schedule, warning colour) «لديك حتى 2026-10-11 (10 أيام)»; button «عرض التفاصيل» → D07.
  - Progress card: «أين وصلت حالتك» + link «كل المراحل» (→ D03); 5-segment bar (done = charcoal, current = orange, todo = grey); label «3 من 5 · اختيار الحل المناسب».
  - Two quick tiles (grid 1fr 1fr): «المستندات» / «كلها مكتملة» (icon task_alt) → D04; «المديونية» / «تفصيل واضح» (icon receipt_long) → D05.
  - Contact card: avatar initials «س ق», «سارة · مسؤولة حالتك», «مكالمتكم الثلاثاء 4:30 م», icon button chat `aria-label="مراسلة سارة"` (48px) → D12.
- EN-LTR variant (dir=ltr, lang=en): header (symbol logo, «Hello, Abdullah» / «Your case with Al-Ufuq Bank», bell «Notifications, 1 new»); card «Your next step» / «Review the new offer» / «A monthly payment of **SAR 15,074.52** on the 10th of each month, as you asked.» / «You have until 2026-10-11 (10 days)» / «See details»; «Where your case is» / «All stages» / «3 of 5 · Choosing the right solution»; nav Home · Documents · Options · Payments · Help. Note (verbatim): «Layout mirrors to LTR; amounts, dates and the progress order follow reading direction. The Arabic logo mark stays as supplied.» (Quick tiles/contact card not drawn in EN.)
- Desktop 1440: top header (horizontal logo 176px; top nav links الرئيسية [current] · المستندات · الخيارات · المدفوعات · المساعدة; right «عبدالله م. · مصرف الأفق»), no sidebar; `main` max-width 1040, grid `minmax(0,1.4fr) minmax(0,1fr)`: left next-step card (H2 «راجع العرض الجديد», same copy, deadline without «(10 أيام)», buttons «عرض التفاصيل» primary + «أحتاج مساعدة» secondary); right column: progress card (no «كل المراحل» link) + contact card with text button «مراسلة» (avatar 48).
- States (spec): «بلا إجراء: «لا شيء مطلوب منك الآن»؛ دون اتصال: آخر نسخة» (no action → show «لا شيء مطلوب منك الآن»; offline → last cached version).
- Rules: one primary action per screen; next-step derived from case state (offer awaiting, doc rejected, payment due, closed…); deadline countdown in days; owner sees own case only.
- Entities/API: `GET /owner/home` → `{ greetingName, lenderName, nextStep { type, title, body, deadline, daysLeft, ctaRoute } | null, journey { current: 3, total: 5, label }, docsSummary, debtSummary, caseManager { firstName, initials, nextAppointment }, unreadCount }`.
- Spec row: «معرفة الخطوة التالية ومهلتها فوراً» · «إجراء واحد + مراسلة المسؤول».

---

## D03 — الرحلة بلغة مبسطة / Plain-language journey
`P1-Debtor-Journey-Mobile · D03` (390). Route `/owner/journey`. Brief «الرئيسية › مراحل الحالة».
- Shell: `DebtorTop title="مراحل حالتك" back=true`; `DebtorNav active="home"`.
- Content: vertical ordered list (`<ol>`, grid `32px 1fr`; 32px dot + 2px connector line; current `aria-current="step"`). Dot: done = charcoal filled + check; current = tint bg + orange border + more_horiz; todo = white + grey border.
  | status | title | description | meta |
  |---|---|---|---|
  | done | التعرف على حالتك | جمعنا بيانات التمويل والعقار. | 2026-08-14 |
  | done | التحقق والتقييم | راجعنا المستندات وقيّم مكتب مستقل العقار. | 2026-09-10 |
  | current | اختيار الحل المناسب | نعمل معك على حل يناسب دخلك. أنت هنا. | الآن |
  | todo | الاتفاق | بعد موافقتك يُفعَّل الاتفاق. | — |
  | todo | السداد والإغلاق | تسدد حسب الجدول، ثم نغلق الحالة ونرسل لك مستنداتها. | — |
- Info note (icon info): «لن يُتخذ أي إجراء قانوني دون إشعارك مسبقاً وإتاحة فرصة الاعتراض.»
- Rules: owner journey has 5 simplified stages mapped from 7 internal stages (mapping to define: الاستلام→1; التحقق+التقييم→2; إعداد الحل+الموافقة الداخلية+رد المالك→3; agreement→4; execution/closure→5). «المسار لا يعرض الإحالة كمرحلة متوقعة» — referral is never shown as an expected stage.
- API: `GET /owner/journey`.
- Spec row: «فهم الرحلة بلغة مبسطة» · actions «—».

---

## D04 — المستندات المطلوبة / Requested documents
`P1-Debtor-Documents-Mobile-Rejected · D04` (390). Route `/owner/documents`. Brief «المستندات».
- Shell: `DebtorTop title="المستندات المطلوبة" sub="1 يحتاج رفعاً من جديد"`; `DebtorNav active="docs"`.
- Rejected-doc card (top, emphasized): icon undo (error red) + «كشف الراتب لآخر 3 أشهر»; reason box (error tint): «**لماذا نحتاجه مرة أخرى؟** الصفحة الثانية غير واضحة في الصورة. جرّب ملف PDF من تطبيق البنك، أو صوّر الصفحة في مكان مضاء.»; «حتى 2026-09-21 · طلبه مصرف الأفق»; two buttons (grid 1fr 1fr): «رفع ملف» (upload_file), «تصوير» (photo_camera); link «أحتاج مساعدة في هذا المستند».
- Other docs list (icon coloured by status, title, status text same colour):
  - ✓ green «صورة الهوية الوطنية» — «استلمناه · تنتهي صلاحيته قريباً»
  - ✓ green «تعريف بالراتب» — «استلمناه»
  - ✓ green «صك الملكية» — «أرسله المصرف»
  - ⏱ grey «إشعار السداد» — «سنطلبه بعد الاتفاق»
- Actions: upload file (PDF/image), camera capture (mobile `capture`), help link (opens message to case manager prefilled with doc context). After upload → status «قيد المراجعة».
- States (spec): «مطلوب، مرفوض بسبب، قيد المراجعة، مستلم» (+ drawn: «أرسله المصرف» provided by lender, «سنطلبه بعد الاتفاق» future request, expiring soon).
- Rules: rejection must carry a human-readable reason and fix advice (see L23 complaint — «مرفوض» without reason was a template defect); each request has due date + requester; owner only sees docs requested from/shared with them.
- Entities/API: `DocumentRequest { id, caseId, docType, titleAr, requestedBy (lender), dueDate, status: requested|rejected|in_review|received|provided_by_lender|future, rejectionReason, rejectionHelp, currentVersionId, expiresSoon }`, `DocumentVersion { file, uploadedAt, source: owner_upload|camera }`. `GET /owner/documents`, `POST /owner/documents/{requestId}/upload` (multipart), `POST /owner/documents/{requestId}/help`.
- Spec row: «رفع ما يُطلب وفهم سبب الرفض» · «رفع / تصوير / مساعدة».

---

## D05 — تفصيل المديونية / Debt breakdown
`P1-Debtor-DebtBreakdown-Mobile · D05` (390). Route `/owner/debt`. Brief «الرئيسية › المديونية».
- Shell: `DebtorTop title="ما عليك الآن" back=true`; `DebtorNav active="home"`.
- Total card: «المبلغ المتبقي على التمويل» · `1,284,560.00` «ريال» · «من سجلات مصرف الأفق · تحديث 2026-09-22».
- Line items (title, amount LTR, plain explanation):
  | البند | المبلغ | الشرح |
  |---|---|---|
  | أصل التمويل المتبقي | 1,121,840.00 | المبلغ الذي استلمته ولم يُسدَّد بعد. |
  | الأرباح المستحقة | 144,420.00 | أرباح التمويل حسب عقدك للفترة المتبقية حتى اليوم. |
  | غرامات التأخير | 18,300.00 | ناتجة عن الأقساط المتأخرة. العرض الحالي يلغيها. |
  | رسوم أخرى | 0.00 | لا توجد رسوم إضافية. |
  Calculation: total = Σ items (1,121,840.00 + 144,420.00 + 18,300.00 + 0.00 = 1,284,560.00) — validate server-side.
- Action: link «أعتقد أن هناك خطأ في المبلغ» → D13 prefilled type «اعتراض على مبلغ أو قرار».
- Rules: amounts come from lender records with update timestamp («المبالغ من المصرف بتاريخ التحديث»); explanations in plain language; show waiver impact of current offer.
- API: `GET /owner/debt` → `{ total, currency, source, asOf, items[{ key, label, amount, explanation }] }`.
- Spec row: «فهم كل بند ومصدره» · «الإبلاغ عن خطأ ← شكوى من نوع اعتراض».

---

## D06 — الخيارات المتاحة / Available options
`P1-Debtor-Options-Mobile · D06` (390). Route `/owner/options`. Brief «الخيارات».
- Shell: `DebtorTop title="الخيارات المتاحة لك"`; `DebtorNav active="options"`.
- Offer card: eyebrow «عرض مقدَّم لك»; title «قسط أقل لمدة أطول»; «تبقى في منزلك، وتدفع 15,074.52 ريال شهرياً لمدة 84 شهراً.»; button «عرض التفاصيل» → D07.
- Heading «خيارات أخرى يمكنك السؤال عنها»; cards (title, description, link):
  - «تأجيل مؤقت» — «إيقاف الأقساط لفترة قصيرة إذا مررت بظرف مؤقت، ثم الاستئناف.» — «اسأل عن هذا الخيار»
  - «البيع الطوعي» — «إذا رغبت أنت في بيع العقار بنفسك وبسعر السوق، يمكننا مساعدتك في الترتيب. هذا خيارك فقط.» — «أريد معرفة المزيد»
- Actions: links create an "option inquiry" (message/task to case manager), not a commitment.
- Rules: «البيع الطوعي يُشرح بحياد ولا يُقترح إلا بطلب المالك» (neutral, owner-initiated only); offers shown only when approved & sent by lender.
- API: `GET /owner/options` → `{ activeOffer?, otherOptions[] }`; `POST /owner/options/{key}/inquiry`.
- Spec row: «رؤية العرض المقدم وخيارات يمكن السؤال عنها» · «عرض التفاصيل؛ السؤال عن خيار».

---

## D07 — عرض التسوية / Settlement offer
`P1-Debtor-Offer-Mobile · D07` (390). Route `/owner/offers/[offerId]`. Brief «الخيارات › العرض».
- Shell: `DebtorTop title="العرض المقدم لك" sub="صالح حتى 2026-10-11" back=true`. No nav (focused).
- Hero: «قسطك الجديد» · `15,074.52` «ريال» · «يوم 10 من كل شهر · 84 شهراً».
- Terms list (key/value): «يبدأ» 2026-12-10 · «ينتهي» 2033-11-10 · «غرامات التأخير» «تُلغى (18,300 ريال)» · «بيتك» «يبقى ملكك».
- Disclosure `<details open>`: summary «ماذا لو تأخرت عن قسط؟» → «نتواصل معك أولاً. إذا تأخر قسطان متتاليان نمنحك 15 يوماً للتصحيح ونبحث معك عن حل، قبل أي خطوة أخرى.»
- Actions: primary «مراجعة وقبول» → D09; secondary (grid 1fr 1fr) «اقتراح بديل» → D08, «لا يناسبني» (decline — flow not drawn; should capture optional reason and not trigger any adverse action).
- States (spec): «منتهي الصلاحية: طلب عرض جديد» (expired → request new offer). Not drawn.
- Rules: offer = locked approved solution version (v3); validity date; breach/cure terms explained (2 consecutive missed installments → 15-day cure).
- API: `GET /owner/offers/{id}`, `POST /owner/offers/{id}/decline`, `POST /owner/offers/{id}/request-new` (expired).
- Spec row: «فهم الشروط قبل القرار» · «قبول / بديل / لا يناسبني».

---

## D08 — العرض المقابل / Counteroffer
`P1-Debtor-Counteroffer-Mobile · D08` (390). Route `/owner/offers/[offerId]/counter`. Brief «العرض › اقتراح بديل».
- Shell: `DebtorTop title="اقتراح بديل" back=true`. No nav.
- Intro: «أخبرنا بما يناسبك. سيُراجَع اقتراحك، وقد يُقبل أو يُعدَّل، وسنرد خلال 3 أيام عمل.»
- Fieldset «ما الذي تود تغييره؟» (multi-select checkboxes, 52px cards): ☑ «يوم القسط في الشهر» · ☑ «موعد أول قسط» · ☐ «مبلغ القسط».
- Conditional fields (grid 1fr 1fr): select «اليوم المناسب» (value 10; day-of-month) · select «البدء من» (value «ديسمبر»; month). Amount field for «مبلغ القسط» not drawn (implied when checked).
- Textarea «سبب الطلب (اختياري)» sample «راتبي يتأخر أحياناً إلى يوم 5.»
- Primary «إرسال الاقتراح» (enable when ≥1 change selected with value).
- Rules: «يُنشئ طلباً وليس اتفاقاً» — creates a counter-proposal request; lender side transitions case «بانتظار العميل ← تفاوض» (actor «النظام (اقتراح من المالك)») and may produce a new solution version (v3) that goes through internal approval again; response promised within 3 business days.
- Entities/API: `CounterProposal { id, offerId, caseId, changes { installmentDay?, firstInstallmentMonth?, installmentAmount? }, reason?, status: submitted|accepted|modified|declined, submittedAt, responseDueAt }`. `POST /owner/offers/{id}/counter`.
- Spec row: «طلب تعديل بلا ضغط» · «إرسال الاقتراح».

---

## D09 — الموافقة والقبول / Consent & acceptance
Brief «العرض › قبول». Route `/owner/offers/[offerId]/accept` and `/owner/offers/[offerId]/accepted`.

### Review — `P1-Debtor-Consent-Mobile-Review · D09` (390)
- Shell: `DebtorTop title="قبل أن توافق" back=true`. No nav.
- Summary card «أنت توافق على:» «• 84 قسطاً × 15,074.52 ريال» · «• يوم 10 من كل شهر، يبدأ 2026-12-10» · «• إلغاء غرامات التأخير 18,300 ريال» · «• السداد بتحويل إلى حساب المصرف» + link «قراءة الاتفاق كاملاً (PDF)».
- Two required checkboxes (drawn checked): «قرأت الشروط وفهمت ما يحدث إذا تأخرت.» · «أوافق باختياري، ويمكنني طلب المساعدة أو الاعتراض لاحقاً.»
- OTP: label «رمز التأكيد المرسل إلى جوالك»; 6 single-digit boxes, LTR, mono (sample 7 2 0 5 1 _). Resend not drawn.
- Primary «أوافق على العرض» — enabled only when both checkboxes ticked + 6-digit OTP entered.
- States (spec): «خطأ الرمز، نجاح بمرجع ونسخة» (wrong code error — not drawn).

### Success — `P1-Debtor-Consent-Mobile-Success · D09` (390)
- Shell: `DebtorTop title="تمت الموافقة"` (no back). `main role="status"`.
- Icon check_circle 60px; H2 «شكراً، سجّلنا موافقتك»; p «سيراجع المصرف الاتفاق ويفعّله خلال يومي عمل. أول قسط 2026-12-10.»
- Reference box: «رقم المرجع» `AGR-2026-004172-01` · «2026-10-02 14:21 · 20 ربيع الآخر 1448هـ».
- Buttons: «تنزيل نسخة الاتفاق» (PDF), «العودة للرئيسية».
- Rules: acceptance = **consent record + OTP, not a licensed/binding e-signature** (A-05: «القبول داخل المنصة ليس توقيعاً ملزِماً … سجل موافقة + رمز تحقق. الأثر القانوني والتوقيع المرخّص يتطلبان تأكيداً (نمط مشروط في المرحلة 2)»); record both acknowledgements, OTP verification, device, masked IP, timestamp (Gregorian + Hijri) → audit event «قبول المالك للعرض v3» (L24); lender activates agreement within 2 business days; agreement ref format `AGR-YYYY-<caseSeq>-NN`.
- Entities/API: `ConsentRecord { id, offerVersionId, caseId, ownerId, acknowledgements[{key, textAr, checked}], otpChallengeId, verifiedAt, device, ipMasked, agreementRef, documentHash }`. `POST /owner/offers/{id}/consent/otp` (send), `POST /owner/offers/{id}/consent` (acks + otp) → `{ agreementRef, recordedAt, hijriDate }`, `GET /owner/agreements/{ref}/pdf`.
- Integration: SMS OTP (sandbox); licensed e-signature — **not in Phase 1** (X01 conditional, Phase 2).
- Spec row: «موافقة واعية ومسجلة» · «إقراران + رمز تأكيد».

---

## D10 — جدول السداد والإيصالات والمراجع / Schedule, receipts & references
`P1-Debtor-Payments-Mobile · D10` (390). Route `/owner/payments`. Brief «المدفوعات».
- Shell: `DebtorTop title="المدفوعات"`; `DebtorNav active="payments"`.
- Next installment card: «القسط القادم» · `15,074.52` «ريال» · «يوم 2027-02-10 · القسط 3 من 84» · link «كيف أدفع؟» (payment instructions: bank transfer details — sheet not drawn).
- Info note: «تدفع مباشرة للمصرف بتحويل بنكي. رهون لا تستلم أي مبالغ.»
- Installment rows («القسط N · date», status coloured, meta):
  | # | date | status | meta |
  |---|---|---|---|
  | 1 | 2026-12-10 | مستلم (green) | مرجع TRX-88392214 · إيصال متاح |
  | 2 | 2027-01-10 | قيد التأكيد (warning) | استلمنا إشعارك · نتحقق مع المصرف |
  | 3 | 2027-02-10 | قادم (grey) | — |
- Actions: «كيف أدفع؟»; download receipt (spec «تنزيل الإيصال»; button not drawn — make row tappable when receipt available); owner payment notice submission implied («استلمنا إشعارك»).
- Rules: payment outside platform; status «قيد التأكيد» until lender reconciliation matches bank ref (manual, maker-checker A-04); no wallet/escrow/gateway.
- Entities/API: `Installment { seq, dueDate, amount, status: upcoming|pending_confirmation|received|late, bankRef?, receiptFileId?, ownerNoticeAt? }`; `PaymentNotice { installmentSeq, transferDate, amount, ref?, attachment? }`. `GET /owner/payments`, `POST /owner/payments/{seq}/notice`, `GET /owner/payments/{seq}/receipt`, `GET /owner/payments/how-to-pay`.
- Spec row: «معرفة القسط القادم وحالة ما دُفع» · «كيف أدفع؛ تنزيل الإيصال» · «الدفع خارج المنصة؛ «قيد التأكيد» حتى المطابقة».

---

## D11 — المساعدة في حالات العسر / Hardship help
`P1-Debtor-Hardship-Mobile · D11` (390). Route `/owner/help`. Brief «المساعدة».
- Shell: `DebtorTop title="نحن هنا للمساعدة"`; `DebtorNav active="help"`.
- Intro: «إن تغيّر وضعك، أخبرنا مبكراً. لا تحتاج لشرح كل شيء الآن.»
- Fieldset «ما الذي تغيّر؟» (option cards 52px with icon; none selected in design): work_off «فقدت عملي أو انخفض دخلي» · medical_services «ظرف صحي» · family_restroom «تغيّر في الأسرة» · more_horiz «سبب آخر» · do_not_disturb_on «أفضل ألا أذكر السبب».
- Primary (icon call) «اطلب مكالمة من مسؤول حالتك».
- Privacy note: «ما تشاركه يُستخدم فقط لإيجاد حل مناسب، ويراه فريق حالتك فقط.»
- Rules: reason optional («السبب اختياري»); creates hardship flag + callback task for case manager; data visible to case team only (sensitive).
- Entities/API: `HardshipRequest { caseId, reasonKey?: income_loss|health|family|other|prefer_not_say, requestedCallback: true, createdAt, status }`. `POST /owner/hardship`.
- Entry to D13 complaint from Help implied (brief path «المساعدة › شكوى») — link not drawn.
- Spec row: «الإبلاغ المبكر عن ظرف دون حرج» · «طلب مكالمة».

---

## D12 — الرسائل والمواعيد / Messages & appointments
`P1-Debtor-Messages-Mobile · D12` (390). Route `/owner/messages`. Brief «الرسائل».
- Shell: `DebtorTop title="الرسائل" sub="مع سارة · مصرف الأفق" back=true`. No nav.
- Appointment strip (below top bar): date tile 30 / سبتمبر; «مكالمة · 4:30 م»; «أكّدتَ الحضور · يمكنك إعادة الجدولة».
- Messages (max-width 84%; staff start/white/border `#CBCAC6`; owner end/tint `#FDF0EB`/`#F0B8A6`; meta under bubble):
  - سارة: «أرسلنا لك عرضاً معدلاً كما طلبت: يوم 10 والبدء في ديسمبر.» — «سارة · 2026-10-01 11:45»
  - أنت: «ممتاز، سأراجعه الليلة.» — «أنت · 2026-10-01 12:02 · قُرئت»
  - سارة: «خذ وقتك. إن كان لديك أي سؤال فأنا هنا.» — «سارة · 2026-10-01 12:10»
- Composer: placeholder «اكتب رسالتك…», icon send button `aria-label="إرسال"` (50px).
- Actions: send; confirm/reschedule appointment (reschedule UI not drawn).
- Rules: owner sees only owner-channel messages (never internal notes); expected reply within one business day («الرد المتوقع خلال يوم عمل» — show as hint); staff shown by first name.
- API: `GET /owner/messages`, `POST /owner/messages`, `GET /owner/appointments`, `POST /owner/appointments/{id}/confirm|reschedule`.
- Spec row: «تواصل مع شخص حقيقي ومواعيد واضحة» · «إرسال، إعادة جدولة».

---

## D13 — الشكاوى والاعتراضات / Complaints & objections
`P1-Debtor-Complaint-Mobile · D13` (390). Route `/owner/complaints/new` (+ `/owner/complaints/[ref]` for tracking, not drawn). Brief «المساعدة › شكوى».
- Shell: `DebtorTop title="شكوى أو اعتراض" back=true`. No nav.
- Intro: «تراجع شكواك جهة مستقلة عن فريق حالتك، ونرد عليك كتابياً خلال 5 أيام عمل.»
- Fieldset «نوع الطلب» (radio cards; first selected in design): «شكوى على طريقة التعامل» · «اعتراض على مبلغ أو قرار».
- Textarea «اشرح لنا ما حدث» (required, min-height 110, empty).
- Button (dashed) «إرفاق ملف (اختياري)» (icon attach_file).
- Primary «إرسال».
- Footnote: «يحق لك أيضاً التقدم للجهات الرقابية المختصة في أي وقت.»
- Rules: submits Complaint (L23) routed to independent compliance reviewer; while open: blocks referral/cancellation and pauses owner-response SLA; case team sees tag only; written reply within 5 business days; must mention regulator right; D05 «أعتقد أن هناك خطأ في المبلغ» preselects objection type.
- API: `POST /owner/complaints` (type, body, attachments) → `{ ref, dueAt }`; `GET /owner/complaints`, `GET /owner/complaints/{ref}` (status timeline + written response).
- Spec row: «شكوى مستقلة بمهلة ورد مكتوب» · «إرسال، متابعة الحالة» · «يذكر حق التقدم للجهات المختصة».

---

## D14 — مستندات الإغلاق / Closure documents
`P1-Debtor-ClosureDocs-Mobile · D14` (390). Route `/owner/documents/closure` (and home shows closed state). Brief «المستندات › الإغلاق».
- Shell: `DebtorTop title="أهلاً تركي" sub="حالتك مع مصرف الأفق"`; `DebtorNav active="docs"`.
- Content: icon task_alt 56px; H2 «أُغلقت حالتك»; p «اكتملت التسوية في 2026-09-24. هذه مستنداتك، احتفظ بنسخة منها.»
- Doc rows (icon description, title, meta, download icon button `aria-label="تنزيل"` 48px):
  - «خطاب المخالصة النهائية» — «2026-09-21 · PDF»
  - «خطاب فك الرهن» — «2026-09-22 · PDF»
  - «ملخص حالتك» — «كل الخطوات والمبالغ · PDF»
- Note: «يبقى حسابك متاحاً للقراءة حتى 2026-12-23. يمكنك طلب نسخة لاحقاً من المصرف.» (= closure + 90 days)
- Rules: owner read-only access after closure for fixed period then revoked (assumption, configurable); docs published on L26 approval; lien-release submission proof is lender-internal (not shown).
- API: `GET /owner/closure` → `{ closedAt, documents[{ type, title, date, fileId }], accessExpiresAt }`, `GET /owner/documents/{fileId}/download`.
- Spec row: «الحصول على مستندات الإغلاق» · «تنزيل» · «وصول قراءة لمدة محددة ثم يُسحب (افتراض)».

---

## Owner-screen spec table (verbatim, `مواصفات شاشات المالك`)
Columns: # · الشاشة · هدف المستخدم · الإجراءات · الحالات والصلاحية
| # | الشاشة | هدف المستخدم | الإجراءات | الحالات والصلاحية |
|---|---|---|---|---|
| D01 | الدعوة والتحقق | الوثوق بالدعوة والدخول بأمان | متابعة؛ تحقق بمزود وطني أو آخر 4 أرقام + رمز | منتهية، مستخدمة، رقم غير صحيح ← تواصل مع المصرف |
| D02 | الرئيسية | معرفة الخطوة التالية ومهلتها فوراً | إجراء واحد + مراسلة المسؤول | بلا إجراء: «لا شيء مطلوب منك الآن»؛ دون اتصال: آخر نسخة |
| D03 | المراحل | فهم الرحلة بلغة مبسطة | — | المسار لا يعرض الإحالة كمرحلة متوقعة |
| D04 | المستندات | رفع ما يُطلب وفهم سبب الرفض | رفع / تصوير / مساعدة | مطلوب، مرفوض بسبب، قيد المراجعة، مستلم |
| D05 | المديونية | فهم كل بند ومصدره | الإبلاغ عن خطأ ← شكوى من نوع اعتراض | المبالغ من المصرف بتاريخ التحديث |
| D06 | الخيارات | رؤية العرض المقدم وخيارات يمكن السؤال عنها | عرض التفاصيل؛ السؤال عن خيار | البيع الطوعي يُشرح بحياد ولا يُقترح إلا بطلب المالك |
| D07 | العرض | فهم الشروط قبل القرار | قبول / بديل / لا يناسبني | منتهي الصلاحية: طلب عرض جديد |
| D08 | العرض المقابل | طلب تعديل بلا ضغط | إرسال الاقتراح | يُنشئ طلباً وليس اتفاقاً |
| D09 | الموافقة | موافقة واعية ومسجلة | إقراران + رمز تأكيد | خطأ الرمز، نجاح بمرجع ونسخة |
| D10 | المدفوعات | معرفة القسط القادم وحالة ما دُفع | كيف أدفع؛ تنزيل الإيصال | الدفع خارج المنصة؛ «قيد التأكيد» حتى المطابقة |
| D11 | المساعدة | الإبلاغ المبكر عن ظرف دون حرج | طلب مكالمة | السبب اختياري |
| D12 | الرسائل | تواصل مع شخص حقيقي ومواعيد واضحة | إرسال، إعادة جدولة | الرد المتوقع خلال يوم عمل |
| D13 | الشكاوى | شكوى مستقلة بمهلة ورد مكتوب | إرسال، متابعة الحالة | يذكر حق التقدم للجهات المختصة |
| D14 | الإغلاق | الحصول على مستندات الإغلاق | تنزيل | وصول قراءة لمدة محددة ثم يُسحب (افتراض) |

## Cross-cutting data / security for owner portal
- Separate owner auth realm (invite token → last-4 + SMS OTP or national ID placeholder); session scoped to one case (a multi-case owner selector is not designed).
- Owner sees: own case only, owner-channel messages, requested docs, approved offers, own payments, closure docs. Never: internal notes, complaint reviewer notes, other parties.
- Seed data: case RH-2026-004172 عبدالله م., lender مصرف الأفق, case manager سارة القحطاني (shown to owner as «سارة»), offer v3: 84 × 15,074.52, day 10, 2026-12-10 → 2033-11-10, waiver 18,300; D14 owner تركي ب. (RH-2026-003702).

## Reusable components (B6)
| Component | Props / variants | Used in |
|---|---|---|
| `DebtorTop` | title, sub, back, unread | all mobile |
| `DebtorNav` | active home\|docs\|options\|payments\|help | D02–D06, D10, D11, D14 |
| `OwnerDesktopHeader` | links, active, userLabel | D02 desktop |
| `NextStepCard` | eyebrow, title, body, deadline (warning tone), primary CTA, optional secondary («أحتاج مساعدة») | D02, D06 (offer card variant) |
| `SegmentProgress` | total 5, current, label | D02 |
| `QuickTile` | icon, title, status text | D02 |
| `CaseManagerCard` | initials, name/role, next appointment, message button (icon or text) | D02 |
| `JourneyTimeline` (vertical) | steps {status done\|current\|todo, title, desc, meta} | D03 |
| `InfoNote` | icon, text | D03, D10 |
| `DocRequestCard` rejected variant | title, reason, help text, due, requester, upload/camera buttons, help link | D04 |
| `DocStatusRow` | icon/tone, title, status text | D04, D14 (with download) |
| `AmountHero` | label, amount, unit «ريال», source/asOf line | D05, D07, D10 |
| `ExplainedLineItem` | label, amount, explanation | D05 |
| `OptionCard` | title, description, link | D06 |
| `KeyValueList` | rows {k, v} | D07 |
| `Disclosure` | summary, body, defaultOpen | D07 |
| `ChoiceCard` checkbox / radio (52px) | checked, label, optional icon | D08, D09, D11, D13 |
| `Select` (52px) | label, value | D08 |
| `OtpInput` | length 6, LTR mono boxes, error state | D01 step 2, D09 |
| `ConsentSummary` | bullet list + full-PDF link | D09 |
| `SuccessStatus` | icon, title, body, reference box (ref + Gregorian + Hijri), actions | D09 |
| `InstallmentRow` | seq, date, status received\|pending\|upcoming, meta | D10 |
| `AppointmentStrip` | day, month, title/time, status text | D12 |
| `MessageBubble` / `Composer` | side, text, meta; placeholder, icon send | D12 |
| `FileAttachButton` (dashed) | label | D13 |

## Conflicts / ambiguities (B6)
1. **«أحتاج مساعدة» always visible** (intro rule) — only on desktop D02 and D04 link; mobile relies on the «المساعدة» nav tab, absent on focused flows (D07–D09, D12, D13). Decide on a persistent help affordance.
2. **D01 OTP step 2 not drawn**; D01b sub «خطوة 1 من 2». Masked last-4 input shows «••42» — clarify input masking behaviour (A-10 masks ID to first digit + last two). National ID provider = placeholder only.
3. **«لا أعرف هذه الدعوة»**, **«لا يناسبني»**, expired-offer, wrong-OTP, reschedule, receipt download, payment notice, complaint tracking screens are listed but not drawn.
4. **Snapshot inconsistencies**: D02 «المستندات كلها مكتملة» vs D04 one rejected doc; D04 due 2026-09-21 is before "today" (2026-09-23) but not shown as overdue; D02 deadline 2026-10-11 «(10 أيام)» implies today = 2026-10-01; D12 appointment (30 Sep) «أكّدتَ الحضور» shown with messages dated 2026-10-01; D10 installment 2 «قيد التأكيد» on 2027-01-10 vs working notes (inst.2 TRX-88410027 pending match). Treat as independent state mockups.
5. **Weekday**: «الثلاثاء 4:30 م» / 30 سبتمبر — 2026-09-30 is a Wednesday.
6. **Offer validity**: D07 «صالح حتى 2026-10-11» vs L22 message «الرد حتى 2026-10-03» (that was v2) and L23 extension example (other case). Validity is per offer version.
7. **Complaint SLA**: D13 «5 أيام عمل» vs L23 chip deadline on a Friday; define business-day calendar (Sun–Thu).
8. **Journey mapping** 7 internal stages → 5 owner stages is not specified; referral/external sale states must map to something neutral (never show referral as expected).
9. **D08** amount change field not drawn; month select shows only month name («ديسمبر») — year inference needed. Counter creates request; lender-side versioning (v3) per B4/L24.
10. **D11** option cards have no selection indicator — single vs multi-select unclear; call request with no reason must be allowed.
11. **D14 closure access** «حتى 2026-12-23» = 90 days after 2026-09-24 (assumption) — configurable; D14 uses DebtorNav «docs» active while greeting title is home-like.
12. **EN variant** only for D02 (partial); bank name translation «Al-Ufuq Bank»; need full i18n keys for all owner copy.
