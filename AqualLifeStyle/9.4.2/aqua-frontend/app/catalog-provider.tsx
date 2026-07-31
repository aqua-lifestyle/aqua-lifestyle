"use client";

import type { ReactNode } from "react";

import { ProductsProvider } from "@/src/providers/Products";

export const CatalogProvider = ({
  children,
  dataScope,
}: {
  children: ReactNode;
  dataScope: string;
}) => (
  <ProductsProvider key={`products-${dataScope}`}>{children}</ProductsProvider>
);
