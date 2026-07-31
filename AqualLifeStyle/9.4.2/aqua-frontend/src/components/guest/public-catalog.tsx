"use client";

import { Package } from "lucide-react";
import { useEffect, useMemo, useState } from "react";

import {
  useProductsActions,
  useProductsState,
} from "@/src/providers";
import {
  Avatar,
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

type CatalogAvailability = "available" | "unavailable";

export const PublicCatalog = () => {
  const [availabilityFilter, setAvailabilityFilter] = useState<
    CatalogAvailability | "all"
  >("all");
  const { getProducts } = useProductsActions();
  const { errorMessage, isError, isPending, products } =
    useProductsState();

  useEffect(() => {
    void getProducts();
  }, [getProducts]);

  const filteredProducts = useMemo(() => {
    return products.filter((product) => {
      return (
        availabilityFilter === "all" ||
        (availabilityFilter === "available" && product.isActive) ||
        (availabilityFilter === "unavailable" && !product.isActive)
      );
    });
  }, [availabilityFilter, products]);

  const tableColumns = [
    {
      header: "Product",
      key: "name",
      render: (product: typeof filteredProducts[number]) => (
        <div className="flex items-center gap-3">
          <Avatar fallback={product.name} size="sm" />
          <div>
            <p className="font-semibold text-foreground">{product.name}</p>
            <p className="text-xs text-muted-foreground">
              Product #{product.id}
            </p>
          </div>
        </div>
      ),
      sortable: true,
    },
    {
      header: "Catalog availability",
      key: "isActive",
      render: (product: typeof filteredProducts[number]) => (
        <Badge tone={product.isActive ? "success" : "error"}>
          {product.isActive ? "Available" : "Unavailable"}
        </Badge>
      ),
      sortable: true,
    },
    {
      header: "Actions",
      key: "actions",
      render: (product: typeof filteredProducts[number]) => (
        <div className="flex items-center gap-2">
          <LinkButton href={`/products/${product.id}`} size="sm" variant="outline">
            View
          </LinkButton>
        </div>
      ),
    },
  ];

  return (
    <main className="min-h-dvh bg-muted/30 px-4 py-6 text-foreground sm:px-6 lg:px-8">
      <div className="mx-auto flex w-full max-w-7xl flex-col gap-6">
        <header className="flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
          <div>
            <Breadcrumb
              items={[{ href: "/", label: "Home" }, { label: "Product Catalog" }]}
            />
            <h1 className="mt-2 text-3xl font-bold tracking-tight">Product Catalog</h1>
            <p className="mt-2 max-w-2xl text-base text-muted-foreground">
              Browse our product catalog. Sign up to place orders.
            </p>
          </div>
        </header>

        {isPending ? (
          <Skeleton className="h-96" />
        ) : isError ? (
          <StatusMessage tone="error">
            {errorMessage ?? "Unable to load products."}
          </StatusMessage>
        ) : products.length === 0 ? (
          <EmptyState
            description="No products available."
            icon={Package}
            title="No products"
          />
        ) : (
          <Card className="flex flex-col gap-4">
            <div className="flex flex-col gap-4 lg:flex-row lg:items-end lg:justify-between">
              <SelectField
                label="Catalog availability"
                name="availabilityFilter"
                onChange={(event) =>
                  setAvailabilityFilter(
                    event.target.value as CatalogAvailability | "all",
                  )
                }
                value={availabilityFilter}
              >
                <option value="all">All products</option>
                <option value="available">Available</option>
                <option value="unavailable">Unavailable</option>
              </SelectField>
            </div>

            <DataTable
              columns={tableColumns}
              data={filteredProducts}
              emptyState="No products match these filters."
              keyExtractor={(product) => product.id}
              pageSize={10}
              searchFn={(product, query) =>
                `${product.name} Product #${product.id}`
                  .toLowerCase()
                  .includes(query.toLowerCase())
              }
            />
          </Card>
        )}
      </div>
    </main>
  );
};
