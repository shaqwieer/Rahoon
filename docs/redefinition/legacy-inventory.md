# Redefinition 2026-10-01 — inventory of the old model, what is removed, what is reused

The product changed from the **mortgage-default help platform** to the **Saudi exit/buy platform**
([`docs/product/product-definition.md`](../product/product-definition.md)). This is a change of business model, not a rename.

## How the old model is removed (reversible, no data loss)

| Step | What was done |
|---|---|
| Rollback point | Git tag `legacy-mortgage-final` on `266541e` (last commit of the old model). Work happens on the branch `redefine/exit-marketplace` |
| Feature flag | `Features:LegacyMortgage` (API, default **false**) and `RAHOON_LEGACY_MODES` (web, default off) |
| API | With the flag off, the old endpoints are **not mapped** (404). The old background jobs don't run. The old demo data isn't seeded. Staff login is refused for every organisation except «فريق رهون» |
| Web | With the flag off, `src/proxy.ts` answers **404** for every old portal route. The old nav, landing, SEO texts and links are replaced by the new ones |
| Database | **Nothing is dropped.** The old schemas (`cases`, `solutions`, `agreements`, `requests`, `referral`, `closure`, `sale`, `providers`, `ecosystem`, `complaints`, …) and their rows stay untouched. Every new migration is checked for `Drop*` (none allowed on old schemas). The data is archived in place. Its retention is decision D6 |
| Files | Uploaded documents under `Storage:Root` stay where they are. New market files use their own key prefixes (`market/docs/…`, `market/photos/…`) |
| Tests | The existing suite runs with the flag on, as the regression for the archived code. A new test checks that with the flag off the old routes answer 404 and that non-team staff can't sign in |

The old code is kept behind the flag, not deleted, so the change can be reviewed and rolled back. Deleting it is a later, separate decision.

## Inventory

**Legend.**
- **Removed** = not reachable in the product (flag off). The code is archived, and the data is kept.
- **Reused** = a generic part kept by the new model.
- **New** = built for the new model.

### Backend (`server/src/Rahoon.Api/Modules`)

| Module / item | Old meaning | Action |
|---|---|---|
| Cases (case aggregate, debt snapshots, workflow, wizard, import) | Lender's default case | **Removed** |
| Solutions (rescheduling, settlement offers, approvals, negotiation) | Debt solutions | **Removed** |
| Agreements (installments, payments maker-checker, breach monitor) | Settlement execution | **Removed** (the BreachMonitor job is off) |
| Requests (`requests` schema: individual's help request, P1–P4 paths, lender coordination, offers, execution) | Individual in default asks for help | **Removed**. The new sale request is a **new** entity, not a renamed one |
| Sale (voluntary / consensual sale) | «البيع الرضائي» | **Removed** |
| Referral, AgentEndpoints | Judicial referral, judicial agent | **Removed** |
| Closure | Reconciliation and closure of a debt | **Removed** |
| Owner (invitation portal) | Debtor invited by the lender | **Removed** |
| Providers, Ecosystem | Valuers, brokers, agents for the lender | **Removed** |
| Complaints | Lender-tenant complaints | **Removed** |
| Assessment | Valuation and affordability for a debt case | **Removed** |
| Analytics, Administration (lender settings, SLA, approval limits, platform admin, temp access, billing, workflow designer) | Lender SaaS administration | **Removed** (the TempAccessExpiry job is off) |
| Imports | Lender portfolio import | **Removed** |
| Communications (case messages, notifications, templates) | Case communications | **Removed** from the product. The new market module logs its own notifications |
| Documents (case documents and rules) | Case documents | **Removed**. `IDocumentStorage` and `IFileScanner` are **reused** |
| Identity: sessions, CSRF, MFA, OTP, staff login, roles and permissions | Generic | **Reused** |
| Identity: individual sign-in by national ID + mobile | Debtor self-registration | **Removed** from the product. It's replaced by **mobile-first** sign-in (new). The existing individual accounts are reused when the same mobile signs in |
| Organisation kinds Lender / ServiceProvider / JudicialAgent / Platform | Tenants of the old model | **Removed** (login refused while the flag is off). Their rows are kept |
| Organisation kind Operator «فريق رهون» + team roles | Team that handled requests | **Reused** as the Rahoon team. It gets new `market.*` permissions, synced into existing role rows |
| Audit (hash-chained) | Generic | **Reused** for market events |
| Integrations registry (SMS sandbox, unavailable adapters) | Generic | **Reused** (SMS only). The lending, judicial and core-banking adapters aren't used |
| Seed (DevSeeder: lenders, cases, requests, provider data) | Old demo data | **Removed** from the default seed. It's seeded only with the flag on. **New** market demo data, marked as test data |
| — | — | **New** module `Market` (schema `market`): field catalog, sale requests, obligations, documents and photos, buyer requests, opportunities and terms versions, external approvals, interests, saved opportunities, calculator |

### Web (`web/src/app`)

| Route group | Old meaning | Action |
|---|---|---|
| `(public)/page.tsx` landing, header and footer nav, `(public)/layout` SEO | Help for people in default, «4 مسارات», «للجهات الممولة» | **Replaced** by the new home and navigation (الرئيسية، بيع عقارك، الفرص المتاحة، كيف نعمل، الحاسبات، تواصل معنا، حسابي) |
| `(auth)/start` | Sign-in by national ID + mobile | **Replaced** by mobile-first sign-in |
| `/my/**` | The individual's mortgage-help requests | **Removed**. Replaced by `/account/**` |
| `/owner/**`, `(auth)/invite/**` | Lender-invited debtor portal | **Removed** |
| `(lender)/**` (portfolio, cases, approvals, complaints, reports, settings, …) | Lender workspace | **Removed** |
| `/provider/**`, `/agent/**`, `/platform/**`, `/print/**`, `/select-context` | Providers, judicial agent, platform admin | **Removed** |
| `/team/**` (requests queue, verify, objections) | Team handling help requests | **Replaced** by the new team workspace (sale requests, buyer requests, opportunities, interests, my tasks) |
| `(auth)/login`, `/login/mfa` | Staff login | **Reused** for the Rahoon team |
| `(public)/privacy`, `(public)/terms` | Legal drafts for the old model | **Rewritten** for the new model (still drafts pending legal review) |
| `components/ui/*` (buttons, fields, dialogs, status, toast, …), `lib/format.ts`, `lib/api/client.ts`, i18n plumbing | Generic | **Reused** |
| `components/case`, `components/owner`, `components/individual`, lender shells | Old portals | **Removed** from the product (kept behind the flag) |
| E2E specs `owner-journey`, `lender-flow`, `lender-execution` | Old journeys | They run only when `RAHOON_LEGACY_MODES=1`. A new market journey spec is added |

### Content, SEO, docs

| Item | Action |
|---|---|
| Meta title and description, indexable routes (`next.config.ts`) | Rewritten. The new public pages are indexable, and the old routes aren't |
| `README.md`, `docs/architecture.md` | Updated to the new model |
| `docs/product/product-direction.md`, `docs/adr/0001-*`, `docs/adr/0002-*`, design specs B2–B13, phase files 0–4 | Marked **superseded / archived**. Kept for history |
| Client test guide (`docs/client/*`, untracked) | Left as is (the user's own files) |
