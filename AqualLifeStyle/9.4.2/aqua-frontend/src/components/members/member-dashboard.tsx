"use client";

import { BarChart3, Package, PiggyBank, ShoppingCart } from "lucide-react";
import { useEffect, useMemo } from "react";

import {
  useAuthState,
  useMembershipsActions,
  useMembershipsState,
  useOrderIntentsActions,
  useOrderIntentsState,
} from "@/src/providers";
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
  const { getOrderIntents } = useOrderIntentsActions();
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
  const { session } = useAuthState();

  // ALL hooks before early returns
  useEffect(() => {
    void getOrderIntents();
    void getMemberships();
    void getSavingsWindowStatuses();
  }, [getMemberships, getOrderIntents, getSavingsWindowStatuses]);

  const currentUserId = session?.user?.id ?? null;

  const customerOrders = useMemo(() => {
    if (!currentUserId) return [];
    return orderIntents.filter((order) => order.customerId === currentUserId);
  }, [orderIntents, currentUserId]);

  const activeMembership = useMemo(() => {
    return memberships.find((membership) => membership.isActive) ?? null;
  }, [memberships]);

  const openSavingsWindow = useMemo(() => {
    if (!activeMembership) return null;
    return (
      savingsWindowStatuses.find((s) => s.tier === activeMembership.membershipType) ??
      null
    );
  }, [activeMembership, savingsWindowStatuses]);

  const isLoading =
    isOrdersPending || isMembershipsPending || !isOrdersSuccess || !isMembershipsSuccess;
  const hasError = isOrdersError || isMembershipsError;

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
            {ordersErrorMessage ?? membershipsErrorMessage ?? "Unable to load dashboard data."}
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
                  <p className="text-2xl font-bold">{customerOrders.length}</p>
                </div>
              </Card>
              <Card className="flex items-center gap-4">
                <div className="rounded-full bg-success/10 p-3 text-success">
                  <Package className="size-6" />
                </div>
                <div>
                  <p className="text-sm text-muted-foreground">Membership</p>
                  <p className="text-2xl font-bold">
                    {activeMembership?.name ?? "None"}
                  </p>
                </div>
              </Card>
              <Card className="flex items-center gap-4">
                <div className="rounded-full bg-warning/10 p-3 text-warning">
                  <PiggyBank className="size-6" />
                </div>
                <div>
                  <p className="text-sm text-muted-foreground">Savings Window</p>
                  <p className="text-2xl font-bold">
                    {openSavingsWindow ? openSavingsWindow.statusLabel : "Closed"}
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
                    {customerOrders.filter((o) => o.status === 2).length} completed
                  </p>
                </div>
              </Card>
            </section>

            <section className="grid gap-6 lg:grid-cols-2">
              <Card>
                <h2 className="text-lg font-semibold">Recent orders</h2>
                <div className="mt-4">
                  {customerOrders.length === 0 ? (
                    <EmptyState
                      description="You have no orders yet."
                      icon={Package}
                      title="No orders"
                    />
                  ) : (
                    <div className="flex flex-col gap-3">
                      {customerOrders.slice(0, 5).map((order) => (
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
                  ) : (
                    <EmptyState
                      description="No active savings window."
                      icon={PiggyBank}
                      title="Savings closed"
                    />
                  )}
                </div>
              </Card>
            </section>
          </>
        )}
      </div>
    </main>
  );
};
