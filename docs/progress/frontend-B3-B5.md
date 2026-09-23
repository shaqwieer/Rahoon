# Frontend B3 + B5 — case tabs (L06–L12), comms and tasks (L22), complaints (L23), case audit log (L24), bulk import (L04), cancellation (C01)

**Status:** implemented against the B3 and B5 backend contracts.

**Verification** (in `web/`):

| Check | Result |
|---|---|
| `npx next typegen && npx tsc --noEmit` | 0 errors |
| `npx eslint .` | 0 problems |
| `npm run build` | passes, and every new route is listed as dynamic |

No browser or E2E run was done; the task did not allow browser tools.

## Shared infrastructure (new)

| File | Purpose |
|---|---|
| `web/src/lib/api/load.ts` | `apiLoad<T>(path)`: a server GET that returns `{ok:true,data}` or `{ok:false, kind:"forbidden"\|"error", errorRef, title}`. Unlike `apiGet`, a 403 does **not** redirect to `/access-denied`, so a tab the role cannot see (e.g. audit without `audit.view`) keeps the CaseHeader. 401 still goes to login and 404 to `notFound()`. |
| `web/src/components/case/LoadFailure.tsx` | C11 `SystemState` for forbidden (tab-level copy, no data shown) or error (ERR ref + «إعادة المحاولة» → `router.refresh()`). |
| `web/src/app/(lender)/cases/[ref]/loading.tsx` | Tab skeleton under the CaseHeader for every case tab. |
| `web/src/app/(lender)/complaints/loading.tsx` | List/detail skeleton. |

**Page pattern:**
- The server `page.tsx` loads with `apiLoad`, reads `getMe()` for permission hints, and uses `getWorkspace` (cached) where it needs header data.
- A client `*View.tsx` handles the interactions.
- Tab pages use only h2; the CaseHeader is the h1.
- Every mutation uses `useIdempotencyKey`, resets the key, then shows a toast and calls `router.refresh()`.
- Errors show the API's `title`, `reasons` and `fieldError`.
- Dialog and drawer bodies are padded (`p-5`).

## Shared-file edits (additive)

| File | Change |
|---|---|
| `lib/api/lender.ts` | + `PendingCancellation` interface; `WorkspaceData.sensitive.pendingCancellation: PendingCancellation \| null` (mirrors `CancellationEndpoints.PendingSummaryAsync`). |
| `components/case/OverviewActions.tsx` | `SensitivePanel` only: when a cancellation is pending, it shows a warn box (requester, approver) linking to `/cases/[ref]/cancel`, labelled «اتخاذ القرار» if `canDecide` and «عرض الطلب» otherwise. With nothing pending it keeps the existing «إلغاء الحالة» button. `RevealParty` is untouched. |

New DTO files:
- `lib/api/caseInfo.ts` (parties, finance, property)
- `documents.ts`
- `valuation.ts`
- `comms.ts`
- `complaints.ts`
- `audit.ts`
- `imports.ts`

Each is copied from the C# payloads.

## Screens

### L06 — Parties

**Route:** `/cases/[ref]/parties`

**Components:** `page.tsx`, `PartiesView.tsx`, and the existing `RevealParty`.

**API:**
- `GET /cases/{ref}/parties`
- `POST /parties`
- `PATCH /parties/{id}` (sends changed fields only)
- `POST /parties/{id}/poa-request`
- `POST /owner-invitation`
- `PUT /contact-preferences`

**What it shows:**
- Party cards: facts, one tone note, and actions only as the API's `actions` flags allow.
- The add/edit dialog hides the primary owner's name, ID, phone and role, and shows the API rule instead.
- Contact preferences card with an edit dialog.
- Invitation button:
  - Uses the API's `actionLabel`.
  - When the invite is not allowed, the button is soft-disabled and the reason is linked to it.
  - `devLink` is shown only when the API returns it, labelled «رابط تطويري — بيئة تجريبية فقط، لا يظهر في الإنتاج».
- POA request dialog with a due date and channels.

**States:** forbidden/error, and empty (no parties).

