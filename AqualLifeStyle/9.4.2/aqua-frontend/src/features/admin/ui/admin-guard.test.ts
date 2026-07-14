import { describe, expect, it } from "vitest";

import { isSystemAdmin } from "./admin-guard";

describe("isSystemAdmin", () => {
  it("accepts the ABP SystemAdmin role without case sensitivity", () => {
    expect(isSystemAdmin("SystemAdmin")).toBe(true);
    expect(isSystemAdmin("system_admin")).toBe(true);
  });

  it("rejects non-admin and missing roles", () => {
    expect(isSystemAdmin("Member")).toBe(false);
    expect(isSystemAdmin(null)).toBe(false);
  });
});
