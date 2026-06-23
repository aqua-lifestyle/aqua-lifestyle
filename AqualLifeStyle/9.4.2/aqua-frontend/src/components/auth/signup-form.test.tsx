import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { useAuthActions, useTenantState, useToast } from "@/src/providers";

import { SignupForm } from "./signup-form";

vi.mock("@/src/shared/api/auth-service", () => ({
  login: vi.fn(),
  register: vi.fn(),
}));

vi.mock("@/src/providers", async () => {
  const actual = await vi.importActual<typeof import("@/src/providers")>(
    "@/src/providers",
  );
  return {
    ...actual,
    useAuthActions: vi.fn(),
    useTenantState: vi.fn(),
    useToast: vi.fn(),
  };
});

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: vi.fn(), replace: vi.fn(), prefetch: vi.fn() }),
  usePathname: () => "/signup",
  useSearchParams: () => new URLSearchParams(),
}));

describe("SignupForm", () => {
  const setSession = vi.fn();
  const toast = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(useAuthActions).mockReturnValue({
      clearSession: vi.fn(),
      setReady: vi.fn(),
      setSession,
    });
    vi.mocked(useTenantState).mockReturnValue({ currentTenant: null, isHost: true });
    vi.mocked(useToast).mockReturnValue({ toast });
  });

  it("shows validation errors on the first step", async () => {
    render(<SignupForm />);

    fireEvent.click(screen.getByRole("button", { name: "Next" }));

    expect(
      await screen.findByText("Enter a valid email address."),
    ).toBeInTheDocument();
    expect(
      await screen.findByText("Password must be at least 8 characters."),
    ).toBeInTheDocument();
  });

  it("advances through the multi-step flow and creates an account", async () => {
    const { register, login } = await import("@/src/shared/api/auth-service");
    vi.mocked(register).mockResolvedValue({ ok: true });
    vi.mocked(login).mockResolvedValue({
      ok: true,
      session: {
        accessToken: "real-access-token",
        expiresAt: new Date(Date.now() + 3600000).toISOString(),
        refreshToken: "refresh-token",
        user: {
          id: 1,
          email: "jane@example.com",
          name: "Jane",
          role: "Member",
          permissions: [],
        },
      },
    });

    render(<SignupForm />);

    fireEvent.change(screen.getByLabelText("Email address"), {
      target: { value: "jane@example.com" },
    });
    fireEvent.change(screen.getByLabelText("Password"), {
      target: { value: "StrongPass1!" },
    });
    fireEvent.change(screen.getByLabelText("Confirm password"), {
      target: { value: "StrongPass1!" },
    });

    fireEvent.click(screen.getByRole("button", { name: "Next" }));

    expect(
      await screen.findByText("Personal info"),
    ).toBeInTheDocument();

    fireEvent.change(screen.getByLabelText("Full name"), {
      target: { value: "Jane Doe" },
    });

    fireEvent.click(screen.getByRole("button", { name: "Next" }));

    expect(
      await screen.findByText("Review your details"),
    ).toBeInTheDocument();

    const terms = screen.getByRole("checkbox");
    fireEvent.click(terms);

    fireEvent.click(screen.getByRole("button", { name: "Create account" }));

    await waitFor(() => expect(setSession).toHaveBeenCalledOnce(), {
      timeout: 2000,
    });

    expect(setSession).toHaveBeenCalledWith(
      expect.objectContaining({
        accessToken: "real-access-token",
        user: expect.objectContaining({
          email: "jane@example.com",
          name: "Jane",
        }),
      }),
    );
    expect(toast).toHaveBeenCalledWith(
      expect.objectContaining({
        title: "Welcome",
        type: "success",
      }),
    );
  });
});
