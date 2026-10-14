"use client";

import {
  Building2,
  ClipboardCheck,
  HandCoins,
  KeyRound,
  LayoutDashboard,
  Menu,
  Network,
  PiggyBank,
  Route,
  ShieldCheck,
  UserCheck,
  Users,
  UsersRound,
  X,
} from "lucide-react";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { useState } from "react";

import { useAuthState } from "@/src/providers";
import { usePilotStatus } from "@/src/shared/hooks/use-pilot-status";
import { useRecoveryMode } from "@/src/shared/hooks/recovery-context";
import { usePendingProgrammeApprovals } from "@/src/shared/hooks/use-pending-programme-approvals";
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
  { href: "/admin/weekly-sales-reviews", icon: ClipboardCheck, label: "Weekly sales reviews", permission: "Aqua.Admin.Commissions.ReviewAQGreenWeeklySalesEligibility" },
  { href: "/admin/weekly-earnings", icon: HandCoins, label: "Weekly earnings", permission: "Aqua.Admin.Commissions.View" },
  { href: "/admin/savings", icon: PiggyBank, label: "Savings accounts", permission: "Aqua.Admin.Savings.View" },
  { href: "/admin/loans", icon: HandCoins, label: "Loan agreements", permission: "Aqua.Admin.Loans.View" },
  { href: "/admin/entry-commitments", icon: HandCoins, label: "AQGreen commitments", permission: "Aqua.Admin.EntryMonthlyObligations.View" },
] as const;

export const AdminSidebar = () => {
  const { isPilot } = usePilotStatus();
  const isRecovery = useRecoveryMode();
  const pathname = usePathname();
  const [mobileOpen, setMobileOpen] = useState(false);
  const { session } = useAuthState();
  const permissions = session?.user?.permissions ?? [];
  const canViewProgrammeParticipations = permissions.includes(
    "Aqua.Admin.ProgrammeParticipations.View",
  );
  const { summary } = usePendingProgrammeApprovals(
    !isRecovery && canViewProgrammeParticipations,
  );
  const visibleLinks = adminLinks.filter(
    ({ permission, href }) => (permission === null || permissions.includes(permission)) &&
      (!isPilot || ["/admin/dashboard", "/admin/programme-participations", "/admin/weekly-sales-reviews", "/admin/weekly-earnings"].includes(href)),
  );

  const isActive = (href: string) => pathname === href || pathname.startsWith(`${href}/`);

  return (
    <aside className="border-b border-border bg-card lg:min-h-[calc(100dvh-4rem)] lg:w-64 lg:shrink-0 lg:border-b-0 lg:border-r">
      <div className="flex items-center justify-between gap-4 px-4 py-2 lg:hidden">
        <p className="text-sm font-semibold">Administration</p>
        <button
          aria-controls="admin-navigation-panel"
          aria-expanded={mobileOpen}
          aria-label={`${mobileOpen ? "Close" : "Open"} administration navigation`}
          className="inline-flex min-h-11 min-w-11 items-center justify-center rounded-lg text-muted-foreground transition hover:bg-muted hover:text-foreground"
          onClick={() => setMobileOpen((current) => !current)}
          type="button"
        >
          {mobileOpen ? <X aria-hidden="true" className="size-5" /> : <Menu aria-hidden="true" className="size-5" />}
        </button>
      </div>
      <div className="lg:sticky lg:top-16 lg:max-h-[calc(100dvh-4rem)] lg:overflow-y-auto lg:overscroll-contain lg:p-4">
        <div className="hidden px-3 pb-3 pt-1 lg:block">
          <p className="text-xs font-semibold uppercase tracking-[0.18em] text-muted-foreground">
            Administration
          </p>
        </div>
        <nav
          aria-label="Administration navigation"
          className={cn(
            "hidden grid-cols-1 gap-1 border-t border-border p-2 sm:grid-cols-2 md:grid-cols-3 lg:flex lg:flex-col lg:border-t-0 lg:p-0",
            mobileOpen && "grid",
          )}
          id="admin-navigation-panel"
        >
          {visibleLinks.map((link) => {
            const active = isActive(link.href);
            if (isRecovery && link.href !== "/admin/dashboard") return (
              <span aria-disabled="true" className="flex min-h-11 min-w-0 items-center gap-2 rounded-lg px-3 py-2.5 text-sm font-semibold text-muted-foreground opacity-60" key={link.href} title="Unavailable during restricted recovery">
                <link.icon aria-hidden="true" className="size-4" />
                <span className="min-w-0">{link.label}<span className="block text-xs font-normal">Unavailable during recovery</span></span>
              </span>
            );

            return (
              <Link
                aria-current={active ? "page" : undefined}
                className={cn(
                  "flex min-h-11 min-w-0 items-center gap-2 rounded-lg px-3 py-2.5 text-sm font-semibold transition",
                  active
                    ? "bg-accent/10 text-accent-ink"
                    : "text-muted-foreground hover:bg-muted hover:text-foreground",
                )}
                href={link.href}
                key={link.href}
                onClick={() => setMobileOpen(false)}
              >
                <link.icon aria-hidden="true" className="size-4" />
                <span className="min-w-0">{link.label}</span>
                {link.href === "/admin/programme-participations" &&
                summary?.totalCount ? (
                  <span
                    aria-label={`${summary.totalCount} approvals awaiting review`}
                    className="ml-auto inline-flex min-w-5 items-center justify-center rounded-full bg-warning/10 px-1.5 py-0.5 text-xs font-bold text-warning ring-1 ring-warning/20"
                  >
                    {summary.totalCount}
                  </span>
                ) : null}
              </Link>
            );
          })}
        </nav>
      </div>
    </aside>
  );
};
