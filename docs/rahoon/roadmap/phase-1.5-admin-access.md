# Phase 1.5 — Rahoon administration, team members, roles and permissions

**Status:** ✅ Completed on 2026-10-01 and deployed to staging (see [handoff](handoffs/phase-1.5-handoff.md)).
**Brief:** `Rahoon_Next_Phases/Rahoon_Phase_1_5_Admin_Access.md` (owner's phase pack). This file is how that brief maps onto the code.

## Business context (stable)

Saudi exit/buy platform: owners exit developer contracts or sell bank/finance-tied property; buyers find opportunities;
«فريق رهون» reviews evidence and figures, prepares and publishes opportunities and follows interests. The mortgage-help
model stays removed. Arabic-first RTL, SAR, Asia/Riyadh. Internal approval ≠ owner acceptance ≠ external approval.
Phase 1.5 adds no business transaction. It builds the access foundation so several employees can work before offers
(3A) and completion (3B) exist.

## Scope and exclusions

In: staff membership separate from customer accounts; identity-bound invitations; suspension, reactivation, removal with
work reassignment; permission catalog by area; protected system roles and custom roles; per-grant resource scope;
effective-access view; grant authority (no self-escalation); last-owner protection under concurrency; scope enforced on
lists, counts, details, mutations, contact reveals and downloads; administration log; controlled bootstrap; console UI.

Out (later phases): offers/reservations/closing/payment actions (permission keys reserved only), settings and policy
versions, reports/exports, staged directory import from the console (Phase 4), e-mail delivery (no provider), SSO,
multi-tenant SaaS.

## Model

| Concept | Where | Notes |
|---|---|---|
| Staff user | `identity.users` (`AccountKind = Staff`) | E-mail + password + SMS code. Customers are separate `Individual` users (mobile sign-in); no endpoint turns one into the other. |
| Membership | `identity.memberships` | `Active` / `Suspended` / `Revoked` (removed), with `status_reason`, `status_changed_at/by`, `xmin`. Never deleted. One row per (user, team); a returning member reuses it. |
| Role | `identity.roles` | `is_system`, `description_ar`, `archived_at`, `xmin`. System roles are fixed; custom roles get key `custom_…`. |
| Grant | `identity.role_permissions` | (`role_id`, `permission_key`, `scope`). `scope` ∈ `Assigned` / `All`; non-scopable keys are always `All`. |
| Invitation | `identity.staff_invitations` | E-mail, name, mobile, title and role ids fixed by the inviter; only the token's SHA-256; `Pending`/`Accepted`/`Revoked` + expiry; unique pending invitation per (team, e-mail). |

Migration: `20261001183735_TeamAccessAdministration`. `SystemRoleSync` (runs on every migrate) renames the pre-1.5 keys in
place (`team_lead` → `platform_owner`, `team_coordinator` → `case_manager`, `team_verifier` → `publisher`), creates the
missing system roles, and sets each system role's grants exactly.

## Permission catalog (`Modules/Identity/Permissions.cs`)

Existing keys keep their names; their meaning is narrowed where the brief separates duties (noted).

| Area | Key | Scopable | Meaning |
|---|---|---|---|
| Dashboard | `dashboard.view` | | Operations overview and «مهامي» |
| Team | `team.read` | | Members, their roles and effective access |
| | `team.manage` | | Invite, change roles, suspend, reactivate, remove, reassign work |
| Roles | `roles.read` | | Roles and the permission matrix |
| | `roles.manage` | | Create, edit, archive custom roles |
| Requests | `market.view` | ✓ | Read sale/buyer requests, opportunities, interests (lists, details, counts) |
| | `market.edit` *(new)* | ✓ | Correct a field with a reason; internal notes (was part of `market.review`) |
| | `market.review` | ✓ | Start review, request information, external-approval log, buyer capacity review |
| | `market.verify` *(new)* | ✓ | Verify a figure from its source; record buyer financing approval (was `market.review`) |
| | `market.decide` *(new)* | ✓ | Approve / reject a sale or buyer request (was `market.review`) |
| | `market.assign` | | Assign work to another member |
| Opportunities | `market.prepare` | ✓ | Create, edit, send to owner |
| | `market.publish` | ✓ | Publish, pause, resume, withdraw |
| Follow-up | `market.follow` | ✓ | Interests, reveal a customer's mobile, contact messages (inbox needs scope `all`) |
| Documents | `documents.read` *(new)* | ✓ | Open/download private documents (was any `market.view`) |
| | `documents.review` *(new)* | ✓ | Accept/reject documents and photos (was `market.review`) |
| Directory | `directory.read` *(new)* | | Read the directory administration |
| | `directory.manage` | | Add/edit/activate/deactivate |
| | `directory.import` | reserved (4) | Console import (today: CLI only) |
| Audit | `audit.read` | | Administration and sign-in log |
| Settings | `settings.manage` | reserved (4) | |
| Reports | `reports.export` | reserved (4) | |
| Transactions | `offers.manage`, `reservations.manage` | reserved (3A) | |
| | `closing.manage`, `payments.evidence` | reserved (3B) | |

Reserved keys are shown in the matrix and the role editor as «لمرحلة لاحقة» and are refused by the API if granted.

## Default role matrix (`Modules/Identity/SystemRoles.cs`)

A = all of the team's work, S = assigned work only, ✓ = non-scopable grant, — = not granted.

| Permission | Owner | Ops manager | Case manager | Doc reviewer | Publisher | Finance | Support | Auditor |
|---|---|---|---|---|---|---|---|---|
| dashboard.view | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| team.read | ✓ | ✓ | — | — | — | — | — | ✓ |
| team.manage | ✓ | — | — | — | — | — | — | — |
| roles.read | ✓ | ✓ | — | — | — | — | — | ✓ |
| roles.manage | ✓ | — | — | — | — | — | — | — |
| market.view | A | A | S | S | A | S | S | A |
| market.edit | A | A | S | — | — | — | — | — |
| market.review | A | A | S | S | — | — | — | — |
| market.verify | A | A | — | S | — | S | — | — |
| market.decide | A | A | S | — | — | — | — | — |
| market.assign | ✓ | ✓ | — | — | — | — | — | — |
| market.prepare | A | A | S | — | — | — | — | — |
| market.publish | A | — | — | — | A | — | — | — |
| market.follow | A | A | S | — | — | — | A | — |
| documents.read | A | A | S | S | — | S | — | — |
| documents.review | A | A | S | S | — | — | — | — |
| directory.read | ✓ | ✓ | ✓ | — | ✓ | — | — | ✓ |
| directory.manage | ✓ | — | — | — | — | — | — | — |
| audit.read | ✓ | ✓ | — | — | — | — | — | ✓ |

Adopted defaults (reversible in a custom role): the operations manager doesn't publish (publication stays a separate
duty) and doesn't manage the team; the case manager decides on assigned requests (keeps the Phase 1 flow) but doesn't
verify figures; support follows all work so it can answer the visitor inbox, but sees only assigned requests.

