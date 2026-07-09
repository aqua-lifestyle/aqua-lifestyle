"use client";

import { useEffect, useMemo } from "react";
import dynamic from "next/dynamic";
import {
  Activity,
  Banknote,
  Building2,
  Calendar,
  MessageSquare,
  Package,
  Users,
  Wallet,
} from "lucide-react";

import {
  useCustomersActions,
  useCustomersState,
  useEnquiriesActions,
  useEnquiriesState,
  useMembershipsActions,
  useMembershipsState,
  useOrderIntentsActions,
  useOrderIntentsState,
  useProductsActions,
  useProductsState,
  useTenantState,
} from "@/src/providers";
import { getMembershipTypeLabel } from "@/src/shared/domain";
import { Badge, Card, Skeleton, StatusMessage } from "@/src/shared/ui";

import { ActivityFeed } from "@/src/components/dashboard/activity-feed";
import { MetricCard } from "@/src/components/dashboard/metric-card";
import { QuickActions } from "@/src/components/dashboard/quick-actions";

const DashboardCharts = dynamic(
  () =>
    import("@/src/components/dashboard/dashboard-charts").then(
      (mod) => mod.DashboardCharts,
    ),
  {
    loading: () => <div className="h-64 w-full skeleton-shimmer rounded-xl" />,
    ssr: false,
  },
);

const enquiryStatusLabels = ["Pending", "Responded", "Closed"];

const formatCurrency = (amount: number) => {
  return new Intl.NumberFormat("en-ZA", {
    currency: "ZAR",
    style: "currency",
  }).format(amount);
};

const formatDate = (date: string) => {
  return new Intl.DateTimeFormat("en-ZA", {
    dateStyle: "long",
    timeStyle: "short",
  }).format(new Date(date));
};

