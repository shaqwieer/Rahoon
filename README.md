# رهون (Rahoon)

**رهون** is a platform for **Saudi Arabia** that helps the owner of a property tied to obligations with a **developer** or a **bank / financing institution** submit a request to exit the contract or sell the property, and helps a **buyer** find an opportunity that fits what they can pay **now** and what they can commit to **later**. The **Rahoon team** reviews requests, documents and figures, prepares opportunities, gets the owner's confirmation, publishes them and follows each interest. Rahoon is not a lender, a judicial agent, a consensual-sale or debt-settlement service; it holds no funds and promises no sale, price, recovery or approval.

- Product source of truth: `docs/product/product-definition.md` (2026-10-01).
- The earlier mortgage-default model was removed permanently on 2026-10-01 (code, routes, schemas and data): `docs/redefinition/legacy-inventory.md`. Its last state is the git tag `legacy-mortgage-final`.
- Work plan and status: `docs/phases/README.md` → `docs/phases/phase-m-exit-marketplace.md`.
- Architecture: `docs/architecture.md`.

- Staging for the client: **https://rahoon.talentfold.net** (Docker on the VPS; `deploy/README.md`).
- `server/`: ASP.NET Core 10 API + EF Core, on **http://localhost:5080**
- `web/`: Next.js 16 app, on **http://localhost:3000** (forwards `/api/*` to the API — always open the app through :3000).
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
bash scripts/dev-api.sh --reset    # drop, migrate and reseed the demo data first (local demo DB only)
```
An existing database keeps its data: `dotnet run -- migrate` (in `server/src/Rahoon.Api`) applies migrations and moves any
file still on disk into the database; `dotnet run -- seed` adds missing demo data. `--reset` and `reset-demo` drop the
database (and empty the organization directory) and are refused outside Development/Staging/Testing.

**Organization directory** (real developers, banks and finance companies from official sources; never demo data):
```bash
cd server/src/Rahoon.Api
dotnet run -- import-directory                      # all sources: SAMA banks, SAMA finance companies, REGA developers
dotnet run -- import-directory --source banks       # or finance | developers; --pages 21-40 resumes REGA; --dry-run
dotnet run -- import-directory --file Seed/Data/rega-developers-2026-10-01.csv   # bundled REGA developer list
dotnet run -- import-directory --file list.csv      # a verified official dataset (columns in DirectoryImporter.cs)
```
Reruns never duplicate and never change records an administrator edited; see `docs/architecture.md`.
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

The marketplace demo is fictional test data, shown as «تجريبي» in the app (people, parties, projects, amounts and the drawn photos). The organization directory is not demo data: it holds real organizations imported from official sources. Team password: `Rahoon-Demo-2026!`.

**SMS confirmation flag, `Auth:SmsConfirmation`** (env `Auth__SmsConfirmation`, default **false**): off → no SMS is sent and the code is confirmed automatically, and the app says plainly that the mobile was **not verified**. Local development turns it on (the sandbox code is shown on screen). No SMS provider is contracted yet (decision D1).

### Owners and buyers — sign in at **`/signin`** with the mobile, then the on-screen code

One account per mobile; the same account can sell and buy. Any new `05…` mobile creates an account.

| Name | Mobile | What you can try |
|---|---|---|
| خالد عبدالله المطيري | `0561110001` | SR-2026-00001 · طلب مستلم (developer, apartment under construction): complete the file |
| نورة سعد القحطاني | `0561110002` | SR-2026-00002 · قيد المراجعة (bank-financed villa, payoff letter not yet available) |
| فهد ناصر العتيبي | `0561110003` | SR-2026-00003 · يحتاج استكمال: answer the team's request and resend |
| سارة محمد الشهري | `0561110004` | SR-2026-00004 → OP-2026-00001 · منشورة (the brief's worked example 1: due now 325,000) |
| عبدالرحمن علي الدوسري | `0561110005` | OP-2026-00002 · منشورة (bank track, exact location, conditional financier approval) |
| ريم خالد الزهراني | `0561110006` | OP-2026-00003 · بانتظار تأكيد المالك: confirm or ask for changes at `/account/sell/SR-2026-00006/opportunity` |
| ماجد فيصل الحربي | `0561110007` | OP-2026-00004 · قيد الإعداد (land: no rooms; costs not entered → incomplete) |
| هيفاء سعود العنزي | `0561110008` | OP-2026-00005 · منشورة with an incomplete estimate (quarterly installment + annual payment) |
| سلطان عمر القرني | `0561110011` | BR-2026-00001 · buyer request received |
| لينا أحمد الغامدي | `0561110012` | BR-2026-00002 · approved for matching; interest IN-2026-00001 on OP-2026-00001; one saved opportunity |

### «فريق رهون» — sign in at **`/login`**, lands on the first console area the member's grants open

One demo member per default role (Phase 1.5; matrix in `docs/rahoon/roadmap/phase-1.5-admin-access.md`). «Assigned» members
see only the work assigned to them: assign it first as the owner or the operations manager.

| Name | Email | Role |
|---|---|---|
| لمى الحربي | `l.alharbi@team.rahoon.example` | Platform owner: everything, incl. team, roles (`/team/members`, `/team/roles`) and the directory |
| فهد العتيبي | `f.alotaibi@team.rahoon.example` | Operations manager: all queues, assignment, review decisions; no team/role admin, no publishing |
| نايف اليامي | `n.alyami@team.rahoon.example` | Case manager (assigned work): review, completion, decide, prepare opportunities, interests |
| تركي الشهري | `t.alshehri@team.rahoon.example` | Case manager (assigned work) |
| هدى الزهراني | `h.alzahrani@team.rahoon.example` | Document reviewer (assigned work): documents and figure verification; no decision, no publishing |
| عبير القحطاني | `a.alqahtani@team.rahoon.example` | Publisher: publish / pause / withdraw; no private documents |
| ماجد الغامدي | `m.alghamdi@team.rahoon.example` | Finance officer (assigned work): figures and their documents |
| ريم المطيري | `r.almutairi@team.rahoon.example` | Support: contact messages, assigned requests; no private documents |
| سارة الدوسري | `s.aldosari@team.rahoon.example` | Auditor: read-only, the log (`/team/audit`); no private documents |

Production has no demo members: the first platform owner is created with
`dotnet Rahoon.Api.dll bootstrap-owner --email … --name "…" --phone 05…` (prints a one-time `/join#…` link; refused once
an active owner exists). Everyone else is invited from `/team/members/invite`.

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
3. Tables are under **Databases › rahoon › Schemas**: `market` (requests, opportunities, interests), `directory` (organizations), `files` (uploaded files: metadata in `stored_files`, bytes in `file_blobs`), `identity`, `audit`, `app`.

