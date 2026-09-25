# Frontend B6 — Owner (debtor) journey

Scope: `web/` only. The owner placeholders (`app/owner/page.tsx`, `app/owner/[section]/page.tsx`) are replaced by dedicated routes. Every screen renders data from `/api/owner/*` (or `/api/notifications`). There are no demo arrays.

## Architecture

- **Guard.** `app/owner/layout.tsx` now only guards the portal (`requireOwnerPortal`).
- **Page shell.** Each screen renders its own `OwnerShell` through `components/owner/OwnerPage.tsx`, because the DebtorTop title, sub and back target and the bottom nav differ per screen.
  - `OwnerPage` also renders the page `<h1>`. It is `sr-only` on mobile, where DebtorTop shows the title. From 1024px, where DebtorTop is hidden, it is visible and has a back link.
- **Server helpers.** `components/owner/server.ts` provides:
  - `getOwnerContext()` — runs the owner guard first, then resolves locale, copy and shell data.
  - `ownerGet()` — calls `apiGet('/owner…')` after the guard.
  - `ownerMetadata()`.
  - `riyadhToday()` — the Riyadh calendar date, used for date limits and the month list.
- **Copy.** `components/owner/copy.ts` holds Arabic (verbatim from the B6 spec) and English. `useOwnerCopy()` returns it on the client. Shared `dictionaries/*` were not touched.
- **Response types.** `components/owner/types.ts` mirrors `OwnerEndpoints.cs`.
  - Anonymous-type members arrive camelCase.
  - `.ToString()` enums arrive PascalCase (`Sent`, `Rejected`, `Complaint`, `ReducedPayoff`).
- **Display helpers.** `components/owner/values.tsx` and `components/owner/ui.tsx`:
  - `Amount` shows «ريال» / «SAR»; the kit's `Money` shows «ر.س».
  - `ServerText` wraps each number, date or reference inside server-authored Arabic text in `<bdi dir="ltr">`. In the English UI it marks the whole run `lang="ar" dir="rtl"`.
  - Also: `Card`, `InfoNote`, `SegmentProgress`, `ChoiceCard` (52px checkbox/radio card), `TouchLink` (48px).
- **Mutations.** `components/owner/useSubmit.ts` keeps one idempotency key per logical submit.
  - The key survives only `network`/`offline` failures. It is reset after success, after any API error and whenever the input changes.
  - Errors map to humane copy, or to the server's Arabic title or field message.
  - After a mutation the screen calls `router.refresh()`. Results are announced through `aria-live` regions.

## Screens

