import { expect, test, type Page } from "@playwright/test";
import { api } from "./helpers";
import { registerIndividual, submitRequest, teamPage } from "./journey";

/**
 * Phase 1A step 9 — responsive and accessibility sweep of the MVP screens (individual at 390/768/1440, team at
 * 390/768/1440) plus 200% zoom. Automatic checks: no horizontal page scroll, and WCAG AA contrast of every visible
 * text node against its effective background (4.5:1, 3:1 for large text). Screenshots: test-results/qa-*.png.
 */

const WIDTHS = [390, 768, 1440] as const;

interface QaIssue {
  page: string;
  width: number | string;
  kind: "overflow" | "contrast";
  detail: string;
}

async function audit(page: Page, name: string, width: number | string, issues: QaIssue[]) {
  await page.waitForLoadState("networkidle");
  const overflow = await page.evaluate(() => {
    const el = document.documentElement;
    return el.scrollWidth - el.clientWidth;
  });
  if (overflow > 1) issues.push({ page: name, width, kind: "overflow", detail: `${overflow}px wider than the viewport` });

  const low = await page.evaluate(() => {
    const parse = (c: string) => {
      const m = c.match(/rgba?\(([^)]+)\)/);
      if (!m) return null;
      const [r, g, b, a = "1"] = m[1].split(/[,\s/]+/).filter(Boolean);
      return { r: +r, g: +g, b: +b, a: +a };
    };
    const lum = ({ r, g, b }: { r: number; g: number; b: number }) => {
      const f = (v: number) => {
        const s = v / 255;
        return s <= 0.03928 ? s / 12.92 : ((s + 0.055) / 1.055) ** 2.4;
      };
      return 0.2126 * f(r) + 0.7152 * f(g) + 0.0722 * f(b);
    };
    const bgOf = (el: Element | null): { r: number; g: number; b: number } => {
      for (let e = el; e; e = e.parentElement) {
        const c = parse(getComputedStyle(e).backgroundColor);
        if (c && c.a > 0.5) return c;
      }
      return { r: 255, g: 255, b: 255 };
    };
    const out: string[] = [];
    const walker = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT);
    const seen = new Set<Element>();
    for (let n = walker.nextNode(); n; n = walker.nextNode()) {
      const el = n.parentElement;
      if (!el || seen.has(el) || !n.textContent?.trim()) continue;
      seen.add(el);
      const cs = getComputedStyle(el);
      const rect = el.getBoundingClientRect();
      if (cs.visibility === "hidden" || cs.display === "none" || rect.width === 0 || rect.height === 0 || +cs.opacity < 0.5) continue;
      if (el.closest("[aria-hidden='true'], .sr-only, nextjs-portal, [disabled], [aria-disabled='true']")) continue;
      const fg = parse(cs.color);
      if (!fg) continue;
      const l1 = lum(fg);
      const l2 = lum(bgOf(el));
      const ratio = (Math.max(l1, l2) + 0.05) / (Math.min(l1, l2) + 0.05);
      const size = parseFloat(cs.fontSize);
      const large = size >= 24 || (size >= 18.66 && +cs.fontWeight >= 700);
      if (ratio < (large ? 3 : 4.5)) out.push(`${ratio.toFixed(2)}:1 «${n.textContent.trim().slice(0, 40)}»`);
    }
    return out.slice(0, 5);
  });
  for (const l of low) issues.push({ page: name, width, kind: "contrast", detail: l });
  await page.screenshot({ path: `test-results/qa-${name}-${width}.png`, fullPage: true });
}

test("MVP screens reflow at 390/768/1440 and 200% zoom, with AA text contrast", async ({ page, browser }) => {
  test.setTimeout(300_000);
  const issues: QaIssue[] = [];

  // Data: one submitted request in review with a coordination entry, an objection and a message.
  await registerIndividual(page);
  const reference = await submitRequest(page);
  await page.getByRole("link", { name: "متابعة طلبي" }).click();
  const team = await teamPage(browser, "n.alyami@team.rahoon.example");
  const base = `/team/requests/${reference}`;
  for (const [path, body] of [
    [`${base}/pick-up`, { nextStep: null }],
    [`${base}/identity-check`, { note: "طابقنا الهوية." }],
    [`${base}/coordination`, { channel: "phone", occurredAt: new Date(Date.now() - 600_000).toISOString(), counterpart: "إدارة التحصيل", summary: "عرضنا الطلب.", visibleToApplicant: true, applicantText: "تواصلنا مع جهتك الممولة." }],
    [`${base}/messages`, { body: "مرحباً، نحن ندرس طلبك." }],
  ] as const)
    expect((await api(team, "POST", path, body)).status, path).toBe(200);
  expect((await api(page, "POST", `/my/requests/${reference}/concerns`, { kind: "objection", subject: "amount", text: "القسط الصحيح 4,500." })).status).toBe(200);
  await page.goto("/my");
  await page.getByRole("button", { name: "ابدأ طلب معالجة جديد" }).click();
  await page.waitForURL(/apply/);
  const draft = page.url().match(/REQ-\d{4}-\d{5}/)![0];

  const individualPages: Array<[string, string]> = [
    ["landing", "/"],
    ["start", "/start"],
    ["my", "/my"],
    ["wizard-1", `/my/requests/${draft}/apply?step=1`],
    ["wizard-2", `/my/requests/${draft}/apply?step=2`],
    ["wizard-4", `/my/requests/${draft}/apply?step=4`],
    ["tracker", `/my/requests/${reference}`],
    ["paths", `/my/requests/${reference}/paths`],
    ["messages", `/my/requests/${reference}/messages`],
    ["concern", `/my/requests/${reference}/concern?kind=objection`],
  ];
  const teamPages: Array<[string, string]> = [
    ["team-queue", "/team?tab=mine"],
    ["team-review", base],
    ["team-objections", "/team/objections"],
  ];

  for (const width of WIDTHS) {
    await page.setViewportSize({ width, height: 900 });
    for (const [name, url] of individualPages) {
      await page.goto(url);
      await audit(page, name, width, issues);
    }
    await team.setViewportSize({ width, height: 900 });
    for (const [name, url] of teamPages) {
      await team.goto(url);
      await audit(team, name, width, issues);
    }
  }

  // 200% zoom (WCAG 1.4.4 / 1.4.10): a 768px window at 200% leaves a 384px layout.
  for (const [p, list] of [[page, individualPages.filter(([n]) => ["my", "wizard-2", "tracker"].includes(n))], [team, teamPages.slice(0, 2)]] as const) {
    await p.setViewportSize({ width: 768, height: 900 });
    for (const [name, url] of list) {
      await p.goto(url);
      await p.evaluate(() => {
        (document.documentElement.style as CSSStyleDeclaration & { zoom: string }).zoom = "2";
      });
      await audit(p, name, "zoom200", issues);
    }
  }
  await team.context().close();

  console.log(`QA issues (${issues.length}):\n` + issues.map((i) => `${i.kind} · ${i.page} @${i.width} · ${i.detail}`).join("\n"));
  expect(issues.filter((i) => i.kind === "overflow"), "horizontal overflow").toEqual([]);
  expect(issues.filter((i) => i.kind === "contrast"), "text contrast").toEqual([]);
});
