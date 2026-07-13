import { render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import type { Facilitator } from "@/src/providers/Facilitators/context";
import { useFacilitatorsActions, useFacilitatorsState, useAuthState } from "@/src/providers";

import { FacilitatorDetails } from "./facilitator-details";

vi.mock("@/src/providers", async () => {
  const actual = await vi.importActual<typeof import("@/src/providers")>(
    "@/src/providers",
  );
  return {
    ...actual,
    useFacilitatorsActions: vi.fn(),
    useFacilitatorsState: vi.fn(),
    useAuthState: vi.fn(),
  };
});

const facilitator: Facilitator = {
  id: 1,
  tenantId: 1,
  customerId: 20,
  areaLeaderId: 10,
  rank: 1,
  directReferrals: 8,
  indirectReferrals: 4,
  awardBalance: 1500,
};

const baseState = {
  facilitators: [],
  isLoadError: false,
  isLoadPending: false,
  isLoadSuccess: true,
  isRegisterError: false,
  isRegisterPending: false,
  isRegisterSuccess: false,
  isSelectedError: false,
  isSelectedPending: false,
  isSelectedSuccess: true,
  loadErrorMessage: null,
  registerErrorMessage: null,
  selectedFacilitator: facilitator,
  selectedErrorMessage: null,
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
        permissions: ["Pages.Facilitators"],
        role: "admin",
      },
    },
  });

  vi.mocked(useFacilitatorsState).mockReturnValue(baseState);
  vi.mocked(useFacilitatorsActions).mockReturnValue({
    getFacilitator: vi.fn(),
    getFacilitators: vi.fn(),
    getFacilitatorsByCustomer: vi.fn(),
    registerFacilitator: vi.fn(),
  });
});

describe("FacilitatorDetails", () => {
  it("renders facilitator details", async () => {
    render(<FacilitatorDetails facilitatorId={1} />);

    await waitFor(() => {
      expect(screen.getByRole("heading", { name: /Facilitator details/i })).toBeDefined();
    });

    expect(screen.getByText("Customer #20")).toBeDefined();
  });

  it("shows loading state", () => {
    vi.mocked(useFacilitatorsState).mockReturnValue({
      ...baseState,
      isSelectedPending: true,
      selectedFacilitator: null,
    });

    render(<FacilitatorDetails facilitatorId={1} />);

    expect(screen.queryByText("Customer #20")).toBeNull();
  });

  it("shows error state", () => {
    vi.mocked(useFacilitatorsState).mockReturnValue({
      ...baseState,
      isSelectedError: true,
      selectedErrorMessage: "Facilitator not found",
    });

    render(<FacilitatorDetails facilitatorId={1} />);

    expect(screen.getByText("Facilitator not found")).toBeDefined();
  });

  it("shows invalid id error", () => {
    render(<FacilitatorDetails facilitatorId={-1} />);

    expect(screen.getByText("This facilitator id is invalid.")).toBeDefined();
  });
});
