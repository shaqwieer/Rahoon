import { expect, type Page } from "@playwright/test";

export const PASSWORD = process.env.E2E_DEMO_PASSWORD ?? "Rahoon-Demo-2026!";

const ORIGIN = () => process.env.E2E_BASE_URL?.replace(/\/[^/]*$/, "") ?? "http://localhost:3000";

async function csrf(page: Page) {
  const cookies = await page.context().cookies();
  return cookies.find((c) => c.name === "rahoon_csrf")?.value ?? "";
}

/** Calls the API through the web origin (same cookies the browser uses), like lib/api/client.ts does. */
export async function api<T = unknown>(page: Page, method: "GET" | "POST" | "PUT" | "PATCH", path: string, body?: unknown): Promise<{ status: number; json: T }> {
  const headers: Record<string, string> = { Origin: ORIGIN(), "X-CSRF-Token": await csrf(page) };
  if (method !== "GET") headers["Idempotency-Key"] = crypto.randomUUID();
  const res = await page.request.fetch(`/api${path}`, { method, headers, data: body ?? (method === "GET" ? undefined : {}) });
  const text = await res.text();
  return { status: res.status(), json: (text ? JSON.parse(text) : null) as T };
}

/** Signs a Rahoon team member in through the API (the sandbox SMS code is echoed in Development). */
export async function apiLogin(page: Page, email: string) {
  await page.context().clearCookies();
  const login = await api<{ sandboxCode: string }>(page, "POST", "/auth/login", { email, password: PASSWORD });
  expect(login.status, "login").toBe(200);
  const mfa = await api<{ next: string }>(page, "POST", "/auth/mfa/verify", { code: login.json.sandboxCode });
  expect(mfa.status, "mfa").toBe(200);
}
