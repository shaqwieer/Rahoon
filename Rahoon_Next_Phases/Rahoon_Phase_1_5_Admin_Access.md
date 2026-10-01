# Rahoon — Phase 1.5: Rahoon administration, team members, roles and permissions

**Implement this phase only.** Prerequisites: Existing Phase 0/1 identity, seller/buyer requests, private files and review queues.

## Context carried into this new session

You are the product engineer and UX specialist working in the existing Rahoon repository. Rahoon operates in Saudi Arabia. It helps a property owner exit a developer contract or sell a property connected to a bank/finance provider commitment, and helps a buyer find a suitable opportunity. Rahoon staff review evidence and numbers, prepare and publish opportunities, match buyers, and coordinate completion with the relevant parties.

The owner reports Phase 0 and Phase 1 completed. Verify the relevant implementation in the repository; do not redo those phases. Their baseline is seller and buyer intake, conditional developer/bank/property-type fields, saved drafts, account linking, private documents, staff review and requests for more information, owner confirmation, publication, basic listings/search/maps/calculators, and expressions of interest. Record any material gap that affects this phase. Fix a narrow dependency when necessary; do not silently restart the entire project.

The old mortgage-help, judicial-agent, debt-settlement and related business modules were subsequently removed. Keep them removed; do not restore their schemas, roles, flags, routes or seed data. Existing developer/bank/finance-provider directory and administration must be reused rather than duplicated. Source coverage can be partial: do not present imported entries as the complete official directory without evidence.

The latest agreed file-storage direction is database-backed storage behind an abstraction, allowing a future move to object storage. Preserve it. Apply authentication, authorization and the upload validation already appropriate to the product. A new malware-scanning integration and an object-storage migration are outside these phases.

Arabic-first, proper RTL, SAR, Saudi locations and Asia/Riyadh display time remain the defaults. The same account may be both seller and buyer. A public account is not a staff account simply because its profile claims a role. Use the existing stack and architecture; no framework rewrite, new lending product, wallet, escrow, automatic collection, or multi-tenant SaaS redesign.

Internal approval, owner acceptance, buyer financing approval, developer/bank approval and official completion are distinct. A bank-financed property's old installment is not automatically available to its buyer. Historic bank payments do not determine seller proceeds. Financial values may be verified, declared, estimated or unknown; unknown is never zero. Keep commission rate, basis, payer, tax treatment and timing in the approved policy, not a copied or invented percentage.

## Session execution contract

1. Read applicable repository instructions, architecture, migrations and any `docs/rahoon/roadmap/README.md`, decisions and completed-phase handoffs. Inspect the actual relevant code before editing. Repository implementation and explicit owner decisions take precedence over an old plan.
2. Implement ONLY the named phase in this document. Reuse working behavior. Do not add later-phase functionality early or mark a later phase complete through placeholders.
3. Maintain a coherent implementation from database and server rules through UI, permissions, jobs and tests. Buttons, notifications, timers, maps and reports must describe actual behavior, not a mock. Validate trusted calculations and transitions on the server.
4. Treat existing schemas and permission names as authoritative. The model names and dotted permission codes below illustrate semantics; map them onto the existing architecture and document the mapping. Do not create duplicate request, listing, document or identity models.
5. Keep public property media separate from private evidence. Return purpose-specific DTOs. UI hiding alone does not authorize an endpoint, export or download. Preserve any existing company/tenant boundary; do not introduce cross-boundary reads.
6. Use transactions, database constraints, version checks and retry-safe commands where the phase requires them. Do not replace business invariants with a front-end check or an in-memory lock.
7. Make normal reversible implementation decisions and continue. If a material business decision is missing, finish independent work, identify the precise unresolved choice, and do not manufacture a financial policy or external approval. Record adopted defaults and their rationale.
8. Follow the owner's existing Git and deployment workflow and repository rules. Respect uncommitted work. Do not force-push, invent a `main` branch where the repository uses `master`, expose credentials, or claim a push/deployment you did not perform. This phase does not require opening a PR or rewriting history.
9. Run checks appropriate to the change, including meaningful authorization/workflow/financial tests. Use the project's existing test tools. Check important new screens in a real browser at desktop and phone widths when possible. Report unavailable checks honestly rather than marking them passed.

## Mandatory handoff at the end of this session

Create or update the phase handoff under `docs/rahoon/roadmap/handoffs/`. It must contain: phase ID and status (`Completed`, `Partial`, or `Blocked`), verified prerequisites, final implemented behavior, important code entry points, migrations and their actual application state, permissions, adopted product decisions, tests with actual results, how to reproduce the acceptance scenario, remaining limitations, commit reference if available, actual push/deployment state, and the next phase with its starting instruction. Never put credentials or customer documents in the handoff.

Update roadmap status only from evidence. A partial feature, disabled required worker, or untested material invariant must remain visible in the status. Finish the named phase, summarize what changed and how it was verified, and STOP. Do not start the next phase in the same session.

## Outcome and scope

Build the administration and access foundation needed before several employees handle customers and before transactional offers are implemented. An owner must be able to manage the Rahoon team and custom roles; each employee must see and act only on the work their permission and resource scope allow.

This is a real operating console, not a second public registration flow or a single hard-coded `isAdmin` flag. Reuse existing working Phase 1 review screens. The operating dashboards, advanced policy settings and business reports are extended in Phase 4.

## Team membership and identity

