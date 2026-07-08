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
  useProductsActions,
  useProductsState,
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
  const { getMemberships } = useMembershipsActions();
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
  } = useMembershipsState();
  const {
    errorMessage: productsErrorMessage,
    isError: isProductsError,
    isPending: isProductsPending,
    products,
  } = useProductsState();

  useEffect(() => {
    void getMemberships();
    void getProducts();
    void getCustomers();
    void getEnquiries();
  }, [getCustomers, getEnquiries, getMemberships, getProducts]);

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
      convertedEnquiries: convertedEnquiries.length,
      pendingEnquiries: pendingEnquiries.length,
      salesReadyEnquiries: salesReadyEnquiries.length,
    };
  }, [customers, enquiries, memberships, products]);

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

  const latestEnquiries = enquiries.slice(0, 3);
  const isLoading =
    isCustomersPending ||
    isEnquiriesPending ||
    isMembershipsPending ||
    isProductsPending;
  const errorMessages = [
    customersErrorMessage,
    enquiriesErrorMessage,
    membershipsErrorMessage,
    productsErrorMessage,
  ].filter(Boolean);
  const hasError =
    isCustomersError || isEnquiriesError || isMembershipsError || isProductsError;

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
                This turns the PDF vision into a working club-commerce journey.
              </p>
            </div>
            <div className="flex flex-col gap-3 sm:flex-row">
              <LinkButton href="/memberships" variant="primary">
                Start with memberships
              </LinkButton>
              <LinkButton href="/enquiries">Open enquiry pipeline</LinkButton>
            </div>
          </div>

          <Card>
            <p className="text-sm font-semibold uppercase tracking-wide text-emerald-700">
              Next validated learning
            </p>
            <h2 className="mt-3 text-xl font-semibold">
              Prove the conversion handoff
            </h2>
            <p className="mt-3 text-sm leading-6 text-zinc-600">
              Enquiries can now move from interest to a converted state in ABP.
              The next best slice should show the customer-side outcome clearly
              enough for a buyer or operator to understand the workflow.
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
            <p className="text-sm text-zinc-600">Pending enquiries</p>
            <div className="mt-3 flex items-end justify-between gap-4">
              <p className="text-3xl font-semibold">
                {dashboardMetrics.pendingEnquiries}
              </p>
              <Badge tone={getMetricTone(dashboardMetrics.pendingEnquiries)}>
                Pipeline
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
              </dl>
              <div className="mt-6">
                <LinkButton href="/enquiries/sales-ready">
                  Open sales-ready view
                </LinkButton>
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
