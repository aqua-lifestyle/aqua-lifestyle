import { expect, test, type Page } from "@playwright/test";

test.skip(process.env.AQGREEN_V2_DEMO_REAL_E2E !== "true",
  "Requires the explicitly started disposable AQGreenV2Demo stack.");

const required = (name: string) => {
  const value = process.env[name];
  if (!value) throw new Error(`${name} is required for the disposable browser test.`);
  return value;
};

const signIn = async (page: Page, user: string, password: string,
  workspace: "area" | "host" = "area") => {
  await page.goto("/login");
  await page.getByLabel("Workspace").selectOption(
    workspace === "host" ? { label: "Platform administration" } : { label: "Area workspace" });
  await page.getByLabel("Username or email").fill(user);
  await page.getByLabel("Password", { exact: true }).fill(password);
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page).toHaveURL(workspace === "host" ? /\/admin\/dashboard$/ : /\/dashboard$/);
};

test("first-time invited guest completes test checkout and Area approval without provider traffic", async ({ browser }) => {
  test.setTimeout(180_000);
  const providerRequests: string[] = [];
  const sponsorContext = await browser.newContext();
  const sponsor = await sponsorContext.newPage();
  sponsor.on("request", (request) => {
    if (/payments\.yoco\.com/i.test(request.url())) providerRequests.push(request.url());
  });
  await signIn(sponsor, "aqgreen.demo.member", required("AQGREEN_V2_DEMO_MEMBER_PASSWORD"));
  await sponsor.goto("/member/invitations");
  await expect(sponsor.getByRole("heading", { name: "Invite Club Members" })).toBeVisible();
  const inviteCard = sponsor.getByRole("article").filter({ has: sponsor.getByRole("heading", { name: "AQGreen", exact: true }) });
  const code = (await inviteCard.getByText("Invitation code").locator("..").locator("p").nth(1).textContent())?.trim();
  expect(code).toBeTruthy();

  const guestContext = await browser.newContext();
  const guest = await guestContext.newPage();
  guest.on("request", (request) => {
    if (/payments\.yoco\.com/i.test(request.url())) providerRequests.push(request.url());
  });
  await signIn(guest, "aqgreen.demo.guest", required("AQGREEN_V2_DEMO_MEMBER_PASSWORD"));
  await guest.goto("/i/ZZZZZZZZZZZZ");
  await expect(guest.getByText("This invitation link is not valid.")).toBeVisible();
  await guest.goto(`/i/${code}`);
  await expect(guest.getByText("TEST ENVIRONMENT — SIMULATED MONEY — NO REAL PAYMENT OR EARNINGS").first()).toBeVisible();
  await expect(guest.getByRole("heading", { name: "Join AQGreen" })).toBeVisible();
  await expect(guest.getByText("AQGreen V2 Demo Member")).toBeVisible();
  await guest.getByRole("button", { name: "Accept and open simulated checkout" }).click();
  await expect(guest).toHaveURL(/\/test\/aqgreen-checkout$/);
  await expect(guest.getByText(/R\s*1\s*200,00/)).toBeVisible();

  const checkout = await guest.request.get("/api/backend/api/demo/aqgreen-checkout");
  expect(checkout.ok()).toBeTruthy();
  const checkoutRecord = (await checkout.json()).result;
  expect(checkoutRecord.amount).toBe(1200);
  const wrongAmount = await guest.request.post("/api/backend/api/demo/aqgreen-checkout/confirm", {
    data: { checkoutId: checkoutRecord.checkoutId, amount: 1199 },
    headers: { origin: "http://127.0.0.1:3000" },
  });
  expect(wrongAmount.status()).toBe(400);

  await guest.getByRole("button", { name: "Confirm simulated payment" }).click();
  await expect(guest.getByText(/pending Area approval/)).toBeVisible();
  const duplicate = await guest.request.post("/api/backend/api/demo/aqgreen-checkout/confirm", {
    data: { checkoutId: checkoutRecord.checkoutId, amount: 1200 },
    headers: { origin: "http://127.0.0.1:3000" },
  });
  expect(duplicate.ok()).toBeTruthy();
  expect((await duplicate.json()).result.alreadyConfirmed).toBe(true);
  const otherMemberCheckout = await sponsor.request.post(
    "/api/backend/api/demo/aqgreen-checkout/confirm", {
      data: { checkoutId: checkoutRecord.checkoutId, amount: 1200 },
      headers: { origin: "http://127.0.0.1:3000" },
    });
  expect(otherMemberCheckout.status()).toBe(404);
  await sponsorContext.close();
  await guest.goto("/member/programmes");
  await expect(guest.getByText("Awaiting Area approval").first()).toBeVisible();
  const deniedAdmin = await guest.request.get(
    "/api/backend/api/services/app/AdminProgrammeParticipation/GetAll?MaxResultCount=1");
  expect(deniedAdmin.status()).toBe(403);

  const adminContext = await browser.newContext();
  const admin = await adminContext.newPage();
  await signIn(admin, "admin", required("AQGREEN_V2_DEMO_ADMIN_PASSWORD"));
  await admin.goto("/admin/programme-participations");
  const guestRow = admin.getByRole("row", { name: /AQGreen V2 Demo First-time Guest/ });
  await expect(guestRow).toBeVisible();
  await guestRow.getByRole("button", { name: "Approve" }).click();
  await expect(admin.getByText(/First-time Guest's AQGreen participation was approved and activated/)).toBeVisible();
  await expect(guestRow.getByText("Active")).toBeVisible();
  await adminContext.close();

  // Approval changes the member's role/permissions and invalidates the prior
  // browser session. Sign in again to observe the authoritative active state.
  await signIn(guest, "aqgreen.demo.guest", required("AQGREEN_V2_DEMO_MEMBER_PASSWORD"));
  await guest.goto("/member/programmes");
  await expect(guest.getByText("Active", { exact: true }).first()).toBeVisible();
  await guest.goto("/member/programme-progress");
  await expect(guest.getByText("Qualifying placement occupants").first()).toBeVisible();
  await expect(guest.getByText("0 / 5").first()).toBeVisible();
  await expect(guest.getByText(/Paid/, { exact: true })).toHaveCount(0);
  expect(providerRequests).toEqual([]);
  await guestContext.close();
});
