import { expect, test, type Browser, type Page } from "@playwright/test";
import { api, apiLogin, completeStepUp } from "./helpers";

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

/** A team member's page at 1440 (staff login through the API, as the lender specs do). */
export async function teamPage(browser: Browser, email: string): Promise<Page> {
  const ctx = await browser.newContext({ viewport: { width: 1440, height: 900 }, locale: "ar-SA", timezoneId: "Asia/Riyadh" });
  const page = await ctx.newPage();
  await page.goto("/login");
  await apiLogin(page, email, "فريق رهون");
  return page;
}

const tshot = (page: Page, name: string) => page.screenshot({ path: `test-results/team-${name}.png`, fullPage: true });

test("the Rahoon team reviews, asks for information, checks identity and starts coordinating", async ({ page, browser }) => {
  await registerIndividual(page);
  const reference = await submitRequest(page);

  const team = await teamPage(browser, "n.alyami@team.rahoon.example");
  await team.goto("/team?tab=unassigned");
  await expect(team.getByRole("heading", { name: "الطلبات" })).toBeVisible();
  const row = team.locator("table").getByRole("link", { name: reference });
  await expect(row).toBeVisible();
  await tshot(team, "01-queue-unassigned");

  await row.click();
  await team.waitForURL(`**/team/requests/${reference}`);
  await team.getByRole("button", { name: "بدء دراسة الطلب" }).click();
  await expect(team.getByText("قيد الدراسة").first()).toBeVisible();

  // T03: ask for information → the individual sees «نحتاج معلومة منك» and answers.
  await team.getByRole("button", { name: "طلب استكمال…" }).click();
  await team.getByLabel("ما نحتاجه").fill("كشف حساب آخر 3 أشهر");
  await team.getByLabel("رسالة للعميل").fill("نحتاج كشف الحساب لنفهم دخلك الحالي قبل التواصل مع جهتك.");
  await tshot(team, "02-request-info");
  await team.getByRole("button", { name: "إرسال الطلب للعميل" }).click();
  await expect(team.getByText("بانتظار معلومة من العميل").first()).toBeVisible();

  await page.goto(`/my/requests/${reference}`);
  await expect(page.getByText("نحتاج معلومة منك").first()).toBeVisible();
  await expect(page.getByText("نحتاج كشف الحساب لنفهم دخلك").first()).toBeVisible();
  await shot(page, "13-info-requested");
  await page.getByRole("link", { name: "إضافة معلومة أو مستند" }).first().click();
  await page.getByLabel("المعلومة").fill("أرفقت كشف الحساب في المستندات.");
  await page.getByRole("button", { name: "إرسال" }).click();
  await page.waitForURL(`**/my/requests/${reference}`);
  await expect(page.getByText("قيد دراسة فريق رهون").first()).toBeVisible();

  // V12 identity check, then T04 coordination entry (visible text only), then start coordination.
  await team.reload();
  await team.getByRole("button", { name: "تسجيل التحقق من الهوية…" }).click();
  await team.getByLabel("كيف تحققت؟").fill("طابقنا صورة الهوية مع الاسم ورقم الجوال.");
  await team.getByRole("button", { name: "حفظ" }).click();
  await expect(team.getByText(/تحقق منها/)).toBeVisible();

  await team.getByRole("button", { name: "إضافة قيد…" }).click();
  await team.getByLabel("الطرف لدى الجهة").fill("إدارة التحصيل — أ. خالد");
  await team.getByLabel("الملخص").fill("اتصلنا بإدارة التحصيل وأرسلنا ملخص الطلب بموافقة العميل.");
  await team.getByRole("checkbox", { name: /يظهر للعميل؟/ }).check();
  await team.getByLabel("النص الذي يراه العميل").fill("تواصلنا مع جهتك الممولة وأرسلنا لها ملخص طلبك.");
  await tshot(team, "03-coordination-entry");
  await team.getByRole("button", { name: "حفظ" }).click();
  await expect(team.getByText("إدارة التحصيل — أ. خالد")).toBeVisible();

  await team.getByRole("button", { name: "بدء التنسيق مع الجهة" }).click();
  await expect(team.getByText("قيد التنسيق مع الجهة").first()).toBeVisible();
  await tshot(team, "04-review-coordinating");

  await page.reload();
  await expect(page.getByText("قيد التنسيق مع جهتك الممولة").first()).toBeVisible();
  await expect(page.getByText("تواصلنا مع جهتك الممولة وأرسلنا لها ملخص طلبك.")).toBeVisible();
  await expect(page.locator("main")).not.toContainText("إدارة التحصيل");
  await expect(page.locator("main")).not.toContainText("داخلي");
  await shot(page, "14-coordinating");
  await team.context().close();
});

