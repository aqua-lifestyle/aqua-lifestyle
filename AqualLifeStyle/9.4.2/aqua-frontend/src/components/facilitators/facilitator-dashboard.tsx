"use client";

import { Copy, DollarSign, TrendingUp, UserPlus, Users } from "lucide-react";
import { useEffect, useMemo, useState } from "react";

import {
  useFacilitatorsActions,
  useFacilitatorsState,
  useCustomersActions,
  useCustomersState,
  useReferralsActions,
  useReferralsState,
  useAuthState,
  useToast,
} from "@/src/providers";
import {
  Badge,
  Breadcrumb,
  Button,
  Card,
  LinkButton,
  Skeleton,
  StatusMessage,
} from "@/src/shared/ui";

export const FacilitatorDashboard = () => {
  const [isCopied, setIsCopied] = useState(false);
  const { toast } = useToast();
  const { getFacilitators } = useFacilitatorsActions();
  const { facilitators, isLoadError: isFacilitatorsError, isLoadPending: isFacilitatorsPending, loadErrorMessage: facilitatorsErrorMessage } = useFacilitatorsState();

  const { getReferrals } = useReferralsActions();
  const { referrals, isLoadError: isReferralsError, isLoadPending: isReferralsPending, loadErrorMessage: referralsErrorMessage } = useReferralsState();
  const { getMyCustomer } = useCustomersActions();
  const { myCustomer, isMyCustomerError, isMyCustomerPending, myCustomerErrorMessage } = useCustomersState();

  // ALL hooks before early returns
  useEffect(() => {
    void getFacilitators();
    void getReferrals();
    void getMyCustomer();
  }, [getFacilitators, getMyCustomer, getReferrals]);

  const { session } = useAuthState();
  const isLoading = isFacilitatorsPending || isReferralsPending || isMyCustomerPending;
  const hasError = isFacilitatorsError || isReferralsError || isMyCustomerError;

  const facilitator = useMemo(() => {
    if (!session?.user?.id || !myCustomer) return null;
    return facilitators.find((f) => f.customerId === myCustomer.id) ?? null;
  }, [facilitators, myCustomer, session?.user?.id]);

  const myReferrals = useMemo(() => {
    if (!facilitator) return [];
    return referrals.filter((r) => r.referrerFacilitatorId === facilitator.id);
  }, [referrals, facilitator]);

  const totalAwards = useMemo(() => myReferrals.reduce((sum, r) => sum + r.awardAmount, 0), [myReferrals]);
  const confirmedCount = useMemo(() => myReferrals.filter((r) => r.confirmedAt !== null).length, [myReferrals]);
  const recentReferrals = useMemo(
    () => [...myReferrals].sort((left, right) =>
      (right.convertedAt ?? "").localeCompare(left.convertedAt ?? ""),
    ).slice(0, 5),
    [myReferrals],
  );
  const referralCode = facilitator ? `FAC-${facilitator.id}` : null;
  const referralPath = facilitator ? `/signup?ref=${facilitator.id}` : null;

  const copyReferralLink = async () => {
    if (!facilitator) return;

    const referralLink = new URL("/signup", window.location.origin);
    referralLink.searchParams.set("ref", String(facilitator.id));
    let copied = false;
    try {
      await navigator.clipboard?.writeText(referralLink.toString());
      copied = Boolean(navigator.clipboard);
    } catch {
      // Fall back for browsers that deny Clipboard API permission.
    }

    if (!copied) {
      const textArea = document.createElement("textarea");
      textArea.value = referralLink.toString();
      textArea.style.position = "fixed";
      textArea.style.opacity = "0";
      document.body.appendChild(textArea);
      textArea.select();
      copied = document.execCommand("copy");
      textArea.remove();
    }

    if (!copied) {
      toast({
        message: "Copy was blocked. Select the referral link and copy it manually.",
        title: "Unable to copy link",
        type: "error",
      });
      return;
    }

    setIsCopied(true);
    toast({
      message: "Referral signup link copied to your clipboard.",
      title: "Link copied",
      type: "success",
    });
  };

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
            {facilitatorsErrorMessage ?? referralsErrorMessage ?? myCustomerErrorMessage ?? "Unable to load dashboard data."}
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
                  <LinkButton href="/facilitator/my-referrals" variant="outline">
                    View my referrals
                  </LinkButton>
                  <LinkButton href={facilitator ? `/facilitator/${facilitator.id}` : "/facilitator"} variant="outline">
                    View facilitator details
                  </LinkButton>
                </div>
              </Card>
              <Card>
                <h2 className="text-lg font-semibold">Share your referral link</h2>
                <p className="mt-2 text-sm text-muted-foreground">
                  Send this signup link to a prospective member. Their customer journey can be attributed to your Facilitator profile.
                </p>
                <div className="mt-4 flex flex-col gap-3 sm:flex-row sm:items-center">
                  <code className="flex-1 rounded-lg bg-muted px-3 py-2 text-sm font-semibold">
                    {referralCode && referralPath
                      ? `${referralCode} · ${referralPath}`
                      : "Referral profile unavailable"}
                  </code>
                  <Button disabled={!facilitator} onClick={() => void copyReferralLink()} variant="secondary">
                    <Copy className="size-4" />
                    {isCopied ? "Copied" : "Copy link"}
                  </Button>
                </div>
              </Card>
            </section>
            <Card>
              <div className="flex items-center justify-between gap-3">
                <div>
                  <h2 className="text-lg font-semibold">Recent referral activity</h2>
                  <p className="mt-1 text-sm text-muted-foreground">Your latest attributed customer conversions.</p>
                </div>
                <LinkButton href="/facilitator/my-referrals" size="sm" variant="ghost">View all</LinkButton>
              </div>
              {recentReferrals.length === 0 ? (
                <p className="mt-4 rounded-lg bg-muted/60 p-4 text-sm text-muted-foreground">No referral activity yet.</p>
              ) : (
                <ul className="mt-4 divide-y divide-border">
                  {recentReferrals.map((referral) => (
                    <li className="flex flex-wrap items-center justify-between gap-3 py-3 first:pt-0" key={referral.id}>
                      <div>
                        <p className="font-semibold">Customer #{referral.referredCustomerId}</p>
                        <p className="text-xs text-muted-foreground">Enquiry #{referral.sourceEnquiryId}</p>
                      </div>
                      <div className="flex items-center gap-3">
                        <span className="text-sm font-semibold">R {referral.awardAmount.toFixed(2)}</span>
                        <Badge tone={referral.awardIssued ? "success" : "neutral"}>
                          {referral.awardIssued ? "Awarded" : "Pending"}
                        </Badge>
                      </div>
                    </li>
                  ))}
                </ul>
              )}
            </Card>
          </>
        )}
      </div>
    </main>
  );
};
