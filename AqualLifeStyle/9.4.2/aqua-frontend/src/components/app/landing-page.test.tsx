import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";

import { AuthProvider } from "@/src/providers";

import { LandingPage } from "./landing-page";

describe("LandingPage", () => {
  it("explains the club before presenting account actions", () => {
    render(
      <AuthProvider>
        <LandingPage />
      </AuthProvider>,
    );

    expect(
      screen.getByRole("heading", { level: 1, name: "Live well. Grow together." }),
    ).toBeInTheDocument();
    expect(screen.getByRole("link", { name: /Explore the club/i })).toHaveAttribute(
      "href",
      "#value",
    );
    expect(screen.getByRole("heading", { name: "More than a membership account" })).toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "A clear path from discovery to participation" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: /Create an account/i })).toHaveAttribute(
      "href",
      "/signup",
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
