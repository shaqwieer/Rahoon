# Phase 1B: Shared pages + lender operations ⬜

**Start only after Phase 1A (and 1A-2, if Q13 keeps it in the MVP) is done.**

> **Re-planned 2026-09-25** for the individual-first direction (`docs/product/product-direction.md`):
> - **S01 moved into the MVP** as the owner-first landing (B13), replacing the old institution-facing S01.
> - S02 stays here as a **secondary** page reached from «للجهات الممولة».
> - This phase now also carries the rework of the **secondary entry routes**: lender invitation D01, manual case L03 and import L04.

**Goal:** complete the everyday experience around the MVP:
- account, search and help pages
- the institutions' secondary entry
- bulk import limited to migration
- the full complaints and objections process, including objecting to a declined request
- the manual judicial-referral readiness package (L25)

There's no live court API; the manual and unavailable states stay.

## Definition of done

- [ ] Every row below is ✅ with *Direction review* OK or Secondary.
- [ ] Web typecheck, lint and build and `dotnet test` are green.
- [ ] Playwright covers:
  - a demo request from «للجهات الممولة»
  - session revoke with step-up
  - an individual's complaint, and an objection to a declined request, through to the reviewer's decision
  - a manual case created with a mandatory reason
  - a CSV import that sends no contact to the owner

## Demo cast

| Who | Login | Does |
|---|---|---|
| Individual from Phase 1A | own sign-in | files a complaint (D13) or an objection to a decline (OA08 → «الاعتراض على الرد») |
| هند المطيري (compliance) | h.almutairi@alufuq.example | reviews complaints (L23) |
| Rahoon team reviewer | depends on Q3 | reviews objections to a lender's decline («يراجعه فريق رهون»). **OPEN (Q3)**: no platform role exists for this yet |
| سارة القحطاني | s.alqahtani@alufuq.example | creates a manual case as an exception (L03 + reason), runs an import (L04) |
| ماجد الحربي (legal) | m.alharbi@alufuq.example | L25 referral readiness |
| Institution visitor | public | demo request (S02) |

## Scope and status

| ID | Screen | Tech status | Direction review | Where / notes |
|---|---|---|---|---|
| S01 | ~~Public landing~~ | — | **Moved to 1A** | Replaced by the B13 owner-first landing |
| S02 | Demo request for institutions | ⬜ UI · 🟩 API | Secondary | Reached from «للجهات الممولة»; API `POST /api/public/demo-requests` (lookups at `/api/public/lookups/demo-request`; honeypot field `website`) |
| S07 | Profile, security and sessions (revoke with step-up) | 🟧 WIP `…a1008744…` @ `05c715b` | OK | Needed by staff and individuals alike |
| S08 | Notifications | 🟧 WIP (same) | OK | Moves to 1A-2 if finished there |
| S09 | Tasks | 🟧 WIP (same) | OK (staff) | |
| S10 | Global search + live command palette | 🟧 WIP (same) | OK (staff) | |
| S11 | Help and support | ⬜ | Rework | Content must answer the individual's question first, then staff help |
| D01 | Lender invites the owner first | 🟩 | **Secondary, rework** | Reword it as a secondary route; the invited individual ends up with the same account type as a self-registered one (Q14/Q15) |
| L03 | Manual case wizard | ✅ | **Secondary, rework** | Add a mandatory reason («استثناء بسبب إلزامي — مثل عميل حضر للفرع») and audit it |
| L04 | Bulk CSV import | 🟨 branch `…a3939fc4…` (merged in 1A step 2) | **Secondary, rework** | «للترحيل فقط؛ لا تواصل مع المالك قبل دعوته»: enforce no owner contact until invited |
| L23 + D13 | Complaints (reviewer) + owner complaints | 🟨 merged in 1A step 2 | OK | Add objections to a declined request (Q3: who reviews) |
| L25 | Manual judicial-referral readiness package | ⬜ UI · 🟨 API | Review | Arrives with the B10 backend merge (1A-2 step 2). Confirm it stays in scope for an individual-first product (Q11/Q8) |
| C01 | Cancellation request + approver decision | 🟨 merged in 1A step 2 | OK | |

## Steps

1. ⬜ Bring in the WIP commit `05c715b` (S07–S10). Typecheck, lint and build; finish what's missing; browser check.
2. ⬜ Build S02, the secondary institutions page and form, against the exact API contract. Then S11 help, individual-first.
3. ⬜ Rework the secondary routes:
   - D01 wording and account type
   - L03 mandatory reason + audit
   - L04 no-contact rule
4. ⬜ Complaints and objections:
   - individual D13 → هند's decision → the individual sees the response; the SLA pause is visible
   - objection to a declined request, with the reviewer per Q3
5. ⬜ L25 referral readiness UI, if still in scope:
   - legal requests the referral; a different approver decides with step-up
   - the external reference is stored exactly as entered
   - the individual gets a notice
6. ⬜ Playwright + responsive QA; update the implementation map.

## Findings / open decisions

- Audit export is open to anyone with `audit.view` (there's no `audit.export` permission). Decide whether to add one.
- Q3 (Rahoon pre-screening and review of objections) may need a new platform role. Coordinate with Phase 1C.
