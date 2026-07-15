import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";

import {
  AuthProvider,
  useAuthActions,
} from "@/src/providers";
import { Navbar } from "./navbar";

vi.mock("next/navigation", () => ({
  usePathname: () => "/customers",
}));

vi.mock("./tenant-switcher", () => ({
  TenantSwitcher: () => <div data-testid="tenant-switcher" />,
}));

vi.mock("./user-menu", () => ({
  UserMenu: () => <div data-testid="user-menu" />,
}));

const SetSession = () => {
  const { setSession } = useAuthActions();

  return (
    <button
      onClick={() =>
        setSession({
          accessToken: "access-token",
          expiresAt: "2026-01-01T00:00:00Z",
          user: {
            id: 1,
            email: "user@example.com",
            name: "Demo User",
            role: "SystemAdmin",
            permissions: [
              "Aqua.Members.View",
              "Aqua.Members.Create",
              "Aqua.Members.Edit",
              "Aqua.Members.Delete",
              "Aqua.Admin.Customers.Import",
              "Pages.Customers",
              "Pages.Products",
              "Pages.Enquiries",
              "Pages.Memberships",
              "Pages.Orders",
            ],
          },
        })
      }
    >
      Set session
    </button>
  );
};

describe("Navbar", () => {
  it("renders the brand and main navigation links", async () => {
    render(
      <AuthProvider>
        <SetSession />
        <Navbar />
      </AuthProvider>,
    );

    fireEvent.click(screen.getByRole("button", { name: "Set session" }));

    await waitFor(() => {
      expect(screen.getByRole("link", { name: "Dashboard" })).toBeTruthy();
    });

    expect(screen.getByRole("link", { name: /Aqua Lifestyle/i })).toHaveAttribute("href", "/");
    expect(screen.getByRole("link", { name: "Dashboard" })).toHaveAttribute("href", "/dashboard");
    expect(screen.getByRole("link", { name: "Customers" })).toHaveAttribute("href", "/customers");
    expect(screen.getByRole("link", { name: "Admin customers" })).toHaveAttribute("href", "/admin/customers");
    expect(screen.getByRole("link", { name: "Products" })).toHaveAttribute("href", "/products");
    expect(screen.getByRole("link", { name: "Enquiries" })).toHaveAttribute("href", "/enquiries");
  });

  it("highlights the active link", async () => {
    render(
      <AuthProvider>
        <SetSession />
        <Navbar />
      </AuthProvider>,
    );

    fireEvent.click(screen.getByRole("button", { name: "Set session" }));

    await waitFor(() => {
      expect(screen.getByRole("link", { name: "Customers" })).toBeTruthy();
    });

    const customersLink = screen.getByRole("link", { name: "Customers" });
    expect(customersLink).toHaveClass("text-accent");
  });

  it("renders the tenant switcher and user menu", async () => {
    render(
      <AuthProvider>
        <SetSession />
        <Navbar />
      </AuthProvider>,
    );

    fireEvent.click(screen.getByRole("button", { name: "Set session" }));

    await waitFor(() => {
      expect(screen.getByTestId("tenant-switcher")).toBeInTheDocument();
    });

    expect(screen.getByTestId("user-menu")).toBeInTheDocument();
  });
});
