# Backend progress — B10 (judicial & financial) and B11 (optimization)

Scope: API only (`server/`). The platform is not a court or auction operator. Judicial referral is a separate, approved,
manual decision, and nothing ends in foreclosure automatically. Specs: `docs/design-specs/B5-comms-referral-closure.md` (L25, L26),
`B10-judicial-financial.md`, `B11-optimization.md`, `08-flows-prototypes.md` (f8, f9).

**No EF migration was created** (per instructions). Tests run with `RAHOON_TEST_ENSURE_CREATED=1 dotnet test` from `server/`.
A consolidated migration must add the tables and columns listed under "Schema delta" below. Until it exists, `dotnet run -- seed`
against a migrated database will fail.

## Test results

`RAHOON_TEST_ENSURE_CREATED=1 dotnet test` gives **53 passed, 1 failed, 54 total**. There are 15 new tests and 39 existing ones.

- The single failure is the pre-existing `SecurityTests.Database_rejects_audit_tampering`.
- The append-only trigger lives in migration SQL, so it is absent under `EnsureCreated`.
- It fails identically on the base commit. It passes with migrations.

| File | Tests |
|---|---|
| `ReferralTests.cs` | 6 — readiness/notice/objection/live-offer/complaint guards, initiator ≠ approver, step-up; verbatim external status and explicit transition only; owner referral view before and after the notice; seeded 003511 canon (read-only); evidence pack numbered and hashed, export only after approval; J04 exceptions |
| `JudicialClosureTests.cs` | 1 end-to-end journey (details below) |
| `DecisionSupportTests.cs` | 5 — prediction override reason, no state change, not exposed to owner endpoints; draft suggestion until human approval; operational change needs a second person; integration cannot be enabled without an adapter; portfolio uses one base and the bottleneck insight is labelled |
| `WaterfallUnitTests` (same file) | 3 — canon waterfall; fee order and shortfall; heuristic deterministic and bounded |

The journey in `JudicialClosureTests.cs` covers these steps in order:
1. Agent sees only its own JudicialSale assignments, with no other case's files.
2. The agent's result never changes state.
3. The external result needs official confirmation.
4. Agent access ends after delivery and expiry.
5. An unexplained difference blocks submission and closure.
6. Reconciliation preparer ≠ reviewer ≠ approver, with step-up.
7. The waterfall equals the canon: 760,000 − 22,800 = 737,200 net, of which 684,200 goes to the lender and 53,000 to the owner.
8. Distribution maker-checker.
9. Documents gate closure.
10. The trace has no missing source.
11. Closure publishes three owner documents.
12. The owner portal becomes read-only, and the session lapses after the window.
13. The audit chain segment verifies.

Helpers:
- `Infrastructure/ReferralScenarios.cs`: ready and approved referral cases, and temporary role grants. The grants make SoD tests hit the rule itself, not a missing permission.
- `TestClient`: `UploadAsync` (multipart) and `DeleteAsync`.

## Screens, routes, permissions

All lender routes use `RequireOrg(Lender)` + `case.view` and go through `CaseAccess`. Every state-changing POST/PUT/DELETE is
`.Idempotent()`, except multipart uploads (same as the existing document upload). Refusals are audited with
`RecordBlockedAsync` before any chain append. Every transition is `workflow.TransitionAsync` followed by `audit.RecordAsync`.

