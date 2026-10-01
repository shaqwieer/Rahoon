# Rahoon roadmap — decisions

Product source of truth: [`docs/product/product-definition.md`](../../product/product-definition.md) (its §9 lists the open
product decisions D1–D8). This file records what the roadmap phases adopted.

## Approved by the owner

| Date | Decision |
|---|---|
| 2026-10-01 | The mortgage-help / judicial-agent / debt-settlement model is removed for good; Rahoon is the Saudi exit/buy platform. |
| 2026-10-01 | Files stay in PostgreSQL behind `IFileContentStore`; object storage and malware scanning are out of these phases. |
| 2026-10-01 | Roadmap after Phase 1: 1.5 (team & access) → 2 (discovery & matching) → 3A (offers & reservations) → 3B (transfer & completion) → 4 (operations & reports) → 5 (launch readiness), one phase per session. Brief: `Rahoon_Next_Phases/`. |
| 2026-10-01 | Implement Phase 1.5 directly, writing its specification and handoff in the same session; the other phase plans are written by their own sessions. |

## Adopted in Phase 1.5 (reversible technical/product defaults)

| Topic | Choice | Why |
|---|---|---|
| Existing permission keys | Kept; `market.edit`, `market.verify`, `market.decide`, `documents.read`, `documents.review`, `directory.read` split out of the broad ones | Brief requires separating review, decision, verification and document access |
| Old role keys | Renamed in place: team_lead → platform_owner, team_coordinator → case_manager, team_verifier → publisher | Members keep their access; no orphaned rows |
| Scope model | Per grant: `assigned` / `all`; effective = widest per permission only | Brief: scope on one permission can't widen another |
| "Assigned" | Record's assignee, or the assignee of the work it belongs to (request → opportunity → interest) | Case managers keep following their file into its opportunity |
| Out-of-scope answer | 404 (record not revealed); in scope but action not granted: 403 | Matches existing file endpoint behaviour |
| System roles | Fixed grants, re-synced on every migration; custom roles for other mixes | Brief: protected system roles vs custom roles |
| Ops manager | No publishing, no team/role administration by default | Separation of duties; grant through a custom role if wanted |
| Case manager | Decides (approve/reject) on assigned requests; doesn't verify figures | Keeps the Phase 1 flow; brief: "no financial confirmation by default" |
| Support | Follows all work (visitor inbox) but views only assigned requests | Contact messages belong to no case |
| Invitations | No e-mail sent; one-time `/join#token` link shown once to the inviter; 72 h (`Team:InvitationHours`) | No e-mail provider is configured; the UI says so |
| Session effect | Grants re-read per request; sessions revoked on suspend/remove | Existing per-request resolution is the "security version" |
| Last owner | Per-team advisory transaction lock, actor re-read inside it | Database-backed concurrency, not an in-memory lock |
| Bootstrap | CLI `bootstrap-owner`, refused once an active owner exists | No public super-admin endpoint, no default password |
| Password for invited staff | ≥ 10 characters with letters and digits | No policy existed for staff passwords set by users |

## Adopted in Phase 2 (reversible technical/product defaults)

| Topic | Choice | Why |
|---|---|---|
| One spine | `SearchCriteria` + `DiscoveryQuery` + `Affordability` for list, map, facets, comparison, matching, capacity calculator and alerts | Brief: list, map, comparison, calculator and saved search agree on the same data |
| Strict affordability | Unknown → incomplete (excluded and counted); annual extra payment counted in the year; one-off extra must fit in cash left after paying now | Brief acceptance 2; Phase 1 extra-payment rule kept |
| Snapshot backfill | New typed columns filled from stored JSON only; sent/published terms never recomputed | Terms are immutable once sent |
| Relevance | Preferences met (with a profile) → verified figures → lowest cash now (with a budget) → newest → id; no percentage | Explainable and SQL-paginated |
| Buyer profile in search | `match=me` from the session only; URL never carries the buyer's numbers | Brief: shared URLs must not expose another buyer's profile |
| Developer name in public | Only a developer chosen from the directory is shown and searchable; owner-typed names stay internal; financiers never | Brief asks for a project/developer filter; typed text isn't reviewed for publication |
| Guest favorites | Not stored locally; guests sign in (existing UX) | Brief: «if supported by current UX» |
| Negotiability filter | Not offered | Not recorded in the model |
| Next payments | Due now, installment cadence («next due date not recorded»), extra payment with its recorded date | No next-installment date in the model; no invented dates |
| Map | OSM tiles via env; client-side grid clustering; 500-marker cap with a notice; bounds on public points only, span ≤ 40° | D3 open; no extra dependency |
| Comparison | ≤ 4, browser-local selection; «لا ينطبق» / «غير معروف بعد» cells; unavailable → reference only | Brief |
| Market reference | Calculator only, entered by the person with date and source; difference wording, never saving/return | Brief: dated comparable reference, no guaranteed saving |
| Alerts | Baseline on save/enable/resume; alert new matches, re-alert only when cash now drops; one in-app notification per search per run; SMS optional, sandboxed (D1) | «Meaningful qualifying revisions»; no spam; honest delivery |
| Alert worker | Hosted service, `Alerts:Enabled` (Dev/Staging on, Testing off), `Alerts:IntervalSeconds`; CLI `run-alerts` | No worker existed; tests call the job directly |
| Saved searches | ≤ 20 per person; same criteria → same search; soft delete | Duplicate operations are idempotent |
| Photo caching | `public, no-cache` + ETag | A withdrawn listing's photo must not stay cached |
| Search platform | PostgreSQL; one partial index on published public points; measured on 20k rows | Brief: no separate engine by default |

## Open material decisions (owner)

None blocks Phase 1.5. Still open from the product definition: D1 SMS provider (team sign-in codes and customer codes
are sandboxed), D2 commission policy, D3 map provider, D4 REGA/FAL advertising, D5 complete developer list, D7 object
storage/scanning. New: **an e-mail provider** for invitations (today the link is handed over manually).
