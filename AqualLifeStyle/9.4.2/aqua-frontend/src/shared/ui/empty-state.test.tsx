import { Package } from "lucide-react";
import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";

import { EmptyState } from "./empty-state";

describe("EmptyState", () => {
  it("renders title and description", () => {
    render(
      <EmptyState
        description="No products found"
        icon={Package}
        title="No products"
      />,
    );
    expect(screen.getByText("No products")).toBeInTheDocument();
    expect(screen.getByText("No products found")).toBeInTheDocument();
  });

  it("renders action", () => {
    render(<EmptyState action={<button>Add</button>} icon={Package} title="Empty" />);
    expect(screen.getByRole("button")).toBeInTheDocument();
  });
});