### L25 / J01 — readiness, notice, decision (`/api/cases/{ref}/referral`)
| Route | Permission | Notes |
|---|---|---|
| `GET ""` | case.view | Overview: 8 readiness items with detail, evidence link and date, «N من 8»; blockers; pack; external reference; official status (verbatim); J03 history; judicial-channel integration state + `entryMode: manual`; agent assignment; sale result; waterfall **preview** (labelled "agent-reported / not a distribution"); actions (hidden without permission, disabled with reasons otherwise) |
| `GET /readiness` | case.view | Server-computed items: `amicable_exhausted, voluntary_sale_offered, no_open_complaints, core_docs_valid, lien_review, debt_reconciled, owner_notified, objection_period_elapsed` |
| `POST /readiness/{key}/evidence` | referral.initiate | Manual evidence for non-computable items only (`voluntary_sale_offered`, `amicable_exhausted`) |
| `POST /notice` | referral.initiate | Template `TPL-PREREF-01`. Sends a sandbox SMS, a portal message and an owner notification in plain language: this is not a final decision, you can object within N days, and your rights. Starts the objection period (O03 setting, default 15). Only from solution states |
| `POST /request` | referral.initiate | Reason required. Refused (422, audited `referral.request_blocked`) while any readiness item is unmet or an offer is live |
| `POST /decision` | referral.approve + **step-up** | approve/reject. The initiator is refused with `separation_of_duties` (403, audited). Approve runs `refer_judicial`: its guards `no_open_complaint` and `no_live_offer`, plus readiness re-evaluated at decision time via `extraGuardFailures`. Locks the pack |

### J02 — evidence pack
| Route | Permission | Notes |
|---|---|---|
| `POST /referral/pack` | referral.initiate | Builds numbered items 01–07 (case summary, contract, deed/mortgage, debt statement, offers log, notice proof, communications log), each with source, version and sha256. Manifest sha256 over all items. Field mapping (`match / mismatch / format_diff / auto_converted`, blocking flag). Refused after approval (`pack_locked`) |
| `GET /referral/pack` | case.view | Manifest and mappings, with the export-blocked reasons |
| `GET /referral/pack/export?format=csv\|json` | referral.initiate | Only after approval, with every item verified and no blocking mapping; otherwise 409 `export_blocked`, audited. Audited download; records `EvidencePackExportedAt`/hash. No Arabic PDF rendering |

### J03 — external reference and official status
| Route | Permission | Notes |
|---|---|---|
| `PUT /referral/external-reference` | referral.external_update | Authority, request number and source are stored as given; manual entry; only after approval |
| `GET/POST /referral/external-status` | referral.external_update (POST) | `statusText` is stored **byte-for-byte** (no trim or mapping) with `source` and `observedAt`, and a history is kept. Never changes the case status |
| `POST /referral/external-sale-started` | referral.external_update | Explicit `external_sale_started` transition with a reason; extra guard: external reference entered |
| `POST /referral/external-result` | referral.external_update | Explicit `external_result_recorded`; extra guard: the sale result is **officially confirmed** |
| `POST /referral/sale-result/confirm` · `/return` | referral.external_update | Legal confirms the agent-reported result against the official record. Corrected figures need a note. Confirmation delivers the assignment: agent is read-only for 7 days, then access lapses |

### J04 — exceptions
| Route | Permission |
|---|---|
| `POST /api/cases/{ref}/referral/exceptions` (types: status_mismatch, missing_document, objection_filed, package_rejected, no_response, channel_unavailable, other; owner; due date) | referral.initiate \| referral.external_update |
| `GET /api/referrals/exceptions?status=open\|resolved\|all` (cross-case queue through CaseAccess) | referral.initiate \| external_update \| approve |
| `POST /api/referrals/exceptions/{id}/resolve {action, note}` — `escalate` → Pending; other actions → Resolved; a note is required; the case never moves | referral.initiate \| external_update |

### J05–J07 — judicial agent portal (`/api/agent`, `RequireOrg(JudicialAgent)` + `agent.work`)
The lender hands off with `POST /api/cases/{ref}/referral/agent-assignments` (provider.assign; `GET /api/referrals/agents`). This creates a
`ProviderAssignment` with `Type = JudicialSale` and shares the verified deed, contract and valuation documents. It can be ended with
`POST …/agent-assignments/{id}/end`.

