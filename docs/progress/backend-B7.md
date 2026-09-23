# Backend — B7: provider portal, institution admin, platform admin

Scope: `docs/design-specs/B7-provider-admin.md` (V01–V04, A01–A07, PA01–PA13), `B2-shared-public.md` §S02 (demo request)
and §S05 (staff invitation acceptance). Backend only. All endpoints are minimal APIs registered in `Modules/ModuleRegistry.cs`.

Tests: `RAHOON_TEST_ENSURE_CREATED=1 dotnet test` from `server/` → **59 passed, 1 failed**. The failure is
`SecurityTests.Database_rejects_audit_tampering`, and it already failed before this work. The append-only trigger
ships in the migrations, and EnsureCreated does not create it.
New test classes:

| Class | Tests | Covers |
|---|---|---|
| `ProviderAssignmentTests` | 5 | provider isolation, the access window (write → read-only 7 days → gone), the independence declaration, return/resubmit, accept creating a valuation, share rules, the seeded inbox |
| `InstitutionAdminTests` | 6 | S05 invitation (domain, password policy, single use, MFA), role change by a second admin, suspension revoking sessions, approval-limit maker-checker and PA07 minima, template publisher ≠ editor, A01/A06/A03 bounds, permission matrix, report CSV and the auditor-only report |
| `PlatformAdminTests` | 4 | S02 validation and the PA02 approve → onboarding → admin acceptance flow, masked monitoring, dual approval of temp access (institution admin + auditor), the requester unable to approve, view logging, expiry and notification, PA11 masking and signed export, PA10 metadata only, PA07 fixed rules |
| `B7UnitTests` | 6 (3 facts + 1 theory × 3 cases) | password policy, personal e-mail domains, template variables and tone check |

**Cross-endpoint isolation check.** Live seeded assignments keep مصرف الأفق and السنبلة in عمر's readable organizations,
and an active temporary grant does the same for رنا. Every existing endpoint guarded only by `RequireSession()`, or by
a permission a provider or platform role holds, was grepped. The only ones are notifications (per user), document types
(catalog), `auth/*` and the B7 groups (`RequireOrg`). The tests also assert 403 for provider and grant-holding platform
users on search, tasks, complaints, portfolio, approvals and case routes.

Test helpers:

- `Infrastructure/AdminScenarios.cs` creates fresh users, so tests never suspend or re-role seeded people. It has three helpers:
  - invite + accept
  - a user whose test-only role holds both sides of a maker-checker pair
  - a user holding an existing role
- `TestClient` gained multipart upload, raw GET and raw POST.
- `ApiFixture` sets `Jobs:TempAccessExpiry=false`.

---

## Provider portal (V01–V04) — `Modules/Providers`

Access rule (A-11, PA12 «التكليف + 7 أيام»):

- The provider can write until delivery.
- After delivery it has **read-only** access for 7 days.
- After that the assignment answers exactly like an unknown id (404, same body).

A return reopens write access. The 7-day clock restarts at the next delivery (C1). On accept, `DeliveredAt` is the accepted
submission's time and `AccessExpiresAt = DeliveredAt + 7 days`.

A provider's data organizations include a lender while *any* of its assignments there is live. The middleware grants
this, and the EF filter therefore allows it. For that reason every provider query goes through
`ProviderAssignmentService.ProviderVisible()`. That filter requires own provider org, not cancelled, and access not
expired. Documents are reachable only through `SharedDocumentIds`. Rows written by the provider carry the lender's
`OrganizationId`, and audit events go to the lender's chain.

| Screen | Route | Guard |
|---|---|---|
| V01 inbox | `GET /api/provider/assignments?status=active\|delivered` | `RequireOrg(ServiceProvider)` + `assignment.work` |
| V02 detail | `GET /api/provider/assignments/{id}` | same |
| V02 shared doc | `GET /api/provider/assignments/{id}/documents/{documentId}` (audited + `DownloadLog`) | same; only while access is valid |
| V03 RFI | `GET/POST /api/provider/assignments/{id}/messages` | POST only while writable |
| V02 inspection | `POST /api/provider/assignments/{id}/inspection/confirm` `{at?}` | writable |
| V04 submit/resubmit | `POST /api/provider/assignments/{id}/submissions` (multipart) | writable |

The V04 submission is multipart and needs:

