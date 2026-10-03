import { expect, test, type Page } from "@playwright/test";
import { longName, uiFixtures } from "./admin-ui-fixtures.mjs";
import { auditTextContrast, auditThemePairs } from "./admin-ui-contrast";

const routes = ["dashboard", "customers", "users", "access-levels", "tenants", "area-leaders", "facilitators", "members", "programme-participations", "weekly-sales-reviews", "weekly-earnings", "savings", "loans", "entry-commitments"];
// Retain the recovered matrix and exercise both sides of the existing Tailwind
// sm/md/lg/xl transitions, plus the owner's intermediate phone/tablet cases.
const widths = [320, 360, 375, 390, 430, 480, 600, 639, 640, 641, 767, 768, 769, 820, 900, 1023, 1024, 1025, 1279, 1280, 1281, 1366, 1440];
const screenshotWidths = [320, 375, 480, 600, 768, 1024, 1280, 1440];
const orientationViewports = [
  { width: 320, height: 600 }, { width: 390, height: 844 },
  { width: 600, height: 900 }, { width: 768, height: 1024 },
  { width: 820, height: 1180 }, { width: 900, height: 600 },
  { width: 1024, height: 768 }, { width: 1024, height: 600 },
];

type BrowserDiagnostics = {
  expectedHttpFailures: Set<string>;
  messages: { type: string; text: string; source: string; expected: boolean }[];
};
const browserDiagnostics = new WeakMap<Page, BrowserDiagnostics>();

test.beforeEach(async ({ page }) => {
  const diagnostics: BrowserDiagnostics = { expectedHttpFailures: new Set(), messages: [] };
  browserDiagnostics.set(page, diagnostics);
  page.on("pageerror", error => diagnostics.messages.push({ type: "pageerror", text: error.message, source: "", expected: false }));
  page.on("console", message => {
    if (message.type() !== "error" && message.type() !== "warning") return;
    const source = message.location().url;
    const expected = message.type() === "error" &&
      /^Failed to load resource: the server responded with a status of 503\b/.test(message.text()) &&
      diagnostics.expectedHttpFailures.has(source);
    diagnostics.messages.push({ type: message.type(), text: message.text(), source, expected });
  });
});

test.afterEach(async ({ page }, info) => {
  const messages = browserDiagnostics.get(page)?.messages ?? [];
  await info.attach("browser-diagnostics.json", { body: JSON.stringify(messages, null, 2), contentType: "application/json" });
  // Only exact, deliberately injected 503 fixture failures are expected. React,
  // hydration, unknown resource failures and chart/layout warnings must fail.
  expect(messages.filter(message => !message.expected)).toEqual([]);
});

type FixtureState = "many" | "one" | "empty" | "error" | "optional-error" | "plan-error" | "unsupported-role" | "loading" | "released" | "recovery" | "long-area";
const mockUI = async (page: Page, state: () => FixtureState = () => "many") => {
  await page.route("**/api/backend/**", async route => {
    const fixtureState = state();
    const endpoint = new URL(route.request().url()).pathname.replace("/api/backend", "");
    // This mock affects presentation only; writes are explicitly rejected.
    if (route.request().method() !== "GET") {
      browserDiagnostics.get(page)?.expectedHttpFailures.add(route.request().url());
      return route.fulfill({ status: 503, json: { error: { message: "Synthetic UI write failure. No changes saved." } } });
    }
    if (!(endpoint in uiFixtures)) return route.continue();
    if (fixtureState === "loading") await new Promise(resolve => setTimeout(resolve, 750));
    const optionalFailure = fixtureState === "optional-error" && ["/api/services/app/AreaLeader/GetAll", "/api/services/app/Facilitator/GetAll", "/api/services/app/Referral/GetAll"].includes(endpoint);
    const planFailure = fixtureState === "plan-error" && endpoint === "/api/services/app/AdminCustomer/GetMembershipOptions";
    if (fixtureState === "error" || optionalFailure || planFailure) {
      browserDiagnostics.get(page)?.expectedHttpFailures.add(route.request().url());
      return route.fulfill({ status: 503, json: { error: { message: `Synthetic UI load failure. Reference: ${"test-reference".repeat(12)}` } } });
    }
    let body = uiFixtures[endpoint];
    if (fixtureState === "released" && endpoint === "/api/services/app/AdminCommission/GetAll") {
      const records = body as { items: Record<string, unknown>[]; totalCount: number };
      body = { ...records, items: records.items.map((row, index) => index === 0 ? { ...row, status: "Released — awaiting payment", releasedAt: "2026-10-01T08:00:00.000Z", releaseReason: "Synthetic display fixture" } : row) };
    }
    if (fixtureState === "long-area" && endpoint === "/api/services/app/AdminTenant/GetAll") {
      const records = body as { items: Record<string, unknown>[]; totalCount: number };
      body = { ...records, items: records.items.map(row => ({ ...row, name: longName, tenancyName: "synthetic-area".repeat(10) })) };
    }
    if (fixtureState === "recovery" && endpoint === "/api/services/app/AdminProgrammeParticipation/GetAQGreenJoiningCheckouts") {
      body = { totalCount: 1, items: [{
        amount: 600, areaName: longName, checkoutCreatedAt: "2026-10-01T08:00:00.000Z", checkoutId: "synthetic-checkout",
        clubMemberNumber: "SYNTHETIC-".repeat(12), createdAt: "2026-10-01T08:00:00.000Z", currency: "ZAR", customerName: longName,
        lockReason: "Synthetic locked-checkout explanation ".repeat(12), paymentId: "synthetic-payment",
        providerCheckoutId: "synthetic-provider-reference".repeat(12), schedule: 1, stage: 1, status: 1, tenantId: 7,
      }] };
    }
    if (fixtureState === "unsupported-role" && endpoint === "/api/services/app/AdminUser/GetAll") {
      const records = body as { items: Record<string, unknown>[]; totalCount: number };
      body = { ...records, items: records.items.map((row, index) => index === 0 ? { ...row, role: 99 } : row) };
    }
    if (fixtureState === "empty" || fixtureState === "one") {
      const limit = fixtureState === "empty" ? 0 : 1;
      if (Array.isArray(body)) body = body.slice(0, limit);
      else if (body && typeof body === "object" && "items" in body) {
        const items = (body as { items: unknown[] }).items.slice(0, limit);
        body = { ...body, items, totalCount: items.length };
      }
    }
    await route.fulfill({ json: body });
  });
};

