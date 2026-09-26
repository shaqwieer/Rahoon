# ADR 0001 — Request ownership, the Rahoon team tenant, and the request lifecycle

- **Status:** Accepted for implementation in Phase 1A (steps 4–8). Items marked *interim* follow an open product question and are reversible.
- **Date:** 2026-09-25
- **Decided by:** engineering, within the product decisions recorded in `docs/product/product-direction.md` (Q1, Q3, Q6, Q7, Q8, Q11–Q14). Product questions aren't decided here.
- **Supersedes:** nothing. It **adds** a request model beside the existing lender-tenant case model, which is kept for the later *lender-on-platform* mode.

## 1. Context

The MVP of منصة رهون is individual-first:
- The individual registers and submits a request about an existing mortgage default.
- The **Rahoon team** reviews it and obtains the individual's documented consent.
- The team coordinates with the lender over a **documented manual channel**. The lender has **no account** in the MVP.
- The lender decides its offer, and the individual decides the response (Q1, Q3, Q8).
- An individual has **one independent account and can hold several requests**, each with its own reference, status, documents and access (Q7, Q14).
- The UI shows **no deadlines or promised outcomes** (Q6, Q12).
- Rahoon never holds funds or executes payments (Q13).

The code today (Phase 0 and the merged B3–B9 backend) assumes something different:

| Today | Where |
|---|---|
| Every case is owned by a **lender tenant** (`Case : OrgEntity`). Tenancy is enforced by global query filters on `IOrgOwned` with `RequestContext.DataOrganizationIds`, plus `TenantWriteGuardInterceptor` | `Infrastructure/Persistence`, `Modules/Cases` |
| An owner session (`SessionScope.Owner`) is **pinned to one case** (`RequestContext.OwnerCaseId`). The owner reaches it only through a lender invitation (`OwnerAccess`), and gets a synthetic user e-mail (`owner+{id}@owners.rahoon.local`) | `Modules/Identity/AuthEndpoints.cs`, `Modules/Owner` |
| Organization kinds are `Lender`, `ServiceProvider`, `JudicialAgent` and `Platform`. **Platform staff see tenant data only under an approved, unexpired temporary-access grant** (dual approval, audited) | `SystemRoles.cs`, `RequestContextMiddleware.cs` |
| The audit log is hash-chained and append-only; events carry an optional `CaseId`/`CaseReference` | `Modules/Audit` |

The platform-staff isolation guarantee must survive. The Rahoon team, by contrast, must see request data every day.

## 2. Decision drivers

1. **Individual ownership:** an individual sees only their own requests. A session is tied to the person, not to one case.
2. **Rahoon team access:** team members see only what they're assigned to, unless they're a team lead with explicit view-all.
3. **Lender:** no platform access in the MVP. It's recorded as a **counterparty**.
4. **Platform admins:** no request data without the existing temporary-access control.
5. **Reuse** of what is verified: sessions/CSRF/MFA, OTP, idempotency, audit chain, document storage/scanning, notifier, workflow pattern.
6. **Coexistence:** the lender-on-platform mode (lender cases, L01–L26, D01) keeps working unchanged.

## 3. Options considered

| Option | Summary | Verdict |
|---|---|---|
| A | Requests owned by the existing **Platform** organization; the Rahoon team are Platform members | **Rejected.** It breaks the rule that platform staff see tenant data only under a dual-approved grant, and mixes platform administration with casework |
| **B** | A new organization kind **`Operator`** ("فريق رهون"). Requests are owned by the operator tenant **and** carry the applicant's user id. The Rahoon team are operator members | **Chosen** |
| C | Create a placeholder lender tenant per institution and store requests as lender cases | **Rejected.** It pretends the lender is on the platform, conflicts with Q1, and would expose request data to a future real lender tenant without a fresh consent decision |
| D | Requests owned by the individual only, with no tenant; team access by custom checks | **Rejected.** It loses the tenant filter and write guard, the proven defence in depth |

## 4. Decision

### 4.1 Tenancy and identities

- **New organization kind `Operator`.** One seeded organization, «فريق رهون». The model allows more than one operator organization later (SaaS), but the MVP has exactly one.
- **Rahoon team members are ordinary staff users** with a membership in the operator organization. Staff login is unchanged: password + SMS OTP, choose organization, step-up for sensitive actions.
- **Individual accounts:**
  - a new session scope `Individual`, beside `None`, `Organization` and `Owner`
  - the user row gets `AccountKind = Individual`
  - `User.Email` becomes optional for individuals (today's synthetic owner e-mail isn't repeated)
  - a new `identity.individual_profiles` row holds:
    - national ID/iqama, encrypted, with an HMAC lookup hash (**unique**) and a masked value
    - mobile, encrypted, with a hash and a masked value
    - terms acceptance: version and time
  - registration and sign-in follow the step 4 interim rule for Q10/Q15: ID + OTP to the registered mobile, no password
