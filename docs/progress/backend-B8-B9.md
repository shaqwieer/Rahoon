# Backend progress — B8 Voluntary sale · B9 Service ecosystem

Scope: ASP.NET Core API only (`server/`). Specs: `docs/design-specs/B8-voluntary-sale.md`, `docs/design-specs/B9-service-ecosystem.md`.

**No EF migration was created** (per instructions). The new tables exist in the model only; tests run with
`RAHOON_TEST_ENSURE_CREATED=1 dotnet test` (from `server/`). The consolidated migration must add schemas `sale` and
`ecosystem`, the partial unique indexes and the check constraints listed below. `dotnet run -- seed` against a migrated DB
will fail until that migration exists.

## New modules and shared-file edits

| Area | Files |
|---|---|
| B8 `Modules/Sale/` (schema `sale`) | `SaleEntities.cs`, `SaleConfigurations.cs`, `SaleCalculator.cs`, `DisclosurePolicy.cs`, `SaleService.cs`, `SaleEndpoints.cs` (lender), `SaleApprovalEndpoints.cs` (checker), `BrokerSaleEndpoints.cs`, `OwnerSaleEndpoints.cs` |
| B9 `Modules/Ecosystem/` (schema `ecosystem`) | `EcosystemEntities.cs`, `EcosystemConfigurations.cs`, `ProviderDirectory.cs`, `ProviderOnboardingEndpoints.cs`, `PlatformProviderEndpoints.cs`, `InstitutionProviderEndpoints.cs`, `ProviderInvoiceEndpoints.cs`, `WorkflowModel.cs`, `WorkflowDesignerEndpoints.cs`, `Reporting.cs`, `BillingEndpoints.cs`, `ConditionalIntegrations.cs` |
| Seeds | `Seed/DevSeeder.Sale.cs`, `Seed/DevSeeder.Ecosystem.cs` |
| Tests (27 new cases) | `SaleUnitTests.cs` (10 cases), `SaleTests.cs` (4), `EcosystemUnitTests.cs` (7 cases), `EcosystemTests.cs` (6) |

Shared files touched (minimal):
- `Modules/ModuleRegistry.cs` — 3 scoped services (`SaleService`, `ProviderDirectory`, `ConditionalIntegrations`) + 12 `Map` calls.
- `Modules/Cases/CaseWorkflow.cs` — **one new transition row** `sale_withdrawn` (`VoluntarySale → ProposedSolution`, no actor permission, reason required). Nothing else changed; `start_voluntary_sale` / `sale_completed` / the `owner_sale_consent` guard are used as-is.
- `Seed/DevSeeder.cs` — two call lines (`SeedEcosystemAsync`, `SeedVoluntarySaleAsync`) before `FlushAuditAsync`.
- `RahoonDbContext.cs`, `Permissions.cs`, `SystemRoles.cs`, `Program.cs`: **not edited** (entities use `db.Set<T>()`; configurations are picked up by `ApplyConfigurationsFromAssembly`; no new permission keys were needed).

## B8 — Voluntary sale

Entities (`sale` schema): `VoluntarySale` (track, VS-YYYY-NNNN buyer ref, estimate snapshot, listing, broker link; one *open* track per case — partial unique index), `SaleConsent` (formal scoped consent: min price, 90-day mandate, visit days/window, status Signed/Withdrawn/Expired/Fulfilled), `SalePrepItem` (8-item checklist), `BuyerOffer` (OF-NN; no buyer identity stored), `SaleTrackingStep` (8-step timeline, source internal/external-manual). Reused: `ConsentRecord` (kinds `sale_consent`, `sale_scope_consent`, `sale_offer_acceptance`), `ApprovalRequest` (`Subject = Sale`), `ProviderAssignment` (`Type = Brokerage`, `Scope = ["sale_file"]`), `Reconciliation` (draft, basis `voluntary_sale`).

