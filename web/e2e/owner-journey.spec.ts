import { expect, test, type Browser, type Page } from "@playwright/test";
import { api, completeStepUp } from "./helpers";
import { registerIndividual, sandboxCode, signInIndividual, submitRequest, teamPage } from "./journey";

// Archived mortgage-help model (docs/redefinition/legacy-inventory.md): runs only with RAHOON_LEGACY_MODES=1 on web and API.
test.skip(process.env.RAHOON_LEGACY_MODES !== "1", "legacy model archived");

/**
 * Primary MVP journey (individual-first, Phase 1A): on a 390px phone the individual registers, fills the request
 * wizard with documented consent, submits, and follows the tracker. Screenshots go to test-results/journey-*.png.
 */
test.use({ viewport: { width: 390, height: 844 }, isMobile: true, hasTouch: true });

const shot = (page: Page, name: string) => page.screenshot({ path: `test-results/journey-${name}.png`, fullPage: true });

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

  // T07: relay. An accepted P1 offer is then tracked, not closed (Phase 1A-2, ADR 0002): closing it as «قبل العميل العرض»
  // is refused with the reason.
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
  await close.getByLabel(/ملخص النتيجة/).fill("قبلت عرض جهتك ونقلنا موافقتك إليها.");
  await close.getByRole("button", { name: "إغلاق الطلب" }).click();
  await expect(close.getByText(/ابدأ «متابعة التنفيذ»/)).toBeVisible();
  await tshot(back, "08-close-refused-tracking-required");
  await close.getByRole("button", { name: "إلغاء" }).click();

  // Phase 1A-2 (D-7): start tracking, then record the lender's agreement and schedule from its document.
  await back.getByRole("button", { name: "بدء متابعة التنفيذ" }).click();
  await expect(back.getByText("قيد متابعة التنفيذ").first()).toBeVisible();
  await uploadLenderDocument(back, "agreement.pdf");
  await back.getByRole("button", { name: "تسجيل الاتفاق والجدول…" }).click();
  const agr = back.getByRole("dialog", { name: "تسجيل الاتفاق والجدول من مستند الجهة" });
  await agr.getByLabel("مستند الجهة (إلزامي)").selectOption({ label: "خطاب الجهة الممولة · agreement.pdf" });
  await agr.getByLabel("مرجع مستند الجهة").fill("AF-2026-8001");
  await agr.getByLabel("تاريخ مستند الجهة").fill(isoDay(-1));
  await agr.getByLabel(/جدول الأقساط/).fill(`1, ${isoDay(25)}, 3100\n2, ${isoDay(55)}, 3100`);
  await agr.getByLabel("ما ذكرته الجهة", { exact: true }).fill("فعّلت الجهة ملحق إعادة الجدولة بقسط 3,100 ريال.");
  await agr.getByLabel(/ماذا يعني لك/).fill("أصبح قسطك 3,100 ريال، وتدفعه لجهتك مباشرة بحسب جدولها.");
  await tshot(back, "09-record-agreement");
  await agr.getByRole("button", { name: "إرسال للتحقق" }).click();
  await expect(back.getByText("بانتظار التحقق").first()).toBeVisible();
  await back.context().close();
  await verifyExecution(browser, reference, "10-verify-agreement");

  // D-8: the individual follows the agreement and the lender's schedule, and reports installment 1 with proof.
  await page.goto(`/my/requests/${reference}`);
  await expect(page.getByRole("heading", { name: "متابعة التنفيذ" })).toBeVisible();
  await expect(page.getByText("رهون لا تستلم أي مبالغ").first()).toBeVisible();
  await shot(page, "19-tracking");
  await page.getByRole("link", { name: "أبلغنا عن سداد" }).first().click();
  await page.waitForURL(/\/payment$/);
  await page.getByLabel("المبلغ", { exact: true }).fill("3100");
  await page.getByLabel("تاريخ التحويل").fill(isoDay(0));
  await page.getByLabel("القسط الذي سددته").selectOption("1");
  await page.locator("input[type=file]").setInputFiles({ name: "transfer.pdf", mimeType: "application/pdf", buffer: Buffer.from("%PDF-1.4\n% transfer\n") });
  await expect(page.getByText("تم الرفع")).toBeVisible();
  await shot(page, "20-report-payment");
  await page.getByRole("button", { name: "إرسال البلاغ" }).click();
  await page.waitForURL(/\/execution\?reported=1$/);
  await expect(page.getByText("سجّلنا بلاغك").first()).toBeVisible();
  await expect(page.getByText("أبلغتَنا بسداده، بانتظار تأكيد جهتك")).toBeVisible();
  await shot(page, "21-execution-reported");

  // T11: the lender's confirmation answers the report; another member verifies it; the individual then sees it confirmed.
  const conf = await teamPage(browser, "n.alyami@team.rahoon.example");
  await conf.goto(base);
  await uploadLenderDocument(conf, "confirmation.pdf");
  await conf.getByRole("button", { name: "تسجيل تأكيد الجهة…" }).click();
  const cd = conf.getByRole("dialog", { name: "تسجيل تأكيد سداد من مستند الجهة" });
  await cd.getByLabel("مستند الجهة (إلزامي)").selectOption({ label: "خطاب الجهة الممولة · confirmation.pdf" });
  await cd.getByLabel("مرجع مستند الجهة").fill("AF-2026-8050");
  await cd.getByLabel("تاريخ مستند الجهة").fill(isoDay(0));
  await cd.getByLabel("تاريخ الاستلام بحسب الجهة").fill(isoDay(0));
  await cd.getByLabel("ما ذكرته الجهة", { exact: true }).fill("تؤكد الجهة استلام القسط الأول.");
  await cd.getByLabel(/ماذا يعني لك/).fill("أكدت جهتك استلام قسطك الأول.");
  await cd.getByRole("button", { name: "إرسال للتحقق" }).click();
  await expect(conf.getByText("بانتظار التحقق").first()).toBeVisible();
  await conf.context().close();
  await verifyExecution(browser, reference, "13-verify-confirmation");
  await page.reload();
  await expect(page.getByText("أكدته جهتك").first()).toBeVisible();
  await shot(page, "22-execution-confirmed");

  // A closure letter relevant to P1, verified, then «اكتمل التنفيذ».
  const closer = await teamPage(browser, "n.alyami@team.rahoon.example");
  await closer.goto(base);
  await uploadLenderDocument(closer, "closure.pdf");
  await closer.getByRole("button", { name: "مستند إغلاق…" }).click();
  const doc = closer.getByRole("dialog", { name: "تسجيل مستند إغلاق من مستند الجهة" });
  await doc.getByLabel("مستند الجهة (إلزامي)").selectOption({ label: "خطاب الجهة الممولة · closure.pdf" });
  await expect(doc.getByLabel("نوع مستند الإغلاق")).toHaveValue("rescheduling_confirmation");
  await doc.getByLabel("مرجع مستند الجهة").fill("AF-2026-8090");
  await doc.getByLabel("تاريخ مستند الجهة").fill(isoDay(0));
  await doc.getByLabel("ما ذكرته الجهة", { exact: true }).fill("تؤكد الجهة إتمام إعادة الجدولة.");
  await doc.getByLabel(/ماذا يعني لك/).fill("أتمّت جهتك إعادة جدولة تمويلك. احتفظ بهذا الخطاب.");
  await doc.getByRole("button", { name: "إرسال للتحقق" }).click();
  await expect(closer.getByText("بانتظار التحقق").first()).toBeVisible();
  await closer.context().close();
  await verifyExecution(browser, reference, "11-verify-closure");

  const done = await teamPage(browser, "n.alyami@team.rahoon.example");
  await done.goto(base);
  await done.getByRole("button", { name: "إغلاق الطلب…" }).click();
  const fin = done.getByRole("dialog", { name: "إغلاق الطلب" });
  await fin.getByLabel("النتيجة", { exact: true }).selectOption("executed_closed");
  await fin.getByLabel(/ملخص النتيجة/).fill("اكتملت إعادة الجدولة بحسب ما أكدته جهتك، والمستندات محفوظة في صفحة طلبك.");
  await fin.getByRole("button", { name: "إغلاق الطلب" }).click();
  await expect(done.getByText("مغلق").first()).toBeVisible();
  await tshot(done, "12-closed-executed");
  await done.context().close();

  await page.goto(`/my/requests/${reference}`);
  await expect(page.getByText("اكتمل التنفيذ").first()).toBeVisible();
  await expect(page.getByRole("heading", { name: "مستندات الإغلاق" })).toBeVisible();
  const download = page.waitForEvent("download");
  await page.getByRole("link", { name: "تنزيل" }).first().click();
  expect((await download).suggestedFilename()).toBe("closure.pdf");
  await shot(page, "23-executed-closed");
});

