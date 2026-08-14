import { defineConfig, devices } from "@playwright/test";

export default defineConfig({
  testDir: "./e2e",
  use: {
    baseURL: "http://127.0.0.1:3100",
    trace: "retain-on-failure",
  },
  projects: [
    { name: "desktop", use: { ...devices["Desktop Chrome"] } },
    { name: "mobile", use: { ...devices["Pixel 7"] } },
  ],
  webServer: [
    {
      command: "node e2e/mock-backend.mjs",
      env: { MOCK_BACKEND_PORT: "3200" },
      reuseExistingServer: false,
      timeout: 10_000,
      url: "http://127.0.0.1:3200/api/health",
    },
    {
      command: "node .next/standalone/server.js",
      env: {
        HOSTNAME: "127.0.0.1",
        NEXT_PUBLIC_ABP_API_URL: "http://127.0.0.1:3200",
        NEXT_PUBLIC_DEFAULT_TENANT_NAME: "Default",
        NEXTAUTH_SECRET: "e2e-only-placeholder-secret-at-least-32-characters",
        PORT: "3100",
      },
      reuseExistingServer: false,
      timeout: 30_000,
      url: "http://127.0.0.1:3100",
    },
  ],
});