**Deviations:**
- Reveal is offered only on the ID fact, because `RevealParty` shows the ID only.
- The POA request uses a small dialog, not the L10 request drawer.

### L07 — Finance

**Route:** `/cases/[ref]/finance`

**Components:** `page.tsx`, `FinanceView.tsx`

**API:**
- `GET /cases/{ref}/finance`
- `POST /finance/correction-requests`

**What it shows:**
- Sync banner with the API's tone: delayed or failed shows «غير محدث».
- Contract key/values.
- Debt table: item, amount, source and asOf; total with a verified check.
- 12-month installment strip: accessible label per month, a legend, the summary line and the reported arrears. «عرض كجدول» switches to a real table.
- Correction dialog: field, current value (prefilled), proposed value, note ≥ 10 characters, and a month (history only).

**States:** forbidden/error; no debt figures, no contract or an empty history.

**Deviations:**
- The tab h2 is visible to screen readers only.
- Mobile shows all 12 months wrapped, not only the last 6.

### L08/L09 — Property and mortgage

**Route:** `/cases/[ref]/property`

**Components:** `page.tsx`, `PropertyView.tsx`

**API:**
- `GET /cases/{ref}/property`
- `PATCH /property`
- `POST /mortgage/legal-review`

**What it shows:**
- Property card, with the protection note and the valuation value.
- Mortgage card: legal-review badge, and a warning when the review blocks «حل مقترح».
- Legal note and inspection.
- The photo comes from `/documents/versions/{id}/file`, only with `document.download`.
- **Legal review form:** rendered only when `me.permissions` includes `agreement.activate`. If the API's `canLegalReview` is false, a notice replaces it.
- **Property edit dialog:** shown when `canEditProperty` is true. A new deed number shows a warning that the legal review goes back to pending.

**States:** forbidden/error, no property, no mortgage.

### L10 — Documents

**Route:** `/cases/[ref]/documents` (`?filter=`, `?request=new|{docId}`)

**Components:** `page.tsx`, `DocumentsView.tsx`, `VersionsDrawer.tsx`, `RequestDrawer.tsx`, `UploadDialog.tsx`

**API:**
- `GET /cases/{ref}/documents?filter=all|requested|in_review|expiring` (items + counts)
- `GET /document-types`
- `GET /documents/{id}/versions`
- `GET /api/cases/{ref}/documents/versions/{vid}/file` (plain link; audited; needs `document.download`)
- `POST /versions/{vid}/review` (`verify|reject`; rejecting needs the owner-facing reason)
- `POST /documents/requests/preview` (live, abortable)
- `POST /documents/requests`
- `POST /documents` (multipart; `documentId` adds a new version)

**What it shows:**
- Four route tabs with counts.
- One action per row: review, versions or upload.
- Versions drawer: history, verify/reject.
- Request drawer: type rule, due date with Hijri and `dueLabel`, channels, owner text preview. Send stays disabled until a fresh preview exists.
- Upload uses `UploadDropzone`.

**States:** forbidden/error, empty per filter, and loading and error inside the drawer.

**Deviations:**
- «فريق المصرف (داخلي)» has no request API, so choosing it offers an internal upload instead.
- Preview opens the file in a new tab; there is no full-screen dark viewer.
- The row `more_vert` menu is replaced by a single action.
- The mobile request is a drawer, not a full page.

### L11/L12 — Valuation and analysis

**Route:** `/cases/[ref]/valuation` (`?view=analysis`)

**Components:** `page.tsx`, `ValuationView.tsx`, `AnalysisView.tsx`

**API:**
- `GET /cases/{ref}/valuation`
- `POST /valuation/{reportId}/review` (`accept|return`; return needs a note ≥ 10)
- `POST /valuation/revaluation-requests`
- `GET` / `PUT /cases/{ref}/analysis` (with `version`)
- `GET /documents` (verified income versions)

