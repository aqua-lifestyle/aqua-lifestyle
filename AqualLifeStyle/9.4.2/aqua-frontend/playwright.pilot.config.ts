import { defineConfig, devices } from "@playwright/test";

// Explicit opt-in; this uses an actual loopback API and the disposable pilot database.
// It does not start the legacy mock backend or select any production subject.
export default defineConfig({
  testDir: "./e2e",
  testMatch: "isolated-pilot.spec.ts",
  workers: 1,
  expect: { timeout: 15_000 },
  timeout: 240_000,
  use: {
    ...devices["Desktop Chrome"],
    channel: "chrome",
    baseURL: "http://127.0.0.1:3000",
    trace: "retain-on-failure",
  },
});
