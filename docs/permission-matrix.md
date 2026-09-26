# Permission matrix

Source of truth: `server/src/Rahoon.Api/Modules/Identity/Permissions.cs` (catalog, 69 keys — the design's PA05
shows «كتالوج الصلاحيات 62» with 7 drawn; the 7 drawn keys are used verbatim and the rest complete the catalog)
and `SystemRoles.cs` (role templates copied into each organization at onboarding; institutions may adjust
within platform minima). Enforcement is server-side on every endpoint (`RequirePermission`, `RequireOrg`,
`RequireOwner`) plus data-level rules (tenant filters, `CaseAccess`, separation of duties). ✓ allowed ·
◐ allowed with conditions (enforced in code) · — not granted.

## Lender institution roles

| Permission | مدير حالات | موظف حالة | محلل ائتمان | معتمد | معتمد أول | لجنة المخاطر | القانونية | المالية | الامتثال | مدقق | مسؤول المنشأة |
|---|---|---|---|---|---|---|---|---|---|---|---|
| portfolio.view | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| case.view | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| case.view_all (all teams) | ✓ | — (team only) | — (team/own) | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| case.create | ✓ | — | — | — | — | — | — | — | — | — | ✓ |
| case.edit | ✓ | ✓ | — | — | — | — | — | — | — | — | — |
| case.import / case.assign / case.export (masked) | ✓ | — | — | — | — | — | — | — | — | — | ✓ |
| case.transition (manual stage moves) | ✓ | — | ✓ | — | — | — | ✓ | — | — | — | — |
| case.pause | ✓ | — | — | — | — | — | — | — | — | — | — |
| case.cancel (request) | ✓ | — | — | — | — | — | — | — | — | — | — |
| case.cancel_approve (≠ requester, MFA) | — | — | — | ✓ | ✓ | — | — | — | — | — | ✓ |
| pii.reveal (reason, 60 s, audited) | ◐ | — | ◐ | ◐ | ◐ | — | ◐ | — | — | — | — |
| document.request / upload | ✓ | ✓ | ✓ | — | — | — | ✓ | ✓ | — | — | — |
| document.review | ✓ | ✓ | ✓ | — | — | — | ✓ | — | — | — | — |
| document.download (watermark, audited) | ✓ | — | ✓ | — | — | — | ✓ | — | — | — | — |
| valuation.assign | ✓ | — | ✓ | — | — | — | — | — | — | — | — |
| valuation.review / analysis.edit | — | — | ✓ | — | — | — | — | — | — | — | — |
| solution.prepare | ✓ | — | ✓ | — | — | — | — | — | — | — | — |
| solution.review (≠ preparer) | ✓ | — | — | — | — | — | — | — | — | — | — |
| solution.approve (tier limits, ≠ preparer/reviewer, MFA) | — | — | — | ◐ ≤ 2M, ≤ 5% | ◐ ≤ 5M, ≤ 10% | ◐ unlimited | — | — | — | — | — |
| offer.send / negotiation.manage | ✓ | ✓ (negotiation) | — | — | — | — | — | — | — | — | — |
| agreement.prepare | ✓ | — | — | — | — | — | ✓ | — | — | — | — |
| agreement.activate | — | — | — | — | — | — | ✓ | — | — | — | — |
| payment.record | — | — | — | — | — | — | — | ✓ | — | — | — |
| payment.match (≠ recorder; DB constraint) | — | — | — | — | — | — | — | ✓ | — | — | — |
| breach.manage | ✓ | — | — | — | — | — | — | ✓ | — | — | — |
| reconciliation.prepare | — | — | — | — | — | — | — | ✓ | — | — | — |
| reconciliation.approve / distribution.approve / case.close (two approvals, MFA) | — | — | — | ✓ | ✓ | — | — | ✓ (close, distribution) | — | — | — |
| comms.send / task.manage | ✓ | ✓ | ✓ | — | — | — | ✓ | ✓ | — | — | — |
| complaint.view (tag only without handle) | ✓ | ✓ | ✓ | — | — | — | ✓ | ✓ | ✓ | ✓ | ✓ |
| complaint.handle (independent reviewer) | — | — | — | — | — | — | — | — | ✓ | — | — |
| audit.view | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| sale.manage | ✓ | — | — | — | — | — | — | — | — | — | — |
| sale.approve (MFA) | — | — | — | ✓ | ✓ | ✓ | — | — | — | — | — |
| referral.initiate (legal only) | — | — | — | — | — | — | ✓ | — | — | — | — |
| referral.approve (≠ initiator, MFA) | — | — | — | ✓ | ✓ | — | — | — | — | — | — |
| referral.external_update | — | — | — | — | — | — | ✓ | — | — | — | — |
| provider.assign | ✓ | — | — | — | — | — | ✓ | — | — | — | ✓ |
| invoice.approve | — | — | — | — | — | — | — | ✓ | — | — | ✓ |
| org.settings / user.manage / limits.manage / role.change_approve | — | — | — | — | — | — | — | — | — | — | ✓ (second admin approves changes) |
| template.edit | — | — | — | — | — | — | — | — | — | — | ✓ |
| template.publish (compliance) | — | — | — | — | — | — | — | — | ✓ | — | — |
| reports.view | ✓ | — | — | ✓ | ✓ | ✓ | — | ✓ | ✓ | ✓ | ✓ |
| analytics.view | — | — | ✓ | — | ✓ | ✓ | — | — | — | — | ✓ |

