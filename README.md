# رهون (Rahoon)

**منصة رهون** is a SaaS platform in Saudi Arabia that helps **individuals who are struggling to repay an existing mortgage**. The individual starts a request, and the Rahoon team studies it and coordinates with their financing institution. Rahoon helps through four paths: keeping the property (rescheduling or easing installments), debt settlement, consensual sale when continuing isn't possible, and removing obstacles (objections, documents, complaints). None of these is a guaranteed outcome: the lender decides its offers, and the individual decides the response. Rahoon doesn't offer new finance, isn't a court or an auction operator, and doesn't hold funds or execute payments.

- Product source of truth: `docs/product/product-direction.md` (confirmed direction + open decisions).
- Work plan and status: `docs/phases/README.md`.
- Architecture: `docs/architecture.md`.

> **Current state (2026-09-25):**
> - What runs today is the platform foundation and the **lender-side** screens.
> - The owner portal is still invitation-based.
> - The individual's own journey (landing → registration → request → the Rahoon team follows up) is Phase 1A of the plan, and not built yet.

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

The self-registered individual journey isn't built yet (Phase 1A). Until then, the owner view is reached through the lender invitation below. That route is now secondary.

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

## Tests

```bash
cd server && dotnet test              # needs Docker (it starts a throwaway PostgreSQL)
cd web && npx tsc --noEmit && npx eslint . && npm run build
cd web && npx playwright test         # API and web must be running; E2E_RESET=1 reseeds first
```
