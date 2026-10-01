# رهون — Architecture and conventions

## Overview

```
web/      Next.js 16 (App Router, TypeScript, Tailwind v4) — public pages + role-aware portals
server/   ASP.NET Core 10 Web API (modular monolith) + xUnit integration tests
          PostgreSQL 16 via EF Core 10 (Npgsql), one schema per module
design-source/  Mirror of the design project (source of truth for UI)
docs/adr/       Architecture decision records (0001: request ownership, Rahoon team tenant, request lifecycle)
docs/     Specs per design batch, implementation map, matrices, progress
```

The browser only talks to the Next.js origin. `next.config.ts` rewrites `/api/*` to the API
(`API_ORIGIN`, default `http://localhost:5080`), so session cookies are first-party. Server
Components call the API directly, forwarding the incoming `Cookie` header.

## Current model and the archived one (2026-10-01)

The product is the Saudi exit/buy platform (`docs/product/product-definition.md`). Its backend is the **Market** module
(schema `market`) plus the shared Identity, Audit, storage and integration pieces. Everything else below belongs to the
withdrawn mortgage-help model: `Features:LegacyMortgage` (API, default false) maps its endpoints, jobs and demo seed only
when true; `RAHOON_LEGACY_MODES=1` does the same for the web routes (`src/proxy.ts` answers 404 otherwise). Its tables are
kept untouched (`docs/redefinition/legacy-inventory.md`).

### Market module (`Modules/Market`)

| File | Role |
|---|---|
| `FieldCatalog.cs` | The single rule set for sale-request fields: scope (property / obligation), property types, obligation kinds, conditions, «لا أعرف», submit tier and publish tier, documents per kind, Saudi cities. Served at `GET /api/market/catalog`; applied on every save (inactive branches dropped) |
| `MarketCalculator.cs` | One calculation engine (decimal) for developer, financier and mixed tracks, buyer fit, commission policy (`Market:Commission`, never 0 while unapproved). Used by the public calculators, the owner estimate, the team's terms and the search snapshot |
| `MarketEntities.cs` | SaleRequest, SaleObligation, PrivateDocument, ListingPhoto, FigureVerification, ExternalApproval, CompletionRequest, BuyerRequest, Opportunity, OpportunityTerms (versioned), Interest, SavedOpportunity, MarketEvent (log + in-app notifications), MarketNotification (SMS attempts with honest results), ContactMessage, ObligationParty |
| `SaleRequestEndpoints.cs` | Owner: create (idempotent per device draft id), autosave, submit, resubmit, withdraw, files, opportunity confirmation |
| `BuyerEndpoints.cs` | Buyer: one live buyer request, suggestions with fit, interests, saved, account summary |
| `TeamMarketEndpoints.cs`, `TeamOpportunityEndpoints.cs` | «فريق رهون»: review, completion, figure verification with source/date, corrections with reason, file review, external approvals, opportunity preparation, owner confirmation, checklist, publish/pause/withdraw, interests, contact messages |
| `MarketPublicEndpoints.cs` | Visitors: catalog, contact, calculators, server-side search, opportunity details, public photos |

Rules worth knowing:
- Rows are operator-owned and applicant-owned (`IApplicantOwned`): the owner/buyer reads only their rows; the team reads all.
- Public payloads come from published opportunities and their published terms only; exact coordinates never leave the API
  (`OpportunityProjection.PublicPoint`). Listing photos and private documents use separate storage areas
  (`market-photos/…`, `market-docs/…`); photos are public only while approved and shown in a published opportunity.
- Approving a sale request never publishes. Terms sent to the owner are immutable; any change is a new version that needs
  the owner's confirmation and a new publication. Interests keep the terms version they were made on.
- Search: unknown amounts never pass a budget filter (`IS NOT NULL AND <= max`), and the count excluded for that reason is returned.
- Sign-in is by mobile (`PhoneAuthEndpoints`); the account of an existing mobile is reused, never duplicated.

## Backend modules of the archived model (`server/src/Rahoon.Api/Modules`)

