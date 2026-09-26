import { expect, type Page } from "@playwright/test";

export const PASSWORD = process.env.E2E_DEMO_PASSWORD ?? "Rahoon-Demo-2026!";

export const USERS = {
  sara: "s.alqahtani@alufuq.example",
  fahad: "f.alotaibi@alufuq.example",
  noura: "n.alshehri@alufuq.example",
  majed: "m.alharbi@alufuq.example",
  reem: "r.aldosari@alufuq.example",
  aziz: "a.alshammari@alufuq.example",
  salman: "s.alomari@alufuq.example",
  hind: "h.almutairi@alufuq.example",
} as const;

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

/** Signs in through the API (sandbox SMS code is echoed in Development) and picks the organization. */
export async function apiLogin(page: Page, email: string, organization = "مصرف الأفق") {
  await page.context().clearCookies();
  const login = await api<{ sandboxCode: string }>(page, "POST", "/auth/login", { email, password: PASSWORD });
  expect(login.status, "login").toBe(200);
  const mfa = await api<{ next: string }>(page, "POST", "/auth/mfa/verify", { code: login.json.sandboxCode });
  expect(mfa.status, "mfa").toBe(200);
  if (mfa.json.next === "/select-context") {
    const me = await api<{ memberships: Array<{ id: string; organization: string }> }>(page, "GET", "/auth/me");
    const m = me.json.memberships.find((x) => x.organization === organization) ?? me.json.memberships[0];
    const ctx = await api(page, "POST", "/auth/context", { membershipId: m.id });
    expect(ctx.status, "context").toBe(200);
  }
}

/** Completes the step-up dialog (sandbox code is shown in the dialog in Development). */
export async function completeStepUp(page: Page) {
  const dialog = page.getByRole("dialog", { name: "تأكيد برمز التحقق" });
  await dialog.getByRole("button", { name: "إرسال الرمز" }).click();
  const code = (await dialog.locator("bdi.font-mono").first().innerText()).trim();
  await dialog.getByRole("textbox", { name: "رمز من 6 أرقام" }).fill(code);
  await dialog.getByRole("button", { name: "تحقق" }).click();
}
