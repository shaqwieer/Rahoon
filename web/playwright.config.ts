import { defineConfig, devices } from "@playwright/test";

/**
 * End-to-end tests against the local stack (docs/architecture.md):
 *   API  http://localhost:5080  (bash scripts/dev-api.sh --reset  → fresh demo seed)
 *   Web  http://localhost:3000  (npm run dev)
 * Set E2E_RESET=1 to reseed the demo database before the run. The flows mutate
 * seeded cases, so a second run without a reset may find them in later states.
 */
export default defineConfig({
  testDir: "./e2e",
  fullyParallel: false,
  workers: 1,
  timeout: 90_000,
  expect: { timeout: 15_000 },
  retries: 0,
  reporter: [["list"]],
  globalSetup: "./e2e/global-setup.ts",
  use: {
    baseURL: process.env.E2E_BASE_URL ?? "http://localhost:3000",
    locale: "ar-SA",
    timezoneId: "Asia/Riyadh",
    trace: "retain-on-failure",
    screenshot: "only-on-failure",
  },
  projects: [
    { name: "desktop", use: { ...devices["Desktop Chrome"], viewport: { width: 1440, height: 900 } } },
  ],
});