- **The existing `Owner` scope and `OwnerAccess` stay** for the lender-on-platform mode (D01). Linking an invited owner to an individual account is decided later (Phase 1B).

### 4.2 Access rules (enforced on the server)

| Actor | Sees | Writes | How it's enforced |
|---|---|---|---|
| Individual | own requests only: status, waiting-on, next step, own documents, published offers, messages visible to them, own responses | draft fields, documents, consent, responses, objections, messages, withdrawal | New `IApplicantOwned` (`ApplicantUserId`). The global filter admits a row when `SystemBypass`, or `DataOrganizationIds ∋ OrganizationId`, or (`Scope == Individual` and `ApplicantUserId == UserId`). The write guard allows individual writes only to their own rows. **Endpoints return explicit DTOs.** Internal notes and non-visible coordination entries are never projected |
| Rahoon coordinator | requests **assigned** to them | review, completion requests, coordination log, updates, offer recording, response relay | Operator membership (tenant filter) + a `RequestAccess` check (assignment), as `CaseAccess` does today |
| Rahoon verifier | requests with an offer awaiting verification | verify or return an offer | Permission + DB check `verified_by ≠ recorded_by` |
| Rahoon team lead | all operator requests | assign and reassign; view-all | Permission `request.view_all` |
| Platform admin (existing Platform org) | nothing by default | — | Unchanged. Reading operator data requires the existing temp-access grant, which must be extended to operator tenants when needed (Phase 1C) |
| Lender staff (Lender tenants) | nothing | — | No operator data in `DataOrganizationIds`; no endpoints |
| Providers / agents | nothing | — | Unchanged |

Proposed permission keys (added in step 6; `permission-matrix.md` is updated then):

| Key | For |
|---|---|
| `request.view_assigned` | coordinators, verifiers |
| `request.view_all` | team lead |
| `request.assign` | team lead |
| `request.review` | coordinators |
| `request.request_info` | coordinators |
| `request.coordinate` | writes the coordination log |
| `request.message` | coordinators |
| `request.offer_record` | coordinators |
| `request.offer_verify` | verifiers |
| `request.response_relay` | coordinators |
| `request.close` | coordinators |
| `request.objection_handle` | coordinators, team lead |

Role templates:
- **«منسق حالات»**
- **«مراجِع العروض»**, the verifier
- **«قائد الفريق»**

### 4.3 The lender as counterparty

- `requests.financing_institutions` is a directory of institutions:
  - Arabic and English names, and kind
  - active flag
  - optional `LinkedOrganizationId`, set if that institution later joins as a Lender tenant
- A request stores `InstitutionId`, or `InstitutionOtherName` when the individual picks «أخرى» (V6 interim).
- **No lender user ever reads a request in the MVP.** If a lender later joins, moving a request into that lender's tenant needs a **new, explicit consent** from the individual. That's out of scope here.

### 4.4 Data model (new schema `requests`)

| Entity | Purpose | Key rules |
|---|---|---|
| `Request` (`OrgEntity`, `IApplicantOwned`, `IConcurrencyVersioned`) | the individual's request | `Reference` = `REQ-YYYY-NNNNN` from the reference counter; `Status`; `WaitingOn` (team \| applicant \| lender \| none); `NextStepText` (the team's plain-language next step); `AssignedCoordinatorId`; the individual's declared finance and property facts with source «العميل»; `PreferredPaths` (P1–P3, or not sure); duplicate link (V7 interim: warn and link) |
| `RequestConsent` | documented consent to share data with the lender | `ConsentTextVersion`, the text snapshot, recipient institution, data categories, OTP-confirmed time; `WithdrawnAt`. Append-only: a change creates a new row |
| `RequestDocument` + `RequestDocumentVersion` | the individual's documents and lender evidence | Reuses `IDocumentStorage`, the scanner, the magic-byte checks and the append-only versions pattern. `Visibility` is applicant+team or team-only. Downloads are logged |
| `CoordinationEntry` | the manual channel log with the lender | channel (هاتف / بريد إلكتروني / خطاب رسمي / زيارة / أخرى), time, counterpart (name/role as given), summary, evidence document ids, `VisibleToApplicant` (default false). **Append-only:** corrections are new entries referencing the old one |
| `RequestUpdate` | what the individual sees in the timeline | text, `WaitingOn`, next step, time, author |
| `RequestMessage` | messages between the individual and the team | internal notes are a separate type and never projected to the individual |
| `RequestOffer` | the lender's offer, recorded by the team | path (P1/P2/P3); typed terms per path; plain-language effect text; lender letter (document version); lender reference and date; any validity **as stated by the lender**, shown as the lender's condition (design D-5); `RecordedBy`, `VerifiedBy` (DB check ≠); status draft → pending_verification → published / returned; superseded versions kept |
| `RequestResponse` | the individual's answer | kind accept / decline / question / counter; text; an OTP-confirmed consent record for *accept* (A-05: a consent record, not a licensed signature); `RelayedAt/By` + the coordination entry that relayed it |
| `RequestObjection` (step 8) | P4 objections to data or amounts | reason, disputed item, team response. The complaints module integration is decided in step 8 |