| Screen | Routes | Permission / guard |
|---|---|---|
| D15a explain | `GET /api/owner/sale/explain` | `RequireOwner` (session pinned to one case) |
| D15 owner request | `POST /api/owner/sale/request/otp`, `POST /api/owner/sale/request` {message, proposedMinPrice, acknowledgements[4], code} | owner; case in a solution state; no open track; OTP context `sale:request:{caseId}` → `ConsentRecord(kind "sale_consent")` |
| D15b scoped consent | `GET /api/owner/sale/consent`, `POST /api/owner/sale/consent/otp`, `POST /api/owner/sale/consent` {minPrice, visitDays, visitWindow, acknowledged, code} | owner; only after the decision is approved; OTP context `sale:consent:{saleId}`; creates the 8 prep items |
| D16 progress / withdraw | `GET /api/owner/sale`, `POST /api/owner/sale/withdraw` {reason, confirm} | owner; allowed until an offer is accepted by the owner; audited `sale.withdrawn` |
| D16 offers (anonymised) | `GET /api/owner/sale/offers`, `POST …/offers/{code}/accept/otp`, `POST …/offers/{code}/accept`, `POST …/offers/{code}/decline` | owner; only offers shared with the owner; OTP context `sale:offer:{offerId}`; price ≥ owner minimum |
| Sale tab aggregate | `GET /api/cases/{ref}/sale` | lender `case.view` via `CaseAccess`; returns stage 0–6 + stageNames |
| L27 decision | `GET /api/cases/{ref}/sale/decision`, `POST /api/cases/{ref}/sale/decision` {reason, otherFeesEstimate?, expectedStatus?} | `sale.manage`; prerequisites: owner request, valid valuation, legal review of mortgage, no open complaint (refusal audited as blocked) |
| L27 approval (checker) | `GET /api/sale-approvals`, `GET /api/sale-approvals/{id}`, `POST /api/sale-approvals/{id}/decision` {approve\|return\|reject, reason} | `sale.approve`; ≠ preparer/submitter; assigned approver; **step-up**; approve runs `start_voluntary_sale` through `CaseWorkflow` (engine guards incl. `owner_sale_consent`) |
| L28/L29 file | `GET /api/cases/{ref}/sale/file`, `PUT /api/cases/{ref}/sale/prep-items/{key}` | `sale.manage`; locked until the scoped consent is signed and while the mandate is valid |
| L30 listing & disclosure | `GET /api/cases/{ref}/sale/listing`, `PUT /api/cases/{ref}/sale/listing`, `POST /api/cases/{ref}/sale/listing/compliance-review` | edit `sale.manage`; review `template.publish` (compliance) and ≠ whoever prepared the summary; any edit resets the review |
| L31 broker | `GET /api/cases/{ref}/sale/broker-candidates`, `POST /api/cases/{ref}/sale/broker-assignment` {providerOrganizationId} | `sale.manage`; broker must be in the institution directory, accepted, licence not expired (expiring = warning); access expires at mandate end |
| Broker portal (sale file) | `GET /api/broker/sales`, `GET /api/broker/sales/{VS-ref}`, `POST /api/broker/sales/{VS-ref}/offers` | ServiceProvider org + `assignment.work`; strictly via a live Brokerage assignment that is the sale's broker assignment; listing only after compliance review; disclosure projection only |
| L32 offers | `GET /api/cases/{ref}/sale/offers`, `POST /api/cases/{ref}/sale/offers`, `POST …/offers/share-with-owner`, `POST …/offers/{code}/approval-request` {recommendation, attested} | `sale.manage` (maker); approval request only after the owner accepted, offer valid, price ≥ minimum |
| L32/L33 offer approval | same `/api/sale-approvals/{id}/decision` | `sale.approve`, ≠ maker, step-up; approve → other offers closed, **broker access revoked (`AccessExpiresAt = now`)**, consent fulfilled, tracking created; reject → owner's withdrawal right reopens |
| L33 tracking & completion | `GET /api/cases/{ref}/sale/tracking`, `PUT /api/cases/{ref}/sale/tracking/{key}`, `POST /api/cases/{ref}/sale/complete` | `sale.manage`; external steps need date + reference; completion requires `payment_received` + `lien_release` → `sale_completed` → «بانتظار التسوية المالية» + draft reconciliation with the sale figures |

