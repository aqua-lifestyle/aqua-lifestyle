"use client";

import { useEffect } from "react";

import {
  useMembershipsActions,
  useMembershipsState,
  useProductsActions,
  useProductsState,
} from "@/src/providers";
import { getMembershipNameById } from "@/src/shared/domain";
import { Badge, Card, LinkButton, StatusMessage } from "@/src/shared/ui";

type ProductDetailsProps = {
  productId: number;
};

const formatCurrency = (amount: number) =>
  new Intl.NumberFormat("en-ZA", {
    style: "currency",
    currency: "ZAR",
  }).format(amount);

export const ProductDetails = ({ productId }: ProductDetailsProps) => {
  const { getMemberships } = useMembershipsActions();
  const { getProduct } = useProductsActions();
  const { memberships } = useMembershipsState();
  const {
    isSelectedError,
    isSelectedPending,
    selectedErrorMessage,
    selectedProduct,
  } = useProductsState();

  useEffect(() => {
    if (!Number.isInteger(productId) || productId <= 0) {
      return;
    }

    void getProduct(productId);
    void getMemberships();
  }, [getMemberships, getProduct, productId]);

  return (
    <main className="min-h-dvh bg-zinc-50 px-6 py-8 text-zinc-950 sm:px-8 lg:px-12">
      <div className="mx-auto flex w-full max-w-5xl flex-col gap-8">
        <header className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
          <div className="flex flex-col gap-2">
            <p className="text-sm font-medium uppercase tracking-wide text-emerald-700">
              Aqua Lifestyle Club
            </p>
            <h1 className="text-3xl font-semibold tracking-tight">
              Product details
            </h1>
            <p className="max-w-2xl text-base text-zinc-600">
              Validate product access and pricing before starting the enquiry
              flow.
            </p>
          </div>
          <LinkButton href="/products">Back to products</LinkButton>
        </header>

        {!Number.isInteger(productId) || productId <= 0 ? (
          <StatusMessage tone="error">This product id is invalid.</StatusMessage>
        ) : null}

        {isSelectedPending ? (
          <StatusMessage>Loading product...</StatusMessage>
        ) : null}

        {isSelectedError ? (
          <StatusMessage tone="error">
            {selectedErrorMessage ?? "Unable to load this product."}
          </StatusMessage>
        ) : null}

        {selectedProduct ? (
          <section className="grid gap-6 lg:grid-cols-[1fr_22rem]">
            <Card>
              <div className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
                <div>
                  <h2 className="text-2xl font-semibold tracking-tight">
                    {selectedProduct.name}
                  </h2>
                  <p className="mt-2 text-sm text-zinc-600">
                    {getMembershipNameById(
                      memberships,
                      selectedProduct.membershipId,
                      "Open access",
                    )}
                  </p>
                </div>
                <Badge tone={selectedProduct.isActive ? "success" : "neutral"}>
                  {selectedProduct.isActive ? "Active" : "Inactive"}
                </Badge>
              </div>

              <p className="mt-8 text-4xl font-semibold text-zinc-950">
                {formatCurrency(selectedProduct.price)}
              </p>
            </Card>

            <aside className="flex flex-col gap-6">
              <Card>
                <h2 className="text-lg font-semibold">Demo actions</h2>
                <p className="mt-3 text-sm leading-6 text-zinc-600">
                  Use this product when creating an enquiry, then return to the
                  enquiry workflow to respond or close it.
                </p>
                <div className="mt-6 flex flex-col gap-3">
                  <LinkButton
                    href={`/enquiries/create?productId=${selectedProduct.id}`}
                    variant="primary"
                  >
                    Create enquiry
                  </LinkButton>
                  <LinkButton href="/enquiries">View enquiries</LinkButton>
                </div>
              </Card>

              <Card>
                <dl className="grid gap-3 text-sm">
                  <div className="flex justify-between gap-4">
                    <dt className="text-zinc-600">Product ID</dt>
                    <dd className="font-medium text-zinc-950">
                      {selectedProduct.id}
                    </dd>
                  </div>
                  <div className="flex justify-between gap-4">
                    <dt className="text-zinc-600">Membership ID</dt>
                    <dd className="font-medium text-zinc-950">
                      {selectedProduct.membershipId ?? "None"}
                    </dd>
                  </div>
                </dl>
              </Card>
            </aside>
          </section>
        ) : null}
      </div>
    </main>
  );
};
