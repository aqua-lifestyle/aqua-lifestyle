import { render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";

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

describe("Navbar", () => {
  it("renders the brand and main navigation links", () => {
    render(<Navbar />);

    expect(screen.getByRole("link", { name: /Aqua Lifestyle/i })).toHaveAttribute("href", "/");
    expect(screen.getByRole("link", { name: "Dashboard" })).toHaveAttribute("href", "/");
    expect(screen.getByRole("link", { name: "Customers" })).toHaveAttribute("href", "/customers");
    expect(screen.getByRole("link", { name: "Products" })).toHaveAttribute("href", "/products");
    expect(screen.getByRole("link", { name: "Enquiries" })).toHaveAttribute("href", "/enquiries");
  });

  it("highlights the active link", () => {
    render(<Navbar />);

    const customersLink = screen.getByRole("link", { name: "Customers" });
    expect(customersLink).toHaveClass("text-accent");
  });

  it("renders the tenant switcher and user menu", () => {
    render(<Navbar />);

    expect(screen.getByTestId("tenant-switcher")).toBeInTheDocument();
    expect(screen.getByTestId("user-menu")).toBeInTheDocument();
  });
});