test("a lender notice is explained with no action; completion needs a closure document; others see nothing", async ({ page, browser }) => {
  // Seeded REQ-2026-00308 (هند): a P1 offer accepted and relayed (README «Demo logins»). Re-runs find it already tracked.
  const reference = "REQ-2026-00308";
  const base = `/team/requests/${reference}`;
  const nayef = await teamPage(browser, "n.alyami@team.rahoon.example");
  await nayef.goto(base);
  const start = nayef.getByRole("button", { name: "بدء متابعة التنفيذ" });
  if (await start.isVisible()) await start.click();
  await expect(nayef.getByText("قيد متابعة التنفيذ").first()).toBeVisible();

  // Completion is refused without a verified closure document relevant to the path; the reason is shown.
  await nayef.getByRole("button", { name: "إغلاق الطلب…" }).click();
  const fin = nayef.getByRole("dialog", { name: "إغلاق الطلب" });
  await fin.getByLabel("النتيجة", { exact: true }).selectOption("executed_closed");
  await fin.getByLabel(/ملخص النتيجة/).fill("اكتمل التنفيذ.");
  await fin.getByRole("button", { name: "إغلاق الطلب" }).click();
  await expect(fin.getByText(/لا يوجد مستند إغلاق/)).toBeVisible();
  await fin.getByRole("button", { name: "إلغاء" }).click();

  // A notice from the lender, recorded from its document; the recorder gets no verify panel.
  await uploadLenderDocument(nayef, "notice.pdf");
  await nayef.getByRole("button", { name: "إشعار من الجهة…" }).click();
  const nd = nayef.getByRole("dialog", { name: "تسجيل إشعار من الجهة من مستند الجهة" });
  await nd.getByLabel("مستند الجهة (إلزامي)").selectOption({ label: "خطاب الجهة الممولة · notice.pdf" });
  await nd.getByLabel("مرجع مستند الجهة").fill(`AF-2026-N${Date.now() % 100000}`);
  await nd.getByLabel("تاريخ مستند الجهة").fill(isoDay(0));
  await nd.getByLabel("ما ذكرته الجهة", { exact: true }).fill("تفيد الجهة بأن القسط الأول لم يصلها.");
  await nd.getByLabel(/ماذا يعني لك/).fill("تقول جهتك إن قسطك الأول لم يصلها. إن كنت سددته فأبلغنا مع الإثبات.");
  await nd.getByRole("button", { name: "إرسال للتحقق" }).click();
  await expect(nayef.getByText("يتحقق من السجل عضو آخر من الفريق.").first()).toBeVisible();
  await expect(nayef.getByRole("button", { name: "اعتماد ونشر للعميل" })).toHaveCount(0);
  await nayef.context().close();
  await verifyExecution(browser, reference, "14-verify-notice");

  // E04: هند sees the notice explained, with no action from Rahoon and a way to answer.
  await signInIndividual(page, "1021098765", "0551110007");
  await page.goto(`/my/requests/${reference}`);
  await expect(page.getByRole("heading", { name: "إشعارات من جهتك" })).toBeVisible();
  await expect(page.getByText("لم تتخذ رهون أي إجراء بخصوص تمويلك بسبب هذا الإشعار.").first()).toBeVisible();
  await expect(page.getByText("قيد متابعة التنفيذ").first()).toBeVisible();
  await shot(page, "24-lender-notice");

  // Another individual gets nothing: no execution page, no payment report.
  const other = await (await browser.newContext({ viewport: { width: 390, height: 844 } })).newPage();
  await registerIndividual(other);
  const res = await other.goto(`/my/requests/${reference}/execution`);
  expect(res?.status()).toBe(404);
  expect((await api(other, "POST", `/my/requests/${reference}/payment-reports`, { amount: 100, transferDate: isoDay(0), proofDocumentId: crypto.randomUUID() })).status).toBe(404);
  await other.context().close();
});

