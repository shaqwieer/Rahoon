# Removal of the mortgage-default help model (2026-10-01)

The product changed from the **mortgage-default help platform** to the **Saudi exit/buy platform**
([`docs/product/product-definition.md`](../product/product-definition.md)). After a first step that hid the old model
behind flags, the owner decided to remove it permanently: all its data was disposable test data, including on staging,
and deleting it with its schemas was explicitly authorised. No export or backup of it was made.

- **Last state of the old model:** git tag `legacy-mortgage-final` (`266541e`). It is the only way back.
- **Flags removed:** `Features:LegacyMortgage` (API) and `RAHOON_LEGACY_MODES` (web) no longer exist, nor any
  conditional branch or disabled implementation behind them.

## What was deleted

### Backend (`server/src/Rahoon.Api`)

| Item | Detail |
|---|---|
| Modules | Administration, Agreements (incl. the breach monitor job), Analytics, Assessment, Cases, Closure, Communications, Complaints, Documents, Ecosystem, Imports, Owner, Providers, Referral, Requests, Sale, Solutions |
| Identity | individual sign-in by national ID, owner invitation sign-in, staff invitations, organization settings, e-mail domain and password-policy rules, context switch, step-up, teams, invitations, role-change requests; organization kinds Lender, ServiceProvider, JudicialAgent, Platform; account kind Owner; the legacy permissions and role templates |
| Audit | case-scoped audit endpoints and the case fields of audit events |
| Infrastructure | feature flags, disk document storage and the signature "scanner", integration registry and the unavailable signing/payment/identity/judicial adapters, the case FK convention |
| Seed | lenders, providers, agents, platform staff, cases, requests, agreements, referral, closure, analytics, bulk portfolio |
| Tests | every legacy suite and scenario builder (the old regression suite) |

### Web (`web/src`)

Routes `(lender)/**`, `/owner/**`, `/my/**`, `/provider/**`, `/agent/**`, `/platform/**`, `/print/**`, `/dev/**`,
`(auth)/invite`, `(auth)/start`, `/select-context`, `/team/requests`, `/team/verify`, `/team/objections`; components
`case/`, `owner/`, `individual/`, the lender/owner/platform/portal shells and their navigation, the command palette,
organization switcher and locale switch; unused UI kit pieces (case table, approval chain, audit timeline, KPI tiles, …);
the legacy API clients in `lib/api`; `lib/legacy.ts`; the English dictionary and the legacy dictionary sections; the
legacy E2E specs and helpers. These URLs now answer 404 because the pages no longer exist.

### Database (migration `RemoveLegacyMortgageModel`, then `FilesInDatabase`)

| Object | Action |
|---|---|
| Schemas `admin`, `agreements`, `analytics`, `assessment`, `cases`, `closure`, `comms`, `complaints`, `documents`, `ecosystem`, `providers`, `referral`, `requests`, `sale`, `solutions` | `DROP SCHEMA … CASCADE` (tables, keys, indexes, sequences) |
| `admin.idempotency_records`, `cases.reference_counters` | Moved (not dropped) to the new shared schema `app`; only `market:*` counters kept |
| `identity.teams`, `identity.invitations`, `identity.role_change_requests` | Dropped |
| Legacy columns | organizations: licence number, city, owner language, MFA flag, e-mail domains; users: English name, MFA method; memberships: team; individual profiles: national ID (encrypted, hash, masked), ID type, identity assurance, awareness opt-in; sessions: owner access, step-up; role permissions: grant; audit events: case id and reference; market files: disk path, name, type, size, checksum (now in `files.stored_files`) |
| Data | organizations of the removed kinds with their memberships, roles and grants; staff accounts without a Rahoon team membership; owner accounts; their sessions, codes, terms acceptances and replay records; one-time codes of removed purposes; permissions outside the current catalog; the audit history (cleared once: a hash chain cannot lose some events and still verify) |
| `market.obligation_parties` (fictional demo parties) | Dropped; requests that named one keep the name as typed text |
| Files on disk | Marketplace files copied into the database and verified, then deleted; the removed model's per-organization folders deleted |

The migration is targeted (no database reset) and not reversible (`Down` throws). A fresh database built from the full
migration history ends with only `app`, `audit`, `directory`, `files`, `identity`, `market` (checked by
`CurrentModelTests`); upgrading a database holding the old data is checked by `MigrationTests`.

## What was kept (shared)

Identity (sessions, CSRF, team sign-in with SMS code, roles and permissions, mobile sign-in for owners and buyers, the
existing individual accounts), the hash-chained audit log, idempotency, reference counters, the SMS sandbox gateway,
the `market` schema with all its data, the UI kit pieces the current pages use, i18n plumbing (Arabic only), formatting.
