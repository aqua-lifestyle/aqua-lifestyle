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
  useTenantState,
} from "@/src/providers";

type AppProvidersProps = {
  children: ReactNode;
};

const TenantAwareProviders = ({ children }: { children: ReactNode }) => {
  const { currentTenant } = useTenantState();
  const tenant = currentTenant ?? "host";

  return (
    <AreaLeadersProvider key={`area-leaders-${tenant}`}>
      <AreaSpacesProvider key={`area-spaces-${tenant}`}>
        <FacilitatorsProvider key={`facilitators-${tenant}`}>
          <ReferralsProvider key={`referrals-${tenant}`}>
            <CustomersProvider key={`customers-${tenant}`}>
              <EnquiriesProvider key={`enquiries-${tenant}`}>
                <MembershipsProvider key={`memberships-${tenant}`}>
                  <ProductsProvider key={`products-${tenant}`}>
                    <OrderIntentsProvider key={`order-intents-${tenant}`}>
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
