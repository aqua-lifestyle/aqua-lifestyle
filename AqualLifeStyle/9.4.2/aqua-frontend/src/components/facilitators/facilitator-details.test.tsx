import { render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import type { Facilitator } from "@/src/providers/Facilitators/context";
import { useFacilitatorsActions, useFacilitatorsState } from "@/src/providers";

import { FacilitatorDetails } from "./facilitator-details";

vi.mock("@/src/providers", async () => {
  const actual = await vi.importActual<typeof import("@/src/providers")>(
    "@/src/providers",
  );
  return {
    ...actual,
    useFacilitatorsActions: vi.fn(),
    useFacilitatorsState: vi.fn(),
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

  (useFacilitatorsState as unknown as { mockReturnValue: typeof baseState }).mockReturnValue(baseState);
  (useFacilitatorsActions as unknown as { mockReturnValue: { getFacilitator: () => Promise<void> } }).mockReturnValue({
    getFacilitator: vi.fn(),
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
    (useFacilitatorsState as unknown as { mockReturnValue: typeof baseState }).mockReturnValue({
      ...baseState,
      isSelectedPending: true,
      selectedFacilitator: null,
    });

    render(<FacilitatorDetails facilitatorId={1} />);

    expect(screen.queryByText("Customer #20")).toBeNull();
  });

  it("shows error state", () => {
    (useFacilitatorsState as unknown as { mockReturnValue: typeof baseState }).mockReturnValue({
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
