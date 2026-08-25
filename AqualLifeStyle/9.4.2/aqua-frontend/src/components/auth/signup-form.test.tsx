import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { useTenantState, useToast } from "@/src/providers";

import { SignupForm } from "./signup-form";

vi.mock("@/src/shared/api/auth-service", () => ({
  register: vi.fn(),
}));

vi.mock("@/src/providers", async () => {
  const actual = await vi.importActual<typeof import("@/src/providers")>(
    "@/src/providers",
  );
  return {
    ...actual,
    useTenantState: vi.fn(),
    useToast: vi.fn(),
  };
});

const push = vi.fn();
vi.mock("next/navigation", () => ({
  useRouter: () => ({ push, replace: vi.fn(), prefetch: vi.fn() }),
  usePathname: () => "/signup",
  useSearchParams: () => new URLSearchParams(),
}));

describe("SignupForm", () => {
  const toast = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
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
      await screen.findByText("Use at least 8 characters."),
    ).toBeInTheDocument();
  });

  it("preserves safe invitation context when switching to sign in", () => {
    const { rerender } = render(
      <SignupForm
        inviteCode="AQ7G2X9KLMNP"
        redirectPath="/i/AQ7G2X9KLMNP"
        tenancyName="Johannesburg"
      />,
    );

    expect(screen.getByRole("link", { name: "Sign in" })).toHaveAttribute(
      "href",
      "/login?area=Johannesburg&invite=AQ7G2X9KLMNP&redirect=%2Fi%2FAQ7G2X9KLMNP",
    );

    rerender(
      <SignupForm
        inviteCode="AQ7G2X9KLMNP"
        redirectPath="//attacker.example/path"
        tenancyName="Johannesburg"
      />,
    );
    expect(screen.getByRole("link", { name: "Sign in" })).toHaveAttribute(
      "href",
      "/login?area=Johannesburg&invite=AQ7G2X9KLMNP",
    );
  });

  it("advances through the multi-step flow and creates an account", async () => {
    const { register } = await import("@/src/shared/api/auth-service");
    vi.mocked(register).mockResolvedValue({ ok: true });

    render(<SignupForm inviteCode="AQ7G2X9KLMNP" />);

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

    fireEvent.change(screen.getByLabelText("First name"), { target: { value: "Jane" } });
    fireEvent.change(screen.getByLabelText("Surname"), { target: { value: "Doe" } });
    fireEvent.change(screen.getByLabelText("Contact number"), { target: { value: "+27 82 123 4567" } });
    fireEvent.change(screen.getByLabelText("Home address"), { target: { value: "25 Aqua Street, Johannesburg" } });

    fireEvent.click(screen.getByRole("button", { name: "Next" }));

    expect(
      await screen.findByText("Review your details"),
    ).toBeInTheDocument();

    const terms = screen.getByRole("checkbox");
    fireEvent.click(terms);

    fireEvent.click(screen.getByRole("button", { name: "Create account" }));

    await waitFor(() => expect(push).toHaveBeenCalledOnce(), {
      timeout: 2000,
    });
    expect(register).toHaveBeenCalledWith(
      expect.objectContaining({ inviteCode: "AQ7G2X9KLMNP" }),
    );
    expect(push).toHaveBeenCalledWith(
      "/verify-email-sent?area=Default",
    );
    expect(toast).toHaveBeenCalledWith(
      expect.objectContaining({
        title: "Verify your email",
        type: "success",
      }),
    );
  });
});
