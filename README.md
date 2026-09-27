# رهون (Rahoon)

**منصة رهون** is a SaaS platform in Saudi Arabia that helps **individuals who are struggling to repay an existing mortgage**. The individual starts a request, and the Rahoon team studies it and coordinates with their financing institution. Rahoon helps through four paths: keeping the property (rescheduling or easing installments), debt settlement, consensual sale when continuing isn't possible, and removing obstacles (objections, documents, complaints). None of these is a guaranteed outcome: the lender decides its offers, and the individual decides the response. Rahoon doesn't offer new finance, isn't a court or an auction operator, and doesn't hold funds or execute payments.

- Product source of truth: `docs/product/product-direction.md` (confirmed direction + open decisions).
- Work plan and status: `docs/phases/README.md`.
- Architecture: `docs/architecture.md`.

> **Current state (2026-09-26, tag `mvp-1`):** the Phase 1A MVP runs end to end.
> - **Individual** (phone first): landing → registration → request with documented consent → «ماذا ستفعل رهون لك» and tracking → messages, information, objections and complaints → the lender's verified offer → response.
> - **«فريق رهون»** (`/team`): queue, review, identity check, completion requests, manual coordination log with the lender, offer recording and second-member verification, response relay, closing, objections and complaints.
> - The lender has **no account** in the MVP; the lender workspace (L01–L26) and the invitation-based owner portal are kept for a later lender-on-platform mode.
> - Designs D-1…D-6 and several product questions are still open; see `docs/phases/phase-1a-mvp-settlement.md` (Findings).

- `server/`: ASP.NET Core 10 API + EF Core, running on **http://localhost:5080**
- `web/`: Next.js 16 app, running on **http://localhost:3000**. It forwards `/api/*` to the API, so always open the app through :3000.
- PostgreSQL 16 in Docker, on **localhost:55432**

## Prerequisites

Docker Desktop, .NET SDK 10, Node.js 20+, Git (Git Bash for the `scripts/*.sh` helpers), and the EF tool: `dotnet tool install -g dotnet-ef`.

## First-time setup (once per machine)

```bash
cp .env.example .env                  # then set a real POSTGRES_PASSWORD in .env
docker compose up -d                  # starts the rahoon-postgres container

cd server/src/Rahoon.Api
dotnet user-secrets set "ConnectionStrings:Rahoon" "Host=localhost;Port=55432;Database=rahoon;Username=rahoon;Password=<POSTGRES_PASSWORD from .env>"
dotnet user-secrets set "Security:PiiLookupKey" "<any long random string>"

cd ../../../web
npm ci
```

Secrets live in user-secrets and `.env`. Both stay out of git.

## Daily run

Open three terminals, all from the repo root.

**1. Database** (skip if the container is already running):
```bash
docker compose up -d
```

**2. API**, in Git Bash:
```bash
bash scripts/dev-api.sh            # build + start on :5080 (log: %TEMP%/rahoon-api.log)
bash scripts/dev-api.sh --reset    # drop, migrate and reseed the demo data first
```
Or in PowerShell:
```powershell
cd server/src/Rahoon.Api
dotnet run                          # in Development it applies migrations; the first run seeds demo data
dotnet run -- reset-demo            # wipe and reseed (then run `dotnet run` again)
```

**3. Web:**
```bash
cd web
npm run dev                         # open http://localhost:3000
```

Stop: press Ctrl+C in the web and API terminals. Run `docker compose stop` to stop the database; the data is kept in the `rahoon-pgdata` volume.

## Demo logins

All data is fictional. Staff password for every seeded user: `Rahoon-Demo-2026!`. SMS codes are never sent (sandbox); the code is shown on screen. Reset to this state any time with `bash scripts/dev-api.sh --reset`.

### Individuals (MVP) — sign in at **`/start?mode=signin`** with the national ID + mobile, then the on-screen code

Each account has a request at a different stage, so every screen can be tried without replaying the journey. To start fresh, register any new ID (10 digits starting with 1 or 2) and a mobile starting with 05 at `/start`.

