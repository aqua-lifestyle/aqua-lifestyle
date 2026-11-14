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

const partPaid = {
  programmeName: "AQGreen", currency: "ZAR", status: "Awaiting second joining instalment",
  statusCode: "AwaitingActivationPayment", joinedIndependently: false,
  joiningSchedule: 1 as const, joiningPaidAmount: 600, nextPaymentAmount: 600,
  canRecruitForThisProgramme: false, isActive: false,
};

const invitationReplay = async (
  page: Page,
  record: Record<string, unknown>,
  checkoutCalls: { path: string; body: unknown }[] = [],
  checkoutUrl = "https://payments.example.test/checkout/second",
) => {
  await page.route(`${api}ProgrammeInvitation/GetPreview?**`, route => route.fulfill({ json: preview }));
  await page.route(`${api}ClubMemberProgrammeParticipation/StartEntry`, route => {
    checkoutCalls.push({ path: "StartEntry", body: route.request().postDataJSON() });
    return route.fulfill({ json: record });
  });
  await page.route(`${api}ClubMemberProgrammeParticipation/CreateAQGreenJoiningCheckout`, route => {
    checkoutCalls.push({ path: "checkout", body: route.request().postDataJSON() });
    return route.fulfill({ json: { checkoutId: "local-only", checkoutUrl, amount: 600, currency: "ZAR" } });
  });
  await signIn(page, `/i/${inviteCode}`);
};

test("a replayed invitation honors the recorded plan and offers the legitimate second R600 payment", async ({ page }) => {
  const calls: { path: string; body: unknown }[] = [];
  await invitationReplay(page, partPaid, calls);
  await page.getByRole("button", { name: "Confirm and continue to payment" }).click();
  expect(calls).toEqual([{ path: "StartEntry", body: { inviteCode } }]);
  await expect(page.getByRole("radio", { name: "Pay R1,200 once" })).toHaveCount(0);
  const second = page.getByRole("button", { name: /Complete joining:.*600/ });
  await expect(second).toBeVisible();
  await second.click();
  await expect(page).toHaveURL("https://payments.example.test/checkout/second");
  expect(calls).toEqual([
    { path: "StartEntry", body: { inviteCode } },
    { path: "checkout", body: { schedule: 1 } },
  ]);
  await page.goBack();
  await expect(page).toHaveURL(`/i/${inviteCode}`);
  expect(await page.evaluate(() => window.localStorage.getItem("aqua.aqgreenCheckoutReview.7.42"))).toBe("part-paid");
});

test("a replayed invitation with a recorded-but-unpaid plan continues that plan without restarting selection", async ({ page }) => {
  const calls: { path: string; body: unknown }[] = [];
  await invitationReplay(page, { ...partPaid, statusCode: "AwaitingJoiningPayment", status: "Awaiting joining payment", joiningPaidAmount: 0 }, calls);
  await page.getByRole("button", { name: "Confirm and continue to payment" }).click();
  expect(calls).toEqual([{ path: "StartEntry", body: { inviteCode } }]);
  await expect(page.getByRole("radio", { name: "Pay R1,200 once" })).toHaveCount(0);
  const first = page.getByRole("button", { name: /Complete joining:.*600/ });
  await expect(first).toBeVisible();
  await first.click();
  await expect(page).toHaveURL("https://payments.example.test/checkout/second");
  expect(calls).toEqual([
    { path: "StartEntry", body: { inviteCode } },
    { path: "checkout", body: { schedule: 1 } },
  ]);
  await page.goBack();
  await expect(page).toHaveURL(`/i/${inviteCode}`);
  expect(await page.evaluate(() => window.localStorage.getItem("aqua.aqgreenCheckoutReview.7.42"))).toBe("unpaid");
});