## Resource scope

"Assigned" work (`Modules/Market/TeamScope.cs`): a sale/buyer request assigned to the member; an opportunity assigned to
them or prepared from a request assigned to them; an interest assigned to them or on an opportunity assigned to them; a
private document or photo of a request assigned to them. Each check uses that permission's own scope. Lists and counts
filter in SQL before paging. Loaders answer **404** outside `market.view` scope and **403** when the record is visible but
the action's permission doesn't reach it. Downloads answer 404 either way. Unassigned work is assigned by members with
`market.assign` (owner, operations manager); assigned-scope members can't claim it.

## Escalation and last-owner rules (`TeamAdminEndpoints`)

- Nobody changes their own roles or status (`self_change`, 403).
- To act on a member you must hold every grant they hold at the same or wider scope (`insufficient_authority`).
- To grant (invite, change roles, create or edit a role) you must hold each granted permission at the same or wider
  scope (`grant_exceeds_authority`). Editing a role also requires covering its current grants.
- System roles can't be edited or archived (`system_role`). An in-use role can't be archived (`role_in_use`, lists holders).
- The last active platform owner can't be suspended, removed or lose the role (`last_owner`, 409), even by a member who
  holds every permission through a custom role. Every membership/role/invitation change runs in one transaction under
  `pg_advisory_xact_lock(hashtext('team-admin:'+team))`. Inside the lock the actor's own membership is re-read, so of two
  owners suspending each other at once exactly one succeeds.