| Name | National ID | Mobile | Request · stage | What you can try |
|---|---|---|---|---|
| منيرة سعد الدوسري | `1087654321` | `0551110001` | REQ-2026-00301 · submitted, not assigned | Tracker, «ماذا ستفعل رهون لك», messages, withdraw; team: «غير مسندة» → take it |
| سعد فهد العنزي | `1076543210` | `0551110002` | REQ-2026-00302 · under review (نايف) **and** REQ-2026-00306 · closed, offer accepted | Several requests on one account; a full closed timeline with the relayed response |
| هيا عبدالرحمن القحطاني | `1065432109` | `0551110003` | REQ-2026-00303 · «نحتاج معلومة منك» | Answer the team from «إضافة معلومة أو مستند» (it returns to the team); a team message |
| فيصل ناصر الشمري | `1054321098` | `0551110004` | REQ-2026-00304 · coordinating with the lender; offer recorded, awaiting verification | Waiting-on-lender view; team: عبير verifies the offer in `/team/verify`, then فيصل sees it |
| نوف خالد العتيبي | `2043210987` (iqama) | `0551110005` | REQ-2026-00305 · settlement offer published | «مراجعة العرض» → accept with a code, ask a question, or «لا يناسبني»; the lender letter link |
| ماجد سليمان الزهراني | `1032109876` | `0551110006` | REQ-2026-00307 · «غير مناسب للخدمة حالياً» | Object to the decision; the team answers in `/team/objections` and can reopen the study |
| هند محمد السبيعي | `1021098765` | `0551110007` | REQ-2026-00308 · P1 offer accepted and relayed (Phase 1A-2) | Team: closing as «قبل العميل العرض» is refused; «بدء متابعة التنفيذ» (demo script step 11) |
| عمر عبدالله الغامدي | `1010987654` | `0551110008` | REQ-2026-00309 · «قيد متابعة التنفيذ» (Phase 1A-2) | Agreement and schedule from the lender, installment 1 confirmed, installment 2 reported and its confirmation awaiting عبير; «متابعة التنفيذ» and «الاتفاق والجدول والتأكيدات» |
| ريم سعود المطيري | `2098765432` (iqama) | `0551110009` | REQ-2026-00310 · closed «اكتمل التنفيذ» (Phase 1A-2) | P2 settlement confirmed by the lender; clearance and mortgage-release letters |

### «فريق رهون» (MVP) — sign in at **`/login`**, lands on `/team`

| Name | Email | Role | Try |
|---|---|---|---|
| لمى الحربي | `l.alharbi@team.rahoon.example` | Team lead | «الكل» tab, assign requests, answer objections and complaints, verify offers others recorded |
| نايف اليامي | `n.alyami@team.rahoon.example` | Case coordinator | Owns 00302–00305 and 00308–00310: review, identity check, information requests, coordination log, record offers, relay responses, close |
| تركي الشهري | `t.alshehri@team.rahoon.example` | Case coordinator | Take unassigned 00301; answer a complaint on نايف's requests (a coordinator can't answer complaints on their own) |
| عبير القحطاني | `a.alqahtani@team.rahoon.example` | Offer verifier | `/team/verify` → REQ-2026-00304 → checklist → publish (SMS step-up) or return with a reason. Also verifies execution records in the same queue (REQ-2026-00309) |

### Secondary modes (built in earlier phases, outside the MVP journey)

| Portal | Sign in | Users |
|---|---|---|
| Lender workspace (lender-on-platform mode) | `/login` | سارة القحطاني `s.alqahtani@alufuq.example` (case manager; also in a second institution, so it asks which) · فهد العتيبي `f.alotaibi@` (credit analyst) · نورة الشهري `n.alshehri@` (approver) · سلمان العمري `s.alomari@` (senior approver) · ماجد الحربي `m.alharbi@` (legal) · ريم الدوسري `r.aldosari@` / عبدالعزيز الشمري `a.alshammari@` (finance maker / checker) · هند المطيري `h.almutairi@` (compliance, complaints) · منصور القرني `m.alqarni@` (auditor) · خالد الزهراني `k.alzahrani@` (case officer) · ليلى الغامدي `l.alghamdi@` (institution admin) · سعود الراشد `s.alrashed@` (institution admin) — all `@alufuq.example`; مها الشهراني `m.alshahrani@sunbula.example` (second lender) |
| Owner portal (lender invitation) | open `/invite/demo-RH-2026-004172`, national ID `1098734542` | Owner of case RH-2026-004172 |
| Service providers | `/login` → `/provider` | عمر العنزي `o.alanazi@valuer-b.example` (valuer) · وليد القحطاني `w.alqahtani@broker-d.example` (broker) · حاتم الرشيد `h.alrashid@valuer-b.example` (provider admin) |
| Judicial agent (on hold, V10) | `/login` → `/agent` | ياسر الحمدان `y.alhamdan@agent-j.example` |
| Platform administration | `/login` → `/platform` | أحمد المطيري `a.almutairi@rahoon.example` (operations) · رنا السبيعي `r.alsubaie@` (support) · فيصل الدوسري `f.aldossary@` (auditor) · حصة العتيبي `h.alotaibi@` (compliance) — all `@rahoon.example` |

## Open the database in pgAdmin

1. Make sure the container is running: `docker compose up -d`.
2. In pgAdmin, go to **Register → Server…**
   - **General › Name:** `Rahoon (local)`
   - **Connection:**

     | Field | Value |
     |---|---|
     | Host | `localhost` |
     | Port | `55432` |
     | Maintenance database | `rahoon` |
     | Username | `rahoon` |
     | Password | the `POSTGRES_PASSWORD` value in your `.env` file |