const signIn = async (page: Page, email = "admin-ui@example.test", dismissNotification = true) => {
  await page.goto("/login");
  await page.getByLabel("Username or email").fill(email);
  await page.getByLabel("Password", { exact: true }).fill("test-password");
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page).toHaveURL("/admin/dashboard");
  const dismiss = page.getByRole("button", { name: "Dismiss notification" });
  if (dismissNotification && await dismiss.count()) await dismiss.click();
};

const overflow = (page: Page) => page.evaluate(() => ({
  viewport: document.documentElement.clientWidth,
  document: document.documentElement.scrollWidth,
  body: document.body.scrollWidth,
}));

const setAuditViewport = async (page: Page, viewport: { width: number; height: number }) => {
  await page.setViewportSize(viewport);
  // Mobile emulation can expose intermediate layout metrics while restoring a
  // full-page screenshot's viewport. Sample the rendered viewport, not that
  // unpainted intermediate state; all containment/action assertions still apply.
  await page.evaluate(() => new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve))));
};

const auditActionLayout = (page: Page) => page.evaluate(() => {
  const issues: string[] = [];
  const buttons = Array.from(document.querySelectorAll<HTMLButtonElement>("main button, aside button, aside a"))
    .filter(element => element.getClientRects().length && !element.closest("dialog:not([open])"));
  for (const button of buttons) {
    const bounds = button.getBoundingClientRect();
    const label = button.textContent?.trim() || button.getAttribute("aria-label") || "Unnamed action";
    if (bounds.width < 24 || bounds.height < 24) issues.push(`Small target: ${label} (${bounds.width}x${bounds.height})`);
    const walker = document.createTreeWalker(button, NodeFilter.SHOW_TEXT);
    while (walker.nextNode()) {
      if (!walker.currentNode.textContent?.trim() || walker.currentNode.parentElement?.closest(".sr-only")) continue;
      const range = document.createRange();
      range.selectNodeContents(walker.currentNode);
      if (Array.from(range.getClientRects()).some(rect => rect.left < bounds.left - 1 || rect.right > bounds.right + 1 || rect.top < bounds.top - 1 || rect.bottom > bounds.bottom + 1)) {
        issues.push(`Action text outside control: ${label}`);
      }
    }
    for (const sibling of buttons) {
      if (sibling === button || sibling.parentElement !== button.parentElement) continue;
      const other = sibling.getBoundingClientRect();
      if (Math.min(bounds.right, other.right) - Math.max(bounds.left, other.left) > 1 && Math.min(bounds.bottom, other.bottom) - Math.max(bounds.top, other.top) > 1) {
        issues.push(`Overlapping actions: ${label} / ${sibling.textContent?.trim()}`);
      }
    }
  }
  return issues;
});

const auditOrderAnalyticsLayout = (page: Page) => page.getByRole("heading", { name: "Order analytics", exact: true }).locator("..").locator("..").evaluate(card => {
  const issues: string[] = [];
  const bounds = card.getBoundingClientRect();
  for (const element of card.querySelectorAll("p")) {
    const range = document.createRange();
    range.selectNodeContents(element);
    if (Array.from(range.getClientRects()).some(rect => rect.left < bounds.left - 1 || rect.right > bounds.right + 1)) {
      issues.push(`Order analytics text clipped by card: ${element.textContent}`);
    }
  }
  return issues;
});

