# رهون — roadmap after Phase 1

Rahoon is a Saudi exit/buy platform ([product definition](../../product/product-definition.md)). Phases 0–1 (Phase M0+M1
in [`docs/phases/`](../../phases/README.md)) are done. The next phases come from the owner's phase pack
(`Rahoon_Next_Phases/`, one brief per phase). **One phase per session, in order**; each session ends with a handoff.

| Order | Phase | Outcome | Prerequisite | Plan | Status |
|---|---|---|---|---|---|
| 1 | 1.5 | Team membership, invitations, custom roles, permission catalog, effective access, resource scopes | Phase 1 | [phase-1.5-admin-access.md](phase-1.5-admin-access.md) | ✅ Completed 2026-10-01 — [handoff](handoffs/phase-1.5-handoff.md) (not deployed) |
| 2 | 2 | Search & map discovery, explained matching, comparison, saved searches/alerts, calculators | 1.5 | brief `Rahoon_Phase_2_Discovery_Matching.md` → plan written by its session | ▶ **NEXT** |
| 3 | 3A | Versioned offers, negotiation, owner acceptance, exclusive provisional reservation | 2 | brief `Rahoon_Phase_3A_Offers_Reservations.md` | ⬜ |
| 4 | 3B | Closing cases, external approvals, payment evidence, transfer confirmation | 3A | brief `Rahoon_Phase_3B_Transfer_Completion.md` | ⬜ |
| 5 | 4 | Dashboards, assignments/reminders, versioned settings, directory import, support, reports | 3B | brief `Rahoon_Phase_4_Operations_Reports.md` | ⬜ |
| 6 | 5 | Hardening and launch-readiness evidence | 4 | brief `Rahoon_Phase_5_Launch_Readiness.md` | ⬜ |

- Decisions: [decisions.md](decisions.md). Handoffs: [handoffs/](handoffs/README.md).
- Permission keys for 3A/3B/4 already exist in the catalog as **reserved** (not grantable). The phase that builds the
  action makes its key grantable and adds it to the role matrix.
- Status changes only from evidence. A partial feature or untested invariant stays visible here.

## Starting the next session

> Read the repository instructions, `docs/rahoon/roadmap/README.md`, `decisions.md` and the prerequisite handoffs
> (`handoffs/phase-1.5-handoff.md`). Read the Phase 2 brief `Rahoon_Next_Phases/Rahoon_Phase_2_Discovery_Matching.md`
> and write its plan as `docs/rahoon/roadmap/phase-2-discovery-matching.md`. Verify prerequisites from actual code,
> implement ONLY Phase 2, complete its acceptance checks, write its handoff and update roadmap status. Do not start the
> next phase.
