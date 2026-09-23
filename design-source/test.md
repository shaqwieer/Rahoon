# Working notes (design file conventions)

## Files
- 00 Brief & Assumptions.dc.html — matrix; JS `designed = batch === 'B1' || batch === 'B2'` + BATCHES state ('done'/'next'/'todo'); header nav links.
- 01 Foundations, 02 Components, 03 Phase 0 — Blueprint, 03 Phase 0 — Anchor Screens (A) / B, 04 Phase 1 — B2 Shared & Public.
- Child DCs: LenderSidebar (props active: portfolio|cases|tasks|approvals|complaints|reports|none, userName, userRole, initials, approvals badge), LenderTopbar (crumb1, crumb2).
- assets/: rahoon-horizontal-full.svg, -dark.svg, -mono-white.svg, stacked-full, symbol-full, app-icon, favicon. Never CSS-filter logos; on dark use -dark or app-icon.

- CaseHeader child DC: props tab (overview|parties|finance|property|documents|valuation|solutions|payments|comms|audit + extraTab "key:label"), state-key (awaiting|verification|valuation|proposed|approval|customer|negotiation|settlement|sale|referral|external|reconciliation|closed|paused), sla-tone (ok|warn|err|info|none), sla-text, stage (0-6), stage-names ("a|b|.."), case-ref, title. hint-size 100%,210px.
- Done: B3 = 04 Phase 1 — B3 Case Tabs.dc.html; B4 = 04 Phase 1 — B4 Solutions & Agreement.dc.html (links to B5 file name '04 Phase 1 — B5 Comms, Referral & Closure.dc.html').
- B4 story: owner counteroffer → v3 (day 10, start 2026-12-10, end 2033-11-10), accepted 2026-10-02 14:21 (AGR-2026-004172-01). Inst.1 paid TRX-88392214 matched; inst.2 TRX-88410027 pending match. Breach example on RH-2026-003870 ماجد ت. (inst 3 & 4 missed, cure until 2026-10-04).

- Done B5 (04 Phase 1 — B5 Comms, Referral & Closure.dc.html), B6 (04 Phase 1 — B6 Debtor Journey.dc.html; links to '04 Phase 1 — B7 Provider & Admin.dc.html'). Child DCs DebtorTop (title, sub, back bool, unread) + DebtorNav (active home|docs|options|payments|help). Home icon = space_dashboard (no roof icons).
- Remaining: B7 provider+inst admin+platform admin; B8 voluntary sale (05 Phase 2); B9 provider ecosystem/workflow/reporting/billing/conditional sign+pay. Then update 00 matrix designed set + BATCHES to B9 done, B10 next.

- Done B7 (04 Phase 1 — B7 Provider & Admin.dc.html; links to '05 Phase 2 — B8 Voluntary Sale.dc.html'). Child DCs PlatformSidebar (active ops|inst|users|cases|defaults|providers|complaints|audit|privacy|workflow|billing|integrations, role) + SettingsNav (active org|users|docs|limits|templates|sla|providers|reports). Provider: مكتب تقييم معتمد «ب» (عمر العنزي), inst admin ليلى الغامدي.

- Done B8, B9, B10 (06 Phase 3 — B10 Judicial & Financial Integration.dc.html; links '07 Phase 4 — B11 Optimization.dc.html'). Referral case RH-2026-003511 فيصل ر. جدة: sale 760,000, costs 22,800, lender 684,200, surplus 53,000. 00 matrix: designed list array in JS (add 'B10','B11'), BATCHES rows 'B10','next' / 'B11','todo' / 'B12','todo'.

## Rules learned
- NO sc-for inside <table>/<tbody>/<tr> (parser hoists) — use div grids with role=table/row/cell.
- Desktop artboards: prefer min-height; if fixed height, verify content fits. Grids: minmax(0,1fr).
- Canvas pages: helmet meta design_doc_mode canvas; body bg #E7E6E1; artboard label (IBM Plex Mono 14 600) above; spec aside 380px via sc-for {k,v}.

## Tokens
rust #AA4528 (primary btn, links), hover #8E3920, tint #FDF0EB / border #F0B8A6, orange #F4633A accent only, ink #151513, charcoal #22262A, warm #FAF9F6, subtle #F2F1ED, border #CBCAC6, strong border #85847F, secondary text #5E5D58/#6B6A65, divider #EDECE8.
success #1E6A45/#EAF4EE/#9CCBB0; warning #8A5300/#FBF2DE/#E2C27A; error #B3261E/#FCECEA/#EFA59C; info #1D5A8C/#EAF2F9/#9DC0DE. Inverse #151513, raised #22262A, text-2 #B5B3AD.
Fonts: IBM Plex Sans Arabic / IBM Plex Sans / IBM Plex Mono; icons Material Symbols Rounded class .ms.

## Fictional data (keep consistent)
Case RH-2026-004172 · عبدالله م. · فيلا سكنية، حي النرجس، الرياض · مصرف الأفق · تمويل سكني مرابحة · opened 2026-08-14 · contract MF-88-3317••• · ID 1•••••••42 · phone +966 5• ••• ••81 · deed 3••••••18.
Outstanding 1,284,560.00 SAR (core system 2026-09-22 18:40) · arrears 96,420.00 (7 installments since 2026-02, orig installment 13,774.29) · valuation 1,650,000.00 (مكتب تقييم معتمد «ب», 2026-09-10, valid to 2026-12-09) · LTV 77.9%.
People: سارة القحطاني case manager · فهد العتيبي analyst · نورة الشهري approver (≤2,000,000, waiver ≤5%) · ماجد الحربي legal · ريم الدوسري finance · خالد الزهراني case officer.
Solution v2: reschedule 84 months, installment 15,074.52, waiver 18,300.00 (1.42%), rescheduled 1,266,260.00, first 2026-11-01 (10 جمادى الأولى 1448هـ), last 2033-10-01; net income 32,400.00 (salary stmt v2 verified 2026-09-20); DSR 46.5% vs 55% limit (assumption). v1: 60 months 21,409.33, DSR 66.1%, returned 2026-09-15.
Today 2026-09-23 = 11 ربيع الآخر 1448هـ. Approval sent 2026-09-23 10:12, due 2026-09-26.
Other cases: RH-2026-003988 منيرة ع. جدة (awaiting customer, overdue), RH-2026-004012 عائشة ف. (negotiation), RH-2026-003870 ماجد ت. (active settlement 3/60, 612,900.00), RH-2026-003702 تركي ب. (awaiting reconciliation 455,210.75), RH-2026-004155 سلطان ح. (valuation), RH-2026-004201 شركة ر. للمقاولات.
