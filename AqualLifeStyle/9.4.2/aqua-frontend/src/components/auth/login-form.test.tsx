import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { useAuthActions, useTenantState, useToast } from "@/src/providers";

import { LoginForm } from "./login-form";

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

  it("calls setSession with a demo token after successful submit", async () => {
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
        accessToken: "demo-access-token",
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