- `file` (a PDF that passed the scan)
- `marketValue`, `rangeLow` and `rangeHigh`
- `methodology`
- `comparablesCount`
- `inspectionDate` (≤ today)
- the `checklist` keys `comparables_12m`, `occupancy_stated` and `photos_no_faces`
- `independenceDeclared=true`

All of these are **required** (C2). The submission creates version n and a document version
«تقرير التقييم — ASG-…» on the case.

Lender side (`AssignmentEndpoints`, group `/api/cases/{reference}/assignments`, `case.view` + `CaseAccess`):

| Route | Permission |
|---|---|
| `GET /options` (provider orgs, shareable docs with reasons) | `provider.assign` or `valuation.assign` |
| `POST /` create `ASG-YYYY-NNNN` (counter `asg:{year}` in `cases.reference_counters`) | `provider.assign` or `valuation.assign` (non-valuation types need `provider.assign`) |
| `GET /`, `GET /{id}` | `case.view` |
| `GET/POST /{id}/messages` (a reply may share more documents) | `case.view` |
| `POST /{id}/submissions/{version}/review` `{decision: accept\|return, notes[], resubmitDueOn, note}` | `valuation.review` |

- **Accept** creates or updates the `ValuationReport`, which becomes Accepted. Validity is the smaller of the institution
  rule and 90 days. Other accepted valuations of the case are superseded, and the report document is verified.
- **Return** records itemised notes and a new deadline, and notifies the provider.
- `ProviderAssignmentService` (scoped) exposes `CreateAsync`, `ReviewAsync`, `ProviderVisible`, `EnsureWritable` and the
  notification helpers for other modules.

**Detail payload (V02)** contains:

- scope rows (`"k — v"` split into key and value)
- the inspection and a masked contact («سلطان ح. (المالك) عبر المنصة»)
- shared documents
- access mode and expiry, stated explicitly
- the return alert
- actions with hidden-vs-disabled reasons

It never includes the case reference, debt figures, other parties or identity numbers (asserted in the tests).

---

## S05 staff invitation — `Modules/Identity/StaffInvitationEndpoints.cs`

| Route | Guard |
|---|---|
| `GET/POST /api/settings/invitations`, `POST …/{id}/resend`, `POST …/{id}/cancel` | `RequireOrg(Lender)` + `user.manage` |
| `GET /api/public/staff-invitations/{token}` | public; the state is `active\|expired\|used\|revoked\|invalid` |
| `POST /api/public/staff-invitations/{token}/password-check` | public, rate-limited |
| `POST /api/public/staff-invitations/{token}/accept` `{fullName, password, ackPolicy, phone?, currentPassword?}` | public, rate-limited |
| `POST /api/public/staff-invitations/{token}/report` («لا أعرف هذه الدعوة») | public, rate-limited |

**Creating an invitation**

- The e-mail domain must be in the institution's `AllowedEmailDomains`.
- A Saudi mobile is required, because MFA is by SMS.
- The role and team must belong to the institution.
- The token is stored as SHA-256 and expires after 7 days.
- A sandbox e-mail and SMS are sent. The stored e-mail body has the token **redacted**.
- The raw token is returned as `sandboxToken` only when `Auth:ExposeSandboxOtp` is set, which is the same switch as `sandboxCode`.

**Password policy (S05)**

- at least 12 characters
- not in a local list of common/breached passwords (a k-anonymity service is an open integration)
- no name or e-mail part

**Accepting**

- Acceptance is single use.
- It creates or activates the `User` and the Active `Membership`, including the seeded «invited» membership pattern.
- The policy acknowledgement is recorded in the audit event.
- It then opens an **MFA-pending session and issues the login SMS code**. The client completes it with the existing
  `POST /api/auth/mfa/verify`.
- Someone who already holds an account at another institution confirms with `currentPassword` instead of setting a
  password. Wrong attempts use the login lockout counter (5 attempts → 15 minutes).
- The first admin's acceptance switches an `Onboarding` organization to `Active`.

## Institution admin (A01–A07) — all `RequireOrg(Lender)`

