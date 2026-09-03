import { expect, test, type Page } from "@playwright/test";

test.skip(
  process.env.AQGREEN_V2_DEMO_REAL_E2E !== "true",
  "Runs only against the explicitly started NON-PRODUCTION AQGreen V2 demo stack.",
);

const required = (name: string) => {
  const value = process.env[name];
  if (!value) throw new Error(`${name} is required for the real demo E2E.`);
  return value;
};

const signIn = async (
  page: Page,
  userName: string,
  password: string,
  workspace: "area" | "host",
) => {
  await page.goto("/login");
  const workspaceSelect = page.getByLabel("Workspace");
  const expectedWorkspace = workspace === "host" ? "" : "Default";
  await expect.poll(async () => {
    await workspaceSelect.selectOption(
      workspace === "host"
        ? { label: "Platform administration" }
        : { label: "Area workspace" },
    );
    return workspaceSelect.inputValue();
  }).toBe(expectedWorkspace);
  await page.getByLabel("Username or email").fill(userName);
  await page.getByLabel("Password", { exact: true }).fill(password);
  await page.getByRole("button", { name: "Sign in" }).click();
};

test("real host review creates a durable R400 ledger visible to the real member", async ({ browser }) => {
  const adminContext = await browser.newContext();
  const adminPage = await adminContext.newPage();
  await signIn(
    adminPage,
    "admin",
    required("AQGREEN_V2_DEMO_ADMIN_PASSWORD"),
    "host",
  );
  await expect(adminPage).toHaveURL(/\/admin\/dashboard$/);

  await adminPage.goto("/admin/weekly-sales-reviews");
  await expect(adminPage.getByRole("heading", { name: "AQGreen weekly sales reviews" })).toBeVisible();
  const memberRow = adminPage.getByRole("row", { name: /AQGreen V2 Demo Member/ });
  await expect(memberRow.getByText("Held for evidence")).toBeVisible();
  await memberRow.getByRole("button", { name: "Review" }).click();
  await adminPage.getByLabel("Spray verified quantity").fill("5");
  await adminPage.getByLabel("1L verified quantity").fill("5");
  await adminPage.getByLabel("5L verified quantity").fill("5");
  await adminPage.getByLabel("Evidence references (one per line)")
    .fill("demo-browser:real-playwright-5-5-5");
  await adminPage.getByRole("button", { name: "Confirm sales" }).click();
  await expect(adminPage.getByText("Confirmed · Met").first()).toBeVisible();
  await expect(adminPage.getByText("Met", { exact: true })).toBeVisible();
  await expect(adminPage.getByLabel("Spray verified quantity")).toHaveCount(0);

  await adminPage.goto("/admin/weekly-earnings");
  await adminPage.getByLabel("Area").selectOption("1");
  await adminPage.getByRole("button", { name: "Prepare weekly earnings" }).click();
  await expect(adminPage.getByText(/1 earned R\s*400[,.]00 in total/)).toBeVisible();
  await adminPage.getByRole("searchbox", { name: "Search..." })
    .fill("aqgreen.demo.member@example.test");
  const earningRow = adminPage.getByRole("row", { name: /AQGreen V2 Demo Member/ });
  await expect(earningRow.getByText("Commissioned level: 2")).toBeVisible();
  await expect(earningRow.getByText(/R\s*400[,.]00/).first()).toBeVisible();
  await expect(earningRow.getByText("Earned — awaiting release")).toBeVisible();
  await adminContext.close();

  const memberContext = await browser.newContext();
  const memberPage = await memberContext.newPage();
  await signIn(
    memberPage,
    "aqgreen.demo.member",
    required("AQGREEN_V2_DEMO_MEMBER_PASSWORD"),
    "area",
  );
  await expect(memberPage).toHaveURL(/\/dashboard$/);
  await memberPage.goto("/member/programmes");
  const aqGreen = memberPage.getByRole("article", { name: "AQGreen" });
  await expect(aqGreen.getByText("Structurally qualified through Level 2.")).toBeVisible();
  await expect(aqGreen.getByText("Earned — awaiting release")).toBeVisible();
  await expect(aqGreen.getByText("Qualified depth: Level 2 · Commissioned depth: Level 2")).toBeVisible();
  await expect(aqGreen.getByText("Level 1 component")).toBeVisible();
  await expect(aqGreen.getByText(/R\s*150[,.]00/).first()).toBeVisible();
  await expect(aqGreen.getByText("Level 2 component")).toBeVisible();
  await expect(aqGreen.getByText(/R\s*250[,.]00/).first()).toBeVisible();
  await expect(aqGreen.getByText(/R\s*400[,.]00/).first()).toBeVisible();
  await expect(aqGreen.getByText(/21 Aug 2026.*27 Aug 2026/)).toBeVisible();
  await expect(aqGreen.getByText("Paid", { exact: true })).toHaveCount(0);
  await memberContext.close();
});
