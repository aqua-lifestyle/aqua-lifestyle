import { describe, expect, it } from "vitest";

import { getOrderStatusLabel, getOrderStatusTone } from "./order-status";

describe("order status presentation", () => {
  it("matches the documented order-intent lifecycle", () => {
    expect([0, 1, 2, 3].map(getOrderStatusLabel)).toEqual([
      "Draft",
      "Reserved",
      "Cancelled",
      "Completed",
    ]);
  });

  it("uses success and error tones only for terminal outcomes", () => {
    expect(getOrderStatusTone(0)).toBe("neutral");
    expect(getOrderStatusTone(1)).toBe("info");
    expect(getOrderStatusTone(2)).toBe("error");
    expect(getOrderStatusTone(3)).toBe("success");
  });
});
