import { readFileSync } from "node:fs";
import { expect, test, type Page } from "@playwright/test";

test.skip(!process.env.PILOT_BROWSER_FIXTURE_OUTPUT, "Requires disposable prospective identities and the actual isolated pilot API.");
const fixtures = () => JSON.parse(readFileSync(process.env.PILOT_BROWSER_FIXTURE_OUTPUT!, "utf8")) as {
  TestOnly: boolean; TenantId: number; Tenant: string;
  Root: { UserName: string; CustomerId: number };
  Member: { UserName: string; CustomerId: number };
};
test.beforeEach(async ({ request }) => {
  await expect.poll(async () => {
    try {
      const response = await request.get("http://127.0.0.1:5000/api/services/app/AQGreenPilot/GetStatus");
      return response.ok() ? (await response.json()).result : null;
    } catch { return null; }
  }, { timeout: 60_000 }).toEqual({ isPilot: true, testMoneyOnly: true });
});
async function signIn(page: Page, username: string, password: string, host = false) {
  await page.goto("/login");
  await page.getByLabel("Workspace").selectOption(host ? "" : "Default");
  await page.getByLabel("Username or email").fill(username);
  await page.getByLabel("Password", { exact: true }).fill(password);
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  await expect(page).not.toHaveURL(/\/login/);
  await expect(page.getByRole("note")).toContainText("TEST MONEY ONLY");
}

test("prospective root and invited first member use real test payment and Area approval", async ({ browser }) => {
  const identities = fixtures();
  expect(identities.TestOnly).toBe(true);
  const administratorPassword = process.env.PILOT_ADMIN_PASSWORD!;
  const memberPassword = process.env.PILOT_MEMBER_PASSWORD!;
  expect(administratorPassword).toBeTruthy(); expect(memberPassword).toBeTruthy();
  const hostContext = await browser.newContext(); const host = await hostContext.newPage();
  await signIn(host, "admin", administratorPassword, true);
  await host.goto("/admin/programme-participations");
  await host.getByLabel("Exact Tenant ID").fill(String(identities.TenantId));
  await host.getByRole("button", { name: "Initialize test commission configuration" }).click();
  await expect(host.getByText(/prospective Tenant baseline are recorded/)).toBeVisible();
  await host.getByLabel("Exact prospective customer ID").fill(String(identities.Root.CustomerId));
  await host.getByLabel("Root admission reason").fill("TEST-ONLY browser prospective root admission");
  await host.getByRole("button", { name: "Authorize prospective root", exact: true }).click();
  await expect(host.getByText(/Prospective root admission recorded/)).toBeVisible();

  const rootContext = await browser.newContext(); const root = await rootContext.newPage();
  await signIn(root, identities.Root.UserName, memberPassword);
  await root.goto("/member/programmes");
  const rootClubNumber = await root.evaluate(async () => (await (await fetch("/api/backend/api/services/app/ClubMemberProgrammeParticipation/GetMyParticipations")).json()).result.clubMemberNumber);
  await root.getByRole("button", { name: "Pay R1,200 once", exact: true }).click();
  await expect(root.getByRole("button", { name: "Confirm test joining payment", exact: true })).toBeVisible();
  const pending = await root.evaluate(async () => (await (await fetch("/api/backend/api/services/app/ClubMemberProgrammeParticipation/GetMyParticipations")).json()).result.pendingAQGreenCheckout);
  expect(pending?.checkoutId).toBeTruthy();
  const strangerContext = await browser.newContext(); const stranger = await strangerContext.newPage();
  await signIn(stranger, identities.Member.UserName, memberPassword);
  const deniedCheckout = await stranger.evaluate(async (checkoutId) => {
    const response = await fetch("/api/backend/api/services/app/AQGreenPilot/ConfirmTestPayment", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ checkoutId }) });
    return { status: response.status, payload: await response.json() };
  }, pending.checkoutId);
  expect([401, 403]).toContain(deniedCheckout.status);
  expect(deniedCheckout.payload.error.message).toContain("does not belong to the current member");
  await strangerContext.close();
  await root.getByRole("button", { name: "Confirm test joining payment", exact: true }).click();
  await expect(root.getByText("Awaiting Area approval", { exact: true }).first()).toBeVisible();
  await host.reload();
  await host.getByRole("button", { name: "Awaiting approval", exact: true }).click();
  await host.getByRole("row").filter({ hasText: rootClubNumber }).getByRole("button", { name: "Approve", exact: true }).click();
  await expect(host.getByText(/browser-root.*approved and activated/)).toBeVisible();
  await rootContext.close();

  const activeContext = await browser.newContext(); const activeRoot = await activeContext.newPage();
  await signIn(activeRoot, identities.Root.UserName, memberPassword);
  await activeRoot.goto("/member/invitations");
  const invitations = await activeRoot.evaluate(async () => {
    const response = await fetch("/api/backend/api/services/app/ProgrammeInvitation/GetMyInvitations");
    return { status: response.status, payload: await response.json() };
  });
  expect(invitations.status).toBe(200);
  const invitation = invitations.payload.result.invitations.find((item: { programmeKey: string }) => item.programmeKey === "AQGREEN");
  expect(invitation.code).toBeTruthy();

  const memberContext = await browser.newContext(); const member = await memberContext.newPage();
  await signIn(member, identities.Member.UserName, memberPassword);
  const memberClubNumber = await member.evaluate(async () => (await (await fetch("/api/backend/api/services/app/ClubMemberProgrammeParticipation/GetMyParticipations")).json()).result.clubMemberNumber);
  await member.goto(`/i/${invitation.code}`);
  await member.getByRole("button", { name: "Confirm and continue to payment" }).click();
  await expect(member).toHaveURL(/\/member\/programmes/);
  await member.getByRole("button", { name: "Confirm test joining payment", exact: true }).click();
  await expect(member.getByText("Awaiting Area approval", { exact: true }).first()).toBeVisible();
  const areaContext = await browser.newContext(); const area = await areaContext.newPage();
  await signIn(area, "admin", administratorPassword);
  await area.goto("/admin/programme-participations");
  await area.getByRole("button", { name: "Awaiting approval", exact: true }).click();
  await area.getByRole("row").filter({ hasText: memberClubNumber }).getByRole("button", { name: "Approve", exact: true }).click();
  await expect(area.getByText(/browser-member.*approved and activated/)).toBeVisible();
  await member.reload();
  // Existing authentication invalidates the old Guest session after promotion.
  // Preserve that boundary and exercise the prompted sign-in with new access.
  await expect(member).toHaveURL(/\/login\?reason=session-ended/);
  await expect(member.getByText(/Your secure session ended because your access changed or expired/)).toBeVisible();
  await signIn(member, identities.Member.UserName, memberPassword);
  await member.goto("/member/programmes");
  await expect(member.getByText("Active", { exact: true }).first()).toBeVisible();
  await member.goto("/member/programme-progress");
  await expect(member.getByText("Structural progress:", { exact: false })).toBeVisible();
  await expect(member.getByRole("heading", { name: "Weekly test-money ledger" })).toBeVisible();
  await expect(member.getByText(/could not be loaded|unavailable/i)).toHaveCount(0);

  await area.goto("/admin/weekly-earnings");
  await expect(area.getByRole("button", { name: /Release|Record payment/ })).toHaveCount(0);
  await area.goto("/admin/loans");
  await expect(area.getByText(/capability is closed in the isolated pilot/)).toBeVisible();
  await Promise.all([hostContext.close(), activeContext.close(), memberContext.close(), areaContext.close()]);
});

