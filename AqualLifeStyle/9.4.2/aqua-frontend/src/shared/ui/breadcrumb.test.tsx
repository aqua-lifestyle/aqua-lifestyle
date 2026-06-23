import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";

import { Breadcrumb } from "./breadcrumb";

describe("Breadcrumb", () => {
  it("renders links for non-last items", () => {
    render(
      <Breadcrumb
        items={[
          { href: "/", label: "Home" },
          { href: "/customers", label: "Customers" },
          { label: "Detail" },
        ]}
      />,
    );
    expect(screen.getByRole("link", { name: "Home" })).toHaveAttribute("href", "/");
    expect(screen.getByRole("link", { name: "Customers" })).toHaveAttribute(
      "href",
      "/customers",
    );
    expect(screen.getByText("Detail")).toBeInTheDocument();
  });
});
