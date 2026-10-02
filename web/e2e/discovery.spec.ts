import { expect, test, type Page } from "@playwright/test";
import { pick } from "./helpers";

/**
 * Phase 2 (docs/rahoon/roadmap/phase-2-discovery-matching.md) through the real UI: the URL is the search state (reload and
 * back/forward give the same list and map set), a slow earlier response never overwrites a newer search, comparison, saved
 * searches, and phone width. Needs the API and the web dev server with the demo seed (published OP-2026-00001/2/5).
 */

const ORIGIN = "http://localhost:3000";

async function signInByPhone(page: Page, mobile: string) {
  await page.goto("/");
  const start = await page.request.post("/api/auth/phone/start", { headers: { Origin: ORIGIN, "Idempotency-Key": crypto.randomUUID() }, data: { phone: mobile, name: "مشتري اكتشاف آلي" } });
  const { sandboxCode } = (await start.json()) as { sandboxCode: string };
  const verify = await page.request.post("/api/auth/phone/verify", { headers: { Origin: ORIGIN, "Idempotency-Key": crypto.randomUUID() }, data: { code: sandboxCode, acceptTerms: true } });
  expect(verify.status()).toBe(200);
}

const cardRefs = (page: Page) => page.locator("article[data-ref]").evaluateAll((els) => els.map((e) => e.getAttribute("data-ref")!));

async function mapRefs(page: Page, query: string) {
  const r = await page.request.get(`/api/market/opportunities/map?${query}`);
  return ((await r.json()) as { markers: { reference: string }[] }).markers.map((m) => m.reference);
}

test("the URL restores the same list and map set, and back/forward follow it", async ({ page }) => {
  await page.goto("/opportunities?maxNow=400000&city=riyadh");
  await page.evaluate(() => localStorage.removeItem("rahoon.discovery.view"));
  await page.reload();
  const first = await cardRefs(page);
  expect(first.length).toBeGreaterThan(0);
  // The list/map switch works on desktop too; the map shows the same filtered set.
  await page.getByRole("button", { name: "الخريطة" }).click();
  await expect(page.locator(".leaflet-container")).toBeVisible();
  const markers = await mapRefs(page, "city=riyadh&maxNow=400000");
  for (const ref of first) expect(markers).toContain(ref);
  await page.getByRole("button", { name: "القائمة" }).click();

  await page.reload();
  expect(await cardRefs(page)).toEqual(first);
  // Wait for hydration before using the controls (the view switch answers only once React is attached).
  await page.getByRole("button", { name: "الخريطة" }).click();
  await expect(page.locator(".leaflet-container")).toBeVisible();
  await page.getByRole("button", { name: "القائمة" }).click();

  // A filter change goes into the URL (page 1), and Back restores the earlier search.
  await pick(page, "المدينة", "جدة");
  await expect(page).toHaveURL(/city=jeddah/);
  await expect.poll(async () => (await cardRefs(page)).join(",")).not.toBe(first.join(","));
  await page.goBack();
  await expect(page).toHaveURL(/city=riyadh/);
  await expect(page.getByLabel("المدينة", { exact: true })).toContainText("الرياض");
  await expect.poll(async () => (await cardRefs(page)).join(",")).toBe(first.join(","));
});

test("a slow earlier response never overwrites a newer search", async ({ page }) => {
  await page.goto("/opportunities");
  await expect(page.locator("article[data-ref]").first()).toBeVisible();
  // The villa search answers late; the apartment search asked after it must win.
  await page.route(/\/api\/market\/opportunities\?.*types=villa/, async (route) => {
    await new Promise((r) => setTimeout(r, 3500));
    await route.continue().catch(() => undefined);
  });
  await page.getByLabel("نوع العقار", { exact: true }).click();
  await page.getByRole("option", { name: "فيلا" }).click();
  await expect(page).toHaveURL(/types=villa/);
  await page.getByRole("option", { name: "فيلا" }).click();
  await page.getByRole("option", { name: "شقة" }).click();
  await page.keyboard.press("Escape");
  await expect(page).toHaveURL(/types=apartment(?!,)/);
  await page.waitForTimeout(4500); // the late villa answer has arrived (or was aborted) by now
  const cards = page.locator("article[data-ref]");
  expect(await cards.count()).toBeGreaterThan(0);
  for (const text of await cards.allInnerTexts()) expect(text).toContain("شقة");
});

