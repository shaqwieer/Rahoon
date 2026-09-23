# Case-transition matrix

Source of truth in code: `server/src/Rahoon.Api/Modules/Cases/CaseWorkflow.cs` (`Transitions`).
Design sources: 03 Phase 0 — Blueprint (state map + «جدول الانتقالات والحواجز»), 09 Handoff (CaseState enum,
transition excerpt), B4/B5/B8/B10 specs.

Every transition goes through `CaseWorkflow.TransitionAsync`, which checks, in order:
1. **Expected status** sent by the client (stale view → `409 stale_state`).
2. **Allowed source state** (else `409 transition_not_allowed`, attempt audited as blocked).
3. **Actor permission** (else `403`, audited as blocked) — skipped only for `systemInitiated` domain events
   (owner response, payment completion) whose own endpoints already authorize the actor.
4. **Reason** when required (`400`).
5. **MFA step-up** when required (`403 step_up_required`).
6. **Guards** (named, evaluated server-side; failures returned as Arabic `reasons`, `422 guard_failed`, audited as blocked).
Then: status + timestamp + SLA due date (business days from the institution's SLA rule) + hash-chained audit event
(from → to, actor, role, reason, evidence). Mutations run inside a DB transaction with `Idempotency-Key` replay
protection and `xmin` optimistic concurrency.

| Key | From | To | Actor (permission) | Manual action? | Reason | Step-up | Guards / prerequisites |
|---|---|---|---|---|---|---|---|
| finalize_intake | draft | awaiting_data | case.create (مدير الحالات) | wizard «إنشاء» | — | — | intake_complete (owner, ID, phone, contract, property, mortgage, debt), duplicate_resolved (open duplicate contract needs documented reason) |
| start_verification | awaiting_data | verification | case.transition | yes | — | — | mandatory_data |
| start_valuation | verification | valuation | case.transition | yes | — | — | core_docs_verified (deed, national ID, financing contract verified and unexpired) |
| propose_solution | valuation | proposed_solution | case.transition (المحلل) | yes | — | — | valuation_valid (accepted report, ≤ 90 days), legal_review_complete |
| submit_for_approval | proposed_solution, negotiation | internal_approval | solution.review (reviewer ≠ preparer) | review screen | note (mandatory) + attestation | — | valuation_valid, analysis_complete, no_open_complaint; version locked; approver resolved by limits |
| approve_and_offer | internal_approval | awaiting_customer | solution.approve (assigned approver ≠ preparer ≠ reviewer, within tier) | approval decision | yes | **yes** | re-check of tier at decision; offer created from locked version, validity 10 days |
| return_to_solution | internal_approval | proposed_solution | solution.approve | approval decision (return/reject) | yes | **yes** | — |
| owner_counteroffer | awaiting_customer | negotiation | owner (system-initiated) | owner portal | — | — | live offer; creates a request, never an agreement |
| owner_declined | awaiting_customer | proposed_solution | owner (system-initiated) | owner portal | optional | — | **never** leads to referral |
| counter_declined | negotiation | proposed_solution | negotiation.manage | apology dialog | yes | — | never referral |
| activate_agreement | awaiting_customer | active_settlement | agreement.activate (القانونية) | agreement screen | — | — | consent_recorded (2 acknowledgements + OTP), agreement_ready (legal review done + schedule created) — owner acceptance alone never activates |
| restructure_after_breach | active_settlement | proposed_solution | breach.manage | breach review outcome | yes | — | open_breach_review |
| settlement_completed | active_settlement | awaiting_reconciliation | reconciliation.prepare / system on last match | automatic after final checker match | — | — | all_installments_matched |
| start_voluntary_sale | proposed_solution, internal_approval, awaiting_customer, negotiation, active_settlement | voluntary_sale | sale.manage + sale approval | sale decision (B8) | yes | approval | owner_sale_consent, valuation_valid, no_open_complaint |
| sale_completed | voluntary_sale | awaiting_reconciliation | sale.manage | sale tracking (B8) | — | — | sale completed |
| refer_judicial | solution states | judicial_referral | referral.approve (≠ legal initiator) | referral decision (L25/J01) | yes | **yes** | no_open_complaint, no_live_offer, readiness pack, notice sent + objection period elapsed |
| external_sale_started | judicial_referral | external_judicial_sale | referral.external_update | external reference (J03) | yes | — | external reference recorded (official status stored verbatim, never mapped) |
| external_result_recorded | external_judicial_sale | awaiting_reconciliation | referral.external_update | result capture (J07) | yes | — | result + evidence |
| close | awaiting_reconciliation | closed | case.close (المالية + معتمد) | closure (L26/F04) | yes | **yes** | reconciliation_approved (zero or explained difference, 3 different people), closure_documents_ready |
| pause | any active non-terminal | paused | case.pause | yes («إيقاف الحالة مؤقتاً…») | yes | — | SLA frozen; previous status remembered |
| resume | paused | previous status | case.pause | yes | yes | — | SLA resumes |
| cancel | draft, active non-terminal, paused | cancelled | case.cancel_approve after a case.cancel request (two people) | «إلغاء الحالة…» review | yes | **yes** | no_open_complaint |

Forbidden by design (asserted in tests): decline → referral; any automatic transition into
`judicial_referral`; debtor acceptance → `active_settlement` without legal review and schedule;
preparer approving own version; payment recorder matching own payment.

Sub-states are separate entities, not statuses: tasks, approval requests, documents/versions, installments/payments,
breach reviews, complaints, external official status (verbatim).
