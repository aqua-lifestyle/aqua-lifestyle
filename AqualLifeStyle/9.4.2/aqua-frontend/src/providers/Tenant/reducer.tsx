import type { TenantAction } from "./actions";
import { TenantActionTypes } from "./actions";
import type { TenantState } from "./context";

export const tenantReducer = (
  state: TenantState,
  action: TenantAction,
): TenantState => {
  switch (action.type) {
    case TenantActionTypes.clearTenant:
      return {
        ...state,
        currentTenant: null,
        isHost: true,
      };

    case TenantActionTypes.setTenant:
      return {
        ...state,
        currentTenant: action.payload,
        isHost: false,
      };

    default:
      return state;
  }
};
