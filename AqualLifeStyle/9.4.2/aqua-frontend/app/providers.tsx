"use client";

import type { ReactNode } from "react";

import {
  AuthProvider,
  CustomersProvider,
  EnquiriesProvider,
  MembershipsProvider,
  OrderIntentsProvider,
  ProductsProvider,
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