**What it shows:**
- Report card: value, range, LTV, validity label with days left, methodology.
- Under-review reports can be accepted or returned when `canReview`. Accepting an expired report is shown disabled with its reason.
- Revaluation:
  - Hidden without `valuation.assign`.
  - Disabled with the API's `disabledReason`, plus a path to request with a documented reason.
  - A 409 `revaluation_not_eligible` or `revaluation_pending` shows the API message.
- Assignment timeline and provider-access label.
- Analysis:
  - API rows, and a DSR meter (current and proposed against the limit, with text).
  - Indicators, options, and what is missing for completion.
  - Edit dialog (`canEdit`). A 409 `concurrency` shows a warning with «إعادة التحميل».

**States:** forbidden/error; no accepted report; no assignment.

**Deviations:**
- The sub-tab is a query parameter, not a route.
- The indicator icon and option kind lists in the edit form are UI choices; the API stores free text.
- The meter scale is `max(limit×1.4, values)`.

### L22 — Communication and tasks

**Route:** `/cases/[ref]/comms`

**Components:** `page.tsx`, `CommsView.tsx`. It contains Composer, TemplatePicker, AppointmentsCard/Dialog, TasksCard/CreateTaskDialog and PreferencesCard.

**API:**
- `GET /cases/{ref}/comms`
- `POST /comms/messages` (`{body, internal, channel}`)
- `POST /comms/appointments` (`{type, startsAt(+03:00), attendees}`)
- `POST /tasks`
- `POST /tasks/{id}/complete`
- `GET /org/members` (server-side, only with `task.manage`)

**What it shows:**
- Two separate lanes as in-place tabs: «مع المالك» and «ملاحظات داخلية». The internal lane has a warm background and dashed bubbles, with the note «لا تظهر للمالك».
- Template insert, the open-complaints tag (text hidden) and the hardship alert.
- Contact hours and preferences card, linking to the Parties tab.
- The composer and appointments need `comms.send`; without it a read-only line shows.
- Creating a task needs `task.manage`.

**States:** forbidden/error, empty per lane and card, field errors.

**Deviations:**
1. The provider lane is left out; there is no provider channel API.
2. There is no template-by-key or `templateKey` on posting, so inserting a template only fills in the text.
3. Out-of-hours auto-scheduling is not in the API; a contact-hours reminder is shown instead.
4. SMS is not offered (the API sends portal messages plus an in-app notification); the choices are portal or a logged call.
5. Read receipts have no timestamp.
6. Appointments are display-only; there is no confirm or reschedule API.
7. At 390px the aside stacks below the thread.

### L23 — Complaints

**Route:** `/complaints` and `/complaints/[ref]`

**Components:** `complaints/page.tsx` (list), `[ref]/page.tsx`, `ComplaintView.tsx`

**API:**
- `GET /complaints?status=open|mine|closed`
- `GET /complaints/{ref}`
- `POST /findings`
- `PUT /draft`
- `POST /decision` (`{decision, response, extendOfferDeadline, extensionDays: 7}`)

**List:** route tabs (the «المسندة إليّ» tab is for reviewers only). A table from 768px up, cards below.

**Detail:**
- Main column: text, findings with an add form, and a decision segmented control (مقبولة جزئياً / مقبولة / غير مقبولة).
- The response editor has «حفظ مسودة» and the offer-extension checkbox.
- Aside: impact, a path stepper with `aria-current="step"`, and other open complaints.

**Tag-only view:** driven by the API. The list's subject column is hidden when `subject` is null, and the detail page switches when `body === null`. Reviewers who are not assigned see a read-only view. Closed complaints show the sent response.

**States:** forbidden/error (the h1 is kept), empty per tab, loading.

**Deviations:**
- Draft save does not keep the decision; the API ignores it.
- No type or attachments are shown; the API does not return them.
- Escalate is not shown in the UI.

### L24 — Case audit log

**Route:** `/cases/[ref]/audit`

**Components:** `page.tsx`, `AuditView.tsx`

**API:**
- `GET /cases/{ref}/audit?type=&actor=&blocked=&from=&to=&page=&pageSize=50`
- `GET /audit/verify` (server-side, plus a client «إعادة التحقق» button)
- `GET /audit/export?…` (plain link carrying the same filters)

