# رهون — Architecture and conventions

## Overview

```
web/      Next.js 16 (App Router, TypeScript, Tailwind v4): public pages, the owner/buyer account, the team workspace
server/   ASP.NET Core 10 Web API (modular monolith) + xUnit integration tests
          PostgreSQL 16 via EF Core 10 (Npgsql), one schema per module
deploy/   Staging compose files, Dockerfiles, nginx site
docs/     Product definition, phase plan, this file
```

The browser only talks to the Next.js origin. `next.config.ts` rewrites `/api/*` to the API (`API_ORIGIN`, default
`http://localhost:5080`), so session cookies are first-party. Server Components call the API directly and forward the
incoming `Cookie` header.

The product is the Saudi exit/buy platform (`docs/product/product-definition.md`). The mortgage-default help model that
preceded it was **removed** on 2026-10-01 (code, routes, schemas, data and files; see
`docs/redefinition/legacy-inventory.md`). Its last state is the git tag `legacy-mortgage-final`.

## Backend modules (`server/src/Rahoon.Api/Modules`)

| Module | Schema | Responsibility |
|---|---|---|
| Identity | `identity` | the Rahoon team organization, users, memberships, roles and permissions, sessions, one-time codes, owners' and buyers' mobile accounts |
| Market | `market` | sale requests, obligations, private documents, listing photos, verifications, external approvals, buyer requests, opportunities and their versioned terms, interests, saved items, events and notifications, contact messages |
| OrgDirectory | `directory` | the directory of real Saudi developers, banks and finance companies; importer; administration |
| Files | `files` | stable file endpoint; storage itself is `Infrastructure/Storage` |
| Audit | `audit` | append-only, hash-chained audit events (a DB trigger rejects UPDATE/DELETE) |
| (shared) | `app` | idempotency records, reference counters, the SMS log |

Each module owns its entities, EF configuration and endpoints (a static `Map(IEndpointRouteBuilder)` registered in
`Modules/ModuleRegistry.cs`).

### Market module (`Modules/Market`)