3. Tables are under **Databases › rahoon › Schemas**, one schema per module (`cases`, `solutions`, `agreements`, `documents`, `assessment`, `comms`, `complaints`, `identity`, `providers`, `sale`, `ecosystem`, `referral`, `closure`, `audit`, `admin`).

Notes:
- Personal data (national ID, phone, deed number) is stored **encrypted** (`*_enc` columns). The masked value sits next to it.
- `audit` tables are append-only. A database trigger rejects UPDATE and DELETE; that's intended.
- Prefer changing data through the app. `reset-demo` rebuilds everything from the seed.

## MVP demo script (owner-first, about 15 minutes)

Two browser windows: a phone-sized one (390 px) for the individual, a desktop one for the Rahoon team. Reset first: `bash scripts/dev-api.sh --reset`.

1. **Individual:** open `/`, read the four help paths and «ابدأ طلب المعالجة». Register at `/start` (ID + mobile + code + terms).
2. **Individual:** «ابدأ طلب معالجة جديد» → choose «مصرف الأفق» → name, installment, how long behind, city → what suits you → upload a salary letter → tick the consent and confirm it with the code → review → «إرسال الطلب».
3. **Individual:** «متابعة طلبي» shows the status, «ننتظر: فريق رهون», the next step and «ماذا ستفعل رهون لك» (provisional paths). No dates anywhere.
4. **Coordinator (نايف):** `/team` → «غير مسندة» → open the request → «بدء دراسة الطلب» → «طلب استكمال…» (e.g. a bank statement).
5. **Individual:** sees «نحتاج معلومة منك» and answers from «إضافة معلومة أو مستند»; the request returns to the team.
6. **Coordinator:** «تسجيل التحقق من الهوية…» → «إضافة قيد…» in the coordination log (phone, counterpart, summary; tick «يظهر للعميل؟» with a short text) → «بدء التنسيق مع الجهة». The individual now sees «قيد التنسيق مع جهتك الممولة».
7. **Coordinator:** upload the lender's letter («رفع مستند…», type «خطاب الجهة الممولة») → «تسجيل عرض الجهة…» → «إرسال للتحقق».
8. **Verifier (عبير):** `/team/verify` → open → tick the four checks → «اعتماد ونشر للعميل» → SMS step-up.
9. **Individual:** «مراجعة العرض» → terms, «أثره عليك», validity «بحسب خطاب الجهة», «ليس نهائياً حتى توافق عليه» → «أوافق» with the code (or «لا يناسبني», or a question).
10. **Coordinator:** «تسجيل نقل الرد للجهة…». The individual sees «نقلنا ردك…». An accepted P1/P2 offer is tracked, not closed («قبل العميل العرض» is refused with the reason); a declined offer or a P3 sale still closes as before.
11. **Execution tracking (Phase 1A-2), manual mode — the lender executes, Rahoon tracks and explains:**
    1. **Coordinator:** «بدء متابعة التنفيذ». Upload the lender's letter («رفع مستند…», type «خطاب الجهة الممولة») → «التنفيذ» → «تسجيل الاتفاق والجدول…» (terms prefilled from the offer; schedule lines `1, 2026-11-01, 3100`) → «إرسال للتحقق».
    2. **Verifier (عبير):** `/team/verify` → the execution record → the four checks → «اعتماد ونشر للعميل» (SMS step-up). The recorder never gets this panel.
    3. **Individual:** the tracker shows «متابعة التنفيذ» and «رهون لا تستلم أي مبالغ» → «الاتفاق والجدول والتأكيدات» (the lender's schedule, no reminders) → «أبلغنا عن سداد» with proof → «بانتظار تأكيد جهتك».
    4. **Coordinator:** on the report, «تسجيل تأكيد الجهة…» from the lender's confirmation letter; **عبير** verifies → the individual sees «أكدته جهتك». A lender notice («إشعار من الجهة…») reaches the individual with «لم تتخذ رهون أي إجراء…».
    5. **Coordinator:** «مستند إغلاق…» (the kind relevant to the path is preselected; V13) → verified → «إغلاق الطلب…» → «اكتمل التنفيذ». The individual downloads the closure documents from the request page.
    - Shortcuts: REQ-2026-00308 is ready for step 1, REQ-2026-00309 is mid-tracking (a confirmation awaits عبير), REQ-2026-00310 is completed with closure letters.
12. **Obstacles:** from the tracker the individual can object («اعتراض على بيانات أو مبالغ أو قرار») or complain; the team lead (لمى) answers from `/team/objections`.

## Tests

```bash
cd server && dotnet test              # needs Docker (it starts a throwaway PostgreSQL)
cd web && npx tsc --noEmit && npx eslint . && npm run build
cd web && npx playwright test         # API and web must be running; E2E_RESET=1 reseeds first
                                      # owner-journey.spec.ts = the MVP journey; responsive-qa.spec.ts = 390/768/1440 + 200% zoom + contrast
```