Notes:
- Mobile numbers are stored **encrypted** (`*_enc` columns). The masked value sits next to it.
- `audit` tables are append-only. A database trigger rejects UPDATE and DELETE; that's intended.
- Prefer changing data through the app. `reset-demo` rebuilds everything from the seed.

## Demo script (about 15 minutes)

A phone-sized window for the owner/buyer and a desktop window for the team. Steps 1–9 are also automated in `web/e2e/market-journey.spec.ts`.

1. **Owner:** `/` → «ابدأ طلب بيع». Step 1: type, city, district, «مطور عقاري», the developer (search the directory, or «غير موجود في الدليل»). Step 2: the figures you know (try «لا أعرف»), see «نتيجة أولية تقديرية». Step 3: sign in with a new mobile and the code, confirm, «إرسال الطلب» → reference, status, next step.
2. **Owner:** «استكمل ملفك الآن» → the four groups; place the pin on the map, add a photo and a document; saving is automatic.
3. **Team (لمى):** `/team/sale` → the request → «بدء المراجعة» → «طلب استكمال» (tick what is missing, write a note).
4. **Owner:** sees the request, completes it and «إرسال الملف للمراجعة».
5. **Team:** accept the photo and documents, «اعتماد الرقم» with its source and date, record the developer's approval state, «اعتماد لإعداد فرصة» → «تجهيز الفرصة».
6. **Team:** in the opportunity, write the description, choose the photos and location precision, enter costs → «حفظ وحساب» → «إرسال الملخص للمالك».
7. **Owner:** «مراجعة الملخص» → confirm (or ask for changes). Confirming does not publish.
8. **Team:** tick the checklist → «نشر الفرصة». Changing a published figure creates a new version that needs the owner again.
9. **Buyer:** `/opportunities` → filter by what you can pay now and the installment; open a card; «مهتم بالفرصة». The team sees it in `/team/interests` and follows it up; nothing is reserved.
10. **Buyer:** «سجّل قدرتك الشرائية» (`/buy/new`; with external finance, optionally name the bank or finance company) → suggestions with why they fit; `/calculators` for the three calculators.
11. **Team lead:** `/team/organizations` → search and filter, add an organization, edit it (it becomes protected from imports), deactivate it with a reason.

## Tests

```bash
cd server && dotnet test              # needs Docker (it starts a throwaway PostgreSQL)
cd web && npx tsc --noEmit && npx eslint . && npm run build
cd web && npx playwright test         # API and web must be running; E2E_RESET=1 reseeds first
                                      # market-journey.spec.ts = the full exit/buy journey, directory administration,
                                      # directory pickers and 390px checks (needs the directory imported)
```
