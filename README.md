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

Every seeded user has the password `Rahoon-Demo-2026!`. The SMS code isn't sent anywhere (sandbox); it's shown on screen.

**Individuals** don't have seeded logins for the demo: register live at **`/start`** with any valid-looking ID (10 digits starting with 1 or 2) and a Saudi mobile (05…). The code appears on screen.

**«فريق رهون»** (sign in at `/login`, lands on `/team`):

| User | Email | Role |
|---|---|---|
| لمى الحربي | l.alharbi@team.rahoon.example | Team lead (sees all, assigns) |
| نايف اليامي | n.alyami@team.rahoon.example | Case coordinator (REQ-2026-00302 is assigned to him) |
| تركي الشهري | t.alshehri@team.rahoon.example | Case coordinator |
| عبير القحطاني | a.alqahtani@team.rahoon.example | Offer verifier (a different member than the recorder) |

Two fictional requests are seeded (REQ-2026-00301 unassigned, REQ-2026-00302 in review). The lender-side users below are the **secondary** lender-on-platform mode; the owner invitation route is secondary too.

| User | Email | Role |
|---|---|---|
| سارة القحطاني | s.alqahtani@alufuq.example | Case manager (also a member of a second institution, so it's asked which to use) |
| فهد العتيبي | f.alotaibi@alufuq.example | Credit analyst |
| نورة الشهري | n.alshehri@alufuq.example | Approver |
| سلمان العمري | s.alomari@alufuq.example | Senior approver |
| ماجد الحربي | m.alharbi@alufuq.example | Legal |
| ريم الدوسري / عبدالعزيز الشمري | r.aldosari@ / a.alshammari@alufuq.example | Finance (payment maker / checker) |
| هند المطيري | h.almutairi@alufuq.example | Compliance, complaints reviewer |
| ليلى الغامدي | l.alghamdi@alufuq.example | Institution admin |
| عمر العنزي | o.alanazi@valuer-b.example | Valuer (provider portal) |
| أحمد المطيري | a.almutairi@rahoon.example | Platform operations |
| Owner of RH-2026-004172 | open `/invite/demo-RH-2026-004172`, national ID `1098734542` | Property owner (debtor) |

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
10. **Coordinator:** «تسجيل نقل الرد للجهة…» → «إغلاق الطلب…» with the outcome. The individual sees «قبلت العرض» and «نقلنا ردك…».
11. **Obstacles:** from the tracker the individual can object («اعتراض على بيانات أو مبالغ أو قرار») or complain; the team lead (لمى) answers from `/team/objections`.

## Tests

```bash
cd server && dotnet test              # needs Docker (it starts a throwaway PostgreSQL)
cd web && npx tsc --noEmit && npx eslint . && npm run build
cd web && npx playwright test         # API and web must be running; E2E_RESET=1 reseeds first
                                      # owner-journey.spec.ts = the MVP journey; responsive-qa.spec.ts = 390/768/1440 + 200% zoom + contrast
```
