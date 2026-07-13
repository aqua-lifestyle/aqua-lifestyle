import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";

import { AuthProvider, useAuthActions } from "@/src/providers";

import { UserMenu } from "./user-menu";

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
  it("renders sign-in and sign-up links when unauthenticated", () => {
    render(
      <AuthProvider>
        <UserMenu />
      </AuthProvider>,
    );

    expect(screen.getByRole("link", { name: "Sign in" })).toHaveAttribute("href", "/login");
    expect(screen.getByRole("link", { name: "Sign up" })).toHaveAttribute("href", "/signup");
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
    fireEvent.click(screen.getByRole("button", { name: "Sign out" }));

    expect(screen.getByRole("link", { name: "Sign in" })).toBeInTheDocument();
  });
});
