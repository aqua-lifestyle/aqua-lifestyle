import { MessageSquare, Package, UserPlus } from "lucide-react";

import { ActivityFeed } from "@/src/components/dashboard/activity-feed";

export type AreaLeaderActivity = {
  description: string;
  id: string;
  kind: "enquiry" | "member" | "order";
  meta: string;
  timestamp: string;
  title: string;
};

type RecentActivityProps = {
  activities: AreaLeaderActivity[];
};

const icons = {
  enquiry: MessageSquare,
  member: UserPlus,
  order: Package,
} as const;

export const RecentActivity = ({ activities }: RecentActivityProps) => (
  <ActivityFeed
    items={activities.slice(0, 8).map((activity) => ({
      ...activity,
      icon: icons[activity.kind],
    }))}
  />
);
