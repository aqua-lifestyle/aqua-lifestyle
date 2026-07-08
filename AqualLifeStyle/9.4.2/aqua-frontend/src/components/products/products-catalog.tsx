"use client";

import { useEffect, useState } from "react";

import {
  type Product,
  useMembershipsActions,
  useMembershipsState,
  useProductsActions,
  useProductsState,
} from "@/src/providers";
import { getMembershipNameById } from "@/src/shared/domain";
import {
  Badge,
  Card,
  LinkButton,
  SelectField,
  StatusMessage,
} from "@/src/shared/ui";

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

      <div className="mt-6 flex flex-col gap-3">
        <LinkButton href={`/products/${product.id}`}>Open product</LinkButton>
        <LinkButton href={`/enquiries/create?productId=${product.id}`}>
          Create enquiry
        </LinkButton>
      </div>
    </Card>
  );
};

export const ProductsCatalog = () => {
  const [membershipFilter, setMembershipFilter] = useState("all");
  const { getMemberships } = useMembershipsActions();
  const { getProducts } = useProductsActions();
  const { memberships } = useMembershipsState();
  const { errorMessage, isError, isPending, products } = useProductsState();

  useEffect(() => {
    void getProducts();
    void getMemberships();
  }, [getMemberships, getProducts]);

  const filteredProducts = products.filter((product) => {
    if (membershipFilter === "all") {
      return true;
    }

    if (membershipFilter === "open") {
      return product.membershipId === null;
    }

    return product.membershipId === Number(membershipFilter);
  });

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
          <StatusMessage>
            <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
              <span>No products are available yet.</span>
              <LinkButton href="/memberships">Check memberships</LinkButton>
            </div>
          </StatusMessage>
        ) : null}

        {products.length > 0 ? (
          <section className="grid gap-4 rounded-lg border border-zinc-200 bg-white p-4 shadow-sm md:grid-cols-[1fr_18rem] md:items-end">
            <div>
              <h2 className="text-lg font-semibold text-zinc-950">
                Catalog filters
              </h2>
              <p className="mt-2 text-sm leading-6 text-zinc-600">
                Filter the live product list by membership access to validate
                the club-commerce rules during the demo.
              </p>
            </div>
            <SelectField
              label="Membership access"
              name="membershipFilter"
              onChange={(event) => setMembershipFilter(event.target.value)}
              value={membershipFilter}
            >
              <option value="all">All products</option>
              <option value="open">Open access</option>
              {memberships.map((membership) => (
                <option key={membership.id} value={membership.id}>
                  {membership.name}
                </option>
              ))}
            </SelectField>
          </section>
        ) : null}

        {products.length > 0 && filteredProducts.length === 0 ? (
          <StatusMessage>
            <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
              <span>No products match this membership access filter.</span>
              <LinkButton href="/memberships">Review memberships</LinkButton>
            </div>
          </StatusMessage>
        ) : null}

        {filteredProducts.length > 0 ? (
          <section className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
            {filteredProducts.map((product) => (
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
