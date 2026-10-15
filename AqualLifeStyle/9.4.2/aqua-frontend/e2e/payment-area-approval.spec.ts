import { readFileSync, writeFileSync } from "node:fs";
import { expect, test, type Browser, type Page } from "@playwright/test";

test.skip(!process.env.PAYMENT_APPROVAL_FIXTURE_OUTPUT, "Requires the isolated PostgreSQL web-test qualification host.");
type Fixture = {
  testOnly: boolean; tenantId: number; tenant: string; password: string;
  areaId: string; areaName: string; administrator: string; otherAreaAdministrator: string; hostObserver: string;
  member: { userName: string; name: string; participationId: string; amount: number; currency: string };
  rejectedMember: { userName: string; name: string; participationId: string };
};
let passed = false;
const evidence: Record<string, boolean | number | string> = {};
test.afterAll(() => {
  if (process.env.PAYMENT_APPROVAL_FIXTURE_OUTPUT)
    writeFileSync(process.env.PAYMENT_APPROVAL_FIXTURE_OUTPUT + ".browser-complete", JSON.stringify({ passed, evidence }), { mode: 0o600 });
});

async function signIn(browser: Browser, fixture: Fixture, user: string, host = false) {
  const context = await browser.newContext();
  // No external checkout, analytics, provider or production requests from the browser.
  await context.route("**/*", async (route) => {
    const url = new URL(route.request().url());
    if (!["127.0.0.1", "localhost"].includes(url.hostname)) return route.abort("blockedbyclient");
    return route.continue();
  });
  const page = await context.newPage();
  await page.goto("/login");
  await page.getByLabel("Username or email").fill(user);
  await page.getByLabel("Password", { exact: true }).fill(fixture.password);
  await page.getByLabel("Workspace").selectOption({ label: host ? "Platform administration" : "Area workspace" });
  await expect(page.getByLabel("Workspace")).toHaveValue(host ? "" : fixture.tenant);
  const loginResponse = page.waitForResponse((response) => new URL(response.url()).pathname === "/api/auth/login");
  await page.getByRole("button", { name: "Sign in", exact: true }).click();
  const login = await loginResponse;
  expect(login.request().postDataJSON().tenant).toBe(host ? null : fixture.tenant);
  if (!login.ok()) throw new Error(`Isolated login failed (${login.status()}): ${JSON.stringify((await login.json()).error)}`);
  await expect(page).not.toHaveURL(/\/login/);
  return { context, page };
}

async function decision(page: Page, endpoint: string, participationId: string) {
  return page.evaluate(async ({ endpoint, participationId }) => {
    const response = await fetch(`/api/backend/api/services/app/AdminProgrammeParticipation/${endpoint}`, {
      method: "POST", headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ programme: 1, participationId, reason: "TEST ONLY conflicting decision" }),
    });
    return { status: response.status, body: await response.json() };
  }, { endpoint, participationId });
}

async function memberState(page: Page) {
  // The existing MVC web-test host has no Web.Host health route. Exercise the
  // authenticated member service directly; admin UI is exercised below.
  return page.evaluate(async () => {
    const response = await fetch("/api/backend/api/services/app/ClubMemberProgrammeParticipation/GetMyParticipations");
    const body = await response.json();
    return { status: response.status, value: body.result };
  });
}