Money (server-only, `SaleCalculator`, decimal, away-from-zero rounding):
- before offers: `surplus = valuation − debt − valuation × brokerRate − otherFees` → 2,850,000 − 2,240,000 − 57,000 − 8,000 = **545,000**;
- per offer: `net = price × (1 − brokerRate)` → 2,724,400 / 2,744,000 / 2,763,600;
- distribution: `owner surplus = price − debt payoff − commission` → 2,800,000 − 2,240,000 − 56,000 = **504,000**; a negative remainder is reported as shortfall (lender recovery = min(debt, net)).

Disclosure: `DisclosurePolicy.Matrix` is the 8-row table verbatim; «العامة» is «لا» on every row and `Project(…, Public)` throws — there is no public listing route. Projections are built field-by-field from the matrix; owner identity, the owner's minimum, debt/default, the case reference and the lender name have no projection at all. Buyers get the generalised area label (e.g. «شمال الرياض»); exact location for buyers is «بعد الزيارة».

### Decisions on the B8 conflicts
1. **Consent order (resolved as recommended).** The owner's documented portal *request* (OTP + the four acknowledgements → `ConsentRecord` kind `sale_consent`) satisfies the `owner_sale_consent` transition guard. The formal scoped consent (kind `sale_scope_consent` + `SaleConsent`) is requested after the decision is approved and gates preparation, listing, broker assignment and offers. Because the existing guard only checks that a `sale_consent` record exists, the approval decision also passes `extraGuardFailures` when the track is no longer an open request (e.g. withdrawn), so an old record can never reopen a sale.
2. **Other fees.** The 8,000 «رسوم أخرى» is an assumption applied only to the pre-offer estimate (L27). Offer comparison and the distribution deduct only the broker commission (matching L32/L33); actual extra costs can be passed to `SaleCalculator.Offer(otherCosts)` later.
3. **Debt figure.** Figures use the current debt snapshot at each read/approval/completion (not the value frozen at request time).
4. **«≥ الحد الأدنى»** compares the offer **price** with the owner's consented minimum.
5. **Debt-letter prep item** is a checklist item (“ready”); the actual issue is the tracking step `debt_letter` (valid 30 days).
6. **Approval limits.** Solution approval-limit tiers do **not** apply to opening a sale (any `sale.approve` holder ≠ preparer). An offer that leaves a **shortfall** is a concession and is routed to, and can only be approved by, a senior approver / risk committee. «+ القانونية» is satisfied by the existing mortgage legal-review prerequisite, not by a second approval request.
7–8. Route family `/api/cases/{ref}/sale/*`; titles as in the spec (UI concern).
9. Not drawn screens were implemented as APIs: approver decision, offer approval review, owner comparison/accept, withdrawal.
10. Visit time window is owner-entered (`visitWindow`, optional) next to the visit days.
11. Expiring-licence brokers stay selectable with a warning; `broker-candidates` also flags `expiresBeforeMandate`.
12. Offer certainty is analyst/broker-entered.
- Opening the sale withdraws live amicable offers (`Offer.Status = Withdrawn`, solution version superseded) and supersedes pending solution approvals (rule 11).
- Withdrawal before approval supersedes the pending decision request (no case transition); after approval it runs `sale_withdrawn` → «حل مقترح».

## B9 — Service ecosystem

Entities (`ecosystem` schema): `ProviderProfile` (platform registry entry, one per provider org), `ProviderLicense`, `ProviderReviewDecision`, `InstitutionProvider` (institution directory; `IOrgOwned`), `ProviderInvoice`, `WorkflowVersion` (stages/rules as jsonb), `ReportSchedule` (`IOrgOwned`), `BillingPlan`, `InstitutionSubscription`, `PlatformInvoice`, `InstitutionIntegration`, `IntegrationAttempt`. Plain (non-`IOrgOwned`) rows are cross-party or platform metadata and every endpoint scopes them explicitly (provider by `ProviderOrganizationId`, lender by `LenderOrganizationId`/`InstitutionOrganizationId`, platform by permission).

