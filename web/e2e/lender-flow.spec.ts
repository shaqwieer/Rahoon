import { expect, test } from "@playwright/test";
import { USERS, PASSWORD, api, apiLogin, completeStepUp } from "./helpers";

test.describe("lender", () => {
  test("sign in through the UI (password → SMS sandbox code → organization)", async ({ page }) => {
    await page.context().clearCookies();
    await page.goto("/login");
    await page.getByLabel("البريد المؤسسي").fill(USERS.sara);
    await page.getByLabel("كلمة المرور", { exact: true }).fill(PASSWORD);
    await page.getByRole("button", { name: "متابعة" }).click();
    await expect(page).toHaveURL(/\/login\/mfa/);
    const code = (await page.getByRole("note").locator("bdi").innerText()).trim();
    await page.getByLabel("رمز التحقق").fill(code);
    await page.getByRole("button", { name: "تحقق", exact: true }).click();
    await expect(page).toHaveURL(/\/select-context/);
    await page.getByRole("radio", { name: /مصرف الأفق/ }).click();
    await expect(page).toHaveURL(/\/portfolio/);
    await expect(page.getByRole("heading", { level: 1 })).toBeVisible();
    // Session cookie is HttpOnly; nothing sensitive in web storage.
    const storage = await page.evaluate(() => JSON.stringify({ ...localStorage }) + JSON.stringify({ ...sessionStorage }));
    expect(storage).not.toMatch(/rahoon_sid|token/i);
  });

  test("create a case through the wizard and land on its workspace", async ({ page }) => {
    await apiLogin(page, USERS.sara);
    await page.goto("/cases/new"); // creates the draft (idempotent) and redirects to the wizard
    await expect(page).toHaveURL(/\/cases\/new\/RH-\d{4}-\d{6}/);
    const reference = page.url().match(/RH-\d{4}-\d{6}/)![0];

    const contract = `E2E-${Date.now()}`;
    await page.locator("#f-contract").fill(contract);
    await page.locator("#f-contract").blur();
    await page.locator("#f-amount").fill("850000");
    await page.locator("#f-term").fill("240");
    await page.getByRole("button", { name: /^التالي/ }).click();

    await expect(page.getByRole("heading", { level: 2, name: /الأطراف|المالك/ })).toBeVisible();
    await page.locator("#f-name").fill("مالك تجريبي للاختبار الآلي");
    await page.locator("#f-id").fill(`10${String(Date.now()).slice(-8)}`);
    await page.locator("#f-phone").fill("0551234567");
    await page.getByRole("button", { name: /^التالي/ }).click();

    await page.locator("#f-ptype").selectOption({ index: 1 });
    await page.locator("#f-city").fill("الرياض");
    await page.getByRole("button", { name: /^التالي/ }).click();

    await page.locator("#f-principal").fill("640000");
    await page.locator("#f-profit").fill("35000");
    await page.getByRole("button", { name: /^التالي/ }).click();
    await page.getByRole("button", { name: /^التالي/ }).click(); // documents are optional at creation

    await page.getByRole("button", { name: "إنشاء الحالة" }).click();
    await expect(page).toHaveURL(new RegExp(`/cases/${reference}$`));
    await expect(page.getByRole("heading", { level: 1 })).toContainText(/.+/);

    // The server — not the browser — owns the status and the audit trail.
    const ws = await api<{ header: { status: string } }>(page, "GET", `/cases/${reference}`);
    expect(ws.status).toBe(200);
    expect(ws.json.header.status).not.toBe("Draft");
  });

  test("maker-checker: reviewer submits v2, approver decides with step-up", async ({ page }) => {
    const ref = "RH-2026-004172";
    await apiLogin(page, USERS.sara);
    await page.goto(`/cases/${ref}/solutions/2/submit`);
    await page.getByLabel("ملاحظة للمعتمد").fill("راجعت الحل مع كشف الراتب. القسط ضمن حد الاستقطاع، والتنازل محصور في غرامات التأخير.");
    await page.getByLabel(/أؤكد أنني راجعت شروط الحل/).check();
    await page.getByRole("button", { name: "إرسال للموافقة" }).click();
    await expect(page).toHaveURL(new RegExp(`/cases/${ref}$`));

    // The submitter cannot approve their own submission (the API hides it from their inbox).
    const mine = await api<{ items: Array<{ caseRef: string }> }>(page, "GET", "/approvals");
    expect(mine.json.items.map((i) => i.caseRef)).not.toContain(ref);

    await apiLogin(page, USERS.noura);
    await page.goto("/approvals");
    await page.getByRole("link", { name: new RegExp(ref) }).click();
    await page.getByLabel("سبب القرار").fill("الحل قابل للسداد وضمن حدودي، والتنازل عن الغرامات مبرر بظرف موثق.");
    await page.getByRole("button", { name: "تأكيد الاعتماد" }).click();
    await completeStepUp(page);
    await expect(page.getByText("اعتُمد الحل وأُرسل العرض للمالك.")).toBeVisible();

    const ws = await api<{ header: { status: string } }>(page, "GET", `/cases/${ref}`);
    expect(ws.json.header.status).toBe("AwaitingCustomer");
  });
});
