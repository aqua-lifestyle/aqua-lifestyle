"use client";

import type { ReactNode } from "react";

import {
  AreaLeadersProvider,
  AreaSpacesProvider,
  AuthProvider,
  CustomersProvider,
  EnquiriesProvider,
  FacilitatorsProvider,
  MembershipsProvider,
  OrderIntentsProvider,
  ProductsProvider,
  ReferralsProvider,
  SystemHealthProvider,
  TenantProvider,
  ToastProvider,
  useAuthState,
  useTenantState,
} from "@/src/providers";

type AppProvidersProps = {
  children: ReactNode;
};

const TenantAwareProviders = ({ children }: { children: ReactNode }) => {
  const { currentTenant } = useTenantState();
  const { session } = useAuthState();
  const tenant = currentTenant ?? "host";
  const dataScope = getDataScopeKey(tenant, session?.user?.id);

  return (
    <AreaLeadersProvider key={`area-leaders-${dataScope}`}>
      <AreaSpacesProvider key={`area-spaces-${dataScope}`}>
        <FacilitatorsProvider key={`facilitators-${dataScope}`}>
          <ReferralsProvider key={`referrals-${dataScope}`}>
            <CustomersProvider key={`customers-${dataScope}`}>
              <EnquiriesProvider key={`enquiries-${dataScope}`}>
                <MembershipsProvider key={`memberships-${dataScope}`}>
                  <ProductsProvider key={`products-${dataScope}`}>
                    <OrderIntentsProvider key={`order-intents-${dataScope}`}>
                      {children}
                    </OrderIntentsProvider>
                  </ProductsProvider>
                </MembershipsProvider>
              </EnquiriesProvider>
            </CustomersProvider>
          </ReferralsProvider>
        </FacilitatorsProvider>
      </AreaSpacesProvider>
    </AreaLeadersProvider>
  );
};

export const getDataScopeKey = (
  tenant: string | null,
  userId: number | undefined,
) => `${tenant ?? "host"}:${userId ?? "anonymous"}`;

export const AppProviders = ({ children }: AppProvidersProps) => {
  return (
    <AuthProvider>
      <TenantProvider>
        <ToastProvider>
          <SystemHealthProvider>
            <TenantAwareProviders>{children}</TenantAwareProviders>
          </SystemHealthProvider>
        </ToastProvider>
      </TenantProvider>
    </AuthProvider>
  );
};
