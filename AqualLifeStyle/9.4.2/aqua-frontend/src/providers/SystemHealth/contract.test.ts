import { describe, expect, it } from "vitest";

import { isPaymentApiCompatible, parseSystemHealth } from "./contract";

describe("parseSystemHealth", () => {
  it("accepts the backend health response contract", () => {
    const health = parseSystemHealth({
      status: "Healthy",
      isDatabaseReachable: true,
      databaseStatus: "Healthy",
      version: "1.0.0",
      buildId: "abc123",
      imageId: "image-1",
      paymentContractVersion: "aqua-payments-2026-08-01",
      contractCapabilities: [
        "aqgreen-joining-schedules-v1",
        "direct-onyx-checkout-v1",
      ],
      releaseDate: "2026-07-09T00:00:00Z",
      checkedAtUtc: "2026-07-09T10:00:00Z",
      environment: "Development",
      traceId: "trace-1",
    });

    expect(health.status).toBe("Healthy");
    expect(health.isDatabaseReachable).toBe(true);
    expect(isPaymentApiCompatible(health)).toBe(true);
  });

  it("rejects an older payment contract", () => {
    const health = parseSystemHealth({
      status: "Healthy",
      isDatabaseReachable: true,
      databaseStatus: "Healthy",
      version: "1.0.0",
      buildId: "abc123",
      imageId: "unavailable",
      paymentContractVersion: "legacy-entry-v1",
      contractCapabilities: [],
      releaseDate: "2026-07-09T00:00:00Z",
      checkedAtUtc: "2026-07-09T10:00:00Z",
      environment: "Development",
      traceId: "trace-1",
    });

    expect(isPaymentApiCompatible(health)).toBe(false);
  });

  it("rejects missing database readiness fields", () => {
    expect(() =>
      parseSystemHealth({
        status: "Healthy",
        version: "1.0.0",
        releaseDate: "2026-07-09T00:00:00Z",
        checkedAtUtc: "2026-07-09T10:00:00Z",
        environment: "Development",
        traceId: "trace-1",
      }),
    ).toThrow();
  });
});
