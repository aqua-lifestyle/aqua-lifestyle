"use client";

import type { ReactNode } from "react";

import { ProductsProvider } from "@/src/providers";

type AppProvidersProps = {
  children: ReactNode;
};

export const AppProviders = ({ children }: AppProvidersProps) => {
  return <ProductsProvider>{children}</ProductsProvider>;
};
