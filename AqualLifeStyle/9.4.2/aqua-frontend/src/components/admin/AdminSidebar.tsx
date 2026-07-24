"use client";

import {
  Building2,
  HandCoins,
  KeyRound,
  LayoutDashboard,
  Network,
  PiggyBank,
  Route,
  ShieldCheck,
  UserCheck,
  Users,
  UsersRound,
} from "lucide-react";
import Link from "next/link";
import { usePathname } from "next/navigation";

import { useAuthState } from "@/src/providers";
import { cn } from "@/src/shared/lib/utils";

const adminLinks = [
  { href: "/admin/dashboard", icon: LayoutDashboard, label: "Dashboard", permission: null },
  { href: "/admin/customers", icon: Users, label: "Customer accounts", permission: "Aqua.Admin.Customers.View" },
  { href: "/admin/users", icon: ShieldCheck, label: "User accounts & access", permission: "Aqua.Admin.Users.View" },
  { href: "/admin/access-levels", icon: KeyRound, label: "Access levels", permission: "Pages.Roles" },
  { href: "/admin/tenants", icon: Building2, label: "Areas", permission: "Aqua.Admin.Tenants.View" },
  { href: "/admin/area-leaders", icon: Network, label: "Area leaders", permission: "Aqua.Admin.AreaLeaders.View" },
  { href: "/admin/facilitators", icon: UserCheck, label: "Facilitators", permission: "Aqua.Admin.Facilitators.View" },
  { href: "/admin/members", icon: UsersRound, label: "Club members", permission: "Aqua.Admin.Members.View" },
  { href: "/admin/programme-participations", icon: Route, label: "Programme participation", permission: "Aqua.Admin.ProgrammeParticipations.View" },
  { href: "/admin/weekly-earnings", icon: HandCoins, label: "Weekly earnings", permission: "Aqua.Admin.Commissions.View" },
  { href: "/admin/savings", icon: PiggyBank, label: "Savings accounts", permission: "Aqua.Admin.Savings.View" },
  { href: "/admin/loans", icon: HandCoins, label: "Loan agreements", permission: "Aqua.Admin.Loans.View" },
  { href: "/admin/entry-commitments", icon: HandCoins, label: "Entry commitments", permission: "Aqua.Admin.EntryMonthlyObligations.View" },
] as const;

export const AdminSidebar = () => {
  const pathname = usePathname();
  const { session } = useAuthState();
  const permissions = session?.user?.permissions ?? [];
  const visibleLinks = adminLinks.filter(
    ({ permission }) => permission === null || permissions.includes(permission),
  );

  const isActive = (href: string) => pathname === href || pathname.startsWith(`${href}/`);

  return (
    <aside className="border-b border-border bg-card lg:min-h-[calc(100dvh-4rem)] lg:w-64 lg:shrink-0 lg:border-b-0 lg:border-r">
      <div className="lg:sticky lg:top-16 lg:p-4">
        <div className="hidden px-3 pb-3 pt-1 lg:block">
          <p className="text-xs font-semibold uppercase tracking-[0.18em] text-muted-foreground">
            Administration
          </p>
        </div>
        <nav aria-label="Administration navigation" className="flex gap-1 overflow-x-auto p-2 lg:flex-col lg:overflow-visible lg:p-0">
          {visibleLinks.map((link) => {
            const active = isActive(link.href);

            return (
              <Link
                aria-current={active ? "page" : undefined}
                className={cn(
                  "flex shrink-0 items-center gap-2 rounded-lg px-3 py-2.5 text-sm font-semibold transition",
                  active
                    ? "bg-accent/10 text-accent"
                    : "text-muted-foreground hover:bg-muted hover:text-foreground",
                )}
                href={link.href}
                key={link.href}
              >
                <link.icon aria-hidden="true" className="size-4" />
                {link.label}
              </Link>
            );
          })}
        </nav>
      </div>
    </aside>
  );
};
