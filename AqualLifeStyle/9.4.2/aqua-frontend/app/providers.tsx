"use client";

import type { ReactNode } from "react";

import {
  CustomersProvider,
  EnquiriesProvider,
  MembershipsProvider,
  ProductsProvider,
} from "@/src/providers";

type AppProvidersProps = {
  children: ReactNode;
};

export const AppProviders = ({ children }: AppProvidersProps) => {
  return (
    <CustomersProvider>
      <EnquiriesProvider>
        <MembershipsProvider>
          <ProductsProvider>{children}</ProductsProvider>
        </MembershipsProvider>
      </EnquiriesProvider>
    </CustomersProvider>
  );
};
