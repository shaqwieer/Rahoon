# رهون — web (Next.js 16 · React 19 · Tailwind v4)

Frontend for «رهون»: a Saudi platform that coordinates distressed mortgage cases between lenders, property owners, service providers, judicial agents and platform admins. Arabic RTL is the default and English LTR is fully supported by the infrastructure.

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
```

| Env var | Default | Purpose |
|---|---|---|
| `API_ORIGIN` | `http://localhost:5080` | ASP.NET Core API origin. It is used by the `/api/:path*` rewrite and by server-side fetches. The browser never calls it directly. |

**API prerequisite:** the API rejects unsafe requests whose `Origin` is not allowed (`Web:AllowedOrigins` in the API config). Add the Next origin (e.g. `http://localhost:3000`) there, or every POST fails with 403 `origin`.

The dev component gallery is at **`/dev/ui`**. It returns 404 in production builds.

Development-only SMS codes: when the API returns `sandboxCode`, the login, MFA and owner OTP screens show it in a box labelled «بيئة تجريبية — الرسائل النصية محاكاة ولا تُرسل: الرمز …».

## Structure

```
public/brand/            supplied logo + icon artwork (byte-identical copies; never recolour / CSS-filter)
src/app/
  layout.tsx             <html lang dir>, IBM Plex fonts (next/font), Material Symbols <link>, I18n + Toast providers
  globals.css            tokens.css variables on :root → Tailwind @theme (palette only), base styles, .ms icons
  icon.svg               app icon (rahoon-favicon.svg)
  (public)/              S01 landing (temporary hero) — indexable
  (auth)/                login, login/mfa, select-context, invite/[token], invite/[token]/verify
  (lender)/              LenderShell layout; portfolio, cases, tasks, approvals, complaints, reports,
                         notifications, search, profile, help, settings (+SettingsNav) — placeholders for now
  owner/ provider/ agent/ platform/   portal layouts (scope/kind guarded) + placeholder pages
  access-denied/         S12 (same wording for not-found and forbidden)
  dev/ui/                component gallery (dev only)
  error.tsx, not-found.tsx
src/components/ui/       component library (barrel: @/components/ui)
src/components/shell/    shells: Lender, Settings, CaseHeader, Owner (DebtorTop/DebtorNav/desktop header),
                         Provider/Agent (PortalShell), Platform, Public header/footer, AuthSplit, LocaleSwitch
src/lib/
  api/                   server.ts (server-only), client.ts (browser), types.ts (shared), guards.ts, owner.ts
  i18n/                  config, ar/en dictionaries, server getLocale(), client I18nProvider/useI18n
  format.ts              pure money / number / date / Hijri / percent / SLA helpers
  hooks.ts               useNow (hydration-safe clock), sessionStorage hand-off
  navigation.ts          hardNavigate (full reload after auth / context changes)
src/proxy.ts             UX redirect when rahoon_sid is missing + x-pathname header
```

## API conventions

- **Same origin only.** `next.config.ts` rewrites `/api/:path*` to `${API_ORIGIN}/api/:path*`, so the `rahoon_sid` and `rahoon_csrf` cookies stay first-party.
- **Server Components** use `src/lib/api/server.ts`:
  - `apiGet<T>(path)` calls `${API_ORIGIN}/api${path}` with the incoming cookies and `cache: 'no-store'`. It handles failures as follows:
    - 401 → `/login?next=<current path>` (the path comes from the `x-pathname` header set by the proxy)
    - 403 → `/access-denied`
    - 404 → `notFound()`
    - anything else → throws `ApiError`, which `error.tsx` shows with an `ERR-xxxx` reference
  - `getMe()` wraps `GET /api/auth/me` and is cached per request with React `cache`. This endpoint answers 200 `{authenticated:false}` when the user is signed out, and `getMe()` passes that through.
  - `requireMe()` sends the user to the step they still need: login, then MFA (`stage: mfapending`), then `/select-context` (`scope: none`).
  - The portal layouts call `requireOrgPortal(kind, prefix)` or `requireOwnerPortal()`. When the kind or scope is wrong, these redirect to `me.home`, with a loop guard that sends the user to `/access-denied` instead.
  - **All of this is UX only. The API authorizes every call.**
- **Client code** uses `src/lib/api/client.ts`:
  - `apiSend<T>(method, path, body?, { idempotencyKey?, signal? })` sends JSON with `credentials: 'same-origin'`.
    - It reads the `rahoon_csrf` cookie on **every** call and sends it as `X-CSRF-Token`. The token rotates after MFA, a context switch and owner sign-in.
    - Every mutation sends an `Idempotency-Key`.
  - `apiUpload(path, formData)` sends multipart uploads.
  - Errors are thrown as `ApiError`, which carries the RFC 7807 fields `{ status, code, title, errors, reasons, remainingAttempts, lockedUntil, minutes }`. Branch on `error.code`.
    - The client never redirects by itself: a 401 on a login form is a validation outcome, not an expired session.
    - `code: "network" | "offline"` means the request never completed.
    - `code: "unavailable"` means the proxy or API returned a non-JSON 5xx.
