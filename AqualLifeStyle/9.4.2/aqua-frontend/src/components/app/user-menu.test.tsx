import { fireEvent, render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import { AuthProvider, useAuthActions } from "@/src/providers";

import { UserMenu } from "./user-menu";

const replace = vi.fn();

vi.mock("next/navigation", () => ({
  useRouter: () => ({ replace }),
}));

const SetSession = () => {
  const { setSession } = useAuthActions();

  return (
    <button
      onClick={() =>
        setSession({
          accessToken: "token",
          expiresAt: "2026-01-01T00:00:00Z",
          user: {
            email: "jane@example.com",
            id: 1,
            name: "Jane Doe",
            permissions: [],
            role: "Member",
          },
        })
      }
    >
      Sign in
    </button>
  );
};

describe("UserMenu", () => {
  beforeEach(() => vi.resetAllMocks());

  it("renders sign-in and sign-up links when unauthenticated", () => {
    render(
      <AuthProvider>
        <UserMenu />
      </AuthProvider>,
    );

    expect(screen.getByRole("link", { name: "Sign in" })).toHaveAttribute("href", "/login");
    expect(screen.getByRole("link", { name: "Create account" })).toHaveAttribute("href", "/signup");
  });

  it("renders the user name and a sign-out button when authenticated", () => {
    render(
      <AuthProvider>
        <SetSession />
        <UserMenu />
      </AuthProvider>,
    );

    fireEvent.click(screen.getByRole("button", { name: "Sign in" }));

    expect(screen.getByText("Jane Doe")).toBeInTheDocument();
    expect(screen.getByText("jane@example.com")).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Open user menu" }));
    expect(screen.getByRole("button", { name: "Sign out" })).toBeInTheDocument();
  });

  it("signs out when the sign-out button is clicked", () => {
    render(
      <AuthProvider>
        <SetSession />
        <UserMenu />
      </AuthProvider>,
    );

    fireEvent.click(screen.getByRole("button", { name: "Sign in" }));
    fireEvent.click(screen.getByRole("button", { name: "Open user menu" }));
    fireEvent.click(screen.getByRole("button", { name: "Sign out" }));

    expect(screen.getByRole("link", { name: "Sign in" })).toBeInTheDocument();
    expect(replace).toHaveBeenCalledWith("/login");
  });

  it("closes the user menu with Escape", () => {
    render(
      <AuthProvider>
        <SetSession />
        <UserMenu />
      </AuthProvider>,
    );

    fireEvent.click(screen.getByRole("button", { name: "Sign in" }));
    const trigger = screen.getByRole("button", { name: "Open user menu" });
    fireEvent.click(trigger);
    expect(trigger).toHaveAttribute("aria-expanded", "true");

    fireEvent.keyDown(document, { key: "Escape" });
    expect(trigger).toHaveAttribute("aria-expanded", "false");
    expect(trigger).toHaveFocus();
    expect(screen.queryByLabelText("User menu")).not.toBeInTheDocument();
  });
});
