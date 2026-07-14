"use client";

import { ArrowRight, PackageOpen } from "lucide-react";

import { Badge, Button, Card, EmptyState, LinkButton } from "@/src/shared/ui";
import { formatCurrency } from "../model/dashboard";

export type AreaLeaderOrder = {
  amount: number;
  createdAt: string;
  customerName: string;
  id: number;
  status: number;
  statusText: string;
};

type OrderManagementProps = {
  isProcessing?: boolean;
  onProcess: (id: number) => void;
  orders: AreaLeaderOrder[];
};

const statusTone = (status: number): "neutral" | "info" | "success" | "error" => {
  if (status === 2) return "success";
  if (status === 3) return "error";
  if (status === 1) return "info";
  return "neutral";
};

export const OrderManagement = ({ isProcessing, onProcess, orders }: OrderManagementProps) => (
  <Card className="flex h-full flex-col">
    <div className="flex items-start justify-between gap-4">
      <div>
        <h2 className="text-lg font-semibold">Recent orders</h2>
        <p className="text-sm text-muted-foreground">Latest orders from your Area Space.</p>
      </div>
      <LinkButton href="/area-leader/orders" size="sm" variant="ghost">
        View all <ArrowRight className="size-4" />
      </LinkButton>
    </div>

    {orders.length === 0 ? (
      <EmptyState className="mt-4" description="New orders will appear here." icon={PackageOpen} title="No orders yet" />
    ) : (
      <ul className="mt-4 divide-y divide-border">
        {orders.slice(0, 5).map((order) => (
          <li className="flex flex-col gap-3 py-4 first:pt-0 sm:flex-row sm:items-center sm:justify-between" key={order.id}>
            <div className="min-w-0">
              <div className="flex flex-wrap items-center gap-2">
                <p className="font-semibold">Order #{order.id}</p>
                <Badge tone={statusTone(order.status)}>{order.statusText}</Badge>
              </div>
              <p className="mt-1 truncate text-sm text-muted-foreground">{order.customerName}</p>
            </div>
            <div className="flex items-center justify-between gap-3 sm:justify-end">
              <span className="font-semibold">{formatCurrency(order.amount)}</span>
              {order.status < 2 ? (
                <Button disabled={isProcessing} onClick={() => onProcess(order.id)} size="sm">
                  Process
                </Button>
              ) : null}
            </div>
          </li>
        ))}
      </ul>
    )}
  </Card>
);
