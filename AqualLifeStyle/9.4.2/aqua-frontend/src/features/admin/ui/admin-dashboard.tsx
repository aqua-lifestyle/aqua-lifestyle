"use client";

import { Activity, Building2, ClipboardCheck, Database, HandCoins, KeyRound, Network, RefreshCw, ShieldCheck, UserCheck, Users, UsersRound } from "lucide-react";
import Link from "next/link";
import { useEffect, useMemo } from "react";

import {
  useAreaLeadersActions,
  useAreaLeadersState,
  useAuthState,
  useCustomersActions,
  useCustomersState,
  useEnquiriesActions,
  useEnquiriesState,
  useFacilitatorsActions,
  useFacilitatorsState,
  useMembershipsActions,
  useMembershipsState,
  useOrderIntentsActions,
  useOrderIntentsState,
  useReferralsActions,
  useReferralsState,
  useSystemHealthActions,
  useSystemHealthState,
} from "@/src/providers";
import { Badge, Button, Card, Skeleton, StatusMessage } from "@/src/shared/ui";
import { buildAdminDashboard, formatCurrency } from "../model/dashboard";
import { isSystemAdmin } from "@/src/shared/auth/roles";
import { useRecoveryMode } from "@/src/shared/hooks/recovery-context";
import { KpiCards } from "./kpi-cards";
import { MemberAnalytics } from "./member-analytics";
import { OrderAnalytics } from "./order-analytics";
import { RecentActivity } from "./recent-activity";

