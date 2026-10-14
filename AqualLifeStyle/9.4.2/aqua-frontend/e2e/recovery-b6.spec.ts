import { test, expect } from "@playwright/test";

test("existing login session reaches recovery workspace and scoped aggregate discovery across reloads", async ({ page }) => {
  const errors: string[] = [];
  page.on("pageerror", error => errors.push(error.message));
  await page.goto("/login");
  await page.getByLabel("Username or email").fill("admin-b6-good@example.test");
  await page.getByLabel("Password", { exact: true }).fill("test-password");
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await expect(page.getByRole("heading", { name: "Recovery administrator workspace" })).toBeVisible();
  await expect(page.getByText("Tenant 1: Synthetic Tenant (Default)")).toBeVisible();
  await expect(page.getByText("a0000000-0000-0000-0000-000000000001")).toBeVisible();
  await page.getByRole("link", { name: "Open bounded B6 discovery" }).click();
  await expect(page.getByText("Examined 3 of 3 current-scope records. Limit: 100.")).toBeVisible();
  await expect(page.getByText("JOINING_PAYMENT_LINKS_CONFLICT", { exact: true })).toBeVisible();
  await expect(page.getByText("SPONSOR_HISTORY_UNVERIFIABLE", { exact: true })).toBeVisible();
  await expect(page.getByText("SENSITIVE_SYNTHETIC_MARKER")).toHaveCount(0);
  await page.reload();
  await expect(page.getByText("Examined 3 of 3 current-scope records. Limit: 100.")).toBeVisible();
  const response = await page.evaluate(async () => {
    const result = await fetch("/api/recovery/b6?operation=discovery", { credentials: "same-origin", cache: "no-store" });
    return { status: result.status, body: await result.text() };
  });
  const body = response.body;
  expect(response.status).toBe(200);
  expect(body).not.toContain("SENSITIVE_SYNTHETIC_MARKER");
  expect(body).not.toContain("accessToken");
  await page.goto("/admin/weekly-earnings");
  await expect(page.getByText("Establishing the application capability boundary…")).toBeVisible();
  expect(errors).toEqual([]);
});

for (const [scenario, message] of [
  ["host", "host-scoped session"], ["wrong-tenant", "not verified for Tenant 1"],
  ["wide", "multiple Area assignments"], ["wrong-area", "does not match the original Johannesburg"],
  ["no-area-permission", "application denied assigned-Area access"],
  ["no-discovery-permission", "application denied B6 discovery permission"],
] as const) {
  test(`scope and permission refusal stop discovery for ${scenario}`, async ({ page, request }) => {
    const username = `admin-b6-${scenario}`;
    await page.goto("/login");
    await page.getByLabel("Username or email").fill(`${username}@example.test`);
    await page.getByLabel("Password", { exact: true }).fill("test-password");
    await page.getByRole("button", { name: "Sign in", exact: true }).click();
    await expect(page.getByRole("heading", { name: "Recovery administrator workspace" })).toBeVisible();
    await page.goto("/admin/b6-discovery");
    await expect(page.getByRole("alert").filter({ hasText: message })).toBeVisible();
    await expect(page.getByRole("heading", { name: "Observed historical inventory" })).toHaveCount(0);
    const state = await request.get(`http://127.0.0.1:3200/__recovery-test-observation?scenario=${username}`);
    expect((await state.json()).result.discoveryReads).toBe(0);
  });
}
