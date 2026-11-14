import { expect, test, type Page } from "@playwright/test";
import { createJourneyResponse, createProgrammeJourney } from "../src/components/members/programme-journey-test-data";

// Local browser/API contracts only. No production customer, provider or payment.
const api = "**/api/backend/api/services/app/";
const inviteCode = "AQ7G2X9KLMNP";
const preview = {
  inviteCode, programmeKey: "AQGREEN", programmeName: "AQGreen",
  tenancyName: "Default", recruiterName: "Local test recruiter",
  recruiterClubMemberNumber: "LOCAL-RECRUITER", areaName: "Local test Area",
  recruiterEligible: true,
};
const awaiting = {
  programmeName: "AQGreen", currency: "ZAR", status: "Awaiting joining payment",
  statusCode: "AwaitingJoiningPayment", joinedIndependently: true,
  joiningSchedule: null as 0 | 1 | null, joiningPaidAmount: 0, nextPaymentAmount: 1200,
  canRecruitForThisProgramme: false, isActive: false,
};

const protectLocalBrowser = async (page: Page) => {
  await page.route("https://**/*", route => {
    if (new URL(route.request().url()).hostname === "payments.example.test")
      return route.fulfill({ contentType: "text/html", body: "Local checkout boundary; no provider request was sent." });
    return route.abort("blockedbyclient");
  });
};
const signIn = async (page: Page, redirect: string) => {
  await page.goto(`/login?area=Default&redirect=${encodeURIComponent(redirect)}`);
  await page.getByLabel("Username or email").fill("member@example.test");
  await page.getByLabel("Password", { exact: true }).fill("test-password");
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page).toHaveURL(redirect);
};

const programmeReads = async (page: Page, entry: typeof awaiting | null, preparing = () => false) => {
  await page.route(`${api}ClubMemberProgrammeParticipation/GetMyParticipations`, route => route.fulfill({ json: {
    areaId: "local-area", areaName: "Local test Area", clubMemberNumber: "LOCAL-MEMBER",
    entry: preparing() && entry ? { ...entry, joiningSchedule: 0 } : entry,
    onyx: null, funeralCover: null, travelBenefit: null, pendingDirectOnyxCheckout: null,
    // The currently deployed member read hides PreparingCheckout without a URL.
    // The browser must retain its uncertainty rather than interpreting this as none.
    pendingAQGreenCheckout: null,
  } }));
  await page.route(`${api}ClubMemberProgrammeProgress/GetMyJourney`, route => route.fulfill({ json: createJourneyResponse(createProgrammeJourney("AQGREEN", entry ? {
    hasParticipation: true, participationStatus: entry.status,
    nextActionCode: "CompleteJoiningPayment", nextActionTitle: "Complete AQGreen joining",
  } : {})) }));
};

test.beforeEach(async ({ page }) => { await protectLocalBrowser(page); });

test("unapproved founder gets genuine next-step guidance without an admission or checkout request", async ({ page }) => {
  await programmeReads(page, null);
  const joiningRequests: string[] = [];
  page.on("request", request => {
    if (/\/(StartEntry|CreateAQGreenJoiningCheckout)$/.test(new URL(request.url()).pathname)) joiningRequests.push(request.url());
  });
  await signIn(page, "/member/programmes");
  const aqGreen = page.getByRole("article", { name: "AQGreen", exact: true });
  await expect(aqGreen.getByText("Before you join AQGreen")).toBeVisible();
  await expect(aqGreen.getByText(/authorised Host administrator must confirm your founder eligibility/)).toBeVisible();
  await expect(aqGreen.getByRole("link", { name: "Contact the club team" })).toHaveAttribute("href", "/contact");
  await expect(aqGreen.getByRole("button", { name: /Join AQGreen|Pay R1,200|request founder approval/i })).toHaveCount(0);
  expect(joiningRequests).toEqual([]);
});

for (const [schedule, button] of [[0, "Pay R1,200 once"], [1, "Pay first R600 instalment"]] as const) {
  test(`authorised root uses its existing participation for schedule ${schedule}, without StartEntry`, async ({ page }) => {
    await programmeReads(page, awaiting);
    const calls: { path: string; body: unknown }[] = [];
    await page.route(`${api}ClubMemberProgrammeParticipation/StartEntry`, route => { calls.push({ path: "StartEntry", body: route.request().postDataJSON() }); return route.fulfill({ status: 500, json: {} }); });
    await page.route(`${api}ClubMemberProgrammeParticipation/CreateAQGreenJoiningCheckout`, route => {
      calls.push({ path: "checkout", body: route.request().postDataJSON() });
      return route.fulfill({ json: { checkoutId: "local-only", checkoutUrl: "https://payments.example.test/checkout/root", currency: "ZAR", amount: schedule === 0 ? 1200 : 600 } });
    });
    await signIn(page, "/member/programmes");
    await page.getByRole("button", { name: button, exact: true }).click();
    await expect(page).toHaveURL("https://payments.example.test/checkout/root");
    expect(calls).toEqual([{ path: "checkout", body: { schedule } }]);
  });
}

