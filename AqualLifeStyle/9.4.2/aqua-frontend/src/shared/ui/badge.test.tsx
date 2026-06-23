import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";

import { Badge } from "./badge";

describe("Badge", () => {
  it("renders children", () => {
    render(<Badge tone="success">Active</Badge>);
    expect(screen.getByText("Active")).toBeInTheDocument();
  });

  it("applies tone classes", () => {
    const { container } = render(<Badge tone="success">Active</Badge>);
    expect(container.firstChild).toHaveClass("bg-success/10");
  });
});
