import { render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { RecoveryAdminWorkspace } from "./recovery-admin-workspace";
import { ORIGINAL_JOHANNESBURG_AREA_ID } from "./b6-contract";
let auth = { isReady: true, isAuthenticated: true, status: "authenticated", session: {
  user: { id: 4, tenantId: 1, role: "SystemAdmin" }, tenant: "Default", expiresAt: "2099-01-01",
} };
vi.mock("@/src/providers", () => ({ useAuthState: () => auth }));
const scope = { userId: 4, tenant: { id: 1, name: "Synthetic Tenant", tenancyName: "Default" }, areas: [
  { areaId: ORIGINAL_JOHANNESBURG_AREA_ID, code: "JHB", name: "Johannesburg" },
] };
describe("recovery administrator workspace", () => {
  beforeEach(() => {
    auth = { isReady: true, isAuthenticated: true, status: "authenticated", session: {
      user: { id: 4, tenantId: 1, role: "SystemAdmin" }, tenant: "Default", expiresAt: "2099-01-01",
    } };
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(new Response(JSON.stringify({
      recoveryVerified: true, scope, scopeVerified: true,
    }))));
  });
  afterEach(() => vi.unstubAllGlobals());
  it("shows authenticated live scope and a discoverable B6 action without a business dashboard", async () => {
    render(<RecoveryAdminWorkspace />);
    await screen.findByText(ORIGINAL_JOHANNESBURG_AREA_ID);
    expect(screen.getByText("Tenant 1: Synthetic Tenant (Default)")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Open bounded B6 discovery" })).toHaveAttribute("href", "/admin/b6-discovery");
    expect(fetch).toHaveBeenCalledWith("/api/recovery/b6?operation=scope", expect.objectContaining({ cache: "no-store", credentials: "same-origin" }));
    expect(screen.queryByText(/Establishing the application capability/)).not.toBeInTheDocument();
  });
  it("shows an exact scope blocker and no historical inventory", async () => {
    vi.mocked(fetch).mockResolvedValue(new Response(JSON.stringify({
      recoveryVerified: true, scope, scopeVerified: false, blocker: "This account has multiple Area assignments.",
    }), { status: 403 }));
    render(<RecoveryAdminWorkspace discovery />);
    expect(await screen.findByRole("alert")).toHaveTextContent("multiple Area assignments");
    expect(screen.queryByRole("region", { name: "Discovery inventory" })).not.toBeInTheDocument();
  });
  it("distinguishes partial coverage and displays conflict and missing evidence counts", async () => {
    vi.mocked(fetch).mockResolvedValue(new Response(JSON.stringify({ recoveryVerified: true, scope, scopeVerified: true,
      inventory: { examined: 100, totalCount: 105, limit: 100, wholeCurrentScopeExamined: false,
        classifications: [0, 100, 0, 0, 0], evidenceIssues: { SPONSOR_HISTORY_UNVERIFIABLE: 90, JOINING_PAYMENT_LINKS_CONFLICT: 3 }, observedAt: "2026-10-14T00:00:00Z" },
    })));
    render(<RecoveryAdminWorkspace discovery />);
    await screen.findByText(/Partial population/);
    expect(screen.getByText("JOINING_PAYMENT_LINKS_CONFLICT")).toBeInTheDocument();
    expect(screen.getByText("SPONSOR_HISTORY_UNVERIFIABLE")).toBeInTheDocument();
    expect(screen.getByText(/Missing evidence does not prove/)).toBeInTheDocument();
  });
  it("does not reuse another identity's displayed scope after a session change", async () => {
    const view = render(<RecoveryAdminWorkspace />);
    await screen.findByText(ORIGINAL_JOHANNESBURG_AREA_ID);
    vi.mocked(fetch).mockImplementation(() => new Promise(() => {}));
    auth = { ...auth, session: { ...auth.session, user: { ...auth.session.user, id: 5, tenantId: 2 } } };
    view.rerender(<RecoveryAdminWorkspace />);
    expect(screen.queryByText(ORIGINAL_JOHANNESBURG_AREA_ID)).not.toBeInTheDocument();
    await waitFor(() => expect(fetch).toHaveBeenCalledTimes(2));
  });
  it.each(["anonymous", "non-admin", "error"])("halts before scope requests for %s access", async kind => {
    if (kind === "anonymous") auth = { ...auth, isAuthenticated: false };
    if (kind === "non-admin") auth.session.user.role = "Member";
    if (kind === "error") auth.status = "error";
    render(<RecoveryAdminWorkspace />);
    expect(screen.getByRole("alert")).toBeInTheDocument();
    expect(fetch).not.toHaveBeenCalled();
  });
});
