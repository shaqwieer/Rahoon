import { expect, test, type Browser, type Page } from "@playwright/test";
import { apiLogin } from "./helpers";

/**
 * Phase M1 definition of done (docs/phases/phase-m-exit-marketplace.md), through the real UI:
 * an owner sends a request → the team asks for completion → the owner completes and resends → the team approves and
 * prepares the opportunity → the owner confirms the summary → the team publishes → a buyer finds it and sends interest →
 * the team sees the interest. Plus: old routes are gone, and the public pages have no horizontal scroll at 390px.
 * Needs the API (Development, sandbox SMS codes) and the web dev server; the demo seed provides the team, and the
 * organization directory must hold at least one developer (`dotnet run -- import-directory`).
 */

const LEAD = "l.alharbi@team.rahoon.example";
const phone = () => `0568${String(Date.now()).slice(-6)}`;
const PNG = Buffer.from("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAIAAACQd1PeAAAADElEQVR4nGP4z8AAAAMBAQDJ/pLvAAAAAElFTkSuQmCC", "base64");

async function sandboxCode(page: Page) {
  const box = page.getByRole("note").filter({ has: page.locator("bdi.font-mono") }).last();
  return (await box.locator("bdi.font-mono").innerText()).trim();
}

/** The mobile sign-in component (PhoneSignIn), wherever it is embedded. */
async function phoneSignIn(page: Page, mobile: string, name?: string, submit = "دخول") {
  if (name) await page.getByLabel("الاسم").first().fill(name);
  await page.getByLabel("رقم الجوال").fill(mobile);
  await page.getByRole("button", { name: "متابعة", exact: true }).click();
  const code = await sandboxCode(page);
  await page.locator("input[autocomplete='one-time-code']").fill(code);
  await page.getByRole("checkbox", { name: /أوافق على/ }).check();
  await page.getByRole("button", { name: submit }).click();
}

/** Chooses the first directory match in an OrgPicker combobox. */
async function pickFirstOrg(page: Page, label: string, query: string) {
  const box = page.getByRole("combobox", { name: label });
  await box.fill(query);
  // Wait for a real directory match: before the directory has loaded, the only option is «… غير موجود في الدليل».
  await page.getByRole("listbox", { name: label }).getByRole("option", { name: new RegExp(query) }).first().click();
  await expect(page.getByRole("button", { name: "تغيير" })).toBeVisible();
}

async function teamPage(browser: Browser) {
  const ctx = await browser.newContext({ locale: "ar-SA", timezoneId: "Asia/Riyadh", viewport: { width: 1440, height: 900 } });
  const page = await ctx.newPage();
  await page.goto("/");
  await apiLogin(page, LEAD);
  return page;
}

