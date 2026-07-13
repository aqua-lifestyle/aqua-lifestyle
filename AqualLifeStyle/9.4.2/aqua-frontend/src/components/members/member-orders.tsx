"use client";

import { Package } from "lucide-react";
import { useEffect, useMemo } from "react";

import {
  useAuthState,
  useOrderIntentsActions,
  useOrderIntentsState,
} from "@/src/providers";
import {
  Avatar,
  Badge,
  Breadcrumb,
  Card,
  DataTable,
  EmptyState,
  Skeleton,
  StatusMessage,
} from "@/src/shared/ui";

const orderStatusLabel = (value: number) => {
  const labels = ["Pending", "Reserved", "Completed", "Cancelled"];
  return labels[value] ?? `Status ${value}`;
};

const orderStatusTone = (value: number): "neutral" | "info" | "success" | "error" => {
  if (value === 2) return "success";
  if (value === 3) return "error";
  if (value === 1) return "info";
  return "neutral";
};

export const MemberOrders = () => {
  const { getOrderIntents } = useOrderIntentsActions();
  const { orderIntents, isLoadError, isLoadPending, loadErrorMessage } =
    useOrderIntentsState();
  const { session } = useAuthState();
  const hasPermission = session?.user?.permissions?.includes("Pages.Orders") ?? false;

  // ALL hooks before early returns
  useEffect(() => {
    void getOrderIntents();
  }, [getOrderIntents]);

  const customerOrders = useMemo(() => {
    const currentUserId = session?.user?.id ?? null;
    if (currentUserId === null) return [];
    return orderIntents.filter((order) => order.customerId === currentUserId);
  }, [orderIntents, session?.user?.id]);

  const tableColumns = [
    {
      header: "Order",
      key: "id",
      render: (order: typeof customerOrders[number]) => (
        <div className="flex items-center gap-3">
          <Avatar fallback={`O ${order.id}`} size="sm" />
          <div>
            <p className="font-semibold text-foreground">Order #{order.id}</p>
            <p className="text-xs text-muted-foreground">
              Product #{order.productId}
            </p>
          </div>
        </div>
      ),
      sortable: true,
    },
    {
      header: "Status",
      key: "status",
      render: (order: typeof customerOrders[number]) => (
        <Badge tone={orderStatusTone(order.status)}>
          {orderStatusLabel(order.status)}
        </Badge>
      ),
      sortable: true,
    },
    {
      header: "Unit Price",
      key: "unitPrice",
      render: (order: typeof customerOrders[number]) => (
        <span className="text-sm">{order.unitPrice.toFixed(2)}</span>
      ),
      sortable: true,
    },
    {
      header: "Reserved Price",
      key: "reservedPrice",
      render: (order: typeof customerOrders[number]) => (
        <span className="text-sm">{order.reservedPrice.toFixed(2)}</span>
      ),
      sortable: true,
    },
    {
      header: "Created",
      key: "createdAt",
      render: (order: typeof customerOrders[number]) => (
        <span className="text-sm">
          {new Date(order.createdAt).toLocaleDateString()}
        </span>
      ),
      sortable: true,
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
                { href: "/member", label: "Member" },
                { label: "My orders" },
              ]}
            />
            <h1 className="mt-2 text-3xl font-bold tracking-tight">My orders</h1>
            <p className="mt-2 max-w-2xl text-base text-muted-foreground">
              View your order history and track deliveries.
            </p>
          </div>
        </header>

        {isLoadPending ? (
          <Skeleton className="h-96" />
        ) : isLoadError ? (
          <StatusMessage tone="error">
            {loadErrorMessage ?? "Unable to load orders."}
          </StatusMessage>
        ) : customerOrders.length === 0 ? (
          <EmptyState
            description="You have no orders yet."
            icon={Package}
            title="No orders"
          />
        ) : (
          <Card className="flex flex-col gap-4">
            <DataTable
              columns={tableColumns}
              data={customerOrders}
              emptyState="You have no orders."
              keyExtractor={(order) => order.id}
              pageSize={10}
              searchFn={(order, query) =>
                `Order #${order.id} Product #${order.productId}`
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