| Route | Notes |
|---|---|
| `GET /assignments` · `GET /assignments/{asgRef}` | Only this office's JudicialSale assignments, keyed by the masked authority reference. The detail includes the property, J06 readiness, plan, shared documents, updates and result, plus «ما تراه / ما لا تراه». There is no owner identity, negotiation or complaints |
| `GET /assignments/{asgRef}/documents/{versionId}/file` | Shared documents or this office's own uploads only; audited and watermarked |
| `POST /plan` · `POST /updates` (kinds `inspection_done, plan_submitted, minutes_issued, general`) · `POST /evidence` (multipart) | Writable only while New/InProgress/Returned and not delivered |
| `PUT /result` · `POST /result/submit` | Needs price, minutes date and ≥1 of its own evidence files. Tagged «أبلغ بها الوكيل». Adds an unconfirmed J03 row. **No state change, no distribution** |

Every agent query starts from the assignment row, and **expiry is checked per assignment**. The tenant filter alone is not enough,
because any other live assignment with the same lender keeps that lender in the agent's data scope. The test found this. Agent
audit events are written to the lender's chain.

### L26 / F01 — reconciliation (`/api/cases/{ref}/reconciliation`)
| Route | Permission |
|---|---|
| `GET` · `POST` (the judicial basis is taken from the **confirmed** result: expected = price − costs; other bases need an expected amount and its source) | reconciliation.prepare |
| `POST /lines` (receipt: bank ref required, unique per org · waiver: decision ref · cost/lender_share/surplus/info: display-only) · `DELETE /lines/{id}` · `PUT /explanation` | reconciliation.prepare |
| `POST /submit` — refused with `unbalanced` if there is no receipt or the difference is non-zero and unexplained | reconciliation.prepare |
| `POST /review` — reviewer ≠ preparer | reconciliation.prepare |
| `POST /approve` — **step-up**, approver ≠ preparer ≠ reviewer | reconciliation.approve |

The formula `difference = expected − received − waived` matches the existing DB check.

### F02 — distribution (`/api/cases/{ref}/distribution`)
- `POST` (reconciliation.prepare): requires a sale basis and a difference of exactly 0.00. The server computes the waterfall from the confirmed price and costs, the received amount and the current debt statement.
- `POST /submit`: requires a verified surplus destination when the surplus is above 0.
- `POST /check` (distribution.approve, ≠ preparer).
- `POST /approve` (distribution.approve + **step-up**, ≠ preparer and checker).
- `POST /execute` (bank transfer references per line).
- DB checks enforce the waterfall identity (`net = lender + fees + surplus`), `net = price − costs`, and that the three users are distinct.

### F03 — release and clearance documents (`/api/cases/{ref}/closure/documents`)
- `POST /init` creates the standard set:
  - `final_clearance`: finance, blocks closure, shared with owner.
  - `lien_release_letter`: legal, blocks, shared.
  - `lien_release_submission_proof`: legal, **non-blocking** (assumption), not shared.
  - `owner_final_summary`: generated, blocks, shared.
- `POST /{id}/file` (document.upload, multipart via `IDocumentStorage`) · `PUT /{id}` (external reference, share flag, mark ready — the proof needs its external reference) · `POST /owner-summary` (generated PDF).
- `ShareWithOwner` is the flag. `VisibleToOwner` flips **only at closure**, so nothing leaks to `/api/owner/closure` early.

### F04 — traceable closure
- `GET /closure`: overview with the three chains, documents, trace, consequences, blockers and actions.
- `GET /closure/trace`: every figure carries a source type (`official / bank / core_banking / internal`) and a reference.
- `POST /closure/request` (reconciliation.prepare): requires an approved reconciliation, an executed distribution for sale bases, the required documents ready, and no figure without a source. Stores a trace snapshot with its sha256.
- `POST /closure/decision` (case.close + **step-up**, `traceAcknowledged` required): approver ≠ requester ≠ reconciliation preparer ≠ reviewer. The trace must be unchanged. Then the `close` transition runs its guards (`reconciliation_approved`, `closure_documents_ready`) plus extra blockers. On success it publishes owner documents, closes open tasks, and sends a portal notification and sandbox SMS.
- The owner's closure documents appear in the existing `GET /api/owner/closure`.

**Owner access after closure (assumption, configurable `owner.closed_read_only_days`, default 90):**
- The owner portal is read-only after closure. A group filter returns `case_closed_read_only` 403 for any non-GET.
- Once `ClosedAt + window` has passed, `RequestContextMiddleware` refuses to resolve the owner session and revokes it.