| Module | Schema | Responsibility |
|---|---|---|
| Identity | `identity` | users, organizations (tenants), memberships, roles/permissions, sessions, OTP, invitations |
| Cases | `cases` | case aggregate, parties (PII encrypted), property, mortgage, financing, debt snapshots, workflow, access, wizard, import |
| Documents | `documents` | document types/rules, case documents, append-only versions, requests, download log |
| Assessment | `assessment` | valuation reports, affordability analysis |
| Solutions | `solutions` | solution versions, approval requests/limits, offers, negotiation, consent records |
| Agreements | `agreements` | agreements, installments, payments (maker-checker), breach reviews |
| Communications | `comms` | messages, tasks, appointments, notifications, templates, outbound (sandbox) |
| Complaints | `complaints` | complaints/objections with independent reviewer |
| Referral | `referral` | manual judicial referral, external reference + official status (verbatim) |
| Closure | `closure` | reconciliation, closure documents |
| Providers | `providers` | assignments for valuers/brokers/agents, messages, submissions |
| Administration | `admin` | SLA rules, integration states, temp access, applications, retention, idempotency |
| Audit | `audit` | append-only hash-chained audit events (DB trigger rejects UPDATE/DELETE) |
| Owner | — | owner (debtor) portal endpoints over the modules above |

Each module owns its entities (`*Entities.cs`), configuration (in `Infrastructure/Persistence/Configurations.cs`,
one `IEntityTypeConfiguration` per entity, table in the module schema) and endpoints (`*Endpoints.cs`,
a static `Map(IEndpointRouteBuilder)` registered in `Modules/ModuleRegistry.cs`).

## Security model

* **Sessions** — `Infrastructure/Auth`. Opaque random token in an HttpOnly `rahoon_sid` cookie
  (SameSite=Lax, Secure outside development); only its SHA-256 is stored (`identity.sessions`).
  Idle timeout per organization (30 min default), absolute 12 h, immediate revocation.
  No bearer tokens, nothing in localStorage.
* **CSRF** — double submit: a readable `rahoon_csrf` cookie must be echoed in `X-CSRF-Token` for
  every unsafe `/api` request, and is verified against the hash stored on the server-side session.
  Unsafe requests must also carry an allowed `Origin`/`Referer` (`Web:AllowedOrigins`).
* **MFA** — password + SMS OTP (sandbox gateway) for every institutional login; 5 failed
  passwords or 3 wrong codes → 15-minute lock. Sensitive decisions (approvals, cancellation,
  referral, closure) require a fresh OTP step-up (`EndpointAccess.EnsureStepUp`).
* **Owners** never use passwords: invitation link + last 4 digits of the national ID + SMS OTP.
  An owner session is pinned to exactly one case (`RequestContext.OwnerCaseId`).
  > **Superseded by the product direction of 2026-09-25** (`docs/product/product-direction.md`, X3/X4).
  > Individuals self-register (national ID/iqama + mobile + OTP) and own **requests** before any case exists; the
  > lender invitation becomes a secondary route. Decided (Q7, Q14): an independent account with several requests,
  > each with its own reference, status, documents and access; the session is not tied to one case. The code above is
  > still the implemented behaviour; the rework is planned in Phase 1A step 4 (returning sign-in: Q15, open).
  > In the MVP, lenders have no platform access; the **Rahoon team** (platform staff) reviews and coordinates (Q1).
* **Individuals** (implemented in Phase 1A step 4): self-registration and sign-in with national ID/iqama + SMS code at
  `/start` (no password, Q15 interim); session scope `Individual` with no organization and no tenant data; anti-enumeration
  (same answer whether the ID is registered; decoy challenge when the mobile differs); 3 wrong codes on the real mobile →
  15-minute lock. Identity is self-declared until a national identity provider exists (Q10).
* **Tenancy** — `RequestContext` is resolved from the session and the *active membership*, never
  from client input. Every `IOrgOwned` entity has a global EF query filter on
  `RequestContext.DataOrganizationIds`, and `TenantWriteGuardInterceptor` refuses writes outside it.
  Providers/agents get lender orgs only through unexpired assignments; platform staff get tenant
  data only through an approved, unexpired temporary-access grant.
