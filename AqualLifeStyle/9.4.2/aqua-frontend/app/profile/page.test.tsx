import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { useAuthActions, useAuthState } from "@/src/providers";
import { httpClient } from "@/src/shared/api";
import ProfilePage from "./page";

vi.mock("@/src/components/auth/authenticated-page", () => ({
  AuthenticatedPage: ({ children }: { children: React.ReactNode }) => children,
}));
vi.mock("@/src/providers", () => ({
  useAuthActions: vi.fn(),
  useAuthState: vi.fn(),
}));
vi.mock("@/src/shared/api", async () => {
  const actual = await vi.importActual<typeof import("@/src/shared/api")>("@/src/shared/api");
  return { ...actual, httpClient: { get: vi.fn(), put: vi.fn() } };
});

describe("customer profile", () => {
  const setSession = vi.fn();
  const session = {
    accessToken: "token",
    expiresAt: "2099-01-01T00:00:00Z",
    refreshToken: "refresh",
    user: { email: "ada@example.com", id: 7, name: "Ada Lovelace", permissions: [], role: "Guest" },
  };

  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(useAuthState).mockReturnValue({ isAuthenticated: true, isReady: true, session });
    vi.mocked(useAuthActions).mockReturnValue({ clearSession: vi.fn(), setReady: vi.fn(), setSession });
    vi.mocked(httpClient.get).mockResolvedValue({
      contactNumber: "+27 82 123 4567",
      emailAddress: "ada@example.com",
      firstName: "Ada",
      homeAddress: "10 Aqua Street, Johannesburg",
      surname: "Lovelace",
    });
  });

  it("loads and saves the linked customer details", async () => {
    vi.mocked(httpClient.put).mockResolvedValue({
      contactNumber: "+27 83 234 5678",
      emailAddress: "ada@example.com",
      firstName: "Augusta Ada",
      homeAddress: "20 Club Road, Johannesburg",
      surname: "Lovelace",
    });

    render(<ProfilePage />);
    expect(await screen.findByText("10 Aqua Street, Johannesburg")).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Edit profile" }));
    fireEvent.change(screen.getByLabelText("First name"), { target: { value: "Augusta Ada" } });
    fireEvent.change(screen.getByLabelText("Contact number"), { target: { value: "+27 83 234 5678" } });
    fireEvent.change(screen.getByLabelText("Home address"), { target: { value: "20 Club Road, Johannesburg" } });
    fireEvent.click(screen.getByRole("button", { name: "Save changes" }));

    await waitFor(() => expect(httpClient.put).toHaveBeenCalledWith(
      "/api/services/app/MyAccount/UpdateProfile",
      expect.objectContaining({
        contactNumber: "+27 83 234 5678",
        firstName: "Augusta Ada",
        homeAddress: "20 Club Road, Johannesburg",
      }),
    ));
    expect(setSession).toHaveBeenCalledWith(expect.objectContaining({
      user: expect.objectContaining({ name: "Augusta Ada Lovelace" }),
    }));
  });
});