### 4.5 Request lifecycle

Explicit transitions implemented by a `RequestWorkflow` following the `CaseWorkflow` pattern:
- expected-status check (409)
- allowed source
- actor permission or ownership
- reason where required
- step-up where required
- named guards, with blocked attempts audited
- idempotency

| Key | From → To | Actor | Guards / notes |
|---|---|---|---|
| `submit` | draft → submitted | applicant | identity verified; interim required fields (Q5); an active consent row; duplicate warning acknowledged (V7) |
| `pick_up` | submitted → team_review | coordinator (assigned) or lead | assignment exists |
| `request_info` | team_review / lender_coordination → info_requested | coordinator | items listed; `WaitingOn = applicant` |
| `info_provided` | info_requested → *previous state* | applicant | at least one addition |
| `start_coordination` | team_review → lender_coordination | coordinator | consent active and not withdrawn; **at least one coordination entry recorded** |
| `publish_offer` | lender_coordination → offer_available | verifier | offer verified; verifier ≠ recorder; lender source document attached |
| `respond` | offer_available → response_recorded | applicant | accept needs an OTP step-up; decline or question needs no reason (optional). **No automatic action follows a decline** |
| `continue_coordination` | response_recorded → lender_coordination | coordinator | after relaying a question or counter; relay entry recorded |
| `close` | response_recorded / lender_coordination → closed | coordinator | outcome code (offer_accepted, offer_declined, lender_no_offer, other) + summary visible to the applicant. Execution tracking is Phase 1A-2 |
| `not_eligible` | team_review → not_eligible | coordinator | plain-language reason. **Interim:** free text + category «خارج نطاق الخدمة» until Q4; the applicant can object (P4) |
| `withdraw` | any non-terminal → withdrawn | applicant | optional reason; sharing stops |
| `withdraw_consent` | (on any non-terminal) | applicant | coordination can't start or continue; the request moves to info_requested with a plain explanation (effect wording V4) |

UI rule (Q6): every state maps to a **status label**, **«ننتظر»** (فريق رهون / أنت / جهتك الممولة) and a **next step**. There are no dates in the future. Internal timers (V5) live on team screens only.

### 4.6 Audit

- Every transition, consent change, coordination entry, offer record/verify/publish, response, document download and blocked attempt is audited in the existing hash chain.
- The `AuditEvent` gets nullable `SubjectType` (`request`) and `SubjectReference` (`REQ-…`) columns.
- They're included in the hash **for new events** through a hash-format version, so earlier events still verify.
- The individual's actor type is `individual`.

### 4.7 Reuse decisions

| Existing piece | Decision |
|---|---|
| Sessions, cookies, CSRF, Origin check, idempotency, rate limits, lockout | **Reuse** unchanged; new `Individual` scope |
| `OtpService` (sandbox SMS) | **Reuse** for registration, sign-in, consent and offer acceptance |
| `PiiProtector` (encryption + HMAC), masking | **Reuse** for individual profiles and request facts |
| Document storage, scanner, versions pattern | **Reuse** with new request-scoped tables |
| Notifier, sandbox outbound messages | **Reuse** |
| `CaseWorkflow` pattern | **Reuse the pattern** in a new `RequestWorkflow` |
| L13–L17 solution builder and approval tiers | **Don't reuse for MVP offers.** They model the lender's internal decision, which in the MVP happens outside Rahoon. Kept for the lender-on-platform mode |
| Offer display and consent UI (D07/D09 on the owner branch) | **Reuse the components** with a request-offer data adapter (design D-5) |
| Owner portal pages (D02–D14) | **Reuse the layouts and components**; data sources move to request endpoints; the case-bound pages stay for the lender mode |
| Complaints module | Decided in step 8: extend with an optional `RequestId`, or keep `RequestObjection` separate |

## 5. Consequences

- **Positive:**
  - The individual's data sits under an explicit tenant with the same filter and write guard as everything else.
  - The platform-admin isolation guarantee is untouched.
  - The lender-on-platform mode keeps working.
  - Consent is versioned evidence.
