"use client";

import { Building2, LayoutDashboard, Package, UserCheck } from "lucide-react";
import Link from "next/link";
import { usePathname } from "next/navigation";

import { cn } from "@/src/shared/lib/utils";

const links = [
  { href: "/area-leader/dashboard", icon: LayoutDashboard, label: "Overview" },
  { href: "/area-leader/orders", icon: Package, label: "Orders" },
  { href: "/area-leader/facilitators", icon: UserCheck, label: "Facilitators" },
  { href: "/area-leader/area-space", icon: Building2, label: "Area Space" },
];

export const AreaLeaderNav = () => {
  const pathname = usePathname();

  return (
    <div className="border-b border-border bg-card/95 backdrop-blur">
      <nav
        aria-label="Area Leader navigation"
        className="mx-auto flex max-w-7xl gap-1 overflow-x-auto px-4 py-2 sm:px-6 lg:px-8"
      >
        {links.map((link) => {
          const active =
            pathname === link.href || pathname.startsWith(`${link.href}/`);
          const Icon = link.icon;

          return (
            <Link
              aria-current={active ? "page" : undefined}
              className={cn(
                "flex shrink-0 items-center gap-2 rounded-lg px-3 py-2 text-sm font-semibold transition",
                active
                  ? "bg-accent/10 text-accent"
                  : "text-muted-foreground hover:bg-muted hover:text-foreground",
              )}
              href={link.href}
              key={link.href}
            >
              <Icon className="size-4" />
              {link.label}
            </Link>
          );
        })}
      </nav>
    </div>
  );
};
