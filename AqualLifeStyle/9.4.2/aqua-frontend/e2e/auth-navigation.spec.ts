import { expect, test, type Page } from "@playwright/test";

const observeBrowserErrors = (page: Page) => {
  const consoleErrors: string[] = [];
  const pageErrors: string[] = [];
  page.on("console", (message) => {
    if (message.type() === "error") {
      const source = message.location().url;
      consoleErrors.push(source ? `${message.text()} (${source})` : message.text());
    }
  });
  page.on("pageerror", (error) => pageErrors.push(error.message));
  return { consoleErrors, pageErrors };
};

const signIn = async (page: Page, email = "member@example.test") => {
  await page.goto("/login");
  await page.getByLabel("Username or email").fill(email);
  await page.getByLabel("Password", { exact: true }).fill("test-password");
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page).toHaveURL("/dashboard");
  await expect(page.getByRole("heading", { name: "Test Member" })).toBeVisible();
};

test("anonymous route policy does not expose protected content", async ({ page }) => {
  const errors = observeBrowserErrors(page);

  await page.goto("/");
  await expect(page).toHaveURL("/");
  await expect(page.getByRole("banner").getByRole("link", { name: "Member access" })).toBeVisible();

  await page.goto("/dashboard");
  await expect(page).toHaveURL(/\/login\?redirect=%2Fdashboard$/);
  await expect(page.getByRole("heading", { name: "Sign in to your account" })).toBeVisible();
  await expect(page.getByText("Products for you")).toHaveCount(0);
  await expect(page.getByText("Business Premier Bundle")).toHaveCount(0);
  expect(errors.consoleErrors).toEqual([]);
  expect(errors.pageErrors).toEqual([]);
});

test("session survives reload, a new tab, and programme navigation", async ({ context, page }) => {
  const errors = observeBrowserErrors(page);
  await signIn(page);

  await page.reload();
  await expect(page.getByRole("heading", { name: "Test Member" })).toBeVisible();
  await page.getByRole("link", { name: "View programmes" }).click();
  await expect(page).toHaveURL("/member/programmes");
  await expect(page.getByRole("heading", { name: "AQGreen", exact: true })).toBeVisible();

  const secondPage = await context.newPage();
  const secondPageErrors = observeBrowserErrors(secondPage);
  await secondPage.goto("/");
  await expect(secondPage).toHaveURL("/dashboard");
  await secondPage.goto("/login");
  await expect(secondPage).toHaveURL("/dashboard");
  await secondPage.goto("/member/programmes");
  await expect(secondPage.getByRole("heading", { name: "My programme journey" })).toBeVisible();
  await expect(secondPage.getByRole("heading", { name: "AQGreen", exact: true })).toBeVisible();
  await expect(secondPage.getByText(/programme journey is unavailable/i)).toHaveCount(0);

  await page.goto("/dashboard");
  if (test.info().project.name === "mobile") {
    await page.getByRole("button", { name: "Open navigation" }).click();
  }
  await page.getByRole("navigation", { name: test.info().project.name === "mobile" ? "Mobile navigation" : "Primary navigation" })
    .getByRole("link", { name: "Programmes" })
    .click();
  await expect(page).toHaveURL("/member/programmes");
  await expect(page.getByRole("heading", { name: "AQGreen", exact: true })).toBeVisible();

  expect(errors.consoleErrors).toEqual([]);
  expect(errors.pageErrors).toEqual([]);
  expect(secondPageErrors.consoleErrors).toEqual([]);
  expect(secondPageErrors.pageErrors).toEqual([]);
});

test("logout invalidates the cookie and browser history cannot restore access", async ({ page }) => {
  const errors = observeBrowserErrors(page);
  await signIn(page);

  await page.getByRole("button", { name: "Open user menu" }).click();
  await page.getByRole("button", { name: "Sign out" }).click();
  await expect(page).toHaveURL("/");
  await page.goto("/dashboard");
  await expect(page).toHaveURL(/\/login\?redirect=%2Fdashboard$/);
  await expect(page.getByRole("heading", { name: "Sign in to your account" })).toBeVisible();

  expect(errors.consoleErrors).toEqual([]);
  expect(errors.pageErrors).toEqual([]);
});

test("expired sessions fail closed on protected deep links", async ({ page }) => {
  await signIn(page, "expiring@example.test");
  await page.waitForTimeout(1_200);
  await page.goto("/member/programmes?source=expiry-test");
  await expect(page).toHaveURL(/\/login\?redirect=%2Fmember%2Fprogrammes%3Fsource%3Dexpiry-test$/);
  await expect(page.getByRole("heading", { name: "Sign in to your account" })).toBeVisible();
});
