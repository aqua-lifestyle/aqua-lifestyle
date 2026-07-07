"use client";

import { useEffect } from "react";

import {
  type Product,
  useProductsActions,
  useProductsState,
} from "@/src/providers";

const membershipLabels: Record<number, string> = {
  1: "Jasper",
  2: "Onyx",
  3: "AQGreen",
  4: "Business Premier",
};

const formatCurrency = (amount: number) =>
  new Intl.NumberFormat("en-ZA", {
    style: "currency",
    currency: "ZAR",
  }).format(amount);

const getMembershipLabel = (membershipId: number | null) => {
  if (membershipId === null) {
    return "Open access";
  }

  return membershipLabels[membershipId] ?? `Membership ${membershipId}`;
};

const ProductCard = ({ product }: { product: Product }) => {
  return (
    <article className="rounded-lg border border-zinc-200 bg-white p-5 shadow-sm">
      <div className="flex items-start justify-between gap-4">
        <div>
          <h2 className="text-lg font-semibold text-zinc-950">{product.name}</h2>
          <p className="mt-1 text-sm text-zinc-600">
            {getMembershipLabel(product.membershipId)}
          </p>
        </div>
        <span className="rounded-full bg-emerald-50 px-3 py-1 text-sm font-medium text-emerald-700">
          {product.isActive ? "Active" : "Inactive"}
        </span>
      </div>

      <p className="mt-6 text-2xl font-semibold text-zinc-950">
        {formatCurrency(product.price)}
      </p>
    </article>
  );
};

export const ProductsCatalog = () => {
  const { getProducts } = useProductsActions();
  const { errorMessage, isError, isPending, products } = useProductsState();

  useEffect(() => {
    void getProducts();
  }, [getProducts]);

  return (
    <main className="min-h-dvh bg-zinc-50 px-6 py-8 text-zinc-950 sm:px-8 lg:px-12">
      <div className="mx-auto flex w-full max-w-6xl flex-col gap-8">
        <header className="flex flex-col gap-2">
          <p className="text-sm font-medium uppercase tracking-wide text-emerald-700">
            Aqua Lifestyle Club
          </p>
          <h1 className="text-3xl font-semibold tracking-tight">Products</h1>
          <p className="max-w-2xl text-base text-zinc-600">
            Live product data from the ABP backend, including membership access
            requirements.
          </p>
        </header>

        {isPending ? (
          <section className="rounded-lg border border-dashed border-zinc-300 bg-white p-8 text-zinc-600">
            Loading products...
          </section>
        ) : null}

        {isError ? (
          <section className="rounded-lg border border-red-200 bg-red-50 p-5 text-red-800">
            {errorMessage ?? "Unable to load products."}
          </section>
        ) : null}

        {!isPending && !isError && products.length === 0 ? (
          <section className="rounded-lg border border-dashed border-zinc-300 bg-white p-8 text-zinc-600">
            No products are available yet.
          </section>
        ) : null}

        {products.length > 0 ? (
          <section className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
            {products.map((product) => (
              <ProductCard key={product.id} product={product} />
            ))}
          </section>
        ) : null}
      </div>
    </main>
  );
};