| Screen | Routes | Permission |
|---|---|---|
| V05 directory | `GET /api/institution/providers?type=&licence=expiring`, `GET …/platform-directory`, `POST /api/institution/providers`, `PUT …/{providerOrgId}`, `GET …/{providerOrgId}/performance` | lender; read `org.settings` or `provider.assign`; edit `org.settings` |
| V06a onboarding | `GET /api/provider/onboarding`, `PUT …/steps/{1-6}` {data, advance} (autosave), `POST …/documents` (multipart kind/number/expiresOn/file), `POST …/submit`; `GET /api/provider/profile` | ServiceProvider + `org.settings` (provider admin) |
| V06b review / PA16 registry | `GET /api/platform/providers`, `GET …/applications`, `GET …/applications/{id}`, `POST …/applications/{id}/decision` {accept\|request_info\|reject, message}, `GET …/applications/{id}/documents/{licenseId}/file`, `POST /api/platform/providers/{providerOrgId}/status` {suspend\|reinstate, reason} | Platform + `platform.institutions` or `platform.defaults` |
| V07 invoices & performance | provider: `GET /api/provider/invoices`, `GET …/invoiceable`, `POST /api/provider/invoices` {assignmentReference}, `POST …/{number}/submit`, `GET /api/provider/performance`; lender: `GET /api/institution/provider-invoices`, `POST …/{id}/review` {approve\|reject, reason}, `POST …/{id}/payment` {reference} | provider `invoice.submit`; lender `invoice.approve` and ≠ submitter (also a DB check constraint) |
| PA14 workflow designer | `GET /api/workflows/{institutionId}`, `POST …/versions`, `PUT …/versions/{v}/stages/{key}`, `POST …/versions/{v}/rules`, `DELETE …/versions/{v}/rules/{ruleId}`, `POST …/versions/{v}/simulate`, `POST …/versions/{v}/submit`, `POST …/versions/{v}/approve`, `POST …/versions/{v}/return` | `platform.defaults` (any lender) or `org.settings` (own institution); approve/return `platform.defaults` + step-up |
| PA15 reporting | `GET /api/reports/definitions`, `POST /api/reports/query`, `POST /api/reports/export` (CSV, audited `report.exported`), `GET /api/reports/kpis`, `GET/POST /api/reports/schedules` | lender `reports.view`; schedule recipients must hold `reports.view` |
| PA17 billing | `GET /api/platform/billing`, `PUT …/plans/{key}`, `PUT …/subscriptions/{institutionId}`, `POST …/invoices/{number}/payment` {reference} | Platform + `platform.billing` |
| X01/X02 conditional | `GET /api/integrations/conditional` (lender staff, owner; platform with `platform.integrations` + `?institutionId`), `POST /api/owner/agreement/signature-sessions`, `POST /api/owner/installments/{no}/payment-attempts` | 409 `integration_unavailable` (with the manual alternative and «آخر محاولة») unless the platform state is `enabled`, the institution row is `enabled` and a real adapter is registered |

