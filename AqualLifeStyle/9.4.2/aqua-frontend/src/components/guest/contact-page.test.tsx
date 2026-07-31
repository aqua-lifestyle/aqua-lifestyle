import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";

import { ContactPage } from "./contact-page";

describe("ContactPage", () => {
  it("offers only supported help paths", () => {
    render(<ContactPage />);

    expect(
      screen.getByRole("heading", { name: "Find the right place to continue" }),
    ).toBeInTheDocument();
    expect(screen.getByRole("link", { name: /Browse the catalog/i })).toHaveAttribute(
      "href",
      "/catalog",
    );
    expect(screen.getByRole("link", { name: /Create an account/i })).toHaveAttribute(
      "href",
      "/signup",
    );
    expect(screen.getByRole("link", { name: /Sign in/i })).toHaveAttribute(
      "href",
      "/login",
    );
  });

  it("does not show unverified contact details or a fake submission form", () => {
    render(<ContactPage />);

    expect(screen.queryByText("support@aqualifestyle.com")).not.toBeInTheDocument();
    expect(screen.queryByText("+1 (555) 123-4567")).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /Send message/i })).not.toBeInTheDocument();
    expect(screen.getByText(/does not currently provide a public contact-message service/i)).toBeInTheDocument();
  });
});
