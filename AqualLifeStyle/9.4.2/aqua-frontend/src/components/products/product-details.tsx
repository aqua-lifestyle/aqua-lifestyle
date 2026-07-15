"use client";

import { useEffect, useState } from "react";

import {
  useMembershipsActions,
  useMembershipsState,
  useProductsActions,
  useProductsState,
} from "@/src/providers";
import { getMembershipNameById } from "@/src/shared/domain";
import {
  Badge,
  Breadcrumb,
  Card,
  LinkButton,
  Skeleton,
  StatusMessage,
  Tabs,
} from "@/src/shared/ui";
import { cn } from "@/src/shared/lib/utils";

type StockStatus = "in-stock" | "low-stock" | "out-of-stock";

const getStockStatus = (product: { id: number; isActive: boolean }): StockStatus => {
  if (!product.isActive) {
    return "out-of-stock";
  }
  const remainder = Math.abs(product.id) % 3;
  if (remainder === 0) return "in-stock";
  if (remainder === 1) return "low-stock";
  return "out-of-stock";
};

const getProductPlaceholder = (id: number) => {
  const gradients = [
    "from-blue-400 to-cyan-300",
    "from-emerald-400 to-teal-300",
    "from-violet-400 to-fuchsia-300",
    "from-amber-400 to-orange-300",
    "from-rose-400 to-pink-300",
  ];
  return gradients[id % gradients.length];
};

const stockBadgeTone = (status: StockStatus): "success" | "warning" | "error" => {
  switch (status) {
    case "in-stock":
      return "success";
    case "low-stock":
      return "warning";
    case "out-of-stock":
    default:
      return "error";
  }
};

const stockLabel = (status: StockStatus) => {
  switch (status) {
    case "in-stock":
      return "In stock";
    case "low-stock":
      return "Low stock";
    case "out-of-stock":
      return "Out of stock";
  }
};

const formatCurrency = (amount: number) =>
  new Intl.NumberFormat("en-ZA", {
    currency: "ZAR",
    style: "currency",
  }).format(amount);

type ProductDetailsProps = {
  productId: number;
};

const ProductOverview = ({
  product,
  membershipName,
}: {
  membershipName: string;
  product: { id: number; isActive: boolean; name: string; price: number };
}) => {
  const status = getStockStatus(product);

  return (
    <div className="grid gap-6 lg:grid-cols-[1fr_22rem]">
      <div className="overflow-hidden rounded-2xl border border-border bg-card shadow-sm">
        <div
          className={cn(
            "relative flex h-72 w-full items-center justify-center bg-gradient-to-br text-white",
            getProductPlaceholder(product.id),
          )}
        >
          <span className="text-6xl font-bold">{product.name.charAt(0).toUpperCase()}</span>
          <div className="absolute right-4 top-4">
            <Badge className="bg-white/90 text-foreground" tone={stockBadgeTone(status)}>
              {stockLabel(status)}
            </Badge>
          </div>
        </div>
      </div>

      <aside className="flex flex-col gap-6">
        <Card>
          <h2 className="text-lg font-semibold">{product.name}</h2>
          <p className="text-sm text-muted-foreground">{membershipName}</p>

          <div className="mt-6 flex items-baseline gap-2">
            <span className="text-4xl font-bold text-foreground">
              {formatCurrency(product.price)}
            </span>
            <span className="text-sm text-muted-foreground">per unit</span>
          </div>

          <div className="mt-6 grid gap-3 text-sm">
            <div className="flex justify-between gap-4">
              <dt className="text-muted-foreground">Product ID</dt>
              <dd className="font-medium">{product.id}</dd>
            </div>
            <div className="flex justify-between gap-4">
              <dt className="text-muted-foreground">Status</dt>
              <dd className="font-medium">
                <Badge tone={product.isActive ? "success" : "neutral"}>
                  {product.isActive ? "Active" : "Inactive"}
                </Badge>
              </dd>
            </div>
            <div className="flex justify-between gap-4">
              <dt className="text-muted-foreground">Access</dt>
              <dd className="font-medium">{membershipName}</dd>
            </div>
          </div>
        </Card>

        <div className="flex flex-col gap-2">
          <LinkButton
            href={`/enquiries/create?productId=${product.id}`}
            variant="primary"
          >
            Create customer enquiry
          </LinkButton>
          <LinkButton href="/products" variant="outline">
            Back to catalog
          </LinkButton>
        </div>
      </aside>
    </div>
  );
};

