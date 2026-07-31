import { describe, expect, it } from "vitest";

import manifest from "./manifest";

describe("manifest", () => {
  it("provides standard installable application icon sizes", () => {
    expect(manifest().icons).toEqual(
      expect.arrayContaining([
        expect.objectContaining({ sizes: "192x192", src: "/icon1.png" }),
        expect.objectContaining({ sizes: "512x512", src: "/icon.png" }),
      ]),
    );
  });
});
