import { MessageSquareText, ShoppingBag, UserPlus } from "lucide-react";

import { ActivityFeed } from "@/src/components/dashboard/activity-feed";
import type { AdminDashboardData } from "../model/dashboard";

type RecentActivityProps = {
  activity: AdminDashboardData["activity"];
};

const icons = {
  enquiry: MessageSquareText,
  member: UserPlus,
  order: ShoppingBag,
};

export const RecentActivity = ({ activity }: RecentActivityProps) => (
  <ActivityFeed items={activity.map((item) => ({ ...item, icon: icons[item.kind] }))} />
);
