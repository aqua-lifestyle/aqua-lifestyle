"use client";

import { RefreshCw, Users } from "lucide-react";
import { useEffect, useMemo, useState } from "react";

import {
  useAreaLeadersActions, useAreaLeadersState, useAreaSpacesActions, useAreaSpacesState,
  useCustomersActions, useCustomersState, useEnquiriesActions, useEnquiriesState,
  useFacilitatorsActions, useFacilitatorsState, useOrderIntentsActions, useOrderIntentsState,
} from "@/src/providers";
import { httpClient } from "@/src/shared/api";
import { Badge, Button, Card, StatusMessage } from "@/src/shared/ui";
import { buildAreaLeaderDashboard } from "../model/dashboard";
import { mockAreaLeaderDashboard } from "../model/mock-data";
import { AreaSpaceManagement } from "./area-space-management";
import { FacilitatorApproval } from "./facilitator-approval";
import { KpiCards } from "./kpi-cards";
import { OrderManagement } from "./order-management";
import { RecentActivity } from "./recent-activity";

export const AreaLeaderDashboard = () => {
  const leaders = useAreaLeadersState(); const leaderActions = useAreaLeadersActions();
  const spaces = useAreaSpacesState(); const spaceActions = useAreaSpacesActions();
  const customers = useCustomersState(); const customerActions = useCustomersActions();
  const enquiries = useEnquiriesState(); const enquiryActions = useEnquiriesActions();
  const facilitators = useFacilitatorsState(); const facilitatorActions = useFacilitatorsActions();
  const orders = useOrderIntentsState(); const orderActions = useOrderIntentsActions();
  const [actionError, setActionError] = useState<string | null>(null);
  const [isApproving, setIsApproving] = useState(false);

  const loadDashboard = () => void Promise.all([
    leaderActions.getAreaLeaders(), spaceActions.getAreaSpaces(), customerActions.getCustomers(),
    customerActions.getMyCustomer(), enquiryActions.getEnquiries(),
    facilitatorActions.getFacilitators(), orderActions.getOrderIntents(),
  ]);

  useEffect(() => {
    loadDashboard();
    // Provider actions are stable callbacks; load once for the current guarded session.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const failed = leaders.isLoadError || spaces.isLoadError || customers.isLoadError ||
    enquiries.isLoadError || facilitators.isLoadError || orders.isLoadError;
  const loading = leaders.isLoadPending || spaces.isLoadPending || customers.isLoadPending ||
    customers.isMyCustomerPending || enquiries.isLoadPending || facilitators.isLoadPending || orders.isLoadPending;
  const dashboard = useMemo(() => failed ? mockAreaLeaderDashboard : buildAreaLeaderDashboard({
    areaLeaders: leaders.areaLeaders, areaSpaces: spaces.areaSpaces, customers: customers.customers,
    enquiries: enquiries.enquiries, facilitators: facilitators.facilitators,
    myCustomerId: customers.myCustomer?.id ?? null, orders: orders.orderIntents,
  }), [failed, leaders.areaLeaders, spaces.areaSpaces, customers.customers, customers.myCustomer?.id,
    enquiries.enquiries, facilitators.facilitators, orders.orderIntents]);

  const processOrder = async (id: number) => {
    setActionError(null);
    if (!(await orderActions.completeOrderIntent(id))) setActionError(orders.actionErrorMessage ?? "Unable to process the order.");
  };
  const approveFacilitator = async (id: number) => {
    setIsApproving(true); setActionError(null);
    try {
      await httpClient.post(`/api/services/app/Facilitator/Approve?id=${id}`, {});
      await facilitatorActions.getFacilitators();
    } catch (error) {
      setActionError(error instanceof Error ? error.message : "Unable to approve the facilitator.");
    } finally { setIsApproving(false); }
  };

  return (
    <main className="min-h-[calc(100dvh-7.5rem)] bg-muted/30 px-4 py-6 sm:px-6 lg:px-8">
      <div className="mx-auto flex max-w-7xl flex-col gap-6">
        <header className="flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
          <div><div className="flex items-center gap-2"><p className="text-sm font-semibold text-accent">Area operations</p><Badge tone={failed ? "warning" : "success"}>{failed ? "Demo fallback data" : "Live Area data"}</Badge></div><h1 className="mt-1 text-3xl font-bold tracking-tight sm:text-4xl">Area Leader dashboard</h1><p className="mt-2 text-muted-foreground">Live performance, people, and order activity for your Area Space.</p></div>
          <Button disabled={loading} onClick={loadDashboard} variant="secondary"><RefreshCw className={`size-4 ${loading ? "animate-spin" : ""}`} />Refresh data</Button>
        </header>
        {failed ? <StatusMessage tone="warning">Live Area Space data is unavailable. Demo data is shown so this dashboard remains presentation-ready.</StatusMessage> : null}
        {actionError ? <StatusMessage tone="error">{actionError}</StatusMessage> : null}
        <KpiCards isLoading={loading} stats={dashboard.stats} />
        <section className="grid gap-6 xl:grid-cols-2"><OrderManagement isProcessing={orders.isActionPending} onProcess={processOrder} orders={dashboard.recentOrders} /><FacilitatorApproval facilitators={dashboard.pendingFacilitators} isApproving={isApproving} onApprove={approveFacilitator} /></section>
        <section className="grid gap-6 xl:grid-cols-2"><AreaSpaceManagement areaSpace={dashboard.areaSpace} /><RecentActivity activities={dashboard.activities} /></section>
        <Card><div className="flex items-center gap-2"><Users className="size-5 text-accent" /><h2 className="text-lg font-semibold">Recent members</h2></div><ul className="mt-4 grid gap-2 sm:grid-cols-2 lg:grid-cols-5">{dashboard.recentMembers.map((member) => <li className="rounded-lg bg-muted/60 p-3 text-sm font-semibold" key={member.id}>{member.name}</li>)}</ul></Card>
      </div>
    </main>
  );
};
