"use client";

import type { ReactNode } from "react";

import { CustomersProvider, ProductsProvider } from "@/src/providers";

type AppProvidersProps = {
  children: ReactNode;
};

export const AppProviders = ({ children }: AppProvidersProps) => {
  return (
    <CustomersProvider>
      <ProductsProvider>{children}</ProductsProvider>
    </CustomersProvider>
  );
};
