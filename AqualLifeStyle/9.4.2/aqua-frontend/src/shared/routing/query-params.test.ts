import { describe, expect, it } from "vitest";

import { toPositiveNumberOrNull } from "./query-params";

describe("toPositiveNumberOrNull", () => {
  it("returns a positive integer from a query value", () => {
    expect(toPositiveNumberOrNull("42")).toBe(42);
  });

  it("rejects missing, non-numeric, zero, negative, and decimal values", () => {
    expect(toPositiveNumberOrNull(undefined)).toBeNull();
    expect(toPositiveNumberOrNull("abc")).toBeNull();
    expect(toPositiveNumberOrNull("0")).toBeNull();
    expect(toPositiveNumberOrNull("-1")).toBeNull();
    expect(toPositiveNumberOrNull("1.5")).toBeNull();
  });
});
