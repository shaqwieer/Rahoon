import { NextResponse, type NextRequest } from "next/server";

/**
 * Optimistic UX gate only — the API enforces authorization on every call.
 * 1. Protected prefixes without a `rahoon_sid` cookie → /login?next=… (owner pages → /access-denied?reason=owner;
 *    individual pages /my → /start?mode=signin&next=…).
 * 2. Every page request gets an `x-pathname` header so Server Components can build `?next=` redirects.
 */
const PROTECTED = [
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
  "/my",
  "/provider",
  "/agent",
  "/platform",
  "/team",
  "/select-context",
];

function matches(pathname: string, prefix: string) {
  return pathname === prefix || pathname.startsWith(`${prefix}/`);
}

export function proxy(request: NextRequest) {
  const { pathname, search } = request.nextUrl;
  const hasSession = Boolean(request.cookies.get("rahoon_sid")?.value);
  const prefix = PROTECTED.find((p) => matches(pathname, p));

  if (prefix && !hasSession) {
    const url = request.nextUrl.clone();
    url.search = "";
    if (prefix === "/owner") {
      url.pathname = "/access-denied";
      url.searchParams.set("reason", "owner");
    } else if (prefix === "/my") {
      url.pathname = "/start";
      url.searchParams.set("mode", "signin");
      url.searchParams.set("next", `${pathname}${search}`);
    } else {
      url.pathname = "/login";
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