export const AdminDashboard = () => {
  const auth = useAuthState();
  const isRecovery = useRecoveryMode();
  const customerActions = useCustomersActions();
  const enquiryActions = useEnquiriesActions();
  const membershipActions = useMembershipsActions();
  const orderActions = useOrderIntentsActions();
  const leaderActions = useAreaLeadersActions();
  const facilitatorActions = useFacilitatorsActions();
  const referralActions = useReferralsActions();
  const healthActions = useSystemHealthActions();
  const customerState = useCustomersState();
  const enquiryState = useEnquiriesState();
  const membershipState = useMembershipsState();
  const orderState = useOrderIntentsState();
  const leaderState = useAreaLeadersState();
  const facilitatorState = useFacilitatorsState();
  const referralState = useReferralsState();
  const healthState = useSystemHealthState();
  const permissions = auth.session?.user?.permissions ?? [];
  const isPlatformAdministrator = !auth.session?.user?.tenantId;

  const loadDashboard = () => {
    if (!auth.isAuthenticated || !isSystemAdmin(auth.session?.user?.role)) return;

    if (isRecovery || isPlatformAdministrator) {
      void healthActions.checkHealth();
      return;
    }
    void Promise.all([
      customerActions.getCustomers(),
      enquiryActions.getEnquiries(),
      membershipActions.getMemberships(),
      orderActions.getOrderIntents(),
      leaderActions.getAreaLeaders(),
      facilitatorActions.getFacilitators(),
      referralActions.getReferrals(),
      healthActions.checkHealth(),
    ]);
  };

  useEffect(() => {
    if (auth.isReady) loadDashboard();
    // Provider actions are memoized. Loading is intentionally keyed to the authenticated session.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [auth.isAuthenticated, auth.isReady, auth.session?.user?.id, isRecovery]);

  const requiredFailed = !isRecovery && !isPlatformAdministrator && (
    customerState.isLoadError || enquiryState.isLoadError ||
    membershipState.isError || orderState.isLoadError
  );
  const requiredSettled = isRecovery || isPlatformAdministrator ||
    (customerState.isLoadSuccess || customerState.isLoadError) &&
    (enquiryState.isLoadSuccess || enquiryState.isLoadError) &&
    (membershipState.isSuccess || membershipState.isError) &&
    (orderState.isLoadSuccess || orderState.isLoadError);
  const isLoading = isRecovery ? healthState.isPending : !requiredSettled || customerState.isLoadPending ||
    enquiryState.isLoadPending || membershipState.isPending || orderState.isLoadPending;

  const dashboard = useMemo(() => isRecovery ? null : buildAdminDashboard({
    areaLeaderCount: leaderState.areaLeaders.length,
    customers: customerState.customers,
    enquiries: enquiryState.enquiries,
    facilitatorCount: facilitatorState.facilitators.length,
    memberships: membershipState.memberships,
    orders: orderState.orderIntents,
    referrals: referralState.referrals,
  }), [
    isRecovery, customerState.customers, enquiryState.enquiries, facilitatorState.facilitators.length,
    leaderState.areaLeaders.length, membershipState.memberships, orderState.orderIntents,
    referralState.referrals,
  ]);
  const managementLinks = [
    { href: "/admin/customers", icon: Users, label: "Customer accounts", permission: "Aqua.Admin.Customers.View", summary: "Welcome customers and manage their account details." },
    { href: "/admin/users", icon: ShieldCheck, label: "User accounts & access", permission: "Aqua.Admin.Users.View", summary: "Create accounts, assign access, and reset passwords." },
    { href: "/admin/access-levels", icon: KeyRound, label: "Access levels", permission: "Pages.Roles", summary: "Review the account types and responsibilities available." },
    { href: "/admin/tenants", icon: Building2, label: "Areas", permission: "Aqua.Admin.Tenants.View", summary: "Create area workspaces and appoint their leaders." },
    { href: "/admin/area-leaders", icon: Network, label: "Area leaders", permission: "Aqua.Admin.AreaLeaders.View", summary: "Review applications and manage progression." },
    { href: "/admin/facilitators", icon: UserCheck, label: "Facilitators", permission: "Aqua.Admin.Facilitators.View", summary: "Approve facilitators and monitor their network." },
    { href: "/admin/members", icon: UsersRound, label: "Club members", permission: "Aqua.Admin.Members.View", summary: "Maintain profiles, plans, and account access." },
    { href: "/admin/weekly-sales-reviews", icon: ClipboardCheck, label: "Weekly sales reviews", permission: "Aqua.Admin.Commissions.ReviewAQGreenWeeklySalesEligibility", summary: "Verify closed-week AQGreen sales evidence and quantities." },
    { href: "/admin/weekly-earnings", icon: HandCoins, label: "Weekly earnings", permission: "Aqua.Admin.Commissions.View", summary: "Review calculated AQGreen and Onyx network earnings." },
  ].filter((item) => permissions.includes(item.permission));

  return (
    <main className="min-h-[calc(100dvh-4rem)] bg-muted/30 px-4 py-6 text-foreground sm:px-6 lg:px-8">
      <div className="mx-auto flex max-w-7xl flex-col gap-6">
        <header className="flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
          <div className="min-w-0 flex-1">
            <div className="flex flex-wrap items-center gap-2">
              <p className="text-sm font-semibold text-accent">Administration</p>
              <Badge tone="success">Secure administrator workspace</Badge>
            </div>
            <h1 className="mt-1 text-3xl font-bold tracking-tight sm:text-4xl">Administration overview</h1>
            <p className="mt-2 max-w-2xl text-muted-foreground">
              Manage people, areas, membership access, and leadership responsibilities from one place.
            </p>
          </div>
          <Button className="shrink-0" disabled={isLoading} onClick={loadDashboard} variant="secondary">
            <RefreshCw className={`size-4 ${isLoading ? "animate-spin" : ""}`} />
            {isRecovery ? "Refresh system status" : "Refresh data"}
          </Button>
        </header>

        {isRecovery ? <StatusMessage tone="warning">
          Restricted recovery mode is active. Operational data, membership changes, payments,
          messaging and financial processing are unavailable. Management actions are disabled.
        </StatusMessage> : null}

        {isRecovery ? <Card>
          <h2 className="font-semibold">System status</h2>
          <p className="mt-2">{healthState.isPending ? "Checking…" : healthState.isError ? "Unavailable" : healthState.health?.status ?? "Unavailable"}</p>
          {healthState.isError ? <p className="mt-2 text-sm text-muted-foreground">{healthState.errorMessage}</p> : null}
        </Card> : null}

        {requiredFailed ? (
          <StatusMessage tone="warning">
            Live operational performance information is temporarily unavailable. Management tools remain available below.
          </StatusMessage>
        ) : null}

        <section aria-labelledby="management-heading">
          <h2 className="text-xl font-bold" id="management-heading">Management</h2>
          <p className="mt-1 text-sm text-muted-foreground">{isRecovery ? "These tools are unavailable during restricted recovery." : "Choose an area to continue."}</p>
          <div className="mt-4 grid grid-cols-[repeat(auto-fit,minmax(min(100%,18rem),1fr))] gap-4">{managementLinks.map((item) => {
            const card = <Card className="h-full transition group-hover:border-accent group-hover:shadow-md"><div className="flex items-start gap-3"><div className="rounded-lg bg-accent/10 p-2 text-accent"><item.icon className="size-5" /></div><div><h3 className="font-semibold">{item.label}</h3><p className="mt-1 text-sm text-muted-foreground">{item.summary}</p><p className="mt-3 text-sm font-semibold text-accent">{isRecovery ? "Unavailable during recovery" : `Open ${item.label.toLowerCase()} →`}</p></div></div></Card>;
            return isRecovery ? <div aria-disabled="true" key={item.href}>{card}</div> :
              <Link className="group" href={item.href} key={item.href}>{card}</Link>;
          })}</div>
        </section>

        {dashboard && !isPlatformAdministrator && !requiredFailed ? <KpiCards isLoading={isLoading} stats={dashboard.stats} /> : null}

        {dashboard && !isPlatformAdministrator && !requiredFailed ? <section className="grid grid-cols-[repeat(auto-fit,minmax(min(100%,26rem),1fr))] gap-6">
          {isLoading ? <><Skeleton className="h-96" /><Skeleton className="h-96" /></> : <>
            <MemberAnalytics members={dashboard.members} />
            <OrderAnalytics orders={dashboard.orders} />
          </>}
        </section> : null}

        {dashboard && !isPlatformAdministrator && !requiredFailed ? <section className="grid grid-cols-[repeat(auto-fit,minmax(min(100%,16rem),1fr))] gap-4">
          <Card>
            <div className="flex items-start justify-between"><p className="text-sm text-muted-foreground">Area leaders</p><UsersRound className="size-5 text-accent" /></div>
            <p className="mt-2 break-words text-2xl font-bold tabular-nums">{leaderState.isLoadError ? "Unavailable" : leaderState.isLoadPending || !leaderState.isLoadSuccess ? "Loading…" : dashboard.leaders.total}</p>
            <p className="mt-2 text-xs text-muted-foreground">
              {dashboard.leaders.pendingApplications === null
                ? "Pending applications are not available here"
                : `${dashboard.leaders.pendingApplications} pending`}
            </p>
          </Card>
          <Card>
            <div className="flex items-start justify-between"><p className="text-sm text-muted-foreground">Facilitators</p><UserCheck className="size-5 text-accent" /></div>
            <p className="mt-2 break-words text-2xl font-bold tabular-nums">{facilitatorState.isLoadError ? "Unavailable" : facilitatorState.isLoadPending || !facilitatorState.isLoadSuccess ? "Loading…" : dashboard.people.totalFacilitators}</p>
            <p className="mt-2 text-xs text-muted-foreground">{referralState.isLoadError ? "Referral data unavailable" : referralState.isLoadPending || !referralState.isLoadSuccess ? "Loading referrals…" : `${dashboard.people.recentReferrals} referrals this month`}</p>
          </Card>
          <Card>
            <div className="flex items-start justify-between"><p className="text-sm text-muted-foreground">Savings balance</p><Activity className="size-5 text-success" /></div>
            <p className="mt-2 break-words text-2xl font-bold tabular-nums">
              {dashboard.savings.total === null
                ? "Unavailable"
                : formatCurrency(dashboard.savings.total)}
            </p>
            <p className="mt-2 text-xs text-muted-foreground">
              Open Savings accounts for confirmed balances and projected interest.
            </p>
          </Card>
          <Card>
            <div className="flex items-start justify-between"><p className="text-sm text-muted-foreground">System status</p><Network className="size-5 text-accent" /></div>
            <p className="mt-2 break-words text-lg font-bold">{healthState.isPending ? "Checking…" : healthState.health?.status ?? "Unavailable"}</p>
            <p className="mt-2 flex items-center gap-1 text-xs text-muted-foreground"><Database className="size-3.5" /> Database: {healthState.health?.databaseStatus ?? "Unknown"}</p>
          </Card>
        </section> : null}

        {dashboard && !isPlatformAdministrator && !requiredFailed ? isLoading ? <Skeleton className="h-64" /> : <RecentActivity activity={dashboard.activity} /> : null}
      </div>
    </main>
  );
};