### Owner referral status (B10 owner mobile) — `GET /api/owner/referral`
- Returns `{visible:false}` until the notice has been sent.
- After that it returns:
  - calm text;
  - the notice, with the objection period and whether objection is still open;
  - rights;
  - actions (message the case manager, object while the period is open, view the amounts).
- Once entered, it includes the **verbatim** official status text with «كما أبلغتنا الجهة» and the date.
- It never includes the decision trail, agent reports, internal or sync entries, exceptions or decision-support output.
- Referral is never shown as a journey stage.

### PA18 — integrations
- `GET /api/platform/integrations` (platform.ops | platform.integrations). It returns state, note, last change (who and when), purpose, live/sandbox adapter flags and the legend. Metrics are `null`, with a note that there is no live sync; no success or failure figures are fabricated.
- `PUT /api/platform/integrations/{key} {state, note}` (platform.integrations, audited):
  - `enabled` without a live adapter is refused with `adapter_not_configured` (422, audited).
  - `simulated` is allowed only where a sandbox adapter exists (sms, email).
- `GET /api/settings/integrations`: the institution's read-only view.

### B11
| ID | Routes | Permission | Notes |
|---|---|---|---|
| O01 | `GET /api/analytics/portfolio?months=12` | analytics.view | Live aggregates with k ≥ 10 small-cell merge into «أخرى» (regions and cohorts); provenance line; KPI numerators and denominators; comparison marked unavailable |
| O02 | `GET /api/analytics/bottlenecks` · `POST /api/analytics/insights/feedback` | analytics.view | Per stage: open count, median age and completed duration in business days, overdue, SLA, `aboveNormal`. Also overdue by team and the monthly valuation trend. The insight is labelled `heuristic_insight`, `BNK-rule-v1 (not a statistical model)`, with confidence, a causality caveat and limitations. «غير مفيدة» requires a reason |
| O03 | `GET /api/settings/operations` · `POST …/change-requests` · `POST …/change-requests/{id}/decision` | org.settings / **operations.approve** (new; org_admin and compliance) | Versioned `OperationalSetting` per key: routing rules, team capacity (with computed load), reminder cadence (≤ 2 a week, contact windows, none during a complaint), referral objection days (15–60), owner read-only days (30–365). Server validation; one pending change per key; proposer ≠ approver (API + DB check); audited |
| O04 | `/api/cases/{ref}/drafts` — `GET`, `POST {kind}`, `GET/{id}`, `PUT/{id}` (edit = new version), `POST /{id}/reject`, `POST /{id}/approve` (**step-up**), `POST /{id}/attach` | agreement.prepare \| referral.initiate | Deterministic `DRAFT-template-v1` (no LLM). Fills `TPL-AGR-02 v5` or `TPL-PREREF-01` from case facts with a source per value, and flags clause 7 against the cure-period policy. Approval is blocked while a blocking flag remains. Attaching before approval is refused (`draft_not_approved`). `humanReview` is agree (unedited), override (edited) or not_used (rejected) |
| O05 | `/api/cases/{ref}/predictions` — `GET`, `POST` (analysis.edit), `POST /{id}/opinion`, `POST /{id}/attach-to-approval`; `GET /api/analytics/models/{version}/governance` | analysis.edit \| analytics.view | See the notes below the table |

O05 notes:
- `ADH-v1.2 heuristic`: a transparent weighted rule over DSR, installment history, arrears, verified income and owner responsiveness.
- It outputs a 0–100 **score, not a probability**, as a range.
- Confidence is `heuristic_low` or `heuristic_medium`, reflecting input completeness only.
- It stores inputs, factor contributions, excluded protected attributes and limitations, and makes **no accuracy claims**.
- The analyst's opinion is agree, override (reason ≥ 10 characters plus a direction; DB check) or not_used. An opinion is required before attaching the estimate to a pending approval request.
- It is never exposed to owner endpoints and never read by the workflow.

