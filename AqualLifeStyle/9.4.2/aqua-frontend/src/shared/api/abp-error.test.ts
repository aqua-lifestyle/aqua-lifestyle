import { describe, expect, it } from "vitest";

import {
  AbpHttpError,
  getRequestErrorMessage,
  getRequestErrorPresentation,
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
    expect(error.message).toContain("Aqua Lifestyle Club");
    expect(error.message).toContain("Check your connection");
    expect(error.message).not.toMatch(/CORS|certificate|localhost/);
  });

  it("returns a TLS-specific message for certificate failures", () => {
    const error = normalizeNetworkError(
      new Error("self-signed certificate DEPTH_ZERO_SELF_SIGNED_CERT"),
    );

    expect(error).toBeInstanceOf(AbpHttpError);
    expect(error.status).toBe(0);
    expect(error.code).toBe("Aqua:Tls");
    expect(error.message).toContain("secure connection");
    expect(error.message).toContain("contact the club team");
    expect(error.message).not.toMatch(/localhost|dev-certs|accept the certificate/);
  });
});

describe("customer-facing domain error presentation", () => {
  it.each([
    ["Independent admission and recruiter-only joining are unavailable on this path.", "Before you join AQGreen", "authorised Host administrator"],
    ["The invitation code was not found.", "Invitation unavailable", "original link"],
    ["The inviting Club Member is not currently eligible to invite members to this programme.", "This invitation is not ready to use", "check their participation"],
    ["Your email address is not confirmed.", "Verify your email to continue", "Resend verification email"],
    ["Online payment is temporarily unavailable.", "Payments are temporarily unavailable", "check that payment's status"],
    ["Payment failed.", "Payment could not be completed", "If money left your account"],
    ["Your AQGreen payment checkout is still being prepared.", "Check your payment status", "Do not start a second payment"],
  ])("maps the recognised domain response %s", (details, title, nextAction) => {
    const error = new AbpHttpError(500, { message: "The request failed.", details });
    // A confirmed business failure is not replaced with generic success or retry.
    const result = getRequestErrorPresentation(error, "Please contact the club team.");
    expect(result.title).toBe(title);
    expect(result.message).toContain(nextAction);
    expect(getRequestErrorMessage(error, "Fallback")).toBe(result.message);
  });

  it("keeps a lost checkout response uncertain, including generic HTTP failures", () => {
    for (const error of [normalizeNetworkError(), new AbpHttpError(503, {}), new Error("Lost response")]) {
      const result = getRequestErrorPresentation(error, "Fallback", "checkout");
      expect(result.title).toBe("Payment status not confirmed");
      expect(result.reviewPaymentStatus).toBe(true);
      expect(result.message).toContain("Do not start a second payment");
      expect(result.message).not.toMatch(/No payment has been taken|retry|try again shortly/i);
    }
  });

  it("does not misdiagnose a composite founder-registration rejection as an unverified email", () => {
    const result = getRequestErrorPresentation(new AbpHttpError(500, {
      message: "D10_CLEAN_START_ROOT_VERIFIED_REGISTRATION_REQUIRED",
    }), "Fallback");
    expect(result.title).toBe("Founder eligibility needs review");
    expect(result.message).toContain("current first-founder registration requirements");
    expect(result.message).not.toContain("Open the verification email");
    expect(result.message).not.toContain("D10_");
  });

  it("does not print internal codes or exception diagnostics from any display field", () => {
    const error = new AbpHttpError(500, {
      code: "D10_CURRENT_JOINING_AREA_UNVERIFIABLE", correlationId: "diagnostic-reference",
      message: "D10_CURRENT_JOINING_AREA_UNVERIFIABLE",
      details: "Npgsql exception in /src/Application/Service.cs",
      validationErrors: [{ message: "YOCO_CUTOVER_BUSINESS_STATE_UNKNOWN" }],
    });
    const result = getRequestErrorPresentation(error, "Please contact the club team.");
    expect(result.message).toBe("Please contact the club team.");
    expect(JSON.stringify(result)).not.toMatch(/D10_|YOCO_|Npgsql|diagnostic-reference|\/src\//);
    expect(error.code).toBe("D10_CURRENT_JOINING_AREA_UNVERIFIABLE");
    expect(error.correlationId).toBe("diagnostic-reference");
  });

  it("keeps an unexpected invitation service error actionable without revealing its internals", () => {
    const result = getRequestErrorPresentation(new Error("TypeError: cannot read properties"), "Fallback", "invitation");
    expect(result.title).toBe("Invitation unavailable");
    expect(result.message).toContain("contact the club team");
    expect(result.message).not.toContain("TypeError");
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
