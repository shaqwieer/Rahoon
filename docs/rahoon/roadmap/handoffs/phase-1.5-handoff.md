# Phase 1.5 handoff — Rahoon administration, team members, roles and permissions

**Status:** Completed · **Date:** 2026-10-01 · **Commits on `master`:** `4c19cec` (API, migration, tests) and the
following commit "feat(team): Phase 1.5 console …" (web console, docs).
**Push / deployment:** not pushed, not deployed. Staging (`rahoon.talentfold.net`) still runs `0c942dc`.

## Verified prerequisites

Checked in code and tests before building: team sign-in (e-mail + password + SMS), per-request membership resolution
(`RequestContextMiddleware`), team review of sale/buyer requests, opportunities, interests, private documents served by
`/api/files/{id}`, the directory admin, hash-chained audit. The full API suite passed before the changes (88 tests).
Gap found and fixed: any operator with `market.view` could download every private document and reveal any owner's mobile.
Both are now scoped (see below).

## Implemented behaviour

- **Catalog** by area with per-grant **scope** (`assigned` / `all`); reserved keys for 3A/3B/4 listed, not grantable.
- **Eight protected system roles** (owner, operations manager, case manager, document reviewer, publisher, finance,
  support, auditor). Exact matrix in `../phase-1.5-admin-access.md`. **Custom roles** are created, edited (version
  check) and archived (only when unused).
- **Scope enforced** on lists and counts (in SQL), details, every mutation, contact reveals and file downloads.
  Out of scope → 404; visible but action not granted → 403.
- **Members**: list/search/status tabs, detail with effective access (which role grants what, at which scope), open work,
  history; change roles, suspend, reactivate, remove (history kept), reassign open work (to the unassigned queue or a
  member; each record logs former → new assignee). Open work must be placed before a member loses access.
- **Escalation rules**: no self-change; act only on members you fully cover; grant only what you hold (scope included);
  last active platform owner protected under a per-team advisory lock with the actor re-read inside it.
- **Invitations**: bound to e-mail + mobile + roles chosen by the inviter; token hash only (never in idempotency
  records); 72 h; renew (old link dies), revoke, single use; acceptance re-checks the roles and the inviter's authority;
  a returning staff account proves its password. **No e-mail is sent**; the console shows the link once and says so.
- **Live revocation**: grants re-read per request; suspend/remove revoke the member's sessions; private files `no-store`.
- **Bootstrap**: `bootstrap-owner` CLI (refused once an active owner exists).
- **Landing**: the first area the member's grants open (`TeamHome`); navigation follows grants and scopes.
- **Audit**: `team.*` and `role.*` events with grants before/after; `/team/audit` (`audit.read`).

## Code entry points

API (`server/src/Rahoon.Api`):
- `Modules/Identity/Permissions.cs`, `SystemRoles.cs`, `SystemRoleSync.cs`, `EffectiveAccess.cs`, `TeamHome.cs`
- `Modules/Identity/TeamAdminEndpoints.cs` (`/api/team/admin/*`), `InvitationEndpoints.cs` (`/api/auth/invitations/*`),
  `TeamBootstrap.cs`
- `Modules/Market/TeamScope.cs` (scope filters and checks), `TeamWorkload.cs` (open work, reassignment)
- Changed: `TeamMarketEndpoints.cs`, `TeamOpportunityEndpoints.cs`, `Files/FileEndpoints.cs`,
  `OrgDirectory/DirectoryEndpoints.cs`, `Infrastructure/Tenancy/RequestContext.cs` (`Grants`, `CanOn`, `HasAll`),
  `Infrastructure/Auth/RequestContextMiddleware.cs`, `AuthEndpoints.cs` (`/me` scopes and home), `Seed/DevSeeder.cs`,
  `Program.cs` (CLI)

Web (`web/src`): `components/team/TeamShell.tsx`, `components/team/admin/*`, `lib/team/admin.ts`,
`app/team/{members,roles,audit}/…`, `app/(auth)/join/…`; scope-aware action flags in
`components/market/team/Team{Sale,Buyer,Interest}View.tsx`; read-only directory pages without `directory.manage`.

## Migrations and application state

`20261001183735_TeamAccessAdministration`: role/grant/membership columns and `identity.staff_invitations`. The role
renames and grants are applied by `SystemRoleSync` on every `migrate`.
- Test databases: applied (Testcontainers, every run).
- Local development database: applied with `dotnet run -- seed` (non-destructive; added the five new demo members).
- Staging and production: **not applied** (not deployed). On deploy, migrate-on-startup applies it and renames
  lama's `team_lead` role to `platform_owner`; existing coordinators become case managers (assigned scope), so the
  owner or an operations manager must **assign open work** to them. New demo members appear on staging only after
  `seed` there.

## Permissions

