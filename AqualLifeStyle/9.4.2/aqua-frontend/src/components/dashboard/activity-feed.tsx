"use client";

import { type LucideIcon } from "lucide-react";

import { Card } from "@/src/shared/ui";

type ActivityItem = {
  description: string;
  icon: LucideIcon;
  id: string;
  meta: string;
  timestamp: string;
  title: string;
};

type ActivityFeedProps = {
  items: ActivityItem[];
};

const formatDate = (date: string) => {
  return new Intl.DateTimeFormat("en-ZA", {
    dateStyle: "medium",
    timeStyle: "short",
  }).format(new Date(date));
};

export const ActivityFeed = ({ items }: ActivityFeedProps) => {
  return (
    <Card className="flex h-full flex-col">
      <h3 className="text-lg font-semibold">Recent activity</h3>
      <p className="text-sm text-muted-foreground">
        Latest movements across the club platform.
      </p>

      {items.length === 0 ? (
        <p className="mt-6 text-sm text-muted-foreground">No recent activity.</p>
      ) : (
        <ul className="mt-4 space-y-4">
          {items.map((item) => {
            const Icon = item.icon;

            return (
              <li
                key={item.id}
                className="flex items-start gap-3 rounded-lg border border-border p-3 transition hover:bg-muted"
              >
                <div className="mt-0.5 rounded-full bg-accent/10 p-2 text-accent">
                  <Icon className="size-4" />
                </div>
                <div className="min-w-0 flex-1">
                  <p className="text-sm font-semibold text-foreground">{item.title}</p>
                  <p className="text-sm text-muted-foreground">{item.description}</p>
                  <div className="mt-1 flex flex-wrap items-center gap-2 text-xs text-muted-foreground">
                    <span className="rounded-full bg-muted px-2 py-0.5">{item.meta}</span>
                    <span>{formatDate(item.timestamp)}</span>
                  </div>
                </div>
              </li>
            );
          })}
        </ul>
      )}
    </Card>
  );
};
