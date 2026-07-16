import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { useAuthActions, useTenantActions, useTenantState, useToast } from "@/src/providers";
import { getLoginDestination } from "@/src/shared/auth/roles";

import { LoginForm } from "./login-form";

const { push } = vi.hoisted(() => ({ push: vi.fn() }));

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
    useTenantActions: vi.fn(),
    useTenantState: vi.fn(),
    useToast: vi.fn(),
  };
});

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push, replace: vi.fn(), prefetch: vi.fn() }),
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
    vi.mocked(useAuthActions).mockReturnValue({
      clearSession: vi.fn(),
      setReady: vi.fn(),
      setSession,
    });
    vi.mocked(useTenantState).mockReturnValue({ currentTenant: null, isHost: true });
    vi.mocked(useTenantActions).mockReturnValue({ clearTenant: vi.fn(), setTenant: vi.fn() });
    vi.mocked(useToast).mockReturnValue({ toast });
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
    expect(push).toHaveBeenCalledWith("/dashboard");
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

    await waitFor(() => expect(push).toHaveBeenCalledWith("/admin/dashboard"));
    expect(login).toHaveBeenCalledWith(
      expect.objectContaining({ email: "admin", password: "123qwe" }),
    );
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
      expect(push).toHaveBeenCalledWith("/area-leader/dashboard"),
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
      expect(push).toHaveBeenCalledWith("/facilitator/dashboard"),
    );
  });
});
