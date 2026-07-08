"use client";

import type { ReactNode } from "react";

import { AppContextBar } from "@/src/components/app/app-context-bar";
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
                  <AppContextBar />
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
