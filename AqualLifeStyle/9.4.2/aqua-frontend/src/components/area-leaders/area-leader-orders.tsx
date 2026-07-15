"use client";

import { useEffect, useMemo, useState } from "react";

import {
  useAreaLeadersActions,
  useAreaLeadersState,
  useOrderIntentsActions,
  useOrderIntentsState,
} from "@/src/providers";
import {
  Avatar,
  Badge,
  Breadcrumb,
  Card,
  DataTable,
  SelectField,
  Skeleton,
  StatusMessage,
} from "@/src/shared/ui";
import { getOrderStatusLabel, getOrderStatusTone } from "@/src/shared/lib/order-status";

type OrderStatusFilter = "all" | "0" | "1" | "2" | "3";

export const AreaLeaderOrders = () => {
  const [statusFilter, setStatusFilter] = useState<OrderStatusFilter>("all");
  const { getOrderIntents } = useOrderIntentsActions();
  const { orderIntents, isLoadError, isLoadPending, loadErrorMessage } = useOrderIntentsState();
  const { getAreaLeaders } = useAreaLeadersActions();
  const { areaLeaders } = useAreaLeadersState();

  // ALL hooks before early returns
  useEffect(() => {
    void getOrderIntents();
    void getAreaLeaders();
  }, [getOrderIntents, getAreaLeaders]);

  const customerAlias = (customerId: number) => {
    const areaLeader = areaLeaders.find((al) => al.customerId === customerId);
    return areaLeader ? `Area Leader #${areaLeader.id}` : `Customer #${customerId}`;
  };

  const filteredOrders = useMemo(() => {
    return orderIntents.filter((order) => {
      const matchesStatus = statusFilter === "all" || order.status === Number(statusFilter);
      return matchesStatus;
    });
  }, [orderIntents, statusFilter]);

  const tableColumns = [
    {
      header: "Order",
      key: "id",
      render: (order: typeof filteredOrders[number]) => (
        <div className="flex items-center gap-3">
          <Avatar fallback={`O ${order.id}`} size="sm" />
          <div>
            <p className="font-semibold text-foreground">Order #{order.id}</p>
            <p className="text-xs text-muted-foreground">{customerAlias(order.customerId)}</p>
          </div>
        </div>
      ),
      sortable: true,
    },
    {
      header: "Status",
      key: "status",
      render: (order: typeof filteredOrders[number]) => (
        <Badge tone={getOrderStatusTone(order.status)}>
          {getOrderStatusLabel(order.status)}
        </Badge>
      ),
      sortable: true,
    },
    {
      header: "Unit Price",
      key: "unitPrice",
      render: (order: typeof filteredOrders[number]) => (
        <span className="text-sm">{order.unitPrice.toFixed(2)}</span>
      ),
      sortable: true,
    },
    {
      header: "Reserved Price",
      key: "reservedPrice",
      render: (order: typeof filteredOrders[number]) => (
        <span className="text-sm">{order.reservedPrice.toFixed(2)}</span>
      ),
      sortable: true,
    },
    {
      header: "Created",
      key: "createdAt",
      render: (order: typeof filteredOrders[number]) => (
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
                { href: "/area-leader", label: "Area Leaders" },
                { label: "Orders" },
              ]}
            />
            <h1 className="mt-2 text-3xl font-bold tracking-tight">Orders</h1>
            <p className="mt-2 max-w-2xl text-base text-muted-foreground">
              Review and manage orders across your team.
            </p>
          </div>
        </header>

        {isLoadPending ? (
          <Skeleton className="h-96" />
        ) : isLoadError ? (
          <StatusMessage tone="error">
            {loadErrorMessage ?? "Unable to load orders."}
          </StatusMessage>
        ) : (
          <Card className="flex flex-col gap-4">
            <div className="flex flex-col gap-4 lg:flex-row lg:items-end lg:justify-between">
              <SelectField
                label="Status"
                name="statusFilter"
                onChange={(event) => setStatusFilter(event.target.value as OrderStatusFilter)}
                value={statusFilter}
              >
                <option value="all">All statuses</option>
                <option value="0">Pending</option>
                <option value="1">Reserved</option>
                <option value="2">Completed</option>
                <option value="3">Cancelled</option>
              </SelectField>
            </div>

            <DataTable
              columns={tableColumns}
              data={filteredOrders}
              emptyState="No orders match these filters."
              keyExtractor={(order) => order.id}
              pageSize={10}
              searchFn={(order, query) =>
                `Order #${order.id} ${customerAlias(order.customerId)}`
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
