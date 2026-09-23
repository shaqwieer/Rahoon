# B2 — Shared & Public screens (S01–S12): implementation spec

Source: `design-source/04 Phase 1 - B2 Shared & Public.dc.html`. The condensed mirror was cross-checked against the raw file. Also used: shell components `LenderSidebar` and `LenderTopbar`, the S01–S12 table in the brief (`00 Brief & Assumptions`) and the rules in `09 Handoff`.
Every Arabic string below is copied verbatim from the design. "Aside" means the grey spec panel (`specXxx` in the script) that sits next to each screen's artboards. These asides are normative requirements.

---

## 0. Frame inventory and global notes

### 0.1 Artboards actually drawn

| # | Frame label | Screen | Viewport | Size in design |
|---|---|---|---|---|
| 1 | `P1-Public-Landing-Desktop-Default` | S01 | desktop | 1440 × auto ("public, indexable") |
| 2 | `P1-Public-Landing-Mobile-Default` | S01 | mobile | 390 × 844 (above the fold only) |
| 3 | `P1-Public-DemoRequest-Desktop-Default` | S02 | desktop | 1440 × min 900 |
| 4 | `P1-Public-DemoRequest-Mobile-Success` | S02 | mobile | 390 × 844 |
| 5 | `P1-Shared-Login-Desktop-Default` | S03 | desktop | 1440 × 900 |
| 6 | `P1-Shared-Login-Desktop-EN-LTR` | S03 (EN, error state) | desktop | 1440 × 900 |
| 7 | `P1-Shared-MFA-Mobile-Default` | S04 | mobile | 390 × 844 |
| 8 | `P1-Shared-MFA-Mobile-Error` | S04 | mobile | 390 × 844 |
| 9 | `P1-Shared-MFA-Mobile-Locked` | S04 | mobile | 390 × 844 |
| 10 | `P1-Shared-InvitationAccept-Desktop-Default` | S05 | desktop | 1440 × 900 |
| 11 | `P1-Shared-RoleOrgSelect-Desktop-Default` | S06 | **960** (non-standard) | 960 × 900 |
| 12 | `P1-Shared-ProfileSecurity-Desktop-Sessions` | S07 | desktop | 1440 × min 900 |
| 13 | `P1-Shared-NotificationCenter-Desktop-Open` | S08 (popover) | desktop | 1440 × 900 |
| 14 | `P1-Shared-GlobalSearch-Desktop-Results` | S10 | desktop | 1440 × 900 |
| 15 | `P1-Shared-NotificationCenter-Mobile` | S08 | mobile | 390 × 844 |
| 16 | `P1-Shared-Tasks-Desktop-Mine` | S09 | desktop | 1440 × min 960 |
| 17 | `P1-Shared-HelpSupport-Desktop-Default` | S11 | desktop | 1440 × min 960 |
| 18 | `P1-Shared-AccessDenied-Desktop-Default` | S12 | desktop | 1440 × 800 |
| 19 | `P1-Shared-AccessDenied-Mobile-ExpiredLink` | S12 (debtor expired link) | mobile | 390 × 844 |

- Count: **12 desktop** frames (including the 960 frame and the EN-LTR frame) and **7 mobile** frames.
- The file's footer says "13 إطار سطح مكتب … + 7 إطارات جوال". I count 12 desktop frames, so the footer claim does not match the drawn frames.
- English/LTR variant: only S03 Login has one (frame 6).
- Public pages "للجهات المموّلة", "لملاك العقارات" and "الحوكمة والخصوصية" are **not designed as separate pages**. They exist only as landing-page nav links (`href="#"`) and as landing sections (S01). "كيف تعمل" is also only a section.

### 0.2 Not designed but referenced or implied (spec gaps)
- MFA on desktop, and MFA in EN.
- Arabic copy for the login error. Only the EN frame shows the error.
- Forgot-password flow ("نسيت كلمة المرور؟").
- The MFA enrolment screen reached after S05 ("…وإعداد التحقق").
- The demo form on mobile, and the demo success state on desktop.
- Mobile versions of S05, S06, S07, S09, S10 and S11. They have only responsive rules in the asides.
- S07 tabs other than "الأمان والجلسات": "الملف الشخصي", "تفضيلات الإشعارات" and "اللغة والعرض".
- The confirmation and MFA re-prompt screen for "إنهاء كل الجلسات الأخرى". It is described but not drawn.
- The change-password and recovery-codes flows.
- The full-page desktop notification center that "فتح مركز الإشعارات" opens, and the notification settings.
- The manual-task creation form, the reassign UI and the team/unassigned/completed tab contents.
- Help article pages, ticket detail pages and the "طلب وصول" dialog.
- The "كيف أتحقق من صحة الدعوة؟" page and the footer targets (سياسة الخصوصية، الشروط، تقديم شكوى، تواصل معنا).
- The success state after "طلب رابط جديد".
- Responsive rules for **768** and **390** appear only in the asides and have no artboard. They are captured per screen below.

### 0.3 Global rules (from this file and the handoff)
- Default language is `dir="rtl" lang="ar"`. For English, set `dir="ltr" lang="en"`. **The Arabic logo stays unchanged in the EN UI.** The layout mirrors. Non-directional icons are not mirrored. Frame note: "الشعار العربي يبقى كما هو في الواجهة الإنجليزية. يُعكس التخطيط، ولا تُعكس الأيقونات غير الاتجاهية."
- Numbers, refs, dates, emails and phones are wrapped in `<bdi dir="ltr">`. Refs, codes and phones use **IBM Plex Mono**.
- Public pages use SSR, hierarchical headings, `hreflang` ar/en and meta tags in Arabic and English. **Every app page** gets `noindex, nofollow`, the "محتوى خاص" header chip and no public caching.
- Masking happens **server-side**. The UI never receives full identities unless an audited "reveal" happens. An action the user has no permission for is omitted from the payload. An action the user is permitted but ineligible for is sent with a `reason` and rendered disabled.
- Switching organization re-creates the session and clears the query cache.
- Fonts: `IBM Plex Sans Arabic` (400/500/600/700), `IBM Plex Sans` (Latin), `IBM Plex Mono` (400/500). Icons: `Material Symbols Rounded` (decorative icons get `aria-hidden`).
- Global focus style: `outline:2px solid #151513; outline-offset:2px`. Links are `#AA4528` with underline offset 3px; hover is `#8E3920`.
- Page background `#FAF9F6`. Cards are `#FFFFFF` with a 1px `#CBCAC6` border and radius 14.
- **Sticky elements:** none are drawn in any frame. The raw file has no `position:sticky` or `position:fixed`. Only the S08 popover and the S10 scrim/dialog are positioned, both with `position:absolute`. *Proposal (not in the design):* make the app sidebar and topbar fixed while `main` scrolls, and make the landing header sticky.

---

## Shells used in this batch

