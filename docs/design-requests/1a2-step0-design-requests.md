# Design requests D-7 and D-8 (Phase 1A-2, step 0)

_Prepared 2026-09-27 for the product owner or designer, to be added to the design project. Basis: `docs/product/product-direction.md` (Q1, Q6, Q8, Q12, Q13; open Q16–Q18) and `docs/adr/0002-request-execution-tracking.md`._

The reading rules and **global rules** of [`1a-step2-design-requests.md`](1a-step2-design-requests.md) apply unchanged: no deadline, no promise, «ننتظر» + next step on every state, nothing implying representation (V1), «منصة رهون» only. Proposed text is marked «نص مقترح — يحتاج اعتماد صاحب المشروع», and legal wording «يحتاج مراجعة نظامية». **Nothing here is approved copy.**

**Extra rules for execution (from the decisions)**
- Rahoon **tracks and explains**; the lender executes (Q13, A-04). Every screen shows «رهون لا تستلم أي مبالغ. الدفع يتم لجهتك الممولة مباشرة».
- Every execution fact names its **source**: «بحسب خطاب جهتك الممولة بتاريخ …» or «أبلغتَنا به أنت».
- A due date is the **lender's schedule**, never a Rahoon deadline: no countdowns, no «متأخر» badge unless the lender reported it, no reminders.
- A lender-reported breach is **explained**, with no blame and no action from Rahoon.

---

## D-7 Rahoon team: execution tracking (extends D-4; no frames exist)

- **Why:** Q13 + ADR 0002. After the individual accepts a P1/P2 offer and the team relays it, the team records what the lender sends (agreement, schedule, payment confirmations, notices, closure letters). A second member verifies each record before the individual sees it.
- **Shell:** the D-4 «فريق رهون» workspace at 1440. «بحاجة إلى تحقق» now lists offers **and** execution records.
- **Frames:**

  | Frame | Content |
  |---|---|
  | **T02 (addition) «التنفيذ» tab** | Shown from `execution_tracking` on. Sections: الاتفاق (current, superseded) · جدول الأقساط كما ذكرته الجهة · تأكيدات السداد · بلاغات العميل عن السداد · إشعارات الجهة · مستندات الإغلاق. Per record: status (بانتظار التحقق / أُعيد / منشور / مستبدل), source letter, recorder, verifier. Actions: «بدء متابعة التنفيذ» (from T07 after relaying an acceptance, disabled with the reason otherwise), «تسجيل من الجهة…» (menu by kind), «إغلاق الطلب…» (the new outcomes), «متابعة التنسيق مع الجهة…» (reason shown to the individual, Q17 interim). «ما سيراه العميل» preview |
  | **T09 تسجيل الاتفاق** | Lender letter (required), activation date as stated, path terms prefilled from the accepted offer and editable to the letter, with differences highlighted; the schedule (rows: no · due date · amount; paste or add rows; optional, Q16); «ماذا يعني لك»; «إرسال للتحقق» |
  | **T10 تسجيل تأكيد سداد / إشعار / مستند إغلاق** (drawer by kind) | **Payment confirmation:** amount, date received per the lender, schedule row (optional), «ردًا على بلاغ العميل» link (optional). **Lender notice:** category (قسط لم يُسدَّد بحسب الجهة · تعديل على الاتفاق · أخرى), what the lender stated, the explanation for the individual. **Closure document:** kind (مخالصة / إخلاء طرف · خطاب فك الرهن · خطاب إتمام الجدولة أو الاتفاق · أخرى; Q18). Every kind: source document (required), lender reference and date, «ماذا يعني لك», preview |
  | **T06 (extension) التحقق من سجل تنفيذ** | Same ReviewScreen pattern and step-up as offers: source beside the values, a checklist per kind, «اعتماد ونشر للعميل» / «إعادة مع سبب». The recorder can't verify |
  | **T11 بلاغ سداد من العميل** | The individual's report (amount, date, reference, proof). Actions: «ربطه بتأكيد من الجهة» (select a published confirmation) or «ملاحظة للعميل» (plain «لم تؤكده جهتك بعد», never «مرفوض») |

- **States:** tracking not startable (reason: no accepted response, not relayed, P3 path); record returned by the verifier; a newer agreement supersedes the schedule; close refused with `executed_closed` and no verified closure document.
- **Acceptance:** the T02 tab, T09, T10 (three kinds), the T06 variant and T11 at 1440; the T02 tab at 768.

## D-8 The individual: following execution (D10 and D14 successors under `/my/requests/[ref]`)

- **Why:** Q13, Q14 (same request, same account), Q6/Q12. Today D10/D14 exist only for the lender-invited owner (case session). Their components are reused; the layout follows the request tracker (D-3).
- **Frames (390 first):**

  | Frame | Content |
  |---|---|
  | **E01 Tracker in tracking** | Status «قيد متابعة التنفيذ» (نص مقترح), «ننتظر: جهتك الممولة» or «أنت», next step. Cards: «اتفاقك مع جهتك» (summary, activation date as stated, letter) · «جدول الأقساط بحسب جهتك» · «آخر ما أكدته جهتك» · «مستندات الإغلاق» (when present). The «رهون لا تستلم أي مبالغ» block |
  | **E02 الاتفاق والجدول** | Agreement as the lender stated it, «ماذا يعني لك», source letter; the schedule as rows with each state: **أكدته جهتك** (with the confirmation date) · **أبلغتَنا بسداده، بانتظار تأكيد جهتك** · no state (no «متأخر» unless a lender notice says so). Source label on the table |
  | **E03 أبلغنا عن سداد** | Amount, transfer date, bank reference (optional), proof (required). Confirmation: «سجّلنا بلاغك. سنطلب من جهتك الممولة تأكيده، ونعرض التأكيد هنا» (نص مقترح). No time promise |
  | **E04 إشعار من جهتك** | What the lender stated, «ماذا يعني لك», «لم تتخذ رهون أي إجراء بخصوص تمويلك» (نص مقترح — يحتاج مراجعة نظامية), options: send a message to the team, object (P4) |
  | **E05 مستندات الإغلاق** (D14 successor) | Closed with `executed_closed`: the documents list with kinds and dates, download; stays on the account (no 90-day window). Other outcomes (`tracking_ended`, `agreement_ended_by_lender`): the summary and what they can do next |
  | **E06 سحب الطلب أثناء المتابعة** | Explains that withdrawal stops Rahoon's tracking and sharing only, and that the agreement with the lender continues (نص مقترح — يحتاج مراجعة نظامية) |

- **Tone:** plain, factual, no blame. Money amounts use the existing `Amount` component; dates are isolated in Arabic text.
- **Acceptance:** E01–E06 at 390; E01 and E02 at 768 and 1440.

---

## Status

| Request | Status | Needed by |
|---|---|---|
| D-7 | ⬜ | Phase 1A-2 step 2 |
| D-8 | ⬜ | Phase 1A-2 step 3 |

If a request isn't designed by the step that needs it, engineering builds it with the existing design system and records «بانتظار اعتماد التصميم» in the phase findings (same rule as Phase 1A).
