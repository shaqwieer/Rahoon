import { expect, test, type Page } from "@playwright/test";

/**
 * Primary MVP journey (individual-first, Phase 1A): on a 390px phone the individual registers, fills the request
 * wizard with documented consent, submits, and follows the tracker. Screenshots go to test-results/journey-*.png.
 */
test.use({ viewport: { width: 390, height: 844 }, isMobile: true, hasTouch: true });

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

test("individual registers, submits a request with consent and sees what Rahoon will do", async ({ page }) => {
  await page.goto("/");
  await shot(page, "01-landing");
  await page.getByRole("link", { name: "ابدأ طلب المعالجة" }).first().click();
  await page.waitForURL("**/start");
  await registerIndividual(page);
  await expect(page.getByRole("heading", { name: "لا توجد طلبات بعد" })).toBeVisible();
  await shot(page, "04-my-empty");

  const reference = await submitRequest(page, true);
  await shot(page, "10-submitted");

  await page.getByRole("link", { name: "متابعة طلبي" }).click();
  await page.waitForURL(`**/my/requests/${reference}`);
  await expect(page.getByText("مقدَّم").first()).toBeVisible();
  await expect(page.getByText("فريق رهون").first()).toBeVisible();
  await expect(page.getByRole("heading", { name: "ماذا ستفعل رهون لك" })).toBeVisible();
  await expect(page.getByText("مبدئي — يتأكد بعد الدراسة")).toBeVisible();
  await expect(page.getByText("الاحتفاظ بالعقار").first()).toBeVisible();
  // Q6: no deadline wording anywhere on the tracker.
  await expect(page.locator("main")).not.toContainText(/حتى تاريخ|مجاني|خلال \d/);
  await shot(page, "11-tracker");

  await page.goto("/my");
  await expect(page.getByText(reference)).toBeVisible();
  await shot(page, "12-my-list");
});
