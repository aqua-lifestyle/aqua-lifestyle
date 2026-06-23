import { describe, expect, it } from "vitest";

import {
  getSavingsWindowStatusesError,
  getSavingsWindowStatusesPending,
  getSavingsWindowStatusesSuccess,
} from "./actions";
import { initialMembershipsState, type SavingsWindowStatus } from "./context";
import { membershipsReducer } from "./reducer";

const savingsWindowStatuses: SavingsWindowStatus[] = [
  {
    asOfDate: "2026-07-16",
    currentDay: 16,
    isSavingsWindowOpen: false,
    savingsWindowCloseDay: 15,
    savingsWindowOpenDay: 1,
    statusLabel: "Closed",
    tier: 0,
    tierName: "Jasper",
  },
];

describe("membershipsReducer", () => {
  it("stores savings window readiness", () => {
    const pendingState = membershipsReducer(
      initialMembershipsState,
      getSavingsWindowStatusesPending(),
    );
    const state = membershipsReducer(
      pendingState,
      getSavingsWindowStatusesSuccess(savingsWindowStatuses),
    );

    expect(state.isSavingsWindowStatusesPending).toBe(false);
    expect(state.isSavingsWindowStatusesSuccess).toBe(true);
    expect(state.savingsWindowStatuses).toEqual(savingsWindowStatuses);
    expect(state.savingsWindowStatusesErrorMessage).toBeNull();
  });

  it("stores savings window readiness errors", () => {
    const state = membershipsReducer(
      initialMembershipsState,
      getSavingsWindowStatusesError("Unable to load savings windows."),
    );

    expect(state.isSavingsWindowStatusesError).toBe(true);
    expect(state.isSavingsWindowStatusesPending).toBe(false);
    expect(state.savingsWindowStatusesErrorMessage).toBe(
      "Unable to load savings windows.",
    );
  });
});
