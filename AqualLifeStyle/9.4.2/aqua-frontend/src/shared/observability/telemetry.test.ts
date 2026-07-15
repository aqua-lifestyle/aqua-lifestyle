import { publicEnv } from "@/src/shared/config";
import { describe, expect, it, vi } from "vitest";

import { reportWebVital } from "./telemetry";

describe("telemetry", () => {
  it("does not send metrics until a monitoring endpoint is configured", () => {
    const beacon = vi.fn(() => true);
    Object.defineProperty(navigator, "sendBeacon", {
      configurable: true,
      value: beacon,
    });

    reportWebVital({ id: "metric-1", name: "LCP", rating: "good", value: 250 });

    expect(publicEnv.NEXT_PUBLIC_MONITORING_ENDPOINT).toBeUndefined();
    expect(beacon).not.toHaveBeenCalled();
  });
});
