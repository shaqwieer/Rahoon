import { expect, test } from "@playwright/test";

/**
 * iPhone Safari (WebKit) regressions: the search map loads tiles, markers and its card strip on the unfiltered page (the map
 * used to skip its first request there and, on iOS, to start before its box had a size), and key pages never scroll sideways.
 */
test("search map on an iPhone: tiles, markers and the card strip", async ({ page }) => {
  await page.goto("/opportunities");
  await page.evaluate(() => localStorage.removeItem("rahoon.discovery.view"));
  await page.reload();
  await page.getByRole("group", { name: "طريقة العرض" }).getByRole("button", { name: "الخريطة" }).click();
  await expect(page.locator(".leaflet-tile-loaded").first()).toBeVisible({ timeout: 20_000 });
  // Price markers are anchored on a zero-size point (the pill is drawn around it), so count them rather than test visibility.
  await expect.poll(() => page.locator(".leaflet-marker-icon").count()).toBeGreaterThan(0);
  const cards = page.locator("[data-mref]");
  await expect(cards.first()).toBeVisible();
  // Choosing a card highlights its marker (the selected marker is drawn on its own, dark).
  await cards.nth(0).locator("div").first().click();
  await expect(page.getByRole("button", { name: "ابحث في هذه المنطقة" })).toHaveCount(0);
});

for (const path of ["/opportunities", "/opportunities/OP-2026-00001", "/calculators", "/compare?refs=OP-2026-00001,OP-2026-00002", "/sell/new", "/signin"]) {
  test(`no sideways scroll on an iPhone: ${path}`, async ({ page }) => {
    await page.goto(path);
    await page.waitForLoadState("networkidle");
    const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
    expect(overflow, `${path} overflows by ${overflow}px`).toBeLessThanOrEqual(1);
  });
}
