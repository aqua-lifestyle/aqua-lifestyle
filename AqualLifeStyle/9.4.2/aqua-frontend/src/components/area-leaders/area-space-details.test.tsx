import { render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import type { AreaSpace } from "@/src/providers/AreaSpaces/context";
import { useAreaSpacesActions, useAreaSpacesState, useAuthState } from "@/src/providers";

import { AreaSpaceDetails } from "./area-space-details";

vi.mock("@/src/providers", async () => {
  const actual = await vi.importActual<typeof import("@/src/providers")>(
    "@/src/providers",
  );
  return {
    ...actual,
    useAreaSpacesActions: vi.fn(),
    useAreaSpacesState: vi.fn(),
    useAuthState: vi.fn(),
  };
});

const areaSpace: AreaSpace = {
  id: 1,
  tenantId: 1,
  areaLeaderId: 10,
  addressLine: "123 Main St",
  capacity: "50",
  interestedMembers: 25,
  status: 1,
  reviewStartedAt: "2024-01-01T00:00:00Z",
  presentationsCompleted: 2,
  startupOrdersCompleted: 10,
  approvedAt: null,
};

const baseState = {
  areaSpaces: [],
  isApplyError: false,
  isApplyPending: false,
  isApplySuccess: false,
  isApproveError: false,
  isApprovePending: false,
  isApproveSuccess: false,
  isLoadError: false,
  isLoadPending: false,
  isLoadSuccess: true,
  isSelectedError: false,
  isSelectedPending: false,
  isSelectedSuccess: true,
  isSuspendError: false,
  isSuspendPending: false,
  isSuspendSuccess: false,
  applyErrorMessage: null,
  approveErrorMessage: null,
  loadErrorMessage: null,
  selectedAreaSpace: areaSpace,
  selectedErrorMessage: null,
  suspendErrorMessage: null,
};

beforeEach(() => {
  vi.resetAllMocks();

  vi.mocked(useAuthState).mockReturnValue({
    isAuthenticated: true,
    isReady: true,
    session: {
      accessToken: "token",
      expiresAt: null,
      user: {
        id: 99,
        email: "test@example.com",
        name: "Test User",
        permissions: ["Pages.AreaSpaces"],
        role: "admin",
      },
    },
  });

  vi.mocked(useAreaSpacesState).mockReturnValue(baseState);
  vi.mocked(useAreaSpacesActions).mockReturnValue({
    applyAreaSpace: vi.fn(),
    approveAreaSpace: vi.fn(),
    getAreaSpace: vi.fn(),
    getAreaSpaces: vi.fn(),
    startReview: vi.fn(),
    recordPresentation: vi.fn(),
    recordStartupOrder: vi.fn(),
    suspendAreaSpace: vi.fn(),
  });
});

describe("AreaSpaceDetails", () => {
  it("renders area space details", async () => {
    render(<AreaSpaceDetails areaSpaceId={1} />);

    await waitFor(() => {
      expect(screen.getByRole("heading", { name: /Area Space details/i })).toBeDefined();
    });

    expect(screen.getByText("Area Space #1")).toBeDefined();
    expect(screen.getByText("123 Main St")).toBeDefined();
  });

  it("shows loading state", () => {
    vi.mocked(useAreaSpacesState).mockReturnValue({
      ...baseState,
      isSelectedPending: true,
      selectedAreaSpace: null,
    });

    render(<AreaSpaceDetails areaSpaceId={1} />);

    expect(screen.queryByText("123 Main St")).toBeNull();
  });

  it("shows error state", () => {
    vi.mocked(useAreaSpacesState).mockReturnValue({
      ...baseState,
      isSelectedError: true,
      selectedErrorMessage: "Area space not found",
    });

    render(<AreaSpaceDetails areaSpaceId={1} />);

    expect(screen.getByText("Area space not found")).toBeDefined();
  });

  it("shows invalid id error", () => {
    render(<AreaSpaceDetails areaSpaceId={-1} />);

    expect(screen.getByText("This area space id is invalid.")).toBeDefined();
  });
});
