"""Generates docs/design-implementation-map.md from the design's traceability matrix
(00 Brief & Assumptions, 111 screens) plus the implementation data below.
Run: python scripts/gen_impl_map.py   (statuses live in STATUS / COMPONENT_STATUS)."""
import re, os, pathlib

ROOT = pathlib.Path(__file__).resolve().parents[1]
BRIEF = ROOT / "design-source" / "00 Brief & Assumptions.dc.html"
FILES = {
    "B1": "03 Phase 0 - Anchor Screens.dc.html / Anchor Screens B.dc.html",
    "B2": "04 Phase 1 - B2 Shared & Public.dc.html", "B3": "04 Phase 1 - B3 Case Tabs.dc.html",
    "B4": "04 Phase 1 - B4 Solutions & Agreement.dc.html", "B5": "04 Phase 1 - B5 Comms, Referral & Closure.dc.html",
    "B6": "04 Phase 1 - B6 Debtor Journey.dc.html", "B7": "04 Phase 1 - B7 Provider & Admin.dc.html",
    "B8": "05 Phase 2 - B8 Voluntary Sale.dc.html", "B9": "05 Phase 2 - B9 Service Ecosystem.dc.html",
    "B10": "06 Phase 3 - B10 Judicial & Financial Integration.dc.html", "B11": "07 Phase 4 - B11 Optimization.dc.html",
}

