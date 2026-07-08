"use client";

import type { ReactNode } from "react";

import { AuthReadinessBanner } from "@/src/components/auth/auth-readiness-banner";
import { TenantSwitcher } from "@/src/components/tenant/tenant-switcher";
import {
  AuthProvider,
  CustomersProvider,
  EnquiriesProvider,
  MembershipsProvider,
  OrderIntentsProvider,
  ProductsProvider,
  TenantProvider,
} from "@/src/providers";

type AppProvidersProps = {
  children: ReactNode;
};

export const AppProviders = ({ children }: AppProvidersProps) => {
  return (
    <AuthProvider>
      <TenantProvider>
        <CustomersProvider>
          <EnquiriesProvider>
            <MembershipsProvider>
              <ProductsProvider>
                <OrderIntentsProvider>
                  <AuthReadinessBanner />
                  <TenantSwitcher />
                  {children}
                </OrderIntentsProvider>
              </ProductsProvider>
            </MembershipsProvider>
          </EnquiriesProvider>
        </CustomersProvider>
      </TenantProvider>
    </AuthProvider>
  );
};
