# Backend B3 — case tabs (L06–L12), case audit log (L24), bulk import (L04), cancellation (C01)

Status: implemented, with integration tests. All routes are registered through `Modules/Cases/CaseTabsModule.cs`, using one line in `ModuleRegistry.cs` (`app.MapCaseTabs()`).

Tests: `RAHOON_TEST_ENSURE_CREATED=1 dotnet test` gives **64 passed, 1 failed (65 total)**. The 26 new tests all pass.
The one failure is the existing `SecurityTests.Database_rejects_audit_tampering`, and it also fails on the untouched baseline under
`RAHOON_TEST_ENSURE_CREATED=1`. The append-only trigger is created by a migration, and EnsureCreated does not apply migrations. The test passes on the migrations path.

## Schema delta (for the consolidated migration)

| Change | Where | Notes |
|---|---|---|
| **New column** `cases.owner_accesses.communication_needs` (text, nullable, max 2000 by convention) | `OwnerAccess.CommunicationNeeds` in `Modules/Cases/CaseEntities.cs` | L06 «احتياج تواصل». |
| No new tables, no new indexes | — | `ImportBatch`/`ImportRow`, `ValuationReport`, `AffordabilityAnalysis`, `ApprovalRequest` (Subject = Cancellation) are reused. |
| Data only: two new document types, `salary_certificate` («تعريف بالراتب») and `legal_memo` («مذكرة المراجعة القانونية») | `Seed/DevSeeder.CaseTabs.cs` | Seed only, not a catalog migration. |

Structured values stored in existing `text[]` columns, with **no schema change**:
- `ImportRow.Issues`: one JSON object per issue: `{field, fieldLabel, code, message, fix, severity}`.
- `AffordabilityAnalysis.CircumstanceIndicators`: JSON `{icon, text}`. Legacy plain strings are still read.
- `AffordabilityAnalysis.OptionNotes`: JSON `{kind, title, feasible, note}`. Legacy plain strings are still read.

## Shared files touched (merge notes)

| File | Change |
|---|---|
| `Modules/ModuleRegistry.cs` | +1 line `app.MapCaseTabs();` |
| `Seed/DevSeeder.cs` | +1 line `await SeedCaseTabsAsync();` right after `SeedCanonicalCasesAsync()` |
| `Modules/Cases/CaseEntities.cs` | +1 property `OwnerAccess.CommunicationNeeds` |
| `Modules/Cases/CaseEndpoints.cs` | +1 line in the workspace `sensitive` block: `pendingCancellation = …` |
| `Modules/Cases/CaseAccess.cs` | `NextActionBuilder`, InternalApproval branch: the pending-approval lookup now filters `Subject == Solution`. Without it, a pending cancellation or offer extension could take over the next-action card. |
| `tests/.../Infrastructure/TestClient.cs` | + `SendRawAsync` (multipart/raw bytes, same cookie/CSRF/idempotency headers) and `PatchAsync` |

`Permissions.cs` and `SystemRoles.cs` are **not** modified, and no permission was added (see L08).

New files: `Modules/Cases/{CaseTabsModule, CaseStaffing, CasePartyEndpoints, CaseFinanceEndpoints, CasePropertyEndpoints, CancellationEndpoints}.cs`,
`Modules/Documents/DocumentRequestPreviewEndpoints.cs`, `Modules/Assessment/{ValuationEndpoints, AnalysisEndpoints}.cs`,
`Modules/Audit/CaseAuditEndpoints.cs`, `Modules/Imports/{ImportService, ImportEndpoints}.cs`, `Seed/DevSeeder.CaseTabs.cs`,
tests `B3Scenarios.cs`, `CasePartiesTests.cs`, `CaseFinancePropertyTests.cs`, `ValuationAnalysisTests.cs`, `CaseAuditTests.cs`, `ImportTests.cs`, `CancellationTests.cs`.