test("a valid invitation context survives sign-in and admission precedes the intended joining checkout", async ({ page }) => {
  await page.route(`${api}ProgrammeInvitation/GetPreview?**`, route => route.fulfill({ json: preview }));
  await page.goto(`/i/${inviteCode}`);
  await expect(page.getByRole("link", { name: "Create my account" })).toHaveAttribute("href", `/signup?area=Default&invite=${inviteCode}&redirect=%2Fi%2F${inviteCode}`);
  await page.getByRole("link", { name: "Sign in to continue" }).click();
  await page.getByLabel("Username or email").fill("member@example.test");
  await page.getByLabel("Password", { exact: true }).fill("test-password");
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page).toHaveURL(`/i/${inviteCode}`);
  const calls: { path: string; body: unknown }[] = [];
  await page.route(`${api}ClubMemberProgrammeParticipation/StartEntry`, route => { calls.push({ path: "StartEntry", body: route.request().postDataJSON() }); return route.fulfill({ json: { joiningPaidAmount: 0, joiningSchedule: null } }); });
  await page.route(`${api}ClubMemberProgrammeParticipation/CreateAQGreenJoiningCheckout`, route => {
    calls.push({ path: "checkout", body: route.request().postDataJSON() });
    return route.fulfill({ json: { checkoutId: "local-only", checkoutUrl: "https://payments.example.test/checkout/invited", amount: 600, currency: "ZAR" } });
  });
  await page.getByRole("radio", { name: "Pay two R600 instalments" }).check();
  await page.getByRole("button", { name: "Confirm and continue to payment" }).click();
  await expect(page).toHaveURL("https://payments.example.test/checkout/invited");
  expect(calls).toEqual([{ path: "StartEntry", body: { inviteCode } }, { path: "checkout", body: { schedule: 1 } }]);
});

test("invalid invitation is understandable and never reaches admission or checkout", async ({ page }) => {
  await page.route(`${api}ProgrammeInvitation/GetPreview?**`, route => route.fulfill({ status: 400, json: { error: { message: "The invitation could not be accepted.", details: "The invitation code was not found." } } }));
  const posts: string[] = [];
  page.on("request", request => { if (request.method() === "POST") posts.push(request.url()); });
  await page.goto(`/i/${inviteCode}`);
  const invitationError = page.locator('section[role="alert"]').filter({ hasText: "Invitation unavailable" });
  await expect(invitationError).toHaveCount(1);
  await expect(invitationError).toContainText("ask them for a valid invitation");
  await expect(page.getByRole("button", { name: /confirm and continue/i })).toHaveCount(0);
  await expect(page.getByText(/field InviteCode|D10_|recruiter-only/)).toHaveCount(0);
  expect(posts).toEqual([]);
});

test("uncertain checkout remains pending across a real browser refresh without a duplicate request", async ({ page }) => {
  let preparing = false, checkoutCalls = 0;
  await programmeReads(page, awaiting, () => preparing);
  await page.route(`${api}ClubMemberProgrammeParticipation/CreateAQGreenJoiningCheckout`, route => {
    preparing = true; checkoutCalls++;
    return route.fulfill({ status: 503, json: { error: { message: "An internal error occurred during your request!" } } });
  });
  await signIn(page, "/member/programmes");
  await page.getByRole("button", { name: "Pay R1,200 once" }).click();
  const paymentError = page.locator('section[role="alert"]').filter({ hasText: "Payment status not confirmed" });
  await expect(paymentError).toHaveCount(1);
  await expect(paymentError).toContainText("Do not start a second payment");
  await page.getByRole("button", { name: "Refresh payment status" }).click();
  await expect(page.getByRole("button", { name: "Refresh payment status" })).toBeVisible();
  await expect(page.getByRole("button", { name: /Pay R1,200|Pay first R600/ })).toHaveCount(0);
  await expect(page.getByRole("link", { name: "Continue secure payment" })).toHaveCount(0);
  await expect(page.getByText(/No payment has been taken/i)).toHaveCount(0);
  expect(checkoutCalls).toBe(1);
});