for (const scheme of ["light", "dark"] as const) {
  test(`admin UI route viewport matrix ${scheme}`, async ({ page }, info) => {
    test.setTimeout(480_000);
    await page.emulateMedia({ colorScheme: scheme, reducedMotion: "reduce" });
    await mockUI(page);
    await signIn(page);
    const themePairs = await auditThemePairs(page);
    await info.attach(`theme-pairs-${scheme}.json`, { body: JSON.stringify(themePairs, null, 2), contentType: "application/json" });
    expect(themePairs.failures).toEqual([]);
    const issues: string[] = [];
    const pageErrors: string[] = [];
    const colors: unknown[] = [];
    page.on("pageerror", error => pageErrors.push(error.message));
    for (const route of routes) {
      await page.goto(`/admin/${route}`);
      await expect(page.locator("main h1")).toBeVisible();
      if (route !== "dashboard") await expect(page.getByRole("table").first()).toBeVisible();
      else await expect(page.getByRole("heading", { name: "Recent orders" })).toBeVisible();
      const cellOverflow = await page.evaluate(() => {
        const issues: string[] = [];
        for (const cell of document.querySelectorAll("td")) {
          const bounds = cell.getBoundingClientRect();
          const walker = document.createTreeWalker(cell, NodeFilter.SHOW_TEXT);
          while (walker.nextNode()) {
            const node = walker.currentNode;
            if (!node.textContent?.trim() || node.parentElement?.closest("dialog")) continue;
            const range = document.createRange();
            range.selectNodeContents(node);
            if (Array.from(range.getClientRects()).some(rect => rect.right > bounds.right + 1 || rect.left < bounds.left - 1)) issues.push(node.textContent.slice(0, 30));
          }
        }
        return issues;
      });
      if (cellOverflow.length) issues.push(`${route} overlapping cells: ${JSON.stringify(cellOverflow)}`);
      for (const width of widths) {
        await setAuditViewport(page, { width, height: 900 });
        const dimensions = await overflow(page);
        if (dimensions.document > width + 1 || dimensions.body > width + 1) issues.push(`${route} ${width}: ${JSON.stringify(dimensions)}`);
        const actionIssues = await auditActionLayout(page);
        if (actionIssues.length) issues.push(`${route} ${width}: ${JSON.stringify(actionIssues)}`);
        if (route === "dashboard") {
          const analyticsIssues = await auditOrderAnalyticsLayout(page);
          if (analyticsIssues.length) issues.push(`${route} ${width}: ${JSON.stringify(analyticsIssues)}`);
        }
        if (route === "dashboard" && width >= 1280) {
          const statusLines = await page.getByRole("region", { name: "Platform overview" }).getByText("Unavailable", { exact: true }).evaluate(element => {
            const range = document.createRange();
            range.selectNodeContents(element);
            return range.getClientRects().length;
          });
          if (statusLines !== 1) issues.push(`dashboard ${width}: metric status split over ${statusLines} lines`);
        }
        if (screenshotWidths.includes(width)) {
          if (route === "dashboard") await expect(page.locator(".recharts-pie-sector path")).toHaveCount(2);
          await page.screenshot({ path: info.outputPath(`${route}-${width}-${scheme}.png`), fullPage: true });
        }
      }
      const contrast = await auditTextContrast(page);
      colors.push({ route, ...contrast });
      if (contrast.failures.length) issues.push(`${route} contrast: ${JSON.stringify(contrast.failures)}`);
      for (const viewport of orientationViewports) {
        await setAuditViewport(page, viewport);
        const dimensions = await overflow(page);
        if (dimensions.document > viewport.width + 1 || dimensions.body > viewport.width + 1) issues.push(`${route} ${viewport.width}x${viewport.height}: ${JSON.stringify(dimensions)}`);
        const actions = await auditActionLayout(page);
        if (actions.length) issues.push(`${route} ${viewport.width}x${viewport.height}: ${JSON.stringify(actions)}`);
        if (route === "dashboard") {
          const analyticsIssues = await auditOrderAnalyticsLayout(page);
          if (analyticsIssues.length) issues.push(`${route} ${viewport.width}x${viewport.height}: ${JSON.stringify(analyticsIssues)}`);
        }
        if (viewport.width === 320 && route !== "dashboard") {
          // Pan to the final column rather than assuming offscreen actions are
          // usable merely because the table does not widen the document.
          for (const region of await page.getByRole("region", { name: / table$/ }).all()) {
            await region.evaluate(element => { element.scrollLeft = element.scrollWidth; });
            const controls = region.locator("tbody tr").first().locator("td").last().getByRole("button");
            for (const control of await controls.all()) {
              await control.focus();
              await control.scrollIntoViewIfNeeded();
              const bounds = await control.boundingBox();
              const container = await region.boundingBox();
              if (bounds!.x < container!.x - 1 || bounds!.x + bounds!.width > container!.x + container!.width + 1) issues.push(`${route}: final-column action is clipped after scrolling`);
            }
            await region.evaluate(element => { element.scrollLeft = 0; });
          }
        }
      }
      if (["dashboard", "users", "weekly-earnings"].includes(route)) {
        // Text enlargement and narrow-equivalent layouts are distinct evidence
        // from literal browser zoom. Keep the application's current font choices.
        await setAuditViewport(page, { width: 1440, height: 900 });
        await page.evaluate(() => { document.documentElement.style.fontSize = "32px"; });
        await page.evaluate(() => new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve))));
        try {
          const dimensions = await overflow(page);
          if (dimensions.document > 1441 || dimensions.body > 1441) issues.push(`${route} enlarged text: ${JSON.stringify(dimensions)}`);
          const actions = await auditActionLayout(page);
          if (actions.length) issues.push(`${route} enlarged text: ${JSON.stringify(actions)}`);
          if (route === "dashboard") {
            const analyticsIssues = await auditOrderAnalyticsLayout(page);
            if (analyticsIssues.length) issues.push(`${route} enlarged text: ${JSON.stringify(analyticsIssues)}`);
            const lines = await page.getByText("Reserved value this month", { exact: true }).locator("..").locator("p").nth(1).evaluate(element => {
              const range = document.createRange();
              range.selectNodeContents(element);
              return range.getClientRects().length;
            });
            if (lines > 4) issues.push(`dashboard enlarged text: reserved value split across ${lines} lines`);
          }
          await page.screenshot({ path: info.outputPath(`${route}-enlarged-text-${scheme}.png`), fullPage: true });
        } finally {
          await page.evaluate(() => { document.documentElement.style.removeProperty("font-size"); });
        }
      }
      if (route === "weekly-sales-reviews") {
        await page.getByRole("button", { name: "Review", exact: true }).click();
        for (const width of widths) {
          await setAuditViewport(page, { width, height: 900 });
          const dimensions = await overflow(page);
          if (dimensions.document > width + 1 || dimensions.body > width + 1) issues.push(`selected review ${width}: ${JSON.stringify(dimensions)}`);
        }
        await setAuditViewport(page, { width: 390, height: 900 });
        await page.screenshot({ path: info.outputPath(`selected-review-390-${scheme}.png`), fullPage: true });
      }
    }
    await info.attach(`contrast-${scheme}.json`, { body: JSON.stringify(colors, null, 2), contentType: "application/json" });
    expect(pageErrors).toEqual([]);
    expect(issues).toEqual([]);
  });

  test(`admin UI keyboard dialogs and scrolling ${scheme}`, async ({ page }, info) => {
    test.setTimeout(180_000);
    await page.emulateMedia({ colorScheme: scheme, reducedMotion: "reduce" });
    await setAuditViewport(page, { width: 390, height: 700 });
    await mockUI(page);
    await signIn(page);
    await page.keyboard.press("Tab");
    // Check skip target separately because login leaves focus in a browser-dependent location.
    const skip = page.getByRole("link", { name: "Skip to admin content" });
    await skip.focus();
    await page.keyboard.press("Enter");
    await expect(page.locator("#main-content")).toBeFocused();
    const navigation = page.getByRole("button", { name: "Open administration navigation" });
    await navigation.focus();
    await page.keyboard.press("Enter");
    await expect(page.getByRole("navigation", { name: "Administration navigation" })).toBeVisible();
    await page.screenshot({ path: info.outputPath(`phone-navigation-${scheme}.png`) });
    await setAuditViewport(page, { width: 820, height: 900 });
    await page.screenshot({ path: info.outputPath(`tablet-navigation-${scheme}.png`) });
    await setAuditViewport(page, { width: 390, height: 700 });
    await page.getByRole("navigation", { name: "Administration navigation" }).getByRole("link", { name: "User accounts" }).click();
    const region = page.getByRole("region", { name: "User accounts table" });
    await expect(region).toBeVisible();
    await page.keyboard.press("Tab");
    await region.focus();
    const outline = await region.evaluate(element => getComputedStyle(element).outlineWidth);
    expect(parseFloat(outline)).toBeGreaterThanOrEqual(2);
    await page.keyboard.press("ArrowRight");
    await expect.poll(() => region.evaluate(element => element.scrollLeft)).toBeGreaterThan(0);
    const manage = page.getByRole("button", { name: "Manage account for Test Member", exact: true });
    await manage.focus();
    await page.keyboard.press("Enter");
    await page.getByRole("button", { name: "Remove", exact: true }).first().focus();
    await page.screenshot({ path: info.outputPath(`expanded-user-${scheme}.png`) });
    const edit = page.getByRole("button", { name: "Edit details", exact: true }).first();
    await edit.focus();
    await page.keyboard.press("Enter");
    const dialog = page.getByRole("dialog", { name: "Edit user account" });
    await expect(dialog).toBeVisible();
    for (let i = 0; i < 12; i++) {
      await page.keyboard.press("Tab");
      expect(await dialog.evaluate(element => element.contains(document.activeElement))).toBe(true);
    }
    const reason = dialog.getByLabel("Reason for change");
    await reason.fill("Synthetic unsaved form input");
    const reasonBounds = await reason.boundingBox();
    await page.mouse.move(reasonBounds!.x + 12, reasonBounds!.y + 12);
    await page.mouse.down();
    await page.mouse.move(1, 1);
    await page.mouse.up();
    await expect(dialog, "Dragging from a form onto the backdrop must not dismiss unsaved input").toBeVisible();
    await expect(reason).toHaveValue("Synthetic unsaved form input");
    await page.mouse.click(1, 1);
    await expect(dialog).not.toBeVisible();
    await expect(manage).toHaveAttribute("aria-expanded", "true");
    await expect(edit).toBeFocused();
    await page.keyboard.press("Enter");
    await expect(dialog).toBeVisible();
    await page.keyboard.press("Escape");
    await expect(dialog).not.toBeVisible();
    await expect(manage).toHaveAttribute("aria-expanded", "true");
    await expect(edit).toBeFocused();
    await page.keyboard.press("Escape");
    await expect(manage).toHaveAttribute("aria-expanded", "false");
    await expect(manage).toBeFocused();

    // Real account names and API identifiers can be long unbroken strings;
    // confirmation prose must fit the modal, not just its heading and fields.
    await page.getByRole("button", { name: /^Manage / }).nth(1).click();
    await page.getByRole("button", { name: "Remove", exact: true }).first().click();
    const removal = page.getByRole("dialog", { name: "Remove user account" });
    await expect(removal).toBeVisible();
    expect(await removal.evaluate(element => element.scrollWidth <= element.clientWidth)).toBe(true);
    await page.screenshot({ path: info.outputPath(`long-name-confirmation-${scheme}.png`) });
    await page.keyboard.press("Escape");

    await page.goto("/admin/customers");
    await page.getByRole("button", { name: "Add customer", exact: true }).click();
    const customerDialog = page.getByRole("dialog", { name: "Add customer", exact: true });
    await expect(customerDialog).toBeVisible();
    for (const width of [320, 375, 390, 768, 820, 1024, 1366, 1440]) {
      await setAuditViewport(page, { width, height: 600 });
      const submit = customerDialog.getByRole("button", { name: "Create customer", exact: true });
      await submit.scrollIntoViewIfNeeded();
      const bounds = await submit.boundingBox();
      expect(bounds!.x).toBeGreaterThanOrEqual(0);
      expect(bounds!.x + bounds!.width).toBeLessThanOrEqual(width);
      expect(bounds!.y + bounds!.height).toBeLessThanOrEqual(600);
      expect(await customerDialog.evaluate(element => element.scrollWidth <= element.clientWidth)).toBe(true);
      await page.screenshot({ path: info.outputPath(`customer-dialog-${width}-${scheme}.png`) });
    }
    await customerDialog.getByRole("button", { name: "Create customer", exact: true }).click();
    await expect(customerDialog.locator('[aria-invalid="true"]').first()).toBeVisible();
    expect((await auditTextContrast(page)).failures).toEqual([]);
    await setAuditViewport(page, { width: 320, height: 300 });
    const invalid = customerDialog.locator('[aria-invalid="true"]').first();
    await invalid.focus();
    await invalid.scrollIntoViewIfNeeded();
    const invalidBounds = await invalid.boundingBox();
    expect(invalidBounds!.y).toBeGreaterThanOrEqual(0);
    expect(invalidBounds!.y + invalidBounds!.height).toBeLessThanOrEqual(301);
    expect(await invalid.evaluate(element => element.getAttribute("aria-describedby")!.split(" ").every(id => Boolean(document.getElementById(id)?.textContent?.trim())))).toBe(true);
    await page.screenshot({ path: info.outputPath(`focused-validation-320x300-${scheme}.png`) });
    await page.keyboard.press("Escape");
    await page.goto("/admin/programme-participations");
    const aqgreen = page.getByRole("tab", { name: "AQGreen" });
    await aqgreen.focus();
    await page.keyboard.press("ArrowRight");
    await expect(page.getByRole("tab", { name: "Onyx" })).toBeFocused();
    await expect(page.getByRole("tab", { name: "Onyx" })).toHaveAttribute("aria-selected", "true");
    await page.keyboard.press("Home");
    await expect(aqgreen).toHaveAttribute("aria-selected", "true");
    await page.getByRole("button", { name: "Reject", exact: true }).first().click();
    const decline = page.getByRole("dialog", { name: "Decline programme participation" });
    await expect(decline).toBeVisible();
    expect(await decline.evaluate(element => element.scrollWidth <= element.clientWidth)).toBe(true);
    await page.screenshot({ path: info.outputPath(`long-name-programme-dialog-${scheme}.png`) });
    await page.keyboard.press("Escape");
  });

  test(`admin UI long header hover and focus states ${scheme}`, async ({ page }, info) => {
    test.setTimeout(90_000);
    await page.emulateMedia({ colorScheme: scheme, reducedMotion: "reduce" });
    await mockUI(page);
    await signIn(page, "admin-ui-long@example.test", false);
    await setAuditViewport(page, { width: 320, height: 600 });
    const notification = page.getByRole("alert").filter({ has: page.getByRole("button", { name: "Dismiss notification" }) });
    await expect(notification).toBeVisible();
    await page.evaluate(() => new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve))));
    const notificationBounds = await notification.boundingBox();
    expect(notificationBounds!.x).toBeGreaterThanOrEqual(0);
    expect(notificationBounds!.x + notificationBounds!.width).toBeLessThanOrEqual(320);
    expect(notificationBounds!.y).toBeGreaterThanOrEqual(64);
    expect(notificationBounds!.y + notificationBounds!.height).toBeLessThanOrEqual(600);
    await page.screenshot({ path: info.outputPath(`phone-toast-${scheme}.png`) });
    await notification.getByRole("button", { name: "Dismiss notification" }).click();
    for (const width of widths) {
      await setAuditViewport(page, { width, height: 800 });
      expect((await overflow(page)).document, `long header ${width}`).toBeLessThanOrEqual(width + 1);
    }
    await setAuditViewport(page, { width: 820, height: 900 });
    await page.getByRole("button", { name: "Open user menu" }).click();
    await page.screenshot({ path: info.outputPath(`long-header-user-menu-${scheme}.png`) });
    await page.keyboard.press("Escape");
    await expect(page.getByRole("button", { name: "Open user menu" })).toBeFocused();
    await page.goto("/admin/dashboard");
    const pie = page.locator(".recharts-pie-sector path").first();
    await expect(pie).toBeVisible();
    // A doughnut's bounding-box centre is its empty hole; target a painted arc
    // relative to its SVG centre (the chart's declared radii are 48 and 76).
    await pie.scrollIntoViewIfNeeded();
    const chartBounds = await page.locator(".recharts-surface").first().boundingBox();
    await page.mouse.move(chartBounds!.x + chartBounds!.width / 2 + 44, chartBounds!.y + chartBounds!.height / 2 - 44);
    await expect(page.locator(".recharts-tooltip-wrapper")).toBeVisible();
    await page.screenshot({ path: info.outputPath(`chart-tooltip-${scheme}.png`) });
    expect((await auditTextContrast(page)).failures).toEqual([]);
    await page.goto("/admin/users");
    const primary = page.getByRole("button", { name: "Add user", exact: true });
    await primary.hover();
    expect((await auditTextContrast(page)).failures).toEqual([]);
    await page.getByRole("button", { name: "Manage account for Test Member", exact: true }).click();
    await page.getByRole("button", { name: "Remove", exact: true }).first().hover();
    expect((await auditTextContrast(page)).failures).toEqual([]);
    await page.keyboard.press("Tab");
    await primary.focus();
    await expect(primary).toBeFocused();
    const focused = await primary.evaluate(element => ({ outline: getComputedStyle(element).outlineWidth, bounds: element.getBoundingClientRect().toJSON() }));
    expect(parseFloat(focused.outline)).toBeGreaterThanOrEqual(2);
    expect(focused.bounds.y).toBeGreaterThanOrEqual(64);
    await page.screenshot({ path: info.outputPath(`primary-focus-${scheme}.png`) });
  });

  test(`admin UI import file keyboard control ${scheme}`, async ({ page }, info) => {
    await page.emulateMedia({ colorScheme: scheme, reducedMotion: "reduce" });
    await setAuditViewport(page, { width: 320, height: 600 });
    await mockUI(page);
    await signIn(page);
    await page.goto("/admin/customers");
    // A deliberately bad sample proves the auditor detects low contrast instead
    // of silently accepting NaN; remove it before checking application controls.
    await page.evaluate(() => {
      const container = document.createElement("main");
      container.id = "contrast-audit-probe";
      const probe = document.createElement("p");
      probe.textContent = "Synthetic low-contrast audit probe";
      probe.style.cssText = "color: #ddd; background: #fff; font-size: 14px";
      container.append(probe);
      document.body.append(container);
    });
    const probeContrast = await auditTextContrast(page);
    expect(Number.isFinite(probeContrast.minimum)).toBe(true);
    expect(probeContrast.failures).toEqual(expect.arrayContaining([expect.objectContaining({ text: "Synthetic low-contrast audit probe" })]));
    await page.locator("#contrast-audit-probe").evaluate(element => element.remove());
    await page.getByRole("button", { name: "Import customers", exact: true }).click();
    const dialog = page.getByRole("dialog", { name: "Import customers", exact: true });
    const chooseFile = dialog.getByRole("button", { name: /choose a customer import file/i });
    await expect(chooseFile).toBeVisible();
    await page.mouse.move(1, 1);
    await page.evaluate(() => new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve))));
    const boundary = await chooseFile.evaluate(element => ({ border: getComputedStyle(element).borderTopColor, borderWidth: getComputedStyle(element).borderTopWidth, background: getComputedStyle(element).backgroundColor, role: element.getAttribute("role"), tag: element.tagName, selected: element.matches('dialog[open] [role="button"]'), hidden: element.matches(':disabled, [type="hidden"], [type="checkbox"]') }));
    await info.attach("import-control-boundary.json", { body: JSON.stringify(boundary, null, 2), contentType: "application/json" });
    const contrast = await auditTextContrast(page);
    await info.attach("import-control-contrast.json", { body: JSON.stringify(contrast, null, 2), contentType: "application/json" });
    expect(contrast.failures).toEqual([]);
    await chooseFile.hover();
    await page.evaluate(() => new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve))));
    expect((await auditTextContrast(page)).failures).toEqual([]);
    await chooseFile.focus();
    const chooser = page.waitForEvent("filechooser");
    await page.keyboard.press("Enter");
    await (await chooser).setFiles({ name: "synthetic-customers.csv", mimeType: "text/csv", buffer: Buffer.from("FirstName,LastName,Email,ContactNumber,HomeAddress\nSynthetic,Member,synthetic@example.test,+27000000000,Synthetic test address\n") });
    await expect(dialog.getByText("synthetic-customers.csv", { exact: true })).toBeVisible();
    await expect(dialog.getByRole("button", { name: "Preview file", exact: true })).toBeEnabled();
    await page.keyboard.press("Escape");
    await expect(dialog).not.toBeVisible();
  });

  test(`admin UI additional dialogs and navigation transitions ${scheme}`, async ({ page }, info) => {
    test.setTimeout(360_000);
    await page.emulateMedia({ colorScheme: scheme, reducedMotion: "reduce" });
    let fixture: FixtureState = "many";
    await mockUI(page, () => fixture);
    await signIn(page);
    const navigation = page.getByRole("navigation", { name: "Administration navigation" });
    for (const width of [639, 640, 641, 767, 768, 769, 900, 1023, 1024, 1025]) {
      await setAuditViewport(page, { width, height: 600 });
      if (width < 1024) {
        const opener = page.getByRole("button", { name: "Open administration navigation" });
        if (await opener.isVisible()) await opener.click();
      }
      await expect(navigation).toBeVisible();
      expect(await auditActionLayout(page), `navigation ${width}`).toEqual([]);
      expect((await overflow(page)).document).toBeLessThanOrEqual(width + 1);
      const lastLink = navigation.getByRole("link").last();
      await lastLink.focus();
      await lastLink.scrollIntoViewIfNeeded();
      const lastBounds = await lastLink.boundingBox();
      if ([639, 768, 1024].includes(width)) await page.screenshot({ path: info.outputPath(`short-navigation-${width}-${scheme}.png`) });
      expect(lastBounds!.y, `navigation ${width}`).toBeGreaterThanOrEqual(64);
      expect(lastBounds!.y + lastBounds!.height, `navigation ${width}`).toBeLessThanOrEqual(601);
    }
    await page.goto("/admin/dashboard");
    for (const width of [768, 820, 1024]) {
      await setAuditViewport(page, { width, height: 600 });
      const refresh = page.getByRole("button", { name: "Refresh data", exact: true });
      await expect(refresh).toBeEnabled();
      const lines = await refresh.evaluate(element => {
        const text = Array.from(element.childNodes).find(node => node.nodeType === Node.TEXT_NODE && node.textContent?.includes("Refresh data"))!;
        const range = document.createRange();
        range.selectNodeContents(text);
        return range.getClientRects().length;
      });
      expect(lines, `Dashboard refresh action should not be crushed at ${width}px`).toBe(1);
    }
    await page.goto("/admin/users");
    await setAuditViewport(page, { width: 1024, height: 600 });
    const addUser = page.getByRole("button", { name: "Add user", exact: true });
    await expect(addUser).toBeVisible();
    const labelLines = await addUser.evaluate(element => {
      const text = Array.from(element.childNodes).find(node => node.nodeType === Node.TEXT_NODE && node.textContent?.includes("Add user"))!;
      const range = document.createRange();
      range.selectNodeContents(text);
      return range.getClientRects().length;
    });
    expect(labelLines, "The short Add user action should retain its normal one-line label").toBe(1);

    const cases: { route: string; disclosure?: string; trigger: string; title: string; fixture?: FixtureState }[] = [
      { route: "customers", trigger: "Import customers", title: "Import customers" },
      { route: "customers", trigger: "Edit account", title: "Edit customer account" },
      { route: "users", trigger: "Add user", title: "Add user" },
      { route: "users", disclosure: "Manage account for Test Member", trigger: "Change access", title: "Change access level" },
      { route: "users", disclosure: "Manage account for Test Member", trigger: "Send password reset email", title: "Send password reset email" },
      { route: "tenants", trigger: "Add area", title: "Add area" },
      { route: "tenants", disclosure: "Manage area for Test Area", trigger: "Edit", title: "Edit area" },
      { route: "tenants", disclosure: "Manage area for Test Area", trigger: "Assign leader", title: "Assign area leader" },
      { route: "tenants", disclosure: "Manage area for Test Area", trigger: "Record cutoff baseline", title: "Record cutoff baseline" },
      { route: "members", disclosure: "Manage member for Test Member", trigger: "Edit profile", title: "Edit club member profile" },
      { route: "members", disclosure: "Manage member for Test Member", trigger: "Change plan", title: "Change membership plan" },
      { route: "members", disclosure: "Manage member for Test Member", trigger: "Suspend", title: "Suspend club member" },
      { route: "area-leaders", trigger: "Approve", title: "Approve area leader" },
      { route: "facilitators", trigger: "Approve", title: "Approve facilitator" },
      { route: "weekly-earnings", trigger: "Release for payment", title: "Release weekly earnings" },
      { route: "weekly-earnings", trigger: "Record external payment", title: "Record external payment", fixture: "released" },
      { route: "programme-participations", trigger: "Correct network placement", title: "Correct network placement" },
      { route: "programme-participations", trigger: "Reject", title: "Decline programme participation" },
      { route: "programme-participations", trigger: "Review termination", title: "Terminate locked AQGreen checkout", fixture: "recovery" },
      { route: "loans", trigger: "Review graduation", title: "Graduate Test Member to Onyx" },
      { route: "tenants", disclosure: `Manage area for ${longName}`, trigger: "Edit", title: "Edit area", fixture: "long-area" },
    ];
    for (const entry of cases) {
      fixture = entry.fixture ?? "many";
      await page.goto(`/admin/${entry.route}`);
      if (entry.disclosure) {
        const disclosure = page.getByRole("button", { name: entry.disclosure, exact: true });
        if (info.project.name === "mobile") await disclosure.tap();
        else await disclosure.click();
      }
      const trigger = page.getByRole("button", { name: entry.trigger, exact: true }).first();
      if (info.project.name === "mobile") await trigger.tap();
      else await trigger.click();
      const dialog = page.getByRole("dialog", { name: entry.title, exact: true });
      await expect(dialog).toBeVisible();
      expect((await auditTextContrast(page)).failures, entry.title).toEqual([]);
      if (entry.title === "Import customers") await expect(dialog.getByRole("button", { name: /choose a customer import file/i })).toBeVisible();
      for (const width of [320, 375, 480, 600, 639, 640, 641, 768, 1024, 1440]) {
        await setAuditViewport(page, { width, height: 600 });
        const footer = dialog.getByRole("button").last();
        await footer.scrollIntoViewIfNeeded();
        const bounds = await footer.boundingBox();
        expect(bounds!.x).toBeGreaterThanOrEqual(0);
        expect(bounds!.x + bounds!.width).toBeLessThanOrEqual(width);
        expect(bounds!.y + bounds!.height).toBeLessThanOrEqual(601);
        expect(await dialog.evaluate(element => element.scrollWidth <= element.clientWidth), `${entry.title} ${width}`).toBe(true);
        expect(await auditActionLayout(page), `${entry.title} ${width}`).toEqual([]);
        if ([320, 600, 1024].includes(width)) await page.screenshot({ path: info.outputPath(`${entry.route}-${entry.title.replaceAll(" ", "-")}-${width}-${scheme}.png`) });
      }
      // A reduced layout viewport approximates keyboard pressure; it does not
      // establish real virtual-keyboard or visualViewport behavior on a device.
      await setAuditViewport(page, { width: 320, height: 300 });
      const lastField = dialog.locator('input:not([type="hidden"]):not([type="checkbox"]):not(:disabled), select:not(:disabled), textarea:not(:disabled)').last();
      if (await lastField.count()) {
        await lastField.focus();
        await lastField.scrollIntoViewIfNeeded();
        const bounds = await lastField.boundingBox();
        expect(bounds!.y).toBeGreaterThanOrEqual(0);
        expect(bounds!.y + bounds!.height).toBeLessThanOrEqual(301);
      }
      const lastAction = dialog.locator("button:enabled").last();
      await lastAction.scrollIntoViewIfNeeded();
      const actionBounds = await lastAction.boundingBox();
      expect(actionBounds!.y + actionBounds!.height).toBeLessThanOrEqual(301);
      expect(await dialog.evaluate(element => element.scrollWidth <= element.clientWidth)).toBe(true);
      const close = dialog.getByRole("button", { name: "Close dialog", exact: true });
      await close.focus();
      await page.keyboard.press("Shift+Tab");
      await expect(lastAction).toBeFocused();
      await page.keyboard.press("Tab");
      await expect(close).toBeFocused();
      await page.screenshot({ path: info.outputPath(`${entry.route}-${entry.title.replaceAll(" ", "-")}-320x300-${scheme}.png`) });
      for (let i = 0; i < 8; i++) {
        await page.keyboard.press("Tab");
        expect(await dialog.evaluate(element => element.contains(document.activeElement))).toBe(true);
      }
      await page.keyboard.press("Escape");
      await expect(dialog).not.toBeVisible();
    }
  });

  test(`admin UI dashboard loading empty one-record and error states ${scheme}`, async ({ page }, info) => {
    test.setTimeout(240_000);
    await page.emulateMedia({ colorScheme: scheme, reducedMotion: "reduce" });
    let state: FixtureState = "many";
    await mockUI(page, () => state);
    await signIn(page);
    for (const value of ["loading", "empty", "one", "error", "optional-error"] as const) {
      state = value;
      for (const width of [375, 768, 1366]) {
        await setAuditViewport(page, { width, height: 800 });
        await page.goto("/admin/dashboard");
        const refresh = page.getByRole("button", { name: "Refresh data", exact: true });
        if (value === "loading") {
          await expect(page.locator("main .skeleton-shimmer").first()).toBeVisible();
          await expect(refresh).toBeDisabled();
        }
        await expect(refresh).toBeEnabled();
        // Enabling the button and completing its opacity transition are separate
        // browser events, even with reduced motion. Require its active appearance
        // rather than sampling a disabled-to-enabled transitional frame.
        await expect(refresh).toHaveCSS("opacity", "1");
        if (value === "error") {
          await expect(page.locator("main").getByRole("status")).toHaveText(/Live operational performance information is temporarily unavailable/);
          await expect(page.getByRole("region", { name: "Platform overview" })).toHaveCount(0);
          await expect(page.getByRole("heading", { name: "Customer account analytics" })).toHaveCount(0);
          await expect(page.getByRole("heading", { name: "Management", exact: true })).toBeVisible();
        } else {
          await expect(page.getByRole("region", { name: "Platform overview" })).toBeVisible();
          if (value === "empty") await expect(page.getByText("No recent activity.", { exact: true })).toBeVisible();
          if (value === "one") await expect(page.locator("main").getByText("Test Member", { exact: true })).toBeVisible();
          if (value === "optional-error") {
            for (const label of ["Area leaders", "Facilitators"]) {
              const card = page.locator("main article").filter({ has: page.locator("p", { hasText: new RegExp(`^${label}$`) }) });
              await expect(card.getByText("Unavailable", { exact: true })).toBeVisible();
            }
            await expect(page.getByText("Referral data unavailable", { exact: true })).toBeVisible();
            await expect(page.getByRole("heading", { name: "Customer account analytics" })).toBeVisible();
          }
        }
        expect((await overflow(page)).document, `dashboard ${value} ${width}`).toBeLessThanOrEqual(width + 1);
        expect((await auditTextContrast(page)).failures).toEqual([]);
        if (value === "empty" || value === "error" || value === "optional-error") await page.screenshot({ path: info.outputPath(`dashboard-${value}-${width}-${scheme}.png`), fullPage: true });
      }
    }
  });

  test(`admin UI selections preserve known values and reject unsupported defaults ${scheme}`, async ({ page }, info) => {
    test.setTimeout(90_000);
    await page.emulateMedia({ colorScheme: scheme, reducedMotion: "reduce" });
    await setAuditViewport(page, { width: 375, height: 700 });
    let state: FixtureState = "plan-error";
    await mockUI(page, () => state);
    await signIn(page);
    await page.goto("/admin/customers");
    await page.getByRole("button", { name: "Edit account", exact: true }).first().click();
    const customerDialog = page.getByRole("dialog", { name: "Edit customer account" });
    await expect(customerDialog.getByRole("alert")).toBeVisible();
    await expect(customerDialog.getByLabel("Membership plan")).toHaveValue("1");
    expect(await customerDialog.evaluate(element => element.scrollWidth <= element.clientWidth)).toBe(true);
    await page.screenshot({ path: info.outputPath(`plan-lookup-failure-${scheme}.png`) });
    await page.keyboard.press("Escape");
    state = "unsupported-role";
    await page.goto("/admin/users");
    await page.getByRole("button", { name: "Manage account for Test Member", exact: true }).click();
    await page.getByRole("button", { name: "Change access", exact: true }).click();
    const roleDialog = page.getByRole("dialog", { name: "Change access level" });
    const writes: unknown[] = [];
    page.on("request", request => { if (request.method() === "POST" && request.url().endsWith("/AdminUser/AssignRole")) writes.push(request.postDataJSON()); });
    await expect(roleDialog.getByLabel("Access level")).toHaveValue("");
    await roleDialog.getByLabel("Reason for change").fill("Explicit access selection");
    await roleDialog.getByRole("button", { name: "Update access", exact: true }).click();
    expect(writes).toEqual([]);
    await roleDialog.getByLabel("Access level").selectOption("0");
    await roleDialog.getByRole("button", { name: "Update access", exact: true }).click();
    await expect.poll(() => writes.length).toBe(1);
    expect(writes[0]).toEqual({ id: 1, justification: "Explicit access selection", role: 0 });
    // This write is intentionally rejected by the mock, never applied to an account.
    await expect(roleDialog.getByRole("alert")).toBeVisible();
    expect((await auditTextContrast(page)).failures).toEqual([]);
    await page.screenshot({ path: info.outputPath(`explicit-role-selection-${scheme}.png`) });
  });
}

