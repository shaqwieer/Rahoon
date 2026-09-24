# Phase 1B: Shared pages + lender operations ⬜

**Start only after Phase 1A (MVP) is done.**

**Goal:** complete the everyday lender experience around the MVP. That means the public entry pages, the account area, search and help, bulk import, the full complaints process, and the manual judicial-referral readiness package (L25). No live court API: the manual/unavailable states stay.

## Definition of done

- [ ] Every row below is ✅.
- [ ] Web typecheck, lint and build are green; `dotnet test` is green.
- [ ] Playwright covers: demo request, session revoke with step-up, a complaint from the owner to a reviewer decision, and a CSV import commit.

## Scope and status

| ID | Screen | Status | Where / notes |
|---|---|---|---|
| S01 | Public landing (indexable, SSR) | ⬜ | spec `B2-shared-public.md` |
| S02 | Demo request form | ⬜ UI · 🟩 API | `POST /api/public/demo-requests`, lookups at `/api/public/lookups/demo-request`; honeypot field `website` |
| S07 | Profile, security and sessions (revoke with step-up) | 🟧 WIP | branch `…a1008744…` @ `05c715b` |
| S08 | Notifications center | 🟧 WIP | same branch (moves to 1A if finished there) |
| S09 | My tasks / team tasks | 🟧 WIP | same branch |
| S10 | Global search + live command palette | 🟧 WIP | same branch |
| S11 | Help and support | ⬜ | |
| L04 | Bulk CSV import | 🟨 | branch `…a3939fc4…` (merged in 1A step 1) → browser check here |
| L23 | Complaints, full reviewer flow + owner D13 | 🟨 | merged in 1A step 1 → end-to-end check here |
| L25 | Manual judicial-referral readiness package | ⬜ UI · 🟨 API | API arrives with the B10 backend merge (1A step 4) |
| C01 | Cancellation request + approver decision | 🟨 | merged in 1A step 1 → browser check here |

## Steps

1. ⬜ Rebase or merge the WIP commit `05c715b` (S07–S10). Typecheck, lint and build; finish what's missing; browser check.
2. ⬜ S01 landing + S02 demo form (build against the exact API contract above) + S11 help.
3. ⬜ L04 import and C01 cancellation in the browser; fix defects.
4. ⬜ L23 complaints: owner D13 → reviewer (هند المطيري, h.almutairi@alufuq.example) decision → owner sees the response; SLA pause is visible.
5. ⬜ L25 referral readiness UI (legal requests, a different approver decides with step-up, external reference stored verbatim, owner notice).
6. ⬜ Playwright specs + responsive QA; update the implementation map.

## Findings / open decisions

- Audit export is currently open to anyone with `audit.view` (there is no `audit.export` permission). Decide whether to add one.
