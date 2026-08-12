import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { useAuthActions, useTenantActions, useTenantState, useToast } from "@/src/providers";
import { getTenantSelfRegistrationAvailability, login } from "@/src/shared/api/auth-service";
import { getLoginDestination } from "@/src/shared/auth/roles";

import { LoginForm } from "./login-form";

const { push, replace, searchParams } = vi.hoisted(() => ({
  push: vi.fn(),
  replace: vi.fn(),
  searchParams: { current: new URLSearchParams() },
}));

vi.mock("@/src/shared/api/auth-service", () => ({
  getTenantSelfRegistrationAvailability: vi.fn(),
  login: vi.fn(),
}));

vi.mock("@/src/providers", async () => {
  const actual = await vi.importActual<typeof import("@/src/providers")>(
    "@/src/providers",
  );
  return {
    ...actual,
    useAuthActions: vi.fn(),
    useTenantActions: vi.fn(),
    useTenantState: vi.fn(),
    useToast: vi.fn(),
  };
});

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push, replace, prefetch: vi.fn() }),
  usePathname: () => "/login",
  useSearchParams: () => searchParams.current,
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
    searchParams.current = new URLSearchParams();
    vi.mocked(getTenantSelfRegistrationAvailability).mockResolvedValue({
      isSelfRegistrationEnabled: false,
      ok: true,
    });
    vi.mocked(useAuthActions).mockReturnValue({
      clearSession: vi.fn(),
      setReady: vi.fn(),
      setSession,
    });
    vi.mocked(useTenantState).mockReturnValue({ currentTenant: null, isHost: true });
    vi.mocked(useTenantActions).mockReturnValue({ clearTenant: vi.fn(), setTenant: vi.fn() });
    vi.mocked(useToast).mockReturnValue({ toast });
  });

  it("explains when access changes require a fresh sign-in", async () => {
    searchParams.current = new URLSearchParams("reason=session-ended");

    render(<LoginForm />);

    expect(
      screen.getByText(/secure session ended because your access changed or expired/i),
    ).toBeInTheDocument();
    await waitFor(() =>
      expect(getTenantSelfRegistrationAvailability).toHaveBeenCalled(),
    );
  });

  it("does not describe a temporary refresh problem as an ended session", async () => {
    searchParams.current = new URLSearchParams("reason=refresh-temporary");

    render(<LoginForm />);

    expect(
      screen.queryByText(/secure session ended because your access changed or expired/i),
    ).not.toBeInTheDocument();
    await waitFor(() =>
      expect(getTenantSelfRegistrationAvailability).toHaveBeenCalled(),
    );
  });

  it("does not let a generic redirect override a role dashboard", () => {
    expect(getLoginDestination("AreaLeader", "/")).toBe(
      "/area-leader/dashboard",
    );
    expect(getLoginDestination("AreaLeader", "/dashboard")).toBe(
      "/area-leader/dashboard",
    );
    expect(getLoginDestination("AreaLeader", "/area-leader/orders")).toBe(
      "/area-leader/orders",
    );
    expect(getLoginDestination("Facilitator", "/")).toBe(
      "/facilitator/dashboard",
    );
    expect(getLoginDestination("Facilitator", "/facilitator/my-referrals")).toBe(
      "/facilitator/my-referrals",
    );
  });

  it("shows validation errors for empty fields", async () => {
    render(<LoginForm />);

    submitForm();

    expect(
      await screen.findByText("Enter your username or email address."),
    ).toBeInTheDocument();
    expect(
      await screen.findByText("Password is required."),
    ).toBeInTheDocument();
  });

  it("does not advertise public registration when the Area has disabled it", async () => {
    render(<LoginForm />);

    await waitFor(() =>
      expect(getTenantSelfRegistrationAvailability).toHaveBeenCalledWith("Default"),
    );
    expect(screen.queryByRole("link", { name: "Sign up" })).not.toBeInTheDocument();
  });

  it("advertises public registration when the selected Area enables it", async () => {
    vi.mocked(getTenantSelfRegistrationAvailability).mockResolvedValue({
      isSelfRegistrationEnabled: true,
      ok: true,
    });

    render(<LoginForm />);

    expect(await screen.findByRole("link", { name: "Sign up" })).toHaveAttribute(
      "href",
      "/signup?area=Default",
    );
  });

  it("provides a verification recovery path for unconfirmed accounts", async () => {
    render(<LoginForm />);

    expect(
      await screen.findByRole("link", { name: "Resend verification email" }),
    ).toHaveAttribute("href", "/verify-email-sent?area=Default");
    expect(screen.getByRole("link", { name: "Forgot your password?" }))
      .toHaveAttribute("href", "/forgot-password?area=Default");
  });

  it("does not offer Area verification for platform administration", async () => {
    render(<LoginForm />);

    fireEvent.change(screen.getByLabelText("Workspace"), {
      target: { value: "" },
    });

    expect(
      screen.queryByRole("link", { name: "Resend verification email" }),
    ).not.toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Forgot your password?" }))
      .toHaveAttribute("href", "/forgot-password");
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

    fireEvent.change(screen.getByLabelText("Username or email"), {
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
    expect(replace).toHaveBeenCalledWith("/dashboard");
  });

  it("uses the Area carried by an account email link", async () => {
    searchParams.current = new URLSearchParams("area=Johannesburg");
    vi.mocked(login).mockResolvedValue({
      ok: false,
      message: "Sign in rejected for test.",
    });

    render(<LoginForm />);
    fireEvent.change(screen.getByLabelText("Username or email"), {
      target: { value: "user@example.com" },
    });
    fireEvent.change(screen.getByLabelText("Password"), {
      target: { value: "password123" },
    });
    submitForm();

    await waitFor(() => expect(login).toHaveBeenCalledWith(
      expect.objectContaining({ tenant: "Johannesburg" }),
    ));
  });

  it("accepts the seeded admin username and opens the admin dashboard", async () => {
    const { login } = await import("@/src/shared/api/auth-service");
    vi.mocked(login).mockResolvedValue({
      ok: true,
      session: {
        accessToken: "admin-access-token",
        expiresAt: new Date(Date.now() + 3600000).toISOString(),
        user: {
          id: 1,
          email: "admin@defaulttenant.com",
          name: "admin",
          role: "SystemAdmin",
          permissions: [],
        },
      },
    });

    render(<LoginForm />);

    fireEvent.change(screen.getByLabelText("Username or email"), {
      target: { value: "admin" },
    });
    fireEvent.change(screen.getByLabelText("Password"), {
      target: { value: "123qwe" },
    });
    submitForm();

    await waitFor(() => expect(replace).toHaveBeenCalledWith("/admin/dashboard"));
    expect(login).toHaveBeenCalledWith(
      expect.objectContaining({ email: "admin", password: "123qwe" }),
    );
  });

  it("does not treat a legacy customer account type as an Area name", async () => {
    const { login } = await import("@/src/shared/api/auth-service");
    vi.mocked(useTenantState).mockReturnValue({ currentTenant: "customer", isHost: false });
    vi.mocked(login).mockResolvedValue({
      ok: false,
      message: "The username, password, or Area workspace is incorrect.",
    });

    render(<LoginForm />);
    fireEvent.change(screen.getByLabelText("Username or email"), {
      target: { value: "new.customer@example.com" },
    });
    fireEvent.change(screen.getByLabelText("Password"), {
      target: { value: "Temporary123!" },
    });
    submitForm();

    await waitFor(() => expect(login).toHaveBeenCalledWith(
      expect.objectContaining({ tenant: "Default" }),
    ));
    expect(
      await screen.findByText("The username, password, or Area workspace is incorrect."),
    ).toBeInTheDocument();
  });

  it("opens the Area Leader dashboard after an Area Leader signs in", async () => {
    const { login } = await import("@/src/shared/api/auth-service");
    vi.mocked(login).mockResolvedValue({
      ok: true,
      session: {
        accessToken: "area-leader-access-token",
        expiresAt: new Date(Date.now() + 3600000).toISOString(),
        user: {
          id: 24,
          email: "area.leader.demo@aqualifestyle.local",
          name: "area.leader.demo",
          role: "AreaLeader",
          permissions: [],
        },
      },
    });

    render(<LoginForm />);

    fireEvent.change(screen.getByLabelText("Username or email"), {
      target: { value: "area.leader.demo" },
    });
    fireEvent.change(screen.getByLabelText("Password"), {
      target: { value: "AreaLeader123!" },
    });
    submitForm();

    await waitFor(() =>
      expect(replace).toHaveBeenCalledWith("/area-leader/dashboard"),
    );
  });

  it("opens the Facilitator dashboard after a Facilitator signs in", async () => {
    const { login } = await import("@/src/shared/api/auth-service");
    vi.mocked(login).mockResolvedValue({
      ok: true,
      session: {
        accessToken: "facilitator-access-token",
        expiresAt: new Date(Date.now() + 3600000).toISOString(),
        user: {
          id: 25,
          email: "facilitator.demo@aqualifestyle.local",
          name: "facilitator.demo",
          role: "Facilitator",
          permissions: [],
        },
      },
    });

    render(<LoginForm />);

    fireEvent.change(screen.getByLabelText("Username or email"), {
      target: { value: "facilitator.demo" },
    });
    fireEvent.change(screen.getByLabelText("Password"), {
      target: { value: "Facilitator123!" },
    });
    submitForm();

    await waitFor(() =>
      expect(replace).toHaveBeenCalledWith("/facilitator/dashboard"),
    );
  });
});