test("a completed joining replay never reaches another checkout", async ({ page }) => {
  const calls: { path: string; body: unknown }[] = [];
  await invitationReplay(page, {
    ...partPaid, statusCode: "PaymentConfirmedAwaitingApproval",
    status: "Awaiting Area Administrator approval", joiningPaidAmount: 1200, nextPaymentAmount: null,
  }, calls);
  await page.getByRole("button", { name: "Confirm and continue to payment" }).click();
  await expect(page.getByText("Check your joining status")).toBeVisible();
  await expect(page.getByRole("button", { name: /Complete joining|Confirm and continue/ })).toHaveCount(0);
  expect(calls).toEqual([{ path: "StartEntry", body: { inviteCode } }]);
});

test("a settled first-stage reminder is retired on return through the invitation", async ({ page }) => {
  await page.addInitScript(() => window.localStorage.setItem("aqua.aqgreenCheckoutReview.7.42", "unpaid"));
  const calls: { path: string; body: unknown }[] = [];
  await invitationReplay(page, partPaid, calls);
  await page.getByRole("button", { name: "Confirm and continue to payment" }).click();
  const second = page.getByRole("button", { name: /Complete joining:.*600/ });
  await expect(second).toBeVisible();
  expect(await page.evaluate(() => window.localStorage.getItem("aqua.aqgreenCheckoutReview.7.42"))).toBeNull();
  await second.click();
  await expect(page).toHaveURL("https://payments.example.test/checkout/second");
  expect(calls).toEqual([
    { path: "StartEntry", body: { inviteCode } },
    { path: "checkout", body: { schedule: 1 } },
  ]);
});

test("a confirmed first instalment clears a settled reminder and allows the second R600 payment after re-sign-in", async ({ page }) => {
  await page.goto("/login");
  await page.evaluate(() => window.localStorage.setItem("aqua.aqgreenCheckoutReview.7.42", "unpaid"));
  await programmeReads(page, partPaid);
  const calls: { path: string; body: unknown }[] = [];
  await page.route(`${api}ClubMemberProgrammeParticipation/CreateAQGreenJoiningCheckout`, route => {
    calls.push({ path: "checkout", body: route.request().postDataJSON() });
    return route.fulfill({ json: { checkoutId: "local-only", checkoutUrl: "https://payments.example.test/checkout/second", amount: 600, currency: "ZAR" } });
  });
  await signIn(page, "/member/programmes");
  const pay = page.getByRole("button", { name: /Complete joining:.*600/ });
  await expect(pay).toBeVisible();
  await pay.click();
  await expect(page).toHaveURL("https://payments.example.test/checkout/second");
  expect(calls).toEqual([{ path: "checkout", body: { schedule: 1 } }]);
  await page.goBack();
  await expect(page).toHaveURL("/member/programmes");
  expect(await page.evaluate(() => window.localStorage.getItem("aqua.aqgreenCheckoutReview.7.42"))).toBe("part-paid");
});

test("an unresolved second-stage reminder survives re-sign-in without a duplicate request", async ({ page }) => {
  await page.goto("/login");
  await page.evaluate(() => window.localStorage.setItem("aqua.aqgreenCheckoutReview.7.42", "part-paid"));
  await programmeReads(page, partPaid);
  const checkoutRequests: string[] = [];
  page.on("request", request => {
    if (/CreateAQGreenJoiningCheckout$/.test(new URL(request.url()).pathname)) checkoutRequests.push(request.url());
  });
  await signIn(page, "/member/programmes");
  await expect(page.getByRole("button", { name: "Refresh payment status" })).toBeVisible();
  await expect(page.getByRole("button", { name: /Complete joining|Pay R1,200|Pay first R600/ })).toHaveCount(0);
  await expect(page.getByRole("link", { name: "Continue secure payment" })).toHaveCount(0);
  await page.context().clearCookies();
  await signIn(page, "/member/programmes");
  await expect(page.getByRole("button", { name: "Refresh payment status" })).toBeVisible();
  await expect(page.getByRole("button", { name: /Complete joining|Pay R1,200|Pay first R600/ })).toHaveCount(0);
  expect(await page.evaluate(() => window.localStorage.getItem("aqua.aqgreenCheckoutReview.7.42"))).toBe("part-paid");
  expect(checkoutRequests).toEqual([]);
});