test("an offer recorded from the lender letter is verified by another member, then the individual accepts with a code", async ({ page, browser }) => {
  await registerIndividual(page);
  const reference = await submitRequest(page);

  // Setup through the API (the UI for these steps is covered by the previous test).
  const nayef = await teamPage(browser, "n.alyami@team.rahoon.example");
  const base = `/team/requests/${reference}`;
  expect((await api(nayef, "POST", `${base}/pick-up`, { nextStep: null })).status).toBe(200);
  expect((await api(nayef, "POST", `${base}/identity-check`, { note: "طابقنا الهوية." })).status).toBe(200);
  expect(
    (await api(nayef, "POST", `${base}/coordination`, { channel: "phone", occurredAt: new Date(Date.now() - 600_000).toISOString(), counterpart: "إدارة التحصيل", summary: "عرضنا الطلب.", visibleToApplicant: false })).status,
  ).toBe(200);
  expect((await api(nayef, "POST", `${base}/start-coordination`, { nextStep: null })).status).toBe(200);

  // T05 through the UI: upload the lender letter, record the offer.
  await nayef.goto(base);
  await nayef.getByRole("button", { name: "رفع مستند…" }).click();
  const upload = nayef.getByRole("dialog", { name: "رفع مستند للطلب" });
  await upload.locator("input[type=file]").setInputFiles({ name: "letter.pdf", mimeType: "application/pdf", buffer: Buffer.from("%PDF-1.4\n% letter\n") });
  await upload.getByRole("button", { name: "حفظ" }).click();
  await expect(nayef.getByText("خطاب الجهة الممولة").first()).toBeVisible();
  await nayef.getByRole("button", { name: "تسجيل عرض الجهة…" }).click();
  const rec = nayef.getByRole("dialog", { name: "تسجيل عرض الجهة من خطابها" });
  await rec.getByLabel("خطاب الجهة (إلزامي)").selectOption({ index: 1 });
  await rec.getByLabel("القسط الجديد").fill("3100");
  await rec.getByLabel("المدة بالأشهر").fill("240");
  await rec.getByLabel(/أثره عليك/).fill("ينخفض قسطك الشهري إلى 3,100 ريال وتطول مدة التمويل.");
  await rec.getByLabel("مرجع خطاب الجهة").fill("AF-2026-7781");
  await rec.getByLabel("تاريخ خطاب الجهة").fill(new Date(Date.now() - 86_400_000).toISOString().slice(0, 10));
  await rec.getByLabel(/الصلاحية كما في الخطاب/).fill("صالح 30 يوماً من تاريخ الخطاب");
  await tshot(nayef, "05-record-offer");
  await rec.getByRole("button", { name: "إرسال للتحقق" }).click();
  await expect(nayef.getByText("بانتظار التحقق").first()).toBeVisible();
  await nayef.context().close();

  // T06 through the UI: another member verifies with the checklist and an MFA step-up.
  const abeer = await teamPage(browser, "a.alqahtani@team.rahoon.example");
  await abeer.goto("/team/verify");
  await expect(abeer.getByText(reference)).toBeVisible();
  await tshot(abeer, "06-verify-queue");
  await abeer.goto(base);
  for (const label of ["المبالغ مطابقة للخطاب", "المسار والشروط مطابقة للخطاب", "المرجع والتاريخ مطابقان", "«أثره عليك» دقيق وغير مضلل"])
    await abeer.getByRole("checkbox", { name: label }).check();
  await tshot(abeer, "07-verify-panel");
  await abeer.getByRole("button", { name: "اعتماد ونشر للعميل" }).click();
  await completeStepUp(abeer);
  await abeer.waitForURL("**/team/verify");
  await expect(abeer.getByRole("heading", { name: "بحاجة إلى تحقق" })).toBeVisible();
  await expect(abeer.getByText(reference)).toHaveCount(0);
  await abeer.context().close();

  // D07 + D09: the individual reviews and accepts with an SMS code.
  await page.goto(`/my/requests/${reference}`);
  await expect(page.getByText("وصل عرض من جهتك الممولة").first()).toBeVisible();
  await page.getByRole("link", { name: "مراجعة العرض" }).click();
  await page.waitForURL(/\/offer$/);
  await expect(page.getByText("عرض من جهتك الممولة", { exact: true })).toBeVisible();
  await expect(page.getByText("وتحقق منه عضو آخر من الفريق")).toBeVisible();
  await expect(page.getByText("صالح 30 يوماً من تاريخ الخطاب")).toBeVisible();
  await expect(page.getByText("بحسب خطاب الجهة")).toBeVisible();
  await shot(page, "15-offer");
  await page.getByRole("link", { name: "أوافق" }).click();
  await page.waitForURL(/\/offer\/accept$/);
  await page.getByRole("checkbox", { name: "قرأت النص أعلاه وأوافق عليه" }).check();
  await page.getByRole("button", { name: "إرسال رمز التأكيد" }).click();
  await page.locator("input[inputmode='numeric']").first().fill(await sandboxCode(page));
  await shot(page, "16-accept");
  await page.getByRole("button", { name: "تأكيد الموافقة" }).click();
  await page.waitForURL(`**/my/requests/${reference}`);
  await expect(page.getByText("سجّلنا ردك").first()).toBeVisible();
  await expect(page.getByText("وافقت على العرض")).toBeVisible();
  await shot(page, "17-response-recorded");

  // T07: relay, then close with the outcome shown to the individual.
  const back = await teamPage(browser, "n.alyami@team.rahoon.example");
  await back.goto(base);
  await back.getByRole("button", { name: "تسجيل نقل الرد للجهة…" }).click();
  const relay = back.getByRole("dialog", { name: "نقل رد العميل إلى الجهة الممولة" });
  await relay.getByLabel("الطرف لدى الجهة").fill("إدارة التحصيل");
  await relay.getByLabel("الملخص").fill("أرسلنا موافقة العميل بالبريد الرسمي.");
  await relay.getByRole("button", { name: "حفظ" }).click();
  await expect(back.getByText("نُقل للجهة")).toBeVisible();
  await back.getByRole("button", { name: "إغلاق الطلب…" }).click();
  const close = back.getByRole("dialog", { name: "إغلاق الطلب" });
  await close.getByLabel("النتيجة", { exact: true }).selectOption("offer_accepted");
  await close.getByLabel(/ملخص النتيجة/).fill("قبلت عرض جهتك ونقلنا موافقتك إليها. تنفيذ الاتفاق يتم مع جهتك الممولة.");
  await close.getByRole("button", { name: "إغلاق الطلب" }).click();
  await expect(back.getByText("مغلق").first()).toBeVisible();
  await tshot(back, "08-closed");
  await back.context().close();

  await page.reload();
  await expect(page.getByText("قبلت العرض").first()).toBeVisible();
  await expect(page.getByText("نقلنا ردك إلى جهتك الممولة").first()).toBeVisible();
  await shot(page, "18-closed");
});
