"use client";

import { useEffect, useMemo } from "react";

import {
  type EnquiryStatus,
  type MembershipType,
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
  useSystemHealthState,
} from "@/src/providers";
import { getMembershipTypeLabel } from "@/src/shared/domain";
import { Badge, Card, LinkButton, StatusMessage } from "@/src/shared/ui";

const journeySteps = [
  {
    description:
      "Compare Jasper, Onyx, AQGreen, and Business Premier before assigning access.",
    href: "/memberships",
    label: "Membership access",
    status: "Tier story",
  },
  {
    description:
      "Review the live product catalog and the membership tier each product requires.",
    href: "/products",
    label: "Product eligibility",
    status: "Catalog",
  },
  {
    description:
      "Register or edit a customer and connect them to the right club tier.",
    href: "/customers/register",
    label: "Customer activation",
    status: "Live write",
  },
  {
    description:
      "Create, respond, follow up, and mark converted enquiries as the pipeline matures.",
    href: "/enquiries",
    label: "Enquiry workflow",
    status: "Pipeline",
  },
  {
    description:
      "Turn converted demand into a lightweight reservation before adding payment complexity.",
    href: "/order-intents",
    label: "Order intent handoff",
    status: "Reservation",
  },
] as const;

const enquiryStatusLabels: Record<EnquiryStatus, string> = {
  0: "Pending",
  1: "Responded",
  2: "Closed",
};

const formatPercent = (value: number) => {
  return new Intl.NumberFormat("en-ZA", {
    maximumFractionDigits: 0,
    style: "percent",
  }).format(value / 100);
};

const getMetricTone = (value: number) => (value > 0 ? "success" : "neutral");