- Separate the public customer account from active staff membership. A staff member can also own a public customer profile without merging the two authorization contexts.
- Provide team list/detail, search, active/invited/suspended status, assigned roles, permitted data scopes and workload references. Use a safe deactivate/revoke operation rather than deleting the history of an employee's actions.
- Support adding or inviting an employee through the real identity system. Invitations are identity-bound, expire, can be revoked and cannot be reused. Accepting an invitation must not allow the recipient to choose a more privileged role. If delivery is unconfigured, show the actual pending condition; do not fake an email being sent.
- On role change, suspension, membership removal or invitation revocation, enforce the change on the next protected request using the existing session/security-version mechanism or equivalent. A previously issued token cannot retain revoked staff authority until its natural expiry.
- Warn about assigned active cases during deactivation and allow an authorized manager to reassign them or return them to an unassigned queue. Preserve the former assignee and action history.
- Bootstrap the initial platform owner through the existing controlled setup. Do not add a public super-admin endpoint, default production password, or automatic staff privilege to every existing user.

## Roles and permission catalog

Implement a discoverable catalog grouped by business area. Roles may be created, renamed and assigned a selected set of catalog permissions. Distinguish protected system roles from custom roles. Removing an in-use role must either be blocked with a clear reassignment route or safely deactivate its grants; it must not orphan access records.

Seed defaults idempotently and map them onto existing roles where valid:

| Default role | Intended responsibilities | Important default limits |
|---|---|---|
| Platform owner | Team, roles, platform configuration and full operating oversight | Protect the last active owner; audit privileged actions |
| Operations manager | All operational queues, assignment and approved review decisions | No team/role administration unless explicitly granted |
| Case manager | Assigned customer requests, follow-up and permitted case edits | Assigned scope; no publishing or financial confirmation by default |
| Document reviewer | Review assigned evidence and verify permitted facts | No public publication, role changes or owner acceptance |
| Publisher | Review publication readiness and publish/suspend permitted opportunities | No customer impersonation or authority to accept an offer for the owner |
| Finance officer | Assigned financial breakdowns and, in later phases, payment evidence and fees | No role management or official-transfer completion by default |
| Support agent | Assigned customer conversations and limited request status | No private identity/finance documents by default |
| Auditor | Read permitted activity and aggregate operational information | Read-only; no automatic access to private document contents |

Minimum catalog areas: dashboard access; team read/manage; role read/manage; seller and buyer request read/edit/review/approve/reject/request-information; assignment; opportunity read/edit/publish/suspend; document read/download/review; directory read/manage/import; settings; audit read; reports/export. Reserve clearly named offer, reservation, closing and payment-evidence permissions for later phases, without pretending their actions already exist.

Create a default role-permission matrix in the repository. A custom role changes grants, not the definitions of protected actions. Show effective permissions on a team member, including which role grants each permission. No generic endpoint may accept an arbitrary permission or staff role from an unprivileged caller.

## Resource scopes and escalation controls

- Support the scopes actually needed: all permitted Rahoon work or assigned work; customers retain access to their own resources through the customer policy. A read-only operation is a permission, not a reason to bypass resource scope.
- Associate scope with the relevant grant. All-scope audit access must not accidentally turn assigned-scope document access into access to every private file.
- A staff actor must have explicit grant authority to change roles. Prevent self-escalation, unauthorized scope expansion and granting permissions beyond their allowed grant authority.
- Protect the last active platform owner against suspension, removal and loss of owner grants, including concurrent attempts. A manager may not bypass that invariant through a bulk endpoint or custom-role edit.
- Validate resource access on detail APIs, mutations, counts, exports and file downloads. Do not fetch all records and filter them only in the browser.
- Preserve actor, action, subject, before/after grants, scope, timestamp and reason where relevant. Redact secrets and document contents from the audit event.

## Administration UX

Use one consistent Rahoon console shell and permission-aware navigation. Provide team list/detail, invite/add form, role list/editor, permission matrix, effective-access view, audit timeline and existing assigned-work queues. A logged-in employee's landing page must lead to an area they can access.

Separate view, edit and approve buttons. Show why an otherwise authorized action is unavailable, such as a request needing documents. Do not expose internal notes to the seller/buyer view. Forms need loading, validation, duplicate-invitation and forbidden states at phone width as well as desktop.

## Acceptance and verification

1. A case manager can review their assigned request and cannot read another manager's private file by changing an ID.
2. A reviewer can verify evidence but cannot publish or approve an offer as the owner. A publisher cannot manage roles without the explicit grant.
3. Changing a role or suspending a member takes effect with an already-issued session, on both mutation and download endpoints.
4. A public user cannot gain staff membership through profile editing or a forged role field.
5. Expired/revoked/used invitations fail safely. Duplicate submissions do not create duplicate memberships.
6. The last-owner constraint survives concurrent administrative actions; effective scopes do not widen unexpectedly when roles are combined.
7. Role/seed changes are repeatable, and existing Phase 1 seller/buyer flows still work.

Include an API authorization matrix and focused negative tests, not only an end-to-end scenario run as the platform owner. The delivery scenario uses at least two case managers, a reviewer and a publisher with genuinely different grants.

## Phase-specific handoff and next session

Write `docs/rahoon/roadmap/handoffs/phase-1.5-handoff.md` and update this phase's actual status in the roadmap. The tailored implementation specification belongs at `docs/rahoon/roadmap/phase-1.5-admin-access.md`. Next: **Phase 2 — discovery, matching and calculators**. Do not execute it in this session.
