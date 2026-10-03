import { Card, Badge } from "@/src/shared/ui";
import { formatCurrency, type AdminDashboardData } from "../model/dashboard";

type OrderAnalyticsProps = {
  orders: AdminDashboardData["orders"];
};

const formatDate = (value: string) =>
  new Intl.DateTimeFormat("en-ZA", { dateStyle: "medium" }).format(new Date(value));

export const OrderAnalytics = ({ orders }: OrderAnalyticsProps) => (
  <Card className="overflow-hidden p-0">
    <div className="border-b border-border p-5">
      <h2 className="text-lg font-semibold">Order analytics</h2>
      <p className="text-sm text-muted-foreground">This month&apos;s volume, reserved value, and latest orders.</p>
    </div>
    <div className="p-5">
      <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
        <div className="rounded-lg bg-accent/10 p-4">
          <p className="text-xs font-medium uppercase tracking-wide text-accent-ink">Orders this month</p>
          <p className="mt-1 break-words text-2xl font-bold tabular-nums">{orders.monthVolume}</p>
        </div>
        <div className="rounded-lg bg-success/10 p-4">
          <p className="text-xs font-medium uppercase tracking-wide text-success">Reserved value this month</p>
          <p className="mt-1 break-words text-2xl font-bold tabular-nums">{formatCurrency(orders.monthReservedValue)}</p>
        </div>
      </div>
      <h3 className="mt-5 text-sm font-semibold">Recent orders</h3>
      {orders.recent.length > 0 ? (
        <ul className="mt-3 divide-y divide-border">
          {orders.recent.map((order) => (
            <li className="grid gap-3 py-3 first:pt-0 sm:grid-cols-[minmax(0,1fr)_auto]" key={order.id}>
              <div className="min-w-0">
                <p className="break-words text-sm font-semibold">#{order.id} · {order.memberName}</p>
                <p className="text-xs text-muted-foreground">{formatDate(order.createdAt)}</p>
              </div>
              <div className="sm:text-right">
                <p className="break-words text-sm font-semibold tabular-nums">{formatCurrency(order.amount)}</p>
                <Badge tone={order.status.toLowerCase().includes("complete") ? "success" : "warning"}>{order.status}</Badge>
              </div>
            </li>
          ))}
        </ul>
      ) : (
        <p className="mt-4 text-sm text-muted-foreground">No orders available.</p>
      )}
    </div>
  </Card>
);
