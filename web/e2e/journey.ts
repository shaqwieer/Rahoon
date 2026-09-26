import { expect, type Browser, type Page } from "@playwright/test";
import { apiLogin } from "./helpers";

/** Shared steps of the individual-first journey (owner-journey.spec.ts, responsive-qa.spec.ts). */

const shot = (page: Page, name: string) => page.screenshot({ path: `test-results/journey-${name}.png`, fullPage: true });
const digits = (n: number) => Array.from({ length: n }, () => Math.floor(Math.random() * 10)).join("");

export async function sandboxCode(page: Page): Promise<string> {
  const box = page.getByRole("note").filter({ has: page.locator("bdi.font-mono") }).last();
  return (await box.locator("bdi.font-mono").innerText()).trim();
}

/** OR01/OR02: register a fresh individual and land on /my. */
export async function registerIndividual(page: Page) {
  await page.goto("/start");
  await page.getByLabel(/رقم الهوية/).fill(`1${digits(9)}`);
  await page.getByLabel(/رقم الجوال/).fill(`05${digits(8)}`);
  await page.getByRole("button", { name: "إرسال رمز التحقق" }).click();
  const code = await sandboxCode(page);
  await page.locator("input[autocomplete='one-time-code'], input[inputmode='numeric']").first().fill(code);
  await page.getByRole("checkbox").first().check();
  await page.getByRole("button", { name: /إنشاء الحساب|تسجيل الدخول/ }).click();
  await page.waitForURL("**/my");
}

/** OA01–OA05 with consent, then submit; returns the REQ reference. */
export async function submitRequest(page: Page, screenshots = false): Promise<string> {
  await page.getByRole("button", { name: "ابدأ طلب معالجة جديد" }).click();
  await page.waitForURL(/\/my\/requests\/REQ-\d{4}-\d{5}\/apply/);
  const reference = page.url().match(/REQ-\d{4}-\d{5}/)![0];

  await page.getByRole("radio", { name: /مصرف الأفق/ }).check();
  if (screenshots) await shot(page, "05-oa01-lender");
  await page.getByRole("button", { name: "التالي" }).click();

  await page.waitForURL(/step=2/);
  await page.getByLabel("اسمك الكامل كما في الهوية").fill("عبدالله محمد السبيعي");
  await page.getByLabel("القسط الشهري الحالي").fill("4200");
  await page.getByRole("radio", { name: "من 3 إلى 6 أشهر" }).check();
  await page.getByLabel("مدينة العقار").fill("الرياض");
  if (screenshots) await shot(page, "06-oa02-finance");
  await page.getByRole("button", { name: "التالي" }).click();

  await page.waitForURL(/step=3/);
  await page.getByRole("radio", { name: /البقاء في منزلي/ }).check();
  await page.getByLabel(/ما الذي تغيّر؟/).fill("انخفض دخلي بعد تغيير العمل.");
  if (screenshots) await shot(page, "07-oa03-situation");
  await page.getByRole("button", { name: "التالي" }).click();

  await page.waitForURL(/step=4/);
  await page.locator("input[type=file]").first().setInputFiles({ name: "salary.pdf", mimeType: "application/pdf", buffer: Buffer.from("%PDF-1.4\n% e2e\n") });
  await expect(page.getByText("salary.pdf")).toBeVisible();
  await page.getByRole("checkbox", { name: "أوافق على النص أعلاه" }).check();
  await page.getByRole("button", { name: "تأكيد الموافقة برمز الجوال" }).click();
  const code = await sandboxCode(page);
  await page.locator("input[inputmode='numeric']").first().fill(code);
  await page.getByRole("button", { name: "تأكيد الموافقة" }).click();
  await expect(page.getByRole("heading", { name: "سُجّلت موافقتك" })).toBeVisible();
  if (screenshots) await shot(page, "08-oa04-docs-consent");
  await page.getByRole("button", { name: "التالي" }).click();

  await page.waitForURL(/step=5/);
  await expect(page.getByRole("heading", { name: "راجع طلبك قبل الإرسال" })).toBeVisible();
  if (screenshots) await shot(page, "09-oa05-review");
  await page.getByRole("button", { name: "إرسال الطلب" }).click();
  await page.waitForURL(/\/submitted$/);
  await expect(page.getByRole("heading", { name: "وصل طلبك إلى فريق رهون" })).toBeVisible();
  return reference;
}

/** A team member's page at 1440 (staff login through the API, as the lender specs do). */
export async function teamPage(browser: Browser, email: string): Promise<Page> {
  const ctx = await browser.newContext({ viewport: { width: 1440, height: 900 }, locale: "ar-SA", timezoneId: "Asia/Riyadh" });
  const page = await ctx.newPage();
  await page.goto("/login");
  await apiLogin(page, email, "فريق رهون");
  return page;
}