export const DemoDashboard = () => {
  const { getCustomers } = useCustomersActions();
  const { getEnquiries } = useEnquiriesActions();
  const { getMemberships, getSavingsWindowStatuses } = useMembershipsActions();
  const { getOrderIntents } = useOrderIntentsActions();
  const { getProducts } = useProductsActions();

  const {
    customers,
    isLoadError: isCustomersError,
    isLoadPending: isCustomersPending,
    loadErrorMessage: customersErrorMessage,
  } = useCustomersState();
  const {
    enquiries,
    isLoadError: isEnquiriesError,
    isLoadPending: isEnquiriesPending,
    loadErrorMessage: enquiriesErrorMessage,
  } = useEnquiriesState();
  const {
    errorMessage: membershipsErrorMessage,
    isError: isMembershipsError,
    isPending: isMembershipsPending,
    memberships,
    savingsWindowStatuses,
  } = useMembershipsState();
  const {
    isLoadError: isOrderIntentsError,
    isLoadPending: isOrderIntentsPending,
    loadErrorMessage: orderIntentsErrorMessage,
    orderIntents,
  } = useOrderIntentsState();
  const {
    errorMessage: productsErrorMessage,
    isError: isProductsError,
    isPending: isProductsPending,
    products,
  } = useProductsState();
  const {
    health,
    isError: isSystemHealthError,
    isPending: isSystemHealthPending,
    isSuccess: isSystemHealthSuccess,
  } = useSystemHealthState();

  useEffect(() => {
    void getMemberships();
    void getSavingsWindowStatuses();
    void getProducts();
    void getCustomers();
    void getEnquiries();
    void getOrderIntents();
  }, [
    getCustomers,
    getEnquiries,
    getMemberships,
    getSavingsWindowStatuses,
    getOrderIntents,
    getProducts,
  ]);

  const dashboardMetrics = useMemo(() => {
    const pendingEnquiries = enquiries.filter((enquiry) => enquiry.isPending);
    const closedEnquiries = enquiries.filter((enquiry) => enquiry.isClosed);
    const convertedEnquiries = enquiries.filter((enquiry) => enquiry.isConverted);
    const salesReadyEnquiries = enquiries.filter(
      (enquiry) => enquiry.isSalesReady,
    );
    const activeCustomers = customers.filter((customer) => customer.isActive);
    const activeProducts = products.filter((product) => product.isActive);
    const activeMemberships = memberships.filter(
      (membership) => membership.isActive,
    );
    const openSavingsWindowTiers = savingsWindowStatuses.filter(
      (status) => status.isSavingsWindowOpen,
    );
    const reservedOrderIntents = orderIntents.filter(
      (orderIntent) => orderIntent.status === 1,
    );
    const completedOrderIntents = orderIntents.filter(
      (orderIntent) => orderIntent.status === 3,
    );
    const totalConversionProbability = enquiries.reduce(
      (total, enquiry) => total + enquiry.conversionProbability,
      0,
    );
    const averageConversionProbability =
      enquiries.length > 0 ? totalConversionProbability / enquiries.length : 0;

    return {
      activeCustomers: activeCustomers.length,
      activeMemberships: activeMemberships.length,
      activeProducts: activeProducts.length,
      averageConversionProbability,
      closedEnquiries: closedEnquiries.length,
      completedOrderIntents: completedOrderIntents.length,
      convertedEnquiries: convertedEnquiries.length,
      pendingEnquiries: pendingEnquiries.length,
      reservedOrderIntents: reservedOrderIntents.length,
      salesReadyEnquiries: salesReadyEnquiries.length,
      openSavingsWindowTiers: openSavingsWindowTiers.length,
      savingsWindowTiers: savingsWindowStatuses.length,
    };
  }, [
    customers,
    enquiries,
    memberships,
    orderIntents,
    products,
    savingsWindowStatuses,
  ]);

  const membershipTypeCounts = useMemo(() => {
    return memberships.reduce<Record<MembershipType, number>>(
      (counts, membership) => ({
        ...counts,
        [membership.membershipType]: counts[membership.membershipType] + 1,
      }),
      {
        0: 0,
        1: 0,
        2: 0,
        3: 0,
      },
    );
  }, [memberships]);

  const demoReadinessItems = [
    {
      action: "Check top bar",
      href: "/",
      isReady: isSystemHealthSuccess && health?.isDatabaseReachable === true,
      label: "Backend and database reachable",
      readyText: "Healthy",
      waitingText: isSystemHealthError ? "Unavailable" : "Checking",
    },
    {
      action: "Review memberships",
      href: "/memberships",
      isReady: dashboardMetrics.activeMemberships > 0,
      label: "Membership tiers loaded",
      readyText: "Ready",
      waitingText: "Needs tiers",
    },
    {
      action: "Filter catalog",
      href: "/products",
      isReady: dashboardMetrics.activeProducts > 0,
      label: "Product catalog available",
      readyText: "Ready",
      waitingText: "Needs products",
    },
    {
      action: "Review savings",
      href: "/memberships",
      isReady: dashboardMetrics.savingsWindowTiers > 0,
      label: "Savings windows visible",
      readyText: "Ready",
      waitingText: "Needs signal",
    },
    {
      action: "Register customer",
      href: "/customers/register",
      isReady: dashboardMetrics.activeCustomers > 0,
      label: "Customer activation proven",
      readyText: "Ready",
      waitingText: "Next",
    },
    {
      action: "Create enquiry",
      href: "/enquiries/create",
      isReady:
        dashboardMetrics.pendingEnquiries +
          dashboardMetrics.salesReadyEnquiries +
          dashboardMetrics.convertedEnquiries >
        0,
      label: "Enquiry pipeline started",
      readyText: "Ready",
      waitingText: "Next",
    },
    {
      action: "Open pipeline",
      href: "/enquiries",
      isReady: dashboardMetrics.convertedEnquiries > 0,
      label: "Conversion handoff proven",
      readyText: "Ready",
      waitingText: "Next",
    },
    {
      action: "Open intents",
      href: "/order-intents",
      isReady: dashboardMetrics.reservedOrderIntents > 0,
      label: "Order intent handoff proven",
      readyText: "Ready",
      waitingText: "Next",
    },
  ] as const;

  const latestEnquiries = enquiries.slice(0, 3);
  const isLoading =
    isCustomersPending ||
    isEnquiriesPending ||
    isMembershipsPending ||
    isOrderIntentsPending ||
    isProductsPending ||
    isSystemHealthPending;
  const errorMessages = [
    customersErrorMessage,
    enquiriesErrorMessage,
    membershipsErrorMessage,
    orderIntentsErrorMessage,
    productsErrorMessage,
  ].filter(Boolean);
  const hasError =
    isCustomersError ||
    isEnquiriesError ||
    isMembershipsError ||
    isOrderIntentsError ||
    isProductsError ||
    isSystemHealthError;

  return (
    <main className="min-h-dvh bg-zinc-50 px-6 py-8 text-zinc-950 sm:px-8 lg:px-12">
      <div className="mx-auto flex w-full max-w-7xl flex-col gap-8">
        <header className="grid gap-8 lg:grid-cols-[1fr_24rem] lg:items-end">
          <div className="flex flex-col gap-5">
            <div className="flex flex-wrap gap-3">
              <Badge tone="success">ABP integration demo</Badge>
              <Badge>Membership-led commerce</Badge>
              <Badge>Validated learning path</Badge>
            </div>
            <div className="max-w-4xl">
              <p className="text-sm font-medium uppercase tracking-wide text-emerald-700">
                Aqua Lifestyle Club
              </p>
              <h1 className="mt-2 text-4xl font-semibold tracking-tight sm:text-5xl">
                Club operations demo dashboard
              </h1>
              <p className="mt-4 text-base leading-7 text-zinc-600">
                A live, end-to-end view of the current demo: membership tiers,
                product access, customer activation, and enquiry conversion.
                Savings-window readiness and order intents now prove the first
                commerce signals after membership access is selected.
              </p>
            </div>
            <div className="flex flex-col gap-3 sm:flex-row">
              <LinkButton href="/memberships" variant="primary">
                Start with memberships
              </LinkButton>
              <LinkButton href="/enquiries">Open enquiry pipeline</LinkButton>
              <LinkButton href="/order-intents">Order intents</LinkButton>
            </div>
          </div>

          <Card>
            <p className="text-sm font-semibold uppercase tracking-wide text-emerald-700">
              Next validated learning
            </p>
            <h2 className="mt-3 text-xl font-semibold">
              Run the full demo loop
            </h2>
            <p className="mt-3 text-sm leading-6 text-zinc-600">
              Start with a tier, filter products by access, register a customer,
              create an enquiry, record follow-ups, then mark the enquiry
              converted and create an order intent. The dashboard now shows
              which parts are proven live.
            </p>
          </Card>
        </header>

        {isLoading ? <StatusMessage>Loading live demo metrics...</StatusMessage> : null}

        {hasError ? (
          <StatusMessage tone="error">
            {errorMessages.length > 0
              ? errorMessages.join(" ")
              : "Unable to load one or more dashboard data sets."}
          </StatusMessage>
        ) : null}

        <section className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
          <Card>
            <p className="text-sm text-zinc-600">Membership tiers</p>
            <div className="mt-3 flex items-end justify-between gap-4">
              <p className="text-3xl font-semibold">
                {dashboardMetrics.activeMemberships}
              </p>
              <Badge tone={getMetricTone(dashboardMetrics.activeMemberships)}>
                Active
              </Badge>
            </div>
          </Card>

          <Card>
            <p className="text-sm text-zinc-600">Active products</p>
            <div className="mt-3 flex items-end justify-between gap-4">
              <p className="text-3xl font-semibold">
                {dashboardMetrics.activeProducts}
              </p>
              <Badge tone={getMetricTone(dashboardMetrics.activeProducts)}>
                Catalog
              </Badge>
            </div>
          </Card>

          <Card>
            <p className="text-sm text-zinc-600">Active customers</p>
            <div className="mt-3 flex items-end justify-between gap-4">
              <p className="text-3xl font-semibold">
                {dashboardMetrics.activeCustomers}
              </p>
              <Badge tone={getMetricTone(dashboardMetrics.activeCustomers)}>
                Members
              </Badge>
            </div>
          </Card>

          <Card>
            <p className="text-sm text-zinc-600">Reserved intents</p>
            <div className="mt-3 flex items-end justify-between gap-4">
              <p className="text-3xl font-semibold">
                {dashboardMetrics.reservedOrderIntents}
              </p>
              <Badge tone={getMetricTone(dashboardMetrics.reservedOrderIntents)}>
                Handoff
              </Badge>
            </div>
          </Card>

          <Card>
            <p className="text-sm text-zinc-600">Savings windows</p>
            <div className="mt-3 flex items-end justify-between gap-4">
              <p className="text-3xl font-semibold">
                {dashboardMetrics.openSavingsWindowTiers}/
                {dashboardMetrics.savingsWindowTiers}
              </p>
              <Badge
                tone={getMetricTone(dashboardMetrics.openSavingsWindowTiers)}
              >
                Open tiers
              </Badge>
            </div>
          </Card>
        </section>

        <section className="grid gap-6 xl:grid-cols-[1fr_24rem]">
          <div className="grid gap-4 lg:grid-cols-2">
            {journeySteps.map((step, index) => (
              <Card className="flex flex-col justify-between gap-6" key={step.href}>
                <div>
                  <div className="flex items-start justify-between gap-4">
                    <p className="text-sm font-semibold text-emerald-700">
                      Step {index + 1}
                    </p>
                    <Badge>{step.status}</Badge>
                  </div>
                  <h2 className="mt-4 text-lg font-semibold">{step.label}</h2>
                  <p className="mt-3 text-sm leading-6 text-zinc-600">
                    {step.description}
                  </p>
                </div>
                <LinkButton href={step.href}>Open</LinkButton>
              </Card>
            ))}
          </div>

          <aside className="flex flex-col gap-6">
            <Card>
              <h2 className="text-lg font-semibold">Demo readiness</h2>
              <div className="mt-5 grid gap-3">
                {demoReadinessItems.map((item) => (
                  <div
                    className="flex items-center justify-between gap-4 rounded-lg border border-zinc-200 bg-zinc-50 p-3"
                    key={item.label}
                  >
                    <div>
                      <p className="text-sm font-medium text-zinc-950">
                        {item.label}
                      </p>
                      <LinkButton href={item.href}>{item.action}</LinkButton>
                    </div>
                    <Badge tone={item.isReady ? "success" : "neutral"}>
                      {item.isReady ? item.readyText : item.waitingText}
                    </Badge>
                  </div>
                ))}
              </div>
            </Card>

            <Card>
              <h2 className="text-lg font-semibold">Enquiry health</h2>
              <dl className="mt-5 grid gap-3 text-sm">
                <div className="flex justify-between gap-4">
                  <dt className="text-zinc-600">Sales ready</dt>
                  <dd className="font-medium text-zinc-950">
                    {dashboardMetrics.salesReadyEnquiries}
                  </dd>
                </div>
                <div className="flex justify-between gap-4">
                  <dt className="text-zinc-600">Closed</dt>
                  <dd className="font-medium text-zinc-950">
                    {dashboardMetrics.closedEnquiries}
                  </dd>
                </div>
                <div className="flex justify-between gap-4">
                  <dt className="text-zinc-600">Converted</dt>
                  <dd className="font-medium text-zinc-950">
                    {dashboardMetrics.convertedEnquiries}
                  </dd>
                </div>
                <div className="flex justify-between gap-4">
                  <dt className="text-zinc-600">Avg. probability</dt>
                  <dd className="font-medium text-zinc-950">
                    {formatPercent(dashboardMetrics.averageConversionProbability)}
                  </dd>
                </div>
                <div className="flex justify-between gap-4">
                  <dt className="text-zinc-600">Reserved intents</dt>
                  <dd className="font-medium text-zinc-950">
                    {dashboardMetrics.reservedOrderIntents}
                  </dd>
                </div>
                <div className="flex justify-between gap-4">
                  <dt className="text-zinc-600">Completed intents</dt>
                  <dd className="font-medium text-zinc-950">
                    {dashboardMetrics.completedOrderIntents}
                  </dd>
                </div>
              </dl>
              <div className="mt-6 flex flex-col gap-3">
                <LinkButton href="/enquiries/sales-ready">
                  Open sales-ready view
                </LinkButton>
                <LinkButton href="/order-intents">Open order intents</LinkButton>
              </div>
            </Card>

            <Card>
              <h2 className="text-lg font-semibold">Tier mix</h2>
              <dl className="mt-5 grid gap-3 text-sm">
                {Object.entries(membershipTypeCounts).map(([type, count]) => (
                  <div className="flex justify-between gap-4" key={type}>
                    <dt className="text-zinc-600">
                      {getMembershipTypeLabel(Number(type) as MembershipType)}
                    </dt>
                    <dd className="font-medium text-zinc-950">{count}</dd>
                  </div>
                ))}
              </dl>
            </Card>
          </aside>
        </section>

        {latestEnquiries.length > 0 ? (
          <section className="flex flex-col gap-4">
            <div className="flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
              <div>
                <p className="text-sm font-medium uppercase tracking-wide text-emerald-700">
                  Live pipeline
                </p>
                <h2 className="mt-1 text-2xl font-semibold tracking-tight">
                  Latest enquiries
                </h2>
              </div>
              <LinkButton href="/enquiries">View all enquiries</LinkButton>
            </div>

            <div className="grid gap-4 lg:grid-cols-3">
              {latestEnquiries.map((enquiry) => (
                <Card className="flex flex-col justify-between gap-6" key={enquiry.id}>
                  <div>
                    <div className="flex items-start justify-between gap-4">
                      <p className="font-semibold">Enquiry #{enquiry.id}</p>
                      <Badge
                        tone={
                          enquiry.isConverted || !enquiry.isClosed
                            ? "success"
                            : "neutral"
                        }
                      >
                        {enquiry.isConverted
                          ? "Converted"
                          : enquiryStatusLabels[enquiry.status]}
                      </Badge>
                    </div>
                    <p className="mt-3 line-clamp-3 text-sm leading-6 text-zinc-600">
                      {enquiry.message}
                    </p>
                  </div>
                  <LinkButton href={`/enquiries/${enquiry.id}`}>Open</LinkButton>
                </Card>
              ))}
            </div>
          </section>
        ) : null}
      </div>
    </main>
  );
};
