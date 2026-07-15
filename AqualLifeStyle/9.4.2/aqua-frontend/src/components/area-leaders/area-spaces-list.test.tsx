import { render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import type { AreaSpace } from "@/src/providers/AreaSpaces/context";
import { useAreaSpacesActions, useAreaSpacesState, useAuthState } from "@/src/providers";

import { AreaSpacesList } from "./area-spaces-list";

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

const areaSpaces: AreaSpace[] = [
  {
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
  },
  {
    id: 2,
    tenantId: 1,
    areaLeaderId: 11,
    addressLine: "456 Oak Ave",
    capacity: "30",
    interestedMembers: 35,
    status: 2,
    reviewStartedAt: "2024-01-01T00:00:00Z",
    presentationsCompleted: 4,
    startupOrdersCompleted: 20,
    approvedAt: "2024-01-15T00:00:00Z",
  },
];

const baseState = {
  areaSpaces,
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
  isSelectedSuccess: false,
  isSuspendError: false,
  isSuspendPending: false,
  isSuspendSuccess: false,
  applyErrorMessage: null,
  approveErrorMessage: null,
  loadErrorMessage: null,
  selectedAreaSpace: null,
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

describe("AreaSpacesList", () => {
  it("renders the area spaces list", () => {
    render(<AreaSpacesList />);

    expect(screen.getByRole("heading", { name: /Area Spaces/i })).toBeDefined();
    expect(screen.getByText("123 Main St")).toBeDefined();
    expect(screen.getByText("456 Oak Ave")).toBeDefined();
  });

  it("shows loading state", () => {
    vi.mocked(useAreaSpacesState).mockReturnValue({
      ...baseState,
      isLoadPending: true,
    });

    render(<AreaSpacesList />);

    expect(screen.queryByText("123 Main St")).toBeNull();
  });

  it("shows error state", () => {
    vi.mocked(useAreaSpacesState).mockReturnValue({
      ...baseState,
      isLoadError: true,
      loadErrorMessage: "Failed to load area spaces",
    });

    render(<AreaSpacesList />);

    expect(screen.getByText("Failed to load area spaces")).toBeDefined();
  });

  it("shows empty state when there are no area spaces", () => {
    vi.mocked(useAreaSpacesState).mockReturnValue({
      ...baseState,
      areaSpaces: [],
    });

    render(<AreaSpacesList />);

    expect(screen.getByText("No area spaces")).toBeDefined();
    expect(screen.getByText("No area spaces found.")).toBeDefined();
  });

  it("renders the status filter", () => {
    render(<AreaSpacesList />);

    expect(screen.getByLabelText("Status")).toBeDefined();
  });
});
