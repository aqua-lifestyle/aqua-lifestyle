import { describe, expect, it } from "vitest";

import {
  isPaymentApiCompatible,
  isPilotAQGreenJoiningApiCompatible,
  isProgrammeJourneyApiCompatible,
  parseSystemHealth,
} from "./contract";

describe("parseSystemHealth", () => {
  const pilotHealth = () => parseSystemHealth({
    status: "Healthy", isDatabaseReachable: true, databaseStatus: "Healthy",
    version: "1.0.0", buildId: "TEST-ONLY", imageId: "unavailable",
    paymentContractVersion: "aqua-payments-2026-08-09-flexible-payment-approval",
    contractCapabilities: ["aqgreen-flexible-joining-v1", "programme-approval-queue-v1", "member-programme-journey-v1"],
    releaseDate: "2026-10-05T00:00:00Z", checkedAtUtc: "2026-10-05T00:00:00Z",
    environment: "AQGreenV2Pilot", traceId: "TEST-ONLY",
  });
  it("requires only commissioned AQGreen capabilities in the exact pilot environment", () => {
    expect(isPilotAQGreenJoiningApiCompatible(pilotHealth())).toBe(true);
    expect(isPaymentApiCompatible(pilotHealth())).toBe(false);
  });
  it("rejects unknown or mismatched pilot contracts", () => {
    expect(isPilotAQGreenJoiningApiCompatible(null)).toBe(false);
    expect(isPilotAQGreenJoiningApiCompatible({ ...pilotHealth(), environment: "Production" })).toBe(false);
    expect(isPilotAQGreenJoiningApiCompatible({ ...pilotHealth(), paymentContractVersion: "legacy-entry-v1" })).toBe(false);
  });
  it("requires both joining and approval support without a capability fallback", () => {
    for (const missing of ["aqgreen-flexible-joining-v1", "programme-approval-queue-v1"])
      expect(isPilotAQGreenJoiningApiCompatible({ ...pilotHealth(), contractCapabilities: pilotHealth().contractCapabilities.filter((item) => item !== missing) })).toBe(false);
  });
  it("accepts the backend health response contract", () => {
    const health = parseSystemHealth({
      status: "Healthy",
      isDatabaseReachable: true,
      databaseStatus: "Healthy",
      version: "1.0.0",
      buildId: "abc123",
      imageId: "image-1",
      paymentContractVersion: "aqua-payments-2026-08-09-flexible-payment-approval",
      contractCapabilities: [
        "aqgreen-flexible-joining-v1",
        "programme-approval-queue-v1",
        "member-programme-journey-v1",
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
    expect(isProgrammeJourneyApiCompatible(health)).toBe(true);
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
    expect(isProgrammeJourneyApiCompatible(health)).toBe(false);
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