| File | Role |
|---|---|
| `FieldCatalog.cs` | The single rule set for sale-request fields: scope (property / obligation), property types, obligation kinds, conditions, «لا أعرف», submit and publish tiers, documents per kind, Saudi cities. Served at `GET /api/market/catalog`; applied on every save |
| `MarketCalculator.cs` | One calculation engine (decimal) for developer, financier and mixed tracks, buyer fit and commission policy (`Market:Commission`, never 0 while unapproved) |
| `MarketEntities.cs` | The market entities. Files are referenced by stable file id (`PrivateDocument.FileId`, `ListingPhoto.FileId`); obligation parties by directory id plus the name recorded when chosen |
| `SaleRequestEndpoints.cs` | Owner: create (idempotent per device draft id), autosave, submit, resubmit, withdraw, files, opportunity confirmation |
| `BuyerEndpoints.cs` | Buyer: one live buyer request (optional preferred financier from the directory), suggestions with fit, interests, saved, account summary |
| `TeamMarketEndpoints.cs`, `TeamOpportunityEndpoints.cs` | «فريق رهون»: review, completion, figure verification, corrections, file review, external approvals, opportunity preparation, owner confirmation, publish/pause/withdraw, interests, contact messages |
| `MarketPublicEndpoints.cs` | Visitors: catalog, contact, calculators (fees with payer, as-of date, dated market reference), opportunity details with the next payments, public photos |
| `Discovery/` (Phase 2) | The one discovery spine: `SearchCriteria` (the only parser/validator of search state, canonical query + hash), `DiscoveryQuery` (the only filter builder: published + public points; strict budget in SQL), `Affordability` (its C# twin: fits / does_not_fit / incomplete, reasons, next payments), `Matching` (eligibility vs ranking preferences, explanations, `match=me` from the session), `DiscoveryEndpoints` (list, map, compare), `SavedSearchEndpoints`, `SearchAlertJob` + `SearchAlertWorker` |

Rules worth knowing:
- Rows are operator-owned and applicant-owned (`IApplicantOwned`): an owner or buyer reads only their rows; the team reads all.
- Public payloads come from published opportunities and their published terms only; exact coordinates never leave the
  API unless the owner explicitly chose to show them (`OpportunityProjection.PublicPoint`).
- Approving a sale request never publishes. Terms sent to the owner are immutable; a change is a new version that needs
  the owner's confirmation and a new publication. Interests keep the terms version they were made on.
- Unknown amounts never pass a budget filter, and the number excluded for that reason is returned. A year of installments
  plus an annual extra payment must fit within a year of the buyer's comfortable installment; an unknown schedule is
  «incomplete», never a pass.
- List, map, facets, comparison, matching, the capacity calculator and saved-search alerts all read through
  `Discovery/DiscoveryQuery`, so they always see the same published set; map bounds apply to public points only.
- The typed terms snapshot (`TermsSnapshot`) is written once per version; published terms are never recomputed.
- Sign-in for owners and buyers is by mobile (`PhoneAuthEndpoints`); one account per mobile.

### Organization directory (`Modules/OrgDirectory`)

`directory.organizations`: Arabic and English names (with normalized forms for matching), one or more types
(`developer`, `bank`, `finance_company`), official website, source name and URL, verification date, active flag,
origin (`import` | `manual`), import keys, licence/registration numbers **only when the cited official source
publishes them**. Listing says nothing about partnership with Rahoon, support for contract transfers, or (without a
cited source) licensing.

- **Importer** (`DirectoryImporter`, CLI `dotnet Rahoon.Api.dll import-directory`): sources in `DirectorySources.Catalog.cs`
  — SAMA licensed banks and licensed finance companies (SAMA's JSON list handler) and REGA's qualified off-plan
  developers (Wafi results pages, paced, stops when REGA throttles and says where to resume). A verified CSV/JSON
  dataset goes through the same pipeline (`--file`). The REGA developer list collected on 2026-10-01 (1,176 qualified
  developers from 86 of REGA's 92 pages; pages 1–3 come from the live import) is bundled as
  `Seed/Data/rega-developers-2026-10-01.csv`, generated from the workbook by `scripts/rega_xlsx_to_csv.py`. Matching: the source's own key, then the normalized Arabic name,
  then the normalized English name; types merge. The source item that created a record owns its fields; other items only
  fill gaps. Records an administrator edited (`AdminEditedAt`) are never changed again. Nothing is ever deleted or
  deactivated by an import; a failed source changes nothing. Report: discovered, created, updated, skipped, failed.
- **Administration** (`/api/team/directory`; reading needs `directory.read` or `directory.manage`, changes need `directory.manage`): list/search/filter (type,
  status, origin), add, edit (optimistic concurrency), activate/deactivate with reason — no delete. Every change is
  audited. Web: `/team/organizations`.
- **Forms**: `GET /api/directory/organizations?kind=developer|financier` (active only) feeds the searchable picker in
  the sale wizard and the buyer form. A request keeps the id **and** the name recorded when chosen, so later edits or
  deactivation never rewrite it; deactivated organizations can no longer be chosen.

## File storage (`Infrastructure/Storage`)

Business code holds only a stable **file id**. Each upload is two rows:

- `files.stored_files` — metadata and ownership: original file name, content type detected from the file signature,
  size, SHA-256, provider, provider storage reference, visibility (`Private` | `ListingPhoto`), uploader, owner,
  subject (`sale_request` + id), timestamps. Safe in any query: it has no payload.
- the payload, behind `IFileContentStore`. Today's provider is **`database`** (`DatabaseContentStore`): bytes in
  `files.file_blobs.content` (`bytea`, never Base64), written in the same transaction as the business change and read
  only when the file is served.

`FileStore` validates (`Storage:Limits`: `MaxPhotoBytes`, `MaxDocumentBytes`, `MaxPhotosPerRequest`,
`MaxDocumentsPerRequest`, `PhotoTypes`, `DocumentTypes`; types detected from magic bytes — PDF, JPEG, PNG, WebP), stores
through the provider named by `Storage:Provider` (default `database`), and reads through **the provider recorded on each
file**, checking size and checksum. Listing photos have EXIF/XMP/IPTC metadata (including GPS) stripped before storing.

Serving: `GET /api/files/{fileId}` decides access from what the file is attached to — a private document: its owner and
team members holding `documents.read` for that request (all work, or the request is assigned to them); a listing photo:
its owner and members holding `market.view` for that request, plus anyone while it is approved and shown in a published
opportunity. Anything else is 404. Private files are sent `Cache-Control: no-store`. Public photos also keep `GET /api/market/photos/{photoId}`.

No malware scanning is part of this platform today.

### Moving to object storage later (not provisioned)

Database and object-storage files can coexist because every file row names its provider:
1. Implement `IFileContentStore` for the object store (e.g. `Provider = "s3"`, `StorageRef` = object key), register it
   next to `DatabaseContentStore`.
2. Set `Storage:Provider` to the new provider: new uploads go there; existing files keep being read from the database.
3. To move old files: for each `stored_files` row with `provider = 'database'`, read the blob, put it in the object
   store, verify size and SHA-256 by reading it back, then in one transaction update `provider` and `storage_ref` and
   delete the `file_blobs` row. File ids, URLs and references never change.

### Backup and restore

Files live in PostgreSQL, so **a database backup is the complete data backup** (`pg_dump` of the `rahoon` database:
schemas `identity`, `market`, `directory`, `files`, `app`, `audit`). The API's `/data` volume holds only the
Data Protection key ring (`/data/keys`), which must be backed up too and kept with the database: without it the
encrypted mobile numbers cannot be read. `PII_LOOKUP_KEY` must stay the same. See `deploy/README.md`.

### Migration of the earlier disk files (2026-10-01)

`DatabaseMigrator` (CLI `migrate`, migrate-on-startup, tests) applies migrations up to `RemoveLegacyMortgageModel`, then
`LegacyDiskFileMigrator` copies every marketplace file still on disk (`Storage:Root`) into `files.*` — checking the
recorded size and SHA-256 before, and the bytes read back from the database after — links the row, and only then deletes
the disk copy; it also deletes the removed model's per-organization folders. `FilesInDatabase` then drops the disk-path
columns and refuses to run while any file is not copied.

## Security model

* **Sessions** — opaque random token in an HttpOnly `rahoon_sid` cookie (SameSite=Lax, Secure outside development);
  only its SHA-256 is stored. Idle timeout 30 min, absolute 12 h, immediate revocation. No bearer tokens.
* **CSRF** — double submit: the readable `rahoon_csrf` cookie must be echoed in `X-CSRF-Token` on every unsafe `/api`
  request and is checked against the server-side session; unsafe requests also need an allowed `Origin`/`Referer`.
* **Team sign-in** — e-mail + password + SMS code (sandbox gateway); 5 failed passwords or 3 wrong codes → 15-minute
  lock. Only members of «فريق رهون» can sign in at `/login`.
* **Owners and buyers** — mobile + SMS code, no password (`Auth:SmsConfirmation`; when off, codes are confirmed
  automatically and the UI never claims the mobile was verified).
* **Tenancy** — `RequestContext` comes from the session and the active membership, never from client input. Operator
  rows have a global EF query filter; individuals see only their own applicant-owned rows; `TenantWriteGuardInterceptor`
  refuses writes outside them.
* **Authorization (Phase 1.5)** — permission catalog `Modules/Identity/Permissions.cs`, grouped by area; reserved keys of
  later phases are listed but not grantable. Each role grant carries a **scope** (`assigned` | `all`); a member's effective
  scope is the widest per permission across their roles (`EffectiveAccess`), never borrowed from another permission.
  Eight protected system roles (`SystemRoles.cs`: platform owner, operations manager, case manager, document reviewer,
  publisher, finance officer, support, auditor) are re-applied exactly by `SystemRoleSync` on every migration; custom roles
  are edited in the console. Scope is enforced in the query and in every loader (`Modules/Market/TeamScope.cs`): lists,
  counts, details, mutations, contact reveals and downloads. Out-of-scope records answer 404; in scope but without the
  action's permission, 403.
* **Team administration** — `/api/team/admin/*` (`TeamAdminEndpoints`): members, invitations, roles, effective access,
  log. Nobody changes their own roles or status; a manager acts only on members whose grants they fully hold and grants
  only what they hold; the last active platform owner is protected under a per-team advisory lock that also re-reads the
  actor. Invitations are bound to an e-mail and mobile, store only the token hash, expire, are single-use and revocable;
  no e-mail is sent (no provider), the inviter hands over `/join#token`. Bootstrap: CLI `bootstrap-owner`.
  Membership and grants are re-read on every request, so role changes, suspension and removal apply to sessions already
  issued; suspension and removal also revoke the member's sessions.
* **PII** — mobiles are encrypted with ASP.NET Data Protection (`PiiProtector`) with an HMAC lookup hash and masked
  copies. The SMS log stores masked numbers and blanks one-time codes.
* **Idempotency & concurrency** — state-changing endpoints use `.Idempotent()` (`Idempotency-Key`; same key + same
  payload replays the stored response; different payload → 422). Entities with `IConcurrencyVersioned` use `xmin`.
* **Audit** — sign-ins, contact reveals and directory changes; hash chain per organization verified by
  `AuditLog.VerifyChainAsync` (hash version 3; `data_json` is stored as text so the hash verifies after a round-trip).

## Integrations

SMS goes through `ISmsGateway`; the only adapter is the **sandbox** (records the attempt as `simulated` in
`app.outbound_sms`, sends nothing). No other external system is connected.

## Background work

`SearchAlertWorker` (hosted service, Phase 2) runs `SearchAlertJob` every `Alerts:IntervalSeconds` when `Alerts:Enabled`
(on in Development and Staging, off by default and in Testing; CLI `run-alerts` runs it once). The job queues unseen matches of
active, consented saved searches with `INSERT … ON CONFLICT DO NOTHING` on (search, opportunity, terms version), claims them with
`FOR UPDATE SKIP LOCKED`, re-checks publication and opt-out at send time, writes the in-app notification and the status in one
transaction, and marks SMS rows `sending` before the gateway call so a crash can't send twice. With one API instance or several,
nothing is sent twice.

## Tests

`server/tests/Rahoon.Api.Tests`: xUnit + `WebApplicationFactory` + Testcontainers PostgreSQL. The fixture migrates
through `DatabaseMigrator` and seeds the demo. `MigrationTests` upgrades a database holding data of the removed model;
`CurrentModelTests` checks a fresh database has none of its objects. Frontend: ESLint, `tsc --noEmit`, `next build`,
Playwright (`web/e2e/market-journey.spec.ts`).