export const DemoDashboard = () => {
  const { getCustomers } = useCustomersActions();
  const { getEnquiries } = useEnquiriesActions();
  const { getMemberships, getSavingsWindowStatuses } = useMembershipsActions();
  const { getOrderIntents } = useOrderIntentsActions();
  const { getProducts } = useProductsActions();

  const { currentTenant, isHost } = useTenantState();
  const { customers, isLoadError: customersError, isLoadPending: customersLoading, loadErrorMessage: customersErrorMessage } =
    useCustomersState();
  const { enquiries, isLoadError: enquiriesError, isLoadPending: enquiriesLoading, loadErrorMessage: enquiriesErrorMessage } =
    useEnquiriesState();
  const { memberships, isError: membershipsError, isPending: membershipsLoading, errorMessage: membershipsErrorMessage } =
    useMembershipsState();
  const { orderIntents, isLoadError: orderIntentsError, isLoadPending: orderIntentsLoading, loadErrorMessage: orderIntentsErrorMessage } =
    useOrderIntentsState();
  const { products, isError: productsError, isPending: productsLoading, errorMessage: productsErrorMessage } =
    useProductsState();

  useEffect(() => {
    void getCustomers();
    void getEnquiries();
    void getMemberships();
    void getOrderIntents();
    void getProducts();
    void getSavingsWindowStatuses();
  }, [
    getCustomers,
    getEnquiries,
    getMemberships,
    getOrderIntents,
    getProducts,
    getSavingsWindowStatuses,
  ]);

  const metrics = useMemo(() => {
    const activeCustomers = customers.filter((c) => c.isActive).length;
    const activeProducts = products.filter((p) => p.isActive).length;
    const totalRevenue = orderIntents.reduce(
      (sum, o) => sum + o.reservedPrice,
      0,
    );
    const pendingEnquiries = enquiries.filter((e) => e.isPending).length;
    const convertedEnquiries = enquiries.filter((e) => e.isConverted).length;

    return {
      activeCustomers,
      activeProducts,
      convertedEnquiries,
      customers: customers.length,
      enquiries: enquiries.length,
      pendingEnquiries,
      totalRevenue,
    };
  }, [customers, enquiries, orderIntents, products]);

  const membershipChartData = useMemo(() => {
    return memberships.map((membership) => ({
      count: customers.filter((c) => c.membershipId === membership.id).length,
      name: getMembershipTypeLabel(membership.membershipType),
    }));
  }, [customers, memberships]);

  const enquiryChartData = useMemo(() => {
    const counts = [0, 1, 2].map((status) =>
      enquiries.filter((e) => e.status === status).length,
    );

    return enquiryStatusLabels.map((name, index) => ({
      name,
      value: counts[index],
    }));
  }, [enquiries]);

  const activityItems = useMemo(() => {
    const items = [
      ...enquiries.map((enquiry) => {
        const customer = customers.find((c) => c.id === enquiry.customerId);
        const product = products.find((p) => p.id === enquiry.productId);

        return {
          description: `${customer?.name ?? `Customer ${enquiry.customerId}`} enquired about ${product?.name ?? `Product ${enquiry.productId}`}.`,
          icon: MessageSquare,
          id: `enquiry-${enquiry.id}`,
          meta: enquiryStatusLabels[enquiry.status],
          timestamp: enquiry.createdAt,
          title: `New enquiry #${enquiry.id}`,
        };
      }),
      ...orderIntents.map((orderIntent) => {
        const customer = customers.find((c) => c.id === orderIntent.customerId);
        const product = products.find((p) => p.id === orderIntent.productId);

        return {
          description: `${customer?.name ?? `Customer ${orderIntent.customerId}`} reserved ${product?.name ?? `Product ${orderIntent.productId}`}.`,
          icon: Wallet,
          id: `order-${orderIntent.id}`,
          meta: orderIntent.status === 1 ? "Reserved" : orderIntent.status === 3 ? "Completed" : "Draft",
          timestamp: orderIntent.createdAt,
          title: `Order intent #${orderIntent.id}`,
        };
      }),
    ];

    return items.sort(
      (a, b) => new Date(b.timestamp).getTime() - new Date(a.timestamp).getTime(),
    );
  }, [customers, enquiries, orderIntents, products]);

  const isLoading =
    customersLoading ||
    enquiriesLoading ||
    membershipsLoading ||
    orderIntentsLoading ||
    productsLoading;

  const errorMessages = [
    customersErrorMessage,
    enquiriesErrorMessage,
    membershipsErrorMessage,
    orderIntentsErrorMessage,
    productsErrorMessage,
  ].filter(Boolean);

  const hasError =
    customersError ||
    enquiriesError ||
    membershipsError ||
    orderIntentsError ||
    productsError;

  return (
    <main className="min-h-dvh bg-muted/30 px-4 py-6 text-foreground sm:px-6 lg:px-8">
      <div className="mx-auto flex w-full max-w-7xl flex-col gap-6">
        <header className="flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
          <div>
            <p className="text-sm font-semibold text-accent">Aqua Lifestyle Club</p>
            <h1 className="mt-1 text-3xl font-bold tracking-tight sm:text-4xl">
              Dashboard
            </h1>
            <p className="mt-2 max-w-2xl text-base text-muted-foreground">
              Live operational overview for the current tenant. Track customers,
              products, enquiries, and order intents in one place.
            </p>
          </div>
          <div className="flex items-center gap-2 text-sm text-muted-foreground">
            <Calendar className="size-4" />
            <span>{formatDate(new Date().toISOString())}</span>
            <Badge tone="accent" className="ml-2">
              {isHost ? "Host" : currentTenant ?? "Host"}
            </Badge>
          </div>
        </header>

        {isLoading ? (
          <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
            {[...Array(4)].map((_, index) => (
              <Skeleton key={index} className="h-32" />
            ))}
          </div>
        ) : (
          <section className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
            <MetricCard
              icon={Users}
              label="Total customers"
              value={metrics.customers}
              trend={metrics.activeCustomers > 0 ? 12 : 0}
            />
            <MetricCard
              icon={Package}
              label="Active products"
              value={metrics.activeProducts}
              trend={metrics.activeProducts > 0 ? 8 : 0}
            />
            <MetricCard
              icon={MessageSquare}
              label="Total enquiries"
              value={metrics.enquiries}
              trend={metrics.pendingEnquiries > 0 ? 5 : 0}
            />
            <MetricCard
              icon={Banknote}
              label="Reserved revenue"
              value={formatCurrency(metrics.totalRevenue)}
              trend={metrics.totalRevenue > 0 ? 15 : 0}
            />
          </section>
        )}

        {hasError ? (
          <StatusMessage tone="error">
            {errorMessages.length > 0
              ? errorMessages.join(" ")
              : "Unable to load dashboard data."}
          </StatusMessage>
        ) : null}

        <section className="grid gap-6 lg:grid-cols-3">
          <div className="lg:col-span-2">
            <DashboardCharts
              enquiryData={enquiryChartData}
              membershipData={membershipChartData}
            />
          </div>

          <div className="flex flex-col gap-6">
            <QuickActions />
            <ActivityFeed items={activityItems} />
          </div>
        </section>

        <section className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          <Card className="flex items-center gap-4">
            <div className="rounded-full bg-accent/10 p-3 text-accent">
              <Activity className="size-6" />
            </div>
            <div>
              <p className="text-sm text-muted-foreground">Conversion rate</p>
              <p className="text-2xl font-bold">
                {metrics.enquiries > 0
                  ? `${Math.round((metrics.convertedEnquiries / metrics.enquiries) * 100)}%`
                  : "0%"}
              </p>
            </div>
          </Card>

          <Card className="flex items-center gap-4">
            <div className="rounded-full bg-success/10 p-3 text-success">
              <Building2 className="size-6" />
            </div>
            <div>
              <p className="text-sm text-muted-foreground">Membership tiers</p>
              <p className="text-2xl font-bold">{memberships.length}</p>
            </div>
          </Card>

          <Card className="flex items-center gap-4">
            <div className="rounded-full bg-warning/10 p-3 text-warning">
              <Wallet className="size-6" />
            </div>
            <div>
              <p className="text-sm text-muted-foreground">Order intents</p>
              <p className="text-2xl font-bold">{orderIntents.length}</p>
            </div>
          </Card>

          <Card className="flex items-center gap-4">
            <div className="rounded-full bg-info/10 p-3 text-info">
              <Users className="size-6" />
            </div>
            <div>
              <p className="text-sm text-muted-foreground">Active customers</p>
              <p className="text-2xl font-bold">{metrics.activeCustomers}</p>
            </div>
          </Card>
        </section>
      </div>
    </main>
  );
};