# id: (route, react component, API operations, domain entities, UI states, integration dependencies)
IMPL = {
 "S01": ("/", "app/(public)/page.tsx · LandingPage", "—", "—", "SSR, hreflang, mobile menu", "—"),
 "S02": ("/demo", "app/(public)/demo/page.tsx · DemoRequestForm", "POST /api/public/demo-requests", "InstitutionApplication", "validation, success, rate-limited", "email (simulated)"),
 "S03": ("/login", "app/(auth)/login/page.tsx · LoginForm", "POST /api/auth/login", "User, Session", "default, error+remaining attempts, locked, offline", "—"),
 "S04": ("/login/mfa", "app/(auth)/login/mfa/page.tsx · OtpInput", "POST /api/auth/mfa/verify|resend", "OtpChallenge, Session", "default, wrong code, locked, resend countdown", "SMS gateway (simulated)"),
 "S05": ("/invite/staff/[token]", "app/(auth)/invite/staff/[token]/page.tsx", "GET/POST /api/public/staff-invitations/{token}", "Invitation, User, Membership", "valid, expired, used", "email/SMS (simulated)"),
 "S06": ("/select-context", "app/(auth)/select-context/page.tsx · OrgRoleSwitcher", "POST /api/auth/context", "Membership, Session (rotated)", "list, current", "—"),
 "S07": ("/profile", "app/(lender)/profile/page.tsx · SessionsTable", "GET /api/auth/sessions, POST revoke / revoke-others (step-up)", "Session", "current session protected", "—"),
 "S08": ("/notifications", "app/(lender)/notifications/page.tsx · NotificationList", "GET /api/notifications, POST read", "Notification", "unread, empty", "—"),
 "S09": ("/tasks", "app/(lender)/tasks/page.tsx · TaskList", "GET/POST /api/tasks, complete, reassign", "CaseTask", "mine/team, overdue, empty", "—"),
 "S10": ("/search", "components/shell/CommandPalette + app/(lender)/search", "GET /api/search", "Case, CaseTask", "no results, forbidden-scoped", "—"),
 "S11": ("/help", "app/(lender)/help/page.tsx", "—", "—", "static + contact", "—"),
 "S12": ("/access-denied", "app/access-denied/page.tsx · SystemState(forbidden)", "(403/case_forbidden from any endpoint)", "—", "forbidden, expired link", "—"),
 "L01": ("/portfolio", "app/(lender)/portfolio/page.tsx · KpiTile, BarList", "GET /api/portfolio", "Case, CaseTask, SlaRule", "loading, empty, error", "core banking (unavailable → manual source label)"),
 "L02": ("/cases", "app/(lender)/cases/page.tsx · CaseGrid/CaseCard", "GET /api/cases, views, reassign, export", "Case, SavedView", "empty, loading, error, forbidden", "—"),
 "L03": ("/cases/new/[ref]", "app/(lender)/cases/new · CreateCaseWizard", "POST /api/cases/drafts, PUT steps, submit, duplicate-check", "Case, CaseParty, Property, Mortgage, FinancingContract, DebtSnapshot", "autosave, errors summary, duplicate warning", "—"),
 "L04": ("/cases/import", "app/(lender)/cases/import", "POST /api/cases/imports, decisions, commit", "ImportBatch, ImportRow", "row errors, duplicates", "core banking file (manual)"),
 "L05": ("/cases/[ref]", "app/(lender)/cases/[ref]/page.tsx · CaseHeader, NextActionCard", "GET /api/cases/{ref}, transitions, reveal", "Case (+ all sub-entities)", "loading, forbidden, offline read-only", "—"),
 "L06": ("/cases/[ref]/parties", "…/parties/page.tsx · PartyCard", "GET/POST/PATCH parties, owner-invitation, reveal", "CaseParty, OwnerAccess, PiiRevealLog", "masked, revealed 60 s", "national ID provider (unavailable)"),
 "L07": ("/cases/[ref]/finance", "…/finance/page.tsx · InstallmentHistoryStrip", "GET finance, POST correction-requests", "FinancingContract, DebtSnapshot, InstallmentHistoryEntry", "sync ok/delayed/failed/manual", "core banking (unavailable)"),
 "L08": ("/cases/[ref]/property", "…/property/page.tsx", "GET property, POST mortgage/legal-review", "Mortgage", "legal pending/complete", "real-estate registry (unavailable)"),
 "L09": ("/cases/[ref]/property", "…/property/page.tsx", "GET/PATCH property", "Property", "—", "—"),
 "L10": ("/cases/[ref]/documents", "…/documents/page.tsx · DocumentItem, RequestDrawer", "GET/POST documents, versions, review, requests, download", "CaseDocument, DocumentVersion, DocumentRequest, DownloadLog", "requested, uploaded, verified, rejected, expiring", "file scanning (basic placeholder)"),
 "L11": ("/cases/[ref]/valuation", "…/valuation/page.tsx", "GET valuation, review, revaluation-requests", "ValuationReport, ProviderAssignment", "valid, expiring, expired", "—"),
 "L12": ("/cases/[ref]/valuation#analysis", "…/valuation/AnalysisPanel", "GET/PUT analysis", "AffordabilityAnalysis", "incomplete, complete", "—"),
 "L13": ("/cases/[ref]/solutions/new", "…/solutions/[n]/builder · SolutionBuilder", "POST/PUT solutions, calculate, handover", "SolutionVersion", "draft, locked, DSR over limit", "—"),
 "L14": ("/cases/[ref]/solutions/compare", "…/solutions/compare", "GET solutions", "SolutionVersion", "—", "—"),
 "L15": ("/cases/[ref]/solutions/[n]/submit", "…/submit · ReviewScreen", "GET submission, POST submit", "ApprovalRequest, SolutionVersion (locked)", "blocked (preparer), success", "—"),
 "L16": ("/approvals", "app/(lender)/approvals · ApprovalInbox, DecisionPane", "GET /api/approvals, detail, POST decision (step-up)", "ApprovalRequest", "empty, escalated, decided, changed-after-open", "—"),
 "L17": ("/cases/[ref]/solutions/[n]/preview", "…/preview · PhoneMock", "GET solutions/{n}/preview", "SolutionVersion, CommunicationTemplate", "—", "—"),
 "L18": ("/cases/[ref]/solutions/negotiation", "…/negotiation", "GET negotiation, notes, decline-counter, offer-extensions", "NegotiationEntry, Offer", "counter, internal note", "—"),
 "L19": ("/cases/[ref]/agreement", "…/agreement", "GET agreement, legal-review, schedule, activate", "Agreement, ConsentRecord", "pending activation, active", "licensed e-signature (unavailable)"),
 "L20": ("/cases/[ref]/payments", "…/payments · RecordPaymentDrawer", "GET/POST payments, match, reject, check-reference", "Installment, PaymentRecord", "upcoming, due, pending match, matched, partial, overdue, rejected", "licensed payment (unavailable)"),
 "L21": ("/cases/[ref]/payments/breach", "…/payments/breach", "GET breach, POST outcome", "BreachReview", "open (cure), cured, restructuring, other options", "—"),
 "L22": ("/cases/[ref]/comms", "…/comms", "GET comms, POST messages, appointments", "CaseMessage, Appointment, CaseTask", "internal vs owner channel", "SMS (simulated)"),
 "L23": ("/complaints/[ref]", "app/(lender)/complaints", "GET/POST complaints, findings, draft, decision, escalate", "Complaint", "received, in review, awaiting owner, closed, escalated, overdue", "—"),
 "L24": ("/cases/[ref]/audit", "…/audit · AuditTimeline", "GET audit, verify, export", "AuditEvent", "blocked attempts, reveal", "—"),
 "L25": ("/cases/[ref]/referral", "…/referral", "readiness, notice, decision (step-up), evidence pack, external reference", "JudicialReferral, ExternalStatusEntry", "readiness unmet, objection period", "judicial channel (unavailable → manual)"),
 "L26": ("/cases/[ref]/closure", "…/closure", "reconciliation, closure documents, close (step-up)", "Reconciliation, ClosureDocument", "difference explained, 3-person approval", "core banking (unavailable)"),
 "D01": ("/invite/[token] → /invite/[token]/verify", "app/(auth)/invite/[token]", "GET /api/public/invitations/{token}, POST owner/verify-id, verify-otp", "OwnerAccess, OtpChallenge, Session", "active, used, expired, invalid, wrong code", "national ID provider (unavailable), SMS (simulated)"),
 "D02": ("/owner", "app/owner/page.tsx · NextStepCard", "GET /api/owner/home", "Case, Offer, Agreement", "offer waiting, no action, closed", "—"),
 "D03": ("/owner/journey", "app/owner/journey · JourneyTimeline", "GET /api/owner/journey", "AuditEvent", "—", "—"),
 "D04": ("/owner/documents", "app/owner/documents · DocRequestCard", "GET /api/owner/documents, upload, help", "CaseDocument, DocumentVersion", "required, rejected+reason, in review, received", "—"),
 "D05": ("/owner/debt", "app/owner/debt · ExplainedLineItem", "GET /api/owner/debt", "DebtSnapshot", "—", "—"),
 "D06": ("/owner/options", "app/owner/options · OptionCard", "GET /api/owner/options, inquiry", "Offer", "offer, no offer", "—"),
 "D07": ("/owner/offers/[id]", "app/owner/offers/[id]", "GET /api/owner/offers/{id}, decline", "Offer, SolutionVersion", "valid, expired", "—"),
 "D08": ("/owner/offers/[id]/counter", "…/counter", "POST /api/owner/offers/{id}/counter", "NegotiationEntry", "—", "—"),
 "D09": ("/owner/offers/[id]/accept", "…/accept · OtpInput, ConsentSummary", "POST consent/otp, consent", "ConsentRecord, Agreement", "wrong code, success reference", "SMS (simulated); licensed e-sign (unavailable)"),
 "D10": ("/owner/payments", "app/owner/payments · InstallmentRow", "GET /api/owner/payments, notice", "Installment, PaymentRecord, PaymentNotice", "received, being confirmed, upcoming", "payment gateway (unavailable)"),
 "D11": ("/owner/help", "app/owner/help", "POST /api/owner/hardship", "HardshipRequest", "—", "—"),
 "D12": ("/owner/messages", "app/owner/messages · MessageBubble", "GET/POST /api/owner/messages, appointments", "CaseMessage, Appointment", "—", "—"),
 "D13": ("/owner/complaints/new", "app/owner/complaints", "GET/POST /api/owner/complaints", "Complaint", "submitted with reference", "—"),
 "D14": ("/owner/documents/closure", "app/owner/documents/closure", "GET /api/owner/closure", "ClosureDocument", "closed, read-only period", "—"),
}
for sid in ["V01","V02","V03","V04"]:
    IMPL[sid] = ("/provider/assignments" + ("" if sid=="V01" else "/[id]"), "app/provider/…", "/api/provider/assignments…", "ProviderAssignment, AssignmentMessage, AssignmentSubmission", "new, in progress, returned, submitted, access expiring", "—")
