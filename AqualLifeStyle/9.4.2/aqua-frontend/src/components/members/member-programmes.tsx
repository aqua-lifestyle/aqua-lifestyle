"use client";

import { CircleDollarSign, Network, Plane, Route } from "lucide-react";
import { useCallback, useEffect, useState } from "react";

import { useAuthState } from "@/src/providers";
import { apiEndpoints, httpClient } from "@/src/shared/api";
import { getRequestErrorMessage } from "@/src/shared/api/abp-error";
import type {
  MyProgrammeParticipations,
  OnyxTravelBenefit,
  ProgrammeParticipation,
} from "@/src/shared/domain/programme-participations";
import {
  Badge,
  Breadcrumb,
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
  participation,
}: {
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
            : `Under Club Member #${participation.recruiterCustomerId}`}
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
              Payment instructions will be provided by the club. Participation
              activates only after the payment provider confirms receipt.
            </p>
          </div>
        </div>
      </div>
    ) : null}
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
            Join Entry or Onyx, follow activation progress, and see whether your
            network starts independently or under an existing recruiter.
          </p>
        </header>

        {error ? <StatusMessage tone="error">{error}</StatusMessage> : null}

        {loading ? (
          <div className="grid gap-5 lg:grid-cols-2">
            <Skeleton className="h-80" />
            <Skeleton className="h-80" />
          </div>
        ) : participations ? (
          <div className="grid gap-5 lg:grid-cols-2">
            {participations.entry ? (
              <ParticipationCard participation={participations.entry} />
            ) : (
              <Card className="flex flex-col items-start gap-4">
                <Route className="size-8 text-accent" />
                <div>
                  <h2 className="text-xl font-bold">Entry</h2>
                  <p className="mt-2 text-sm text-muted-foreground">
                    Start with the feeder programme and qualify for a separate
                    Onyx participation later. A recruiter is optional.
                  </p>
                </div>
                <JoinProgrammeDialog
                  onJoined={loadParticipations}
                  programme="Entry"
                />
              </Card>
            )}

            {participations.onyx ? (
              <ParticipationCard participation={participations.onyx} />
            ) : (
              <Card className="flex flex-col items-start gap-4">
                <Network className="size-8 text-accent" />
                <div>
                  <h2 className="text-xl font-bold">Onyx</h2>
                  <p className="mt-2 text-sm text-muted-foreground">
                    Join Onyx directly with the full R6,120 payment. Entry
                    participation is not required and a recruiter is optional.
                  </p>
                </div>
                <JoinProgrammeDialog
                  onJoined={loadParticipations}
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
