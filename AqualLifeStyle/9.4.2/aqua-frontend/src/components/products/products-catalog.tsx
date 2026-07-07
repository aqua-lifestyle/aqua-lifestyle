"use client";

import { useEffect } from "react";

import {
  type Product,
  useMembershipsActions,
  useMembershipsState,
  useProductsActions,
  useProductsState,
} from "@/src/providers";
import { getMembershipNameById } from "@/src/shared/domain";
import { Badge, Card, LinkButton, StatusMessage } from "@/src/shared/ui";

const formatCurrency = (amount: number) =>
  new Intl.NumberFormat("en-ZA", {
    style: "currency",
    currency: "ZAR",
  }).format(amount);

const ProductCard = ({
  membershipName,
  product,
}: {
  membershipName: string;
  product: Product;
}) => {
  return (
    <Card>
      <div className="flex items-start justify-between gap-4">
        <div>
          <h2 className="text-lg font-semibold text-zinc-950">{product.name}</h2>
          <p className="mt-1 text-sm text-zinc-600">{membershipName}</p>
        </div>
        <Badge tone={product.isActive ? "success" : "neutral"}>
          {product.isActive ? "Active" : "Inactive"}
        </Badge>
      </div>

      <p className="mt-6 text-2xl font-semibold text-zinc-950">
        {formatCurrency(product.price)}
      </p>
    </Card>
  );
};

export const ProductsCatalog = () => {
  const { getMemberships } = useMembershipsActions();
  const { getProducts } = useProductsActions();
  const { memberships } = useMembershipsState();
  const { errorMessage, isError, isPending, products } = useProductsState();

  useEffect(() => {
    void getProducts();
    void getMemberships();
  }, [getMemberships, getProducts]);

  return (
    <main className="min-h-dvh bg-zinc-50 px-6 py-8 text-zinc-950 sm:px-8 lg:px-12">
      <div className="mx-auto flex w-full max-w-6xl flex-col gap-8">
        <header className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
          <div className="flex flex-col gap-2">
            <p className="text-sm font-medium uppercase tracking-wide text-emerald-700">
              Aqua Lifestyle Club
            </p>
            <h1 className="text-3xl font-semibold tracking-tight">Products</h1>
            <p className="max-w-2xl text-base text-zinc-600">
              Live product data from the ABP backend, including membership
              access requirements.
            </p>
          </div>
          <div className="flex flex-col gap-3 sm:flex-row">
            <LinkButton href="/enquiries">
              View enquiries
            </LinkButton>
            <LinkButton href="/enquiries/create">
              Create enquiry
            </LinkButton>
            <LinkButton href="/memberships">
              View memberships
            </LinkButton>
            <LinkButton href="/customers">
              View customers
            </LinkButton>
            <LinkButton href="/customers/register" variant="primary">
              Register customer
            </LinkButton>
          </div>
        </header>

        {isPending ? (
          <StatusMessage>Loading products...</StatusMessage>
        ) : null}

        {isError ? (
          <StatusMessage tone="error">
            {errorMessage ?? "Unable to load products."}
          </StatusMessage>
        ) : null}

        {!isPending && !isError && products.length === 0 ? (
          <StatusMessage>No products are available yet.</StatusMessage>
        ) : null}

        {products.length > 0 ? (
          <section className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
            {products.map((product) => (
              <ProductCard
                key={product.id}
                membershipName={getMembershipNameById(
                  memberships,
                  product.membershipId,
                  "Open access",
                )}
                product={product}
              />
            ))}
          </section>
        ) : null}
      </div>
    </main>
  );
};
