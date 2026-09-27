# ADR 0002 — Tracking the execution of an accepted offer (manual-coordination mode)

- **Status:** Accepted for implementation in Phase 1A-2 (steps 1–4). Items marked *interim* follow an open product question (Q16–Q18) and are reversible.
- **Date:** 2026-09-27
- **Decided by:** engineering, within the product decisions in `docs/product/product-direction.md` (Q1, Q6, Q8, Q12, Q13) and ADR 0001. Product questions aren't decided here.
- **Extends:** [ADR 0001](0001-request-ownership.md) §4.4–§4.5 (amendment row added there).

## 1. Context

- Phase 1A ends with the individual's response recorded and relayed. The team then closes the request with an outcome (`offer_accepted`, …). Today an accepted offer is closed immediately, with the summary «تنفيذ الاتفاق يتم مع جهتك الممولة».
- Q13 puts the execution of an accepted offer in Phase 1A-2: the agreement, the installments, payment confirmations, a breach and closure. Rahoon **tracks and explains**. It **doesn't hold funds or execute payments** (A-04: payments happen outside the platform).
- Q1/Q8: the lender isn't on the platform. Everything Rahoon knows about execution comes from the lender over the documented manual channel, or from the individual.
- The existing execution code (L19–L21 agreement/payments, B10 reconciliation and closure, the owner portal D10/D14) is bound to the **lender-tenant case** and the **owner-case session** (`RequestContext.OwnerCaseId`, `/api/owner/*`). X4 superseded that session for the primary journey.

## 2. Decision drivers

1. **Evidence, not assertion.** Rahoon never marks money as received on its own authority. Every execution fact shown to the individual comes from a lender document (source + date), checked by a second team member, or is clearly labelled as the individual's own report.
2. **Same controls as offers.** The offer pattern (T05/T06) already enforces a lender source document, verifier ≠ recorder (code and DB check), audited refusals and step-up. It's proven; reuse it.
3. **No deadlines or promises (Q6/Q12).** Due dates exist only as the lender's stated schedule, labelled with the source. No reminders styled as deadlines.
4. **No automatic action against the individual.** A breach reported by the lender is recorded and explained. Rahoon takes no action and triggers none.
5. **One request, one history (Q14).** Execution stays on the same request, visible under `/my/requests/[ref]`, on the individual's own account.

## 3. Options considered

| Option | Verdict |
|---|---|
| A. Keep closing on acceptance; add a post-closure «execution» section | Rejected: a closed request can't show «ننتظر» or a next step, and the team's queue would lose it |
| B. Convert the request into a lender-tenant case and reuse L19–L21 | Rejected for manual mode: the lender isn't a tenant, and a case needs lender-side users (maker-checker) who don't exist. Kept for the lender-on-platform mode, with new consent (ADR 0001 §7) |
| **C. A new request state `execution_tracking` with lender-evidenced child records** | **Chosen** |

## 4. Decision

### 4.1 Lifecycle additions

| Key | From → To | Actor | Guards / notes |
|---|---|---|---|
| `start_execution_tracking` | response_recorded → **execution_tracking** | coordinator (`request.execution_record`) | the latest response is an **accept** on the published offer; the response is relayed (`response_relayed`); consent active. The offer path is P1 or P2 (P3 sale execution is Phase 2; until then an accepted P3 offer is closed with `offer_accepted` as today) |
| `close` (extended) | + execution_tracking → closed | coordinator (`request.close`) | new outcome codes below; reason/summary required as today |
| `continue_coordination` (extended, **interim Q17**) | + execution_tracking → lender_coordination | coordinator | reason required and shown to the individual; for a new arrangement with the lender after a breach or a change the lender reports. The execution records stay on the request |
| `withdraw` | unchanged (any non-terminal) | applicant | withdrawal during tracking only stops Rahoon's tracking and sharing. The confirmation must say that the agreement with the lender isn't affected by the withdrawal (proposed text, V4) |

