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

Conditions marked ◐ are enforced by code, not by the grant alone: approval tier re-check at decision time,
separation-of-duties exclusions, mandatory reasons, MFA step-up, reveal TTL, independence of reviewers.