New keys: `dashboard.view`, `team.read`, `team.manage`, `roles.read`, `roles.manage`, `market.edit`, `market.verify`,
`market.decide`, `documents.read`, `documents.review`, `directory.read`, `audit.read`; reserved: `directory.import`,
`settings.manage`, `reports.export`, `offers.manage`, `reservations.manage`, `closing.manage`, `payments.evidence`.
Changed meaning: `market.review` no longer covers decisions, figure verification, corrections or file review;
`market.view` no longer opens private documents. Contact messages need `market.follow` with scope `all`.

## Adopted decisions

See `../decisions.md` ("Adopted in Phase 1.5"). Notable: operations manager doesn't publish; case manager decides on
assigned requests; support follows all work for the visitor inbox; invitation links are handed over manually.

## Tests and checks (actual results)

- `dotnet test server/tests/Rahoon.Api.Tests`: **101 passed, 0 failed**. That is 88 existing plus 13 new in `TeamAccessTests.cs`. Among them: concurrent mutual suspension of the only two owners
  (exactly one succeeds), last owner vs a member holding every permission, live revocation on mutation and download,
  invitation lifecycle, scope combination.
  Existing tests that acted as a case manager on unassigned work now assign it first (or use the operations manager),
  the lockout test uses an account no other test signs in with, and the schema/role checks know the new table and
  role key. Also fixed a pre-existing flaky check: `SmsConfirmationTests` counted SMS by masked number (last two digits only)
  and now counts from the test's start time.
- Web: `tsc --noEmit` ✓, `eslint src` ✓, `next build` ✓.
- Playwright `market-journey.spec.ts` (local stack, owner account): **15 passed, 1 failed**. The failure is in the
  public sale wizard's developer picker. On the local database (with the 2026-10-01 REGA dataset) the test's first
  option click lands on «غير موجودة». This phase changed neither the picker nor the lookup endpoint. Recorded as a
  known issue below.
- Browser: console pages checked in Chrome at desktop width, and with Playwright at 390 px. No horizontal overflow on
  members, member, invite, roles, role, new role, audit, overview or join. The real UI flow was also run: invite →
  link shown with «لم يُرسل الرابط إلى أحد» → `/join` at 390 px → password → joined → reused link refused → document
  reviewer lands on `/team` with request queues only and is refused `/team/members`.

## How to reproduce the acceptance scenario (local)

1. `bash scripts/dev-api.sh` (or `--reset`), `cd web && npm run dev`. Demo team password: see README «Demo logins».
2. As **لمى** (owner): `/team/sale` → open a submitted request → assign to **نايف**. Assign another to **تركي**.
3. As **نايف**: only his request is listed; opening تركي's reference returns «غير موجود»; his document opens, تركي's doesn't.
4. As **هدى** (document reviewer), after the owner assigns her a request: verify a figure and accept a document. No
   approve/reject or publish actions appear, and the API refuses them.
5. As **عبير** (publisher): `/team/members` is refused; publishing works.
6. As لمى: `/team/members/invite` → copy the link → open it in a private window → set a password → sign in as the new
   member. Change their roles on `/team/members/<id>`; their open tab loses the old actions on the next request.
   Suspend them: their session ends.
7. Try to suspend yourself or remove the last owner: refused with the reason.

## Remaining limitations and known issues

- No e-mail provider: invitation links are handed over manually (open decision). SMS remains sandboxed (D1).
- Staff passwords can't be reset or changed from the console yet. The workaround: remove the member, then re-invite;
  they then prove their old password, so a forgotten password needs a database-side reset. Worth adding in Phase 4/5.
- "Assigned" scope for a document reviewer or finance officer means the request is assigned to them. There is no
  multi-assignee model (one assignee per record), so a case manager and a reviewer can't both hold the same file at
  once. Phase 4 (assignments/workload) should decide whether records get a reviewer slot.
- Reserved transaction/settings/report permissions do nothing until their phases.
- E2E `market-journey.spec.ts` developer-picker step fails on the local data set (pre-existing, not 1.5). Not
  re-baselined against a `--reset` database in this session.
- No Playwright spec for the console yet. Coverage is the API suite plus the manual/scripted browser checks above.

## Next phase

**Phase 2 — discovery, matching and calculators.** Starting instruction:

> Read the repository instructions, `docs/rahoon/roadmap/README.md`, `decisions.md` and `handoffs/phase-1.5-handoff.md`.
> Read the Phase 2 brief `Rahoon_Next_Phases/Rahoon_Phase_2_Discovery_Matching.md` and write its plan as
> `docs/rahoon/roadmap/phase-2-discovery-matching.md`. Verify prerequisites from actual code, implement ONLY Phase 2,
> complete its acceptance checks, write its handoff and update roadmap status. Do not start the next phase.
