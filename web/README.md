# رهون — web (Next.js 16 · React 19 · Tailwind v4)

Frontend of «رهون», the Saudi exit/buy platform: public pages and opportunity search, the owner/buyer account
(`/account`, mobile sign-in at `/signin`) and the Rahoon team workspace (`/team`, sign-in at `/login`). Arabic only, RTL.

> Next 16 differs from older Next versions. Read `node_modules/next/dist/docs/` before you use an API. Key differences:
> - `middleware.ts` is now **`src/proxy.ts`**, which exports `proxy`.
> - `cookies()`, `headers()`, `params` and `searchParams` are async.
> - Turbopack is the default bundler.
> - `next lint` is gone; run `eslint` instead.

## Run

```bash
npm install
npm run dev          # http://localhost:3000 (the API is expected on http://localhost:5080)
npm run build && npm start
npx eslint .         # lint
npx next typegen && npx tsc --noEmit   # typecheck (typegen creates PageProps/LayoutProps route types)
npx playwright test  # E2E: API and web running; the organization directory imported
```

| Env var | Default | Purpose |
|---|---|---|
| `API_ORIGIN` | `http://localhost:5080` | ASP.NET Core API origin, used by the `/api/:path*` rewrite and by server-side fetches. The browser never calls it directly. |

The API rejects unsafe requests whose `Origin` is not in `Web:AllowedOrigins`.

## Structure

```
src/app/
  (public)/        home, sell, buy, opportunities (search + details), calculators, how-it-works, contact, legal drafts, signin
  (auth)/login     Rahoon team sign-in (+ /login/mfa)
  account/         owner/buyer account: sale requests and their follow-up file, buyer request, interests, saved
  team/            team workspace: sale and buyer requests, opportunities, interests, messages, organizations (directory)
  access-denied/, error.tsx, not-found.tsx
src/components/
  market/          marketplace UI (wizards, file view, OrgPicker, cards, maps, calculators); market/team/* team views
  team/TeamShell   team navigation (filtered by the member's permissions)
  shell/           public header/footer, session actions
  ui/              shared primitives (Button, fields, Dialog, Alert, Toast, Icon, Logo, …)
src/lib/
  api/             server.ts (server-only), client.ts (browser), types.ts, guards.ts
  market/          types, catalog rules, formatting, directory lookup (client) and type labels
  i18n/            Arabic dictionary for shared components, server getLocale(), client I18nProvider/useI18n
src/proxy.ts       UX redirect when rahoon_sid is missing + x-pathname header
```

## API conventions

- **Same origin only.** `/api/:path*` is rewritten to the API, so `rahoon_sid` and `rahoon_csrf` stay first-party.
- **Server Components** use `src/lib/api/server.ts` (`apiGet`, `getMe`, `requireMe`): 401 → sign-in with `?next=`,
  403 → `/access-denied`, 404 → `notFound()`. Layout guards are UX only; **the API authorizes every call**.
- **Client code** uses `src/lib/api/client.ts`: `apiSend` (JSON, CSRF header on every call, `Idempotency-Key` on
  mutations) and `apiUpload`. Errors are `ApiError` with the RFC 7807 fields. Use one idempotency key per logical submit
  (`useIdempotencyKey`).
- **Files** are served by the API at `/api/files/{fileId}` (stable URL whatever the storage provider).
- **Organization directory:** `OrgPicker` loads `/api/directory/organizations?kind=developer|financier` once per page and
  searches it client-side (Arabic normalization like the server's); «غير موجودة في الدليل» lets the owner type a name.

## Conventions

- Logical properties only (`ps/pe/ms/me/start/end`); wrap LTR values (references, amounts, phones, URLs) in `<bdi dir="ltr">`.
- Palette tokens only (`rust`, `ink`, `muted`, `line`, `ok|warn|err|info`, …); orange is an accent only.
- One h1 per page. Hidden vs disabled: an action the member can never take is hidden; an ineligible one is shown disabled with its reason.
- Marketplace demo data is labelled «تجريبي»; the organization directory holds real organizations only.
