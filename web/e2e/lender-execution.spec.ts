import { expect, test, type Browser, type Page } from "@playwright/test";
import { api, apiLogin, completeStepUp } from "./helpers";

// Archived mortgage-help model (docs/redefinition/legacy-inventory.md): runs only with RAHOON_LEGACY_MODES=1 on web and API.
test.skip(process.env.RAHOON_LEGACY_MODES !== "1", "legacy model archived");

/**
 * Phase 1A-2 step 5 — lender-on-platform mode (secondary to the individual-first journey): after approval and the owner's
 * acceptance, legal activates the agreement (L19, guard reasons shown first), finance records a payment and a second
 * finance user matches it (L20, maker ≠ checker), the owner sees it received (D10), and a seeded breach review ends with a
 * path decision, never an automatic referral (L21). Changes seeded cases: run with E2E_RESET=1.
 */
test.use({ viewport: { width: 1440, height: 900 }, locale: "ar-SA", timezoneId: "Asia/Riyadh" });

const today = () => new Date(Date.now() + 3 * 3_600_000).toISOString().slice(0, 10);

/** The invited owner of a seeded case, signed in through the owner portal API in their own browser context. */
async function ownerPage(browser: Browser, reference: string, idLast4: string): Promise<Page> {
  const ctx = await browser.newContext({ viewport: { width: 390, height: 844 }, locale: "ar-SA", timezoneId: "Asia/Riyadh" });
  const owner = await ctx.newPage();
  await owner.goto("/");
  const token = `demo-${reference}`;
  const sent = await api<{ sandboxCode: string }>(owner, "POST", "/auth/owner/verify-id", { token, idLast4 });
  expect(sent.status, "owner verify-id").toBe(200);
  expect((await api(owner, "POST", "/auth/owner/verify-otp", { token, code: sent.json.sandboxCode })).status, "owner otp").toBe(200);
  return owner;
}

/** Fill after hydration: retry until the value sticks (a fill before hydration is reset by React). */
async function fillStable(page: Page, label: RegExp | string, value: string) {
  const field = page.getByLabel(label);
  await expect(async () => {
    await field.fill(value);
    await expect(field).toHaveValue(value, { timeout: 1000 });
  }).toPass();
}