## Entities (new configuration files per module; `RahoonDbContext.cs` untouched, accessed via `db.Set<T>()`)
- **Referral** (`ReferralConfigurations.cs`, schema `referral`): `ReferralChecklistEvidence`, `EvidencePack` + `EvidencePackItem`, `ReferralException`, `SaleResult`, `AgentUpdate`, `SalePlanMilestone`.
- **Closure** (`ClosureConfigurations.cs`, schema `closure`): `Distribution` + `DistributionLine`, `ClosureRequest`.
- **Analytics** (`AnalyticsConfigurations.cs`, schema `analytics`): `OperationalSetting`, `InsightFeedback`, `DocumentDraft` + `DocumentDraftVersion`, `Prediction`, `PredictionOpinion`.
- **Schema delta on existing tables** (these still need a migration):
  - `judicial_referrals`: notice template and sender, requested/decided by and at, external reference source/entered at/by.
  - `external_status_entries`: `source_kind`, `kind`, `officially_confirmed`.
  - `reconciliations`: expected source, note, submitted/reviewed at, review/approval/return reasons; new status `Reviewed`.
  - `reconciliation_lines`: `source_type`, `value_date`.
  - `closure_documents`: `share_with_owner`.

## Shared files touched (minimal)
- `Modules/ModuleRegistry.cs`: registers `ReferralService`, `ClosureService` and the new endpoint classes.
- `Modules/Identity/Permissions.cs` + `SystemRoles.cs`: `operations.approve`, granted to org_admin and compliance.
- `Seed/DevSeeder.cs`: calls `SeedReferralAsync`, `SeedClosureAsync` and `SeedAnalyticsAsync`.
- `Modules/Owner/OwnerEndpoints.cs`: one line adding the closed-case read-only filter to the owner group.
- `Infrastructure/Auth/RequestContextMiddleware.cs`: owner access lapses after the closed-case window.
- Entity files `ReferralEntities.cs` and `ClosureEntities.cs`: additive fields.
- `CaseWorkflow.cs` is **not** modified. Referral readiness is passed through `extraGuardFailures`.

## Seed
- **RH-2026-003511** (`DevSeeder.Referral.cs`). Dates are shifted before the demo "today" of 2026-09-23. The case stays in `judicial_referral`.
  - Offers: 3 (2 declined, 1 expired).
  - Written voluntary-sale refusal, recorded as evidence.
  - Notice 2026-07-20 with a 15-day objection period ending 2026-08-04.
  - Request by ماجد; approval by نورة on 2026-09-01.
  - Pack `PKG-3511-01`, exported.
  - Reference `EXT-JD-2026-0038841`, masked `•••8841`, with 4 verbatim official statuses; the latest is «قيد التنفيذ لدى الجهة المختصة».
  - `ASG-2026-0511` to «مكتب وكيل البيع «ج»» (ياسر الحمدان), with plan, 3 updates and 2 evidence files.
  - Agent-reported result: **760,000 price, 22,800 costs, still unconfirmed**. The overview preview shows 737,200 net, of which 684,200 goes to the lender and 53,000 to the owner.
  - Open exception `EXC-2026-0001`.
  - Templates `TPL-PREREF-01` and `TPL-CLOSED-01`.
- **RH-2026-003702** (`DevSeeder.Closure.cs`):
  - Settlement reconciliation **Submitted** by ريم, awaiting review: TRX-88201744 300,000.00 + TRX-88355102 155,210.75 = **455,210.75**, difference 0.00.
  - The approved waiver of 38,204.10 is an `info` line, because it is already reflected in the agreed amount.
  - Documents: clearance and lien-release letter ready; submission proof pending (non-blocking); owner summary pending.
- **Analytics** (`DevSeeder.Analytics.cs`):
  - Settings v1 for both lenders.
  - One pending team-capacity change.
  - An ADH-v1.2 estimate on RH-2026-004172 solution v2, with فهد's override.

