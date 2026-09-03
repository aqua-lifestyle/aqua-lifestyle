import { expect, test, type Page } from "@playwright/test";

const signIn = async (page: Page, email: string, redirect: string) => {
  await page.goto(`/login?redirect=${encodeURIComponent(redirect)}`);
  await page.getByLabel("Username or email").fill(email);
  await page.getByLabel("Password", { exact: true }).fill("test-password");
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page).toHaveURL(redirect);
};

test("member sees the Level 2 R400 ledger as earned and the Onyx graduation", async ({ page }) => {
  await signIn(page, "member@example.test", "/member/programmes");

  const aqGreen = page.getByRole("article", { name: "AQGreen" });
  await expect(aqGreen.getByText("Structurally qualified through Level 2.")).toBeVisible();
  await expect(aqGreen.getByText("Earned — awaiting release")).toBeVisible();
  await expect(aqGreen.getByText("Qualified depth: Level 2 · Commissioned depth: Level 2")).toBeVisible();
  await expect(aqGreen.getByText("Level 1 component")).toBeVisible();
  await expect(aqGreen.getByText("Level 2 component")).toBeVisible();
  await expect(aqGreen.getByText(/R\s*400[,.]00/).first()).toBeVisible();
  await expect(aqGreen.getByText(/21 Aug 2026.*27 Aug 2026/)).toBeVisible();
  await expect(aqGreen.getByText("Paid", { exact: true })).toHaveCount(0);

  const onyx = page.getByRole("article", { name: "Onyx" });
  await expect(onyx.getByText("AQGreen graduation with an Onyx loan")).toBeVisible();
  await expect(onyx.getByText("Active", { exact: true }).first()).toBeVisible();
});

test("host reviews 5/5/5 and sees the system-computed Met result and R400", async ({ page }, testInfo) => {
  await signIn(
    page,
    `admin-${testInfo.project.name}@example.test`,
    "/admin/programme-participations",
  );

  await page.getByRole("link", { name: "Review weekly sales" }).click();
  await expect(page).toHaveURL(/\/admin\/weekly-sales-reviews/);
  await expect(page.getByText("Held for evidence").first()).toBeVisible();
  await page.getByLabel("Spray verified quantity").fill("5");
  await page.getByLabel("1L verified quantity").fill("5");
  await page.getByLabel("5L verified quantity").fill("5");
  await page.getByLabel("Evidence references (one per line)")
    .fill("ticket:playwright-weekly-sales");
  await page.getByRole("button", { name: "Confirm sales" }).click();

  await expect(page.getByText("Confirmed · Met").first()).toBeVisible();
  await expect(page.getByText("System result")).toBeVisible();
  await expect(page.getByText("Met", { exact: true })).toBeVisible();
  await expect(page.getByLabel("Spray verified quantity")).toHaveCount(0);

  await page.goto("/admin/weekly-earnings");
  await expect(page.getByText("Commissioned level: 2")).toBeVisible();
  await expect(page.getByText(/R\s*400[,.]00/).first()).toBeVisible();
  await expect(page.getByText("Earned — awaiting release")).toBeVisible();
});