| Screen | Routes | Permission |
|---|---|---|
| A01 | `GET/PUT /api/settings/organization` | `org.settings` |
| A02 | `GET /api/settings/users` | `user.manage` or `role.change_approve` |
| A02 | `GET /api/settings/roles` | `user.manage` or `role.change_approve` |
| A02 | `POST /api/settings/users/{membershipId}/suspend\|reactivate` `{reason}` | `user.manage` |
| A02 | `POST /api/settings/users/{membershipId}/role-change-requests` | `user.manage` |
| A02 | `GET /api/settings/role-change-requests` | `user.manage` or `role.change_approve` |
| A02 | `POST /api/settings/role-change-requests/{id}/approve\|reject` | `role.change_approve` |
| A02 | `GET /api/settings/permission-matrix` | `user.manage`, `role.change_approve` or `org.settings` |
| A03 | `GET/POST /api/settings/document-rules`, `PUT/DELETE /api/settings/document-rules/{id}` | `org.settings` |
| A04 | `GET /api/settings/approval-limits`, `GET /versions` | `limits.manage` |
| A04 | `POST /proposals`, `POST /proposals/{v}/submit\|approve\|reject` | `limits.manage` |
| A05 | `GET /api/settings/templates`, `GET /{code}`, `POST /{code}/tone-check` | `template.edit` or `template.publish` |
| A05 | `PUT /{code}` (save draft), `POST /{code}/submit` | `template.edit` |
| A05 | `POST /{code}/publish`, `POST /{code}/return` | `template.publish` |
| A06 | `GET/PUT /api/settings/sla-rules` | `org.settings` (GET also `reports.view`) |
| A07 | `GET /api/settings/reports`, `POST /api/settings/reports/{key}/export` (CSV) | `reports.view`; «نشاط المستخدمين الحساس» needs role `auditor` |
| PA06 (institution side) | `GET /api/settings/temp-access`, `POST /{id}/approve\|reject` | `user.manage`, and the approver must hold role `org_admin` |

### A01 organization settings

- Name, city and owner language (`ar`/`en`) are editable.
- The idle timeout must fall within the platform bounds of **10–60 minutes**. This is an assumption; the B2 default is 30.
- Allowed domains must be valid and not personal, 1 to 5 of them.
- `mfaRequired=false` is refused, because MFA is mandatory for all roles.

### A02 users and role changes

- **Suspension:** a reason is required and the action is audited. The user's sessions for that membership are revoked
  at once. Self-suspension and suspending the last active org admin are refused.
- **Role change:** a maker-checker flow on `RoleChangeRequest`.
  - The approver must differ from the requester (`403 maker_checker`) and from the target.
  - Approval needs an MFA step-up.
  - One pending request is allowed per member.

### A03 document rules

A rule's validity may not exceed the catalog type's validity. For `valuation_report` the limit is the platform
maximum, 90 days (PA07).

### A04 approval limits

- Statuses: `draft → pending_approval → effective`, plus `superseded` and `rejected`.
- The approver must differ from the proposer. Approval needs an MFA step-up.
- The PA07 minima are checked at proposal and again at approval:
  - `separationOfDuties` and `dualApprovals` cannot be false.
  - Every approving tier except `risk_committee` needs a waiver cap of 10% or less (the platform value).
- On approval, the previous effective version and older open proposals are superseded.

### A05 templates

- `CommunicationTemplate` is not tenant-filtered by EF, so every query scopes explicitly.
- An institution edit creates an org-owned version. Its number continues after the highest base or own version.
- A tenant's effective template is its latest published copy, otherwise the latest base.
- Unknown variables are rejected. English bodies must use the aliases listed in `TemplateRules.EnglishAlias`.
- **Tone check:**
  - threatening or coercive words: **blocking**
  - deadline and «how to ask» mentioned: warning
  - under 60 words: warning
  - English variant updated with the Arabic: info
- Publishing requires `template.publish` and a publisher who is **not the last editor** (`403 maker_checker`). The
  previously published version of the same scope becomes `Superseded` (a new enum value).

### A06 stage SLAs

SLAs are in business days, between 1 and 60. `awaiting_customer` and `negotiation` must be at least the platform
owner-response minimum of **7**.

### A07 reports

Four reports are defined. Exports are CSV with a BOM and neutralised formulae. They are aggregated with no PII, and each
export is audited with its SHA-256. «نشاط المستخدمين الحساس» is limited to the `auditor` role (90 days of sensitive
audit events).

## Platform admin (PA01–PA13) — all `RequireOrg(Platform)`