## Decisions on listed conflicts
**B10**
1. J04 «4 مفتوحة»: the count is **all non-resolved** (open + pending + info), returned as `unresolved` with a label.
2. J03 order: sorted by `observedAt` descending.
3. `judicial_referral → external_judicial_sale` is a manual internal decision with a reason, gated on the external reference being entered.
4. F01/F02 vs F04 are sequential snapshots: reconciliation → distribution → documents → closure.
5. F03 ownership: finance prepares the clearance and summary; legal prepares the lien release and submission proof. Upload needs `document.upload`.
6. The official lien-release confirmation is **non-blocking** (assumption) and not shown to the owner.
7. Hijri values are computed with Umm al-Qura (mapping result `auto_converted`, non-blocking); fixture strings are not trusted.
8. **Fee order: costs (netted by the authority) → debt → other approved fees → owner surplus.** A shortfall (`debt − net`) stays with the lender and never reduces the surplus below 0.
9. Costs are treated as already deducted: expected net = price − costs, compared with the received amount.
10. F01 uses only **officially confirmed** figures. Legal confirms the agent's result with a source, and agent-reported values are never used for money.
11. Guards for undesigned disabled states are enforced on the server and returned as `actions[].reasons`.
12. PA18 edit is `PUT {state, note}`; there are no fabricated metrics.
13. The agent "add update" composer and evidence upload are implemented as API endpoints.
14. J01 vs L25: the **8 L25 items are kept**. The decision, pack and channel mandate appear as referral status, pack status and integration state.
15. The minutes date (`SaleMinutesDate`) and the confirmation date (`OfficialConfirmationDate`) are stored separately.

**B5 / flows**
- L25 active-offer rule: an expired or unanswered offer is not live (`ValidUntil < today`).
- The 15-day objection period and the 90-day owner read-only window are configurable (O03).
- f8 «موافقتان»: legal's request plus the approver's decision, by distinct users.
- f9 checker: an explicit review step.
- L26 core-system sync is manual.

**B11**
1. **One base for O01:** every non-draft case that was open in the window, or closed or cancelled during it.
   - Outcomes include «ما زالت مفتوحة», so the counts sum to N.
   - Amicable and referral rates use N as the denominator, and each KPI returns its numerator and denominator.
   - Recovery is money-weighted over the closed subset of the same base.
2. Region axis: zero-based.
3. «فوق المعتاد» means the median open age exceeds the stage SLA.
4. There is no hard-coded ≥ 13 trend threshold.
5. O03 approval: new `operations.approve`; proposer ≠ approver.
6. Drafts fill from the latest reschedule solution, with the owner-requested due day when present. Predictions are computed on demand for the latest solution version.
7. An override-direction field covers both directions.
8. No model or provider: O04 is deterministic template filling, and O05 is an explicit heuristic.
9. O04 reject requires a reason.
10. `AiOutputMeta` (model version, inputs, confidence, limitations, human review) is stored for O04 and O05; the O02 insight carries a model version and limitations.
11. The O03 experimental rule is status only, with no suggestion engine.

## Open issues
- **CI / plain `dotnet test` (migrations path) stays red until the consolidated migration is generated.** The fixture calls
  `MigrateAsync`, and the new tables and columns are in no migration. With `RAHOON_TEST_ENSURE_CREATED=1` all tests pass except the
  pre-existing tampering test.
- **Migration required** for all new tables and columns. `CaseWorkflow` guard names from the Handoff (`notice_sent`, `objection_period_elapsed`) are enforced through `extraGuardFailures`; they could be promoted to named guards when `CaseWorkflow.cs` can be edited.
- The approval inbox (L16) lists only solution approvals. Referral, reconciliation, distribution, closure and settings decisions notify approvers but do not appear there.
- The generated owner summary PDF is Latin-only (no Arabic shaping); the Arabic content comes from the API. The file scanner is still the placeholder.
- Pack export is an audited GET with side effects, like the existing document download.
- «طلب تحديث الآن» (channel sync) is not implemented, because the channel is unavailable.
- O01 period comparison and the O02 work/wait split need historical snapshots or wait-reason data.
- When provider (valuer/broker) endpoints are built, they need the same per-assignment expiry check as the agent portal.
