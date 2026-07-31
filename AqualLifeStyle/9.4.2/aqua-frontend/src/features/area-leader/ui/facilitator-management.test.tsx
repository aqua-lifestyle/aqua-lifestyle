import { render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import {
  useCustomersActions,
  useCustomersState,
  useFacilitatorsActions,
  useFacilitatorsState,
} from "@/src/providers";

import { FacilitatorManagement } from "./facilitator-management";

vi.mock("@/src/providers", () => ({
  useCustomersActions: vi.fn(),
  useCustomersState: vi.fn(),
  useFacilitatorsActions: vi.fn(),
  useFacilitatorsState: vi.fn(),
}));

beforeEach(() => {
  vi.resetAllMocks();
  vi.mocked(useCustomersActions).mockReturnValue({ getCustomers: vi.fn() } as never);
  vi.mocked(useFacilitatorsActions).mockReturnValue({ getFacilitators: vi.fn() } as never);
  vi.mocked(useCustomersState).mockReturnValue({
    customers: [],
    isLoadError: true,
  } as never);
  vi.mocked(useFacilitatorsState).mockReturnValue({
    facilitators: [],
    isLoadError: false,
  } as never);
});

describe("FacilitatorManagement", () => {
  it("does not substitute demo applications when live data fails", () => {
    render(<FacilitatorManagement />);

    expect(screen.getByText(/Live facilitator applications could not be loaded/i)).toBeDefined();
    expect(screen.queryByText(/Demo applications/i)).toBeNull();
    expect(screen.queryByRole("button", { name: /approve/i })).toBeNull();
  });
});