* **Authorization** — permission catalog `Modules/Identity/Permissions.cs` (`P.*`), role templates
  `SystemRoles.cs` copied per organization. Endpoints declare `.RequirePermission(...)`,
  `.RequireOrg(kind)`, `.RequireOwner()`. Within a tenant, `CaseAccess` restricts roles without
  `case.view_all` to their own/team cases. Refusals use the same response whether or not the
  resource exists.
* **Hidden vs disabled** — an action the role can never perform is not returned; an action the role
  may perform but the case is not eligible for is returned `enabled:false` with Arabic `reasons`.
* **PII** — national ID, phone and deed numbers are encrypted with ASP.NET Data Protection
  (`PiiProtector`) and stored with masked copies (A-10 rules). Full values are only returned by the
  audited 60-second reveal endpoint. **Production** must store the key ring in a KMS/HSM.

## Domain rules implemented in code

* **Case state machine** — `Modules/Cases/CaseWorkflow.cs`: 16 statuses, an explicit transition
  table (actor permission, allowed sources, reason, step-up, named guards). `TransitionAsync`
  checks expected status (stale → 409), permission, reason, step-up, guards; refusals are audited
  in a separate transaction (`AuditLog.RecordBlockedAsync`) so they survive the rollback.
  Declines never lead to referral; referral is a separate approved decision; `Paused` restores the
  previous status on resume.
* **Separation of duties** — preparer ≠ reviewer ≠ approver (enforced in submit/decision), payment
  recorder ≠ matcher (also a DB check constraint), complaint reviewer independent of the case team.
* **Approval limits** — `ApprovalRouting` picks the lowest effective tier covering amount, waiver %
  and solution kind; exceeding a tier escalates rather than blocking; re-checked at decision time.
* **Money** — `decimal(18,2)`; calculations only on the server (`SolutionCalculator`); the final
  installment absorbs rounding so Σ installments = rescheduled amount.
* **Time** — all timestamps `timestamptz` UTC; business dates `date`; SLA in Saudi business days
  (Fri/Sat weekend); Hijri display via Umm al-Qura.
* **Idempotency & concurrency** — state-changing endpoints use `.Idempotent()` (`Idempotency-Key`
  header; same key + same payload replays the stored response byte-for-byte; different payload → 422).
  Aggregates carrying `IConcurrencyVersioned` use PostgreSQL `xmin` optimistic concurrency (→ 409).
  Unique partial indexes prevent duplicate pending approvals.
* **Audit** — every transition, decision, reveal, upload/download, config change; hash chain per
  organization serialised with `pg_advisory_xact_lock`; verified by `AuditLog.VerifyChainAsync`.
  **Ordering rule:** inside a transaction call `workflow.TransitionAsync` (which may record a blocked
  attempt in its own transaction) *before* any `audit.RecordAsync`, which takes the chain lock.

## Integrations

`Infrastructure/Integrations`: every integration has a state `enabled | simulated | pending |
unavailable | failed` stored in `admin.integration_settings` and shown in the UI. No live external
system is connected: SMS/e-mail are **simulated** (messages stored, never sent; in Development the
OTP is echoed as `sandboxCode` and labelled as sandbox in the UI); national identity, licensed
e-signature, licensed payment, core banking and the judicial channel are **unavailable** adapters
that refuse with a clear message, and the product uses documented manual paths instead.

## Document storage

`IDocumentStorage` with `LocalDocumentStorage` (private directory, org-scoped keys). Content types
are detected from magic bytes (PDF/JPG/PNG, ≤ 20 MB); `BasicSignatureScanner` is a placeholder
(EICAR signature only) — production needs a real scanner and object storage adapter.

## Tests

`server/tests/Rahoon.Api.Tests`: xUnit + `WebApplicationFactory` + Testcontainers PostgreSQL. The
fixture applies the real EF migrations and seeds fictional data (without the bulk portfolio).
`Scenarios` builds independent cases through the API so tests never depend on order.
Frontend: ESLint, `tsc --noEmit`, `next build`, Playwright E2E (see web/README.md).
