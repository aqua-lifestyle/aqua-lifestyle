import { expect, test, type Page } from "@playwright/test";
import { uiFixtures } from "./admin-ui-fixtures.mjs";

test.skip(process.env.MOCK_APPLICATION_RECOVERY !== "1", "Requires the existing synthetic backend in recovery mode.");

const signIn = async (page: Page, email = "admin-ui@example.test") => {
  await page.goto("/login");
  await page.getByLabel("Username or email").fill(email);
  await page.getByLabel("Password", { exact: true }).fill("test-password");
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
};

test("existing sign-in reaches the original dashboard, navigation and verified recovery notice", async ({ page }) => {
  const errors: string[] = [];
  const backendReads: string[] = [];
  page.on("pageerror", error => errors.push(error.message));
  page.on("request", request => {
    if (request.url().includes("/api/backend/")) backendReads.push(new URL(request.url()).pathname);
  });
  await signIn(page);
  await expect(page).toHaveURL("/admin/dashboard");
  await expect(page.getByRole("heading", { name: "Administration overview" })).toBeVisible();
  await expect(page.getByText(/Restricted recovery mode is active/)).toBeVisible();
  await expect(page.getByRole("heading", { name: "Management", exact: true })).toBeVisible();
  await expect(page.getByRole("heading", { name: "Customer accounts" })).toBeVisible();
  await expect(page.getByRole("heading", { name: "System status" })).toBeVisible();
  if (await page.getByRole("button", { name: "Open administration navigation" }).isVisible())
    await page.getByRole("button", { name: "Open administration navigation" }).click();
  const nav = page.getByRole("navigation", { name: "Administration navigation" });
  await expect(nav).toBeVisible();
  await expect(nav.getByRole("link", { name: "Dashboard" })).toBeVisible();
  await expect(nav.locator("[aria-disabled=true]").filter({ hasText: /^Customer accounts/ })).toBeVisible();
  await expect(nav.getByRole("link", { name: "Customer accounts" })).toHaveCount(0);
  await expect(page.getByText(/Establishing the application/)).toHaveCount(0);
  await expect(page.getByText("Recovery administrator workspace")).toHaveCount(0);
  await expect(page.getByText("Savings balance")).toHaveCount(0);
  await expect(page.getByRole("button", { name: "Refresh system status" })).toBeEnabled();
  await page.getByRole("button", { name: "Refresh system status" }).click();
  await page.reload();
  await expect(page.getByRole("heading", { name: "Administration overview" })).toBeVisible();
  expect(backendReads.every(path => path.endsWith("AQGreenPilot/GetStatus") || path.endsWith("api/health"))).toBe(true);
  // Synthetic backend denies both direct membership and payment writes through the existing proxy.
  const denials = await page.evaluate(async () => {
    const outcomes = [];
    for (const path of ["AdminProgrammeParticipation/Approve", "ProgrammeParticipation/StartEntry", "YocoWebhook/Receive"]) {
      const response = await fetch(`/api/backend/api/services/app/${path}`, { method: "POST", headers: { "Content-Type": "application/json" }, body: "{}" });
      outcomes.push({ status: response.status, recovery: response.headers.get("X-Aqua-Recovery-Suspended") });
    }
    return outcomes;
  });
  expect(denials).toEqual(Array(3).fill({ status: 503, recovery: "true" }));
  await page.goto("/admin/customers");
  await expect(page.getByText(/Restricted recovery mode is active/)).toBeVisible();
  await expect(page.getByRole("heading", { name: "Customer accounts" })).toHaveCount(0);
  // Verify discarded routes are absent without mounting an unrelated generic 404 page.
  const removedPage = await page.evaluate(async () => (await fetch("/admin/b6-discovery")).status);
  expect(removedPage).toBe(404);
  const removedProxy = await page.evaluate(async () => (await fetch("/api/recovery/b6?operation=discovery")).status);
  expect(removedProxy).toBe(404);
  expect(errors).toEqual([]);
});

test("ordinary dashboard retains existing authorized management controls outside recovery", async ({ page }) => {
  await page.route("**/api/backend/**", async route => {
    const path = new URL(route.request().url()).pathname.replace("/api/backend", "");
    if (path.endsWith("AQGreenPilot/GetStatus"))
      return route.fulfill({ json: { isPilot: false, testMoneyOnly: false } });
    if (path in uiFixtures) return route.fulfill({ json: uiFixtures[path] });
    return route.continue();
  });
  await signIn(page);
  await expect(page.getByRole("heading", { name: "Administration overview" })).toBeVisible();
  await expect(page.getByRole("region", { name: "Management", exact: true }).locator('a[href="/admin/customers"]')).toBeVisible();
  await expect(page.getByText(/Restricted recovery mode is active/)).toHaveCount(0);
  await expect(page.getByRole("button", { name: "Refresh data" })).toBeEnabled();
});

test("a signed-in member cannot enter the administrator dashboard in recovery", async ({ page }) => {
  await signIn(page, "member@example.test");
  await page.goto("/admin/dashboard");
  await expect(page).toHaveURL("/dashboard");
  await expect(page.getByRole("heading", { name: "Administration overview" })).toHaveCount(0);
});

test("an anonymous visitor must sign in before entering the existing administrator route", async ({ page }) => {
  await page.goto("/admin/dashboard");
  await expect(page).toHaveURL(/\/login\?redirect=%2Fadmin%2Fdashboard/);
  await expect(page.getByRole("button", { name: "Sign in", exact: true })).toBeVisible();
  await expect(page.getByRole("heading", { name: "Administration overview" })).toHaveCount(0);
});