### Public header/footer (S01 and S02; minimal versions in S05, S06 and S12-mobile)
- **Landing header** (desktop): height 80, white, bottom border `#CBCAC6`, padding 0 80, gap 32.
  - Logo `rahoon-horizontal-full.svg` 176px, alt "رهون — الصفحة الرئيسية".
  - Nav `aria-label="الرئيسية"` (15px/500, color #151513, no underline): كيف تعمل · للجهات المموّلة · لملاك العقارات · الحوكمة والخصوصية.
  - At inline-end: link "English" (`lang="en"`, IBM Plex Sans 600 14); outline button-link "تسجيل الدخول" (min-h 44); primary button-link "طلب عرض للمنشأة" (min-h 44, bg #AA4528).
- **Landing footer**: bg `#151513`, padding 40 80.
  - Logo `rahoon-horizontal-dark.svg` 176px.
  - Nav `aria-label="روابط التذييل"` (14px, white links): سياسة الخصوصية · الشروط · تقديم شكوى · تواصل معنا.
  - At end: "© 2026 رهون · بيانات العرض خيالية" (13px `#B5B3AD`). The second half is probably a demo-only marker and should be dropped in production.
- **Mobile public header**: height 60, white, bottom border, logo 172px. The landing page adds a menu button (44×44, `aria-label="القائمة"`, icon `menu`) that opens a popover menu ("قائمة منبثقة").
- **Minimal headers**:
  - S02: header 80 with logo and the link "العودة للرئيسية" at the end.
  - S05: header 72 with logo only, padding 0 48.
  - S06: logo centered in the page, no header bar.

### Lender app shell (S07, S08, S09, S10, S11, S12-desktop)
Composed as `LenderSidebar` (264px, full height) plus a column containing `LenderTopbar` (height 64) and `main`.

**LenderSidebar** — `nav aria-label="القائمة الرئيسية"`, width 264.
- Logo 172px.
- Org switcher button (`aria-haspopup="menu"`): avatar 32px "أف", org name **مصرف الأفق**, subtitle `{userRole}`, icon `unfold_more`.
- Nav items (min-h 40, radius 6, 14px):

  | key | label | icon | badge |
  |---|---|---|---|
  | portfolio | المحفظة | dashboard | — |
  | cases | الحالات | folder_open | — |
  | tasks | مهامي | task_alt | 7 |
  | approvals | الموافقات | approval | `props.approvals` |
  | complaints | الشكاوى | support_agent | 2 |
  | reports | التقارير | bar_chart | — |

- Active item: `aria-current="page"`, weight 700, color `#8E3920`, bg `#FDF0EB`, `box-shadow: inset -3px 0 0 #F4633A` (a 3px accent bar on the right edge = inline-start in RTL). Inactive items are weight 500, color `#22262A`.
- Badge: 12px/600, pill, bg #151513, white text, line-height 20.
- Footer: link "المساعدة والدعم" (icon `help`); user block with initials avatar 32 "س ق", name "سارة القحطاني" and "جلسة آمنة · MFA".
- Props: `active` (portfolio|cases|tasks|approvals|complaints|reports; B2 also passes `"none"`), `userName`, `userRole` (default "مديرة حالات"), `initials`, `approvals`.

**LenderTopbar** — height 64.
- Breadcrumb `nav aria-label="مسار التنقل"`: crumb1 is a link; when crumb2 exists, show `chevron_left` then crumb2 with `aria-current="page"`.
- Search trigger `role="search"`: width 380, max 40%, icon `search`, placeholder "ابحث بالمرجع أو رقم العقد أو المهمة", `<kbd dir="ltr">Ctrl K</kbd>`.
- Chip "محتوى خاص" with icon `shield_person`.
- Language button "EN" (40px, `aria-label="English"`).
- Bell button (40px, `aria-label="الإشعارات، 4 غير مقروءة"`, icon `notifications`) with an 18px count badge "4".

| Screen | Sidebar `active` | Topbar crumbs |
|---|---|---|
| S07 | none | حسابي › الأمان والجلسات |
| S08 | portfolio | المحفظة |
| S10 | cases | الحالات |
| S09 | tasks | مهامي |
| S11 | none | المساعدة والدعم |
| S12 | cases | الحالات |

App `main` padding is `28px 40px 40px`, or `32px 40px 40px` in S11. The page title is H2 32/44 bold. There is **no h1** on these app pages; see Conflicts.

**Tab pattern** (S07, S08, S09): `role="tablist"` with a 1px `#CBCAC6` bottom border.
- Each tab: min-h 44 (40 in the popover), padding 0 14, 14px.
- Selected: weight 700 with an underline `box-shadow: inset 0 -3px 0 #F4633A`.
- Unselected: color `#5E5D58`.
- Counts are wrapped in `<bdi>`.

Other shells (debtor mobile top/bottom nav, platform sidebar) are **not used** in B2.

---

## S01 — الصفحة التعريفية / Informative landing page

1. **Frames:** `P1-Public-Landing-Desktop-Default` (1440, "public, indexable"); `P1-Public-Landing-Mobile-Default` (390 × 844).
2. **Role:** anonymous public. **Route:** `/` (brief: "عام، قابل للفهرسة"). EN at `/en` (proposed) with hreflang pairs.
3. **Layout (desktop):** landing header (above) → `main` → dark footer.
   - **Hero**: padding 96/80/80; grid `minmax(0,1fr) 560px`, gap 64, vertically centered.
     - Text column (gap 24): eyebrow 15px/600 #AA4528; H1 52/72 bold; lead 20/34 #22262A max-width 34em; CTA row gap 12 with two buttons of min-h 52 and 17px.
     - Visual column: placeholder, height 420, radius 14, border, diagonal hatch `repeating-linear-gradient(135deg,#F2F1ED 0 12px,#FAF9F6 12px 24px)`. `role="img"`, `aria-label="مكان لقطة من مساحة عمل الحالة"`. Caption chip (LTR, mono): "product screenshot — case workspace (masked data)". Replace it with a real screenshot that uses masked data.
   - **"ما لا تقوم به رهون"** band: margin 0 80, white card, radius 14, padding 24/28. Grid `220px repeat(4,1fr)`, gap 24: H2 18/28, then 4 items, each with icon `do_not_disturb_on` (20px #5E5D58) and 15/24 text.
   - **"كيف تعمل"** (`id="how"`): padding 88/80. H2 36/50. Ordered list, grid 4 columns, gap 24. Each step has a 2px `#22262A` top border and padding-top 16: number (mono 600 14, #AA4528, LTR) → H3 20/30/600 → p 16/26.
   - **Audiences**: padding 0 80 88; grid 1fr 1fr, gap 24.
     - Lenders card: dark `#151513`, white text, padding 40, radius 14.
     - Owners card: white with border.
   - **"الحوكمة والخصوصية"** (`id="gov"`): white band with top border, padding 72/80. Grid `320px repeat(3,1fr)`, gap 32. Each item: icon 24 #AA4528, H3 18/600, p 15/24.
   - **Mobile (390)**:
     - Header 60 with logo 172 and menu button.
     - Content padding 32/20, gap 18: eyebrow 14px (shorter copy), H1 32/46, lead 17/29 (shorter copy).
     - Two full-width 52px buttons, stacked.
     - Condensed "ما لا تقوم به" card: radius 10, padding 16.
     - Below the fold is not drawn; assume the same sections stacked.
   - **Aside responsive rules**: at 1440 the hero has two columns. At 768 it becomes one column with the image under the text. At 390 buttons are full-width at 52px and the menu is a popover.
4. **Content (verbatim):**
   - Eyebrow (desktop): "منصة سعودية لإدارة حالات التمويل العقاري المتعثرة". Eyebrow (mobile): "منصة لإدارة حالات التمويل العقاري المتعثرة".
   - H1: "مسار واضح لكل حالة، من التقدير حتى الإغلاق الموثّق"
   - Lead (desktop): "تنسّق رهون العمل بين الجهة المموّلة ومالك العقار وفريق الحالة ومقدمي الخدمة، وتسجّل كل قرار بسببه ودليله. هدفنا الوصول إلى حل مناسب: تسوية أو بيع طوعي، أو إحالة منفصلة معتمدة عند الحاجة."
   - Lead (mobile): "تنسّق رهون العمل بين الجهة المموّلة ومالك العقار وفريق الحالة، وتسجّل كل قرار بسببه ودليله."
   - ما لا تقوم به رهون:
     - desktop items: "ليست محكمة ولا تصدر أحكاماً" · "لا تدير مزادات ولا تبيع العقارات علناً" · "ليست جهة تمويل ولا تقدّم قروضاً" · "لا ملكية جزئية ولا ترميز للأصول"
     - mobile single line: "ليست محكمة · لا مزادات · ليست جهة تمويل · لا ترميز للأصول"
   - كيف تعمل (`how` data):

     | n | title | text |
     |---|---|---|
     | 01 | إنشاء الحالة | تستورد الجهة المموّلة الحالة أو تنشئها يدوياً، مع فحص التكرار وتحديد المسؤول. |
     | 02 | التحقق والتقييم | مستندات بإصداراتها، وتقييم مستقل عبر مقدم خدمة مكلّف لمدة محددة. |
     | 03 | حل مبرر ومعتمد | يُعدّ المحلل الحل، ويراجعه آخر، ويعتمده معتمد مخوّل. كل إصدار محفوظ. |
     | 04 | رد المالك والإغلاق | يرى المالك خياراته بلغة واضحة ويقبل أو يقترح بديلاً، ثم تُطابق المبالغ وتُغلق الحالة بمستنداتها. |

   - **للجهات المموّلة** (H2 28/40). Checklist with icon `check` 22px #F4633A, 17/28:
     - محفظة واحدة بمهل ومسؤولين واضحين
     - فصل بين المُعِدّ والمعتمد وحدود موافقة قابلة للتهيئة
     - سجل تدقيق كامل لكل انتقال ومستند
     - عزل تام لبيانات كل منشأة

     CTA: white button-link "طلب عرض للمنشأة" (min-h 48, 700 16px).
   - **لملاك العقارات**:
     - p1 (17/30): "إذا وصلتك دعوة من جهتك المموّلة عبر رهون، فهذا يعني أن هناك فريقاً يعمل على حالتك. سترى الخطوة التالية بوضوح، والمستندات المطلوبة، والخيارات المتاحة، ويمكنك طلب المساعدة أو تقديم اعتراض في أي وقت."
     - p2 (15/24 #5E5D58): "لا نطلب منك أي دفعة عبر رهون، ولن نتواصل معك إلا من خلال الجهة المموّلة وقنوات موثّقة."
     - Link: "كيف أتحقق من صحة الدعوة؟"
   - **الحوكمة والخصوصية** (`gov` data):

     | icon | title | text |
     |---|---|---|
     | visibility_lock | عزل البيانات | لكل منشأة بياناتها. المالك يرى حالته فقط، ومقدم الخدمة يرى تكليفه فقط. |
     | history | سجل غير قابل للتعديل | كل انتقال وقرار وكشف بيانات يُسجل بفاعله ووقته وسببه. |
     | **rule** | حدود واضحة | لا تعرض رهون قراراً قضائياً ولا تتصرف بالنيابة عن جهة رسمية. |

     The script deliberately remaps `gavel_off` to **`rule`**, because gavel, key, roof and lock icons are banned.
5. **Actions:**
   - "طلب عرض للمنشأة" appears in the header, the hero and the lenders card, and goes to S02.
   - "لملاك العقارات: كيف نساعدك" goes to the owners section or page.
   - "تسجيل الدخول" goes to S03.
   - "English" switches locale.
   - Nav items go to anchors or pages.
   - "كيف أتحقق من صحة الدعوة؟" goes to an invite-verification help page (not designed).
   - Footer links go to legal and complaint pages (not designed).
   - Priority per the aside: the primary action is the demo request. Secondary actions are the owners page and login.
6. **States:** static page only.
7. **Rules (aside):**
   - One H1, hierarchical headings, real indexable text, AR+EN meta and hreflang.
   - Tone: "بلا صور تهديد أو مزادات أو مطارق أو مفاتيح. لا وعود بنتائج."
   - Section order: title and value → what Rahoon doesn't do → how it works → the two audiences → governance.
8. **Data/API:** none (static CMS content optional).
9. **Integrations:** none.

---

## S02 — طلب عرض للمنشأة / Institution demo request

1. **Frames:** `P1-Public-DemoRequest-Desktop-Default` (1440, form default with an email error shown); `P1-Public-DemoRequest-Mobile-Success` (390).
2. **Role:** anonymous (licensed lenders). **Route:** `/demo` (brief). Success state is inline, or `/demo/submitted` (proposed).
3. **Layout (desktop):**
   - Minimal header 80 with the "العودة للرئيسية" link at the end.
   - `main` padding 56/80, grid `minmax(0,1fr) 680px`, gap 64.
   - Info column (right in RTL): H1 40/56; p 18/30; numbered steps list with 28px black circles holding white numbers.
   - Form card: white, radius 14, padding 28. Grid 2 columns, gap 18/20.
     - Full-width rows: H2, org name, textarea, consent, submit.
     - Paired rows: type | portfolio size; name | job title; email | mobile.
   - Inputs are 44px high, 1px `#85847F` border, radius 6, labels 14/600, gap 6.
   - Mobile form is not drawn; the rule is one column on mobile.
4. **Content:**
   - H1 "طلب عرض للمنشأة". Intro: "للجهات المموّلة المرخّصة. يتواصل معك فريقنا خلال يومي عمل لتحديد موعد، ولن نطلب أي بيانات عن حالات أو عملاء في هذه المرحلة."
   - Steps: 1 "مراجعة الطلب والتحقق من الترخيص" · 2 "عرض بيانات تجريبية مخفية" · 3 "تسجيل المنشأة وإعدادها (بعد الاتفاق)".
   - Form H2 "بيانات المنشأة ومسؤول التواصل".

   | Label (verbatim) | Type | Req | Sample | Notes |
   |---|---|---|---|---|
   | اسم المنشأة * | text, full width | yes | مصرف الأفق | |
   | نوع المنشأة * | select | yes | بنك | only "بنك" shown; other options undefined (suggest بنك / شركة تمويل / …) |
   | حجم المحفظة المتعثرة التقريبي | select | no | 500 – 2,000 حالة | ranges not specified |
   | الاسم * | text | yes | ليلى الغامدي | |
   | المسمى الوظيفي * | text | yes | مديرة التحصيل | |
   | البريد المؤسسي * | email, `dir=ltr`, IBM Plex Sans, right-aligned | yes | l.alghamdi@gmail.com | **error shown**: 2px `#B3261E` border, `aria-invalid="true"`, message with `error` icon: "استخدم بريد المنشأة وليس بريداً شخصياً" (13px #B3261E) |
   | الجوال | tel, `dir=ltr`, mono, with prefix addon `+966` (bg #F2F1ED, mono 500 14) | no | 05• ••• ••12 | the sample value shows a masked phone |
   | ما الذي تودّون معرفته؟ | textarea, min-h 88, full width | no | نرغب في فهم مسار الموافقات وحدودها. | |
   | Consent checkbox | checkbox (shown checked: 20px black box with white `check`) | yes (implied) | — | "أوافق على استخدام بياناتي للتواصل بشأن هذا الطلب وفق [سياسة الخصوصية]." |

5. **Actions:**
   - "إرسال الطلب": primary, full width, min-h 48. It submits and shows the success state.
   - "العودة للرئيسية" returns to `/`.
   - The privacy-policy link.
   - Validation copy is shown for the email only. Other required-field messages are not specified; use standard C06 Field error plus an error summary that takes focus.
6. **States:**
   - Default / field error: the email case above.
   - Success (mobile frame). Mobile header 60 with logo, then a `role="status"` block with padding 48/24:
     - Icon `check_circle` 30px #1E6A45 in a 56px circle #EAF4EE.
     - H1 26/38 "وصلنا طلبك".
     - p: "رقم الطلب **DR-2026-0318**. سيتواصل معك فريقنا خلال يومي عمل على البريد المؤسسي." (the ref uses `bdi` LTR, mono 600).
     - p (15/24 #5E5D58): "لم تُنشأ أي حسابات بعد. لن نطلب بيانات عملاء قبل توقيع الاتفاقية."
     - Outline button-link "العودة للرئيسية" (min-h 48).
   - Loading and network-error states are not drawn.
7. **Rules:**
   - Reject free or personal email domains (gmail etc.).
   - No case or customer data is collected at this stage.
   - Submitting creates no account.
   - Follow-up promised within 2 business days by email.
   - Consent to the privacy policy is required.
   - Request ref format: `DR-YYYY-NNNN`.
8. **Entities/API:**
   - `DemoRequest` fields: `id`, `ref`, `orgName`, `orgType` (enum), `portfolioSizeBand` (enum), `contactName`, `jobTitle`, `workEmail`, `mobile` (E.164 +966), `message`, `consentAt`, `consentPolicyVersion`, `status` (new / reviewing / scheduled / closed), `createdAt`, `locale`.
   - `POST /api/public/demo-requests` returns `{ ref }`. Add server-side validation for the personal-domain blocklist, plus rate-limiting and CAPTCHA (recommended).
   - `GET /api/public/lookups/org-types` and `GET /api/public/lookups/portfolio-size-bands` (or static enums).
9. **Integrations:**
   - Free-email-domain blocklist.
   - Transactional email (acknowledgement and internal notification).
   - CRM (optional).
   - None of these is labelled in the design.

---

## S03 — تسجيل الدخول / Login (AR default, EN-LTR error variant)

1. **Frames:** `P1-Shared-Login-Desktop-Default` (1440 × 900, AR, password field focused); `P1-Shared-Login-Desktop-EN-LTR` (1440 × 900, EN, credential error).
2. **Roles:** lender teams, service providers, platform admins. Owners and debtors do **not** use a password; they use the invite link. **Route:** `/login` (brief).
3. **Layout:**
   - Full-screen grid `minmax(0,1fr) 560px`, no header.
   - Form column: centered, padding 48, form width 420, gap 18. Logo 176 with margin-bottom 12.
   - Dark aside, 560 wide, bg `#151513`, padding 64/56, content bottom-aligned:
     - App icon `rahoon-app-icon.svg` 72px, radius 14 (AR only).
     - Quote 24/38/600.
     - Footnote 14/22 `#B5B3AD`.
   - In RTL the form sits on the right and the aside on the left. EN mirrors this.
   - Inputs are 48px high.
   - **Responsive (aside):** at 768 and 390 only the form shows; the dark panel is hidden.
4. **Content (AR):**
   - H1 "تسجيل الدخول". Sub (15/24 #5E5D58): "لفرق الجهات المموّلة ومقدمي الخدمة وإدارة المنصة."
   - Field "البريد المؤسسي": email, `dir=ltr`, right-aligned, sample `s.alqahtani@alufuq.example`.
   - Field "كلمة المرور": password, with the link "نسيت كلمة المرور؟" on the label row (space-between). It has a 48px eye button (`aria-label="إظهار كلمة المرور"`, icon `visibility`). The focused state is a 2px #151513 border plus a 2px outline with offset 2.
   - Primary "متابعة" (min-h 48, full width).
   - Owner note, separated by a top border (icon `person`): "مالك عقار أو مدين؟ استخدم **رابط الدعوة** المرسل إليك من جهتك المموّلة. [كيف أتحقق من الدعوة؟]"
   - Footer links (13px): "English" (`lang=en`) · "الخصوصية" · "المساعدة".
   - Aside quote: "كل قرار بسببه، وكل مستند بإصداره، وكل خطوة بمسؤولها." Footnote: "جلسة آمنة · التحقق بخطوتين إلزامي لكل الحسابات المؤسسية."
   - **EN strings (verbatim):**
     - "Sign in" / "For lender teams, service providers and platform administrators."
     - Fields: "Work email", "Password", "Forgot password?", aria "Show password".
     - Button: "Continue".
     - Owner note: "Property owner? Use the **invitation link** sent by your lender." It has no verify link.
     - Footer links: "العربية" (`lang=ar`, Arabic font) · "Privacy" · "Help".
     - Aside: "Every decision with its reason. Every document with its version. Every step with its owner." / "Secure session · Two-step verification is required for all institutional accounts." There is no app icon in the EN aside.
5. **Actions:**
   - "متابعة" authenticates and goes to S04 MFA.
   - After MFA: S06 if the user has more than one organization, otherwise the default landing page.
   - "نسيت كلمة المرور؟" opens the reset flow (not designed).
   - The eye button toggles password visibility.
   - The language link, privacy, help, and the invite-verify help link.
6. **States (aside):** default, credential error with remaining attempts, wrong code, temporary lock, offline, success. Only default (AR) and error (EN) are drawn.
   - **Error (EN):** `role="alert"` box, bg `#FCECEA`, border `#EFA59C`, radius 10, padding 12/14, icon `error` #B3261E. Copy: "**Email or password is incorrect.** 3 attempts remain before a 15-minute lock." The password field gets a 2px `#B3261E` border. The alert sits between the email and password fields.
   - **The AR error copy is not designed.** Suggested wording in the same register: "البريد أو كلمة المرور غير صحيحة. تبقّى 3 محاولات قبل إيقاف مؤقت لمدة 15 دقيقة." Mark it as TBD copy.
7. **Security rules:**
   - Do not reveal whether an email is registered. The message and the attempt counter must behave identically for unknown emails.
   - MFA is mandatory for all institutional accounts.
   - **Lockout: 15 minutes after 5 failed attempts. The aside marks this as an assumption ("افتراض").**
   - The EN frame shows "3 attempts remain", which is consistent with 2 failures so far.
   - Switching to EN mirrors the layout but keeps the Arabic logo.
8. **Entities/API:**
   - `User` fields: `id`, `email`, `displayName`, `passwordHash`, `passwordChangedAt`, `locale`, `status`.
   - `LoginAttempt` / lockout counter fields: `userId|emailHash`, `ip`, `failedCount`, `lockedUntil`.
   - `POST /api/auth/login {email,password}` returns one of `{ mfaChallengeId, factorType, maskedDestination }`, `401 { remainingAttempts }` or `423 { lockedUntil }`.
   - `POST /api/auth/password/forgot`.
   - Security audit event: `login_failed`, `login_locked`.
9. **Integrations:**
   - Identity provider (ASP.NET Core Identity or an external IdP).
   - Breached-password service (used at S05).
   - No integration badges are shown.

---

## S04 — التحقق بخطوتين / MFA (mobile: default, error, locked)

1. **Frames:** `P1-Shared-MFA-Mobile-Default`, `…-Error`, `…-Locked` (all 390 × 844). **No desktop frame.** On desktop, reuse the S03 split layout with the MFA form in the form column (assumption).
2. **Role:** every institutional user after the password step. **Route:** `/login/mfa` (brief).
3. **Layout:**
   - Top bar 56 with a back button 44×44 (`aria-label="رجوع"`, icon `arrow_forward`, which is correct for RTL back).
   - `main` padding 8/24, gap 18:
     - Icon tile 48×48, radius 12, with a 26px icon.
     - H1 26/38.
     - Body 16/26.
     - Code group: `role="group"`, `aria-label="رمز من 6 أرقام"`, `dir="ltr"`, grid of 6 equal columns, gap 8. Each box is 56 high, radius 8, white, mono 600 22.
     - Hint line 13px with a 16px icon.
     - Button min-h 48, full width.
     - Alternate link 15/600, min-h 44.
4. **Content per state (from `mfaStates`):**

   | State | Icon (bg/fg) | Title | Body | Code boxes | Hint | Button | Alt link |
   |---|---|---|---|---|---|---|---|
   | Default | `sms` (#F2F1ED / #22262A) | أدخل رمز التحقق | أرسلنا رمزاً من 6 أرقام إلى +966 5• ••• ••81. | "4","8","1", then empty; the 4th (active) box has a 2px #151513 border, the others 1px #85847F | `schedule` "إعادة الإرسال بعد 00:42" (#5E5D58) | تحقق (primary #AA4528, white) | استخدام تطبيق المصادقة بدلاً من ذلك |
   | Error | `sms` | أدخل رمز التحقق | same | "481903", all with 2px #B3261E borders | `error` "الرمز غير صحيح. تبقّت محاولتان." (#B3261E) | تحقق | إعادة إرسال الرمز |
   | Locked | `lock_clock` (#FBF2DE / #8A5300) | أُوقف الدخول مؤقتاً | تجاوزت عدد المحاولات المسموح. يمكنك المحاولة بعد 15 دقيقة، أو التواصل مع مسؤول منشأتك. لم يتغير أي شيء في حسابك. | hidden | hidden | العودة لتسجيل الدخول (white bg, #151513 text) | التواصل مع الدعم |

5. **Actions:**
   - "تحقق" verifies the code, then goes to S06 or the default page.
   - The resend link appears only after the countdown ends. In the default state a countdown is shown; in the error state "إعادة إرسال الرمز" is offered.
   - Switch factor to the authenticator app (TOTP).
   - Back returns to login.
   - In the locked state: return to login, or contact support.
6. **States:**
   - Drawn: default, wrong code with remaining attempts, locked.
   - Also required by the aside: offline and success.
   - The resend cooldown is shown as `mm:ss`.
7. **Rules:**
   - Code length is 6 digits, displayed LTR.
   - **Implement as one logical input** with `autocomplete="one-time-code"` and paste support. The six boxes are visual only.
   - The phone number is masked as `+966 5• ••• ••81`.
   - Wrong code decrements the remaining attempts ("تبقّت محاولتان" means 2 left). After that the lock is 15 minutes.
   - The locked copy reassures the user that nothing in the account changed.
   - The default factor shown is SMS, with a TOTP alternative. S07 shows the primary factor as the authenticator app with SMS as backup. See Conflicts.
8. **Entities/API:**
   - `MfaFactor` fields: `id`, `userId`, `type` (totp | sms), `isPrimary`, `maskedPhone`, `verifiedAt`.
   - `MfaChallenge` fields: `id`, `userId`, `factorType`, `expiresAt`, `attemptsLeft`, `resendAvailableAt`.
   - `POST /api/auth/mfa/verify {challengeId, code}` returns a session, or `401 {attemptsLeft}` or `423 {lockedUntil}`.
   - `POST /api/auth/mfa/resend {challengeId}` returns `{resendAvailableAt}`.
   - `POST /api/auth/mfa/switch {challengeId, factorType}`.
9. **Integrations:**
   - SMS gateway (Saudi provider) for OTP.
   - TOTP (authenticator app).
   - Neither has an integration label in the design. Apply the handoff enum `enabled | simulated | pending | unavailable | failed` (for example, a sandbox SMS marked "محاكاة").

---

## S05 — قبول الدعوة / Invitation acceptance

1. **Frame:** `P1-Shared-InvitationAccept-Desktop-Default` (1440 × 900).
2. **Role:** an invited institutional user (this example is a lender "محلل ائتمان"). **Route:** `/invite/:token` (brief).
3. **Layout:**
   - Header 72, white, bottom border, logo 176, padding 0 48.
   - `main` is centered with padding 48. Content column 640, gap 20.
   - Invitation card: white, radius 14, padding 24, flex row:
     - Org avatar 48px, radius 8, #151513, text "أف".
     - Text stack.
     - Expiry badge (warning tone).
   - Name and password sit in a 2-column grid. The password requirements are a 2-column list. Inputs are 48px high.
   - **Responsive (aside):** at 390, cards are full width with touch targets of at least 72px (this refers to the S06 cards).
4. **Content:**
   - Card:
     - "دعوة من" (14 #5E5D58)
     - "**مصرف الأفق**" (18)
     - "للانضمام بدور **محلل ائتمان** · فريق التحصيل العقاري — الرياض"
     - Badge: icon `schedule` + "تنتهي `2026-09-30`" (12/600, #8A5300 on #FBF2DE, radius 4)
   - H1 "إنشاء حسابك".
   - "البريد المؤسسي": **disabled**, `dir=ltr`, bg #F2F1ED, border #CBCAC6, value `f.alotaibi@alufuq.example`. Help text: "محدد من الدعوة ولا يمكن تغييره".
   - "الاسم الكامل": text, sample "فهد العتيبي".
   - "كلمة المرور": password.
   - Password requirements list (`aria-label="متطلبات كلمة المرور"`, 14px). Met items use `check_circle` in #1E6A45; unmet items use `radio_button_unchecked` in #5E5D58.
     - "12 حرفاً على الأقل" (met)
     - "ليست ضمن كلمات مسرّبة" (met)
     - "لا تحتوي اسمك أو بريدك" (unmet)
   - Checkbox, shown unchecked: "أقر بسياسة الاستخدام المقبول وسرية بيانات العملاء، وأن جميع إجراءاتي تُسجّل."
5. **Actions:**
   - Primary "إنشاء الحساب وإعداد التحقق": creates the account, then moves to MFA enrolment (not designed).
   - Text button (underlined, #AA4528) "لا أعرف هذه الدعوة": reports the invitation as unexpected. It should notify the inviting org or platform security and invalidate the token; the confirmation screen is not designed.
   - Enable/disable: the design shows the primary button enabled while one requirement is unmet and the checkbox is unchecked. Recommended: keep it enabled, and on submit run validation with an error summary. Otherwise, disable it until all requirements are met plus the acknowledgement, and state the reason.
6. **States (aside):**
   - Valid invitation (drawn).
   - Expired: see S12 mobile (debtor variant). An institutional variant is not drawn.
   - Already used, and revoked by the organization. Neither is drawn; use the C11 SystemState pattern.
7. **Rules:**
   - The inviting org, role, team and expiry are visible **before any input**.
   - The email is locked to the invitation.
   - Password policy: at least 12 characters, not in a breached-password list, must not contain the user's name or email.
   - The acceptable-use and confidentiality acknowledgement is recorded with a timestamp.
   - MFA enrolment follows immediately.
8. **Entities/API:**
   - `Invitation` fields: `id`, `tokenHash`, `organizationId`, `roleId`, `teamId`, `email`, `invitedBy`, `expiresAt`, `status` (pending | accepted | expired | revoked | reported), `acceptedAt`.
   - `Membership` fields: `userId`, `organizationId`, `roleId`, `teamId`, `status`.
   - `AcceptanceRecord` fields: `policyVersion`, `acceptedAt`, `ip`.
   - `GET /api/invitations/{token}` returns org, role, team, expiry and masked email, or a status of expired/used/revoked.
   - `POST /api/invitations/{token}/accept {fullName,password,ackPolicy}`.
   - `POST /api/invitations/{token}/report`.
   - `POST /api/auth/password/check` for live requirement feedback (length, breached, contains name or email).
9. **Integrations:**
   - Breached-password check (for example, k-anonymity HIBP).
   - Email for sending the invitation.
   - Neither is labelled.

---

## S06 — اختيار الدور والمنشأة / Role & organization (workspace) selection

1. **Frame:** `P1-Shared-RoleOrgSelect-Desktop-Default` (**960** × 900). Treat it as a centered standalone page that is responsive at any width.
2. **Role:** users with more than one membership. The brief's location is "القائمة العلوية › المنشأة". The sidebar org switcher (`aria-haspopup="menu"`) is the in-app entry point. **Route (proposed):** `/select-workspace`, shown after MFA only when memberships > 1.
3. **Layout:**
   - Column centered, padding 64/48, gap 24.
   - Logo 176, then a 560-wide block with gap 16.
   - List: `role="radiogroup"` (`aria-label="المنشآت والأدوار"`), items with gap 10.
   - Each item: `role="radio"`, `aria-checked`, min-h 72, radius 10, padding 14/16, white.
     - Selected: 2px `#151513` border. Unselected: 1px `#CBCAC6`.
     - Contents: logo tile 44px radius 8; name 16 bold; role 14 #22262A; meta 12 #5E5D58; trailing `chevron_left` 22 #5E5D58.
4. **Content:**
   - H1 "اختر مساحة العمل".
   - p: "لديك صلاحيات في أكثر من منشأة. تعمل في منشأة واحدة كل مرة، ولا تظهر بيانات أي منشأة في الأخرى."
   - Options (`orgs` data):

   | abbr | tile bg/fg | name | role line | meta | checked |
   |---|---|---|---|---|---|
   | أف | #151513 / #FFFFFF | مصرف الأفق | مديرة حالات · فريق التحصيل العقاري | آخر استخدام اليوم 08:12 · MFA مفعّل | ✓ |
   | سن | #F2F1ED / #151513 | شركة السنبلة للتمويل | محللة ائتمان · قراءة فقط | آخر استخدام 2026-09-18 | |
   | رهـ | #FDF0EB / #8E3920 | إدارة منصة رهون | مدققة امتثال · وصول مقيد | يتطلب إعادة التحقق عند الدخول | |

   - Checkbox (unchecked): "تذكّر اختياري على هذا الجهاز".
5. **Actions:**
   - Selecting a row enters that workspace. There is no separate "continue" button; the chevron implies the row itself navigates. Keyboard behaviour: arrow keys move the selection and Enter/Space activates.
   - "Remember" persists the default workspace on this device.
   - Choosing "إدارة منصة رهون" triggers MFA re-verification first.
6. **States:** only the default is drawn.
7. **Rules:**
   - The screen appears only for users with more than one organization.
   - One organization per session. Switching later from the sidebar **re-opens the session and clears the cache**.
   - Platform administration **requires re-verification on every entry**.
   - Data never crosses organizations.
8. **Entities/API:**
   - `GET /api/me/memberships` returns `[ { organizationId, name, abbr, logoTone, roleLabel, teamName, accessLevel (full | read_only | restricted), lastUsedAt, requiresStepUp } ]`.
   - `POST /api/session/workspace {organizationId, remember}` returns a new session/token scoped to that organization, or `{ stepUpRequired }`.
   - `POST /api/auth/step-up`.
9. **Integrations:** identity provider (session re-issue).

---

## S07 — الملف والأمان والجلسات / Profile, security & sessions (tab: Security & sessions)

1. **Frame:** `P1-Shared-ProfileSecurity-Desktop-Sessions` (1440 × min 900).
2. **Role:** all logged-in users. The brief's location is "القائمة الشخصية". **Routes (proposed):** `/account/profile`, `/account/security` (drawn), `/account/notifications`, `/account/preferences`.
3. **Layout:**
   - Lender shell: sidebar `active=none`; crumbs "حسابي › الأمان والجلسات".
   - `main` padding 28/40/40, gap 20.
   - H2 "حسابي" (32/44).
   - Tabs.
   - 3 summary cards in a `repeat(3,1fr)` grid, gap 16. Each card: radius 14, padding 18/20, gap 8; label 14 #5E5D58; value 16 bold; outline button min-h 40.
   - Sessions card: header row padding 14/20 with a bottom divider #EDECE8. Below it, a table with a header row bg #FAF9F6 (600, #5E5D58) and rows separated by top borders #EDECE8. Grid `2fr 1.4fr 1.4fr 1.2fr 140px`, gap 12, row padding 12/20, 14px.
   - Info footnote 13px #5E5D58 with icon `info`.
   - **Responsive (aside):** at 390, each session becomes a card with a full-width end button.
4. **Content:**
   - Tabs: الملف الشخصي · **الأمان والجلسات** (selected) · تفضيلات الإشعارات · اللغة والعرض.
   - Cards:
     - "كلمة المرور" → "غُيّرت قبل 41 يوماً", button "تغيير".
     - "التحقق بخطوتين" → "مفعّل · تطبيق المصادقة" (#1E6A45 with icon `verified_user`). Sub (13px): "احتياطي: رسالة إلى `+966 5• ••• ••81`".
     - "رموز الاسترداد" → "7 من 10 متبقية", button "إنشاء رموز جديدة…". Per the handoff, the trailing "…" means the action opens a review/confirmation screen.
   - Section H3 "الجلسات النشطة". Danger outline button "إنهاء كل الجلسات الأخرى" (1px #B3261E border, #B3261E text).
   - Table `aria-label="الجلسات النشطة"`. Columns: الجهاز والمتصفح · الموقع التقريبي · آخر نشاط · المنشأة · (actions).

   | icon | device | tag (color) | location | last activity | org | action |
   |---|---|---|---|---|---|---|
   | laptop_windows | Windows · Edge | هذه الجلسة (#1E6A45) | الرياض، SA | الآن | مصرف الأفق | **none** (button hidden) |
   | phone_iphone | iPhone · Safari | — | الرياض، SA | 2026-09-23 07:40 | مصرف الأفق | إنهاء |
   | laptop_mac | macOS · Chrome | موقع غير معتاد (#8A5300) | جدة، SA | 2026-09-22 22:15 | شركة السنبلة | إنهاء |

   The "إنهاء" button is min-h 36, 1px #85847F border, 13px/600.
   - Footnote: "تنتهي الجلسة بعد 30 دقيقة خمول (قابلة للتهيئة من المنشأة). إنهاء أي جلسة يُسجَّل في سجل الأمان."
5. **Actions:**
   - "تغيير" opens the change-password flow.
   - "إنشاء رموز جديدة…" opens a review step, then generates 10 new codes and invalidates the old ones.
   - "إنهاء" ends that session.
   - "إنهاء كل الجلسات الأخرى" opens **a short confirmation screen with MFA re-entry**, then ends all sessions except the current one.
   - Other tabs switch views.
6. **States:** only this tab is drawn. Loading and error follow C11.
7. **Rules:**
   - The current session **cannot** be ended from here.
   - "موقع غير معتاد" is conveyed by **both text and colour**.
   - Location is approximate, **city level only**.
   - The sessions list spans all the user's organizations (the "المنشأة" column).
   - Idle timeout is 30 minutes, configurable per organization.
   - Every session termination is written to the security log.
   - The MFA backup phone is masked.
8. **Entities/API:**
   - `Session` fields: `id`, `userId`, `organizationId`, `deviceType` (windows | mac | iphone | android…), `browser`, `approxCity`, `countryCode`, `lastActivityAt`, `isCurrent`, `isUnusualLocation`, `createdAt`, `revokedAt`, `revokedReason`.
   - `RecoveryCode` fields: `userId`, `codeHash`, `usedAt`, `batchId`.
   - `SecurityAuditEvent` fields: `id`, `userId`, `orgId`, `type`, `ip`, `at`, `meta`.
   - Operations:
     - `GET /api/me/security` returns `{passwordChangedAt, mfa:{primaryType, backup:{type, maskedPhone}}, recoveryCodes:{remaining,total}}`.
     - `GET /api/me/sessions`.
     - `DELETE /api/me/sessions/{id}`.
     - `POST /api/me/sessions/revoke-others` (requires a step-up MFA token).
     - `POST /api/me/password`.
     - `POST /api/me/recovery-codes` (step-up).
     - Org setting: `idleTimeoutMinutes`.
9. **Integrations:** Geo-IP (city-level) for the unusual-location heuristic; SMS for the backup factor. Neither is labelled.

---

## S08 — مركز الإشعارات / Notification center (desktop popover and mobile full page)

1. **Frames:** `P1-Shared-NotificationCenter-Desktop-Open` (1440 × 900); `P1-Shared-NotificationCenter-Mobile` (390 × 844).
2. **Role:** all logged-in users. Opened from topbar › bell (brief). **Routes:** the popover has no route; `/notifications` (proposed) serves both the full center ("فتح مركز الإشعارات") and mobile.
3. **Layout:**
   - **Desktop:** lender shell (sidebar `active=portfolio`, crumb "المحفظة"). Page content is dimmed with `rgba(21,21,19,.04)`, with no scrim.
   - The popover is `role="dialog"` with `aria-label="الإشعارات"`. It is anchored `top:68px; left:32px` (the bell's side), width 440, max-h 800, radius 14, shadow `0 16px 40px rgba(21,21,19,.14), 0 0 0 1px rgba(21,21,19,.06)`.
   - Popover sections:
     - Header: padding 16/18/0; title 17 bold; text button (13/600 #AA4528) and a 36px settings icon button.
     - Tabs: padding 8/10/0, min-h 40.
     - List items: grid `32px minmax(0,1fr) 10px`, gap 10, padding 12/18.
     - Group labels: 12/700 #5E5D58, padding 10/18/4.
     - Footer link: centered 14/600 with a top border.
   - **Mobile:** full page, white.
     - Top bar 56: back button (`arrow_forward`), title "الإشعارات" 18 bold, text button "قراءة الكل".
     - Items: padding 14/16 with bottom dividers, text 15/22. They show the time only (no ref) and have no group headers.
     - No tabs are shown.
     - It shows the first 5 items.
4. **Content:**
   - Header: "الإشعارات" · "تعليم الكل كمقروء" · settings (`aria-label="إعدادات الإشعارات"`).
   - Tabs: "**تحتاج إجراء** 3" (selected, default) · "الكل" · "إشارات إليّ".
   - Items (`N` data):

   | group | icon | fg / bg | text | ref | time | unread |
   |---|---|---|---|---|---|---|
   | اليوم | approval | #8A5300 / #FBF2DE | أعاد نورة الشهري الحل v2 إلى «موافقة داخلية» بملاحظة | RH-2026-004172 | قبل 12 د | ✓ |
   | | upload_file | #1D5A8C / #EAF2F9 | رفعت المالكة كشف حساب جديداً للمراجعة | RH-2026-003988 | قبل ساعة | ✓ |
   | | alarm_off | #B3261E / #FCECEA | تجاوزت مهمة «متابعة رد المالكة» مهلتها | RH-2026-003988 | 09:00 | ✓ |
   | أمس | alternate_email | #22262A / #F2F1ED | أشار إليك فهد العتيبي في ملاحظة على الحل v2 | RH-2026-004172 | 16:08 | |
   | | check_circle | #1E6A45 / #EAF4EE | تمت مطابقة دفعة بمرجع بنكي مستورد | RH-2026-003870 | 11:30 | |
   | | person_add | #22262A / #F2F1ED | أُسندت إليك حالة جديدة | RH-2026-004233 | 09:15 | |

   - Item visuals: icon circle 32 with an 18px icon. Unread items are weight 700 and show an 8px dot `#AA4528` (`aria-label="غير مقروء"`). Read items are weight 400, with a transparent dot and `aria-label="مقروء"`. The meta line is "`ref` (mono, LTR) · time", 12px #5E5D58.
   - Footer link: "فتح مركز الإشعارات".
5. **Actions:**
   - Clicking an item opens the target (case, task, etc.) and marks the item read.
   - "تعليم الكل كمقروء" / "قراءة الكل" marks all as read.
   - The settings button opens S07 › تفضيلات الإشعارات.
   - Tabs filter the list.
   - "فتح مركز الإشعارات" goes to the full page.
   - Esc closes the popover and returns focus to the bell.
6. **States (aside):** no results, loading and connection error. None has copy in the design. Live updates use `aria-live="polite"`.
7. **Rules:**
   - The default tab is "تحتاج إجراء", then "الكل" and "إشارات إليّ".
   - Items are grouped by time (اليوم / أمس / …).
   - Unread is shown by a bolder weight, a dot **and** screen-reader text.
   - Notifications are scoped to the active organization. Masking applies: actors are named staff, and the owner is "المالكة", never named.
8. **Entities/API:**
   - `Notification` fields: `id`, `userId`, `organizationId`, `type` (approval_returned | document_uploaded | sla_breached | mention | payment_matched | case_assigned …), `requiresAction` (bool), `isMention`, `text` or template key + params, `caseRef`, `targetUrl`, `createdAt`, `readAt`.
   - Operations:
     - `GET /api/notifications?filter=action|all|mentions&cursor=`
     - `GET /api/notifications/unread-count` (drives the bell badge)
     - `POST /api/notifications/{id}/read`
     - `POST /api/notifications/read-all`
     - A realtime channel (SignalR) for live updates.
9. **Integrations:** none. Email/SMS delivery preferences belong to S07's notification-preferences tab, which is not designed.

---

## S09 — المهام الشخصية ومهام الفريق / Personal & team tasks

1. **Frame:** `P1-Shared-Tasks-Desktop-Mine` (1440 × min 960).
2. **Role:** lender team members; the workload panel is for team managers. Location "القائمة › مهامي". **Route (proposed):** `/tasks?tab=mine|team|unassigned|completed`.
3. **Layout:**
   - Lender shell (sidebar `active=tasks`, crumb "مهامي").
   - `main` grid `minmax(0,1fr) 320px`, gap 24.
   - Left column (gap 16):
     - Title row: H2 "المهام" (flex 1), then the outline button "مهمة يدوية" (min-h 40, icon `add`).
     - Tabs.
     - Group cards: white, radius 14. Each has a header strip bg #FAF9F6, padding 10/18, with icon, label and count.
     - Rows: grid `24px minmax(0,1fr) 170px 120px`, gap 14, padding 12/18, top divider #EDECE8.
       - Circular checkbox, 20px, 1.5px #85847F border.
       - Title 15 bold and source 12 #5E5D58.
       - Ref link in mono 600 13.
       - Due date 13/600, coloured with the group colour.
   - Aside (gap 16):
     - Workload card: radius 14, padding 16/18.
     - Info note: bg #FAF9F6, radius 10, padding 14, 13/20.
   - **Responsive (aside):** at 390, groups are collapsible and workload moves to a separate tab.
4. **Content:**
   - Tabs: "**مهامي** 7" (selected) · "مهام الفريق 64" · "غير مسندة 5" · "مكتملة".
   - Groups (`taskGroups`):

   | Group | icon | colour | count | task | source | ref | due |
   |---|---|---|---|---|---|---|---|
   | متأخرة | alarm_off | #B3261E | 1 | متابعة رد المالكة على العرض | آلية · من انتقال «بانتظار العميل» | RH-2026-003988 | متأخر 3 أيام |
   | خلال يومين | alarm | #8A5300 | 3 | مراجعة الحل v2 وإرساله للموافقة | آلية · أعدّه فهد العتيبي | RH-2026-004172 | 2026-09-25 |
   | | | | | مراجعة رد على العرض المقابل | آلية · من المالكة | RH-2026-004012 | 2026-09-25 |
   | | | | | التحقق من إيصال السداد | يدوية · ريم الدوسري | RH-2026-003870 | 2026-09-24 |
   | هذا الأسبوع وبعده | schedule | #22262A | 3 | مراجعة السجل التجاري المرفوع | آلية | RH-2026-004201 | 2026-09-27 |
   | | | | | تجديد صورة الهوية قبل انتهائها | آلية · تنبيه صلاحية | RH-2026-004172 | 2026-10-05 |
   | | | | | جدولة مكالمة مع المالك | يدوية · خالد ز. | RH-2026-004172 | 2026-09-27 |

   - Workload card title: "عبء الفريق" plus "(لمديري الفرق)" (400, 13 #5E5D58). Each row shows name, then "`n` · `late` متأخرة" (the late count in #B3261E), then an 8px bar: track #F2F1ED, fill #22262A, width `round(n/45*100)%`. Row `aria-label`: "`{name}: {n} مهمة، {late} متأخرة`".

   | name | tasks | late |
   |---|---|---|
   | سارة القحطاني | 38 | 1 |
   | خالد الزهراني | 41 | 4 |
   | فهد العتيبي | 29 | 0 |
   | ريم الدوسري | 22 | 2 |

   - Info note: "المهام الآلية تنشأ من انتقالات الحالة ولا تُحذف؛ تُغلق عند تحقق شرطها."
5. **Actions (aside):**
   - "مهمة يدوية" creates a manual task (form not designed).
   - Completing a **manual** task uses the circular checkbox.
   - The ref link opens the case.
   - Reassign is for managers only (UI not designed).
   - Tabs filter the list.
6. **States (aside):**
   - No tasks: "رسالة إيجابية بلا زخرفة" (a positive message with no decoration; copy not given).
   - Loading and error.
7. **Rules:**
   - Source is either **آلية** (created by a case state transition) or **يدوية** (created by a person).
   - Automated tasks cannot be deleted. They close when their condition is met, so the checkbox should be disabled or read-only for them, with a reason tooltip.
   - Grouping by due date: overdue / within 2 days / this week and later.
   - Overdue items show relative text ("متأخر 3 أيام"). The others show an ISO date.
   - The workload panel is visible only to team managers.
8. **Entities/API:**
   - `Task` fields: `id`, `ref` (`TSK-4172-09` format, seen in S10), `organizationId`, `caseId`/`caseRef`, `title`, `source` (auto | manual), `originTransition`, `originActor`, `assigneeId` (nullable means unassigned), `teamId`, `dueAt`, `status` (open | done | closed_auto), `completionCondition`, `createdBy`, `completedAt`.
   - Operations:
     - `GET /api/tasks?scope=mine|team|unassigned|completed` returns groups with counts.
     - `POST /api/tasks` (manual).
     - `POST /api/tasks/{id}/complete` (manual only; 409 with a reason for auto tasks).
     - `POST /api/tasks/{id}/reassign` (manager).
     - `GET /api/teams/{id}/workload`.
9. **Integrations:** none.

---

## S10 — البحث الشامل / Global search (command palette)

1. **Frame:** `P1-Shared-GlobalSearch-Desktop-Results` (1440 × 900). The query "4172" is shown with results.
2. **Role:** lender team. Opened from the topbar search trigger or **Ctrl/⌘ K**. **Route:** none (overlay). A mobile full-screen route `/search` is proposed.
3. **Layout:**
   - Lender shell (sidebar `active=cases`, crumb "الحالات") under a full scrim `rgba(21,21,19,.4)`.
   - Dialog (`role="dialog"`, `aria-label="البحث الشامل"`): width 720, centered horizontally, 96px from the top, radius 14.
   - Combobox row (`role="combobox" aria-expanded="true"`): height 60, padding 0 20, bottom border. Contains the `search` icon, the query text (mono 500 18, LTR) and `<kbd>Esc</kbd>`.
   - Scope bar: bg #FAF9F6, 12px #5E5D58, icon `shield`.
   - `role="listbox"` with group labels (12/700) and options (`role="option"`, min-h 52, padding 0 20):
     - icon 20 #5E5D58
     - title 14/600 and meta 12 #5E5D58
     - trailing hint 12
     - The selected option has bg `#FDF0EB` and `box-shadow: inset -3px 0 0 #F4633A`.
   - Footer: 12px #5E5D58 with a top border.
   - **Responsive (aside):** at 390, search is a full screen with the keyboard open.
4. **Content:**
   - Scope bar: "النتائج ضمن مصرف الأفق وحدود صلاحيتك فقط · الهويات والأسماء مخفية"
   - Results (`results`):

   | group | icon | title | meta | hint | selected |
   |---|---|---|---|---|---|
   | الحالات | folder_open | RH-2026-004172 | عبدالله م. · حل مقترح · سارة ق. | Enter | ✓ |
   | | folder_open | RH-2026-004172-A (حالة مرتبطة مغلقة) | مغلقة 2025-12-04 | | |
   | المستندات | description | تقرير التقييم v1 — RH-2026-004172 | مكتب «ب» · 2026-09-10 | | |
   | | description | كشف الراتب v2 — RH-2026-004172 | متحقق 2026-09-20 | | |
   | المهام | task_alt | TSK-4172-09 · مراجعة الحل v2 | مسندة إليك · يومان | | |
   | العقود | receipt_long | MF-88-3317••• (4172) | مرتبط بحالة واحدة | | |

   - Footer: "`↑↓` تنقّل" · "`Enter` فتح" · "ابحث بالمرجع، رقم العقد، رقم المهمة، أو اسم المستند".
5. **Actions:**
   - Typing searches.
   - ↑/↓ move through options; Enter opens the selected one.
   - Esc closes the dialog and **returns focus to where it was**.
   - Clicking an option opens it.
6. **States (aside):**
   - No results: suggest searching by contract number (copy not given).
   - Loading and connection error.
7. **Rules:**
   - Results are scoped to the active organization **and** the user's permissions.
   - Never reveal that a case outside that scope exists.
   - A partial ref such as "4172" is enough.
   - Identities and names are masked: owner "عبدالله م.", staff "سارة ق.", contract `MF-88-3317•••`, valuer "مكتب «ب»".
   - The masking is done server-side.
8. **Entities/API:**
   - `GET /api/search?q=&types=cases,documents,tasks,contracts&limit=` returns groups of `{type, id, title, meta, url}`.
   - Server-side filtering by `organizationId` plus authorization policy.
   - Pre-masked fields.
   - Optionally a debounce plus an audit of searches.
9. **Integrations:** none. The search index can be Postgres FTS or trigram.

---

## S11 — المساعدة والدعم / Help & support

1. **Frame:** `P1-Shared-HelpSupport-Desktop-Default` (1440 × min 960).
2. **Role:** all logged-in users (this frame shows a lender). Location "القائمة › المساعدة" (the sidebar footer link). **Routes (proposed):** `/help`, `/help/:topic`, `/help/tickets/:ref`.
3. **Layout:**
   - Lender shell (sidebar `active=none`, crumb "المساعدة والدعم").
   - `main` padding 32/40/40, gap 24.
   - Hero block, max-width 720: H2 32/44 and a search field (height 52, 1px #85847F, radius 6, white, `search` icon, 16px placeholder #5E5D58).
   - Topic grid: `repeat(3,1fr)`, gap 16. Each topic is a card-link: radius 14, padding 18/20, icon 24 #AA4528, title 16 bold, description 14/22 #5E5D58.
   - Two-column grid `1fr 1fr`, gap 16: support form card (padding 20, gap 12) and the "طلباتي" list card.
4. **Content:**
   - H2 "كيف يمكننا المساعدة؟". Search placeholder: "ابحث في الأدلة: حدود الموافقة، إعادة إسناد…".
   - Topics (`helpTopics`):

   | icon | title | description |
   |---|---|---|
   | folder_open | إدارة الحالات | الإنشاء، الاستيراد، الإسناد، العروض المحفوظة |
   | approval | الموافقات | المُعِدّ والمعتمد، الحدود، الإصدارات |
   | description | المستندات | الطلب، الرفع، الصلاحية، الإصدارات |
   | shield_person | الخصوصية والإخفاء | من يرى ماذا، وكشف البيانات المسجل |
   | support_agent | الشكاوى | استلام شكوى المالك ومعالجتها |
   | keyboard | الاختصارات وإمكانية الوصول | لوحة المفاتيح وقارئ الشاشة والتكبير |

   - Support form, "فتح طلب دعم":
     - "نوع المشكلة": select, sample "لا يظهر زر الإرسال للموافقة". The options list is not specified.
     - "مرجع الحالة (اختياري)": text, LTR, mono, sample `RH-2026-004172`.
     - Info callout: bg #EAF2F9, border #9DC0DE, radius 10, icon `shield_person` #1D5A8C, 13/20. Copy: "فريق دعم رهون لا يرى بيانات الحالة. إن احتاج ذلك، سيطلب **وصولاً مؤقتاً مدققاً** يوافق عليه مسؤول منشأتك، وينتهي تلقائياً."
     - Primary "إرسال الطلب" (min-h 44).
   - "طلباتي" list:

   | title | ref · date | status badge |
   |---|---|---|
   | تعذر رفع ملف PDF أكبر من 20 م.ب | SUP-2026-1187 · 2026-09-21 | بانتظار ردك (info: #1D5A8C on #EAF2F9) |
   | طلب إضافة قالب رسالة جديد | SUP-2026-1090 · 2026-09-08 | مغلق · حُل (success: #1E6A45 on #EAF4EE) |

5. **Actions:**
   - Search the guides.
   - Open a topic.
   - Submit a ticket.
   - Open a ticket from "طلباتي" (the detail page is not designed; "بانتظار ردك" implies a reply thread).
6. **States:** not drawn. Use C11 (loading, empty "طلباتي", error, submission success).
7. **Rules:**
   - The ticket may optionally be linked to a case ref.
   - Rahoon support **cannot see case data**. If needed, support requests *temporary, audited access* that the org admin approves and that auto-expires.
   - Sample data implies an upload limit of 20 MB for PDFs.
8. **Entities/API:**
   - `SupportTicket` fields: `id`, `ref` (`SUP-YYYY-NNNN`), `organizationId`, `requesterId`, `issueType`, `caseRef?`, `description?`, `status` (open | awaiting_customer "بانتظار ردك" | resolved "مغلق · حُل" | …), `createdAt`.
   - `SupportAccessGrant` fields: `ticketId`, `requestedBy`, `approvedBy` (org admin), `scope`, `expiresAt`, `revokedAt`.
   - `HelpArticle` / `HelpTopic` fields: `slug`, `title`, `summary`, `body`, `locale`.
   - Operations:
     - `GET /api/help/topics`
     - `GET /api/help/articles?q=`
     - `POST /api/support/tickets`
     - `GET /api/support/tickets?mine=true`
     - `POST /api/support/access-grants/{id}/approve` (org admin)
9. **Integrations:** helpdesk system (optional, not labelled).

---

## S12 — رفض الوصول / Access denied (desktop 403) and expired link (debtor mobile)

1. **Frames:** `P1-Shared-AccessDenied-Desktop-Default` (1440 × 800); `P1-Shared-AccessDenied-Mobile-ExpiredLink` (390 × 844).
2. **Roles:**
   - Desktop: any institutional user (this example is "مديرة حالات" at مصرف الأفق).
   - Mobile: debtor/owner arriving via an expired invitation link.
   - **Route:** the 403 state is a system state rendered in place of the requested page (brief: "حالة نظام"). The expired link is rendered at `/invite/:token` or at the debtor-link route when the token has expired. `/link-expired` is proposed.
3. **Layout:**
   - **Desktop:** lender shell (sidebar `active=cases`, crumb "الحالات"). `main` is centered, padding 40. A `role="status"` block 560 wide, gap 16:
     - Icon circle 56 (#EAF2F9) with `visibility_off` 30px (#1D5A8C). The info tone is used, not the error tone.
     - H1 28/40, p 17/28.
     - Button row, gap 12.
     - Meta line 13px #5E5D58.
   - **Mobile:** public header 60 with logo 172, then `main role="status"` with padding 48/24, gap 16:
     - Icon circle 56 (#FBF2DE) with `link_off` (#8A5300).
     - H1 26/38, p 17/29.
     - Full-width primary button, min-h 52.
     - Link min-h 44.
4. **Content:**
   - Desktop:
     - H1 "لا يمكنك فتح هذه الصفحة".
     - p: "قد لا تكون موجودة، أو أنها خارج صلاحياتك في **مصرف الأفق** بدور **مديرة حالات**. لم نعرض أي بيانات عنها."
     - Buttons: primary "العودة إلى الحالات"; secondary "طلب وصول من المسؤول".
     - Meta: "رمز الخطأ `403-SCOPE` · `2026-09-23 11:04` · يُسجّل الطلب في سجل الأمان".
   - Mobile:
     - H1 "انتهت صلاحية هذا الرابط".
     - p: "لحمايتك، روابط الدعوة صالحة لمدة محددة. يمكنك طلب رابط جديد، وسيُرسل إلى الجوال المسجل لدى جهتك المموّلة."
     - Primary "طلب رابط جديد". Link "التواصل مع الجهة المموّلة".
5. **Actions:**
   - "العودة إلى الحالات" goes to the section the user came from or its parent list.
   - "طلب وصول من المسؤول" sends an access request to the org admin **with a reason**. The reason-input dialog is not designed.
   - "طلب رابط جديد" asks the lender or platform to re-send the link to the **registered** mobile. The success state is not designed.
   - "التواصل مع الجهة المموّلة" shows lender contact details.
6. **States:** denied (desktop) and expired link (mobile). The "used" and "revoked" invitation variants use the same pattern.
7. **Rules (aside):**
   - Use the same wording ("قد لا تكون موجودة أو أنها خارج صلاحياتك") for **every** case, so a user cannot infer that another organization's case exists. The API must return indistinguishable responses for not-found and forbidden.
   - Show no data about the resource.
   - Log every denied attempt with its error code. The code format is `403-SCOPE`, plus a timestamp.
   - For expired debtor links, a new link can be sent **only to the registered mobile**. The user cannot enter a new number.
8. **Entities/API:**
   - `AccessDeniedEvent` (security audit) fields: `userId`, `orgId`, `resourceType`, `resourceIdHash`, `code`, `at`.
   - `AccessRequest` fields: `id`, `requesterId`, `orgId`, `resourcePath`, `reason`, `status`, `decidedBy`, `decidedAt`.
   - `POST /api/access-requests`.
   - `POST /api/invitations/{token}/resend` (rate-limited; always responds generically).
   - The ProblemDetails response carries `code` and `correlationId`.
9. **Integrations:** SMS for re-sending the link (not labelled).

---

## Reusable components introduced or used

| Component | Props / content | Variants and states in B2 |
|---|---|---|
| `LenderSidebar` | `active`, `userName`, `userRole`, `initials`, `approvals` (badge) | active ∈ portfolio/cases/tasks/approvals/complaints/reports/none |
| `LenderTopbar` | `crumb1`, `crumb2?`, unread count (hardcoded 4) | with or without crumb2 |
| `PublicHeader` / `PublicFooter` | nav links, locale link, CTAs | full (landing), minimal with back link (S02), logo-only (S05, S12m); mobile with menu |
| `AuthSplitLayout` | form slot, dark aside (icon?, quote, footnote) | AR / EN (no icon) |
| `Field` (C06) | label, input, help, error, prefix addon (`+966`), `dir` | default, focus (2px + outline), error (2px #B3261E + message), disabled/locked (bg #F2F1ED) |
| `Select` (fake) | label, value, `expand_more` | default |
| `Checkbox` | label with inline link | checked (black box, white check), unchecked |
| `PasswordRequirements` | list of {text, met} | met (green check_circle), unmet (grey radio_button_unchecked) |
| `OtpInput` | length 6, `autocomplete=one-time-code` | default/active box, filled, error |
| `Button` (C01) | primary (#AA4528), secondary/outline (1px #85847F), text (#AA4528, optionally underlined), danger-outline (#B3261E), inverted (white on dark) | heights 36 / 40 / 44 / 48 / 52 |
| `Alert` | icon, bold lead, text | error (#FCECEA / #EFA59C); info callout (#EAF2F9 / #9DC0DE) |
| `Badge / Tag` | icon?, text | warning (expiry), info (بانتظار ردك), success (مغلق · حُل), text-only coloured tags (هذه الجلسة / موقع غير معتاد) |
| `Tabs` | label + optional count | selected underline #F4633A |
| `InvitationCard` | org abbr, org name, role, team/city, expiresAt | — |
| `WorkspaceOption` (radio card) | abbr, tile colours, name, role, meta, checked | selected / unselected |
| `SummaryCard` | label, value, action? | — |
| `SessionsTable` | rows {icon, device, tag, loc, last, org, canEnd} | current (no action), normal, unusual |
| `NotificationItem` | icon + tone, text, ref, time, unread | unread (bold + dot), read; group header |
| `NotificationPopover` | header actions, tabs, list, footer link | desktop popover / mobile page |
| `CommandPalette` | query, scope note, grouped options, key hints | option selected / idle |
| `TaskGroup` + `TaskRow` | group {icon, label, count, tone}; row {title, source, ref, due} | overdue / soon / later tones |
| `WorkloadBar` | name, n, late, max | — |
| `TopicCard` | icon, title, description | link card |
| `SystemState` (C11) | icon + tone circle 56, h1, body, actions, meta | forbidden (info tone), expired link (warning), success (success tone) |
| `StepList` | numbered black circles 28px | — |

## Design tokens and colours

- **Palette tokens used** (all in Foundations):

  | Colour | Role |
  |---|---|
  | `#151513` | text.primary / inverted surface |
  | `#22262A` | charcoal |
  | `#5E5D58` | secondary text as used |
  | `#85847F` | border.strong |
  | `#CBCAC6` | border.default |
  | `#FAF9F6` | surface.warm |
  | `#F2F1ED` | surface.subtle |
  | `#FFFFFF` | white |
  | `#AA4528` | brand.rust (primary, links) |
  | `#8E3920` | rust.700 |
  | `#FDF0EB` | rust.50 (selected) |
  | `#F4633A` | brand.orange (accents only) |
  | `#1E6A45` / `#EAF4EE` | success |
  | `#8A5300` / `#FBF2DE` | warning |
  | `#B3261E` / `#FCECEA` / `#EFA59C` | error |
  | `#1D5A8C` / `#EAF2F9` / `#9DC0DE` | info |
  | `#B5B3AD` | inverted secondary |

- **Not in the Foundations palette:**
  - `#EDECE8`: light divider inside cards and lists. Add it as `border.subtle`.
  - `#E7E6E1`: design-canvas background only; do not implement.
  - `rgba(21,21,19,.4)`: modal scrim.
  - `rgba(21,21,19,.04)`: content dim behind the popover.
  - Shadow `0 16px 40px rgba(21,21,19,.14)`, plus `0 0 0 1px rgba(21,21,19,.06)` on the popover.
  - Hatch placeholder gradient (S01).
- **Radii:** 4 (badges, kbd), 6 (controls), 8 (avatar tiles, OTP boxes), 10 (callouts, radio cards), 12 (MFA icon tile), 14 (cards, dialogs), 50% (icon circles).
- **Type scale:**

  | Use | Size / line-height |
  |---|---|
  | Landing H1 | 52/72 |
  | Landing H2 | 36/50, 28/40 |
  | Landing lead | 20/34 |
  | Demo page H1 | 40/56 |
  | App page title | 32/44 |
  | Auth H1 | 32/44 (login), 28/40 (invite, S06, S12) |
  | Mobile H1 | 26/38 (32/46 on the landing page) |
  | Body | 14–17 |
  | Metadata | 12–13, minimum 12 |

## Conflicts and ambiguities

1. **Frame count.** The footer claims 13 desktop frames, but 12 are drawn.
2. **Unread counts disagree.** The bell says "4 غير مقروءة", while the notifications list has 3 unread items and the tab says "تحتاج إجراء 3".
3. **"تحتاج إجراء" tab shows all items.** It is selected, yet the list includes read and non-actionable items (a mention, a payment match). The drawn list may actually be "الكل"; decide the filter semantics.
4. **"Mark all read" wording differs.** Desktop says "تعليم الكل كمقروء"; mobile says "قراءة الكل". Mobile also has no tabs and no refs.
5. **Default MFA factor.** S04 defaults to an SMS code with an authenticator alternative. S07 says the primary factor is the authenticator app and SMS is backup. The challenge should default to the user's primary factor.
6. **Attempt counters.**
   - Login: "3 attempts remain before a 15-minute lock" (EN), with an assumed 5 attempts.
   - MFA: "تبقّت محاولتان".
   - It is unclear whether password and OTP use separate counters or one shared counter. The lock threshold is explicitly an **assumption**.
7. **Arabic login error copy is missing.** Only the EN frame shows the error.
8. **EN login differs from AR.** It omits the "How do I verify the invitation?" link and the app icon, and its owner note leaves out "or debtor".
9. **S05 primary button shows as enabled** while a password requirement is unmet and the acknowledgement is unchecked. Enabled vs disabled needs a decision.
10. **S06 has no confirm button.** Selecting a row presumably navigates, and "remember" must be ticked before clicking. The 960 frame width is non-standard.
11. **S04 locked-state button** is a white-background button with no border (`border:0`) on a #FAF9F6 page. It should probably be a standard secondary/outline button.
12. **`lock_clock` icon** is used in the MFA locked state, but the brief and Foundations exclude lock icons (القفل). Consider `schedule` or `timer`.
13. **Secondary text colour.** The file uses `#5E5D58` everywhere. The Foundations token `text.secondary` is `#6B6A65`. Confirm which to use; `#5E5D58` gives more contrast.
14. **No `h1` on app pages** (S07, S09, S11 use H2 as the page title). The handoff requires one h1 per page. Promote these titles to h1.
15. **Workload bar scale** is hardcoded to a maximum of 45 tasks. Define it as the team maximum or a configured capacity.
16. **Automated tasks show the same completion checkbox** as manual ones, but they cannot be completed or deleted manually. Render it disabled with a reason.
17. **The support form has no description or attachment field**, only issue type and case ref.
18. **"طلب وصول من المسؤول" requires a reason** (aside), but no input is drawn.
19. **Demo mobile field** shows a masked sample value ("05• ••• ••12") in an input. Real input must be unmasked while the user types.
20. **Gendered role labels.** Examples include "مديرة حالات", "محللة ائتمان", "مدققة امتثال" and "محلل ائتمان". Role display strings need gender-aware variants or neutral wording.
21. **Sidebar highlighting on S11.** The sidebar uses `active=none` even though "المساعدة والدعم" is a sidebar footer link. Consider highlighting it.
22. **Demo-only footer text.** "بيانات العرض خيالية" in the landing footer should be removed in production.
23. **States without copy.** Offline (S03/S04), no results, loading and connection error (S08/S10), empty tasks (S09), and demo-request errors are listed in the asides but have no copy. Do not invent final copy; use C11 patterns with TBD copy.
24. **No integration-status labels in B2.** Implied dependencies:
    - SMS gateway (OTP, backup factor, link resend)
    - TOTP authenticator
    - Breached-password check
    - Geo-IP (city level)
    - Free-email-domain blocklist
    - Transactional email (demo acknowledgement, invitations)

    Apply the handoff `IntegrationState` enum (`enabled | simulated | pending | unavailable | failed`) wherever these surface.
25. **Physical CSS values that break EN mirroring.** The design writes three values physically:
    - the sidebar active bar and the selected S10 option mark, both `box-shadow: inset -3px 0 0 #F4633A` (a right-edge bar)
    - the popover anchor `left:32px`

    Implement the bar as `border-inline-start: 3px solid #F4633A`, or flip the shadow per `dir`. Implement the anchor as `inset-inline-end: 32px`, anchored to the bell. The tab underline (`inset 0 -3px 0`) has no direction and needs no change.
26. **Public pages not designed.** "للجهات المموّلة", "لملاك العقارات", "الحوكمة والخصوصية", privacy, terms, complaint and contact are nav links only. Only the landing sections exist.
