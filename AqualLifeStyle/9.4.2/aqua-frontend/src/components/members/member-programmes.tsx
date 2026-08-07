"use client";

import { CircleDollarSign, Network, Plane, Route } from "lucide-react";
import { useEffect, useRef, useState, type ReactNode } from "react";

import {
  useAuthState,
  useSystemHealthActions,
  useSystemHealthState,
} from "@/src/providers";
import { isPaymentApiCompatible } from "@/src/providers/SystemHealth/contract";
import {
  apiEndpoints,
  getExpiredSessionLoginUrl,
  httpClient,
  refreshAccessToken,
} from "@/src/shared/api";
import { getRequestErrorMessage } from "@/src/shared/api/abp-error";
import { navigateToExternalUrl } from "@/src/shared/browser/navigation";
import type {
  OnyxTravelBenefit,
  ProgrammeParticipation,
} from "@/src/shared/domain/programme-participations";
import { useMyProgrammeParticipations } from "@/src/shared/hooks/use-my-programme-participations";
import {
  Badge,
  Breadcrumb,
  Button,
  Card,
  Skeleton,
  StatusMessage,
} from "@/src/shared/ui";
import { JoinProgrammeDialog } from "./join-programme-dialog";

const VIEW_PERMISSION = "Aqua.ProgrammeParticipations.ViewSelf";

const formatCurrency = (amount: number, currency: string) =>
  new Intl.NumberFormat("en-ZA", {
    currency,
    style: "currency",
  }).format(amount);

const ParticipationCard = ({
  paymentAction,
  participation,
}: {
  paymentAction?: ReactNode;
  participation: ProgrammeParticipation;
}) => (
  <Card className="flex flex-col gap-5">
    <div className="flex items-start justify-between gap-4">
      <div>
        <p className="text-sm font-medium text-muted-foreground">Programme</p>
        <h2 className="mt-1 text-2xl font-bold">{participation.programmeName}</h2>
      </div>
      <Badge
        tone={
          participation.isActive
            ? "success"
            : participation.status === "Declined"
              ? "error"
              : "warning"
        }
      >
        {participation.status}
      </Badge>
    </div>

    <dl className="grid gap-4 text-sm sm:grid-cols-2">
      <div>
        <dt className="text-muted-foreground">Network placement</dt>
        <dd className="mt-1 font-semibold">
          {participation.joinedIndependently
            ? "Independent"
            : `Invited by ${participation.recruiterClubMemberNumber ?? "a verified Club Member"}`}
        </dd>
      </div>
      <div>
        <dt className="text-muted-foreground">Started</dt>
        <dd className="mt-1 font-semibold">
          {new Date(participation.startedAt).toLocaleDateString()}
        </dd>
      </div>
      <div>
        <dt className="text-muted-foreground">Member invitations</dt>
        <dd className="mt-1 font-semibold">
          {participation.canRecruitForThisProgramme
            ? `May invite Club Members to ${participation.programmeName}`
            : "Available after activation"}
        </dd>
      </div>
      <div>
        <dt className="text-muted-foreground">Activation</dt>
        <dd className="mt-1 font-semibold">
          {participation.activatedAt
            ? new Date(participation.activatedAt).toLocaleDateString()
            : "Pending"}
        </dd>
      </div>
    </dl>

    {participation.status === "Awaiting Area approval" ? (
      <div className="rounded-xl border border-accent/30 bg-accent/5 p-4 text-sm">
        <p className="font-semibold">Payment confirmed</p>
        <p className="mt-1 text-muted-foreground">
          Your payment has been received and your participation is under review
          by the Area team. It will activate once approved.
        </p>
      </div>
    ) : participation.status === "Declined" ? (
      <div className="rounded-xl border border-error/30 bg-error/5 p-4 text-sm">
        <p className="font-semibold">Not approved</p>
        <p className="mt-1 text-muted-foreground">
          The Area team could not approve this participation. Contact the club
          team if you believe this is a mistake.
        </p>
      </div>
    ) : null}

    {participation.nextPaymentAmount !== null ? (
      <div className="rounded-xl border border-warning/30 bg-warning/5 p-4">
        <div className="flex items-start gap-3">
          <CircleDollarSign className="mt-0.5 size-5 text-warning" />
          <div>
            <p className="font-semibold">{participation.nextPaymentDescription}</p>
            <p className="mt-1 text-xl font-bold">
              {formatCurrency(
                participation.nextPaymentAmount,
                participation.currency,
              )}
            </p>
            <p className="mt-2 text-sm text-muted-foreground">
              Participation activates only after the payment provider confirms
              receipt.
            </p>
          </div>
        </div>
      </div>
    ) : null}
    {paymentAction}
  </Card>
);

