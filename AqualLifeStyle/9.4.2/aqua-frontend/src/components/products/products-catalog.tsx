"use client";

import {
  Grid3X3,
  Package,
  Plus,
  Table as TableIcon,
} from "lucide-react";
import { useEffect, useMemo, useState } from "react";

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
  DataTable,
  EmptyState,
  LinkButton,
  SelectField,
  Skeleton,
  StatusMessage,
} from "@/src/shared/ui";
import { cn } from "@/src/shared/lib/utils";

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

const formatCurrency = (amount: number) =>
  new Intl.NumberFormat("en-ZA", {
    currency: "ZAR",
    style: "currency",
  }).format(amount);

const searchFn = (
  product: { membershipId: number | null; name: string },
  query: string,
) => {
  const nameMatch = product.name.toLowerCase().includes(query);
  const membershipMatch = product.membershipId
    ? String(product.membershipId).toLowerCase().includes(query)
    : false;
  return nameMatch || membershipMatch;
};

export const ProductsCatalog = () => {
  const [statusFilter, setStatusFilter] = useState<string>("all");
  const [membershipFilter, setMembershipFilter] = useState<string>("all");
  const [view, setView] = useState<"table" | "cards">("cards");
  const { getProducts } = useProductsActions();
  const { getMemberships } = useMembershipsActions();
  const {
    errorMessage,
    isError,
    isPending,
    products,
  } = useProductsState();
  const { memberships } = useMembershipsState();

  useEffect(() => {
    void getProducts();
    void getMemberships();
  }, [getProducts, getMemberships]);

  const filteredProducts = useMemo(() => {
    return products.filter((product) => {
      const matchesStatus =
        statusFilter === "all" ||
        (statusFilter === "available" && product.isActive) ||
        (statusFilter === "unavailable" && !product.isActive);

      const matchesMembership =
        membershipFilter === "all" ||
        (membershipFilter === "none" && product.membershipId === null) ||
        product.membershipId === Number(membershipFilter);

      return matchesStatus && matchesMembership;
    });
  }, [membershipFilter, products, statusFilter]);

  const availableCount = products.filter((product) => product.isActive).length;
  const unavailableCount = products.length - availableCount;

  const tableColumns = [
    {
      header: "Product",
      key: "name",
      render: (product: typeof filteredProducts[number]) => {
        const gradient = getProductPlaceholder(product.id);

        return (
          <div className="flex items-center gap-3">
            <div
              className={cn(
                "flex size-10 shrink-0 items-center justify-center rounded-lg bg-gradient-to-br text-xs font-bold text-white shadow-sm",
                gradient,
              )}
            >
              {product.name.charAt(0).toUpperCase()}
            </div>
            <p className="font-semibold text-foreground">{product.name}</p>
          </div>
        );
      },
      sortable: true,
    },
    {
      header: "Price",
      key: "price",
      render: (product: typeof filteredProducts[number]) => (
        <span className="font-medium text-foreground">{formatCurrency(product.price)}</span>
      ),
      sortable: true,
    },
    {
      header: "Access",
      key: "membershipId",
      render: (product: typeof filteredProducts[number]) => (
        <span className="text-sm text-muted-foreground">
          {getMembershipNameById(memberships, product.membershipId, "Open to all")}
        </span>
      ),
      sortable: true,
    },
    {
      header: "Catalog availability",
      key: "isActive",
      render: (product: typeof filteredProducts[number]) => (
        <Badge tone={product.isActive ? "success" : "neutral"}>
          {product.isActive ? "Available" : "Unavailable"}
        </Badge>
      ),
      sortable: true,
    },
    {
      header: "Actions",
      key: "actions",
      render: (product: typeof filteredProducts[number]) => (
        <LinkButton href={`/products/${product.id}`} size="sm" variant="outline">
          Open
        </LinkButton>
      ),
    },
  ];

  return (
    <main className="min-h-dvh bg-muted/30 px-4 py-6 text-foreground sm:px-6 lg:px-8">
      <div className="mx-auto flex w-full max-w-7xl flex-col gap-6">
        <header className="flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
          <div>
            <Breadcrumb
              items={[
                { href: "/", label: "Dashboard" },
                { label: "Products" },
              ]}
            />
            <h1 className="mt-2 text-3xl font-bold tracking-tight">Products</h1>
            <p className="mt-2 max-w-2xl text-base text-muted-foreground">
              Product catalog, pricing, and access tier assignment.
            </p>
          </div>
          <LinkButton href="/products" variant="primary">
            <Plus className="size-4" />
            Create product
          </LinkButton>
        </header>

        <section className="grid gap-4 sm:grid-cols-3">
          <Card className="flex items-center gap-4">
            <div className="rounded-full bg-accent/10 p-3 text-accent">
              <Package className="size-6" />
            </div>
            <div>
              <p className="text-sm text-muted-foreground">Total products</p>
              <p className="text-2xl font-bold">{products.length}</p>
            </div>
          </Card>
          <Card className="flex items-center gap-4">
            <div className="rounded-full bg-success/10 p-3 text-success">
              <Package className="size-6" />
            </div>
            <div>
              <p className="text-sm text-muted-foreground">Available</p>
              <p className="text-2xl font-bold">{availableCount}</p>
            </div>
          </Card>
          <Card className="flex items-center gap-4">
            <div className="rounded-full bg-warning/10 p-3 text-warning">
              <Package className="size-6" />
            </div>
            <div>
              <p className="text-sm text-muted-foreground">Unavailable</p>
              <p className="text-2xl font-bold">{unavailableCount}</p>
            </div>
          </Card>
        </section>

        {isPending ? (
          <Skeleton className="h-96" />
        ) : isError ? (
          <StatusMessage tone="error">
            {errorMessage ?? "Unable to load products."}
          </StatusMessage>
        ) : products.length === 0 ? (
          <EmptyState
            description="No products have been configured for this tenant yet."
            icon={Package}
            title="No products available"
          />
        ) : (
          <Card className="flex flex-col gap-4">
            <div className="flex flex-col gap-4 lg:flex-row lg:items-end lg:justify-between">
              <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
                <SelectField
                  label="Catalog availability"
                  name="statusFilter"
                  onChange={(event) => setStatusFilter(event.target.value)}
                  value={statusFilter}
                >
                  <option value="all">All products</option>
                  <option value="available">Available</option>
                  <option value="unavailable">Unavailable</option>
                </SelectField>
                <SelectField
                  label="Access tier"
                  name="membershipFilter"
                  onChange={(event) => setMembershipFilter(event.target.value)}
                  value={membershipFilter}
                >
                  <option value="all">All tiers</option>
                  <option value="none">Open to all</option>
                  {memberships.map((membership) => (
                    <option key={membership.id} value={membership.id}>
                      {membership.name}
                    </option>
                  ))}
                </SelectField>
              </div>
              <div className="flex items-center gap-2">
                <button
                  aria-label="Table view"
                  className={cn(
                    "rounded-lg p-2 transition",
                    view === "table"
                      ? "bg-accent text-white"
                      : "bg-muted text-muted-foreground hover:bg-muted-foreground/20",
                  )}
                  onClick={() => setView("table")}
                  type="button"
                >
                  <TableIcon className="size-5" />
                </button>
                <button
                  aria-label="Card view"
                  className={cn(
                    "rounded-lg p-2 transition",
                    view === "cards"
                      ? "bg-accent text-white"
                      : "bg-muted text-muted-foreground hover:bg-muted-foreground/20",
                  )}
                  onClick={() => setView("cards")}
                  type="button"
                >
                  <Grid3X3 className="size-5" />
                </button>
              </div>
            </div>

            {view === "table" ? (
              <DataTable
                columns={tableColumns}
                data={filteredProducts}
                emptyState="No products match these filters."
                keyExtractor={(product) => product.id}
                pageSize={10}
                searchFn={searchFn}
              />
            ) : (
              <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
                {filteredProducts.length === 0 ? (
                  <p className="col-span-full py-8 text-center text-sm text-muted-foreground">
                    No products match these filters.
                  </p>
                ) : (
                  filteredProducts.map((product) => {
                    const gradient = getProductPlaceholder(product.id);

                    return (
                      <div
                        key={product.id}
                        className="group overflow-hidden rounded-xl border border-border bg-card shadow-sm transition hover:-translate-y-0.5 hover:shadow-md"
                      >
                        <div
                          className={cn(
                            "relative flex h-36 items-center justify-center bg-gradient-to-br text-white transition",
                            gradient,
                          )}
                        >
                          <span className="text-4xl font-bold">
                            {product.name.charAt(0).toUpperCase()}
                          </span>
                          <div className="absolute left-3 top-3">
                            <Badge
                              className="bg-white/90 text-foreground"
                              tone={product.isActive ? "success" : "neutral"}
                            >
                              {product.isActive ? "Available" : "Unavailable"}
                            </Badge>
                          </div>
                        </div>
                        <div className="p-4">
                          <div className="flex items-start justify-between gap-3">
                            <div className="min-w-0">
                              <h3 className="truncate font-semibold text-foreground">
                                {product.name}
                              </h3>
                              <p className="text-sm text-muted-foreground">
                                {getMembershipNameById(
                                  memberships,
                                  product.membershipId,
                                  "Open to all",
                                )}
                              </p>
                            </div>
                            <span className="font-bold text-foreground">
                              {formatCurrency(product.price)}
                            </span>
                          </div>
                          <div className="mt-4">
                            <LinkButton
                              href={`/products/${product.id}`}
                              size="sm"
                              variant="outline"
                            >
                              View details
                            </LinkButton>
                          </div>
                        </div>
                      </div>
                    );
                  })
                )}
              </div>
            )}
          </Card>
        )}
      </div>
    </main>
  );
};
