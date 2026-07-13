"use client";

import { DollarSign, TrendingUp, UserPlus, Users } from "lucide-react";
import { useEffect, useMemo } from "react";

import {
  useFacilitatorsActions,
  useFacilitatorsState,
  useReferralsActions,
  useReferralsState,
  useAuthState,
} from "@/src/providers";
import {
  Breadcrumb,
  Card,
  EmptyState,
  LinkButton,
  Skeleton,
  StatusMessage,
} from "@/src/shared/ui";

export const FacilitatorDashboard = () => {
  const { getFacilitators } = useFacilitatorsActions();
  const { facilitators, isLoadError: isFacilitatorsError, isLoadPending: isFacilitatorsPending, isLoadSuccess: isFacilitatorsSuccess, loadErrorMessage: facilitatorsErrorMessage } = useFacilitatorsState();

  const { getReferrals } = useReferralsActions();
  const { referrals, isLoadError: isReferralsError, isLoadPending: isReferralsPending, loadErrorMessage: referralsErrorMessage } = useReferralsState();

  const { session } = useAuthState();
  const hasPermission = session?.user?.permissions?.includes("Pages.Facilitators") ?? false;

  // ALL hooks before early returns
  useEffect(() => {
    void getFacilitators();
    void getReferrals();
  }, [getFacilitators, getReferrals]);

  const isLoading = isFacilitatorsPending || isReferralsPending;
  const hasError = isFacilitatorsError || isReferralsError;

  const facilitator = useMemo(() => {
    const currentUserId = session?.user?.id ?? null;
    if (!currentUserId) return null;
    return facilitators.find((f) => f.customerId === currentUserId) ?? null;
  }, [facilitators, session?.user?.id]);

  const myReferrals = useMemo(() => {
    if (!facilitator) return [];
    return referrals.filter((r) => r.referrerFacilitatorId === facilitator.id);
  }, [referrals, facilitator]);

  const totalAwards = useMemo(() => myReferrals.reduce((sum, r) => sum + r.awardAmount, 0), [myReferrals]);
  const confirmedCount = useMemo(() => myReferrals.filter((r) => r.confirmedAt !== null).length, [myReferrals]);

  return (
    <main className="min-h-dvh bg-muted/30 px-4 py-6 text-foreground sm:px-6 lg:px-8">
      <div className="mx-auto flex w-full max-w-7xl flex-col gap-6">
        <header>
          <Breadcrumb
            items={[
              { href: "/", label: "Dashboard" },
              { label: "Facilitator dashboard" },
            ]}
          />
          <h1 className="mt-2 text-3xl font-bold tracking-tight">Facilitator dashboard</h1>
          <p className="mt-2 max-w-2xl text-base text-muted-foreground">
            Track your referrals, commissions, and rank progression.
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
            {facilitatorsErrorMessage ?? referralsErrorMessage ?? "Unable to load dashboard data."}
          </StatusMessage>
        ) : (
          <>
            <section className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
              <Card className="flex items-center gap-4">
                <div className="rounded-full bg-accent/10 p-3 text-accent">
                  <Users className="size-6" />
                </div>
                <div>
                  <p className="text-sm text-muted-foreground">My Referrals</p>
                  <p className="text-2xl font-bold">{myReferrals.length}</p>
                </div>
              </Card>
              <Card className="flex items-center gap-4">
                <div className="rounded-full bg-success/10 p-3 text-success">
                  <DollarSign className="size-6" />
                </div>
                <div>
                  <p className="text-sm text-muted-foreground">Total Awards</p>
                  <p className="text-2xl font-bold">{totalAwards.toFixed(2)}</p>
                </div>
              </Card>
              <Card className="flex items-center gap-4">
                <div className="rounded-full bg-info/10 p-3 text-info">
                  <TrendingUp className="size-6" />
                </div>
                <div>
                  <p className="text-sm text-muted-foreground">Confirmed</p>
                  <p className="text-2xl font-bold">{confirmedCount}</p>
                </div>
              </Card>
              <Card className="flex items-center gap-4">
                <div className="rounded-full bg-warning/10 p-3 text-warning">
                  <UserPlus className="size-6" />
                </div>
                <div>
                  <p className="text-sm text-muted-foreground">Rank</p>
                  <p className="text-2xl font-bold">{facilitator ? `#${facilitator.rank}` : "-"}</p>
                </div>
              </Card>
            </section>

            <section className="grid gap-6 lg:grid-cols-2">
              <Card>
                <h2 className="text-lg font-semibold">Quick actions</h2>
                <div className="mt-4 flex flex-col gap-3">
                  <LinkButton href="/facilitator/referrals" variant="outline">
                    View referrals
                  </LinkButton>
                  <LinkButton href="/facilitator" variant="outline">
                    View facilitator details
                  </LinkButton>
                </div>
              </Card>
            </section>
          </>
        )}
      </div>
    </main>
  );
};
