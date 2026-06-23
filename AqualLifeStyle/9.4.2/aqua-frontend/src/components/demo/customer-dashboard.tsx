"use client";

import { useEffect, useMemo } from "react";
import { usePathname, useRouter } from "next/navigation";
import {
  Building2,
  Calendar,
  Crown,
  Package,
  ShieldCheck,
  ShoppingCart,
  User,
  Wallet,
  ArrowUpRight,
  CheckCircle2,
  Clock,
} from "lucide-react";

import { useAuthState } from "@/src/providers";
import {
  useCustomersActions,
  useCustomersState,
  useMembershipsActions,
  useMembershipsState,
  useProductsActions,
  useProductsState,
  useOrderIntentsActions,
  useOrderIntentsState,
} from "@/src/providers";
import { Badge, Button, Card, LinkButton, StatusMessage } from "@/src/shared/ui";
import { useHydrated } from "@/src/shared/lib/use-hydrated";

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

const MEMBERSHIP_LABELS = ["Jasper", "Onyx", "AQGreen", "Business Premier"];

export const CustomerDashboard = () => {
  const hasMounted = useHydrated();
  const pathname = usePathname();
  const router = useRouter();
  const { isAuthenticated, isReady, session } = useAuthState();
  const user = session?.user;

  const { changeMembership, getMyCustomer } = useCustomersActions();
  const { getActiveTiers, getSavingsWindowStatuses } = useMembershipsActions();
  const { getEligibleProductsForCustomer } = useProductsActions();
  const { createForCurrentCustomer } = useOrderIntentsActions();
  const {
    actionErrorMessage: orderErrorMessage,
    isActionError: isOrderError,
    isActionPending: isOrderPending,
    isActionSuccess: isOrderSuccess,
  } = useOrderIntentsState();
  const {
    changeMembershipErrorMessage,
    isChangeMembershipError,
    isChangeMembershipPending,
    myCustomer,
    myCustomerErrorMessage,
    isMyCustomerError,
    isMyCustomerPending,
    isMyCustomerSuccess,
  } = useCustomersState();
  const {
    errorMessage: membershipsErrorMessage,
    isError: isMembershipsError,
    isPending: isMembershipsPending,
    memberships,
    savingsWindowStatuses,
    isSavingsWindowStatusesError,
    isSavingsWindowStatusesPending,
    savingsWindowStatusesErrorMessage,
  } = useMembershipsState();
  const {
    eligibleProducts,
    isEligibleError,
    isEligiblePending,
    eligibleErrorMessage,
  } = useProductsState();

  useEffect(() => {
    if (isReady && !isAuthenticated) {
      router.replace(`/login?redirect=${encodeURIComponent(pathname)}`);
    }
  }, [isAuthenticated, isReady, pathname, router]);

  useEffect(() => {
    if (!isReady || !isAuthenticated) return;

    void getMyCustomer();
    void getActiveTiers();
    void getSavingsWindowStatuses();
  }, [
    getMyCustomer,
    getActiveTiers,
    getSavingsWindowStatuses,
    isAuthenticated,
    isReady,
  ]);

  useEffect(() => {
    if (!isReady || !isAuthenticated || !isMyCustomerSuccess || !myCustomer?.id) {
      return;
    }

    void getEligibleProductsForCustomer(myCustomer.id);
  }, [
    myCustomer?.id,
    getEligibleProductsForCustomer,
    isAuthenticated,
    isMyCustomerSuccess,
    isReady,
  ]);

  const currentMembership = myCustomer?.membershipId
    ? memberships.find((m) => m.id === myCustomer.membershipId) ?? null
    : null;

  const availableTiers = useMemo(() => {
    if (!memberships.length) return [];
    return memberships.filter((m) => m.isActive);
  }, [memberships]);

  const currentSavingsWindow = useMemo(() => {
    if (!currentMembership) return null;
    return (
      savingsWindowStatuses.find(
        (s) => s.tier === currentMembership.membershipType,
      ) ?? null
    );
  }, [currentMembership, savingsWindowStatuses]);

  const getInitials = () => {
    if (!user?.name) return "?";
    const parts = user.name.trim().split(/\s+/);
    const first = parts[0]?.[0] ?? "";
    const last = parts.length > 1 ? parts[parts.length - 1]?.[0] : "";
    return `${first}${last}`.toUpperCase();
  };

  const isLoading =
    isMembershipsPending ||
    isMyCustomerPending ||
    isSavingsWindowStatusesPending ||
    isEligiblePending;
  const hasError =
    isMembershipsError ||
    isMyCustomerError ||
    isChangeMembershipError ||
    isSavingsWindowStatusesError ||
    isEligibleError;
  const errorMessages = Array.from(
    new Set(
      [
        membershipsErrorMessage,
        myCustomerErrorMessage,
        changeMembershipErrorMessage,
        savingsWindowStatusesErrorMessage,
        eligibleErrorMessage,
      ].filter((message): message is string => Boolean(message)),
    ),
  );

  if (!isReady || !isAuthenticated) {
    return (
      <main className="min-h-dvh bg-muted/30 px-4 py-10 text-muted-foreground">
        <div className="mx-auto max-w-7xl text-sm font-semibold">
          Verifying customer access…
        </div>
      </main>
    );
  }

  return (
    <main className="min-h-dvh bg-muted/30 px-4 py-6 text-foreground sm:px-6 lg:px-8">
      <div className="mx-auto flex w-full max-w-7xl flex-col gap-6">
        <header className="flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
          <div className="flex items-center gap-4">
            <div className="flex size-16 items-center justify-center rounded-2xl bg-gradient-to-br from-accent to-accent-dark text-2xl font-bold text-white shadow-md">
              {getInitials()}
            </div>
            <div>
              <p className="text-sm font-semibold text-accent">Welcome back</p>
              <h1 className="text-3xl font-bold tracking-tight sm:text-4xl">
                {user?.name ?? "Club member"}
              </h1>
              <p className="mt-1 text-base text-muted-foreground">
                {user?.email}
              </p>
            </div>
          </div>
          <div className="flex items-center gap-2 text-sm text-muted-foreground">
            <Calendar className="size-4" />
            <span>{hasMounted ? formatDate(new Date().toISOString()) : "Current date"}</span>
            <Badge tone="accent" className="ml-2">
              {currentMembership ? "Club member" : "Customer"}
            </Badge>
          </div>
        </header>

        {isLoading ? (
          <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
            {[...Array(4)].map((_, index) => (
              <div key={index} className="h-32 animate-pulse rounded-xl bg-muted" />
            ))}
          </div>
        ) : (
          <section className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
            <Card className="flex items-center gap-4">
              <div className="rounded-full bg-accent/10 p-3 text-accent">
                <User className="size-6" />
              </div>
              <div>
                <p className="text-sm text-muted-foreground">Membership</p>
                <p className="text-2xl font-bold">
                  {currentMembership?.name ?? "None"}
                </p>
                <p className="text-xs text-muted-foreground">
                  {currentMembership
                    ? currentMembership.membershipType >= 0
                      ? `Tier ${currentMembership.membershipType + 1}`
                      : "Custom tier"
                    : "Not joined"}
                </p>
              </div>
            </Card>

            <Card className="flex items-center gap-4">
              <div className="rounded-full bg-success/10 p-3 text-success">
                <Wallet className="size-6" />
              </div>
              <div>
                <p className="text-sm text-muted-foreground">Savings account</p>
                <p className="text-2xl font-bold">
                  {currentMembership ? "Active" : "Locked"}
                </p>
                <p className="text-xs text-muted-foreground">
                  {currentSavingsWindow
                    ? currentSavingsWindow.isSavingsWindowOpen
                      ? "Window open"
                      : "Window closed"
                    : currentMembership
                      ? "Savings status unavailable"
                      : "No membership selected"}
                </p>
              </div>
            </Card>

            <Card className="flex items-center gap-4">
              <div className="rounded-full bg-info/10 p-3 text-info">
                <Package className="size-6" />
              </div>
              <div>
                <p className="text-sm text-muted-foreground">Products</p>
                <p className="text-2xl font-bold">
                  {eligibleProducts.length}
                </p>
                <p className="text-xs text-muted-foreground">
                  {eligibleProducts.length === 1 ? "Item available" : "Items available"}
                </p>
              </div>
            </Card>

            <Card className="flex items-center gap-4">
              <div className="rounded-full bg-warning/10 p-3 text-warning">
                <ShieldCheck className="size-6" />
              </div>
              <div>
                <p className="text-sm text-muted-foreground">Account security</p>
                <p className="text-2xl font-bold">Verified</p>
                <p className="text-xs text-muted-foreground">Profile complete</p>
              </div>
            </Card>
          </section>
        )}

        {hasError ? (
          <StatusMessage tone="error">
            {errorMessages.length > 0
              ? errorMessages.join(" ")
              : "Unable to load your membership details."}
          </StatusMessage>
        ) : null}

        {isOrderError ? (
          <StatusMessage tone="error">
            {orderErrorMessage ?? "Unable to reserve this product."}
          </StatusMessage>
        ) : null}
        {isOrderSuccess ? (
          <StatusMessage tone="success">
            <span>Product reserved successfully. Your new order is now visible to the Area Leader.</span>
            <LinkButton className="ml-3" href="/member/orders" size="sm" variant="outline">
              View my orders
            </LinkButton>
          </StatusMessage>
        ) : null}

        <section className="grid gap-6 lg:grid-cols-2">
          <Card>
            <div className="flex items-center gap-3 border-b border-border pb-4">
              <Crown className="size-5 text-accent" />
              <h2 className="text-lg font-semibold">Membership</h2>
            </div>
            <div className="mt-4 space-y-3 text-sm">
              {currentMembership ? (
                <div className="rounded-lg bg-muted/50 px-4 py-3">
                  <div className="flex items-center justify-between">
                    <span className="text-muted-foreground">Current plan</span>
                    <span className="font-semibold text-foreground">
                      {currentMembership.name}
                    </span>
                  </div>
                  <div className="mt-2 flex items-center justify-between">
                    <span className="text-muted-foreground">Monthly obligation</span>
                    <span className="font-semibold text-foreground">
                      {currentMembership.monthlyObligationAmount !== null
                        ? formatCurrency(currentMembership.monthlyObligationAmount)
                        : "Not set"}
                    </span>
                  </div>
                  <div className="mt-2 flex items-center justify-between">
                    <span className="text-muted-foreground">Status</span>
                    <span className="flex items-center gap-1 font-semibold text-success">
                      <CheckCircle2 className="size-4" />
                      Active
                    </span>
                  </div>
                </div>
              ) : (
                <div className="rounded-lg bg-muted/50 px-4 py-3 text-muted-foreground">
                  You have not joined a membership yet. Choose a tier below to
                  unlock savings, products, and order benefits.
                </div>
              )}

              <div className="mt-4">
                <p className="text-xs font-semibold text-muted-foreground">
                  Available tiers
                </p>
                <div className="mt-2 space-y-2">
                  {availableTiers.map((tier) => {
                    const isCurrent = currentMembership?.id === tier.id;
                    return (
                      <div
                        key={tier.id}
                        className="flex items-center justify-between rounded-lg bg-muted/50 px-4 py-3"
                      >
                        <div>
                          <p className="font-semibold text-foreground">
                            {tier.name}
                          </p>
                          <p className="text-xs text-muted-foreground">
                            {tier.description ?? MEMBERSHIP_LABELS[tier.membershipType] ?? "Membership tier"}
                          </p>
                        </div>
                        <div className="flex items-center gap-2">
                          {isCurrent ? (
                            <Badge tone="success">Current</Badge>
                          ) : (
                            <Button
                              disabled={!myCustomer?.id || isChangeMembershipPending}
                              isLoading={isChangeMembershipPending}
                              size="sm"
                              variant="primary"
                              onClick={async () => {
                                if (myCustomer?.id) {
                                  await changeMembership({
                                    membershipId: tier.id,
                                  });
                                }
                              }}
                            >
                              <ArrowUpRight className="size-4" />
                              {currentMembership ? "Upgrade" : "Join"}
                            </Button>
                          )}
                        </div>
                      </div>
                    );
                  })}
                  {availableTiers.length === 0 ? (
                    <div className="rounded-lg bg-muted/50 px-4 py-3 text-muted-foreground">
                      No membership tiers are available right now.
                    </div>
                  ) : null}
                </div>
              </div>
            </div>
          </Card>

          <Card>
            <div className="flex items-center gap-3 border-b border-border pb-4">
              <Wallet className="size-5 text-accent" />
              <h2 className="text-lg font-semibold">Savings account</h2>
            </div>
            <div className="mt-4 space-y-3 text-sm text-muted-foreground">
              <div className="flex items-center justify-between rounded-lg bg-muted/50 px-4 py-3">
                <span>Current balance</span>
                <span className="font-semibold text-foreground">
                  {currentMembership ? formatCurrency(0) : "Locked"}
                </span>
              </div>
              <div className="flex items-center justify-between rounded-lg bg-muted/50 px-4 py-3">
                <span>Savings window</span>
                <span className="flex items-center gap-1 font-semibold text-foreground">
                  <Clock className="size-4" />
                  {currentSavingsWindow
                    ? currentSavingsWindow.isSavingsWindowOpen
                      ? "Open"
                      : "Closed"
                    : currentMembership
                      ? "Coming soon"
                      : "Locked"}
                </span>
              </div>
              <div className="flex items-center justify-between rounded-lg bg-muted/50 px-4 py-3">
                <span>Membership tier</span>
                <span className="font-semibold text-foreground">
                  {currentMembership?.name ?? "None"}
                </span>
              </div>
            </div>
          </Card>
        </section>

        <section className="grid gap-6 lg:grid-cols-2">
          <Card>
            <div className="flex items-center gap-3 border-b border-border pb-4">
              <Package className="size-5 text-accent" />
              <h2 className="text-lg font-semibold">Products for you</h2>
            </div>
            <div className="mt-4 space-y-3 text-sm text-muted-foreground">
              {eligibleProducts.length === 0 ? (
                <div className="rounded-lg bg-muted/50 px-4 py-3">
                  No eligible products available yet.
                </div>
              ) : (
                eligibleProducts.map((product) => (
                  <div
                    key={product.id}
                    className="flex items-center justify-between rounded-lg bg-muted/50 px-4 py-3"
                  >
                    <div>
                      <p className="font-semibold text-foreground">
                        {product.name}
                      </p>
                      <p className="text-xs text-muted-foreground">
                        {product.membershipId
                          ? memberships.find(
                              (membership) => membership.id === product.membershipId,
                            )?.name ?? "Member product"
                          : "Available to all customers"}
                      </p>
                    </div>
                    <div className="flex items-center gap-3">
                      <span className="font-semibold text-foreground">
                        {formatCurrency(product.price)}
                      </span>
                      <Button
                        disabled={!currentMembership || isOrderPending}
                        isLoading={isOrderPending}
                        onClick={() => void createForCurrentCustomer(product.id)}
                        size="sm"
                      >
                        <ShoppingCart className="size-4" />
                        Reserve
                      </Button>
                    </div>
                  </div>
                ))
              )}
            </div>
          </Card>

          <Card>
            <div className="flex items-center gap-3 border-b border-border pb-4">
              <Building2 className="size-5 text-accent" />
              <h2 className="text-lg font-semibold">My area</h2>
            </div>
            <div className="mt-4 text-sm text-muted-foreground">
              Your area leader and facilitator information will appear here once
              available.
            </div>
          </Card>
        </section>
      </div>
    </main>
  );
};
