import { NextResponse, type NextRequest } from "next/server";

/**
 * Optimistic UX gate only — the API enforces authorization on every call.
 * 1. Protected prefixes without a `rahoon_sid` cookie → sign-in with `?next=` (the account → /signin, the team → /login).
 * 2. Every page request gets an `x-pathname` header so Server Components can build `?next=` redirects.
 */
const PROTECTED: { prefix: string; signIn: string }[] = [
  { prefix: "/account", signIn: "/signin" },
  { prefix: "/team", signIn: "/login" },
];

function matches(pathname: string, prefix: string) {
  return pathname === prefix || pathname.startsWith(`${prefix}/`);
}

export function proxy(request: NextRequest) {
  const { pathname, search } = request.nextUrl;

  const hasSession = Boolean(request.cookies.get("rahoon_sid")?.value);
  const gate = PROTECTED.find((p) => matches(pathname, p.prefix));
  if (gate && !hasSession) {
    const url = request.nextUrl.clone();
    url.search = "";
    url.pathname = gate.signIn;
    url.searchParams.set("next", `${pathname}${search}`);
    return NextResponse.redirect(url);
  }

  const requestHeaders = new Headers(request.headers);
  requestHeaders.set("x-pathname", `${pathname}${search}`);
  return NextResponse.next({ request: { headers: requestHeaders } });
}

export const config = {
  // Skip API rewrites, Next internals and static files (anything with an extension).
  matcher: ["/((?!api/|_next/static|_next/image|brand/|.*\.[a-zA-Z0-9]+$).*)"],
};
