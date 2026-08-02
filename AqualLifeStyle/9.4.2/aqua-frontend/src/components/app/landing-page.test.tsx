import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";

import { AuthProvider } from "@/src/providers";

import { LandingPage } from "./landing-page";

describe("LandingPage", () => {
  it("guides visitors from public discovery to account actions", () => {
    render(
      <AuthProvider>
        <LandingPage />
      </AuthProvider>,
    );

    expect(
      screen.getByRole("heading", { level: 1, name: "Live in health. Inspire to wealth." }),
    ).toBeInTheDocument();
    expect(
      screen.getByText(/combines aQuathz wellbeing products, membership/),
    ).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "What Aqua offers" })).toBeInTheDocument();
    expect(screen.getByRole("main")).toHaveAttribute("id", "main-content");
    expect(screen.getByRole("main")).toHaveAttribute("tabindex", "-1");
    expect(screen.getAllByRole("link", { name: /Browse products/i })[0]).toHaveAttribute(
      "href",
      "/catalog",
    );
    expect(screen.getByRole("heading", { name: "Understand Aqua before choosing a path." })).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "From public browsing to account access." })).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "A club organised through local Areas." })).toBeInTheDocument();
    expect(screen.getByRole("img", { name: "Aqua Lifestyle Club" })).toHaveAttribute(
      "src",
      expect.stringContaining("aqua-lifestyle-logo.jpg"),
    );
    expect(screen.getByRole("link", { name: /Water products/ })).toHaveAttribute(
      "href",
      "/catalog",
    );
    expect(screen.getByRole("link", { name: /Member access/i })).toHaveAttribute(
      "href",
      "/login",
    );
  });

  it("uses native disclosure controls for frequently asked questions", () => {
    render(
      <AuthProvider>
        <LandingPage />
      </AuthProvider>,
    );

    expect(screen.getByText("What is Aqua Lifestyle Club?").closest("details")).toBeTruthy();
    expect(screen.getByText("Do I need an account to browse products?").closest("details")).toBeTruthy();
  });
});
