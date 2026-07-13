"use client";

import { useEffect, useMemo } from "react";
import {
  Building2,
  Calendar,
  Crown,
  Package,
  ShieldCheck,
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
} from "@/src/providers";
import { Badge, Button, Card, StatusMessage } from "@/src/shared/ui";

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
  const { session } = useAuthState();
  const user = session?.user;

  const { changeMembership, getMyCustomer } = useCustomersActions();
  const { getActiveTiers } = useMembershipsActions();
  const {
    isLoadError: isCustomersError,
    isLoadPending: isCustomersPending,
    loadErrorMessage: customersErrorMessage,
  } = useCustomersState();
  const {
    changeMembershipErrorMessage,
    isChangeMembershipError,
  } = useCustomersState();
  const {
    errorMessage: membershipsErrorMessage,
    isError: isMembershipsError,
    isPending: isMembershipsPending,
    memberships,
  } = useMembershipsState();

  useEffect(() => {
    void getMyCustomer();
    void getActiveTiers();
  }, [getMyCustomer, getActiveTiers]);

  const currentMembership = useMemo(() => {
    if (!myCustomer?.membershipId) return null;
    return memberships.find((m) => m.id === myCustomer.membershipId) ?? null;
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [myCustomer?.membershipId, memberships]);

  const availableTiers = useMemo(() => {
    if (!memberships.length) return [];
    return memberships.filter((m) => m.isActive);
  }, [memberships]);

  const getInitials = () => {
    if (!user?.name) return "?";
    const parts = user.name.trim().split(/\s+/);
    const first = parts[0]?.[0] ?? "";
    const last = parts.length > 1 ? parts[parts.length - 1]?.[0] : "";
    return `${first}${last}`.toUpperCase();
  };

  const isLoading = isCustomersPending || isMembershipsPending || isMyCustomerPending;
  const hasError = isCustomersError || isMembershipsError || isMyCustomerError || isChangeMembershipError;
  const errorMessages = [
    customersErrorMessage,
    membershipsErrorMessage,
    myCustomerErrorMessage,
    changeMembershipErrorMessage,
  ].filter(Boolean);

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
                {user?.name ?? "Member"}
              </h1>
              <p className="mt-1 text-base text-muted-foreground">
                {user?.email}
              </p>
            </div>
          </div>
          <div className="flex items-center gap-2 text-sm text-muted-foreground">
            <Calendar className="size-4" />
            <span>{formatDate(new Date().toISOString())}</span>
            <Badge tone="accent" className="ml-2">
              Member
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
                  {currentMembership
                    ? "Join a membership to unlock savings"
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
                <p className="text-2xl font-bold">Filtered for you</p>
                <p className="text-xs text-muted-foreground">
                  Based on your tier
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
                      {currentMembership.monthlyObligationAmount
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
                  {currentMembership ? "R 0.00" : "Locked"}
                </span>
              </div>
              <div className="flex items-center justify-between rounded-lg bg-muted/50 px-4 py-3">
                <span>Savings window</span>
                <span className="flex items-center gap-1 font-semibold text-foreground">
                  <Clock className="size-4" />
                  {currentMembership ? "Coming soon" : "Locked"}
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
              <div className="flex items-center justify-between rounded-lg bg-muted/50 px-4 py-3">
                <span>Featured products</span>
                <span className="font-semibold text-foreground">Coming soon</span>
              </div>
              <div className="flex items-center justify-between rounded-lg bg-muted/50 px-4 py-3">
                <span>Recommended for you</span>
                <span className="font-semibold text-foreground">Coming soon</span>
              </div>
              <div className="flex items-center justify-between rounded-lg bg-muted/50 px-4 py-3">
                <span>New arrivals</span>
                <span className="font-semibold text-foreground">Coming soon</span>
              </div>
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
