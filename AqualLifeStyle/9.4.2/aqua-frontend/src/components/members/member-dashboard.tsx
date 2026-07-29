"use client";

import { BarChart3, Package, PiggyBank, ShoppingCart } from "lucide-react";
import { useEffect, useMemo } from "react";

import {
  useAuthState,
  useCustomersActions,
  useCustomersState,
  useMembershipsActions,
  useMembershipsState,
  useOrderIntentsActions,
  useOrderIntentsState,
} from "@/src/providers";
import {
  getProgrammeStatusLabel,
} from "@/src/shared/domain/programme-participations";
import { useMyProgrammeParticipations } from "@/src/shared/hooks/use-my-programme-participations";
import {
  Avatar,
  Badge,
  Breadcrumb,
  Card,
  EmptyState,
  LinkButton,
  Skeleton,
  StatusMessage,
} from "@/src/shared/ui";
import { getOrderStatusLabel, getOrderStatusTone } from "@/src/shared/lib/order-status";

export const MemberDashboard = () => {
  const { getMyOrderIntents } = useOrderIntentsActions();
  const { getMyCustomer } = useCustomersActions();
  const { getMemberships, getSavingsWindowStatuses } = useMembershipsActions();
  const {
    isLoadError: isOrdersError,
    isLoadPending: isOrdersPending,
    isLoadSuccess: isOrdersSuccess,
    loadErrorMessage: ordersErrorMessage,
    orderIntents,
  } = useOrderIntentsState();
  const {
    errorMessage: membershipsErrorMessage,
    isError: isMembershipsError,
    isPending: isMembershipsPending,
    isSuccess: isMembershipsSuccess,
    memberships,
    savingsWindowStatuses,
  } = useMembershipsState();
  const {
    isMyCustomerError,
    isMyCustomerPending,
    isMyCustomerSuccess,
    myCustomer,
    myCustomerErrorMessage,
  } = useCustomersState();
  const { session } = useAuthState();
  const canViewProgrammes =
    session?.user?.permissions?.includes(
      "Aqua.ProgrammeParticipations.ViewSelf",
    ) ?? false;
  const canViewSavings =
    session?.user?.permissions?.includes("Aqua.Savings.ViewSelf") ?? false;
  const {
    data: programmeParticipations,
    errorMessage: programmeErrorMessage,
    isLoading: isProgrammesPending,
  } = useMyProgrammeParticipations(canViewProgrammes);

  // ALL hooks before early returns
  useEffect(() => {
    void getMyOrderIntents();
    void getMyCustomer();
    void getMemberships();
    void getSavingsWindowStatuses();
  }, [getMemberships, getMyCustomer, getMyOrderIntents, getSavingsWindowStatuses]);

  const assignedMembership = useMemo(
    () =>
      memberships.find((membership) => membership.id === myCustomer?.membershipId) ??
      null,
    [memberships, myCustomer?.membershipId],
  );
  const participationLabel = getProgrammeStatusLabel(
    programmeParticipations,
    assignedMembership?.name,
    "No active participation",
  );

  const openSavingsWindow = useMemo(() => {
    if (!assignedMembership) return null;
    return (
      savingsWindowStatuses.find((s) => s.tier === assignedMembership.membershipType) ??
      null
    );
  }, [assignedMembership, savingsWindowStatuses]);

  const isLoading =
    isOrdersPending ||
    isMembershipsPending ||
    isMyCustomerPending ||
    isProgrammesPending ||
    !isOrdersSuccess ||
    !isMembershipsSuccess ||
    !isMyCustomerSuccess;
  const hasError =
    isOrdersError || isMembershipsError || isMyCustomerError || Boolean(programmeErrorMessage);

  return (
    <main className="min-h-dvh bg-muted/30 px-4 py-6 text-foreground sm:px-6 lg:px-8">
      <div className="mx-auto flex w-full max-w-7xl flex-col gap-6">
        <header>
          <Breadcrumb
            items={[
              { href: "/", label: "Dashboard" },
              { label: "Member dashboard" },
            ]}
          />
          <h1 className="mt-2 text-3xl font-bold tracking-tight">Member dashboard</h1>
          <p className="mt-2 max-w-2xl text-base text-muted-foreground">
            Overview of your orders, savings, and membership activity.
          </p>
        </header>

        {isLoading ? (
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
            <Skeleton className="h-28" />
            <Skeleton className="h-28" />
            <Skeleton className="h-28" />
            <Skeleton className="h-28" />
          </div>
        ) : hasError ? (
          <StatusMessage tone="error">
            {ordersErrorMessage ??
              membershipsErrorMessage ??
              myCustomerErrorMessage ??
              programmeErrorMessage ??
              "Unable to load dashboard data."}
          </StatusMessage>
        ) : (
          <>
            <section className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
              <Card className="flex items-center gap-4">
                <div className="rounded-full bg-accent/10 p-3 text-accent">
                  <ShoppingCart className="size-6" />
                </div>
                <div>
                  <p className="text-sm text-muted-foreground">My Orders</p>
                  <p className="text-2xl font-bold">{orderIntents.length}</p>
                </div>
              </Card>
              <Card className="flex items-center gap-4">
                <div className="rounded-full bg-success/10 p-3 text-success">
                  <Package className="size-6" />
                </div>
                <div>
                  <p className="text-sm text-muted-foreground">Club participation</p>
                  <p className="text-2xl font-bold">
                    {participationLabel}
                  </p>
                </div>
              </Card>
              <Card className="flex items-center gap-4">
                <div className="rounded-full bg-warning/10 p-3 text-warning">
                  <PiggyBank className="size-6" />
                </div>
                <div>
                  <p className="text-sm text-muted-foreground">Savings access</p>
                  <p className="text-2xl font-bold">
                    {canViewSavings
                      ? (openSavingsWindow?.statusLabel ?? "Available")
                      : "Not available"}
                  </p>
                </div>
              </Card>
              <Card className="flex items-center gap-4">
                <div className="rounded-full bg-info/10 p-3 text-info">
                  <BarChart3 className="size-6" />
                </div>
                <div>
                  <p className="text-sm text-muted-foreground">Activity</p>
                  <p className="text-2xl font-bold">
                    {orderIntents.filter((o) => o.status === 2).length} completed
                  </p>
                </div>
              </Card>
            </section>

            <section className="grid gap-6 lg:grid-cols-2">
              <Card>
                <h2 className="text-lg font-semibold">Recent orders</h2>
                <div className="mt-4">
                  {orderIntents.length === 0 ? (
                    <EmptyState
                      description="You have no orders yet."
                      icon={Package}
                      title="No orders"
                    />
                  ) : (
                    <div className="flex flex-col gap-3">
                      {orderIntents.slice(0, 5).map((order) => (
                        <LinkButton
                          key={order.id}
                          href={`/member/orders`}
                          variant="outline"
                        >
                          <div className="flex items-center justify-between gap-4">
                            <div className="flex items-center gap-3">
                              <Avatar fallback={`O ${order.id}`} size="sm" />
                              <div>
                                <p className="font-semibold">Order #{order.id}</p>
                                <p className="text-xs text-muted-foreground">
                                  Product #{order.productId}
                                </p>
                              </div>
                            </div>
                            <Badge tone={getOrderStatusTone(order.status)}>
                              {getOrderStatusLabel(order.status)}
                            </Badge>
                          </div>
                        </LinkButton>
                      ))}
                    </div>
                  )}
                </div>
              </Card>

              <Card>
                <h2 className="text-lg font-semibold">Savings status</h2>
                <div className="mt-4">
                  {openSavingsWindow ? (
                    <div className="flex flex-col gap-3">
                      <div className="flex items-center justify-between">
                        <span className="text-sm text-muted-foreground">Tier</span>
                        <Badge tone="success">{openSavingsWindow.tierName}</Badge>
                      </div>
                      <div className="flex items-center justify-between">
                        <span className="text-sm text-muted-foreground">Status</span>
                        <Badge tone="success">{openSavingsWindow.statusLabel}</Badge>
                      </div>
                      <div className="flex items-center justify-between">
                        <span className="text-sm text-muted-foreground">Window</span>
                        <span className="text-sm font-medium">
                          Day {openSavingsWindow.savingsWindowOpenDay} -{" "}
                          {openSavingsWindow.savingsWindowCloseDay}
                        </span>
                      </div>
                      <div className="flex items-center justify-between">
                        <span className="text-sm text-muted-foreground">As of</span>
                        <span className="text-sm font-medium">
                          {new Date(openSavingsWindow.asOfDate).toLocaleDateString()}
                        </span>
                      </div>
                    </div>
                  ) : canViewSavings ? (
                    <EmptyState
                      description="Open your savings account to see contributions and maturity progress."
                      icon={PiggyBank}
                      title="Savings account available"
                    />
                  ) : (
                    <EmptyState
                      description="Savings becomes available with eligible Club Member access."
                      icon={PiggyBank}
                      title="Savings not available"
                    />
                  )}
                </div>
                {canViewSavings ? (
                  <LinkButton
                    className="mt-4 w-full"
                    href="/member/savings"
                    variant="outline"
                  >
                    View my savings account
                  </LinkButton>
                ) : null}
              </Card>
            </section>
          </>
        )}
      </div>
    </main>
  );
};
