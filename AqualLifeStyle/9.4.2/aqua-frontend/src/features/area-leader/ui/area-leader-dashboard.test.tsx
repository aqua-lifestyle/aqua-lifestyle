import { render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import {
  useAreaLeadersActions,
  useAreaLeadersState,
  useAreaSpacesActions,
  useAreaSpacesState,
  useCustomersActions,
  useCustomersState,
  useEnquiriesActions,
  useEnquiriesState,
  useFacilitatorsActions,
  useFacilitatorsState,
  useOrderIntentsActions,
  useOrderIntentsState,
} from "@/src/providers";

import { AreaLeaderDashboard } from "./area-leader-dashboard";

vi.mock("@/src/providers", () => ({
  useAreaLeadersActions: vi.fn(),
  useAreaLeadersState: vi.fn(),
  useAreaSpacesActions: vi.fn(),
  useAreaSpacesState: vi.fn(),
  useCustomersActions: vi.fn(),
  useCustomersState: vi.fn(),
  useEnquiriesActions: vi.fn(),
  useEnquiriesState: vi.fn(),
  useFacilitatorsActions: vi.fn(),
  useFacilitatorsState: vi.fn(),
  useOrderIntentsActions: vi.fn(),
  useOrderIntentsState: vi.fn(),
}));

const loadState = { isLoadError: false, isLoadPending: false };

beforeEach(() => {
  vi.resetAllMocks();
  vi.mocked(useAreaLeadersState).mockReturnValue({ ...loadState, areaLeaders: [] } as never);
  vi.mocked(useAreaSpacesState).mockReturnValue({ ...loadState, areaSpaces: [] } as never);
  vi.mocked(useCustomersState).mockReturnValue({
    ...loadState,
    customers: [],
    isMyCustomerPending: false,
    myCustomer: null,
  } as never);
  vi.mocked(useEnquiriesState).mockReturnValue({ ...loadState, enquiries: [] } as never);
  vi.mocked(useFacilitatorsState).mockReturnValue({ ...loadState, facilitators: [] } as never);
  vi.mocked(useOrderIntentsState).mockReturnValue({
    ...loadState,
    actionErrorMessage: null,
    isActionPending: false,
    orderIntents: [],
  } as never);

  vi.mocked(useAreaLeadersActions).mockReturnValue({ getAreaLeaders: vi.fn() } as never);
  vi.mocked(useAreaSpacesActions).mockReturnValue({ getAreaSpaces: vi.fn() } as never);
  vi.mocked(useCustomersActions).mockReturnValue({
    getCustomers: vi.fn(),
    getMyCustomer: vi.fn(),
  } as never);
  vi.mocked(useEnquiriesActions).mockReturnValue({ getEnquiries: vi.fn() } as never);
  vi.mocked(useFacilitatorsActions).mockReturnValue({ getFacilitators: vi.fn() } as never);
  vi.mocked(useOrderIntentsActions).mockReturnValue({
    completeOrderIntent: vi.fn(),
    getOrderIntents: vi.fn(),
  } as never);
});

describe("AreaLeaderDashboard operations", () => {
  it("shows a live-data error instead of substituting demo operations", () => {
    vi.mocked(useEnquiriesState).mockReturnValue({
      ...loadState,
      enquiries: [],
      isLoadError: true,
    } as never);

    render(<AreaLeaderDashboard />);

    expect(screen.getByText("Live data unavailable")).toBeDefined();
    expect(screen.getByText(/Some live Area data could not be loaded/i)).toBeDefined();
    expect(screen.queryByText(/Demo fallback data/i)).toBeNull();
  });
});
