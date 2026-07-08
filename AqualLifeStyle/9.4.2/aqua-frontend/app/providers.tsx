"use client";

import type { ReactNode } from "react";

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
                <OrderIntentsProvider>{children}</OrderIntentsProvider>
              </ProductsProvider>
            </MembershipsProvider>
          </EnquiriesProvider>
        </CustomersProvider>
      </TenantProvider>
    </AuthProvider>
  );
};
