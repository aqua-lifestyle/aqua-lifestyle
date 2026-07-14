import { describe, expect, it } from "vitest";

import { claimsToUser } from "./auth-service";

describe("claimsToUser", () => {
  it("maps ABP claim URIs and treats the built-in Admin role as SystemAdmin", () => {
    expect(claimsToUser({
      "http://schemas.microsoft.com/ws/2008/06/identity/claims/role": "Admin",
      "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress":
        "admin@defaulttenant.com",
      "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name": "admin",
      "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier": "1",
    })).toEqual({
      email: "admin@defaulttenant.com",
      id: 1,
      name: "admin",
      permissions: [],
      role: "SystemAdmin",
    });
  });

  it("continues to map standard JWT claims", () => {
    expect(claimsToUser({ email: "member@example.com", name: "Member", role: "Member", sub: "42" }))
      .toMatchObject({ email: "member@example.com", id: 42, role: "Member" });
  });
});
