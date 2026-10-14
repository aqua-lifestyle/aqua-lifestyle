import { expect, test, type Page } from "@playwright/test";

test.skip(process.env.AQGREEN_V2_DEMO_REAL_E2E !== "true",
  "Requires the isolated disposable AQGreenV2Demo stack.");
// The synthetic verification link is used in memory only; never retain a
// browser trace containing its one-time fragment.
test.use({ trace: "off" });

const required = (name: string) => {
  const value = process.env[name];
  if (!value) throw new Error(`${name} is required for the disposable browser test.`);
  return value;
};

const signIn = async (page: Page, user: string, password: string, workspace = "Area workspace") => {
  await page.goto("/login");
  await page.getByLabel("Workspace").selectOption({ label: workspace });
  await page.getByLabel("Username or email").fill(user);
  await page.getByLabel("Password", { exact: true }).fill(password);
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page).toHaveURL(/\/dashboard$|\/i\//);
};

const readSyntheticVerificationLink = async (browser: import("@playwright/test").Browser, email: string) => {
  const adminContext = await browser.newContext();
  try {
    const admin = await adminContext.newPage();
    await signIn(admin, "admin", required("AQGREEN_V2_DEMO_ADMIN_PASSWORD"));
    for (let attempt = 0; attempt < 30; attempt++) {
      const response = await admin.request.post(
        "/api/backend/api/demo/aqgreen-verification-link", {
          data: { email },
          headers: { origin: "http://127.0.0.1:3000" },
        });
      if (response.ok()) {
        const payload = await response.json();
        const link = payload.result?.verificationUrl ?? payload.verificationUrl;
        if (typeof link !== "string") throw new Error("The synthetic test mailbox returned no link.");
        return link as string;
      }
      if (response.status() !== 404) throw new Error(`Synthetic test mailbox returned ${response.status()}.`);
      await new Promise((resolve) => setTimeout(resolve, 500));
    }
    throw new Error("The synthetic email-verification outbox record was not created.");
  } finally {
    await adminContext.close();
  }
};