- Suspending or removing a member with open work requires an explicit choice (`has_assigned_work`, 409): the unassigned
  queue, or another active member who can view requests. Each moved record gets an internal `reassigned` event naming
  both members.

## Session effect

`RequestContextMiddleware` re-reads membership, roles and grants on every request. A role change applies to the next
call of an existing session (mutations and downloads alike). Suspension and removal revoke the member's sessions and the
middleware also kills any session whose membership isn't active. Private file responses are `no-store`.

## API authorization matrix

| Endpoint | Requirement |
|---|---|
| `GET /api/team/market/overview` | operator + `market.view` + `dashboard.view` (counts filtered by scope; contact count only with `market.follow` all) |
| `GET /api/team/market/{sale,buyer}-requests` | `market.view`, rows filtered by scope |
| `GET …/sale-requests/{ref}`, `…/buyer-requests/{ref}` | `market.view` on the record (404 otherwise) |
| `GET …/sale-requests/{ref}/contact` | `market.follow` on the record (audited reveal) |
| `POST …/assign` | `market.review` on the record; assigning to someone else also `market.assign`; assignee must hold `market.view` |
| `POST …/start-review`, `…/request-completion`, `…/external-approvals`, `…/capacity-review` | `market.review` on the record |
| `POST …/approve`, `…/reject` | `market.decide` on the record |
| `POST …/verify-figure`, `…/finance-approval` | `market.verify` on the record |
| `POST …/correct`, `…/note` (sale, buyer) | `market.edit` on the record |
| `POST …/documents/{id}/review`, `…/photos/{id}/review` | `documents.review` on the record |
| `POST …/sale-requests/{ref}/opportunity`, `PUT …/opportunities/{ref}/content|terms`, `POST …/send-to-owner`, `…/opportunities/{ref}/assign` | `market.prepare` on the opportunity (assignees: opportunity + its request) |
| `POST …/checklist` | `market.review` or `market.publish` on the opportunity |
| `POST …/publish|pause|resume|withdraw` | `market.publish` on the opportunity |
| `GET …/interests`, `…/interests/{ref}` | `market.view` (scope via interest or its opportunity) |
| `GET …/interests/{ref}/contact`, `POST …/assign|follow-up|close|note` | `market.follow` on the interest |
| `GET|POST …/contact-messages…` | `market.follow` with scope `all` |
| `GET /api/files/{id}` | owner; or document → `documents.read` + `market.view` on the request; photo → `market.view` on the request; or public photo of a published opportunity |
| `GET /api/team/directory…` | `directory.read` or `directory.manage`; changes `directory.manage` |
| `GET /api/team/admin/catalog` | `team.read` or `roles.read` |
| `GET /api/team/admin/members[/{id}]`, `/invitations` | `team.read` |
| `POST /api/team/admin/members/{id}/roles|suspend|reactivate|remove|reassign` | `team.manage` + rules above |
| `POST /api/team/admin/invitations`, `/{id}/renew` | `team.manage` + grant authority (not idempotency-replayed: the link is never stored) |
| `POST /api/team/admin/invitations/{id}/revoke` | `team.manage` |
| `GET /api/team/admin/roles[/{id}]` | `roles.read` or `team.manage` |
| `POST /api/team/admin/roles`, `PUT …/{id}`, `POST …/{id}/archive` | `roles.manage` + grant authority |
| `GET /api/team/admin/audit` | `audit.read` |
| `POST /api/auth/invitations/lookup|accept` | no session; token; CSRF-exempt, Origin checked, rate-limited |

## State transitions

