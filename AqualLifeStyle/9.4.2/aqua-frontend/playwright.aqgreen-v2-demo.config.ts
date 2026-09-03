import { defineConfig, devices } from "@playwright/test";

export default defineConfig({
  expect: { timeout: 15_000 },
  testDir: "./e2e-real",
  timeout: 60_000,
  use: {
    baseURL: process.env.AQGREEN_V2_DEMO_FRONTEND_URL ?? "http://127.0.0.1:3000",
    trace: "retain-on-failure",
  },
  projects: [
    { name: "real-desktop", use: { ...devices["Desktop Chrome"] } },
  ],
  workers: 1,
});