### Decisions on the B9 conflicts / gaps
- **Licence thresholds (#3):** amber «ينتهي قريباً» ≤ 60 days; directory status «تحذير» ≤ 30 days (still assignable); expired → «موقوف», never assignable (evaluated at read time — no background job yet).
- **Performance scope (#4):** institutions see only their own 12-month slice (on-time %, rework %, active); the provider's own `/api/provider/performance` aggregates across the institutions it served (46/48 = 96%, 4 reworks → 92% first-time in the seed). Rework is not applicable to brokers.
- **Onboarding (#5):** steps save incomplete; validation happens on submit (steps 1–5, all three documents present and unexpired, independence declaration). Accept is blocked while any checklist item is open (expiring ≤ 30 days, missing, unsigned data-protection agreement) — «طلب استكمال» is the path, as drawn.
- **Who approves a workflow version (#6, spec gap):** a *second* platform user holding `platform.defaults`, different from both the draft author and the submitter, with a fresh OTP step-up (DB check constraint `approved_by ≠ submitted_by`). Institution admins can view/draft/submit their own workflow but cannot publish. Locks are per rule (platform rules non-removable; submit re-checks all are present); stages are fixed (no free-form canvas); SLA editable 1–60 business days. New versions apply to new cases only — **case pinning to a version is not implemented** (would need a column on `Case`).
- **Simulation:** single hypothetical case (base tier from the effective approval-limit policy + rule actions) and a replay over up to 50 recent solutions (aggregates only). No SaveChanges, no audit, no idempotency record; the test asserts versions and audit counts are unchanged. Added-days per action are stated assumptions (`WorkflowModel.Actions`).
- **Reporting:** measures `avg_resolution_days`, `case_count`, `amicable_rate`; breakdowns `solution_kind_quarter`, `region_quarter`, `status`, `quarter`. Cells with n < 10 are suppressed server-side (value **and** count withheld, CSV «—»); no totals are emitted, so suppressed cells cannot be back-computed. Scheduled sends are recorded only (email is simulated).
- **Billing (#9):** plans are data; usage = live count of active cases per institution (aggregate only, the one cross-tenant read); invoice status «متأخرة N أيام» computed from the due date; no overage/proration logic.
- **X01/X02 (#10–12):** simulated mode is never shown to the owner (reported as unavailable); refusals record an `IntegrationAttempt` only — never a signature, consent record or payment. To plug in a provider: implement `ILicensedSigningProvider` / `ILicensedPaymentProvider`, register it in `Program.cs`, set the platform `IntegrationSetting` to `enabled` and the institution's `InstitutionIntegration` to `enabled`.
- **Invoice numbers** use `INV-{PROVIDERCODE}-{YYYY}-{NNN}` from the shared `cases.reference_counters` table; brokerage invoices fall back to `ProviderAssignment.FeeAmount` when no framework fee exists.

## Seeds (fictional)
- **RH-2026-004012 عائشة ف.** (negotiation): accepted valuation 2,850,000 (2026-09-05 → 2026-12-04), owner request 2026-09-20 19:12 via portal (consent record + OTP), proposed minimum 2,700,000, buyer ref `VS-2026-0031`, task for سارة — the L27 decision is ready to submit. Property 600/680 m², 2017.
- **RH-2026-003944 لطيفة ش.** (voluntary sale, added): same figures carried to the offers stage — decision approved by نورة, scoped consent 2026-08-28 (min 2,700,000, mandate to 2026-11-26, Thu/Sat 4–7 م), 7/8 prep items, listing reviewed by هند, broker «دار الوسطاء «أ»» (ASG-2026-0871, live until mandate end), OF-01/02/03 (2,780,000 / 2,800,000 / 2,820,000) shared with the owner.
- Providers: new orgs «دار الوسطاء «أ»», «مكتب الوساطة «ج»», «مكتب التقييم «و»», «مكتب التقييم «ح»» (licence expired → suspended), «فحص «ز» الهندسي», applicant «مكتب التقييم «هـ»» (`PRV-APP-0044`, submitted; insurance expires 2026-10-07; DPA unsigned) and «شركة الفحص «ط»» (`PRV-APP-0047`, draft at step 3). Existing «مكتب وساطة عقارية «د»» gets a licence expiring in 20 days (spec calls it «شركة الوساطة «د»»). Users: حاتم الرشيد (valuer-b provider admin), سامي الحربي (broker-a), نادر العمري / طارق السعدي (applicant admins). Password = demo password.
- Directories (alufuq 7 providers, sunbula 1), 12-month assignment history (all with past `AccessExpiresAt`) producing «ب» 96% / 4% at alufuq, invoices INV-B-2026-109/114/118/121, workflow v5 active + v6 draft (alufuq) / v5 (sunbula), plans (18,000 / 42,000 / custom), subscriptions incl. trial «شركة المدى للتمويل» (new lender org without users), BIL-2026-09-003/004/006, alufuq signing `disabled`, payment `simulated`.

## Tests
`RAHOON_TEST_ENSURE_CREATED=1 dotnet test` → **65 passed, 1 failed of 66**. The failure is pre-existing and environment-only: `SecurityTests.Database_rejects_audit_tampering` needs the audit trigger created by the migrations, which `EnsureCreated` does not run (it also failed on the untouched baseline, 38/39).

New tests:
- `SaleUnitTests`: 545,000 estimate; nets 2,724,400 / 2,744,000 / 2,763,600; 56,000 commission and 504,000 to the owner; minimum vs price and 2.5% precision with shortfall; 8-row matrix with no public column; projections never contain owner/case/lender/minimum/debt; `sale_withdrawn` never goes to referral.
- `SaleTests`: owner request → decision → approval (step-up required, approver ≠ preparer) → scoped consent unlocks preparation; **engine guard blocks** the transition without a documented owner consent (422 `guard_failed`, blocked audit, status unchanged); **withdrawal** returns to «حل مقترح» and is audited (never referral); full flow — compliance review, disclosure (no secrets in buyer/broker JSON, public = «لا»), broker scoped access (unassigned broker 404, lender routes 403, access revoked after approval), offers (net/recovery/surplus), owner accepts then withdrawal is closed, offer maker-checker + step-up, completion blocked until references then → «بانتظار التسوية المالية» with a zero-difference draft reconciliation.
- `EcosystemUnitTests`: k-anonymity (value and count suppressed, CSV «—», nothing leaks), quarters, workflow rule evaluation (> 3% waiver → compliance review; tier escalation), licence thresholds and directory status.
- `EcosystemTests`: directory licence states + per-institution performance (alufuq 96% vs sunbula 95.65% for the same provider; institutions cannot see applications); onboarding upload/submit → request_info (accept blocked) → resubmit → accept → institution adds from the platform directory; invoice maker-checker (dual-membership user cannot check own invoice; independent approver; payment reference; cross-tenant 404); workflow publish by a second user with step-up and **simulation without side effects**; report suppression + audited CSV + permission; conditional signing/payment return `integration_unavailable` without creating consent/payment rows, simulated hidden from the owner.

## Open issues / integration points
- **Migration** for `sale` / `ecosystem` (indexes: `ux_sales_open_per_case`, `ux_workflow_one_active`, `ux_workflow_one_open_draft`, invoice/assignment partial unique; check constraints incl. maker-checker).
- **Assignment references:** broker assignments use the shared counter key `assignment:{year}` starting at 5001 (`ASG-2026-5001`…). The provider-portal/lender `POST /api/cases/{ref}/assignments` work should use the same key to avoid collisions on the unique `Reference` index. Brokerage assignments carry `Scope = ["sale_file"]`, a generalised `PropertyLabel` and no shared documents; the provider portal must not expose case data for them.
- A live Brokerage assignment grants the broker org EF-level read of the lender org (existing access model); all broker routes therefore go assignment → sale explicitly. Tightening `RequestContextMiddleware` to scope provider data per assignment would be safer platform-wide.
- Mandate expiry is enforced lazily (access ends at `AccessExpiresAt`; actions check `MandateEnd`); no job flips the track to `Expired` yet. Licence expiry → «موقوف» is also computed at read time (no auto-suspend job).
- Sale photos: the listing stores an approved-photo count; files use the existing documents module (`property_photos`).
- Workflow versions are not yet applied to case SLAs/rules and cases are not pinned to a version.
- Provider rating disputes (14 days) and the provider invitation flow (V05 «دعوة مقدم خدمة» for a *new* provider) are not implemented; adding from the platform directory is.
- Integration configuration UI/API for `InstitutionIntegration` belongs to PA18 (B10).