**What it shows:**
- Filter chips:
  - type: multi-select over the API categories
  - actor: menu
  - period: from/to with validation
  - «المحاولات المحجوبة فقط»
  - clear all
- Filters live in the URL.
- `AuditTimeline` with Gregorian and Hijri times, reason, detail, masked IP, evidence tags and `#seq`.
- Pagination.
- Chain-verification badge and footer: err tone and role=alert when the chain is broken.

**States:** forbidden (no `audit.view`, and the case chrome is kept), error, empty (filtered or unfiltered).

**Deviations:**
- Uses `AuditTimeline` (per the task) instead of the design's 5-column table.
- Export is visible to anyone with `audit.view`, because there is no `audit.export` permission (backend open issue #2).

### L04 — Bulk import

**Route:** `/cases/import` (`?batch=`)

**Components:** `page.tsx`, `ImportView.tsx`. It contains UploadStep, BatchPanel, RowLine, DecisionDialog and RecentBatches.

**API:**
- `GET /cases/imports`
- `GET /cases/imports/{id}?status=&page=`
- `POST /cases/imports` (multipart)
- `POST /rows/{n}/decision`
- `POST /commit`
- links to `/cases/imports/template` and `/{id}/errors.csv`

**What it shows:**
- PageHeader h1 with the step (الرفع / التحقق / النتيجة) and an upload dropzone (CSV).
- KPI counts, status tabs, and a rows table with issues and «الإصلاح».
- Duplicate decision dialog: skip / link / create_with_reason, limited to the row's `actions`, with a reason ≥ 10 characters.
- Sticky «استيراد N صفاً» (N = `counts.importable`) with a confirmation dialog. `alreadyCommitted` is handled, and after a commit it links to the created drafts.
- Recent batches aside.

**States:** forbidden/error, uploading, upload rejected (`fieldError("file")`), empty tab, committed.

**Deviations:**
- «استبدال الملف» uploads a new batch; there is no `PUT /file`.
- CSV only.
- The rows table is hidden below md, which shows the counts only.

### C01 — Cancellation

**Route:** `/cases/[ref]/cancel`

**Components:** `page.tsx`, `CancelView.tsx`. It contains RequestScreen, DecisionScreen, PendingPanel and History.

**API:**
- `GET /cases/{ref}/cancellation-requests`
- `POST /cancellation-requests` (step-up)
- `POST /cancellation-requests/{id}/decision` (step-up)
- workspace `sensitive.pendingCancellation`

**Maker** (`ReviewScreen titleAs="h2"`):
- «ما سيحدث», a reason ≥ 10 characters, and an attestation.
- `expectedStatus` is sent with the request.
- `StepUpDialog` runs before the request, and `step_up_required` retries after verification.
- `guard_failed` shows the reasons. `stale_state`, `cancellation_pending` and `already_decided` show a reload button.

**Checker** (when `pending.canDecide`): approve or reject, with the effects list updating to match the decision, a reason ≥ 10 characters and a step-up.

**Otherwise:** a read-only panel for a pending request (it includes the API's separation-of-duties note), plus the history of past requests.

**States:** forbidden (no `case.cancel`), empty (terminal case), error.

**Deviations:**
- «ما سيحدث» explicitly says owner portal access is **not** revoked automatically, because the backend doesn't revoke it (backend-B3 C01 note). The task text mentioned "access revoked"; the UI follows the API.
- No drawn design exists for this screen, so the copy follows the API behaviour.

## Open issues

1. `app/(lender)/cases/[ref]/solutions/[n]/SolutionBuilder.tsx:335` is an existing dialog whose body is unpadded (`p-0`). It is outside this batch and was not touched.
2. Backend gaps behind the deviations above:
   - provider comms lane
   - template keys on messages
   - appointment confirm/reschedule
   - internal document requests
   - `audit.export` permission
   - an import file replacement route
   - owner access on cancellation (a product decision)
3. There are no Playwright E2E tests for these screens yet.