test("anonymous signup and two distinct R600 test instalments reach authorized placement", async ({ browser }) => {
  test.setTimeout(180_000);
  const sponsorContext = await browser.newContext();
  const sponsor = await sponsorContext.newPage();
  await signIn(sponsor, "aqgreen.demo.member", required("AQGREEN_V2_DEMO_MEMBER_PASSWORD"));
  await sponsor.goto("/member/invitations");
  const card = sponsor.getByRole("article")
    .filter({ has: sponsor.getByRole("heading", { name: "AQGreen", exact: true }) });
  const code = (await card.getByText("Invitation code")
    .locator("..").locator("p").nth(1).textContent())?.trim();
  expect(code).toBeTruthy();
  await sponsorContext.close();

  const context = await browser.newContext();
  const page = await context.newPage();
  const providerRequests: string[] = [];
  page.on("request", (request) => {
    if (/payments\.yoco\.com/i.test(request.url())) providerRequests.push(request.url());
  });
  await page.goto(`/i/${code}`);
  await expect(page.getByRole("heading", { name: "Join AQGreen" })).toBeVisible();
  await page.getByRole("link", { name: "Create my account" }).click();
  await expect(page.getByRole("heading", { name: "Create your customer account" })).toBeVisible();
  const email = `aqgreen.operational.${Date.now()}@example.test`;
  const password = required("AQGREEN_V2_DEMO_MEMBER_PASSWORD");
  await page.getByLabel("Email address").fill(email);
  await page.getByLabel("Password", { exact: true }).fill(password);
  await page.getByLabel("Confirm password").fill(password);
  await page.getByRole("button", { name: "Next", exact: true }).click();
  await page.getByLabel("First name").fill("Synthetic");
  await page.getByLabel("Surname").fill("Instalment Member");
  await page.getByLabel("Contact number").fill("0820000000");
  await page.getByLabel("Home address").fill("Isolated synthetic test address");
  await page.getByRole("button", { name: "Next", exact: true }).click();
  await page.getByRole("checkbox").check();
  await page.getByRole("button", { name: "Create account" }).click();
  await expect(page).toHaveURL(/\/verify-email-sent\?/);

  const verificationLink = await readSyntheticVerificationLink(browser, email);
  await page.goto(verificationLink);
  await page.getByRole("button", { name: "Verify email" }).click();
  await expect(page.getByText("Your email is verified. You can now sign in.")).toBeVisible();
  await page.getByRole("link", { name: "Continue to sign in" }).click();
  await page.getByLabel("Username or email").fill(email);
  await page.getByLabel("Password", { exact: true }).fill(password);
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page).toHaveURL(new RegExp(`/i/${code}$`));
  const deniedMailbox = await page.request.post(
    "/api/backend/api/demo/aqgreen-verification-link", {
      data: { email },
      headers: { origin: "http://127.0.0.1:3000" },
    });
  expect(deniedMailbox.status()).toBe(403);
  await page.getByLabel("Pay two R600 instalments").check();
  await page.getByRole("button", { name: "Accept and open simulated checkout" }).click();
  await expect(page).toHaveURL(/\/test\/aqgreen-checkout$/);
  await expect(page.getByText(/R\s*600,00/)).toBeVisible();
  const firstRecord = (await (await page.request.get(
    "/api/backend/api/demo/aqgreen-checkout")).json()).result;
  expect(firstRecord.amount).toBe(600);
  const wrongAmount = await page.request.post("/api/backend/api/demo/aqgreen-checkout/confirm", {
    data: { checkoutId: firstRecord.checkoutId, amount: 1200 },
    headers: { origin: "http://127.0.0.1:3000" },
  });
  expect(wrongAmount.status()).toBe(400);
  await page.getByRole("button", { name: "Confirm simulated payment" }).click();
  await expect(page.getByText(/This test instalment is confirmed/)).toBeVisible();
  const firstRetry = await page.request.post("/api/backend/api/demo/aqgreen-checkout/confirm", {
    data: { checkoutId: firstRecord.checkoutId, amount: 600 },
    headers: { origin: "http://127.0.0.1:3000" },
  });
  expect((await firstRetry.json()).result.alreadyConfirmed).toBe(true);
  await page.getByRole("link", { name: "View my programme status" }).click();
  await expect(page.getByText("Two instalments")).toBeVisible();
  await expect(page.getByText("R 600,00 remains")).toBeVisible();
  await page.getByRole("button", { name: /Complete joining:.*600/ }).click();
  await expect(page).toHaveURL(/\/test\/aqgreen-checkout$/);
  const secondRecord = (await (await page.request.get(
    "/api/backend/api/demo/aqgreen-checkout")).json()).result;
  expect(secondRecord.amount).toBe(600);
  expect(secondRecord.checkoutId).not.toBe(firstRecord.checkoutId);
  await page.getByRole("button", { name: "Confirm simulated payment" }).click();
  await expect(page.getByText(/pending Area approval/)).toBeVisible();
  const secondRetry = await page.request.post("/api/backend/api/demo/aqgreen-checkout/confirm", {
    data: { checkoutId: secondRecord.checkoutId, amount: 600 },
    headers: { origin: "http://127.0.0.1:3000" },
  });
  expect((await secondRetry.json()).result.alreadyConfirmed).toBe(true);
  await page.goto("/member/programmes");
  await expect(page.getByText("Awaiting Area approval").first()).toBeVisible();
  const deniedAdmin = await page.request.get(
    "/api/backend/api/services/app/AdminProgrammeParticipation/GetAll?MaxResultCount=1");
  expect(deniedAdmin.status()).toBe(403);

  const adminContext = await browser.newContext();
  const admin = await adminContext.newPage();
  await signIn(admin, "admin", required("AQGREEN_V2_DEMO_ADMIN_PASSWORD"));
  await admin.goto("/admin/programme-participations");
  const row = admin.getByRole("row").filter({ hasText: email });
  await expect(row).toBeVisible();
  await row.getByRole("button", { name: "Approve" }).click();
  await expect(row.getByText("Active")).toBeVisible();
  await adminContext.close();
  // Approval changes the member role and invalidates the old session token.
  await context.clearCookies();
  await signIn(page, email, password);
  await page.goto("/member/programmes");
  await expect(page.getByText("Active", { exact: true }).first()).toBeVisible();
  expect(providerRequests).toEqual([]);
  await context.close();
});