for sid, route in {"A01":"/settings/organization","A02":"/settings/users","A03":"/settings/documents","A04":"/settings/approval-limits","A05":"/settings/templates","A06":"/settings/sla","A07":"/reports"}.items():
    IMPL[sid] = (route, "app/(lender)/settings/…", "/api/settings/…", "Organization, Membership, DocumentRule, ApprovalLimitPolicy, CommunicationTemplate, SlaRule", "maker-checker pending", "—")
for i in range(1, 14):
    sid = f"PA{i:02d}"
    IMPL[sid] = ("/platform/…", "app/platform/…", "/api/platform/…", "platform admin entities", "masked by default", "—")
IMPL["PA06"] = ("/platform/cases", "app/platform/cases", "/api/platform/cases/monitor, temp-access (dual approval)", "TempAccessRequest", "masked ids only; grant active/expired", "—")
for sid in ["L27","L28","L29","L30","L31","L32","L33","D15","D16"]:
    IMPL[sid] = ("/cases/[ref]/sale…" if sid.startswith("L") else "/owner/sale", "app/(lender)/cases/[ref]/sale… / app/owner/sale", "/api/cases/{ref}/sale…, /api/owner/sale…", "Sale entities, ConsentRecord(sale_consent)", "consent, listing, offers", "—")