const TravelBenefitCard = ({
  travelBenefit,
}: {
  travelBenefit: OnyxTravelBenefit;
}) => (
  <Card className="flex flex-col gap-5">
    <div className="flex items-start justify-between gap-4">
      <div className="flex items-center gap-3">
        <Plane className="size-7 text-accent" />
        <div>
          <p className="text-sm font-medium text-muted-foreground">
            Onyx Level 3 benefit
          </p>
          <h2 className="mt-1 text-xl font-bold">Travel benefit</h2>
        </div>
      </div>
      <Badge tone={travelBenefit.activatedAt ? "success" : "warning"}>
        {travelBenefit.status}
      </Badge>
    </div>

    <p className="text-sm text-muted-foreground">
      Your eligibility is preserved after completing Onyx Level 3. You
      contribute {travelBenefit.memberTripContributionPercent}% of the trip
      cost when a future trip is arranged.
    </p>

    <dl className="grid gap-4 text-sm sm:grid-cols-2">
      <div>
        <dt className="text-muted-foreground">Eligible from</dt>
        <dd className="mt-1 font-semibold">
          {new Date(travelBenefit.eligibleAt).toLocaleDateString()}
        </dd>
      </div>
      <div>
        <dt className="text-muted-foreground">
          {travelBenefit.activatedAt ? "Available since" : "Waiting period ends"}
        </dt>
        <dd className="mt-1 font-semibold">
          {new Date(
            travelBenefit.activatedAt ??
              travelBenefit.waitingPeriodEndsAt,
          ).toLocaleDateString()}
        </dd>
      </div>
    </dl>

    <StatusMessage tone="info">
      Trip selection, pricing, and booking will be provided separately when
      those services become available.
    </StatusMessage>
  </Card>
);