Conventions applied everywhere:
- Case-scoped routes load through `CaseAccess.GetAsync`. A foreign or unknown case gets the same 403 `case_forbidden`.
- State-changing POSTs are `.Idempotent()`. PUT and PATCH follow the existing draft-wizard convention and are not.
- Refusals go through `RecordBlockedAsync` **before** any `RecordAsync` in the transaction.
- Arabic messages come from `Validator` or `DomainException`.
- Masking happens on the server, and no plaintext national ID or phone number appears in any response, audit text or import row.

`CaseStaffing.PickAsync` routes work. It picks an active member who holds the permission and is not excluded, ranked by preferred role, then open-task load, then name.

---

## L06 — Parties (الأطراف)

| Route | Permission | Notes |
|---|---|---|
| `GET /api/cases/{ref}/parties` | case.view | Every party masked (ID, phone, e-mail), with `facts[]`, one `note{icon,tone,text}`, per-party `actions` (message / edit / requestPoa / reveal: hidden vs disabled by permission), `financialVisibility`, plus a `contact` block. |
| `POST /api/cases/{ref}/parties` | case.edit · idempotent | Roles: co_borrower, guarantor, agent, legal_representative, occupant, informal_representative. The owner is never added here. ID is required for co-borrower and guarantor. Relation is required for occupant and informal rep. The same ID on the case gives 409. Refused on closed or cancelled cases (409). |
| `PATCH /api/cases/{ref}/parties/{id}` | case.edit | Partial update; the audit records the changed field names only. |
| `GET` / `PUT /api/cases/{ref}/contact-preferences` | case.view / case.edit | Channels are `platform, sms, email, call`. Also contact hours and communication needs. Creates a NotSent `OwnerAccess` when none exists. |
| `POST /api/cases/{ref}/owner-invitation` | case.edit · idempotent | Random token with only its SHA-256 stored, 14-day expiry, sandbox SMS containing `{Web:PublicBaseUrl or first allowed origin}/invite/{token}`. `devLink` is returned only outside Production when Development **or** `Auth:ExposeSandboxOtp` (the Testing host). 409 while Draft or terminal, with no phone, or when contact is not allowed. Audited as `owner.invitation_sent`. |
| `POST /api/cases/{ref}/parties/{id}/poa-request` | document.request · idempotent | Creates a `poa` document plus a `DocumentRequest` (`PartyId` set) rendered from TPL-DOCREQ-01, sets `PoaStatus=Requested`, and notifies the owner if they are registered. 409 if one is already open or the POA is verified. |

The existing reveal endpoint (`POST /parties/{id}/reveal`) is unchanged.

Entities: `CaseParty`, `OwnerAccess` (+CommunicationNeeds), `CaseDocument`, `DocumentRequest`, `OutboundMessage` (through `ISmsGateway`).

Decisions:
- **Refreshing an invitation** replaces the hash, which immediately invalidates the old link. It also sets the status back to `Sent`, even for an owner who had already accepted. The auth flow only enforces expiry for `Sent`, so this is what makes the 14-day expiry apply to every issued link. On the next sign-in the owner is marked `Accepted` again. A refresh does not end an owner session that is already signed in, because session resolution does not read the invitation status (this is tested). While the new link is pending, the workspace header shows «دعوة مرسلة».
- **The primary owner's name, national ID and phone cannot be edited through PATCH** (400). They drive owner sign-in (invitation + last 4 digits + OTP), so letting a staff member change the phone would let them redirect the owner's OTP. Corrections go through the source instead. For other parties, the national ID can only be *added* once and never changed.
- «آخر تواصل» is derived from the latest non-internal case message.

Tests (`CasePartiesTests`):
- masking and contact block on the anchor case;
- tenant isolation matches an unknown reference, and providers are refused;
- add and patch validation, permissions (the analyst gets 403), audit with no PII;
- invitation: Draft gets 409, hash stored, SMS simulated, owner sign-in works, refresh invalidates the old token, the new token expires, the analyst gets 403;
- contact-preferences validation;
- POA request, including the duplicate 409.

## L07 — Finance (التمويل والمديونية)

| Route | Permission | Notes |
|---|---|---|
| `GET /api/cases/{ref}/finance` | case.view | See the breakdown below. |
| `POST /api/cases/{ref}/finance/correction-requests` | case.edit **or** analysis.edit · idempotent | Body `{field, currentValue?, proposedValue, note, month?}`. Creates a `data_correction` task for a Finance member (`reconciliation.prepare`, preferring the `finance` role), notifies them and writes `finance.correction_requested`. **It never touches a figure.** |

