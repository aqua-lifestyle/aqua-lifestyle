import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { useAuthActions, useTenantState, useToast } from "@/src/providers";

import { LoginForm } from "./login-form";

vi.mock("@/src/shared/api/auth-service", () => ({
  login: vi.fn(),
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
  usePathname: () => "/login",
  useSearchParams: () => new URLSearchParams(),
}));

const submitForm = () => {
  const button = screen.getByRole("button", { name: "Sign in" });
  const form = button.closest("form");
  expect(form).toBeTruthy();
  fireEvent.submit(form!);
};

describe("LoginForm", () => {
  const setSession = vi.fn();
  const toast = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(useAuthActions).mockReturnValue({ setSession, clearSession: vi.fn() });
    vi.mocked(useTenantState).mockReturnValue({ currentTenant: null, isHost: true });
    vi.mocked(useToast).mockReturnValue({ toast });
  });

  it("shows validation errors for empty fields", async () => {
    render(<LoginForm />);

    submitForm();

    expect(
      await screen.findByText("Enter a valid email address."),
    ).toBeInTheDocument();
    expect(
      await screen.findByText("Password is required."),
    ).toBeInTheDocument();
  });

  it("calls login from auth-service and sets session after successful submit", async () => {
    const { login } = await import("@/src/shared/api/auth-service");
    vi.mocked(login).mockResolvedValue({
      ok: true,
      session: {
        accessToken: "real-access-token",
        expiresAt: new Date(Date.now() + 3600000).toISOString(),
        refreshToken: "refresh-token",
        user: {
          id: 1,
          email: "user@example.com",
          name: "user",
          role: "Member",
          permissions: [],
        },
      },
    });

    render(<LoginForm />);

    fireEvent.change(screen.getByLabelText("Email address"), {
      target: { value: "user@example.com" },
    });
    fireEvent.change(screen.getByLabelText("Password"), {
      target: { value: "password123" },
    });
    submitForm();

    await waitFor(() => expect(setSession).toHaveBeenCalledOnce(), {
      timeout: 2000,
    });

    expect(setSession).toHaveBeenCalledWith(
      expect.objectContaining({
        accessToken: "real-access-token",
        user: expect.objectContaining({
          email: "user@example.com",
          name: "user",
        }),
      }),
    );
    expect(toast).toHaveBeenCalledWith(
      expect.objectContaining({
        title: "Welcome back",
        type: "success",
      }),
    );
  });
});