| Screen | Routes | Permission |
|---|---|---|
| S02 (public) | `GET /api/public/lookups/demo-request`, `POST /api/public/demo-requests` | anonymous, rate-limited (`auth` policy), honeypot `website`, at most 2 per e-mail per 24 h |
| PA01 | `GET /api/platform/ops` | `platform.ops` |
| PA02 | `GET /api/platform/institution-applications`, `GET /{reference}`, `POST /{reference}/review` `{action: in_review\|more_info\|approve\|reject}` | `platform.institutions` |
| PA03 | `GET /api/platform/institutions?kind=` | `platform.institutions` or `platform.ops` |
| PA04 | `GET /api/platform/users` | `platform.users` |
| PA05 | `GET /api/platform/permissions` (from `P.Catalog`) | `platform.users` or `platform.ops` |
| PA06 | `GET /api/platform/cases/monitor?organizationId=&status=&page=` | `platform.ops` |
| PA06 | `POST /api/platform/temp-access` `{handle, reason, supportTicketRef, durationMinutes}` | `platform.temp_access` |
| PA06 | `GET /api/platform/temp-access` | `platform.temp_access`, `platform.temp_access_approve` or `platform.audit` |
| PA06 | `POST /api/platform/temp-access/{id}/approve\|reject` | `platform.temp_access_approve` |
| PA06 | `POST /api/platform/temp-access/{id}/revoke` | requester or auditor |
| PA06 | `GET /api/platform/temp-access/{id}/view?screen=overview\|documents\|templates` | requester only, while active |
| PA07 | `GET/PUT /api/platform/defaults/approval-rules` | `platform.defaults` (GET also `platform.ops`) |
| PA08 | `GET/POST /api/platform/defaults/document-types`, `PUT /{key}` | `platform.defaults` |
| PA09 | `GET /api/platform/defaults/templates`, `GET/PUT /{code}`, `POST /{code}/publish` (publisher ≠ editor) | `platform.defaults` |
| PA10 | `GET /api/platform/complaints?level=all` | `platform.complaints` |
| PA11 | `GET /api/platform/audit?organizationId&type&from&to&q&before`, `POST /api/platform/audit/exports` `{reason, …}` | `platform.audit` |
| PA12 | `GET /api/platform/retention-policies`, `PUT /{id}` | `platform.privacy` |
| PA13 | `GET /api/platform/services` | `platform.ops` |

### PA01 operations

Aggregate figures only:

- active tenants and the quarter's additions
- case counts by state
- users active today
- open temporary access with its next expiry
- integration states
- pending applications
- the attention list

### PA02 applications

- A demo request creates an `InstitutionApplication` with reference `APP-YYYY-NNNN` (counter `app:{year}`, seeded at 30,
  so the first new one is APP-2026-0031). It creates no account.
- **approve** provisions an `Organization` with status `Onboarding` and the platform defaults:
  - lender role templates
  - stage SLAs
  - approval-limit v1
- It then invites the contact as `org_admin`. The organization becomes `Active` when that admin accepts.

### PA06 monitoring and temporary access

**Monitoring**

- The only identifier returned is `Mask.OpaqueCaseId` (salted with `Security:MonitorSalt`, falling back to a key
  derived from `Security:PiiLookupKey`).
- Each row also carries an opaque `handle`: the case id encrypted with ASP.NET Data Protection. A request uses it to
  point at the case without the client ever seeing the id.
- No names, national IDs, amounts or `RH-` references are returned.

**Requesting**

- A request needs:
  - a reason of at least 10 characters
  - a ticket reference matching `SUP-YYYY-NNNN`
  - a duration of 15–240 minutes
- The scope is fixed: read-only, documents and templates tabs, no download.

**Approval and use**

- The request becomes **Active** only when both approvals are present:
  - (a) an `org_admin` of the case's institution
  - (b) a platform auditor
- Both approvals need a step-up, and neither approver may be the requester (`403 maker_checker`).
- Each view writes a `TempAccessViewLog` row and an audit event in the institution's chain. Views are projections of the
  granted case only.

**Expiry**

- Expiry happens lazily in every temp-access, monitoring and ops endpoint, and every minute in `TempAccessExpiryService`.
- The institution's org admins are then notified with the number of screens opened.

### PA07 defaults

The rules are held in the new `PlatformDefaultRule` table. Separation of duties and the dual approvals for referral and
closure are **not editable**. Three values are editable:

