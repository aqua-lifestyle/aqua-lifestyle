import { describe, expect, it } from "vitest";

import {
  checkHealthError,
  checkHealthPending,
  checkHealthSuccess,
} from "./actions";
import { initialSystemHealthState } from "./context";
import { systemHealthReducer } from "./reducer";

describe("systemHealthReducer", () => {
  it("tracks health check pending state", () => {
    const state = systemHealthReducer(
      initialSystemHealthState,
      checkHealthPending(),
    );

    expect(state).toMatchObject({
      errorMessage: null,
      isError: false,
      isPending: true,
      isSuccess: false,
    });
  });

  it("stores successful health metadata", () => {
    const health = {
      status: "Healthy",
      isDatabaseReachable: true,
      databaseStatus: "Healthy",
      version: "1.0.0",
      buildId: "abc123",
      imageId: "unavailable",
      paymentContractVersion: "aqua-payments-2026-08-09-flexible-payment-approval",
      contractCapabilities: [],
      releaseDate: "2026-07-09T00:00:00Z",
      checkedAtUtc: "2026-07-09T10:00:00Z",
      environment: "Development",
      traceId: "trace-1",
    };

    const state = systemHealthReducer(
      initialSystemHealthState,
      checkHealthSuccess(health),
    );

    expect(state.health).toEqual(health);
    expect(state.isSuccess).toBe(true);
    expect(state.isPending).toBe(false);
  });

  it("keeps the last health payload when a later check fails", () => {
    const healthyState = systemHealthReducer(
      initialSystemHealthState,
      checkHealthSuccess({
        status: "Healthy",
        isDatabaseReachable: true,
        databaseStatus: "Healthy",
        version: "1.0.0",
        buildId: "abc123",
        imageId: "unavailable",
        paymentContractVersion: "aqua-payments-2026-08-09-flexible-payment-approval",
        contractCapabilities: [],
        releaseDate: "2026-07-09T00:00:00Z",
        checkedAtUtc: "2026-07-09T10:00:00Z",
        environment: "Development",
        traceId: "trace-1",
      }),
    );

    const failedState = systemHealthReducer(
      healthyState,
      checkHealthError("Backend is unreachable."),
    );

    expect(failedState.health).toEqual(healthyState.health);
    expect(failedState.errorMessage).toBe("Backend is unreachable.");
    expect(failedState.isError).toBe(true);
  });
});
