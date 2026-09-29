# Staging deployment: https://rahoon.talentfold.net

A demo server the client can use themselves. It runs the whole stack in Docker on the VPS (`173.249.37.177`) behind the host's nginx:

| Container | What | Reachable from |
|---|---|---|
| `rahoon-web-1` | Next.js (`next start`) | `127.0.0.1:8095` → nginx → `https://rahoon.talentfold.net` |
| `rahoon-api-1` | ASP.NET Core API, `ASPNETCORE_ENVIRONMENT=Staging` | the web container only (`http://api:8080`) |
| `rahoon-postgres-1` | PostgreSQL 16 | the API only (no published port) |

- **Settings:** `server/src/Rahoon.Api/appsettings.Staging.json` holds them.
- **First start:** it migrates the database and seeds the fictional demo data (the same logins as the README). Later starts keep the data.
- **SMS confirmation is off** (`SMS_CONFIRMATION=false`). No SMS is sent, and no code step appears anywhere: sign-in, consent, accepting an offer and the verifier's step-up confirm with one button.
  - Only turn it on once a real SMS gateway exists. The API only has a sandbox gateway, which delivers nothing, so with the flag on nobody could sign in.

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

# 4. nginx + TLS (only this site's file; nginx -t before reload)
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

New migrations apply on start, and the demo data is kept.

## Reset the demo data

Use this when the client has changed the seeded requests and you want every account back at its starting stage.

```bash
cd /opt/Rahoon
docker compose -f deploy/compose.staging.yml --env-file deploy/.env stop api web
docker compose -f deploy/compose.staging.yml --env-file deploy/.env run --rm api reset-demo
docker compose -f deploy/compose.staging.yml --env-file deploy/.env up -d
```

## Notes

- **Open access.** With SMS confirmation off, sign-in has no second factor, and the demo passwords are published in the README. Anyone with the URL can sign in. All data is fictional; don't enter real personal data.
- **Resource use.** The containers use a separate compose project (`rahoon`), their own network and their own named volumes (`rahoon_pgdata`, `rahoon_apidata`). Don't run `docker system prune` or `down -v` on this project unless you mean to delete the demo data.
- **Logs:** `docker compose -f deploy/compose.staging.yml --env-file deploy/.env logs --tail 200 api web`