| Rule | Allowed range |
|---|---|
| Waiver without committee | 0–50% |
| Valuation validity | 30–180 days |
| Owner-response minimum | 1–30 days |

`PlatformMinima` enforces these on A03, A04 and A06.

### PA10 complaints

- The endpoint returns KPIs: open, escalated, overdue and average resolution time.
- Rows are escalated complaints across tenants by default, or all complaints with `level=all`.
- Rows carry **metadata only**. The subject is returned only when the caller holds an active grant for that case.

### PA11 audit log

- Events from all tenants are returned in pages.
- `RH-…` references in titles, details and reasons are masked, and case ids are shown as opaque ids.
- **Signed export:** a CSV carrying hash and prev_hash per row. Its SHA-256 is sent in the `X-Content-SHA256` header and
  recorded in an `audit.exported` event together with the stated reason.

### PA12 retention and privacy

- Policies stay drafts until confirmed. Making one `effective` requires a legal reference (A-12).
- Data-subject requests are shown as zero counts with a note, because the entity is out of scope for Phase 1.

### PA13 service monitoring

- database up, with its latency
- the breach monitor and temp-access expiry heartbeats (new `JobHeartbeat`)
- integration states
- outbound SMS and e-mail counts for the last 24 h

---

## Schema changes (need the consolidated migration — none created here)

New tables in the `admin` schema, configured in `Modules/Administration/AdminConfigurations.cs`:

- `temp_access_view_logs`
- `platform_default_rules`
- `job_heartbeats` (string key)

New columns:

- `admin.temp_access_requests`: `rejected_by_user_id`, `decision_note`, `closed_at`, `institution_notified_at`
- `admin.institution_applications`: `verification_note`, `consent_at`, `consent_policy_version`, `decided_at`, `created_organization_id`
- `admin.retention_policies`: `after_action`, `legal_reference`, `updated_at`, `updated_by_user_id`

New enum values (stored as text, so no column change): `InvitationStatus.Reported` and `TemplateStatus.Superseded`.

⚠️ Until that migration exists, `MigrateAsync`, `dotnet run -- migrate|seed|reset-demo` and CI (which uses migrations)
fail on the new tables. Tests run with `RAHOON_TEST_ENSURE_CREATED=1`.

## Seed (`Seed/DevSeeder.ProviderAdmin.cs`, one call in `SeedAsync`)

**Provider assignments** for مكتب تقييم معتمد «ب» (عمر العنزي):

- **ASG-2026-0871**: in progress. Inspection 2026-09-24 10:00 is confirmed; due 2026-09-26. Three shared documents: the
  deed (masked), «مخطط البناء» and «نموذج التقرير المطلوب». The RFI thread matches the spec.
- **ASG-2026-0864**: returned v1 with the two notes from the spec; resubmission due 2026-09-27.
- **ASG-2026-0880**: new, from شركة السنبلة.

Two new catalog types were added for these documents: `building_plan` and `report_template`.

**Institution (مصرف الأفق)**

- Second org admin **سعود الراشد** (`s.alrashed@alufuq.example`, «المدير التنفيذي للمخاطر»). This resolves C5 so that
  maker-checker has a distinct approver.
- **بدر السالم** has a pending invitation (dev link `/invite/staff-demo-badr`, expires 2026-09-30). His password and MFA
  enrolment were cleared.
- Approval-limit **v5** is pending approval: «رفع حد «المعتمد» من 2,000,000 إلى 2,500,000 ر.س.», proposed by ليلى.
- The institution copy of **TPL-DOCREQ-01 v3** is pending compliance.

**Platform**

- The six PA07 rules.
- Three extra retention categories, with the after-expiry actions filled in.
- Applications **APP-2026-0017/0019/0021/0022** at each stage.
- One pending temp-access request by رنا (SUP-2026-1201, two hours).
- Base templates TPL-VISIT-01 and TPL-BREACH-01.

Reference counters are set to `asg:2026=880` and `app:2026=30`.

## Decisions on the spec's conflicts

