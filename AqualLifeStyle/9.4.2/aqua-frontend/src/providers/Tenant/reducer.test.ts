import { describe, expect, it } from "vitest";

import { clearTenant, setTenant } from "./actions";
import { initialTenantState } from "./context";
import { tenantReducer } from "./reducer";

describe("tenantReducer", () => {
  it("selects tenant mode", () => {
    const state = tenantReducer(initialTenantState, setTenant("tenant-a"));

    expect(state.currentTenant).toBe("tenant-a");
    expect(state.isHost).toBe(false);
  });

  it("returns to host mode", () => {
    const tenantState = tenantReducer(initialTenantState, setTenant("tenant-a"));
    const state = tenantReducer(tenantState, clearTenant());

    expect(state.currentTenant).toBeNull();
    expect(state.isHost).toBe(true);
  });
});
