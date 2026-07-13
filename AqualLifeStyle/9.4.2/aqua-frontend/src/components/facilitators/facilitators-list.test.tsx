import { render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import type { Facilitator } from "@/src/providers/Facilitators/context";
import { useFacilitatorsActions, useFacilitatorsState } from "@/src/providers";

import { FacilitatorsList } from "./facilitators-list";

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

const facilitators: Facilitator[] = [
  {
    id: 1,
    tenantId: 1,
    customerId: 20,
    areaLeaderId: 10,
    rank: 1,
    directReferrals: 8,
    indirectReferrals: 4,
    awardBalance: 1500,
  },
  {
    id: 2,
    tenantId: 1,
    customerId: 21,
    areaLeaderId: 10,
    rank: 3,
    directReferrals: 25,
    indirectReferrals: 12,
    awardBalance: 5000,
  },
];

const baseState = {
  facilitators,
  isLoadError: false,
  isLoadPending: false,
  isLoadSuccess: true,
  isRegisterError: false,
  isRegisterPending: false,
  isRegisterSuccess: false,
  loadErrorMessage: null,
  registerErrorMessage: null,
  selectedFacilitator: null,
  selectedErrorMessage: null,
};

beforeEach(() => {
  vi.resetAllMocks();

  (useFacilitatorsState as unknown as { mockReturnValue: typeof baseState }).mockReturnValue(baseState);
  (useFacilitatorsActions as unknown as { mockReturnValue: { getFacilitators: () => Promise<void> } }).mockReturnValue({
    getFacilitators: vi.fn(),
  });
});

describe("FacilitatorsList", () => {
  it("renders the facilitators list", () => {
    render(<FacilitatorsList />);

    expect(screen.getByRole("heading", { name: /Facilitators/i })).toBeDefined();
    expect(screen.getByText("Customer #20")).toBeDefined();
    expect(screen.getByText("Customer #21")).toBeDefined();
  });

  it("shows loading state", () => {
    (useFacilitatorsState as unknown as { mockReturnValue: typeof baseState }).mockReturnValue({
      ...baseState,
      isLoadPending: true,
    });

    render(<FacilitatorsList />);

    expect(screen.queryByText("Customer #20")).toBeNull();
  });

  it("shows error state", () => {
    (useFacilitatorsState as unknown as { mockReturnValue: typeof baseState }).mockReturnValue({
      ...baseState,
      isLoadError: true,
      loadErrorMessage: "Failed to load facilitators",
    });

    render(<FacilitatorsList />);

    expect(screen.getByText("Failed to load facilitators")).toBeDefined();
  });

  it("shows empty state when there are no facilitators", () => {
    (useFacilitatorsState as unknown as { mockReturnValue: typeof baseState }).mockReturnValue({
      ...baseState,
      facilitators: [],
    });

    render(<FacilitatorsList />);

    expect(screen.getByText("No facilitators")).toBeDefined();
    expect(screen.getByText("No facilitators found.")).toBeDefined();
  });

  it("renders the rank filter", () => {
    render(<FacilitatorsList />);

    expect(screen.getByLabelText("Rank")).toBeDefined();
  });
});
