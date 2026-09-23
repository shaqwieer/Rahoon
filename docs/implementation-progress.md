# Implementation progress (checkpoint)

_Last updated: 2026-09-24_

## Where things are

| Slice | API | UI | Tests | Notes |
|---|---|---|---|---|
| Foundation: design mirror, specs, tokens, auth, tenancy, audit, workflow, storage, integrations | ✅ | 🔄 design system + shells (agent) | ✅ | `docs/architecture.md` |
| Phase 0 anchors (L01 portfolio, L02 list, L03 wizard, L05 workspace, L13 builder, L15 submit review) | ✅ | ⏳ next | ✅ | API smoke: `scripts/api-smoke.sh` |
| B4 approvals/offers/negotiation/agreement/payments/breach | ✅ | ⏳ | ✅ | |
| B6 owner portal API | ✅ | ⏳ | ✅ | |
| B5 complaints, comms, tasks, notifications, search | ✅ (L22, L23, S08–S10) | ⏳ | ✅ | |
| B3 tabs, L04 import, L24 audit, cancellation | 🔄 agent (worktree) | ⏳ | — | `docs/progress/backend-B3.md` |
| B7 provider portal, institution admin, platform admin, S02/S05 | 🔄 agent (worktree) | ⏳ | — | `docs/progress/backend-B7.md` |
| B8 voluntary sale, B9 ecosystem | 🔄 agent (worktree) | ⏳ | — | `docs/progress/backend-B8-B9.md` |
| L25/L26, B10, B11 | 🔄 agent (worktree) | ⏳ | — | `docs/progress/backend-B10-B11.md` |

## Resume checklist (if interrupted)

1. `docker compose up -d` (PostgreSQL on 55432; `.env` holds the local password), `bash scripts/dev-api.sh --reset`.
2. `cd server && dotnet test` (Testcontainers; Docker must be running).
3. Merge any finished worktree branches (`git worktree list`), then `dotnet ef migrations add <Name>` in
   `server/src/Rahoon.Api` for new entities (worktree agents do not create migrations), re-run tests.
4. Frontend: `cd web && npm run dev` (needs the API on :5080); build Phase 0 screens next, then batch screens.
5. Regenerate the map: `python scripts/gen_impl_map.py` after updating `API_DONE`/`UI_DONE`.
