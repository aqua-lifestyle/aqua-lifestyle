import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";

import { Avatar } from "./avatar";

describe("Avatar", () => {
  it("renders initials from fallback name", () => {
    render(<Avatar fallback="Jane Doe" />);
    expect(screen.getByText("JD")).toBeInTheDocument();
  });

  it("renders an image when imageUrl is provided", () => {
    const { container } = render(
      <Avatar fallback="Jane Doe" imageUrl="https://example.com/avatar.png" />,
    );
    const image = container.querySelector("img");
    expect(image).toHaveAttribute("src", "https://example.com/avatar.png");
  });
});