test("comparison shows unknown and not-applicable cells instead of zeros", async ({ page }) => {
  await page.goto("/compare?refs=OP-2026-00001,OP-2026-00002,OP-2026-00005,OP-2099-99999");
  const table = page.getByRole("table");
  await expect(table).toBeVisible();
  await expect(table.getByText("لا ينطبق").first()).toBeVisible();      // bank track: no developer installment
  await expect(table.getByText("غير معروف بعد").first()).toBeVisible(); // OP-2026-00005: cash now unknown
  await expect(table.getByText("لم تعد متاحة")).toBeVisible();          // the unknown reference reveals nothing
  // Picking from the list: the tray counts and opens the comparison.
  await page.goto("/opportunities");
  await page.evaluate(() => localStorage.removeItem("rahoon.compare.v1"));
  await page.reload();
  const toggles = page.getByRole("button", { name: "قارن", exact: true });
  await toggles.nth(0).click();
  await toggles.nth(0).click(); // the first one now reads «في المقارنة»; nth(0) is the next card
  await expect(page.getByRole("region", { name: "المقارنة" })).toContainText("2");
  await page.getByRole("link", { name: "قارن الآن" }).click();
  await expect(page).toHaveURL(/\/compare\?refs=/);
  await expect(page.getByRole("table").locator("thead th")).toHaveCount(3);
});

test("a signed-in buyer saves a search with alerts and manages it", async ({ page }) => {
  await signInByPhone(page, `0567${String(Date.now()).slice(-6)}`);
  await page.goto("/opportunities?city=riyadh&maxNow=400000");
  await page.getByRole("button", { name: "احفظ هذا البحث" }).click();
  const dialog = page.getByRole("dialog", { name: "حفظ البحث" });
  await dialog.getByLabel("اسم البحث").fill("شقق الرياض الآلية");
  await dialog.getByRole("checkbox", { name: /أوافق على أن ترسل/ }).check();
  await dialog.getByRole("button", { name: "حفظ", exact: true }).click();
  await expect(dialog.getByText("حفظنا البحث في حسابك.")).toBeVisible();
  await dialog.getByRole("link", { name: "عمليات البحث المحفوظة" }).click();
  await expect(page).toHaveURL(/\/account\/searches/);
  await expect(page.getByRole("heading", { name: "شقق الرياض الآلية" })).toBeVisible();
  await expect(page.getByText("التنبيهات مفعلة")).toBeVisible();
  await page.getByRole("button", { name: "إيقاف مؤقت" }).click();
  await expect(page.getByText("التنبيهات موقوفة")).toBeVisible();
  await page.getByRole("link", { name: "فتح النتائج" }).click();
  await expect(page).toHaveURL(/city=riyadh/);
  await expect(page).toHaveURL(/maxNow=400000/);
});

test.describe("phone width (discovery)", () => {
  test.use({ viewport: { width: 390, height: 844 }, isMobile: true, hasTouch: true });

  async function noOverflow(page: Page, path: string, shot: string) {
    await page.goto(path);
    await page.waitForLoadState("networkidle");
    const overflow = await page.evaluate(() => document.documentElement.scrollWidth - window.innerWidth);
    expect(overflow, `${path} overflows by ${overflow}px`).toBeLessThanOrEqual(1);
    await page.screenshot({ path: `test-results/p2-390-${shot}.png`, fullPage: true });
  }

  test("public discovery screens", async ({ page }) => {
    await noOverflow(page, "/opportunities?maxNow=400000", "list");
    await page.getByRole("button", { name: "الخريطة" }).click();
    await expect(page.locator(".leaflet-container")).toBeVisible();
    const overflow = await page.evaluate(() => document.documentElement.scrollWidth - window.innerWidth);
    expect(overflow).toBeLessThanOrEqual(1);
    await page.screenshot({ path: "test-results/p2-390-map.png" });
    await noOverflow(page, "/compare?refs=OP-2026-00001,OP-2026-00002,OP-2026-00005", "compare");
    await noOverflow(page, "/calculators", "calculators");
  });

  test("signed-in discovery screens", async ({ page }) => {
    await signInByPhone(page, "0561110012"); // seeded buyer with a buyer request
    await noOverflow(page, "/opportunities?match=me", "match-me");
    await noOverflow(page, "/account/buy", "account-buy");
    await noOverflow(page, "/account/searches", "account-searches");
    await noOverflow(page, "/account/saved", "account-saved");
  });
});
