import { describe, expect, it } from "vitest";

import {
  AbpHttpError,
  getRequestErrorMessage,
  normalizeAbpError,
  normalizeNetworkError,
  unwrapAbpResponse,
} from "./abp-error";

describe("getRequestErrorMessage", () => {
  it("uses the public error message without exposing technical details", () => {
    const error = new AbpHttpError(500, {
      message: "Request failed.",
      details: "Database connection string details",
    });

    expect(getRequestErrorMessage(error, "Fallback")).toBe("Request failed.");
    expect(getRequestErrorMessage({ unexpected: true }, "Fallback")).toBe("Fallback");
  });

  it("turns generic HTTP failures into actionable messages", () => {
    expect(getRequestErrorMessage(new AbpHttpError(405, {}), "Customer update failed."))
      .toContain("not supported by the current server version");
    expect(getRequestErrorMessage(new AbpHttpError(403, {}), "Customer update failed."))
      .toContain("does not have permission");
    expect(getRequestErrorMessage(new AbpHttpError(409, {}), "Customer update failed."))
      .toContain("conflicts with an existing record");
  });

  it("prioritizes field validation messages from the server", () => {
    const error = new AbpHttpError(400, {
      validationErrors: [
        { members: ["email"], message: "Enter a valid email address." },
        { members: ["membershipId"], message: "Select an available membership plan." },
      ],
    });

    expect(getRequestErrorMessage(error, "Customer update failed."))
      .toBe("Enter a valid email address. Select an available membership plan.");
  });

  it("shows the business reason supplied with a user-friendly server error", () => {
    const error = new AbpHttpError(500, {
      message: "Customer creation failed.",
      details: "The selected membership plan is unavailable or inactive.",
    });

    expect(getRequestErrorMessage(error, "The customer could not be created."))
      .toBe("The selected membership plan is unavailable or inactive.");
  });

  it("does not expose technical diagnostics as error details", () => {
    const error = new AbpHttpError(500, {
      message: "An internal error occurred during your request!",
      details: "Stack trace:\n   at Application.Service in /src/Application/Service.cs",
    });

    expect(getRequestErrorMessage(error, "The customer could not be created."))
      .toBe("The customer could not be created.");
  });
});

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

describe("normalizeNetworkError", () => {
  it("returns a user-actionable backend reachability error", () => {
    const error = normalizeNetworkError();

    expect(error).toBeInstanceOf(AbpHttpError);
    expect(error.status).toBe(0);
    expect(error.code).toBe("Aqua:Network");
    expect(error.message).toContain("Unable to reach the backend API");
    expect(error.message).toContain("HTTPS certificate");
    expect(error.message).toContain("CORS");
  });

  it("returns a TLS-specific message for certificate failures", () => {
    const error = normalizeNetworkError(
      new Error("self-signed certificate DEPTH_ZERO_SELF_SIGNED_CERT"),
    );

    expect(error).toBeInstanceOf(AbpHttpError);
    expect(error.status).toBe(0);
    expect(error.code).toBe("Aqua:Tls");
    expect(error.message).toContain("HTTPS certificate is not trusted");
    expect(error.message).toContain("http://localhost:21021");
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