- **Negative / cost:**
  - A second, parallel lifecycle (request) beside the case lifecycle.
  - The owner portal needs data adapters.
  - Migrations are needed on `identity` (account kind, nullable e-mail, individual profiles, organization kind) and `audit` (subject columns), plus a new `requests` schema.
- **Risks to watch:**
  - The individual filter is a new filter shape and needs dedicated isolation tests.
  - An internal coordination entry must never leak to the individual. Tests must assert on DTOs.

## 6. Tests required (steps 4–8)

1. An individual can't read or write another individual's request: 404, and a blocked write is audited.
2. A lender-tenant user and a platform admin get nothing from the request endpoints. Temp access is covered when it's extended.
3. A coordinator sees only assigned requests; a lead sees all.
4. `start_coordination` is refused without active consent; consent withdrawal stops further coordination.
5. `publish_offer` is refused when verifier = recorder, or when there's no lender source document.
6. Individual DTOs never contain internal notes, non-visible coordination entries or team-only documents.
7. The same national ID can't create a second account; several requests per account work.
8. Idempotent submit and respond; replaying returns the same response.
9. Audit chain verification passes with request events (hash-format versioning).

## 7. Open items linked to this ADR

| Item | Where it's handled |
|---|---|
| Q4 eligibility | the `not_eligible` wording |
| Q5 required fields | interim set |
| Q10 / Q15 identity and sign-in | interim ID + OTP |
| V1 no on-behalf actions | coordination = consented sharing + relaying only |
| V2 channel wording | |
| V4 consent text and effects | |
| V5 internal SLA | |
| V6 institution list vs free text | |
| V7 duplicates | |
| Linking an invited owner (D01) to an individual account | Phase 1B |
| Converting a request into a lender-tenant case when a lender joins | needs new consent; later |

## 8. Amendments

| Date | Change | Reason |
|---|---|---|
| 2026-09-25 (Phase 1A step 4) | `User.Email` stays **required**. Individuals get a reserved non-routable placeholder (`individual+{id}@individuals.rahoon.local`), as invited owners already do. It's never used to sign in or to send messages, and `/api/auth/me` returns an empty e-mail for non-staff accounts. | 26 code paths read `User.Email`; making it optional would ripple through staff login, audit and masking for no product gain. The individual's identity key is the national-ID HMAC (unique). |
| 2026-09-25 (Phase 1A step 4) | A registration creates a `UserStatus.Pending` user and profile **before** the code is verified (sessions require a user). The pending user never gets a resolved session. A pending registration from another mobile can be replaced only after its code expires. | Keeps the one-account-per-ID rule without letting an unverified start block a real owner for long. |
| 2026-09-25 (Phase 1A step 4) | OTP attempts for individuals: **3** (the existing policy), not the 5 in B13 OR02. Only a real, already-registered account is locked after exhaustion; decoy challenges (ID registered with another mobile) never lock. | One policy across the platform; prevents locking a victim through someone else's attempts. Recorded in `design-conflicts.md` #18. |
| 2026-09-26 (Phase 1A step 5) | Request child rows (consents, documents, versions, timeline, and later coordination, messages, offers, responses, concerns, referrals) all carry `ApplicantUserId` and implement `IApplicantOwned`, so one combined query filter and one write-guard branch cover them. Audit events for requests chain under the operator tenant with hash format 2 (`SubjectType`/`SubjectReference`). | One rule for every request table; no hand-written ownership checks. |
| 2026-09-26 (Phase 1A step 5) | Withdrawing consent after submission moves the request to `info_requested` (flag `InfoRequestIsConsent`); a new consent resumes it to the previous state. | ADR §4.5 `withdraw_consent` needed a concrete, reversible effect; wording is still V4. |
| 2026-09-26 (Phase 1A step 6) | Coordinators may open unassigned requests and take them; verifiers see a request only while an offer awaits their check. Drafts are never visible to the team. | Small team, no dispatch step needed in the MVP; least privilege for verifiers. |
| 2026-09-26 (Phase 1A step 8) | **Objections and complaints on requests are a separate `RequestConcern`** (schema `requests`), not the lender-tenant complaints module (which is case-bound with a required `CaseId` and a `DueOn` deadline). A complaint is answered by a member other than the request's assigned coordinator (interim independence rule). Specialist referral is a manual `SpecialistReferral` record (V9). New transition `reopen` (`not_eligible → team_review`, `request.objection_handle`, reason, requires an upheld objection). | Keeps the operator tenant self-contained; no deadline promises (Q6/Q12); gives the P4 objection a real effect. |