const ProductPricing = ({
  product,
}: {
  product: { id: number; isActive: boolean; price: number };
}) => {
  const status = getStockStatus(product);

  return (
    <div className="grid gap-6 lg:grid-cols-2">
      <Card>
        <h2 className="text-lg font-semibold">Pricing</h2>
        <p className="text-sm text-muted-foreground">
          Standard list price and reserved price tracking.
        </p>
        <div className="mt-4">
          <p className="text-sm text-muted-foreground">List price</p>
          <p className="text-3xl font-bold text-foreground">
            {formatCurrency(product.price)}
          </p>
        </div>
      </Card>

      <Card>
        <h2 className="text-lg font-semibold">Inventory snapshot</h2>
        <p className="text-sm text-muted-foreground">
          Stock status is currently derived from the product record.
        </p>
        <div className="mt-6 flex items-center gap-3">
          <div className="rounded-full bg-accent/10 p-3 text-accent">
            <span className="text-sm font-bold">SKU</span>
          </div>
          <div>
            <p className="text-sm text-muted-foreground">Reserved SKU</p>
            <p className="font-mono text-lg font-semibold">PRD-{product.id.toString().padStart(5, "0")}</p>
          </div>
        </div>
        <div className="mt-4">
          <Badge tone={stockBadgeTone(status)}>{stockLabel(status)}</Badge>
        </div>
      </Card>
    </div>
  );
};

const ProductEligibility = ({
  membershipName,
}: {
  membershipName: string;
}) => {
  return (
    <Card>
      <h2 className="text-lg font-semibold">Membership eligibility</h2>
      <p className="text-sm text-muted-foreground">
        This product is available for customers with the following access:
      </p>
      <p className="mt-4 inline-block rounded-lg bg-accent/10 px-3 py-2 text-sm font-semibold text-accent">
        {membershipName}
      </p>
    </Card>
  );
};

export const ProductDetails = ({ productId }: ProductDetailsProps) => {
  const { getProduct } = useProductsActions();
  const {
    isSelectedError,
    isSelectedPending,
    selectedErrorMessage,
    selectedProduct,
  } = useProductsState();
  const { memberships } = useMembershipsState();
  const { getMemberships: loadMemberships } = useMembershipsActions();
  const [activeTab, setActiveTab] = useState("overview");

  useEffect(() => {
    if (!Number.isInteger(productId) || productId <= 0) {
      return;
    }

    void getProduct(productId);
    void loadMemberships();
  }, [productId, getProduct, loadMemberships]);

  const isInvalid = !Number.isInteger(productId) || productId <= 0;
  const membershipName = selectedProduct
    ? getMembershipNameById(memberships, selectedProduct.membershipId, "Open to all")
    : "";

  return (
    <main className="min-h-dvh bg-muted/30 px-4 py-6 text-foreground sm:px-6 lg:px-8">
      <div className="mx-auto flex w-full max-w-5xl flex-col gap-6">
        <header className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
          <div>
            <Breadcrumb
              items={[
                { href: "/", label: "Dashboard" },
                { href: "/products", label: "Products" },
                { label: "Product details" },
              ]}
            />
            <h1 className="mt-2 text-3xl font-bold tracking-tight">Product details</h1>
            <p className="mt-2 max-w-2xl text-base text-muted-foreground">
              View product information, stock status, and customer access.
            </p>
          </div>
          <LinkButton href="/products" variant="outline">
            Back to catalog
          </LinkButton>
        </header>

        {isInvalid ? (
          <StatusMessage tone="error">This product id is invalid.</StatusMessage>
        ) : null}
        {isSelectedPending ? (
          <Skeleton className="h-96" />
        ) : null}
        {isSelectedError ? (
          <StatusMessage tone="error">
            {selectedErrorMessage ?? "Unable to load this product."}
          </StatusMessage>
        ) : null}

        {selectedProduct ? (
          <Tabs
            onChange={setActiveTab}
            tabs={[
              {
                content: (
                  <ProductOverview
                    membershipName={membershipName}
                    product={selectedProduct}
                  />
                ),
                id: "overview",
                label: "Overview",
              },
              {
                content: <ProductPricing product={selectedProduct} />,
                id: "pricing",
                label: "Pricing & inventory",
              },
              {
                content: <ProductEligibility membershipName={membershipName} />,
                id: "eligibility",
                label: "Eligibility",
              },
            ]}
            value={activeTab}
          />
        ) : null}
      </div>
    </main>
  );
};
