# Staging deployment: https://rahoon.talentfold.net

A demo server the client can use themselves. It runs the whole stack in Docker on the VPS (`173.249.37.177`) behind the host's nginx:

| Container | What | Reachable from |
|---|---|---|
| `rahoon-web-1` | Next.js (`next start`) | `127.0.0.1:8095` → nginx → `https://rahoon.talentfold.net` |
| `rahoon-api-1` | ASP.NET Core API, `ASPNETCORE_ENVIRONMENT=Staging` | the web container only (`http://api:8080`) |
| `rahoon-postgres-1` | PostgreSQL 16 — all data, **including uploaded files** | the API only (no published port) |

- **Settings:** `server/src/Rahoon.Api/appsettings.Staging.json`.
- **Start:** the API migrates the database (`DatabaseMigrator`) and seeds the fictional demo (the Rahoon team and the
  marketplace demo, the logins in the README). Later starts keep the data.
- **Files** are stored in the database (`files.stored_files` + `files.file_blobs`). The `apidata` volume (`/data`) holds
  only the Data Protection key ring (`/data/keys`).
- **SMS confirmation is off** (`SMS_CONFIRMATION=false`): no SMS is sent and sign-in confirms the code automatically.
  Turn it on only with a real SMS gateway (the API has only a sandbox gateway, which delivers nothing).

## First deployment (once)

```bash
# 1. Code
cd /opt && git clone https://github.com/shaqwieer/Rahoon.git && cd /opt/Rahoon

# 2. Secrets (hex only). Keep deploy/.env: PII_LOOKUP_KEY must never change after the first start.
cp deploy/.env.example deploy/.env
sed -i "s/^POSTGRES_PASSWORD=.*/POSTGRES_PASSWORD=$(openssl rand -hex 24)/; s/^PII_LOOKUP_KEY=.*/PII_LOOKUP_KEY=$(openssl rand -hex 32)/" deploy/.env

# 3. Build and start (the first build takes a few minutes)
docker compose -f deploy/compose.staging.yml --env-file deploy/.env up -d --build
docker compose -f deploy/compose.staging.yml --env-file deploy/.env logs -f api   # wait for "Seed complete."
curl -s http://127.0.0.1:8095/api/health                                          # Healthy

# 4. Fill the organization directory from the official sources (see below)

# 5. nginx + TLS (only this site's file; nginx -t before reload)
cp deploy/nginx/rahoon.talentfold.net.conf /etc/nginx/sites-available/rahoon.talentfold.net
ln -s /etc/nginx/sites-available/rahoon.talentfold.net /etc/nginx/sites-enabled/rahoon.talentfold.net
nginx -t && systemctl reload nginx
certbot --nginx -d rahoon.talentfold.net
```

## Update to the latest code

```bash
cd /opt/Rahoon && git pull
docker compose -f deploy/compose.staging.yml --env-file deploy/.env up -d --build
```

Migrations apply on start; the data is kept.

## Organization directory

Real developers, banks and finance companies, imported from official sources. Safe to run any time: it never creates
duplicates, never changes records an administrator edited, and never deletes or deactivates anything.

```bash
C="docker compose -f deploy/compose.staging.yml --env-file deploy/.env"
$C run --rm api import-directory --source banks        # SAMA licensed banks
$C run --rm api import-directory --source finance      # SAMA licensed finance companies
$C run --rm api import-directory --source developers   # REGA qualified off-plan developers (paced, ~15 s per page)
$C run --rm api import-directory --source developers --pages 40-92   # resume where REGA stopped answering
$C run --rm -T api import-directory --file Seed/Data/rega-developers-2026-10-01.csv   # bundled REGA developer list (1,176 rows)
$C run --rm api import-directory --file /data/import/list.csv --dry-run   # another verified dataset file
```

Each run prints `discovered / created / updated / skipped / failed` and why items failed. REGA rate-limits requests
(it answers «Not Exist», sometimes even for the first page): the importer waits 1, 2 and 3 minutes and asks again, then
stops and prints the page to resume from. Results are saved at the end of each run, so import REGA in chunks
(`--pages 1-20`, `--pages 21-40`, …) and run them detached (`nohup … &`). Administrators manage the directory at `/team/organizations` (team lead).

## Backup and restore

The database is the whole backup (files included); keep the key ring with it.

```bash
C="docker compose -f deploy/compose.staging.yml --env-file deploy/.env"
# Backup
$C exec -T postgres pg_dump -U rahoon -d rahoon -Fc > rahoon-$(date +%F).dump
docker run --rm -v rahoon_apidata:/data -v "$PWD":/backup alpine tar czf /backup/rahoon-keys-$(date +%F).tgz -C /data keys
# Restore (stops the API and web first)
$C stop api web
$C exec -T postgres pg_restore -U rahoon -d rahoon --clean --if-exists < rahoon-YYYY-MM-DD.dump
docker run --rm -v rahoon_apidata:/data -v "$PWD":/backup alpine tar xzf /backup/rahoon-keys-YYYY-MM-DD.tgz -C /data
$C up -d
```

`PII_LOOKUP_KEY` in `deploy/.env` must be the one used when the backup was taken.

## Reset the demo data

Drops and recreates the database, then seeds the demo. The directory is emptied too: run the imports again afterwards.

```bash
cd /opt/Rahoon
docker compose -f deploy/compose.staging.yml --env-file deploy/.env stop api web
docker compose -f deploy/compose.staging.yml --env-file deploy/.env run --rm api reset-demo
docker compose -f deploy/compose.staging.yml --env-file deploy/.env up -d
```

## Notes

- **Open access.** With SMS confirmation off, sign-in has no second factor, and the demo passwords are published in the README. All marketplace data is fictional; don't enter real personal data.
- **Resource use.** The containers use a separate compose project (`rahoon`), their own network and named volumes (`rahoon_pgdata`, `rahoon_apidata`). Don't run `docker system prune` or `down -v` on this project unless you mean to delete the data.
- **Logs:** `docker compose -f deploy/compose.staging.yml --env-file deploy/.env logs --tail 200 api web`