for sid in ["V05","V06","V07","PA14","PA15","PA16","PA17","X01","X02"]:
    IMPL[sid] = ("(see B9 spec)", "app/…", "(see docs/progress/backend-B8-B9.md)", "ecosystem entities", "—", "licensed signing/payment (conditional → unavailable)" if sid.startswith("X") else "—")
for sid in ["J01","J02","J03","J04","J05","J06","J07","F01","F02","F03","F04","PA18"]:
    IMPL[sid] = ("(see B10 spec)", "app/…", "(see docs/progress/backend-B10-B11.md)", "referral/closure entities", "—", "judicial channel (unavailable → manual)")
for sid in ["O01","O02","O03","O04","O05"]:
    IMPL[sid] = ("/analytics…", "app/(lender)/analytics…", "(see docs/progress/backend-B10-B11.md)", "analytics (derived), DecisionSupportOutput", "provenance + limitations + human opinion", "no ML service (deterministic heuristic, labelled)")

# Overall status per screen: planned | implemented | verified | blocked. API/UI tracked separately.
API_DONE = {"S03","S04","S06","S07","S08","S09","S10","S12","L01","L02","L03","L05","L10","L13","L15","L16","L17","L18","L19","L20","L21","L22","L23",
            "D01","D02","D03","D04","D05","D06","D07","D08","D09","D10","D11","D12","D13","D14"}
API_VERIFIED = {"S03","S04","S06","L01","L02","L03","L05","L15","L16","L19","L20","L21","L23","D01","D02","D06","D08","D09","D10","D13"}
UI_DONE = set()
BLOCKED = {}

def load_rows():
    s = BRIEF.read_text(encoding="utf-8")
    raw = s[s.index("static RAW = `") + len("static RAW = `"): s.index("`;", s.index("static RAW = `"))]
    rows = []
    for line in raw.strip().splitlines():
        sid, ar, en, role, key, phase, nav, batch = line.split("|")
        slug = "".join(w[:1].upper() + w[1:] for w in re.sub(r"[—&,/-]", " ", re.sub(r"\(.*?\)", "", en)).split())
        rows.append(dict(id=sid, ar=ar, en=en, role=role, key=key, phase=phase, nav=nav, batch=batch, frame=f"{phase}-{key}-{slug}-Desktop/Mobile"))
    return rows

def status(sid):
    if sid in BLOCKED: return "blocked"
    api = "verified" if sid in API_VERIFIED else "implemented" if sid in API_DONE else "planned"
    ui = "implemented" if sid in UI_DONE else "planned"
    overall = "verified" if (api == "verified" and ui == "implemented" and sid in UI_DONE) else "implemented" if (api != "planned" and ui != "planned") else "planned"
    return overall, api, ui