test("direct API denies excluded operations even for the host administrator", async ({ page }) => {
  await signIn(page, "admin", process.env.PILOT_ADMIN_PASSWORD!, true);
  const exactId = "a6877c8a-d98d-40e5-bff9-c99c00f2b67b"; // Synthetic, unpersisted negative subject.
  const operations = [
    { endpoint: "AdminCommission/Release", body: { id: exactId, programme: 0, justification: "TEST-ONLY negative release" } },
    { endpoint: "AdminCommission/RecordPayment", body: { id: exactId, programme: 0, justification: "TEST-ONLY negative payout", paymentReference: "TEST-ONLY-no-payment" } },
    { endpoint: "AdminProgrammeParticipation/GraduateAQGreenToOnyx", body: { loanAgreementId: exactId, justification: "TEST-ONLY negative graduation" } },
    { endpoint: "ClubMemberProgrammeParticipation/CreateDirectOnyxCheckout", body: {} },
  ];
  for (const operation of operations) {
    // ABP may translate an authorization exception to 401; prove the caller's
    // granted scope before each call and require the explicit commissioning denial.
    const permitted = await page.evaluate(async () => (await fetch("/api/backend/api/services/app/AQGreenPilot/GetProspectiveCustomers?tenantId=1")).status);
    expect(permitted).toBe(200);
    const response = await page.evaluate(async ({ endpoint, body }) => {
      const response = await fetch(`/api/backend/api/services/app/${endpoint}`, {
        method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(body),
      });
      return { status: response.status, payload: await response.json() };
    }, operation);
    expect([401, 403]).toContain(response.status);
    if (operation.endpoint === "ClubMemberProgrammeParticipation/CreateDirectOnyxCheckout") {
      // The host has no member Join permission. The direct-service pilot suite
      // separately proves commissioning denial for an otherwise permitted member.
      expect(response.payload.error.message).toContain("Required permissions are not granted");
    } else {
      expect(response.payload.error.message).toContain("closed in the isolated TEST-MONEY AQGreen pilot");
    }
    await page.evaluate(async () => { await fetch("/api/auth/logout", { method: "POST" }); });
    await signIn(page, "admin", process.env.PILOT_ADMIN_PASSWORD!, true);
  }
});