- **Status label** (proposed, D-8): «قيد متابعة التنفيذ».
- **«ننتظر»** during tracking is set by the team on each update: `lender` (default: waiting for the lender's confirmation or documents) or `applicant` (for example, a proof they offered to send). It never says the individual is awaited for a payment. Paying is between the individual and the lender, and Rahoon doesn't chase it.
- **New close outcome codes:**
  - `executed_closed`: the lender confirmed completion. Guard: at least one **verified closure document** (§4.2).
  - `tracking_ended`: Rahoon stops tracking while the agreement continues with the lender (interim Q16).
  - `agreement_ended_by_lender`: the lender reports that the agreement ended without completion. Summary required. No action follows from Rahoon.
  - Existing codes stay.

### 4.2 Data model (schema `requests`, all rows `IApplicantOwned` per ADR 0001 amendment)

| Entity | Purpose | Key fields |
|---|---|---|
| `RequestExecutionRecord` | One fact from the lender, recorded by the team and verified by a second member | `Kind`: `agreement` · `payment_confirmation` · `lender_notice` (breach or change) · `closure_document`; `Status`: PendingVerification / Returned / Published / Superseded (same as offers); `SourceDocumentId` (**required**, request document from the lender); `LenderReference`; `LenderDate`; `SummaryText` (what the lender stated); `ExplanationText` (plain language for the individual, «ماذا يعني لك»); recorder / verifier / checklist / return reason / published time (DB check verifier ≠ recorder); `ShareSourceWithApplicant` (default true, as offers) |
| — `agreement` extras | The agreement *as the lender stated it* | link to the accepted `OfferId`; activation date as stated; path terms copied from the offer and editable to what the lender's letter says; the difference from the offer is flagged to the verifier |
| `RequestScheduleItem` | The lender's stated installment schedule (child of a published `agreement` record) | no, due date, amount. Shown only as «بحسب جدول جهتك الممولة» with the source; replaced as a whole by a newer agreement record |
| — `payment_confirmation` extras | The lender confirms a payment it received | amount, date received per the lender, optional schedule item no; optional link to the individual's report it answers |
| `RequestPaymentReport` | The individual reports a payment they made to the lender, with proof | amount, transfer date, bank reference (optional), proof document (required); `Status`: `reported` → `confirmed_by_lender` (linked to a published `payment_confirmation`) / `not_confirmed_yet` (the team's plain note; never «rejected»). **Never becomes confirmed without a published lender record** |
| — `lender_notice` extras | A breach or change reported by the lender | category (missed installment · agreement change · other); the individual sees the lender's statement, the explanation and «لم تتخذ رهون أي إجراء» wording (proposed, V4) |
| — `closure_document` extras | A closure letter from the lender | document kind (interim Q18): «مخالصة / إخلاء طرف», «خطاب فك الرهن», «خطاب إتمام الجدولة أو الاتفاق», «أخرى». Stays on the individual's account with no expiry (Q14; supersedes D14's 90-day window for this mode) |

Idempotency on every POST as elsewhere. Execution records are append-only. A correction is a new record that supersedes the old one, with a reason.

### 4.3 Permissions

- New: `request.execution_record` (coordinator, lead) and `request.execution_verify` (verifier, lead). Verification requires MFA step-up, as `publish_offer` does.
- Verifier ≠ recorder is enforced in code (refusal audited, even for the lead) and by a DB check constraint.
- A verifier sees a request only while an execution record awaits their check (ADR 0001 amendment, step 6).

### 4.4 What the individual sees (DTO rules)

- Only **published** execution records, the schedule of the current published agreement, and their own payment reports.
- Never pending or returned records, internal notes, recorder or verifier identity beyond «فريق رهون», or team-only documents.
- «رهون لا تستلم أي مبالغ. الدفع يتم لجهتك الممولة مباشرة» on every execution view (reused from D10).
- No countdowns, overdue styling driven by Rahoon, or reminder notifications (Q6/Q12). A due date shows as the lender's schedule. A missed installment appears only when the lender reports it (`lender_notice`).

### 4.5 Audit

Every record, verification, return, publication, payment report, transition and blocked attempt is audited in the existing hash chain (format 2, subject `request`).

### 4.6 Reuse decisions

| Existing piece | Mode | Decision |
|---|---|---|
| Offer record/verify pattern (T05/T06, `/team/verify`, checklist, step-up, DB check) | manual | **Reuse the pattern and the verify screen**; generalize the queue to offers + execution records |
| D10 owner payments (`/owner/payments`, `PaymentsClient`) | lender-on-platform | **Components only** (`InstallmentList`, `HowToPay` note, «رهون لا تستلم…» block, payment-notice form) behind a request data adapter; the route stays case-bound |
| D14 owner closure documents (`/owner/documents/closure`) | lender-on-platform | **Components only** (closed state, document list); its 90-day read-only window doesn't apply to the individual account |
| L19 agreement, L20 payments maker-checker, L21 breach | lender-on-platform | **Not reused** in manual mode (they model the lender's internal work). Kept and re-verified in the later lender-mode steps |
| L26 reconciliation + closure UI, B10 closure API (branch `…ae86d4e3…`) | lender-on-platform | Later steps of this phase; case-bound |
| S08 notifications, S09 tasks (WIP `05c715b`) | both | Not needed for manual mode; stay with Phase 1B |
| `RequestDocument` storage and scanning | manual | **Reuse**: lender letters and payment proofs are request documents (new kinds «خطاب/إشعار من الجهة الممولة», «إثبات سداد») |

## 5. Consequences

- **Positive:** the individual keeps one request with its whole history, from submission to closure. The team works in one workspace. The controls are already tested.
- **Negative / cost:**
  - New state and outcome codes: the team close dialog, the tracker, seed REQ-2026-00306 and the step-7 E2E close path change (an accepted P1/P2 offer now goes to tracking before closure).
  - One more verify queue for the verifier role.
- **Risks to watch:**
  - A schedule shown with dates can read as a Rahoon deadline. The wording and the source label are part of the design review (D-8).
  - A report that stays «not confirmed yet» for long may worry the individual. The plain note and the concern path (P4) are the answer, not a timer.

## 6. Tests required (Phase 1A-2 steps 1–4)

1. `start_execution_tracking` is refused without an accepted, relayed response, or for a P3 offer.
2. An execution record without a lender source document is refused. Verifier = recorder is refused (code and DB), and the refusal is audited.
3. Individual DTOs contain only published records, their schedule and their own reports. Pending or returned records and internal notes never appear (assert on raw JSON, `TestClient.Raw`).
4. A payment report never shows as confirmed until a published `payment_confirmation` links it.
5. `close` with `executed_closed` is refused without a verified closure document.
6. Another individual gets 404 on every execution endpoint; lender-tenant users and platform admins get nothing.
7. Idempotent record, verify, report and close; a replay returns the same response.
8. Audit chain verification passes with the new events.

## 7. Open items linked to this ADR

| Item | Interim rule |
|---|---|
| Q16 tracking horizon | The team records what the lender provides and what the individual reports; no obligation to record every installment. `tracking_ended` closes tracking with a summary while the agreement continues |
| Q17 after a breach | `continue_coordination` from `execution_tracking` on the same request, with a reason shown to the individual |
| Q18 closure documents per path | The four document kinds in §4.2; `executed_closed` needs at least one |
| V4 wording | Withdrawal-during-tracking text, «لم تتخذ رهون أي إجراء» on a lender notice, the payment-report confirmation: proposed text, labelled |
| Reminders / SMS | None. Only in-app updates on the tracker, as in Phase 1A (step 6 finding) |