test("sale request → completion → approval → opportunity → owner confirmation → publication → buyer interest", async ({ page, browser }) => {
  test.setTimeout(240_000);
  const ownerMobile = phone();

  // ── Owner: first request in three steps (signs in at step 3) ──
  await page.goto("/sell/new");
  await page.getByRole("radio", { name: "شقة" }).click();
  await page.getByLabel("المدينة").selectOption("riyadh");
  await page.getByLabel("الحي").fill("الملقا");
  await page.getByRole("radio", { name: "مطور عقاري" }).click();
  await pickFirstOrg(page, "اسم المطور", "شركة");
  await page.getByRole("button", { name: "التالي" }).click();

  await page.getByLabel("المدفوع المعتمد من ثمن الوحدة").fill("300000");
  await page.getByLabel("الرصيد المتبقي للمطور").fill("700000");
  await page.getByLabel("قيمة القسط").fill("12000");
  await page.getByRole("radio", { name: "شهري" }).click();
  await page.getByRole("radio", { name: "لا توجد", exact: true }).click();
  await page.getByRole("radio", { name: "توجد متأخرات", exact: true }).click();
  await page.getByLabel("إجمالي المتأخرات").fill("20000");
  await page.getByLabel("المبلغ الذي تطلبه لنفسك").fill("290000");
  await expect(page.getByText("نتيجة أولية تقديرية")).toBeVisible();
  await page.getByRole("button", { name: "التالي" }).click();

  await expect(page.getByText("محفوظ على هذا الجهاز فقط").first()).toBeVisible();
  await phoneSignIn(page, ownerMobile, "مالك اختبار آلي", "تحقق ومتابعة");
  await expect(page.getByLabel("الاسم")).toHaveValue("مالك اختبار آلي");
  await page.getByRole("radio", { name: "أنا صاحب العقار" }).click();
  await page.getByRole("checkbox", { name: /أقرّ بأنني صاحب العلاقة/ }).check();
  await page.getByRole("checkbox", { name: /أوافق على أن يعالج/ }).check();
  await page.getByRole("button", { name: "إرسال الطلب" }).click();
  await expect(page.getByRole("heading", { name: "استلمنا طلبك" })).toBeVisible();
  const sr = (await page.getByText(/SR-\d{4}-\d{5}/).first().innerText()).trim();

  // ── Team: review and ask for a specific completion ──
  const team = await teamPage(browser);
  await team.goto(`/team/sale/${sr}`);
  await team.getByRole("button", { name: "بدء المراجعة" }).click();
  await expect(team.getByText("قيد المراجعة").first()).toBeVisible();
  await team.getByRole("button", { name: "طلب استكمال" }).click();
  const dlg = team.getByRole("dialog");
  await dlg.getByRole("checkbox", { name: "صور العقار", exact: true }).check();
  await dlg.getByLabel("ملاحظة واضحة لصاحب الطلب").fill("نحتاج صور العقار للمتابعة.");
  await dlg.getByRole("button", { name: "إرسال لصاحب الطلب" }).click();
  await expect(team.getByText("يحتاج استكمال").first()).toBeVisible();

  // ── Owner: adds a photo in the same file (never a second request) and resends ──
  await page.goto(`/account/sell/${sr}`);
  await expect(page.getByText("نحتاج صور العقار للمتابعة.").first()).toBeVisible();
  // The property group opens first (location missing): place the pin on the map; autosave confirms.
  const map = page.getByRole("application", { name: /خريطة لتحديد موقع العقار/ });
  await expect(map).toHaveClass(/leaflet-container/);
  await map.click({ position: { x: 200, y: 150 } });
  await expect(page.locator(".leaflet-marker-icon")).toBeVisible();
  await expect(page.getByText("تم الحفظ في حسابك")).toBeVisible();
  await page.getByRole("button", { name: /الصور والمستندات/ }).click();
  await page.locator("input[type=file][accept^='image']").setInputFiles({ name: "front.png", mimeType: "image/png", buffer: PNG });
  await expect(page.getByAltText("صورة 1")).toBeVisible();
  await page.getByRole("button", { name: /المراجعة والإرسال للفريق/ }).click();
  await page.getByRole("button", { name: "إرسال الملف للمراجعة" }).click();
  await expect(page.getByText("أرسلت الملف للمراجعة.")).toBeVisible();

  // ── Team: accepts the photo, approves, prepares the opportunity ──
  await team.reload();
  await team.getByRole("button", { name: "قبول" }).first().click();
  await expect(team.getByText("مقبولة").first()).toBeVisible();
  await team.getByRole("button", { name: "اعتماد لإعداد فرصة" }).click();
  await team.getByRole("dialog").getByRole("button", { name: "اعتماد", exact: true }).click();
  await expect(team.getByText("معتمد لإعداد فرصة").first()).toBeVisible();
  await team.getByRole("button", { name: "تجهيز الفرصة" }).click();
  await team.waitForURL(/\/team\/opportunities\/OP-\d{4}-\d{5}/);
  const op = team.url().match(/OP-\d{4}-\d{5}/)![0];
  await team.getByLabel("الوصف (لا بيانات شخصية)").fill("شقة جاهزة في حي الملقا قريبة من الخدمات.");
  await Promise.all([team.waitForResponse((r) => r.url().includes("/content") && r.request().method() === "PUT"), team.getByRole("button", { name: "حفظ المحتوى" }).click()]);
  await team.getByLabel("تكاليف المشتري الآن").fill("15000");
  await team.getByLabel("تكاليف المشتري لاحقًا").fill("0");
  await team.getByLabel("تكاليف المالك").fill("0");
  await Promise.all([team.waitForResponse((r) => r.url().includes("/terms") && r.request().method() === "PUT"), team.getByRole("button", { name: "حفظ وحساب" }).click()]);
  await expect(team.getByText("325,000").first()).toBeVisible();
  await team.getByRole("button", { name: "إرسال الملخص للمالك" }).click();
  await expect(team.getByText("بانتظار تأكيد المالك").first()).toBeVisible();

  // ── Owner: confirms the summary (confirming does not publish) ──
  await page.goto(`/account/sell/${sr}/opportunity`);
  await page.getByRole("checkbox", { name: /أؤكد صحة الملخص/ }).check();
  await page.getByRole("button", { name: "تأكيد الملخص" }).click();
  await expect(page.getByText("جاهزة للنشر").first()).toBeVisible();
  const anon = await browser.newContext();
  const visitor = await anon.newPage();
  expect((await visitor.goto(`/opportunities/${op}`))?.status()).toBe(404);

  // ── Team: checklist, then publish ──
  await team.reload();
  for (const label of ["راجعنا علاقة صاحب العقار", "راجعنا الأرقام الجوهرية", "راجعنا الصور والموقع", "حددنا طريق الإتمام", "راجعنا موافقات المطور"]) {
    await team.getByRole("checkbox", { name: new RegExp(label) }).check();
  }
  await Promise.all([team.waitForResponse((r) => r.url().endsWith("/checklist")), team.getByRole("button", { name: "حفظ", exact: true }).click()]);
  await expect(team.getByRole("button", { name: "نشر الفرصة" })).toBeVisible();
  const [published] = await Promise.all([
    team.waitForResponse((r) => r.url().endsWith("/publish") && r.request().method() === "POST"),
    team.getByRole("button", { name: "نشر الفرصة" }).click(),
  ]);
  expect(published.status()).toBe(200);
  await expect(team.getByRole("link", { name: "الصفحة العامة" })).toBeVisible();

  // ── Visitor/buyer: the public page shows due-now first and no private data ──
  await visitor.goto(`/opportunities/${op}`);
  await expect(visitor.getByText("المطلوب منك الآن").first()).toBeVisible();
  await expect(visitor.getByText("325,000").first()).toBeVisible();
  await expect(visitor.getByText("مالك اختبار آلي")).toHaveCount(0);
  await visitor.goto("/signin?next=" + encodeURIComponent(`/opportunities/${op}`));
  await phoneSignIn(visitor, phone());
  await visitor.waitForURL(/\/account|\/opportunities\//);
  await visitor.goto(`/opportunities/${op}`);
  const box = visitor.locator("#interest");
  const nameField = box.getByLabel("اسمك");
  if (await nameField.count()) await nameField.fill("مشتري اختبار آلي");
  await box.getByRole("button", { name: "مهتم بالفرصة" }).click();
  await expect(box.getByText("أرسلت اهتمامك")).toBeVisible();

  // ── Team: sees the interest; the opportunity stays published (no reservation) ──
  await team.goto("/team/interests");
  await expect(team.getByText("مشتري اختبار آلي").first()).toBeVisible();
  await team.goto(`/team/opportunities/${op}`);
  await expect(team.getByText("منشورة").first()).toBeVisible();
  await anon.close();
  await team.context().close();
});

test("old portals are gone and only the Rahoon team signs in at /login", async ({ page }) => {
  for (const path of ["/my", "/cases", "/owner", "/portfolio", "/provider", "/agent", "/platform", "/team/requests", "/select-context", "/start"]) {
    const res = await page.goto(path);
    expect(res?.status(), path).toBe(404);
  }
  await page.goto("/login");
  await page.getByLabel("البريد الإلكتروني").fill("s.alqahtani@alufuq.example");
  await page.getByRole("textbox", { name: "كلمة المرور" }).fill(process.env.E2E_DEMO_PASSWORD ?? "Rahoon-Demo-2026!");
  await page.getByRole("button", { name: "متابعة" }).click();
  await expect(page.getByText("البريد أو كلمة المرور غير صحيحة.").first()).toBeVisible();
});

test("team administrator manages the organization directory", async ({ browser }) => {
  const team = await teamPage(browser);
  await team.goto("/team/organizations");
  await expect(team.getByRole("heading", { name: "دليل الجهات" })).toBeVisible();
  await team.getByRole("link", { name: /بنوك/ }).click();
  await expect(team.getByRole("table").getByText("مفعّلة").first()).toBeVisible();

  // Add a record manually, edit it, deactivate it with a reason, reactivate it.
  const name = `جهة اختبار آلي ${Date.now()}`;
  await team.getByRole("link", { name: "إضافة جهة" }).click();
  await team.getByLabel("الاسم بالعربية").fill(name);
  await team.getByRole("checkbox", { name: "شركة تمويل" }).check();
  await team.getByLabel("الموقع الرسمي").fill("example-finance.sa");
  await team.getByRole("button", { name: "إضافة الجهة" }).click();
  await expect(team.getByRole("heading", { name })).toBeVisible();
  await expect(team.getByText("إضافة يدوية")).toBeVisible();
  await team.getByLabel("الاسم بالإنجليزية").fill("Automated Test Finance");
  await team.getByRole("button", { name: "حفظ التعديلات" }).click();
  await expect(team.getByText("حُفظت التعديلات")).toBeVisible();
  await expect(team.getByText("محمية من الاستيراد")).toBeVisible();
  await team.getByRole("button", { name: "إيقاف الجهة" }).click();
  const dialog = team.getByRole("dialog", { name: "إيقاف الجهة" });
  await dialog.getByLabel("السبب").fill("اختبار الإيقاف");
  await dialog.getByRole("button", { name: "إيقاف" }).click();
  await expect(team.getByText("موقوفة — لا تظهر في النماذج")).toBeVisible();
  await team.getByRole("button", { name: "تفعيل الجهة" }).click();
  await expect(team.getByText("مفعّلة — تظهر في النماذج")).toBeVisible();

  // Search and filter find it.
  await team.goto(`/team/organizations?q=${encodeURIComponent(name)}&origin=manual`);
  await expect(team.getByRole("link", { name }).first()).toBeVisible();
  await team.context().close();
});

test("buyer can name a preferred financier from the directory", async ({ page }) => {
  await page.goto("/buy/new");
  await page.getByLabel("المبلغ المتاح لديك الآن").fill("150000");
  await page.getByRole("radio", { name: "بتمويل من جهة خارجية" }).click();
  await pickFirstOrg(page, "جهة التمويل التي تفضّلها", "بنك");
});

test.describe("phone width", () => {
  test.use({ viewport: { width: 390, height: 844 }, isMobile: true, hasTouch: true });
  for (const path of ["/", "/sell", "/sell/new", "/buy/new", "/opportunities", "/opportunities/OP-2026-00001", "/calculators", "/how-it-works", "/contact", "/signin"]) {
    test(`no horizontal scroll at 390px: ${path}`, async ({ page }) => {
      await page.goto(path);
      await page.waitForLoadState("networkidle");
      const overflow = await page.evaluate(() => document.documentElement.scrollWidth - window.innerWidth);
      expect(overflow, `${path} overflows by ${overflow}px`).toBeLessThanOrEqual(1);
      await page.screenshot({ path: `test-results/m390${path.replace(/\//g, "_") || "_home"}.png`, fullPage: true });
    });
  }
});

test.describe("phone width, signed in", () => {
  test.use({ viewport: { width: 390, height: 844 }, isMobile: true, hasTouch: true });

  async function noOverflow(page: Page, path: string) {
    await page.goto(path);
    await page.waitForLoadState("networkidle");
    const overflow = await page.evaluate(() => document.documentElement.scrollWidth - window.innerWidth);
    expect(overflow, `${path} overflows by ${overflow}px`).toBeLessThanOrEqual(1);
    await page.screenshot({ path: `test-results/m390-in${path.replace(/\//g, "_")}.png`, fullPage: true });
  }

  test("owner and buyer screens", async ({ page }) => {
    await page.goto("/");
    // Seeded demo owner (README «Demo logins»): mobile sign-in with the sandbox code.
    const start = await page.request.post("/api/auth/phone/start", { headers: { Origin: "http://localhost:3000", "Idempotency-Key": crypto.randomUUID() }, data: { phone: "0561110004" } });
    const { sandboxCode } = (await start.json()) as { sandboxCode: string };
    const verify = await page.request.post("/api/auth/phone/verify", { headers: { Origin: "http://localhost:3000", "Idempotency-Key": crypto.randomUUID() }, data: { code: sandboxCode, acceptTerms: true } });
    expect(verify.status()).toBe(200);
    for (const path of ["/account", "/account/sell", "/account/sell/SR-2026-00004", "/account/sell/SR-2026-00004/opportunity", "/account/buy", "/account/interests", "/account/saved"]) {
      await noOverflow(page, path);
    }
  });

  test("team screens", async ({ page }) => {
    await page.goto("/");
    await apiLogin(page, LEAD);
    for (const path of ["/team", "/team/sale", "/team/sale/SR-2026-00002", "/team/buyers/BR-2026-00002", "/team/opportunities/OP-2026-00001", "/team/interests/IN-2026-00001", "/team/messages", "/team/organizations", "/team/organizations/new"]) {
      await noOverflow(page, path);
    }
  });
});