def main():
    rows = load_rows()
    out = ["# Design → implementation map", "",
           "Generated by `scripts/gen_impl_map.py` from the design's traceability matrix (00 Brief & Assumptions — 111 screens)",
           "and the implementation data in the script. **Status**: planned · implemented · verified · blocked (overall), with API and UI",
           "tracked separately. *Verified* = covered by automated tests (API) and, for UI, checked against the design in a browser.", "",
           "| # | Screen | Role | Phase | Source file · frame | Route | React component | API operations | Domain entities | UI states | Integrations | Status (overall · API · UI) |",
           "|---|---|---|---|---|---|---|---|---|---|---|---|"]
    for r in rows:
        route, comp, api, ent, states, integ = (c.replace("|", "\|") for c in IMPL.get(r["id"], ("—",) * 6))
        overall, a, u = status(r["id"])
        out.append(f"| {r['id']} | {r['ar']}<br>{r['en']} | {r['role']} | {r['phase']} | {FILES.get(r['batch'], r['batch'])}<br>`{r['frame']}` | `{route}` | {comp} | {api} | {ent} | {states} | {integ} | **{overall}** · {a} · {u} |")
    out += ["", "## Reusable components (02 Components, 02 Components – Set 2, child DCs)", "",
            "| Component | Design source | React implementation | Status |", "|---|---|---|---|"]
    comps = [
        ("LenderSidebar", "LenderSidebar.dc.html", "components/shell/LenderShell"), ("LenderTopbar", "LenderTopbar.dc.html", "components/shell/LenderShell"),
        ("CaseHeader", "CaseHeader.dc.html", "components/shell/CaseHeader"), ("DebtorTop / DebtorNav", "DebtorTop.dc.html, DebtorNav.dc.html", "components/shell/OwnerShell"),
        ("PlatformSidebar", "PlatformSidebar.dc.html", "components/shell/PlatformShell"), ("SettingsNav", "SettingsNav.dc.html", "components/shell/SettingsNav"),
        ("C01 Button", "02 Components §c01", "components/ui/Button"), ("C02 CaseStatus / SubStatus / SLA", "§c02", "components/ui/StatusChip, SubStatusTag, SlaBadge"),
        ("C03 StageProgress", "§c03", "components/ui/StageProgress"), ("C04 NextActionCard", "§c04", "components/ui/NextActionCard"),
        ("C05 Alert / Toast", "§c05", "components/ui/Alert, Toast"), ("C06 Field family", "§c06", "components/ui/fields/*"),
        ("C07 DocumentItem", "§c07", "components/ui/DocumentItem"), ("C08 ApprovalChain + VersionDiff", "§c08", "components/ui/ApprovalChain, VersionDiff"),
        ("C09 CaseRow / CaseCard", "§c09", "components/ui/CaseTable"), ("C10 AuditTimeline", "§c10", "components/ui/AuditTimeline"),
        ("C11 SystemState", "§c11", "components/ui/SystemState"), ("C12 OrgRoleSwitcher / Tabs / Filters", "§c12", "components/ui/Tabs, FilterChip, shell switcher"),
        ("C13 DatePicker (Hijri/Gregorian)", "02 Components – Set 2", "components/ui/fields/DateField"), ("C14 AccessibleChart", "Set 2", "components/ui/BarList"),
        ("C15 ExportDialog", "Set 2", "components/ui/ExportDialog"), ("ReviewScreen", "Anchor Screens B §review", "components/ui/ReviewScreen"),
        ("DecisionSupportTag", "09 Handoff", "components/ui/DecisionSupportTag"), ("IntegrationState", "09 Handoff", "components/ui/IntegrationStateTag"),
    ]
    for n, src, impl in comps:
        out.append(f"| {n} | {src} | `{impl}` | {'implemented' if n in COMPONENT_DONE else 'planned'} |")
    (ROOT / "docs" / "design-implementation-map.md").write_text("\n".join(out) + "\n", encoding="utf-8")
    print(f"wrote {len(rows)} screens")

COMPONENT_DONE = set()

if __name__ == "__main__":
    main()