test("L19 activation after legal review and schedule, L20 maker-checker, the owner sees it received (D10)", async ({ page, browser }) => {
  const ref = "RH-2026-004090";
  await page.goto("/login");

  // Setup through the API: the approver decides after an MFA step-up, the owner accepts with a code.
  await apiLogin(page, "n.alshehri@alufuq.example");
  const stepUp = await api<{ sandboxCode: string }>(page, "POST", "/auth/step-up/start");
  expect((await api(page, "POST", "/auth/step-up/verify", { code: stepUp.json.sandboxCode })).status, "step-up").toBe(200);
  const inbox = await api<{ items: Array<{ id: string; caseRef: string }> }>(page, "GET", "/approvals");
  const item = inbox.json.items.find((i) => i.caseRef === ref);
  expect(item, `${ref} awaits approval (reseed with E2E_RESET=1)`).toBeTruthy();
  const opened = await api<{ openedVersion: number }>(page, "GET", `/approvals/${item!.id}`);
  expect((await api(page, "POST", `/approvals/${item!.id}/decision`, { decision: "approve", reason: "الحل قابل للسداد وضمن حدودي، والتنازل مبرر بظرف موثق.", openedVersion: opened.json.openedVersion })).status).toBe(200);
  const owner = await ownerPage(browser, ref, "4090");
  const home = await api<{ nextStep: { route: string } }>(owner, "GET", "/owner/home");
  const offerId = home.json.nextStep.route.split("/").pop();
  const otp = await api<{ sandboxCode: string }>(owner, "POST", `/owner/offers/${offerId}/consent/otp`);
  expect((await api(owner, "POST", `/owner/offers/${offerId}/consent`, { acknowledgements: ["terms_read", "voluntary"], code: otp.json.sandboxCode })).status).toBe(200);

  // L19 (ماجد, legal): what is missing is shown; activation before it is refused by the server with the reason.
  await apiLogin(page, "m.alharbi@alufuq.example");
  await page.goto(`/cases/${ref}/agreement`);
  await expect(page.getByText(/يتطلب التفعيل اكتمال/)).toBeVisible();
  const early = await api<{ reasons: string[] }>(page, "POST", `/cases/${ref}/agreement/activate`);
  expect(early.status).toBe(422);
  expect(early.json.reasons.join(" ")).toContain("مراجعة القانونية");
  await page.getByRole("button", { name: "تسجيل مراجعة القانونية" }).click();
  const legal = page.getByRole("dialog", { name: "تسجيل مراجعة القانونية" });
  await legal.getByLabel(/ملاحظة المراجعة/).fill("الاتفاق مطابق للعرض المعتمد v1.");
  await legal.getByRole("button", { name: "تسجيل المراجعة" }).click();
  await expect(page.getByText("سُجّلت مراجعة القانونية.")).toBeVisible();
  await page.getByRole("button", { name: "إنشاء جدول السداد" }).click();
  await page.getByRole("dialog", { name: "إنشاء جدول السداد" }).getByRole("button", { name: "إنشاء الجدول" }).click();
  await expect(page.getByText(/أُنشئ جدول السداد/)).toBeVisible();
  await page.getByRole("button", { name: "تفعيل الاتفاق" }).click();
  await page.getByRole("dialog", { name: "تفعيل الاتفاق…" }).getByRole("button", { name: "تأكيد التفعيل" }).click();
  await expect(page.getByText(/فُعّل الاتفاق/)).toBeVisible();
  await page.screenshot({ path: "test-results/lender-l19-activated.png", fullPage: true });

  // L20 (ريم, maker): record installment 1; she cannot match her own payment.
  await apiLogin(page, "r.aldosari@alufuq.example");
  await page.goto(`/cases/${ref}/payments`);
  await page.getByRole("button", { name: "تسجيل دفعة" }).click();
  const drawer = page.getByRole("dialog", { name: "تسجيل دفعة يدوياً" });
  await drawer.getByLabel("مرجع التحويل البنكي").fill(`TRX-E2E-${Date.now() % 1_000_000}`);
  await drawer.getByLabel("تاريخ الاستلام").fill(today());
  await drawer.getByRole("button", { name: "تسجيل وإرسال للمطابقة" }).click();
  await expect(page.getByText("سجّلت هذه الدفعة؛ يطابقها موظف مالية آخر.").first()).toBeVisible();
  await expect(page.getByRole("button", { name: "مطابقة" })).toHaveCount(0);

  // The owner doesn't see an unmatched payment as received.
  await owner.goto("/owner/payments");
  await expect(owner.getByText("مستلم", { exact: true })).toHaveCount(0);

  // L20 (عبدالعزيز, checker): matches it; the owner then sees it received.
  await apiLogin(page, "a.alshammari@alufuq.example");
  await page.goto(`/cases/${ref}/payments`);
  await page.getByRole("button", { name: "مطابقة" }).first().click();
  await page.getByRole("dialog", { name: "مطابقة الدفعة" }).getByRole("button", { name: "تأكيد المطابقة" }).click();
  await expect(page.getByText(/طوبقت الدفعة/)).toBeVisible();
  await page.screenshot({ path: "test-results/lender-l20-matched.png", fullPage: true });
  await owner.reload();
  await expect(owner.getByText("رهون لا تستلم أي مبالغ").first()).toBeVisible();
  await expect(owner.getByText("مستلم", { exact: true }).first()).toBeVisible();
  await owner.screenshot({ path: "test-results/lender-d10-owner.png", fullPage: true });
  await owner.context().close();
});