const isoDay = (offset: number) => new Date(Date.now() + offset * 86_400_000).toISOString().slice(0, 10);

async function uploadLenderDocument(team: Page, name: string) {
  await team.getByRole("button", { name: "رفع مستند…" }).click();
  const upload = team.getByRole("dialog", { name: "رفع مستند للطلب" });
  await upload.locator("input[type=file]").setInputFiles({ name, mimeType: "application/pdf", buffer: Buffer.from(`%PDF-1.4\n% ${name}\n`) });
  await upload.getByRole("button", { name: "حفظ" }).click();
  await expect(upload).toBeHidden();
}

/** T06 for an execution record: another member (عبير), the checklist and an MFA step-up; back to the queue. */
async function verifyExecution(browser: Browser, reference: string, name: string) {
  const abeer = await teamPage(browser, "a.alqahtani@team.rahoon.example");
  await abeer.goto("/team/verify");
  await expect(abeer.getByText(reference)).toBeVisible();
  await abeer.goto(`/team/requests/${reference}`);
  for (const label of ["القيم مطابقة لمستند الجهة", "المرجع والتاريخ مطابقان", "نوع السجل صحيح", "«ماذا يعني لك» دقيق وغير مضلل"])
    await abeer.getByRole("checkbox", { name: label }).check();
  await tshot(abeer, name);
  await abeer.getByRole("button", { name: "اعتماد ونشر للعميل" }).click();
  await completeStepUp(abeer);
  await abeer.waitForURL("**/team/verify");
  await expect(abeer.getByText(reference)).toHaveCount(0);
  await abeer.context().close();
}