## Other portals

| Role | Organization kind | Permissions | Data scope |
|---|---|---|---|
| مقيّم / وسيط (provider_agent) | ServiceProvider | assignment.work | own org's assignments only; shared documents only; write until delivery, read-only +7 days, then none |
| مسؤول مقدم الخدمة (provider_admin) | ServiceProvider | assignment.work, invoice.submit, user.manage, org.settings | as above |
| وكيل البيع القضائي (judicial_agent) | JudicialAgent | agent.work | assigned referral cases only, after an approved channel |
| مسؤول عمليات | Platform | platform.ops, institutions, users, defaults, billing, integrations | aggregates; no case data |
| دعم تقني | Platform | platform.ops, platform.temp_access (request) | masked monitoring; real data only under an active dual-approved grant |
| الامتثال (منصة) | Platform | platform.ops, complaints, privacy, defaults, audit | metadata |
| مدقق (منصة) | Platform | platform.ops, audit, temp_access_approve | audit trail |
| المالك / المدين | — (owner session) | owner endpoints only | exactly one case; no internal notes, other parties or lender-only documents |
| الفرد (حساب ذاتي التسجيل) | — (individual session, ADR 0001) | `/api/individual/*` and own-account auth endpoints only (`RequireIndividual`); no organization permissions | own account; own requests (`/api/my/requests`, `IApplicantOwned` filter): drafts, consent, documents, messages, responses; never team-only documents, internal notes, coordination details or internal timers; no tenant data (empty `DataOrganizationIds`) |

## «فريق رهون» (operator tenant, ADR 0001 §4.2 — Phase 1A step 6)

| Permission | منسق حالات (team_coordinator) | مراجِع العروض (team_verifier) | قائد الفريق (team_lead) |
|---|---|---|---|
| request.view_assigned | ✓ (assigned to them, or unassigned) | ✓ (offers awaiting verification, step 7) | ✓ |
| request.view_all | — | — | ✓ |
| request.assign | — | — | ✓ |
| request.review (pick up, identity check, internal notes, not eligible) | ✓ | — | ✓ |
| request.request_info | ✓ | — | ✓ |
| request.coordinate (coordination log; ◐ only with the individual's active consent) | ◐ | — | ◐ |
| request.message | ✓ | — | ✓ |
| request.offer_record | ✓ | — | ✓ |
| request.offer_verify (◐ verifier ≠ recorder, MFA) | — | ◐ | ◐ |
| request.response_relay | ✓ | — | ✓ |
| request.close | ✓ | — | ✓ |
| request.objection_handle | ✓ | — | ✓ |

Data scope: operator membership (tenant filter) + assignment check (`RequestAccess`); drafts are never visible to the team; a
refused open of a request assigned to someone else is audited. Lender staff, providers, agents and platform admins get 403 on
`/api/team/*` (the platform temp-access grant is not extended to the operator tenant yet — Phase 1C).

> **Direction change (2026-09-25):** the individual becomes a first-class, self-registered user who owns requests
> before any case (B13 «الصلاحيات حسب المرحلة»): nothing is visible to any lender before submission; during review only
> the **chosen** lender sees the request and the shared documents (other lenders: nothing, not even its existence);
> after acceptance the rules in the owner row above apply. **Decided (Q7, Q14):** one independent account, several requests,
> each with its own access; the "exactly one case" scope is superseded. **MVP (Q1):** lenders have no platform access; a new
> platform role «فريق رهون» (coordinator, verifier) sees only the requests assigned to it (Phase 1A step 6).
> See `docs/product/product-direction.md` §4 and §7 (X4).

Conditions marked ◐ are enforced by code, not by the grant alone: approval tier re-check at decision time,
separation-of-duties exclusions, mandatory reasons, MFA step-up, reveal TTL, independence of reviewers.
