# رهون — Phase plan (work one phase at a time)

> **Product direction, confirmed 2026-09-25:** Rahoon primarily serves **individuals struggling to repay an existing mortgage**. **The individual starts the request**, and the financing institution joins later. Rahoon isn't a bank-facing product first and doesn't originate mortgages.
> - The product source of truth is [`docs/product/product-direction.md`](../product/product-direction.md). Read it before any phase work.
> - Its open decisions (Q1–Q15) gate several steps.
> - The design for the new primary journey is [`docs/design-specs/B13-owner-initiated-journey.md`](../design-specs/B13-owner-initiated-journey.md).

**Working rule:** every session works on **one phase only**, the one marked **▶ CURRENT**.
- Work its steps in order; a large phase continues over several sessions, step by step.
- Tick items off and update the status table and *Findings* before the session ends.
- Never start the next phase until the current one's *Definition of done* is met.
- If a step depends on an unanswered product question (⛔), stop at the labelled assumption or ask the product owner. Don't invent the answer.

| # | Phase file | Goal | Status |
|---|---|---|---|
| 0 | [phase-0-foundation.md](phase-0-foundation.md) | Platform skeleton + five anchor screens | ✅ Done (direction review added) |
| 1A | [phase-1a-mvp-settlement.md](phase-1a-mvp-settlement.md) | **MVP**: the individual enters Rahoon → submits a request → knows what Rahoon will do for them → follows the case study and communication (the Rahoon team leads and coordinates with the lender manually) → sees an approved offer if one exists → responds | ▶ **CURRENT** (steps 0–6 done; next: step 7, approved offer and the individual's response) |
| 1A-2 | [phase-1a2-settlement-execution.md](phase-1a2-settlement-execution.md) | After the response: tracking of the agreement, installments, payment confirmations and closure (Rahoon never holds funds). Not in the first MVP (Q13) | ⬜ Next |
| 1B | [phase-1b-shared-and-lender-ops.md](phase-1b-shared-and-lender-ops.md) | Account/search/help, institutions' secondary page (S02), rework of secondary entry routes (D01/L03/L04), complaints and objections, referral package (L25) | ⬜ |
| 1C | [phase-1c-providers-admin-platform.md](phase-1c-providers-admin-platform.md) | Provider portal, institution admin, platform admin (B7) | ⬜ (backend on master) |
| 2 | [phase-2-sale-ecosystem.md](phase-2-sale-ecosystem.md) | Consensual sale, help path P3 (B8 re-planned for team-led coordination), + service ecosystem (B9) | ⬜ (backend on master) |
| 3 | [phase-3-judicial-financial.md](phase-3-judicial-financial.md) | Judicial referral, agent portal, full reconciliation and closure (B10). **On hold (V10)**: not among the confirmed help paths | ⬜ (backend on a branch) |
| 4 | [phase-4-optimization.md](phase-4-optimization.md) | Analytics, configurable ops, drafting help, predictive insights (B11) | ⬜ (backend on a branch) |

**Two kinds of status.** Phase files track *Tech status* (built and verified, legend below) separately from *Direction review* (does it fit the individual-first direction: OK / Rework / Secondary / New / Proposed / Provisional). A ✅ is never erased because of the new direction; screens that need changes get an explicit rework step.

## Status legend (used in every phase file)

| Mark | Meaning |
|---|---|
| ✅ | Done **and verified**: tests pass and it was checked in a real browser or by Playwright |
| 🟩 | Done on `master`: builds, typechecks and has tests, but hasn't been checked in the browser yet |
| 🟨 | Finished on a **side branch**, not merged into `master` yet (branch named in the row) |
| 🟧 | Partial / WIP on a side branch (unverified; may not build) |
| ⬜ | Not started |
| ⛔ | Blocked (reason in the row) |

## How a step is "done" (same for every phase)

1. **Backend**: `cd server && dotnet build && dotnet test` → all green. Any schema change needs an EF migration: `cd server/src/Rahoon.Api && dotnet ef migrations add <Name>`.
2. **Frontend**: `cd web && npx next typegen && npx tsc --noEmit && npx eslint . && npm run build` → all green.
3. **Browser check** at 1440, 768 and 390 widths against the design (`design-source/*.dc.html`, specs in `docs/design-specs/`).
4. **E2E**: `cd web && npx playwright test`, with the API and web servers running. Use `E2E_RESET=1` to reseed first.
5. Tick the item in the phase file and commit.

## Local run (unchanged)

```bash
docker compose up -d                   # PostgreSQL on :55432 (.env from .env.example)
bash scripts/dev-api.sh --reset        # build API, migrate + reseed demo data, run on :5080
cd web && npm run dev                  # web on :3000
```

Demo password for every seeded user: `Rahoon-Demo-2026!`. The SMS code is shown on screen (sandbox). The users are listed in each phase file.

## Side branches (git worktrees under `.claude/worktrees/`)

These branches were built in parallel before the switch to sequential phases. Each phase file says which branch to merge in which step.

| Branch | Contents | State |
|---|---|---|
| ~~`worktree-agent-a3939fc4d7a8b5a97`~~ | UI: case tabs L06–L12, comms L22, complaints L23, case audit L24, import L04, cancellation | ✅ **merged into `master`** (Phase 1A step 3, 2026-09-25); worktree and branch removed |
| ~~`worktree-agent-a1bd05103bd12a217`~~ | UI: owner portal D02–D14, agreement view, notifications | ✅ **merged into `master`** (Phase 1A step 3); worktree and branch removed |
| `worktree-agent-a10087449fcbd8672` | UI: L18–L21 at `36d316a` (**cherry-picked into `master` as `ca4aefb`**), plus WIP `05c715b` for S07/S08/S09/S10 and the command palette | L18–L21 ✅ merged · WIP 🟧 kept for Phase 1B |
| `worktree-agent-a1417897205c17188` | UI (B7): settings nav, S05 staff invite (committed), plus WIP `9e280ce` for provider/settings/platform pages | 🟧 |
| `worktree-agent-ae86d4e3d70e6413a` | **Backend** B10/B11: referral, agent portal, reconciliation/closure, integrations, analytics | 🟨 53/54 tests pass (the failure is the known EnsureCreated-only case); **needs a migration when merged** |
