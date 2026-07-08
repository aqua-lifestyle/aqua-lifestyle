export const TenantActionTypes = {
  clearTenant: "tenant/clearTenant",
  setTenant: "tenant/setTenant",
} as const;

export type TenantAction =
  | {
      type: typeof TenantActionTypes.clearTenant;
    }
  | {
      type: typeof TenantActionTypes.setTenant;
      payload: string;
    };

export const clearTenant = (): TenantAction => ({
  type: TenantActionTypes.clearTenant,
});

export const setTenant = (tenant: string): TenantAction => ({
  type: TenantActionTypes.setTenant,
  payload: tenant,
});
