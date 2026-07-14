"use client";

import { Activity, Database, Network, RefreshCw, UserCheck, UsersRound } from "lucide-react";
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
import { Badge, Button, Card, StatusMessage } from "@/src/shared/ui";
import { buildAdminDashboard, formatCurrency } from "../model/dashboard";
import { mockAdminDashboard } from "../model/mock-data";
import { isSystemAdmin } from "./admin-guard";
import { KpiCards } from "./kpi-cards";
import { MemberAnalytics } from "./member-analytics";
import { OrderAnalytics } from "./order-analytics";
import { RecentActivity } from "./recent-activity";

export const AdminDashboard = () => {
  const auth = useAuthState();
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

  const loadDashboard = () => {
    if (!auth.isAuthenticated || !isSystemAdmin(auth.session?.user?.role)) return;

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
  }, [auth.isAuthenticated, auth.isReady, auth.session?.user?.id]);

  const requiredFailed =
    customerState.isLoadError || enquiryState.isLoadError ||
    membershipState.isError || orderState.isLoadError;
  const requiredSettled =
    (customerState.isLoadSuccess || customerState.isLoadError) &&
    (enquiryState.isLoadSuccess || enquiryState.isLoadError) &&
    (membershipState.isSuccess || membershipState.isError) &&
    (orderState.isLoadSuccess || orderState.isLoadError);
  const isLoading = !requiredSettled || customerState.isLoadPending ||
    enquiryState.isLoadPending || membershipState.isPending || orderState.isLoadPending;

  const dashboard = useMemo(() => buildAdminDashboard({
    areaLeaderCount: leaderState.areaLeaders.length,
    customers: customerState.customers,
    enquiries: enquiryState.enquiries,
    facilitatorCount: facilitatorState.facilitators.length,
    failed: requiredFailed,
    fallback: mockAdminDashboard,
    memberships: membershipState.memberships,
    orders: orderState.orderIntents,
    referrals: referralState.referrals,
  }), [
    customerState.customers, enquiryState.enquiries, facilitatorState.facilitators.length,
    leaderState.areaLeaders.length, membershipState.memberships, orderState.orderIntents,
    referralState.referrals, requiredFailed,
  ]);

  return (
    <main className="min-h-[calc(100dvh-4rem)] bg-muted/30 px-4 py-6 text-foreground sm:px-6 lg:px-8">
      <div className="mx-auto flex max-w-7xl flex-col gap-6">
        <header className="flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
          <div>
            <div className="flex flex-wrap items-center gap-2">
              <p className="text-sm font-semibold text-accent">Administration</p>
              <Badge tone={dashboard.source === "live" ? "success" : "warning"}>
                {dashboard.source === "live" ? "Live platform data" : "Demo fallback data"}
              </Badge>
            </div>
            <h1 className="mt-1 text-3xl font-bold tracking-tight sm:text-4xl">Admin dashboard</h1>
            <p className="mt-2 max-w-2xl text-muted-foreground">
              A single operational view of membership, orders, savings, enquiries, and network growth.
            </p>
          </div>
          <Button disabled={isLoading} onClick={loadDashboard} variant="secondary">
            <RefreshCw className={`size-4 ${isLoading ? "animate-spin" : ""}`} />
            Refresh data
          </Button>
        </header>

        {requiredFailed ? (
          <StatusMessage tone="warning">
            Live dashboard data is temporarily unavailable. Realistic demo data is shown so the dashboard remains presentation-ready.
          </StatusMessage>
        ) : null}

        <KpiCards isLoading={isLoading} stats={dashboard.stats} />

        <section className="grid gap-6 xl:grid-cols-2">
          <MemberAnalytics members={dashboard.members} />
          <OrderAnalytics orders={dashboard.orders} />
        </section>

        <section className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          <Card>
            <div className="flex items-start justify-between"><p className="text-sm text-muted-foreground">Area leaders</p><UsersRound className="size-5 text-accent" /></div>
            <p className="mt-2 text-2xl font-bold">{dashboard.leaders.total}</p>
            <p className="mt-2 text-xs text-muted-foreground">{dashboard.leaders.pendingApplications} pending <Badge className="ml-1" tone="warning">Estimate</Badge></p>
          </Card>
          <Card>
            <div className="flex items-start justify-between"><p className="text-sm text-muted-foreground">Facilitators</p><UserCheck className="size-5 text-accent" /></div>
            <p className="mt-2 text-2xl font-bold">{dashboard.people.totalFacilitators}</p>
            <p className="mt-2 text-xs text-muted-foreground">{dashboard.people.recentReferrals} referrals this month</p>
          </Card>
          <Card>
            <div className="flex items-start justify-between"><p className="text-sm text-muted-foreground">Savings balance</p><Activity className="size-5 text-success" /></div>
            <p className="mt-2 text-2xl font-bold">{formatCurrency(dashboard.savings.total)}</p>
            <p className="mt-2 text-xs text-muted-foreground">{formatCurrency(dashboard.savings.interestAccrued)} interest <Badge className="ml-1" tone="warning">Estimate</Badge></p>
          </Card>
          <Card>
            <div className="flex items-start justify-between"><p className="text-sm text-muted-foreground">System status</p><Network className="size-5 text-accent" /></div>
            <p className="mt-2 text-lg font-bold">{healthState.isPending ? "Checking…" : healthState.health?.status ?? "Unavailable"}</p>
            <p className="mt-2 flex items-center gap-1 text-xs text-muted-foreground"><Database className="size-3.5" /> Database: {healthState.health?.databaseStatus ?? "Unknown"}</p>
          </Card>
        </section>

        <RecentActivity activity={dashboard.activity} />
      </div>
    </main>
  );
};
