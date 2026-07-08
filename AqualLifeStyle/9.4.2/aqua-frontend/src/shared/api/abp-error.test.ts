import { describe, expect, it } from "vitest";

import {
  AbpHttpError,
  normalizeAbpError,
  unwrapAbpResponse,
} from "./abp-error";

describe("normalizeAbpError", () => {
  it("normalizes nested ABP error envelopes", () => {
    const error = normalizeAbpError(400, {
      error: {
        code: "Aqua:Validation",
        correlationId: "corr-1",
        message: "Validation failed.",
        validationErrors: [{ members: ["name"], message: "Name is required." }],
      },
    });

    expect(error).toBeInstanceOf(AbpHttpError);
    expect(error.status).toBe(400);
    expect(error.code).toBe("Aqua:Validation");
    expect(error.correlationId).toBe("corr-1");
    expect(error.validationErrors).toHaveLength(1);
  });
});

describe("unwrapAbpResponse", () => {
  it("returns plain data without an ABP envelope", () => {
    expect(unwrapAbpResponse({ id: 1, name: "Aqua" })).toEqual({
      id: 1,
      name: "Aqua",
    });
  });

  it("returns envelope result for successful ABP responses", () => {
    expect(
      unwrapAbpResponse({
        __abp: true,
        error: null,
        result: { id: 2 },
        success: true,
        targetUrl: null,
        unAuthorizedRequest: false,
      }),
    ).toEqual({ id: 2 });
  });

  it("throws an AbpHttpError for failed ABP responses", () => {
    expect(() =>
      unwrapAbpResponse({
        __abp: true,
        error: { message: "Business rule failed." },
        result: null,
        success: false,
        targetUrl: null,
        unAuthorizedRequest: false,
      }),
    ).toThrow(AbpHttpError);
  });
});
