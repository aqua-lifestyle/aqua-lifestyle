import { render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";

// Next.js Image component optimizes URLs, so mock it to render a plain img
vi.mock("next/image", () => ({
  __esModule: true,
  default: ({ ...props }: React.ComponentProps<"img">) => (
    // eslint-disable-next-line @next/next/no-img-element
    <img {...props} alt={props.alt ?? ""} />
  ),
}));

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
