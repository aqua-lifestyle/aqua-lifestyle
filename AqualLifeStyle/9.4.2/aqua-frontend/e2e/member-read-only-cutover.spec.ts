import { expect, test } from "@playwright/test";

// Browser contract test with the existing synthetic backend. Actual PostgreSQL
// read-only enforcement and denied writes are tested against the published receiver.
test("member read sections remain usable with checkout capabilities held", async ({ page }) => {
  await page.route("**/api/backend/api/health", async (route) => {
    const response = await route.fetch();
    const body = await response.json();
    body.contractCapabilities = ["member-programme-journey-v1"];
    await route.fulfill({ response, json: body });
  });
  const failedReads: string[] = [];
  page.on("response", (response) => {
    if (response.request().method() === "GET" && response.url().includes("/api/backend/") && response.status() >= 400)
      failedReads.push(new URL(response.url()).pathname + ":" + response.status());
  });
  await page.goto("/login?redirect=%2Fmember");
  await page.getByLabel("Username or email").fill("member@example.test");
  await page.getByLabel("Password", { exact: true }).fill("test-password");
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page).toHaveURL("/member");
  for (const [path, heading, loaded] of [
    ["/member", "Club Member dashboard", "Recent orders"],
    ["/products", "Products", "Water filter"],
    ["/member/programmes", "My programme journey", "Earned — awaiting release"],
    ["/member/savings", "My savings", "No savings account"],
    ["/member/loans", "My loans", "No loan agreements"],
    ["/member/orders", "My orders", "No orders"],
    ["/member/enquiries", "My enquiries", "No enquiries"],
    ["/member/entry-commitments", "AQGreen commitments", "No AQGreen commitments"],
  ]) {
    await page.goto(path);
    await expect(page.getByRole("heading", { name: heading, exact: true })).toBeVisible();
    await expect(page.getByText(loaded, { exact: true }).first()).toBeVisible();
    await expect(page.getByText("Establishing the application capability boundary…")).toHaveCount(0);
    if (path === "/member/programmes") {
      await expect(page.getByRole("article", { name: "AQGreen" })).toBeVisible();
      await expect(page.getByRole("article", { name: "Onyx" })).toBeVisible();
      await expect(page.getByText("Level 1 component")).toBeVisible();
      await expect(page.getByText("Level 2 component")).toBeVisible();
    }
  }
  await page.goto("/member/programme-progress");
  await expect(page).toHaveURL("/member/programmes");
  await expect(page.getByRole("heading", { name: "My programme journey" })).toBeVisible();
  expect(failedReads).toEqual([]);
});
