import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";

import { QuickActions } from "./quick-actions";

describe("QuickActions", () => {
  it("renders all quick action links", () => {
    render(<QuickActions />);

    expect(screen.getByRole("link", { name: /Add customer/i })).toHaveAttribute(
      "href",
      "/customers/register",
    );
    expect(screen.getByRole("link", { name: /Create enquiry/i })).toHaveAttribute(
      "href",
      "/enquiries/create",
    );
    expect(screen.getByRole("link", { name: /View memberships/i })).toHaveAttribute(
      "href",
      "/memberships",
    );
    expect(screen.getByRole("link", { name: /Order intents/i })).toHaveAttribute(
      "href",
      "/order-intents",
    );
  });
});