test("original portal shows scoped paid queue, email-linked details, badge updates and audited approval/rejection", async ({ browser, baseURL }) => {
  expect(baseURL).toBe("http://127.0.0.1:3310");
  const fixture = JSON.parse(readFileSync(process.env.PAYMENT_APPROVAL_FIXTURE_OUTPUT!, "utf8")) as Fixture;
  expect(fixture.testOnly).toBe(true);
  expect(fixture.tenantId).toBe(1);
  const member = await signIn(browser, fixture, fixture.member.userName);
  const pending = await memberState(member.page);
  expect(pending.status).toBe(200);
  expect(pending.value.onyx.statusCode).toBe("PaymentConfirmedAwaitingApproval");
  evidence.memberPendingState = true;
  await member.context.close();

  const other = await signIn(browser, fixture, fixture.otherAreaAdministrator);
  await other.page.goto("/admin/programme-participations");
  await other.page.getByRole("tab", { name: "Onyx", exact: true }).click();
  await expect(other.page.getByRole("row").filter({ hasText: fixture.member.name })).toHaveCount(0);
  const denied = await decision(other.page, "ApproveProgrammeParticipation", fixture.member.participationId);
  expect([401, 403]).toContain(denied.status);
  expect(denied.body.error.message).toMatch(/Area|participation|Tenant/i);
  const foreignTenant = await other.page.evaluate(async () => {
    const response = await fetch("/api/backend/api/services/app/AdminProgrammeParticipation/GetAll?Programme=1&TenantId=2");
    return { status: response.status, body: await response.json() };
  });
  expect([401, 403]).toContain(foreignTenant.status);
  evidence.crossAreaAndTenantDenied = true;
  await other.context.close();

  const host = await signIn(browser, fixture, fixture.hostObserver, true);
  await host.page.goto("/admin/programme-participations");
  await host.page.getByRole("tab", { name: "Onyx", exact: true }).click();
  await host.page.getByRole("button", { name: "Awaiting approval", exact: true }).click();
  await expect(host.page.getByRole("row").filter({ hasText: fixture.member.name })).toBeVisible();
  await expect(host.page.getByRole("button", { name: "Approve", exact: true })).toHaveCount(0);
  await expect(host.page.getByText(/You have read-only access/)).toBeVisible();
  const hostDenied = await decision(host.page, "ApproveProgrammeParticipation", fixture.member.participationId);
  expect([401, 403]).toContain(hostDenied.status);
  evidence.hostVisibilityIsNotApprovalAuthority = true;
  await host.context.close();

  const admin = await signIn(browser, fixture, fixture.administrator);
  await admin.page.goto("/admin/dashboard");
  await expect(admin.page.getByRole("heading", { name: "Administration overview" })).toBeVisible();
  const navigation = admin.page.getByRole("navigation", { name: "Administration navigation" });
  await expect(navigation.getByLabel("2 approvals awaiting review", { exact: true })).toBeVisible();
  await navigation.getByRole("link", { name: /Programme participation/ }).click();
  await admin.page.getByRole("tab", { name: "Onyx", exact: true }).click();
  await admin.page.getByRole("button", { name: "Awaiting approval", exact: true }).click();
  const first = admin.page.getByRole("row").filter({ hasText: fixture.member.name });
  await expect(first).toContainText(fixture.areaName);
  await expect(first).toContainText("Awaiting Area approval");
  await expect(first).toContainText("ISOLATED-QUALIFICATION");
  await expect(first).toContainText(new Intl.NumberFormat("en-ZA", {
    style: "currency", currency: fixture.member.currency,
  }).format(fixture.member.amount));
  await expect(first).toContainText("Reference: TEST-approve-");
  await expect(first).toContainText("Confirmed:");
  evidence.dashboardSidebarCount = 2;
  evidence.memberAreaProgrammePaymentDetails = true;
  await first.getByRole("button", { name: "Approve", exact: true }).click();
  await expect(admin.page.getByText(/TEST Approval Member.*approved and activated/)).toBeVisible();
  await expect(navigation.getByLabel("1 approvals awaiting review", { exact: true })).toBeVisible();
  expect((await decision(admin.page, "ApproveProgrammeParticipation", fixture.member.participationId)).status).toBe(200);
  const conflict = await decision(admin.page, "RejectProgrammeParticipation", fixture.member.participationId);
  expect(conflict.status).toBeGreaterThanOrEqual(400);
  expect(conflict.body.success).toBe(false);
  expect(JSON.stringify(conflict.body.error)).toMatch(/already|active|decision/i);
  evidence.approvalRetryAndConflict = true;

  const second = admin.page.getByRole("row").filter({ hasText: fixture.rejectedMember.name });
  await second.getByRole("button", { name: "Reject", exact: true }).click();
  await admin.page.getByLabel("Reason for declining").fill("TEST ONLY evidence rejection");
  await admin.page.getByRole("button", { name: "Decline participation", exact: true }).click();
  await expect(admin.page.getByText(/TEST Rejection Member.*declined/)).toBeVisible();
  await expect(navigation.getByLabel(/approvals awaiting review/)).toHaveCount(0);
  evidence.rejectionAndBadgeCleared = true;
  await admin.context.close();

  const approved = await signIn(browser, fixture, fixture.member.userName);
  expect((await memberState(approved.page)).value.onyx.statusCode).toBe("Active");
  await approved.context.close();
  const rejected = await signIn(browser, fixture, fixture.rejectedMember.userName);
  expect((await memberState(rejected.page)).value.onyx.statusCode).toBe("Rejected");
  await rejected.context.close();
  evidence.memberFinalStates = true;
  passed = true;
});