- **Idempotency.** Use one key per *logical* submit. Get it with `const key = useIdempotencyKey()`, then call `apiSend(..., { idempotencyKey: key.get() })`, and call `key.reset()` after success or when the input changes. A retry after a network error then reuses the same key, so the API replays the stored result instead of acting twice.
- **Auth flows** (the screens are fully functional against the API):
  - **Login → MFA.** The login response (masked destination, resend time, dev sandbox code) is handed to the MFA step through `sessionStorage` and never through the URL. `?next=` is carried through and sanitized with `safeNext`.
  - **After MFA, a context switch or logout** the app does a full navigation with `hardNavigate`, so no cache crosses tenants.
  - **Owner sign-in:** `/invite/[token]` → `/invite/[token]/verify` (the last 4 digits of the ID, then the SMS code) → `/owner`.

## i18n and bidi

- **Locale** comes from the `rahoon_locale` cookie (`ar` by default). The server reads it with `getLocale()` in `@/lib/i18n/server`, and the root layout sets `<html lang dir>`.
- **Switching language:** `<LocaleSwitch variant="button|link|inverse">` sets the cookie and calls `router.refresh()`.
- **Strings** live in `src/lib/i18n/dictionaries/{ar,en}.ts`. `en` is type-checked against the shape of `ar`. Read them with `useI18n()` in client code or `getServerDictionary()` on the server.
- **Isolate LTR values.** Wrap every reference, amount, date, phone number, email and ID in `<bdi dir="ltr">`. The helpers do this for you: `<Money>`, `<DateText>`, `<Ref>`, `<Ltr>`, `<Percent>`. References and codes use IBM Plex Mono.
- **Logical properties only** (`ps/pe/ms/me/start/end`, `border-s`, `text-start`).
  - The orange active marker is the `bar-start` utility.
  - Icons drawn for RTL flip in LTR with `<Icon mirror>` or the `.ms-mirror` class. This covers arrows, chevrons and progress. Clock, check and currency icons are never mirrored.
  - Arrow-key navigation follows the reading direction: ArrowLeft means "next" in RTL.
  - The Arabic logo stays unchanged in the English UI; only its alt text changes.
- **Formatting** (`src/lib/format.ts`):
  - Digits are Western by default (`ar-SA-u-nu-latn`). Pass `numerals: 'arab'`, taken from `/me` `user.numerals`, to get Arabic-Indic digits. The shells feed this preference into `I18nProvider`.
  - Money: `formatMoney(1284560)` → `1,284,560.00`. Add `withUnit` for «ر.س», or `compact` for `1.62 مليار`.
  - Dates are shown in Riyadh time: `formatDate` → `2026-09-23` and `formatDateTime` → `2026-09-23 10:12`.
  - `formatHijri` gives the Umm al-Qura date, e.g. `12 ربيع الآخر 1448هـ`.
  - Also available: `formatPercent` (value in percent units, e.g. 46.5 → `46.5%`) and `slaText({dueAt, now})` → `{tone, text}`.

## Component library (`@/components/ui`)

