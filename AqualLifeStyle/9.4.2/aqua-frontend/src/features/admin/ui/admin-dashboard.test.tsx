import { fireEvent, render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { RecoveryContext } from "@/src/shared/hooks/recovery-context";
import { AdminDashboard } from "./admin-dashboard";

const state = vi.hoisted(() => ({
  tenantId: 1 as number | undefined,
  role: "SystemAdmin",
  healthError: false,
  checkHealth: vi.fn(),
  operationalReads: vi.fn(),
}));
vi.mock("@/src/providers", () => ({
  useAuthState: () => ({ isReady: true, isAuthenticated: true, session: { user: { id: 1, role: state.role, tenantId: state.tenantId, permissions: ["Aqua.Admin.Customers.View", "Aqua.Admin.Commissions.View"] } } }),
  useCustomersActions: () => ({ getCustomers: state.operationalReads }),
  useEnquiriesActions: () => ({ getEnquiries: state.operationalReads }),
  useMembershipsActions: () => ({ getMemberships: state.operationalReads }),
  useOrderIntentsActions: () => ({ getOrderIntents: state.operationalReads }),
  useAreaLeadersActions: () => ({ getAreaLeaders: state.operationalReads }),
  useFacilitatorsActions: () => ({ getFacilitators: state.operationalReads }),
  useReferralsActions: () => ({ getReferrals: state.operationalReads }),
  useSystemHealthActions: () => ({ checkHealth: state.checkHealth }),
  useCustomersState: () => ({ customers: [], isLoadError: true }),
  useEnquiriesState: () => ({ enquiries: [], isLoadError: true }),
  useMembershipsState: () => ({ memberships: [], isError: true }),
  useOrderIntentsState: () => ({ orderIntents: [], isLoadError: true }),
  // Stale cache values must never be shown as recovery inventory or financial facts.
  useAreaLeadersState: () => ({ areaLeaders: [{ id: 9 }], isLoadSuccess: true }),
  useFacilitatorsState: () => ({ facilitators: [], isLoadSuccess: true }),
  useReferralsState: () => ({ referrals: [], isLoadSuccess: true }),
  useSystemHealthState: () => ({ isError: state.healthError, health: state.healthError ? null : { status: "Healthy" }, errorMessage: "Health unavailable" }),
}));

describe("existing administration overview in restricted recovery", () => {
  beforeEach(() => { vi.clearAllMocks(); state.tenantId = 1; state.role = "SystemAdmin"; state.healthError = false; });
  it.each([1, undefined])("retains the original overview for tenant/host identity %s without requesting restricted records", (tenantId) => {
    state.tenantId = tenantId;
    render(<RecoveryContext.Provider value={true}><AdminDashboard /></RecoveryContext.Provider>);
    expect(screen.getByRole("heading", { name: "Administration overview" })).toBeInTheDocument();
    expect(screen.getByText(/Restricted recovery mode is active/)).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Customer accounts" })).toBeInTheDocument();
    expect(screen.queryByRole("link", { name: /Customer accounts/ })).not.toBeInTheDocument();
    expect(screen.queryByRole("heading", { name: "User accounts & access" })).not.toBeInTheDocument();
    expect(screen.queryByText("Savings balance")).not.toBeInTheDocument();
    expect(screen.queryByText("Total customers")).not.toBeInTheDocument();
    expect(state.operationalReads).not.toHaveBeenCalled();
    expect(state.checkHealth).toHaveBeenCalledTimes(1);
    fireEvent.click(screen.getByRole("button", { name: "Refresh system status" }));
    expect(state.checkHealth).toHaveBeenCalledTimes(2);
    expect(state.operationalReads).not.toHaveBeenCalled();
  });
  it("shows failed health accurately without substituting financial metrics", () => {
    state.healthError = true;
    render(<RecoveryContext.Provider value={true}><AdminDashboard /></RecoveryContext.Provider>);
    expect(screen.getByText("Unavailable", { exact: true })).toBeInTheDocument();
    expect(screen.getByText("Health unavailable")).toBeInTheDocument();
    expect(screen.queryByText("Healthy")).not.toBeInTheDocument();
    expect(state.operationalReads).not.toHaveBeenCalled();
  });
  it("preserves normal authorised management links and normal read behavior outside recovery", () => {
    render(<AdminDashboard />);
    expect(screen.getByRole("link", { name: /Customer accounts/ })).toHaveAttribute("href", "/admin/customers");
    expect(screen.queryByText(/Restricted recovery mode/)).not.toBeInTheDocument();
    expect(state.operationalReads).toHaveBeenCalledTimes(7);
    expect(screen.getByText(/Live operational performance information is temporarily unavailable/)).toBeInTheDocument();
  });
  it("does not issue dashboard requests for a non-administrator", () => {
    state.role = "Member";
    render(<RecoveryContext.Provider value={true}><AdminDashboard /></RecoveryContext.Provider>);
    expect(state.checkHealth).not.toHaveBeenCalled();
    expect(state.operationalReads).not.toHaveBeenCalled();
  });
});
