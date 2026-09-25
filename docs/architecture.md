# رهون — Architecture and conventions

## Overview

```
web/      Next.js 16 (App Router, TypeScript, Tailwind v4) — public pages + role-aware portals
server/   ASP.NET Core 10 Web API (modular monolith) + xUnit integration tests
          PostgreSQL 16 via EF Core 10 (Npgsql), one schema per module
design-source/  Mirror of the Claude Design project (source of truth for UI)
docs/     Specs per design batch, implementation map, matrices, progress
```

The browser only talks to the Next.js origin. `next.config.ts` rewrites `/api/*` to the API
(`API_ORIGIN`, default `http://localhost:5080`), so session cookies are first-party. Server
Components call the API directly, forwarding the incoming `Cookie` header.

## Backend modules (`server/src/Rahoon.Api/Modules`)

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
  > lender invitation becomes a secondary route. The code above is still the implemented behaviour; the rework is
  > planned in Phase 1A step 3 (open questions Q14, Q15).
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