| Subject | From → To | Actor / permission | Preconditions | Side effects |
|---|---|---|---|---|
| Invitation | — → Pending | `team.manage` | roles within authority; e-mail not an active/suspended member; no live pending invitation | token issued once; audit `team.invited` |
| Invitation | Pending → Pending (renew) | `team.manage` | roles still exist and within authority | new token, new expiry, old link dead |
| Invitation | Pending → Revoked | `team.manage` | — | audit |
| Invitation | Pending → Accepted | invitee (token) | not expired; roles exist; inviter still active with `team.manage` covering them (bootstrap: no active owner yet); new user sets a password, existing staff proves theirs | membership created or reused as Active with the invitation's roles; audit |
| Membership | Active → Suspended | `team.manage` | not self; actor covers target; not last owner; open work moved | sessions revoked; audit |
| Membership | Suspended → Active | `team.manage` | not self; actor covers target | audit |
| Membership | Active/Suspended → Revoked | `team.manage` | as suspend | sessions revoked; roles kept for history; audit |
| Membership roles | change | `team.manage` | not self; covers target and new roles; owner role kept by another active owner | effective on next request; audit with grants before/after |
| Custom role | create / edit / archive | `roles.manage` | grants within authority (old and new); not system; archive only when unused and not in a pending invitation; edit version check | audit with grants before/after |

## Bootstrap

`dotnet Rahoon.Api.dll bootstrap-owner --email … --name "…" --phone 05…` (after `migrate`): creates «فريق رهون» and its
roles if missing and prints a one-time `/join#…` link for a platform-owner invitation. Refused while an active owner
exists. No public endpoint, no default password. Demo members exist only in Development/Staging/Testing seeds.

## Console (web)

Permission-aware navigation (`components/team/TeamShell.tsx`). Pages: `/team/members` (tabs, search, invitations),
`/team/members/invite`, `/team/members/[id]` (roles, effective access with granting roles and scope, open work,
suspend/reactivate/remove/reassign dialogs, history), `/team/roles` (cards + permission matrix), `/team/roles/new`,
`/team/roles/[id]` (editor; grants beyond the editor's authority disabled; read-only with the reason for system roles),
`/team/audit`, `/join` (invitation acceptance). Existing request screens show actions only when the record's scope allows
them (document links, contact reveal, notes, decisions, verification, follow-up). Landing page: the first area the
member's grants open (`TeamHome`).

## Acceptance (brief) → evidence

| # | Criterion | Test (`server/tests/Rahoon.Api.Tests/TeamAccessTests.cs` unless noted) |
|---|---|---|
| 1 | Case manager reviews assigned work; can't read another manager's private file by id | `Case_manager_reviews_assigned_work_and_cannot_reach_another_managers_file`; `FileStorageTests.Private_files_are_served_only_to_their_owner_and_the_team` |
| 2 | Reviewer verifies, can't publish or decide; publisher can't manage roles | `Reviewer_verifies_evidence_but_cannot_decide_or_publish_and_publisher_cannot_manage_roles` |
| 3 | Role change and suspension hit an existing session on mutation and download | `Role_change_and_suspension_apply_to_an_existing_session_on_mutations_and_downloads` |
| 4 | Public user can't gain staff access by forged fields | `A_public_user_cannot_gain_staff_access_by_forged_fields`; `SecurityTests.Owners_and_buyers_never_reach_team_endpoints` |
| 5 | Expired/revoked/used invitations fail; no duplicate memberships | `Invitations_are_single_use_expire_can_be_revoked_and_never_duplicate`; `Invitation_roles_must_be_within_the_inviters_authority` |
| 6 | Last owner under concurrency; combined roles don't widen scope | `The_last_active_owner_cannot_be_suspended_removed_or_lose_the_role`; `Combining_roles_never_widens_another_permissions_scope` |
| 7 | Repeatable role/seed sync; Phase 1 flows still work | `Role_sync_is_repeatable_and_renames_the_old_role_keys_in_place`; full suite (`MarketTests`, …) |
| — | Removal with open work; roles editor rules; landing; support inbox | `Removing_a_member_with_open_work_requires_a_reassignment_and_keeps_the_history`, `System_roles_are_fixed_custom_roles_are_validated_and_in_use_roles_cannot_be_archived`, `Each_member_lands_on_an_area_they_can_open_and_navigation_matches_grants`, `Support_sees_contact_messages_but_not_unassigned_requests_or_documents` |

Delivery scenario actors: two case managers (نايف، تركي), a document reviewer (هدى), a publisher (عبير), an operations
manager (فهد), an auditor (سارة), support (ريم), plus fresh members created by real invitations in the tests.
