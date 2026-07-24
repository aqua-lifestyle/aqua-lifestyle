import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import {
  AuthProvider,
  useAuthActions,
} from "@/src/providers";
import { Navbar } from "./navbar";

const mockPathname = vi.fn(() => "/customers");

vi.mock("next/navigation", () => ({
  usePathname: () => mockPathname(),
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
              "Aqua.Admin.Customers.View",
              "Aqua.Savings.ViewSelf",
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
  beforeEach(() => mockPathname.mockReturnValue("/customers"));

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
    fireEvent.click(screen.getByRole("button", { name: "More" }));
    expect(screen.getByRole("link", { name: "My savings" })).toHaveAttribute(
      "href",
      "/member/savings",
    );
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

  it("uses a utility header instead of duplicate navigation in administration", () => {
    mockPathname.mockReturnValue("/admin/customers");

    render(
      <AuthProvider>
        <Navbar />
      </AuthProvider>,
    );

    expect(screen.getByRole("link", { name: /Aqua Lifestyle Administration/i })).toHaveAttribute("href", "/admin/dashboard");
    expect(screen.getByTestId("user-menu")).toBeInTheDocument();
    expect(screen.queryByRole("link", { name: "Customers" })).not.toBeInTheDocument();
    expect(screen.queryByTestId("tenant-switcher")).not.toBeInTheDocument();
  });
});
