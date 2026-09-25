# Design conflicts, reconciliations and open questions

The Arabic product specification referenced by the brief (`Pasted markdown(20260923-180332).md`) was **not
attached** to the design project (00 Brief, assumption A-01), and no PRD exists in this repository. The
screen inventory therefore follows the design's own traceability matrix (111 screens), and domain rules
follow the Blueprint, Handoff and per-screen spec notes. Items below are resolved in code as stated;
items marked **OPEN** need a product/legal decision. Per-batch conflict lists with more detail are at the
end of each `docs/design-specs/*.md`.

## Resolved in implementation

| # | Topic | Sources in conflict | Decision |
|---|---|---|---|
| 1 | Hijri date for 2026-09-23 | working-notes «11 ربيع الآخر 1448هـ» vs Umm al-Qura calendar (12) | Compute with the official Umm al-Qura calendar (.NET `UmAlQuraCalendar`, browser `islamic-umalqura`); design samples are illustrative. |
| 2 | Case visibility of a case manager | L02 «كل الحالات 1,248» for سارة vs «ممنوع لحالة من فريق آخر» | Case managers, legal, finance, approvers see all institution cases (`case.view_all`); analysts and case officers are team/assignment scoped and get the forbidden state for other teams. |
| 3 | Installment rounding | 84 × 15,074.52 = 1,266,259.68 (0.32 short) | Final installment absorbs the remainder (15,074.84) so Σ = rescheduled amount; asserted in tests. |
| 4 | Profit on rescheduled amount | design shows none | No profit component (installment = rescheduled ÷ term). **OPEN** for product confirmation. |
| 5 | Approval amount basis | «المبلغ ≤ 2M» ambiguous | Uses outstanding at preparation. **OPEN** (could be rescheduled amount). |
| 6 | Apology for a counteroffer | L18 «يعيد الحالة إلى حل مقترح» vs Blueprint «رفض ← حل مقترح أو تفاوض» | Negotiation → proposed solution; never referral. |
| 7 | Reject decision target state | L16 does not specify | Rejected version → case back to «حل مقترح» (same as return), no owner contact. |
| 8 | SLA for «حل مقترح» / «تفاوض» | A06 lists 6 stages only; case header shows a proposed-solution deadline | 5 business days each, labelled «افتراض — يتطلب تأكيد المنتج» in the SLA table. |
| 9 | Complaint reviewer name | L23 «هند المطيري» | Seeded compliance reviewer is هند المطيري. |
| 10 | Owner debt breakdown | D05 (1,121,840 + 144,420 + 18,300) vs earlier assumption | Seed follows D05; total 1,284,560.00 unchanged. |
| 11 | Returning owner sign-in | not designed (only first invitation) | The invitation link remains the entry point; «مستخدمة» state continues to the same last-4 + OTP verification. **Superseded 2026-09-25** by self-registration (B13 OR01/OR02); returning sign-in is open (Q15). |
| 12 | Owner MFA factor | S04 SMS default vs S07 authenticator primary | SMS OTP (sandbox) for all; authenticator app enrollment is **OPEN** (not implemented). |
| 13 | Agreement PDF | «النص الكامل PDF» | Printable HTML view (browser print to PDF) — Arabic PDF rendering server-side is outstanding. |
| 14 | Product type / lender / manager hard-coded in CaseHeader DC | CaseHeader.dc.html | Made data-driven props. |
| 15 | App pages have no h1 | B2 screens | Page title rendered as h1 for accessibility. |

## Product direction correction (2026-09-25)

The individual-first direction and its open decisions are recorded in `docs/product/product-direction.md`. Conflicts found inside the B13 design itself (to resolve at implementation, not guessed):

| # | Topic | Conflict | Handling |
|---|---|---|---|
| 16 | Lender response deadline | State model: «مهلة رد الجهة 5 أيام عمل» (افتراض) vs L00b sample «الرد خلال 3 أيام عمل» | **Resolved (Q6):** no deadline is shown to the individual; processing times are tracked internally only. |
| 17 | «مجاني» on OR01 | OR01 sub-title «مجاني · دقيقتان» vs landing «أزلنا "مجاناً" … حتى يتأكد نموذج التسعير» (Q9) | Don't render «مجاني» until **Q9** is answered. |
| 18 | OTP attempts at registration | OR02 «5 محاولات ثم قفل مؤقت» vs implemented staff policy (3 wrong codes → 15-min lock) | Decide at Phase 1A step 3; record here. |
| 19 | Lender default landing | Phase 0 anchor: portfolio (L01) vs LenderSidebar 2026-09-25: «الطلبات الواردة» first and default | **Resolved (Q1):** in the MVP the Rahoon team coordinates manually, and lender screens aren't an entry point; the intake sidebar item belongs to the later lender-on-platform mode. |

## Open questions (need product / legal confirmation)

* Legal effect of the in-platform consent record (A-05) and licensed e-signature provider (X01).
* Payment gateway/escrow is out of scope (A-04); manual recording + maker-checker only.
* Official judicial channel (A-06): referral stays manual with verbatim external status.
* National digital identity provider (A-07): placeholder shown disabled.
* Retention periods (A-12) — seeded as «pending legal».
* Approval-limit and SLA values (A-08/A-09) are examples, configurable per institution.
* Provider access lifetime after delivery (A-11: +7 days read-only) — implemented as stated.