export const MemberProgrammes = () => {
  const { session } = useAuthState();
  const healthActions = useSystemHealthActions();
  const healthState = useSystemHealthState();
  const canView =
    session?.user?.permissions?.includes(VIEW_PERMISSION) ?? false;
  const {
    data: participations,
    errorMessage: loadError,
    isLoading: loading,
  } = useMyProgrammeParticipations(canView);
  const [actionError, setActionError] = useState<string>();
  const [success, setSuccess] = useState<string>();
  const [startingAQGreenPayment, setStartingAQGreenPayment] = useState(false);
  const [accessRefreshFinished, setAccessRefreshFinished] = useState(false);
  const accessRefreshAttempted = useRef(false);
  const contractCheckAttempted = useRef(false);
  const hasActiveInvitationAccess = Boolean(
    participations?.entry?.canRecruitForThisProgramme ||
      participations?.onyx?.canRecruitForThisProgramme,
  );
  const hasInvitationPermission =
    session?.user?.permissions?.includes(
      "Aqua.ProgrammeParticipations.Invite",
    ) ?? false;
  const paymentApiCompatible = isPaymentApiCompatible(healthState.health);
  const paymentActionsUnavailable =
    healthState.isPending || !paymentApiCompatible;

  useEffect(() => {
    if (
      !contractCheckAttempted.current &&
      !healthState.isPending &&
      !healthState.isSuccess
    ) {
      contractCheckAttempted.current = true;
      void healthActions.checkHealth();
    }
  }, [healthActions, healthState.isPending, healthState.isSuccess]);

  useEffect(() => {
    if (
      !hasActiveInvitationAccess ||
      hasInvitationPermission ||
      accessRefreshAttempted.current
    ) {
      return;
    }

    accessRefreshAttempted.current = true;
    void refreshAccessToken()
      .then((accessToken) => {
        if (accessToken) {
          setAccessRefreshFinished(true);
          return;
        }
        if (typeof window === "undefined") return;
        const returnPath = `${window.location.pathname}${window.location.search}`;
        window.location.href = getExpiredSessionLoginUrl(returnPath);
      })
      .catch(() => {
        setAccessRefreshFinished(true);
        setActionError(
          "Your programme is active, but your updated account access could not be loaded. Check your connection and try signing in again.",
        );
      });
  }, [hasActiveInvitationAccess, hasInvitationPermission]);

  useEffect(() => {
    const task = window.setTimeout(() => {
      const paymentResult = new URLSearchParams(window.location.search).get("payment");
      const programme = new URLSearchParams(window.location.search).get("programme");
      if (paymentResult === "success") {
        setSuccess(
          programme === "aqgreen"
            ? "Payment submitted. Once the provider confirms it, the Area team will review your AQGreen participation before it activates."
            : "Payment submitted. Once the provider confirms it, the Area team will review your Onyx participation before it is created.",
        );
      } else if (paymentResult === "cancelled") {
        setActionError(
          programme === "aqgreen"
            ? "You returned without payment confirmation. Your checkout remains locked until Yoco reports a terminal result or an authorised administrator reviews it."
            : "You returned without payment confirmation. No Onyx participation was created; the existing checkout remains pending.",
        );
      } else if (paymentResult === "failed") {
        setActionError(
          programme === "aqgreen"
            ? "The browser returned from payment, but this does not confirm failure. Your AQGreen checkout remains locked while the provider result is verified."
            : "The browser returned from payment, but this does not confirm failure. No Onyx participation was created and the checkout remains pending.",
        );
      }
    }, 0);
    return () => window.clearTimeout(task);
  }, []);

  const startAQGreenPayment = async () => {
    if (paymentActionsUnavailable) return;
    const schedule =
      participations?.entry?.joiningSchedule === 1 &&
      (participations.entry.joiningPaidAmount ?? 0) > 0
        ? 1
        : 0;
    setStartingAQGreenPayment(true);
    setActionError(undefined);
    try {
      const checkout = await httpClient.post<
        { checkoutUrl: string },
        { schedule: 0 | 1 }
      >(
        apiEndpoints.programmeParticipations.createAQGreenJoiningCheckout,
        { schedule },
      );
      navigateToExternalUrl(checkout.checkoutUrl);
    } catch (requestError) {
      setActionError(
        getRequestErrorMessage(
          requestError,
          "AQGreen payment could not be started. No payment has been taken.",
        ),
      );
    } finally {
      setStartingAQGreenPayment(false);
    }
  };

  if (!canView) {
    return (
      <main className="p-6">
        <StatusMessage tone="error">
          Your account does not have access to programme participation.
        </StatusMessage>
      </main>
    );
  }

  return (
    <main className="min-h-dvh bg-muted/30 px-4 py-6 sm:px-6 lg:px-8">
      <div className="mx-auto flex w-full max-w-6xl flex-col gap-6">
        <header>
          <Breadcrumb
            items={[
              { href: "/member", label: "Club Member" },
              { label: "My programmes" },
            ]}
          />
          <h1 className="mt-2 text-3xl font-bold tracking-tight">My programmes</h1>
          <p className="mt-2 max-w-3xl text-muted-foreground">
            Join AQGreen or Onyx, follow activation progress, and see whether your
            network starts independently or through an inviting Club Member.
          </p>
        </header>

        {loadError || actionError ? (
          <StatusMessage tone="error">{loadError ?? actionError}</StatusMessage>
        ) : null}
        {success ? (
          <StatusMessage tone="info">{success}</StatusMessage>
        ) : null}

        {!healthState.isPending && !paymentApiCompatible ? (
          <StatusMessage tone="error">
            Payments are unavailable because this frontend cannot verify a
            compatible payment API deployment. No payment has been taken. Ask
            an operator to deploy and verify the matching database, API, and
            frontend versions.
          </StatusMessage>
        ) : null}

        {hasActiveInvitationAccess ? (
          <Card className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
            <div>
              <h2 className="text-lg font-bold">Grow your network</h2>
              <p className="mt-1 text-sm text-muted-foreground">
                Share a secure invitation link. Your friend will see your name
                and programme before confirming.
              </p>
            </div>
            {hasInvitationPermission ? (
              <a className="inline-flex min-h-10 items-center justify-center rounded-lg bg-accent px-4 py-2 text-sm font-semibold text-white transition hover:bg-accent-dark" href="/member/invitations">
                Invite Club Members
              </a>
            ) : (
              <p className="text-sm font-medium text-muted-foreground">
                {accessRefreshFinished
                  ? "Sign out and sign in again to load your updated Club Member access."
                  : "Updating your Club Member access…"}
              </p>
            )}
          </Card>
        ) : null}

        {loading ? (
          <div className="grid gap-5 lg:grid-cols-2">
            <Skeleton className="h-80" />
            <Skeleton className="h-80" />
          </div>
        ) : participations ? (
          <div className="grid gap-5 lg:grid-cols-2">
            {participations.entry ? (
              <ParticipationCard
                participation={participations.entry}
                paymentAction={
                  !participations.entry.isActive &&
                  participations.entry.status !== "Awaiting Area approval" &&
                  participations.entry.status !== "Declined" ? (
                    participations.pendingAQGreenCheckout ? (
                      paymentActionsUnavailable ? (
                        <Button disabled>Continue secure payment</Button>
                      ) : (
                        <a
                          className="inline-flex min-h-10 items-center justify-center rounded-lg bg-accent px-4 py-2 text-sm font-semibold text-white transition hover:bg-accent-dark"
                          href={participations.pendingAQGreenCheckout.checkoutUrl}
                        >
                          Continue secure payment
                        </a>
                      )
                    ) : (
                      <div className="flex flex-col gap-4">
                        <Button
                          disabled={paymentActionsUnavailable}
                          isLoading={startingAQGreenPayment}
                          onClick={() => void startAQGreenPayment()}
                        >
                          Pay {formatCurrency(
                            participations.entry.nextPaymentAmount ??
                              participations.entry.joiningOutstandingAmount ?? 1200,
                            participations.entry.currency,
                          )} securely
                        </Button>
                      </div>
                    )
                  ) : undefined
                }
              />
            ) : (
              <Card className="flex flex-col items-start gap-4">
                <Route className="size-8 text-accent" />
                <div>
                  <h2 className="text-xl font-bold">AQGreen</h2>
                  <p className="mt-2 text-sm text-muted-foreground">
                    Start with AQGreen and work toward graduating into a separate
                    Onyx participation later. An invitation is optional.
                  </p>
                </div>
                <JoinProgrammeDialog
                  disabled={paymentActionsUnavailable}
                  programme="AQGreen"
                />
              </Card>
            )}

            {participations.onyx ? (
              <ParticipationCard participation={participations.onyx} />
            ) : participations.pendingDirectOnyxCheckout ? (
              <Card className="flex flex-col items-start gap-4">
                <Network className="size-8 text-accent" />
                <div>
                  <div className="flex items-center gap-3">
                    <h2 className="text-xl font-bold">Onyx</h2>
                    <Badge tone="warning">
                      {participations.pendingDirectOnyxCheckout.status}
                    </Badge>
                  </div>
                  <p className="mt-2 text-sm text-muted-foreground">
                    Your Onyx participation and network place do not exist yet.
                    They will be created only after Yoco confirms the full payment.
                  </p>
                  <p className="mt-3 text-2xl font-bold">
                    {formatCurrency(
                      participations.pendingDirectOnyxCheckout.amount,
                      participations.pendingDirectOnyxCheckout.currency,
                    )}
                  </p>
                </div>
                {paymentActionsUnavailable ? (
                  <Button disabled>Continue secure payment</Button>
                ) : (
                  <a
                    className="inline-flex min-h-10 items-center justify-center rounded-lg bg-accent px-4 py-2 text-sm font-semibold text-white transition hover:bg-accent-dark"
                    href={participations.pendingDirectOnyxCheckout.checkoutUrl}
                  >
                    Continue secure payment
                  </a>
                )}
              </Card>
            ) : (
              <Card className="flex flex-col items-start gap-4">
                <Network className="size-8 text-accent" />
                <div>
                  <h2 className="text-xl font-bold">Onyx</h2>
                  <p className="mt-2 text-sm text-muted-foreground">
                    Join Onyx through its single direct joining path with the full
                    R6,120 payment. AQGreen and an invitation are not required.
                  </p>
                </div>
                <JoinProgrammeDialog
                  disabled={paymentActionsUnavailable}
                  programme="Onyx"
                />
              </Card>
            )}
            {participations.travelBenefit ? (
              <TravelBenefitCard
                travelBenefit={participations.travelBenefit}
              />
            ) : null}
          </div>
        ) : null}
      </div>
    </main>
  );
};
