# Phase 0: Foundation + anchor screens ✅

**Goal:** a secure, multi-tenant skeleton with the design system and the five anchor screens, including a real create-case → case-detail flow.

## Definition of done

All items below are done. Kept here as the reference for what the platform already guarantees.

## What's finished

| Area | Item | Status | Where |
|---|---|---|---|
| Infra | Docker PostgreSQL, `.env.example`, user-secrets, dev scripts (`scripts/dev-api.sh`, `api-smoke.sh`) | ✅ | repo root, `scripts/` |
| Backend | ASP.NET Core 10 modular monolith, EF Core + Npgsql, snake_case, schema per module, xmin concurrency, UTC, decimal(18,2) money | ✅ | `server/src/Rahoon.Api` |
| Backend | Real migrations (InitialSchema, audit append-only trigger, …) | ✅ | `Infrastructure/Persistence/Migrations` |
| Security | HttpOnly opaque session cookie, CSRF double-submit + Origin check, SMS-OTP MFA (sandbox), step-up for sensitive actions, lockout, rate limits, Idempotency-Key replay | ✅ | `Infrastructure/Auth`, `Http/EndpointAccess.cs` |
| Security | Tenant isolation: global query filters + write-guard interceptor; tenant taken from membership, never from the browser | ✅ tests | `SecurityTests` |
| Security | PII encrypted at rest + HMAC lookup; server-side masking; audited 60-second reveal | ✅ | `Security/Crypto.cs` |
| Audit | Hash-chained audit log, blocked attempts recorded, DB trigger rejects tampering | ✅ tests | `Modules/Audit` |
| Workflow | Explicit case transition table with named guards (`docs/case-transition-matrix.md`) | ✅ tests | `CaseWorkflow.cs` |
| Permissions | Permission catalog + system roles (`docs/permission-matrix.md`) | ✅ | `Modules/Identity` |
| Seed | Fictional demo data: 1,248 active cases, anchor case RH-2026-004172 | ✅ | `server/src/Rahoon.Api/Seed` |
| Web | Next.js 16 app, Tailwind v4 tokens (palette preserved), IBM Plex Arabic, RTL default, brand SVGs, UI kit (`/dev/ui` gallery) | ✅ | `web/src` |
| Web | Shells: lender, owner, portal, platform, public | ✅ | `components/shell` |
| S03/S04/S06 | Login → MFA → choose organization | ✅ browser | `app/(auth)` |
| D01 | Owner invitation + identity check + OTP | 🟩 | `app/(auth)/invite` |
| S12 | Access denied page | 🟩 | `app/access-denied` |
| L01 | Portfolio dashboard (numbers match design) | ✅ browser | `app/(lender)/portfolio` |
| L02 | Case list, views, filters, bulk reassign, masked CSV | ✅ browser | `app/(lender)/cases` |
| L03 | Create-case wizard (6 steps, autosave, duplicate check) → real case | ✅ browser | `app/(lender)/cases/new` |
| L05 | Case workspace overview, next action, sensitive actions | ✅ browser | `app/(lender)/cases/[ref]` |
| L13 | Solution builder with live server calculation | ✅ browser | `cases/[ref]/solutions/[n]` |
| L14/L15/L17 | Compare, submit for approval, owner preview | ✅ browser (L15) | `cases/[ref]/solutions/*` |
| L16 | Approvals inbox + decision with step-up (maker-checker) | ✅ browser, 2026-09-24 | `app/(lender)/approvals` |

## Direction review (added 2026-09-25)

The ✅ marks above record **technical** completion and verification, and they stay. They don't mean the screens are product-correct under the individual-first direction (`docs/product/product-direction.md`). Review results:

| Item | Review | Follow-up (phase · step) |
|---|---|---|
| Security, tenancy, audit, workflow engine, PII masking, idempotency, design system, shells | **OK.** Reused unchanged | — |
| S03/S04/S06 staff login → MFA → organization | **OK for staff.** The entry must be split from the individual's sign-in | 1A · 3 |
| D01 owner invitation + identity check | **Secondary.** Kept as «مسار ثانوي»; the self-registration path is new | 1A · 2 (label), 1B · 3 (account type) |
| Owner session model (invitation link + last-4 + OTP, pinned to exactly one case) | **Rework.** An individual account must exist before any case (X3, X4; Q14, Q15) | 1A · 3 |
| L01 portfolio as the lender's default landing | **Secondary.** Stays for reports; the proposed default is «الطلبات الواردة» (Q1) | 1A · 5 |
| L03 wizard as the way cases start | **Secondary / exception.** Needs a mandatory reason | 1B · 3 |
| Case workflow starts at `Draft` → `AwaitingData` | **Extend.** A case created from an accepted request starts at `Verification`, linked to `REQ-…` | 1A · 5 |
| L13–L17 solution mechanics + L16 approvals | **OK mechanics, provisional content.** Solution types wait for Q11 | 1A · 7 |
| Seed data (owners exist only as invited parties) | **Extend.** Add B13 individuals and requests | 1A · 4–5 |

## Known small leftovers (carried into Phase 1A step 2)

- Breadcrumb shows the raw `new` segment on `/cases/new/...`.
- Dialog bodies had no padding: fixed on 2026-09-24 (`10f440e`).