| ID | Route | Component(s) | API calls | States implemented | Deviations / notes |
|---|---|---|---|---|---|
| D02 | `/owner` | `app/owner/page.tsx` (server) | `GET /owner/home` | `nextStep.type` offer, document, agreement, payment, closed; `null` → «لا شيء مطلوب منك الآن»; deadline shown as days left (0 → «اليوم», negative → gentle «انتهت المهلة» copy); `caseManager` null; appointment line; 1024px layout: next-step card plus a side column with progress, tiles and manager, and a «أحتاج مساعدة» secondary button; EN LTR mirrors through logical properties; mobile-only `LocaleSwitch` link | Offline "last version" cache is not implemented (no service worker). In EN, the next-step title and CTA are localized by `type`, but the body stays the server's Arabic. The quick tiles also appear in the desktop side column. |
| D03 | `/owner/journey` | `app/owner/journey/page.tsx` | `GET /owner/journey` | done, current (`aria-current="step"`), todo; info note | EN uses localized stage titles; descriptions are the server's Arabic. |
| D04 | `/owner/documents` | `page.tsx` + `DocumentsClient.tsx` | `GET /owner/documents`, `POST /owner/documents/upload` (multipart `file`, `documentId`), `POST /owner/documents/{id}/help` | Rejected card with the reason and help text, requested card, status rows (received, in review, provided by the bank, expiring); upload via file input or camera (`accept="image/*" capture="environment"`); client check for the 20 MB limit only (the server judges the type); upload busy, error and success (`aria-live`); help sent → link to Messages; empty state | Sub «N يحتاج رفعاً من جديد» / «N يحتاج رفعاً» is computed from the items. |
| D05 | `/owner/debt` | `app/owner/debt/page.tsx` | `GET /owner/debt` | Total with source and date, explained line items, `total: null` → empty state; link to `/owner/complaints/new?type=objection` | EN labels are mapped by `key`; explanations stay the server's Arabic. |
| D06 | `/owner/options` | `page.tsx` + `InquiryButton.tsx` | `GET /owner/options`, `POST /owner/options/{key}/inquiry` | Active offer card or «لا يوجد عرض» note; inquiry busy, error and confirmation (server message) | — |
| D07 | `/owner/offers/[id]` | `page.tsx` + `OfferActions.tsx` + `terms.tsx` | `GET /owner/offers/{id}`, `POST …/decline`, `POST /owner/messages` (request a new offer) | `Sent`: accept, suggest a change, and «لا يناسبني» (a gentle dialog with an optional reason → decline → refresh); `Expired`: «طلب عرض جديد» sends an Arabic portal message to the case manager; `Countered`, `Declined`, `Accepted`, `Withdrawn` notes (`role=status`); «بيتك يبقى ملكك» hidden for `VoluntarySale`; payoff hero for `ReducedPayoff` | There is no `request-new` endpoint, so the request is a message, as briefed. |
| D08 | `/owner/offers/[id]/counter` | `page.tsx` + `CounterForm.tsx` | `POST /owner/offers/{id}/counter` | Checkbox cards (day, start, amount); day select 1–28; start month is the next 6 `yyyy-MM` months, computed on the server in Riyadh time; amount field; reason (≤500); submit is `softDisabled` until at least one ticked change has a value; success shows the server message and «نرد عليك قبل» date; non-open offer note | Selects start empty, so the owner makes an explicit choice. |
| D09 | `/owner/offers/[id]/accept` | `page.tsx` + `ConsentFlow.tsx` | `POST …/consent/otp`, `POST …/consent` | Summary bullets; «قراءة الشروط كاملة» opens a dialog with the full terms; two required acknowledgements; send code shows the destination, the sandbox box (`SandboxCodeBox`, as on MFA), a 52px `OtpInput`, a resend countdown and resend; agree is enabled only with both acknowledgements and 6 digits; errors: `otp_invalid` (remaining attempts from `reasons[0]`), `otp_exhausted` / `otp_expired` (asks for a new code), `offer_closed` / `offer_expired` (link back), `acknowledgements` validation; success view (`role=status`, title «تمت الموافقة», no back) with the reference, Gregorian date-time and Hijri (`formatHijri`), and the "consent record, not a licensed e-signature" statement (also on the review view); reloading after acceptance shows «سجّلنا موافقتك… من قبل» with a link to the agreement | No pre-consent PDF endpoint exists, so the full terms open in a dialog instead of «(PDF)». `/accepted` is not a separate route: success renders in place. |
| — | `/owner/agreement` | `page.tsx` + `PrintButton.tsx` | `GET /owner/agreement` | Printable terms (number, status tag, amounts, dates with Hijri, breach terms, consent time, and the API's `signature` sentence verbatim); «تنزيل نسخة الاتفاق» calls `window.print()`; `agreement: null` → empty state | Print CSS: `print:hidden` on the owner chrome (skip link, DebtorTop, desktop header, bottom nav) and on the button. |
| D10 | `/owner/payments` | `page.tsx` + `PaymentsClient.tsx` | `GET /owner/payments`, `POST /owner/payments/{no}/notice` | `active:false` → empty state with the server message; next-installment card; «رهون لا تستلم أي مبالغ» note; rows with status tones; «كيف أدفع؟» dialog with the server steps; per-installment «أرسلت التحويل» notice form (transfer date ≤ Riyadh today, amount prefilled, optional reference) → announced and refreshed | No receipt-download endpoint exists, so there is no receipt button (the server `meta` already says «إيصال متاح»). |
| D11 | `/owner/help` | `page.tsx` + `HardshipForm.tsx` | `POST /owner/hardship` | Optional single-choice reason cards (a call can be requested with no reason); success `role=status`; privacy note; links to Messages, a new complaint and complaint tracking | — |
| D12 | `/owner/messages` | `page.tsx` + `MessagesClient.tsx` | `GET /owner/messages`, `POST /owner/messages`, `POST /owner/appointments/{id}/confirm`, `POST …/reschedule` | Appointment strip (date tile, call/visit, 12-hour time in Riyadh, server status text); confirm (for Proposed/Rescheduled); reschedule dialog with an optional note; `role=log` bubbles (owner at the end, tinted rust-50/rust-200; staff at the start, white) with meta `author · date time · قُرئت`; empty state; sticky composer with a 50px send button and «الرد المتوقع خلال يوم عمل» | — |
| D13 | `/owner/complaints/new`, `/owner/complaints` | `new/page.tsx` + `ComplaintForm.tsx`, `complaints/page.tsx` | `POST /owner/complaints`, `GET /owner/complaints` | Type radio cards (`?type=objection` preselects the objection); body required, ≥10 characters, ≤4000; success shows the reference, the reply due date (Gregorian + Hijri) and the regulator footnote; the list shows the status tag, due date and written reply; empty state | «إرفاق ملف (اختياري)» is omitted because the API accepts no attachment. |
| D14 | `/owner/documents/closure` | `app/owner/documents/closure/page.tsx` | `GET /owner/closure`; downloads via `<a href="/api/owner/files/{fileVersionId}" download>` | Closed: visible h1 «أُغلقت حالتك», closure date, download rows (48px, `aria-label="تنزيل …"`), «قيد التجهيز» when there is no file, and access-until note; not closed → «حالتك ما زالت قائمة» | File type («PDF») is not shown because the API does not return it. |
| — | `/owner/notifications` | `page.tsx` + `NotificationsClient.tsx` | `GET /notifications`, `POST /notifications/{id}/read`, `POST /notifications/read-all` | Unread marker; open marks the notification read, then follows the `/owner…` link; mark all read; empty state | The bell (mobile and desktop) now points here instead of `/owner/messages`. |

B10 owner referral-status screen: skipped, because no `/api/owner` referral endpoint exists.

## Shared-file edits (small, backwards compatible)

- `components/shell/OwnerShell.tsx`:
  - `DebtorTop.title/sub` and `OwnerShell.title/sub` are now `ReactNode`, so dates can sit in `<bdi>`.
  - The DebtorTop bell now defaults to `/owner/notifications`.
  - `OwnerShell` accepts `bellHref`.
  - `OwnerDesktopHeader` gains a bell (`unread`, `bellHref`).
  - `print:hidden` added to the chrome; `main` loses its max width and padding in print.
- `app/owner/layout.tsx`: guard only (the shell moved per page).
- `app/owner/[section]/page.tsx`: deleted (replaced by dedicated routes).

## Verification

`npx next typegen && npx tsc --noEmit` ✔ · `npx eslint .` ✔ · `npm run build` ✔. No dev server or browser checks were run; visual QA is left to the integrator.

## Open issues

- **Backend: consent OTP attempts are likely not counted.** `OwnerEndpoints.Consent` calls `OtpService.VerifyAsync` inside the consent transaction. On a wrong code, `VerifyAsync` saves `Attempts++` and then throws `otp_invalid`. The transaction is never committed, so the increment is likely rolled back and the attempt limit may never trigger for consent codes.
- **API text is Arabic only.** Server-authored text (next-step bodies, statuses, explanations, messages) stays Arabic in the English UI. It is isolated with `lang="ar" dir="rtl"`.
- **Expired session sends the owner to `/login`.** `apiGet` redirects any 401 to `/login?next=…`. When an owner session expires between the layout guard and a fetch, the owner lands on the institutional login instead of the owner notice.
- **No owner `loading.tsx`.** The layout no longer holds the chrome, so a route-level skeleton would drop the chrome while loading. Navigation keeps the previous screen until the next one is ready. Async results use `aria-live` and button loading states.