| # | Decision |
|---|---|
| C1 | A return reopens write access (`AccessExpiresAt=null`). The 7-day read-only clock starts at each delivery; accept keeps `DeliveredAt + 7d` of the accepted version. |
| C2 | Submitting requires all three checklist items plus the independence declaration (400 per field); the detail carries enabled/reason. |
| C3 | Two approvals are required: an institution `org_admin` AND a platform auditor (catalog + PA06), not the institution only (B2 help copy). Duration is ≤ 4 h, with options 30/60/120/240 min. |
| C5 | Added a second institution admin in the seed. Approval-limit approval = another `limits.manage` holder (no dedicated risk-executive role). |
| C8 | «إعداد حل» = `solution.prepare`, «إدارة المستخدمين» = `user.manage` (catalog keys unchanged). |
| C9 | Reports live under `/api/settings/reports` (A07). The UI may also link them from the top-level «التقارير». |
| C11 | PA13 reports only the handoff enum. A degraded SMS state would need a new enum value; not added. |
| C12 | Provider inbox SLA is **calendar** days; A06 stage SLAs stay business days. |
| C13 | Undrawn screens got minimal APIs: PA03, PA04, PA08, PA09, the A05 list, the A03 form, the A06 edit, the PA02 review detail and the temp-access approval lists. |
| C15 | Document-rule and SLA edits are **not** maker-checker. They are validated against platform minima and audited with before/after values. |

## Other decisions and deviations

- **Demo request reference:** `APP-YYYY-NNNN`, as the task asks, instead of B2's `DR-YYYY-NNNN`. The request itself is the
  PA02 application.
- **Seed assignments** use real seeded cases so the lender side stays consistent. The labels differ from the sample
  strings:
  - 0871 → RH-2026-004155 (دور سكني، حي العارض — its owner is «سلطان ح.», matching V02's contact)
  - 0864 → RH-2026-003988 (شقة، حي الروضة، جدة)
  - 0880 → RH-2026-005101 (فيلا، حي أبحر، جدة)
- **Title deed shared with a provider:** it is listed as «صك الملكية (مخفي الأرقام)» with the masked deed number, and it
  is **not downloadable**, because no redaction service exists.
- **Never shareable with providers:** identity, income and hardship types (catalog `Sensitive`) and debt or agreement
  documents (financing contract, agreement, payment proof, clearance, lien release, sale consent).
- **Messages after delivery:** the thread is read-only once delivered (strict §5.4). V03's «read-only after submission-expiry»
  could be read as allowing messages; we chose the strict reading.
- **Approval-limit activation:** a policy becomes effective at approval time (`EffectiveFrom` is kept, and bumped to today if
  it is in the past). `ApprovalRouting` ignores future dates, so scheduled activation is outstanding.
- **Owner-response minimum (7 days):** enforced on the SLA rules, where A06 places it. Approval-limit policies have no
  owner-response field.
- **Tenant-scoped offer template:** `TPL-OFFER-01` lookups in `ApprovalEndpoints` and `SolutionEndpoints` now prefer the
  tenant's own published copy, otherwise the base. They previously took the highest version of any tenant, which would
  have leaked one institution's template into another's offers.
- **S05 e-mail:** the GET returns the full invitation e-mail. The design shows it in a locked field, and only the token
  holder can read it.

## Shared files touched

- `Modules/ModuleRegistry.cs`: service registrations and `Map` calls.
- `Seed/DevSeeder.cs`: one call line.
- `Modules/Solutions/ApprovalEndpoints.cs` and `Modules/Solutions/SolutionEndpoints.cs`: one line each (tenant-scoped template lookup).
- `Modules/Agreements/BreachMonitor.cs`: one heartbeat line.
- Enum values: `Modules/Identity/IdentityEntities.cs` and `Modules/Communications/CommunicationEntities.cs`.
- Tests: `tests/.../Infrastructure/ApiFixture.cs` (one config line) and `TestClient.cs` (multipart and raw helpers).
- Not edited: `RahoonDbContext.cs` and the existing `Configurations.cs`.

## Open issues

- The consolidated EF migration is needed (see above).
- Integrations not yet in place:
  - document redaction, for downloadable masked deeds
  - a k-anonymity breached-password service
  - a real e-mail provider
- The CAPTCHA recommended for S02 is not implemented.
- Not implemented:
  - provider submission drafts (`PUT …/submission/draft`)
  - the data-subject request entity
  - scheduled activation of future-dated approval limits
  - a dedicated rate-limit policy for public forms (they reuse the `auth` policy plus a per-e-mail cap)
