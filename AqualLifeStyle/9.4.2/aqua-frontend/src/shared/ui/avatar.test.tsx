import { render, screen } from "@testing-library/react";
import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it, vi } from "vitest";

// Next.js Image component optimizes URLs, so mock it to render a plain img.
vi.mock("next/image", () => ({
  __esModule: true,
  default: ({ ...props }: React.ComponentProps<"img">) => (
    // eslint-disable-next-line @next/next/no-img-element
    <img {...props} alt={props.alt ?? ""} />
  ),
}));

import { Avatar } from "./avatar";

const rgb = (hue: number, saturation: number, lightness: number) => {
  const s = saturation / 100;
  const l = lightness / 100;
  const a = s * Math.min(l, 1 - l);
  return [0, 8, 4].map(n => {
    const k = (n + hue / 30) % 12;
    return l - a * Math.max(-1, Math.min(k - 3, 9 - k, 1));
  });
};
const whiteContrast = (color: number[]) => {
  const linear = color.map(v => v <= 0.04045 ? v / 12.92 : ((v + 0.055) / 1.055) ** 2.4);
  return 1.05 / (linear[0] * 0.2126 + linear[1] * 0.7152 + linear[2] * 0.0722 + 0.05);
};

describe("Avatar fallback", () => {
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

  it("keeps white initials readable across every generated hue and gradient stop", () => {
    const seen = new Set<number>();
    for (let i = 0; i < 10000 && seen.size < 360; i++) {
      const html = renderToStaticMarkup(<Avatar fallback={`Synthetic member ${i}`} />);
      const stops = [...html.matchAll(/hsl\((\d+) ([\d.]+)% ([\d.]+)%\)/g)];
      expect(stops).toHaveLength(2);
      const hue = Number(stops[0][1]);
      if (seen.has(hue)) continue;
      seen.add(hue);
      const start = rgb(hue, Number(stops[0][2]), Number(stops[0][3]));
      const end = rgb(hue, Number(stops[1][2]), Number(stops[1][3]));
      for (let t = 0; t <= 20; t++) {
        expect(whiteContrast(start.map((v, channel) => v + (end[channel] - v) * t / 20))).toBeGreaterThanOrEqual(4.5);
      }
    }
    expect(seen.size).toBe(360);
  });
});
