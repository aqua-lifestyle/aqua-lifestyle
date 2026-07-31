import { describe, expect, it } from "vitest";

import { getDataScopeKey, getProviderScope } from "./providers";

describe("provider data scope", () => {
  it("changes when the signed-in identity changes within the same Area", () => {
    expect(getDataScopeKey("default", 41)).not.toBe(
      getDataScopeKey("default", 42),
    );
    expect(getDataScopeKey("default", 41)).not.toBe(
      getDataScopeKey("default", undefined),
    );
  });

  it("changes when the Area changes for the same identity", () => {
    expect(getDataScopeKey("johannesburg", 41)).not.toBe(
      getDataScopeKey("durban", 41),
    );
  });
});

describe("getProviderScope", () => {
  it.each([
    "/",
    "/contact",
    "/forgot-password",
    "/login",
    "/signup",
    "/verify-email",
    "/verify-email-sent",
    "/reset-password",
    "/reset-password?token=example",
    "/i/invitation-code",
  ])("uses only shell providers for %s", (pathname) => {
    expect(getProviderScope(pathname)).toBe("shell");
  });

  it("mounts only the product provider for the public catalog", () => {
    expect(getProviderScope("/catalog")).toBe("catalog");
  });

  it.each(["/dashboard", "/products/1", "/admin/dashboard"])(
    "keeps full platform providers for %s",
    (pathname) => {
      expect(getProviderScope(pathname)).toBe("platform");
    },
  );
});
