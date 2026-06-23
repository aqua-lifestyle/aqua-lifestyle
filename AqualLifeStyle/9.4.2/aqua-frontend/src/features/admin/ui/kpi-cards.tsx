import {
  Banknote,
  MessageSquareText,
  PiggyBank,
  ShoppingBag,
  Users,
} from "lucide-react";

import { MetricCard } from "@/src/components/dashboard/metric-card";
import { formatCurrency, type AdminDashboardData } from "../model/dashboard";

type KpiCardsProps = {
  isLoading?: boolean;
  stats: AdminDashboardData["stats"];
};

export const KpiCards = ({ isLoading, stats }: KpiCardsProps) => (
  <section aria-label="Platform overview" className="grid gap-4 sm:grid-cols-2 xl:grid-cols-5">
    <MetricCard icon={Users} isLoading={isLoading} label="Total members" value={stats.totalMembers} />
    <MetricCard icon={ShoppingBag} isLoading={isLoading} label="Total orders" value={stats.totalOrders} />
    <MetricCard icon={PiggyBank} isLoading={isLoading} label="Total savings" value={formatCurrency(stats.totalSavings)} />
    <MetricCard icon={MessageSquareText} isLoading={isLoading} label="Total enquiries" value={stats.totalEnquiries} />
    <MetricCard icon={Banknote} isLoading={isLoading} label="Total revenue" value={formatCurrency(stats.totalRevenue)} />
  </section>
);
