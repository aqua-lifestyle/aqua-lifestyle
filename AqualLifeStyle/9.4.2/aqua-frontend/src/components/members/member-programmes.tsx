"use client";

import { CircleDollarSign, Network, Plane, Route } from "lucide-react";
import { useCallback, useEffect, useState, type ReactNode } from "react";

import { useAuthState } from "@/src/providers";
import { apiEndpoints, httpClient } from "@/src/shared/api";
import { getRequestErrorMessage } from "@/src/shared/api/abp-error";
import { navigateToExternalUrl } from "@/src/shared/browser/navigation";
import type {
  MyProgrammeParticipations,
  OnyxTravelBenefit,
  ProgrammeParticipation,
} from "@/src/shared/domain/programme-participations";
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
      <Badge tone={participation.isActive ? "success" : "warning"}>
        {participation.status}
      </Badge>
    </div>

    <dl className="grid gap-4 text-sm sm:grid-cols-2">
      <div>
        <dt className="text-muted-foreground">Network placement</dt>
        <dd className="mt-1 font-semibold">
          {participation.joinedIndependently
            ? "Independent"
            : `Under ${participation.recruiterClubMemberNumber ?? "a verified Club Member"}`}
        </dd>
      </div>
      <div>
        <dt className="text-muted-foreground">Started</dt>
        <dd className="mt-1 font-semibold">
          {new Date(participation.startedAt).toLocaleDateString()}
        </dd>
      </div>
      <div>
        <dt className="text-muted-foreground">Recruitment eligibility</dt>
        <dd className="mt-1 font-semibold">
          {participation.canRecruitForThisProgramme
            ? `May recruit into ${participation.programmeName}`
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
  const canView =
    session?.user?.permissions?.includes(VIEW_PERMISSION) ?? false;
  const [participations, setParticipations] =
    useState<MyProgrammeParticipations>();
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string>();
  const [success, setSuccess] = useState<string>();
  const [startingAQGreenPayment, setStartingAQGreenPayment] = useState(false);

  const loadParticipations = useCallback(async () => {
    if (!canView) {
      setLoading(false);
      return;
    }
    setLoading(true);
    setError(undefined);
    try {
      setParticipations(
        await httpClient.get<MyProgrammeParticipations>(
          apiEndpoints.programmeParticipations.getMyParticipations,
        ),
      );
    } catch (requestError) {
      setError(
        getRequestErrorMessage(
          requestError,
          "Your programme participation could not be loaded.",
        ),
      );
    } finally {
      setLoading(false);
    }
  }, [canView]);

  useEffect(() => {
    const task = window.setTimeout(() => void loadParticipations(), 0);
    return () => window.clearTimeout(task);
  }, [loadParticipations]);

  useEffect(() => {
    const task = window.setTimeout(() => {
      const paymentResult = new URLSearchParams(window.location.search).get("payment");
      const programme = new URLSearchParams(window.location.search).get("programme");
      if (paymentResult === "success") {
        setSuccess(
          programme === "aqgreen"
            ? "Payment completed. We are waiting for Yoco's secure confirmation before activating your AQGreen participation."
            : "Payment completed. We are waiting for Yoco's secure confirmation before creating your Onyx participation.",
        );
      } else if (paymentResult === "cancelled") {
        setError(
          programme === "aqgreen"
            ? "Payment was cancelled. Your AQGreen place remains recorded, but it is not active."
            : "Payment was cancelled. No Onyx participation was created.",
        );
      } else if (paymentResult === "failed") {
        setError(
          programme === "aqgreen"
            ? "Payment was not completed. Your AQGreen participation is not active. You can try again below."
            : "Payment was not completed. No Onyx participation was created. You can try again below.",
        );
      }
    }, 0);
    return () => window.clearTimeout(task);
  }, []);

  const startAQGreenPayment = async () => {
    setStartingAQGreenPayment(true);
    setError(undefined);
    try {
      const checkout = await httpClient.post<{ checkoutUrl: string }>(
        apiEndpoints.programmeParticipations.createAQGreenJoiningCheckout,
      );
      navigateToExternalUrl(checkout.checkoutUrl);
    } catch (requestError) {
      setError(
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
            network starts independently or under an existing recruiter.
          </p>
        </header>

        {error ? <StatusMessage tone="error">{error}</StatusMessage> : null}
        {success ? (
          <StatusMessage tone="success">{success}</StatusMessage>
        ) : null}

        {participations?.entry?.canRecruitForThisProgramme ||
        participations?.onyx?.canRecruitForThisProgramme ? (
          <Card className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
            <div>
              <h2 className="text-lg font-bold">Grow your network</h2>
              <p className="mt-1 text-sm text-muted-foreground">
                Share a secure invitation link. Your friend will see your name
                and programme before confirming.
              </p>
            </div>
            <a className="inline-flex min-h-10 items-center justify-center rounded-lg bg-accent px-4 py-2 text-sm font-semibold text-white transition hover:bg-accent-dark" href="/member/invitations">
              Invite Club Members
            </a>
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
                  participations.entry.isActive ? undefined : participations.pendingAQGreenCheckout ? (
                    <a
                      className="inline-flex min-h-10 items-center justify-center rounded-lg bg-accent px-4 py-2 text-sm font-semibold text-white transition hover:bg-accent-dark"
                      href={participations.pendingAQGreenCheckout.checkoutUrl}
                    >
                      Continue secure payment
                    </a>
                  ) : participations.entry.nextPaymentAmount === 1200 ? (
                    <Button
                      isLoading={startingAQGreenPayment}
                      onClick={() => void startAQGreenPayment()}
                    >
                      Pay R1,200 securely
                    </Button>
                  ) : (
                    <StatusMessage tone="info">
                      Contact the club team to complete your previous AQGreen
                      payment arrangement without being charged again.
                    </StatusMessage>
                  )
                }
              />
            ) : (
              <Card className="flex flex-col items-start gap-4">
                <Route className="size-8 text-accent" />
                <div>
                  <h2 className="text-xl font-bold">AQGreen</h2>
                  <p className="mt-2 text-sm text-muted-foreground">
                    Start with AQGreen and work toward graduating into a separate
                    Onyx participation later. A recruiter is optional.
                  </p>
                </div>
                <JoinProgrammeDialog programme="AQGreen" />
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
                <a
                  className="inline-flex min-h-10 items-center justify-center rounded-lg bg-accent px-4 py-2 text-sm font-semibold text-white transition hover:bg-accent-dark"
                  href={participations.pendingDirectOnyxCheckout.checkoutUrl}
                >
                  Continue secure payment
                </a>
              </Card>
            ) : (
              <Card className="flex flex-col items-start gap-4">
                <Network className="size-8 text-accent" />
                <div>
                  <h2 className="text-xl font-bold">Onyx</h2>
                  <p className="mt-2 text-sm text-muted-foreground">
                    Join Onyx through its single direct joining path with the full
                    R6,120 payment. AQGreen is not required and a recruiter is optional.
                  </p>
                </div>
                <JoinProgrammeDialog programme="Onyx" />
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