test("L21: a seeded breach review ends with a path decision, never an automatic referral", async ({ page }) => {
  const ref = "RH-2026-003870";
  await page.goto("/login");
  await apiLogin(page, "s.alqahtani@alufuq.example");
  await page.goto(`/cases/${ref}/payments/breach`);
  await expect(page.getByRole("alert").filter({ hasText: /قسط/ }).first()).toBeVisible();
  await page.getByRole("button", { name: "تسجيل قرار المسار" }).click();
  const dialog = page.getByRole("dialog", { name: "قرار مسار مراجعة الإخلال…" });
  await dialog.getByRole("radio", { name: /إعادة هيكلة/ }).check();
  await fillStable(page, /مبرر المسار/, "تواصلنا مع المالك؛ الدخل انخفض مؤقتاً ويحتاج قسطاً أخف.");
  await dialog.getByRole("button", { name: "تأكيد القرار" }).click();
  await expect(page.getByText("سُجّل القرار · الحالة الآن «حل مقترح» لإعداد حل جديد.")).toBeVisible();
  const ws = await api<{ header: { status: string } }>(page, "GET", `/cases/${ref}`);
  expect(ws.json.header.status).toBe("proposed_solution");
  expect(ws.json.header.status).not.toBe("judicial_referral");
  await page.screenshot({ path: "test-results/lender-l21-outcome.png", fullPage: true });
});

/** Clicks a confirm button that may ask for an MFA step-up first, then waits for the success text. */
async function confirmWithStepUp(page: Page, confirm: () => Promise<void>, success: RegExp) {
  await confirm();
  const stepUp = page.getByRole("dialog", { name: "تأكيد برمز التحقق" });
  const done = page.getByText(success).first();
  await expect(done.or(stepUp)).toBeVisible();
  if (await stepUp.isVisible()) await completeStepUp(page);
  await expect(done).toBeVisible();
}

test("L26 settlement closure: preparer ≠ reviewer ≠ approver, step-up, closure documents reach the owner (D14)", async ({ page, browser }) => {
  // Seeded RH-2026-003702: ريم prepared and submitted a zero-difference reconciliation (two matched transfers, approved waiver).
  const ref = "RH-2026-003702";
  const url = `/cases/${ref}/closure`;
  await page.goto("/login");

  // ريم (preparer) can't review her own reconciliation.
  await apiLogin(page, "r.aldosari@alufuq.example");
  await page.goto(url);
  await expect(page.getByRole("heading", { name: "المطابقة المالية" })).toBeVisible();
  await expect(page.getByText("الفرق 0.00").first()).toBeVisible();
  await expect(page.getByText("أعددت هذه التسوية؛ يدققها موظف مالية آخر.")).toBeVisible();
  await page.screenshot({ path: "test-results/lender-l26-submitted.png", fullPage: true });

  // عبدالعزيز (reviewer).
  await apiLogin(page, "a.alshammari@alufuq.example");
  await page.goto(url);
  await page.getByRole("button", { name: "تدقيق التسوية…" }).click();
  const review = page.getByRole("dialog", { name: "تدقيق التسوية المالية" });
  await review.getByLabel(/ملاحظة التدقيق/).fill("طابقت التحويلين مع كشف الحساب؛ الفرق صفر.");
  await review.getByRole("button", { name: "تأكيد التدقيق" }).click();
  await expect(page.getByText("اكتمل تدقيق التسوية · بانتظار الاعتماد.")).toBeVisible();

  // نورة (approver) approves with a step-up.
  await apiLogin(page, "n.alshehri@alufuq.example");
  await page.goto(url);
  await page.getByRole("button", { name: "اعتماد التسوية…" }).click();
  const approve = page.getByRole("dialog", { name: "اعتماد التسوية المالية" });
  await approve.getByLabel(/سبب القرار/).fill("المطابقة صفرية الفرق والمصادر قابلة للتتبع.");
  await confirmWithStepUp(page, () => approve.getByRole("button", { name: "اعتماد" }).click(), /اعتُمدت التسوية المالية/);

  // ريم: owner summary, then the closure request.
  await apiLogin(page, "r.aldosari@alufuq.example");
  await page.goto(url);
  await page.getByRole("button", { name: "توليد ملخص المالك" }).click();
  await page.getByRole("dialog", { name: "توليد ملخص الحالة النهائي للمالك" }).getByRole("button", { name: "توليد الملخص" }).click();
  await expect(page.getByText("وُلّد ملخص الحالة النهائي للمالك.")).toBeVisible();
  await page.getByRole("button", { name: "إرسال للتدقيق والاعتماد" }).click();
  const req = page.getByRole("dialog", { name: "إرسال الإغلاق للاعتماد" });
  await req.getByLabel(/ملاحظة الإغلاق/).fill("المطابقة صفرية الفرق. المستندات مكتملة.");
  await req.getByRole("button", { name: "إرسال للاعتماد" }).click();
  await expect(page.getByText("أُرسل الإغلاق للاعتماد.")).toBeVisible();

  // نورة decides the closure: trace acknowledged, step-up.
  await apiLogin(page, "n.alshehri@alufuq.example");
  await page.goto(url);
  await page.getByRole("button", { name: /اعتماد الإغلاق/ }).click();
  const decide = page.getByRole("dialog", { name: "اعتماد الإغلاق" });
  await decide.getByRole("checkbox", { name: "راجعت تتبع المصادر ولا يوجد رقم دون مصدر" }).check();
  await decide.getByLabel(/سبب القرار/).fill("المصادر قابلة للتتبع والمستندات مكتملة.");
  await confirmWithStepUp(page, () => decide.getByRole("button", { name: "اعتماد الإغلاق" }).click(), /أُغلقت الحالة ونُشرت مستنداتها للمالك/);
  await page.reload();
  await expect(page.getByText("أُغلقت الحالة", { exact: true })).toBeVisible();
  await page.screenshot({ path: "test-results/lender-l26-closed.png", fullPage: true });

  // D14: the owner downloads the closure documents.
  const owner = await ownerPage(browser, ref, "3702");
  await owner.goto("/owner/documents/closure");
  await expect(owner.getByText("خطاب المخالصة النهائية")).toBeVisible();
  await expect(owner.getByText("خطاب فك الرهن")).toBeVisible();
  await owner.screenshot({ path: "test-results/lender-d14-owner.png", fullPage: true });
  await owner.context().close();
});

