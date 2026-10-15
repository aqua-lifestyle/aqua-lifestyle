import { defineConfig, devices } from "@playwright/test";
import { dirname, join } from "node:path";

// Existing PostgreSQL/ABP web-test host + original Next.js UI, never production.
export default defineConfig({
  testDir: "./e2e",
  testMatch: "payment-area-approval.spec.ts",
  outputDir: process.env.PAYMENT_APPROVAL_FIXTURE_OUTPUT
    ? join(dirname(process.env.PAYMENT_APPROVAL_FIXTURE_OUTPUT), "payment-area-approval-browser")
    : "test-results/payment-area-approval",
  workers: 1,
  timeout: 240_000,
  expect: { timeout: 20_000 },
  use: {
    ...devices["Desktop Chrome"],
    channel: "chrome",
    baseURL: "http://127.0.0.1:3310",
    actionTimeout: 20_000,
    navigationTimeout: 30_000,
    trace: "off",
    screenshot: "off",
    video: "off",
  },
});
