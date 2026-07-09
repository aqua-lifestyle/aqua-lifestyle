import { describe, expect, it } from "vitest";

import { parseSystemHealth } from "./contract";

describe("parseSystemHealth", () => {
  it("accepts the backend health response contract", () => {
    const health = parseSystemHealth({
      status: "Healthy",
      isDatabaseReachable: true,
      databaseStatus: "Healthy",
      version: "1.0.0",
      releaseDate: "2026-07-09T00:00:00Z",
      checkedAtUtc: "2026-07-09T10:00:00Z",
      environment: "Development",
      traceId: "trace-1",
    });

    expect(health.status).toBe("Healthy");
    expect(health.isDatabaseReachable).toBe(true);
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
