import axios from "axios";
import { describe, expect, it, vi } from "vitest";

import {
  claimsToUser,
  getAuthenticationErrorMessage,
  getTenantSelfRegistrationAvailability,
} from "./auth-service";

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
    expect(claimsToUser({ email: "member@example.com", name: "Member", role: "Member", sub: "42", tenantId: "7" }))
      .toMatchObject({ email: "member@example.com", id: 42, role: "Member", tenantId: 7 });
  });

  it("maps the granted permission list included in an administrator token", () => {
    expect(claimsToUser({ permissions: "Aqua.Admin.Users.View,Aqua.Admin.Tenants.View", role: "Admin", sub: "1" })?.permissions)
      .toEqual(["Aqua.Admin.Users.View", "Aqua.Admin.Tenants.View"]);
  });
});

describe("getAuthenticationErrorMessage", () => {
  it("shows the Area problem without exposing the correlation identifier", () => {
    const message = getAuthenticationErrorMessage(500, {
      error: {
        message: "Login failed!",
        details: "CorrelationId: request-17\nThere is no tenant defined with name customer",
      },
    });

    expect(message).toBe(
      "The selected Area “customer” does not exist. Choose the correct Area workspace and try again.",
    );
    expect(message).not.toContain("CorrelationId");
    expect(message).not.toContain("tenant");
  });
});

describe("getTenantSelfRegistrationAvailability", () => {
  it("requests and returns the live setting for the selected Area", async () => {
    const get = vi.spyOn(axios, "get").mockResolvedValueOnce({
      data: { result: { isSelfRegistrationEnabled: true } },
    });

    await expect(getTenantSelfRegistrationAvailability("CapeTown")).resolves.toEqual({
      isSelfRegistrationEnabled: true,
      ok: true,
    });
    expect(get).toHaveBeenCalledWith(
      expect.stringContaining("/Account/GetTenantSelfRegistrationAvailability"),
      { params: { TenancyName: "CapeTown" } },
    );

    get.mockRestore();
  });

  it("fails closed when availability cannot be loaded", async () => {
    const get = vi.spyOn(axios, "get").mockRejectedValueOnce(new Error("offline"));

    await expect(getTenantSelfRegistrationAvailability("CapeTown")).resolves.toEqual({
      ok: false,
    });

    get.mockRestore();
  });
});