for (const scheme of ["light", "dark"] as const) test(`admin UI loading empty one-row API error and permissions ${scheme}`, async ({ page }, info) => {
  test.setTimeout(480_000);
  await page.emulateMedia({ colorScheme: scheme, reducedMotion: "reduce" });
  let state: FixtureState = "many";
  await mockUI(page, () => state);
  await signIn(page);
  for (const value of ["loading", "empty", "one", "error"] as const) {
    state = value;
    for (const route of routes.filter(route => route !== "dashboard")) {
      await setAuditViewport(page, { width: 320, height: 700 });
      await page.goto(`/admin/${route}`);
      if (value === "loading") await expect(page.locator("main .skeleton-shimmer").first()).toBeVisible();
      if (value === "error") {
        await expect(page.locator("main").getByRole("alert").first()).toBeVisible();
        await expect(page.getByRole("table")).toHaveCount(0);
        await expect(page.getByRole("button", { name: /^Retry loading/ }).first()).toBeVisible();
        await expect(page.getByText(/No (records|customer accounts|areas|club members|access levels|persisted|AQGreen|locked AQGreen|weekly sales)/)).toHaveCount(0);
        expect((await auditTextContrast(page)).failures).toEqual([]);
        if (route === "weekly-earnings") await expect(page.getByText("Calculated earnings").locator(".." )).toContainText("Unavailable");
        if (["users", "weekly-earnings", "programme-participations"].includes(route)) await page.screenshot({ path: info.outputPath(`${route}-failed-load-${scheme}.png`), fullPage: true });
      } else {
        await expect(page.getByRole("table").first()).toBeVisible();
      }
      const dimensions = await overflow(page);
      expect(dimensions.document, `${route} ${value}`).toBeLessThanOrEqual(321);
    }
  }
  // Failed reads must be retryable without presenting cached/empty data as current.
  for (const route of routes.filter(route => route !== "dashboard")) {
    state = "error";
    await page.goto(`/admin/${route}`);
    const retries = page.getByRole("button", { name: /^Retry loading/ });
    await expect(retries.first()).toBeVisible();
    if (route === "programme-participations") await expect(retries).toHaveCount(2);
    const labels = await retries.evaluateAll(elements => elements.map(element => element.getAttribute("aria-label")!));
    state = "many";
    for (const label of labels) await page.getByRole("button", { name: label, exact: true }).click();
    await expect(page.getByRole("table").first()).toBeVisible();
    await expect(page.getByRole("region", { name: /unavailable$/ })).toHaveCount(0);
    expect((await overflow(page)).document).toBeLessThanOrEqual(321);
  }
  await page.getByRole("button", { name: "Open user menu" }).click();
  await page.getByRole("button", { name: "Sign out" }).click();
  await expect(page).toHaveURL("/");
  await signIn(page, "admin-ui-read@example.test");
  state = "many";
  for (const route of ["users", "tenants", "members", "area-leaders", "facilitators"]) {
    await page.goto(`/admin/${route}`);
    await expect(page.getByRole("table")).toBeVisible();
    await expect(page.getByRole("button", { name: /Manage |More actions/ })).toHaveCount(0);
  }
  await page.getByRole("button", { name: "Open user menu" }).click();
  await page.getByRole("button", { name: "Sign out" }).click();
  await expect(page).toHaveURL("/");
  await signIn(page, "admin-ui-denied@example.test");
  for (const route of routes.filter(route => route !== "dashboard")) {
    await page.goto(`/admin/${route}`);
    await expect(page.locator("main").getByRole("alert")).toBeVisible();
    await expect(page.getByRole("table")).toHaveCount(0);
  }
});
