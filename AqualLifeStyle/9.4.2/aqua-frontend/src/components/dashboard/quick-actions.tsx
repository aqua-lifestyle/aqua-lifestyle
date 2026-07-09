"use client";

import {
  FileText,
  MessageSquare,
  Plus,
  UserPlus,
  type LucideIcon,
} from "lucide-react";
import Link from "next/link";

import { Card } from "@/src/shared/ui";

type QuickAction = {
  description: string;
  href: string;
  icon: LucideIcon;
  label: string;
};

const actions: QuickAction[] = [
  {
    description: "Add a new club member.",
    href: "/customers/register",
    icon: UserPlus,
    label: "Add customer",
  },
  {
    description: "Capture a new sales lead.",
    href: "/enquiries/create",
    icon: MessageSquare,
    label: "Create enquiry",
  },
  {
    description: "Review tier access and benefits.",
    href: "/memberships",
    icon: FileText,
    label: "View memberships",
  },
  {
    description: "See reservation-ready records.",
    href: "/order-intents",
    icon: Plus,
    label: "Order intents",
  },
];

export const QuickActions = () => {
  return (
    <Card>
      <h3 className="text-lg font-semibold">Quick actions</h3>
      <p className="text-sm text-muted-foreground">
        Common tasks to keep the club moving.
      </p>

      <div className="mt-4 grid gap-3 sm:grid-cols-2">
        {actions.map((action) => {
          const Icon = action.icon;

          return (
            <Link
              key={action.href}
              className="group flex items-start gap-3 rounded-xl border border-border p-4 transition hover:border-accent/50 hover:bg-muted"
              href={action.href}
            >
              <div className="rounded-lg bg-accent/10 p-2 text-accent transition group-hover:bg-accent group-hover:text-white">
                <Icon className="size-5" />
              </div>
              <div>
                <p className="text-sm font-semibold text-foreground">{action.label}</p>
                <p className="text-xs text-muted-foreground">{action.description}</p>
              </div>
            </Link>
          );
        })}
      </div>
    </Card>
  );
};