test("lender-mode negatives: another institution gets the same refusal as a missing case; a double submit replays once", async ({ page }) => {
  await page.goto("/login");
  // مها is a case manager at another lender (السنبلة). CaseAccess answers the same refusal whether a case exists or not,
  // so alufuq's cases are indistinguishable from a reference that doesn't exist.
  await apiLogin(page, "m.alshahrani@sunbula.example", "شركة السنبلة للتمويل");
  const missing = await api(page, "GET", "/cases/RH-2026-999999/payments");
  expect(missing.status).toBe(403);
  for (const ref of ["RH-2026-003870", "RH-2026-003702"]) {
    for (const path of ["payments", "closure", "agreement"]) {
      const res = await api(page, "GET", `/cases/${ref}/${path}`);
      expect(res.status, `${ref} ${path}`).toBe(missing.status);
      expect(JSON.stringify(res.json)).toBe(JSON.stringify(missing.json));
    }
  }

  // The same Idempotency-Key replays the stored answer instead of acting twice.
  await apiLogin(page, "s.alqahtani@alufuq.example");
  const cookies = await page.context().cookies();
  const headers = {
    Origin: new URL(page.url()).origin,
    "X-CSRF-Token": cookies.find((c) => c.name === "rahoon_csrf")?.value ?? "",
    "Idempotency-Key": crypto.randomUUID(),
  };
  const body = `ملاحظة داخلية للتحقق من الإعادة ${Date.now()}`;
  const send = () => page.request.post("/api/cases/RH-2026-003870/negotiation/notes", { headers, data: { body, internal: true } });
  const [first, second] = [await send(), await send()];
  expect(first.status()).toBe(200);
  expect(second.status()).toBe(200);
  expect(await second.text()).toBe(await first.text());
  const negotiation = await api<unknown>(page, "GET", "/cases/RH-2026-003870/negotiation");
  expect(JSON.stringify(negotiation.json).split(body).length - 1).toBe(1);
});
