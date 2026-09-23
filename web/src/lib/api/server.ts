import "server-only";
import { cookies, headers } from "next/headers";
import { notFound, redirect } from "next/navigation";
import { cache } from "react";
import { ApiError, parseResponse, type Me, type MeAuthenticated } from "./types";

/** Server-side base URL of the API (never exposed to the browser). */
export const API_ORIGIN = process.env.API_ORIGIN ?? "http://localhost:5080";

/** Path + query of the current request, set by src/proxy.ts (Server Components cannot read the URL). */
export async function currentPath(): Promise<string> {
  return (await headers()).get("x-pathname") ?? "/";
}

export function loginUrl(next: string): string {
  return `/login?next=${encodeURIComponent(next)}`;
}

export interface ServerFetchInit {
  method?: "GET" | "POST" | "PUT" | "PATCH" | "DELETE";
  body?: unknown;
  headers?: Record<string, string>;
  signal?: AbortSignal;
}

/**
 * Raw call to `${API_ORIGIN}/api${path}` forwarding the browser's cookies (session + CSRF) and client hints.
 * Always `cache: 'no-store'` — authenticated data must never be shared between users.
 */
export async function apiFetch(path: string, init: ServerFetchInit = {}): Promise<Response> {
  const incoming = await headers();
  const cookieHeader = (await cookies()).toString();
  const outgoing: Record<string, string> = { accept: "application/json", ...init.headers };
  if (cookieHeader) outgoing.cookie = cookieHeader;
  const ua = incoming.get("user-agent");
  if (ua) outgoing["user-agent"] = ua;
  const fwd = incoming.get("x-forwarded-for");
  if (fwd) outgoing["x-forwarded-for"] = fwd;
  const lang = incoming.get("accept-language");
  if (lang) outgoing["accept-language"] = lang;
  let body: BodyInit | undefined;
  if (init.body !== undefined) {
    outgoing["content-type"] = "application/json";
    body = JSON.stringify(init.body);
  }
  return fetch(`${API_ORIGIN}/api${path}`, { method: init.method ?? "GET", headers: outgoing, body, cache: "no-store", signal: init.signal });
}

/**
 * GET for Server Components. 401 → /login?next=<current page>, 403 → /access-denied, 404 → notFound();
 * any other failure throws ApiError (caught by the nearest error.tsx).
 */
export async function apiGet<T>(path: string): Promise<T> {
  const res = await apiFetch(path);
  if (res.status === 401) redirect(loginUrl(await currentPath()));
  if (res.status === 403) redirect("/access-denied");
  if (res.status === 404) notFound();
  return parseResponse<T>(res);
}

/**
 * `GET /api/auth/me`, deduplicated per request. Returns `{ authenticated:false }` when signed out
 * (the endpoint answers 200, not 401) and also when the session cookie is missing — no API call then.
 */
export const getMe = cache(async (): Promise<Me> => {
  const jar = await cookies();
  if (!jar.get("rahoon_sid")?.value) return { authenticated: false };
  const res = await apiFetch("/auth/me");
  if (res.status === 401) return { authenticated: false };
  if (!res.ok) throw new ApiError(res.status, { title: `auth/me failed (${res.status})`, status: res.status });
  return (await res.json()) as Me;
});

/**
 * For portal layouts: guarantees an active, fully signed-in session or redirects to the right step
 * (login → MFA → workspace selection). Authorization itself is enforced by the API.
 */
export async function requireMe(): Promise<MeAuthenticated> {
  const me = await getMe();
  if (!me.authenticated) redirect(loginUrl(await currentPath()));
  if (me.stage === "mfapending") redirect(`/login/mfa?next=${encodeURIComponent(await currentPath())}`);
  if (me.scope === "none") redirect("/select-context");
  return me;
}

/** Sends the user to their own home when they are in the wrong portal; guards against redirect loops. */
export function redirectToHome(me: MeAuthenticated, currentPortalPrefix: string): never {
  const home = me.home || "/access-denied";
  if (home === currentPortalPrefix || home.startsWith(`${currentPortalPrefix}/`)) redirect("/access-denied");
  redirect(home);
}

/** True when the signed-in member holds the permission (UI hint only; the API is the authority). */
export function can(me: MeAuthenticated, permission: string): boolean {
  return me.permissions.includes(permission);
}