The finance GET returns:
- `contract`: masked number `MF-88-3317•••` and terms;
- `snapshot.items[]`: principal, profit, late fees and fees, each with its source line and `asOf`;
- `total = Σ items` with `totalVerified` against the stored total;
- a sync status (`ok`, `delayed` when over 24 h, `failed` tagged «غير محدث», or `manual`) and a `banner`;
- a 12-month `history`, a `summary` line and `arrears`.

Arrears (spec conflict #6): the **reported** amount and count come from the core system (`Case.ArrearsAmount`, the source of truth). The history only *explains* them: it supplies the derived missed and partial counts, the start month and the shortfall Σ(due − paid). The summary line uses the reported amount, as in the design («7 أقساط غير مسددة منذ 2026-02 · مجموعها 96,420.00 ر.س · دفعة جزئية واحدة في 2026-05 …»).

Tests (`CaseFinancePropertyTests`):
- Σ items = 1,284,560.00, the contract is masked, the partial month appears and the summary text is correct;
- a correction request creates a finance task, the snapshot is unchanged, the approver gets 403, and a missing note gets 400.

## L08 + L09 — Property & mortgage (العقار والرهن)

| Route | Permission | Notes |
|---|---|---|
| `GET /api/cases/{ref}/property` | case.view | Property with an occupancy label and the family-protection note, `valuationValue` from the latest **accepted** report, and a photo document. Mortgage with rank label, encumbrances, insurance tone, deed match, and `legalReview{status,label,note,reviewer,at,footer}`. `inspection` is taken from an `Appointment(type=inspection)`, or failing that from the valuation assignment's inspection slot. Also `blocksProposal` and the `canEditProperty` / `canLegalReview` flags. |
| `PATCH /api/cases/{ref}/property` | case.edit | Partial update. A **new deed number resets `DeedMatched` and sets the legal review back to Pending**, because the review was done on the old deed. The audit records field names only. |
| `POST /api/cases/{ref}/mortgage/legal-review` | **agreement.activate** (legal) · idempotent | `{status: complete|pending, note (≥10), deedMatched?, otherEncumbrances?, verificationSource?}`. Records status, note, reviewer and time, and writes `mortgage.legal_review`. |

Deviations:
- **Permission:** no dedicated legal permission exists and `Permissions.cs` was left untouched, so the legal review uses `agreement.activate`, which only the Legal role holds. It could later be split into `mortgage.legal_review`.
- **Route:** the spec shows `PATCH /mortgage/legal-review`, but the task asked for `POST`; I implemented `POST`, matching `/agreement/legal-review`.

Guard: `legal_review_complete` on `propose_solution` was already in the workflow. Legal can now satisfy it through the API.

Tests:
- Pending review blocks `propose_solution` (422, with the exact Arabic reason, audited as blocked). The case manager gets 403 on legal-review, legal's note is validated, and once legal completes the review the transition succeeds.
- A PATCH changes the property label, and a deed change reopens the legal review with the deed masked.
- The anchor case shows the mortgage, the completed inspection and the protection note.

## L10 — Documents

| Route | Permission | Notes |
|---|---|---|
| `POST /api/cases/{ref}/documents/requests/preview` | case.view + document.request | `{documentTypeKey, dueOn, uploader: owner|internal, channels?}`. Renders **TPL-DOCREQ-01** (the institution copy first, otherwise the platform one) into `ownerMessage`. Also returns `dueOnHijri`, `daysFromToday`, `dueLabel` («21 ربيع الآخر 1448هـ · 10 أيام»), the institution `rule` (formats, validity, visibility, helper text) and `saved:false`. **Nothing is saved or sent.** |

The existing routes are unchanged: the list with filters (all, requested, in_review, expiring), the version list, upload, review, download and request. The preview lives in a new file and is mapped from `CaseTabsModule`, so `DocumentEndpoints.cs` was not edited. `DocumentRequestTemplates.RenderAsync` is shared with the L06 POA request.

Tests: the rendered text matches the template exactly, nothing is persisted, a past due date gets 400, the approver (who lacks document.request) gets 403, and the anchor's «مطلوبة» count is at least 1.

## L11 — Valuation (التقييم)

| Route | Permission | Notes |
|---|---|---|
| `GET /api/cases/{ref}/valuation` | case.view | See the breakdown below. |
| `POST /api/cases/{ref}/valuation/{reportId}/review` | valuation.review · idempotent | `{decision: accept|return, note}`. A return needs a note (≥10). Only `UnderReview` reports can be reviewed (otherwise 409), and expired reports cannot be accepted. **Accepting supersedes any other Accepted report**, serialized per case with `pg_advisory_xact_lock`, so there is only ever one current report. Audited as `valuation.accepted` or `valuation.returned`. |
| `POST /api/cases/{ref}/valuation/revaluation-requests` | valuation.assign · idempotent | `{reason?}`. **409 `revaluation_not_eligible`** unless there is no current report, or today ≥ `validUntil − 14`, or a documented reason (10–1000 chars) is given. The refusal is audited as blocked. Otherwise it creates a `revaluation` task for the analysis preparer, or failing that the least-loaded credit analyst, and writes an audit event. An open request gives 409. |

The valuation GET returns:
- the `current` accepted report: value, range label `1.58M – 1.72M`, methodology, comparables, dates, `validityDays`, `daysLeft`, `validityLabel` «صالح · N يوماً»;
- reports `underReview` and `history`;
- `ltv` (1,284,560 / 1,650,000 → **77.9%**);
- `revaluation{eligible, eligibleFrom, requiresReason, disabledReason}`;
- `assignment{reference, provider, providerAccess{expiresAt, expired, label}, timeline[]}`.

Out of scope: the valuer assignment itself is created by the providers module at `POST /api/cases/{ref}/assignments`. That route is **not** implemented here, and the revaluation response returns it as `assignmentRoute`.

**Timeline convention (for the providers module):** the timeline contains the case audit events whose type starts with `assignment.` or `valuation.`, or whose `Evidence` contains `assignment:{ASG reference}`. The types used here are:
- `assignment.created`
- `assignment.message` or `assignment.question`
- `assignment.inspection`
- `assignment.submitted` or `assignment.delivered` or `valuation.received`
- `assignment.access_expired`
- `valuation.accepted` or `valuation.returned`
- `valuation.revaluation_requested`

If no `assignment.created` or `assignment.access_expired` event exists, those rows are derived from `ProviderAssignment` (`CreatedAt`, `DueOn`, `AccessExpiresAt`) and flagged `derived:true`.

Tests (`ValuationAnalysisTests`):
- The anchor case shows 1,650,000, the range label, 90-day validity, LTV 77.9 and the timeline rows including access expiry. Sunbula gets 403.
- Revaluation: 409 plus a blocked audit event with no reason, OK with a reason, 409 for a second open request, OK with no reason inside the 14-day window, and finance gets 403.
- Review: permission and validation, a single Accepted report after accepting, and a re-review gets 409.

All dates in these tests are relative to today, never to the frozen seed dates.

## L12 — Analysis (التحليل)

| Route | Permission | Notes |
|---|---|---|
| `GET /api/cases/{ref}/analysis` | case.view | See the breakdown below. |
| `PUT /api/cases/{ref}/analysis` | analysis.edit | `{version, netMonthlyIncome?, incomeDocumentVersionId?, otherObligations?, circumstanceIndicators?[{icon,text}], options?[{kind,title,feasible,note}], completed?}`. See the rules below. |

The analysis GET returns:
- the net income and its `incomeSource` (the document version label, e.g. «كشف الراتب لآخر 3 أشهر v2», with its verified flag and date);
- other obligations;
- `currentDsr`, the original installment divided by income (**0.4251**);
- `proposed.dsr`, the latest live solution version divided by income (**0.4653**, i.e. 46.5%);
- `dsrLimit` 0.55, flagged as an assumption;
- `dsrIncludingObligations`, reported separately;
- the k/v `rows`, indicators, options, `missingForCompletion` and `version` (the xmin row version).

Rules for the PUT:
- **Income is accepted only with a *verified* version** of a salary statement, bank statement or salary certificate on the same case. Anything else gets 400.
- `completed=true` requires verified income.
- **Optimistic concurrency:** `version` is required when an analysis exists. A mismatch gets 409 `concurrency`. The xmin original value is also set, so a concurrent writer between the read and the save still gets 409.
- The audit writes `analysis.updated` or `analysis.completed`.

Decision (spec conflict #9): **DSR = installment ÷ verified net income**, identical to `SolutionCalculator` and the design's 46.5%. Other obligations (2,100) are shown beside it and in `dsrIncludingObligations`, not inside it. The policy limit is not editable here, because it is institution configuration.

Tests:
- The anchor DSR values, indicators and options are correct, and the case manager sees `canEdit:false`.
- A PUT with no source gets 400. A pending (unverified) version gets 400. Completing without income gets 400.
- A create followed by complete works. A stale version gets 409 `concurrency`. The case manager gets 403.

## L24 — Case audit log (السجل)

| Route | Permission | Notes |
|---|---|---|
| `GET /api/cases/{ref}/audit?type=&actor=&blocked=&from=&to=&page=&pageSize=` | case.view + audit.view | See the breakdown below. |
| `GET /api/cases/{ref}/audit/verify` | case.view + audit.view | Runs `AuditLog.VerifyChainAsync` over **the case's organization chain**. Returns `{ok, firstBrokenSeq, chainLastSeq, caseLastSeq, caseLastAt, text, tone}`. The text reads «السجل للإضافة فقط. سلسلة البصمة سليمة حتى {time} · الحدث #{seq}». |
| `GET /api/cases/{ref}/audit/export?(same filters)` | case.view + audit.view | See the export format below. |

The list is newest first and paged (default 50, max 200). Its filters work as follows:
- **`type`** takes comma-separated category keys and/or raw type prefixes. The categories are transitions, approvals, reveal, documents, owner, messages, valuation, payments, complaints, data and audit.
- **`actor`** takes a user GUID or an actor type: `user`, `owner`, `system` or `provider`.
- **`blocked`** filters on `true` or `false`.
- **`from` and `to`** are Riyadh calendar dates.

Blocked attempts are included. Each row carries the Gregorian time `yyyy-MM-dd HH:mm` in Riyadh time and the **Hijri** (Umm al-Qura) date, the icon and tone, the actor with role, from and to states, reason, detail, evidence, masked IP and hash. The response also lists the filter options (categories, and the actors present on the case).

The export is UTF-8 CSV with a BOM, oldest first, including `prev_hash` and `hash`, protected against formula injection, and capped at 50,000 rows. It ends with the line **`# sha256:<hex>`**, where the hex is the SHA-256 of every byte before that line, BOM included (verify with `head -n -1 file | sha256sum`). The export itself is audited as `audit.exported`, with the hash and row count.

Deviations:
- **Export permission:** the design says «المدقق يصدّر» (auditor only), but no `audit.export` permission exists. As the task instructed, export requires `audit.view` (the case team, auditor and compliance), and the audit event records every export. Restricting it to auditors needs a new permission.
- **Routes:** the spec names `/audit/integrity` and `POST /audit/export`; the task named `GET /audit/verify` and `GET /audit/export`, and those are what I implemented.

Tests (`CaseAuditTests`):
- Paging, the blocked filter, the type filter across categories, the actor=owner filter, the date filter and the Hijri and time formats.
- Verify is `ok` on a Sunbula case. On Alufuq it must equal a direct `VerifyChainAsync` recomputation, which keeps the test independent of order (see the tampering test note above).
- The export's trailing hash recomputes, filters apply, the `audit.exported` event contains the hash, and providers and other tenants get 403.

## L04 — Bulk import (الاستيراد الجماعي)

| Route | Permission | Notes |
|---|---|---|
| `GET /api/cases/imports/template` | case.import | Template **v3** CSV: header plus an example row. |
| `POST /api/cases/imports` (multipart `file`) | case.import | `.csv`, UTF-8, ≤ 5 MB, ≤ 5,000 rows, comma or semicolon delimiter. English keys or the Arabic labels are both accepted as headers. Missing required columns give 400. Returns the batch summary and the first 50 attention rows. The batch is audited as `import.validated`. |
| `GET /api/cases/imports` | case.import | The last 20 batches of the caller's organization. |
| `GET /api/cases/imports/{id}?status=attention|all|ready|duplicate|error|imported|skipped&page=&pageSize=` | case.import | Summary with `counts{total, ready, duplicates, errors, imported, skipped, undecidedDuplicates, importable}`, plus rows with `issues[{field, fieldLabel, code, message, fix, severity}]` and allowed `actions`. |
| `GET /api/cases/imports/{id}/errors.csv` | case.import | Error file with masked identifiers only. |
| `POST /api/cases/imports/{id}/rows/{rowNumber}/decision` | case.import · idempotent | `{decision: skip|link|create_with_reason, reason}`. Only for duplicate rows (409 otherwise). `link` only when an **open** case holds the same contract (422 otherwise). `create_with_reason` needs a reason of at least 10 characters. Refused after commit (409). |
| `POST /api/cases/imports/{id}/commit` | case.import + case.create · idempotent | See the commit rules below. |

**Template v3 columns.** Required columns are marked \*:

| Key | Label | Required |
|---|---|---|
| `contract_number` | رقم العقد | \* |
| `owner_name` | اسم المالك | \* |
| `national_id` | رقم الهوية | \* |
| `mobile` | الجوال | \* |
| `region` | المنطقة | \* |
| `city` | المدينة | \* |
| `district` | الحي | |
| `property_type` | نوع العقار | |
| `product_type` | نوع المنتج | |
| `contract_date` | تاريخ العقد | |
| `original_amount` | مبلغ التمويل الأصلي | |
| `original_installment` | القسط الأصلي | |
| `principal` | أصل الدين | \* |
| `profit` | الأرباح | |
| `late_fees` | غرامات التأخير | |
| `other_fees` | رسوم أخرى | |
| `outstanding_amount` | المبلغ القائم | \* |
| `arrears_amount` | المتأخرات | |
| `arrears_installments` | عدد الأقساط المتأخرة | |
| `first_overdue_date` | تاريخ أول تأخر | |

**Row validation.** Every rule below produces an issue with a code and an Arabic fix:
- Required fields must be present.
- Contract number: 6–40 characters of `[A-Z0-9-]`, and it must not repeat within the file.
- National ID: exactly 10 digits («9 أرقام؛ يجب 10.»), starting with 1 or 2 (or 7 for the unified registry).
- Saudi mobile: must start with 05.
- Region: must come from the controlled list of the 13 regions («Riyad» is refused).
- Product type: must come from the wizard list.
- Amounts: numeric («1.2 مليون» is refused), non-negative, and principal and outstanding must be greater than 0.
- **Outstanding must equal principal + profit + late fees + other fees.**
- Arrears count: an integer between 0 and 360.
- Dates: `YYYY-MM-DD` and not in the future.

**Duplicate detection** is always scoped to the organization explicitly, because the seeder runs with filters bypassed:
- the same contract in an open case, **drafts included**, so re-importing a file never duplicates drafts: `open_case`, actions skip, link or create_with_reason;
- the same contract in a closed or cancelled case: `closed_case`;
- the same owner (national-ID lookup hash) in a closed or cancelled case: `same_owner_closed`, actions skip or create_with_reason.

**PII at rest.** `RawJson` never holds a plaintext ID or phone number. It holds `national_id_enc`, `national_id_hash`, `national_id_masked`, `mobile_enc` and `mobile_masked`. The commit copies the ciphertexts onto the new party, so the import pipeline never decrypts. The stored `ImportRow` data is encrypted in the same way as `CaseParty`.

**Commit rules:**
- An atomic claim (`ExecuteUpdate` from Validated to Importing) protects the batch. A second commit with any key returns `alreadyCommitted:true` and creates nothing.
- Ready rows and `create_with_reason` duplicates become **Draft** cases through `CaseFactory`. They get `Source=Import`, `ImportBatchId` set, `DuplicateOverrideReason` set to the reason, a `DebtSnapshot` sourced «استيراد ملف · {file}», and a party, contract, property and mortgage.
- **No `OwnerAccess` and no SMS are created.**
- `link` rows become Skipped, with an `import.row_linked` audit on the existing case; that case's data is not modified. `skip` rows become Skipped. Undecided duplicates stay pending.
- The audit writes one `case.draft_created` per case plus `import.committed`.

Deviations:
- **Route prefix and methods:** the spec says `/imports` and `PATCH …/decision`; the task named `/api/cases/imports` and `POST …/decision`, and those are what I implemented.
- **File format:** CSV only. XLSX parsing is not implemented, because the task specified CSV and no spreadsheet dependency was added.
- **«ربط» (link)** was undefined in the design (conflict #11). I implemented it as: attach the row to the existing open case as an audit record, and create nothing new.
- **The count on «استيراد N صفاً»** is `counts.importable`, which is ready rows plus `create_with_reason` decisions.
- **Idempotency on upload:** the upload is not `.Idempotent()`, matching the existing document upload, because the idempotency hash cannot see the file body. A repeated upload only creates another validated batch and has no side effects.

Tests (`ImportTests`):
- **Seed:** the seeded batch is at step 2 with 9/3/1/5 rows.
- **Validation:** a 10-row file produces 2 ready, 1 duplicate and 7 errors, with the exact codes and messages, and every issue has a fix.
- **PII at rest:** no plaintext PII appears in `RawJson`.
- **Decisions:** a decision on a ready row gets 409, create without a reason gets 400, link gets 200, and a decision after commit gets 409.
- **Commit:** it creates 2 drafts with encrypted PII, no owner access and no SMS. A second commit with a different key creates nothing.
- **Follow-on:** the imported draft finalizes through the wizard submit, and re-uploading the file turns its rows into duplicates.
- **Closed-owner duplicate:** link is refused (422), and the case is created with the documented reason.
- **Access:** the template downloads, missing columns and the `.xlsx` extension get 400, the case officer gets 403, Sunbula gets 404 on the batch and its commit, and the batch is absent from Sunbula's list. The errors file downloads.

## C01 — Cancellation (إلغاء الحالة…)

| Route | Permission | Notes |
|---|---|---|
| `GET /api/cases/{ref}/cancellation-requests` | case.view | History plus `pending` and `canRequest`. |
| `POST /api/cases/{ref}/cancellation-requests` | case.cancel · **step-up** · idempotent | See the request steps below. |
| `POST /api/cases/{ref}/cancellation-requests/{id}/decision` | case.cancel_approve · **step-up** · idempotent | See the decision rules below. |
| Workspace `GET /api/cases/{ref}` → `sensitive.pendingCancellation` | — | `{id, reason, requestedBy, requestedAt, approver, dueOn, assignedToMe, canDecide, note}`, or null when there is none. The solution approvals inbox shows only `Subject=Solution`, so this block is how the approver reaches the request. |

The request (`{reason ≥10, expectedStatus?}`) goes through these steps in order:
1. It checks the expected status (stale → 409) and the transition's source states.
2. An existing pending request gives 409 `cancellation_pending`. This is also backed by the unique filtered index on `(Subject, SubjectId)`.
3. It evaluates the **`cancel` guards (open complaint)**. A failure gives 422 `guard_failed` with reasons, and the refusal is audited as blocked in its own transaction.
4. It routes to a member holding `case.cancel_approve` **who is not the requester**, preferring approver, then senior approver, then org admin.
5. It creates an `ApprovalRequest{Subject=Cancellation, SubjectId=caseId}`, with a notification, an approval task and a `cancellation.requested` audit event.

The decision (`{decision: approve|reject, reason ≥10}`) follows these rules:
- **If the requester or preparer tries to decide, they get 403**, and the attempt is audited as blocked (`cancellation.blocked`) even when their role holds both permissions.
- Someone other than the assigned approver gets 403.
- **Approve** runs `workflow.TransitionAsync(c, "cancel", requestReason)`. That call re-checks `case.cancel_approve`, the step-up and the open-complaint guard. It is not system-initiated, and it runs *before* `RecordAsync` in the transaction. On success the case's open tasks are closed.
- **Reject** leaves the case as it was.
- Both outcomes notify the requester and write a `cancellation.decision` audit event.

`Case.CancelReason` is the requester's reason, and the approver's reason is stored in `ApprovalRequest.DecisionReason`.

Owner access is **not** revoked automatically on cancellation, because the spec says nothing about it; that is left for product to decide.

Tests (`CancellationTests`):
- **Request:** a short reason gets 400, no step-up gets 403 `step_up_required`, and a role without case.cancel gets 403.
- **Routing and duplicates:** the request routes to نورة, and a duplicate gets 409.
- **Workspace:** it shows `canDecide` false for the requester and true for the approver.
- **Decision:** the approver without step-up gets 403, and the unassigned senior approver gets 403.
- **Approve:** the case is cancelled with the requester's reason, a transition audit is written, and a re-decision gets 409.
- **Maker-checker:** a user with *both* permissions cannot approve their own request (403, audited as blocked), and a reject keeps the status.
- **Complaints:** an open complaint blocks the request (422, audited, and no request is created), and a complaint opened after the request blocks the approval (422, with the case and the request unchanged).

## Seed (`Seed/DevSeeder.CaseTabs.cs`)

**RH-2026-004172**, aligned with the canon:
- **Parties and contact:**
  - parties: «نورة ع.» (occupant, spouse, no contact, no financial data) and «سلمان ع.» (informal representative, son, POA not uploaded);
  - the owner is marked as «موظف · قطاع خاص»;
  - contact preferences: «9 ص – 5 م، أيام العمل», platform and SMS, and the «بحضور ابنه» communication need.
- **Finance and property:**
  - contract dated 2021-05-10, 1,450,000, 240 months with 176 remaining;
  - sync status OK and a partial payment in 2026-05;
  - property built in 2019;
  - mortgage registered 2021-05-12, insurance until 2027-05-11, deed matched 2026-08-29, and ماجد's legal note dated 2026-08-28.
- **Documents and valuation:**
  - documents: «تعريف بالراتب» and an internal «مذكرة المراجعة القانونية», plus an open request to renew the national ID due 2026-10-03, using the design's text;
  - valuation: range 1.58M–1.72M, «المقارنة (5 صفقات) + التكلفة», inspection 2026-09-04 10:00;
  - assignment timeline events for ASG-2026-0418.
- **Analysis:** income source = salary statement v2, other obligations 2,100, and the design's three indicators and four options.

Other cases:
- RH-2026-004155 has a valuation report under review.
- RH-2026-003655 has a pending cancellation assigned to نورة الشهري.
- The Alufuq batch «محفظة_سبتمبر_2026.csv» holds 9 rows (3 ready, 1 duplicate of RH-2026-004172 and 5 errors, mirroring the design), and is left at step 2.

## Open issues / follow-ups

1. **Integrator:** generate the migration for `owner_accesses.communication_needs`. Until then, plain `dotnet test` and `migrate` report pending model changes.
2. **Permissions** would ideally gain `mortgage.legal_review` and `audit.export` (auditor-only export per design). Both are left out to keep `Permissions.cs` and `SystemRoles.cs` untouched.
3. The **providers module** should emit assignment audit events that follow the timeline convention above, and create the valuer assignment from a `revaluation` task.
4. **Import:** XLSX parsing, and `PUT /imports/{id}/file` (replace the file), are not implemented. The UI can upload a new batch instead.
5. **Invitation links at rest in the SMS log.** The sandbox gateway stores full SMS bodies in `comms.outbound_messages`, so the plaintext invitation link (token) sits next to its hash. The last 4 digits of the ID plus the OTP are still required to sign in. A production SMS adapter must not persist bodies that contain invitation links, or must redact them.
6. The watermark stamping on downloads (existing) and `POST /documents/versions/{vid}/download` from the spec are unchanged.
