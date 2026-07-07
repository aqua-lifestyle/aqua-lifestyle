"use client";

import type { ReactNode } from "react";

import {
  CustomersProvider,
  MembershipsProvider,
  ProductsProvider,
} from "@/src/providers";

type AppProvidersProps = {
  children: ReactNode;
};

export const AppProviders = ({ children }: AppProvidersProps) => {
  return (
    <CustomersProvider>
      <MembershipsProvider>
        <ProductsProvider>{children}</ProductsProvider>
      </MembershipsProvider>
    </CustomersProvider>
  );
};
