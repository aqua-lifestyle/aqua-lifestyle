import { BadgeDollarSign, ClipboardClock, Package, UserCheck, Users } from "lucide-react";

import { MetricCard } from "@/src/components/dashboard/metric-card";
import { formatCurrency } from "../model/dashboard";

export type AreaLeaderStats = {
  pendingApprovals: number;
  totalFacilitators: number;
  totalMembers: number;
  totalOrders: number;
  totalRevenue: number;
};

type KpiCardsProps = {
  isLoading?: boolean;
  stats: AreaLeaderStats;
};

export const KpiCards = ({ isLoading, stats }: KpiCardsProps) => (
  <section aria-label="Area performance" className="grid gap-4 sm:grid-cols-2 xl:grid-cols-5">
    <MetricCard icon={Users} isLoading={isLoading} label="Total members" value={stats.totalMembers} />
    <MetricCard icon={Package} isLoading={isLoading} label="Orders this month" value={stats.totalOrders} />
    <MetricCard icon={BadgeDollarSign} isLoading={isLoading} label="Revenue this month" value={formatCurrency(stats.totalRevenue)} />
    <MetricCard icon={UserCheck} isLoading={isLoading} label="Active facilitators" value={stats.totalFacilitators} />
    <MetricCard icon={ClipboardClock} isLoading={isLoading} label="Pending approvals" value={stats.pendingApprovals} />
  </section>
);
