import { describe, expect, it } from "vitest";

import { cn } from "./utils";

describe("cn", () => {
  it("merges class names", () => {
    expect(cn("a", "b")).toBe("a b");
  });

  it("applies conditional classes", () => {
    expect(cn("a", false && "b", "c")).toBe("a c");
  });

  it("merges tailwind classes with tailwind-merge", () => {
    expect(cn("px-2 py-1", "px-4")).toBe("py-1 px-4");
  });
});
