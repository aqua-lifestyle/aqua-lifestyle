import { describe, expect, it } from "vitest";

import {
  getLoginDestination,
  getRoleHome,
  isAreaLeader,
  isFacilitator,
  isSystemAdmin,
} from "./roles";

describe("role routing", () => {
  it("normalizes supported business roles", () => {
    expect(isSystemAdmin("system_admin")).toBe(true);
    expect(isAreaLeader("area-leader")).toBe(true);
    expect(isFacilitator("Facilitator")).toBe(true);
    expect(isFacilitator("Member")).toBe(false);
  });

  it("provides one canonical home for each role", () => {
    expect(getRoleHome("SystemAdmin").href).toBe("/admin/dashboard");
    expect(getRoleHome("AreaLeader").href).toBe("/area-leader/dashboard");
    expect(getRoleHome("Facilitator").href).toBe("/facilitator/dashboard");
    expect(getRoleHome("Member").href).toBe("/dashboard");
  });

  it("accepts only safe redirects within a privileged role area", () => {
    expect(getLoginDestination("AreaLeader", "/area-leader/orders")).toBe(
      "/area-leader/orders",
    );
    expect(getLoginDestination("AreaLeader", "/administrator")).toBe(
      "/area-leader/dashboard",
    );
    expect(getLoginDestination("Facilitator", "//example.com")).toBe(
      "/facilitator/dashboard",
    );
    expect(getLoginDestination("Member", "/admin/dashboard")).toBe(
      "/dashboard",
    );
    expect(getLoginDestination("Member", "/profile")).toBe("/profile");
  });
});
