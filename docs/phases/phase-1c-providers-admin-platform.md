# Phase 1C: Provider portal, institution admin, platform admin (B7) ⬜

**Start only after Phase 1B is done.**

**Goal:**
- Valuers and inspectors work their assignments in their own portal, with strict per-assignment access.
- Institution admins manage users, roles, limits, templates and SLA with maker-checker.
- The platform team runs onboarding, monitoring with masked data, and audited temporary support access.

The backend is ✅ on `master` (merged 2026-09-24, `fc0622e`; see `docs/progress/backend-B7.md`).

## Definition of done

- [ ] Every row below is ✅.
- [ ] Playwright covers these flows:
  - lender creates an assignment → provider submits → lender returns → provider resubmits → lender accepts
  - staff invitation accept (S05)
  - temporary access, dual-approved and read-only
- [ ] Every negative check passes: a provider sees nothing outside their assignment, and gets 404 after the access window.

## Scope and status

| ID | Screen | Status | Where / notes |
|---|---|---|---|
| S05 | Staff invitation acceptance (password policy → MFA) | 🟨 | branch `…a1417897…` (`189ffa7`) |
| — | Settings nav filtered by permission | 🟨 | branch `…a1417897…` (`c89cec8`) |
| V01–V04 | Provider inbox, detail, Q&A thread, submit/resubmit | 🟧 WIP | branch `…a1417897…` @ `9e280ce` |
| — | Lender side: `/cases/[ref]/assignments` (create, review submission) | 🟧 WIP | same; needs a tab link in `CaseChrome` |
| A01–A07 | Org settings, users/roles, document rules, approval limits, templates, SLA, reports | 🟧 WIP (A01, A02 started) | same |
| — | Institution approval of temporary access | ⬜ | |
| PA01–PA13 | Platform dashboard, applications, institutions, users, permissions, monitoring + temp access, defaults, complaints, audit, retention, service health | 🟧 WIP (PA01, PA03, PA04, PA06 started) | same |

## Steps

1. ⬜ Bring branch `…a1417897…` up to date with `master`. Typecheck, lint and build; decide file by file whether to keep or redo the WIP.
2. ⬜ Provider portal V01–V04 + the lender assignments page (+ the tab link).
3. ⬜ S05 + A01/A02 (users, roles, invitations, suspension, role change maker-checker).
4. ⬜ A03–A07.
5. ⬜ PA01, PA02 (application → institution), PA03–PA05.
6. ⬜ PA06 temporary access (request → institution + platform auditor approve → read-only view with countdown → revoke), then PA07–PA13.
7. ⬜ Playwright + responsive QA; update the implementation map.

## Findings / open decisions (from the backend build)

- No redaction service, so the shared title deed is masked and can't be downloaded by providers.
- Identity, income and debt documents can never be shared with providers.
- There's no CAPTCHA on public forms, and no real e-mail service (sandbox outbox only).
- Temporary access needs an institution admin **and** a platform auditor, and lasts at most 4 hours.
- A live assignment gives the provider org read access to the whole lender org at the DB-filter level. Every provider route filters explicitly by assignment. Hardening item: move the per-assignment check into the access layer.
