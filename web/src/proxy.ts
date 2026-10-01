import { NextResponse, type NextRequest } from "next/server";

/**
 * Optimistic UX gate only — the API enforces authorization on every call.
 * 1. Routes of the withdrawn mortgage-default model answer 404 unless RAHOON_LEGACY_MODES=1
 *    (docs/redefinition/legacy-inventory.md). Their code is archived behind the flag; the API does the same.
 * 2. Protected prefixes without a `rahoon_sid` cookie → sign-in with `?next=` (the account → /signin, the team → /login).
 * 3. Every page request gets an `x-pathname` header so Server Components can build `?next=` redirects.
 */
const LEGACY_ENABLED = process.env.RAHOON_LEGACY_MODES === "1";

/** Old portals: lender workspace, owner invitation portal, mortgage-help requests, providers, judicial agent, platform admin. */
const LEGACY = [
  "/portfolio",
  "/cases",
  "/tasks",
  "/approvals",
  "/complaints",
  "/reports",
  "/analytics",
  "/settings",
  "/notifications",
  "/search",
  "/profile",
  "/help",
  "/owner",
  "/invite",
  "/my",
  "/provider",
  "/agent",
  "/platform",
  "/print",
  "/select-context",
  "/team/requests",
  "/team/verify",
  "/team/objections",
  "/dev",
];

const PROTECTED: { prefix: string; signIn: string }[] = [
  { prefix: "/account", signIn: "/signin" },
  { prefix: "/team", signIn: "/login" },
  // Legacy portals keep their old gate when the flag is on.
  ...(LEGACY_ENABLED ? LEGACY.map((prefix) => ({ prefix, signIn: prefix === "/my" ? "/start" : "/login" })) : []),
];

function matches(pathname: string, prefix: string) {
  return pathname === prefix || pathname.startsWith(`${prefix}/`);
}

export function proxy(request: NextRequest) {
  const { pathname, search } = request.nextUrl;

  if (!LEGACY_ENABLED) {
    if (LEGACY.some((p) => matches(pathname, p))) {
      // Rendered by app/not-found.tsx with a 404 status; nothing of the old model is served.
      return NextResponse.rewrite(new URL("/__withdrawn", request.url));
    }
    // The old individual sign-in moved to the mobile-first sign-in.
    if (pathname === "/start") {
      const url = request.nextUrl.clone();
      url.pathname = "/signin";
      return NextResponse.redirect(url);
    }
  }

  const hasSession = Boolean(request.cookies.get("rahoon_sid")?.value);
  const gate = PROTECTED.find((p) => matches(pathname, p.prefix));
  if (gate && !hasSession) {
    const url = request.nextUrl.clone();
    url.search = "";
    if (gate.prefix === "/owner") {
      url.pathname = "/access-denied";
      url.searchParams.set("reason", "owner");
    } else {
      url.pathname = gate.signIn;
      url.searchParams.set("next", `${pathname}${search}`);
    }
    return NextResponse.redirect(url);
  }

  const requestHeaders = new Headers(request.headers);
  requestHeaders.set("x-pathname", `${pathname}${search}`);
  return NextResponse.next({ request: { headers: requestHeaders } });
}

export const config = {
  // Skip API rewrites, Next internals and static files (anything with an extension).
  matcher: ["/((?!api/|_next/static|_next/image|brand/|.*\\.[a-zA-Z0-9]+$).*)"],
};