test("the individual declines an offer with no action against them, objects to an amount and gets the team's answer", async ({ page, browser }) => {
  await registerIndividual(page);
  const reference = await submitRequest(page);
  const base = `/team/requests/${reference}`;

  // Setup to a published offer through the API.
  const nayef = await teamPage(browser, "n.alyami@team.rahoon.example");
  for (const [path, body] of [
    [`${base}/pick-up`, { nextStep: null }],
    [`${base}/identity-check`, { note: "طابقنا الهوية." }],
    [`${base}/coordination`, { channel: "phone", occurredAt: new Date(Date.now() - 600_000).toISOString(), counterpart: "إدارة التحصيل", summary: "عرضنا الطلب.", visibleToApplicant: false }],
    [`${base}/start-coordination`, { nextStep: null }],
  ] as const)
    expect((await api(nayef, "POST", path, body)).status, path).toBe(200);
  const csrf = (await nayef.context().cookies()).find((c) => c.name === "rahoon_csrf")?.value ?? "";
  const up = await nayef.request.post(`/api${base}/documents`, {
    headers: { Origin: "http://localhost:3000", "X-CSRF-Token": csrf, "Idempotency-Key": crypto.randomUUID() },
    multipart: { file: { name: "letter.pdf", mimeType: "application/pdf", buffer: Buffer.from("%PDF-1.4\n% l\n") }, kind: "lender_letter", visibleToApplicant: "false" },
  });
  const letterId = (await up.json()).documentId as string;
  const rec = await api<{ id: string }>(nayef, "POST", `${base}/offers`, {
    path: "p2", settlementAmount: 380000, paymentConditions: "دفعة واحدة", effectText: "تُسدَّد المديونية بمبلغ 380,000 ريال ويُفك الرهن بعد السداد.",
    lenderReference: "AF-2026-8801", lenderLetterDate: new Date(Date.now() - 86_400_000).toISOString().slice(0, 10), letterDocumentId: letterId, shareLetter: true,
  });
  expect(rec.status).toBe(200);
  await nayef.context().close();
  const abeer = await teamPage(browser, "a.alqahtani@team.rahoon.example");
  const start = await api<{ sandboxCode: string }>(abeer, "POST", "/auth/step-up/start");
  expect((await api(abeer, "POST", "/auth/step-up/verify", { code: start.json.sandboxCode })).status).toBe(200);
  expect((await api(abeer, "POST", `${base}/offers/${rec.json.id}/verify`, { decision: "publish", checklist: ["amounts_match_letter", "terms_match_letter", "reference_and_date_match", "effect_text_accurate"] })).status).toBe(200);
  await abeer.context().close();

  // «لا يناسبني»: no reason required, neutral wording, no legal-action promise.
  await page.goto(`/my/requests/${reference}/offer`);
  await expect(page.getByText("تسوية المديونية").first()).toBeVisible();
  await page.getByRole("link", { name: "لا يناسبني" }).click();
  await page.waitForURL(/respond\?kind=decline/);
  await expect(page.getByText("سننقل ردك لجهتك الممولة ونبلغك بالخطوة التالية.")).toBeVisible();
  await expect(page.locator("main")).not.toContainText("إجراء قانوني");
  await shot(page, "19-decline");
  await page.getByRole("button", { name: "إبلاغ الجهة أن العرض لا يناسبني" }).click();
  await page.waitForURL(`**/my/requests/${reference}`);
  await expect(page.getByText("العرض لا يناسبك").first()).toBeVisible();

  // P4 objection to an amount.
  await page.getByRole("link", { name: "اعتراض على بيانات أو مبالغ أو قرار" }).click();
  await page.waitForURL(/concern\?kind=objection/);
  await page.getByRole("radio", { name: "مبلغ غير صحيح" }).check();
  await page.getByLabel("اشرح ما حدث وما تراه صحيحاً").fill("القسط الصحيح 4,500 ريال وليس 4,200.");
  await shot(page, "20-objection");
  await page.getByRole("button", { name: "إرسال" }).click();
  await expect(page.getByRole("heading", { name: "وصل ما أرسلته" })).toBeVisible();
  await expect(page.locator("main")).not.toContainText(/خلال \d|أيام عمل/);
  const objectionRef = (await page.locator("main p").first().innerText()).match(/OBJ-\d{4}-\d{5}/)![0];

  const lama = await teamPage(browser, "l.alharbi@team.rahoon.example");
  await lama.goto("/team/objections");
  const card = lama.locator("li").filter({ hasText: objectionRef });
  await expect(card).toBeVisible();
  await tshot(lama, "09-objections");
  await card.getByRole("button", { name: "الرد…" }).click();
  const dlg = lama.getByRole("dialog", { name: "الرد على العميل" });
  await dlg.getByLabel("نتيجة المراجعة").selectOption("upheld");
  await dlg.getByLabel("الرد كما سيراه العميل").fill("صححنا القسط في ملف طلبك إلى 4,500 ريال.");
  await dlg.getByRole("button", { name: "إرسال" }).click();
  await expect(lama.getByText(objectionRef)).toHaveCount(0);
  await lama.context().close();

  await page.goto(`/my/requests/${reference}`);
  await expect(page.getByText("صححنا القسط في ملف طلبك إلى 4,500 ريال.").first()).toBeVisible();
  await expect(page.getByText("تم الرد · قُبل")).toBeVisible();
  await shot(page, "21-objection-answered");
});