| Component | Notes |
|---|---|
| `Button` | Variants: `primary \| secondary \| text \| sensitive \| strong \| inverse`. Sizes: `sm 36 \| md 40 \| lg 48 \| xl 54`.<br>Also: `loading` (keeps its width, sets `aria-busy`), `review` (adds the «…» label), `softDisabled` (`aria-disabled` and still focusable), `href` (renders a Link), `icon` / `iconEnd`.<br>`buttonClasses()` gives the same look to links rendered by Server Components. |
| `IconButton` | `label` is required. Also: `badge`, `dot`, `href`, `mirror`. |
| `Icon` | Material Symbols Rounded ligatures. Decorative by default (`aria-hidden`). `label` makes it meaningful; `mirror` flips it in LTR. |
| `Logo` | `variant`: `horizontal \| horizontal-dark \| stacked \| stacked-dark \| symbol \| app-icon`. Minimum sizes from the brand README: horizontal 170, stacked 100, symbol 24, tile 48. |
| `StatusChip` | 16 case states (`CASE_STATES`, keys such as `proposed_solution`). |
| `SubStatusTag` | Square tag with a kind prefix. |
| `SlaBadge` | Tones: `ok / warn / err / info / paused`. |
| `IntegrationStateTag` | `enabled / simulated / pending / unavailable / failed`. |
| `Tag` | General-purpose tag. |
| `StageProgress` | Variants: `responsive / full / header / compact` («المرحلة n من 7»). The current stage gets `aria-current="step"`. |
| `NextActionCard` | `state`: `available / blocked / not-yours`. In the blocked state the reason is linked to the disabled button with `aria-describedby`. |
| `Alert` | Tones: `info / ok / warn / err / neutral`. Role is `status` or `alert`. |
| `SandboxCodeBox` | Shows the development-only SMS code. |
| `ToastProvider` / `useToast` | 6 s, pauses on hover or focus, `role=status`, optional undo action. |
| Field family | `TextField` (`ltr`, `mono`, `prefix`, `endAdornment`, `invalid`), `Textarea` (counter), `AmountField`, `PercentField`, `MaskedValue` (audited `onReveal`), `DateField` (native input with a Hijri caption), `Select`, `Checkbox`, `RadioCardGroup` (options can be disabled with a reason), `OtpInput` (one input with `autocomplete=one-time-code`, drawn as 6 boxes). |
| `ErrorSummary` | `role=alert`, takes focus, links to each field. |
| `DocumentItem` / `DocumentList` / `UploadDropzone` | C07 document statuses. |
| `ApprovalChain` / `VersionDiff` | C08. |
| `CaseTable` / `CaseCard` | From 768px up: an ARIA grid (arrow keys move, Enter opens, Space selects). Below 768px: cards. |
| `AuditTimeline` | C10. Gregorian and Hijri times; blocked transitions are listed too. |
| `SystemState` / `EmptyState` / `Skeleton` | C11 states: loading, empty, error (with an ERR ref), offline, forbidden, success. |
| `Tabs` | `role=tablist` with roving focus. Supports route tabs (`href`) or in-place tabs, and count badges. |
| `FilterChip` | Variants: `active / menu / action`. |
| `Pagination` | «عرض 1–10 من 38». |
| `Menu` | `role=menu`, keyboard support, `menuitemradio` items. |
| `KpiTile` | KPI tile. |
| `BarList` | C14 bar chart. Every bar carries its text value, and «عرض كجدول» switches to a real table. |
| `KeyValueList`, `Avatar` | Key–value rows; initials avatar. |
| `Dialog` / `Drawer` | Native `<dialog>`. Focus is trapped, Esc closes, and focus returns to the opener. |
| `SkipLink` | Skip-to-content link. |
| `Money` / `DateText` / `Ref` / `Ltr` / `Percent` | Bidi-safe value helpers. |
| `DecisionSupportTag` | Shows range, confidence, factors, source and model, limitations, and the human opinion. |
| `ReviewScreen` / `ReviewSection` | Numbered sections «ما سيحدث / الأدلة / السبب والإقرار». Sticky footer; the primary button stays disabled until the form is complete. |

Shells (`@/components/shell/*`):

| Shell | Contents |
|---|---|
| `LenderShell` | Composed of `LenderSidebar` and `LenderTopbar`, plus the mobile top bar, bottom nav and «المزيد» sheet, and a command palette (click or Ctrl/⌘ K). |
| `SettingsLayout` / `SettingsNav` | Settings area layout and navigation. |
| `CaseHeader` | Case header. |
| `OwnerShell` | Composed of `DebtorTop`, `DebtorNav` and `OwnerDesktopHeader`. |
| `ProviderShell` / `AgentShell` | Service-provider and judicial-agent portals. |
| `PlatformShell` / `PlatformSidebar` | Platform administration. |
| `PublicHeader` / `PublicFooter` | Public pages. |
| `AuthSplit` | Split layout for the auth screens. |
| `ScreenPlaceholder` / `PageHeader` | Placeholder pages and page titles. |

Responsive rules:

- **Lender sidebar:** 264px at 1280px and wider; a 72px icon rail with tooltips from 768px to 1279px; below 768px a bottom nav replaces it.
- **Owner portal:** mobile-first; from 1024px, a top header and a centered 1040px column.

## Conventions

- **Palette only.** Tailwind's default colours are cleared; use the token utilities: `rust`, `rust-700`, `rust-50`, `orange`, `ink`, `charcoal`, `warm`, `subtle`, `divider`, `track`, `line`, `line-strong`, `muted`, `soft`, `gold`, and `ok|warn|err|info` with `-bg` and `-line` variants, plus the `inv*` tokens.
  - Orange is an accent only: never use it for small text or behind white text.
  - Put `surface-dark` on dark containers so focus rings turn white.
- **One h1 per page.** App pages use `PageHeader` or `CaseHeader`. The design itself shows no h1 on app pages; in the code, the page title is the h1.
- **Hidden vs disabled.** If the role lacks a permission, the item is hidden (navigation is filtered by `permissions` from `/me`). If the role has the permission but the case is not eligible, the item is shown disabled with its reason.
- **No fake data in business screens.** Placeholder pages show only the shell and an empty state. Replace a page file when its screen is built. A specific route such as `settings/users/page.tsx` takes precedence over `settings/[section]`.
