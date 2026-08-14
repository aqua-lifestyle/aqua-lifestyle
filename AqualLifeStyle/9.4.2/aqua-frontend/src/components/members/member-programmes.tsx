"use client";

import { useEffect, useRef, useState } from "react";

import {
  useAuthState,
  useSystemHealthActions,
  useSystemHealthState,
} from "@/src/providers";
import {
  isPaymentApiCompatible,
  isProgrammeJourneyApiCompatible,
} from "@/src/providers/SystemHealth/contract";
import {
  apiEndpoints,
  httpClient,
  refreshAccessToken,
} from "@/src/shared/api";
import { getRequestErrorMessage } from "@/src/shared/api/abp-error";
import { navigateToExternalUrl } from "@/src/shared/browser/navigation";
import { useMyProgrammeJourney } from "@/src/shared/hooks/use-my-programme-journey";
import { useMyProgrammeParticipations } from "@/src/shared/hooks/use-my-programme-participations";
import {
  Breadcrumb,
  Button,
  Skeleton,
  StatusMessage,
} from "@/src/shared/ui";
import { JoinProgrammeDialog } from "./join-programme-dialog";
import { ProgrammeJourneyOverview } from "./programme-journey-overview";

const VIEW_PERMISSION = "Aqua.ProgrammeParticipations.ViewSelf";

const formatCurrency = (amount: number, currency: string) =>
  new Intl.NumberFormat("en-ZA", {
    currency,
    style: "currency",
  }).format(amount);

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
  const journeyApiCompatible = isProgrammeJourneyApiCompatible(healthState.health);
  const isJourneyContractResolved = healthState.isSuccess || healthState.isError;
  const {
    data: journey,
    errorMessage: journeyError,
    isLoading: journeyLoading,
  } = useMyProgrammeJourney(
    canView && isJourneyContractResolved && journeyApiCompatible,
  );
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
    !isJourneyContractResolved || healthState.isPending || !paymentApiCompatible;
  const entryCanAcceptJoiningPayment = [
    "AwaitingJoiningPayment",
    "AwaitingActivationPayment",
  ].includes(participations?.entry?.statusCode ?? "");
  const journeyAcceptsAQGreenPayment = journey?.programmes.some(
    (programme) =>
      programme.programmeCode === "AQGREEN" &&
      programme.nextActionCode === "CompleteJoiningPayment",
  ) ?? false;

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
      .then(() => {
        // A null result means no refresh credential is available (ABP
        // TokenAuth issues no refresh token), not that the session is
        // invalid. Keep the member signed in and ask them to re-authenticate
        // to load the freshly granted permission instead of redirecting to the
        // sign-in page (which loops back here forever).
        setAccessRefreshFinished(true);
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

  const startAQGreenPayment = async (selectedSchedule?: 0 | 1) => {
    if (paymentActionsUnavailable) return;
    const schedule = selectedSchedule ??
      (participations?.entry?.joiningSchedule === 1 ? 1 : 0);
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

  const aqGreenPaymentAction = journeyAcceptsAQGreenPayment &&
    participations?.entry &&
    entryCanAcceptJoiningPayment ? (
      participations.pendingAQGreenCheckout ? (
        paymentActionsUnavailable ? (
          <Button className="w-full" disabled>Continue secure payment</Button>
        ) : (
          <a
            className="inline-flex min-h-11 w-full items-center justify-center rounded-lg bg-accent px-4 py-2 text-sm font-semibold text-white transition hover:bg-accent-dark focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent focus-visible:ring-offset-2"
            href={participations.pendingAQGreenCheckout.checkoutUrl}
          >
            Continue secure payment
          </a>
        )
      ) : participations.entry.joiningSchedule == null &&
        (participations.entry.joiningPaidAmount ?? 0) === 0 ? (
        <div className="grid gap-3 sm:grid-cols-2">
          <Button
            disabled={paymentActionsUnavailable}
            isLoading={startingAQGreenPayment}
            onClick={() => void startAQGreenPayment(0)}
          >
            Pay R1,200 once
          </Button>
          <Button
            disabled={paymentActionsUnavailable}
            isLoading={startingAQGreenPayment}
            onClick={() => void startAQGreenPayment(1)}
            variant="outline"
          >
            Pay first R600 instalment
          </Button>
        </div>
      ) : participations.entry.nextPaymentAmount != null &&
        participations.entry.nextPaymentAmount > 0 ? (
        <Button
          className="w-full"
          disabled={paymentActionsUnavailable}
          isLoading={startingAQGreenPayment}
          onClick={() => void startAQGreenPayment()}
        >
          Complete joining: {formatCurrency(
            participations.entry.nextPaymentAmount,
            participations.entry.currency,
          )}
        </Button>
      ) : (
        <StatusMessage tone="error">
          The next joining-payment amount is unavailable. No payment can be
          started until the API provides the authoritative amount.
        </StatusMessage>
      )
    ) : undefined;

  const aqGreenJoinAction = !participations?.entry ? (
    <JoinProgrammeDialog
      disabled={paymentActionsUnavailable}
      programme="AQGreen"
    />
  ) : undefined;

  const onyxJoinAction = participations?.pendingDirectOnyxCheckout ? (
    paymentActionsUnavailable ? (
      <Button className="w-full" disabled>Continue secure payment</Button>
    ) : (
      <a
        className="inline-flex min-h-11 w-full items-center justify-center rounded-lg bg-accent px-4 py-2 text-sm font-semibold text-white transition hover:bg-accent-dark focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent focus-visible:ring-offset-2"
        href={participations.pendingDirectOnyxCheckout.checkoutUrl}
      >
        Continue secure payment
      </a>
    )
  ) : !participations?.onyx ? (
    <JoinProgrammeDialog
      disabled={paymentActionsUnavailable}
      programme="Onyx"
    />
  ) : undefined;

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
          <h1 className="mt-2 text-3xl font-bold tracking-tight">My programme journey</h1>
          <p className="mt-2 max-w-3xl text-muted-foreground">
            See where you are, what you have completed, what comes next, and how
            your network progress connects to earnings and benefits.
          </p>
          <p className="mt-3 text-sm font-semibold text-foreground">
            Area: {participations?.areaName ?? "Not assigned"}
          </p>
        </header>

        {[loadError, journeyError, actionError].filter(Boolean).map((message) => (
          <StatusMessage key={message} tone="error">{message}</StatusMessage>
        ))}
        {success ? (
          <StatusMessage tone="info">{success}</StatusMessage>
        ) : null}

        {participations?.pendingDirectOnyxCheckout && !participations.onyx ? (
          <StatusMessage tone="info">
            <strong>Awaiting payment</strong>. Your Onyx participation and network
            place do not exist yet. Continue the existing secure checkout when
            you are ready.
          </StatusMessage>
        ) : null}

        {healthState.isSuccess && !paymentApiCompatible ? (
          <StatusMessage tone="error">
            Payments are unavailable because this frontend cannot verify a
            compatible payment API deployment. No payment has been taken. Ask
            an operator to deploy and verify the matching database, API, and
            frontend versions.
          </StatusMessage>
        ) : null}

        {healthState.isError ? (
          <StatusMessage tone="error">
            {healthState.errorMessage ?? "The API capability check could not be completed."}
          </StatusMessage>
        ) : null}

        {!healthState.isPending && healthState.isSuccess && !journeyApiCompatible ? (
          <StatusMessage tone="error">
            The programme journey is unavailable because this API does not
            advertise the required member journey capability. Payment controls
            remain fail-closed on this page.
          </StatusMessage>
        ) : null}

        {hasActiveInvitationAccess && !hasInvitationPermission ? (
          <StatusMessage tone="info">
            {accessRefreshFinished
              ? "Sign out and sign in again to load your updated invitation access."
              : "Updating your Club Member invitation access…"}
          </StatusMessage>
        ) : null}

        {loading || journeyLoading ? (
          <div aria-busy="true" aria-label="Loading programme journey" className="flex flex-col gap-5" role="status">
            <Skeleton className="h-80" />
            <Skeleton className="h-96" />
          </div>
        ) : participations && journey ? (
          <div className="flex flex-col gap-12">
            {journey.programmes.map((programme) => (
              <ProgrammeJourneyOverview
                canInvite={hasInvitationPermission}
                joinAction={programme.programmeCode === "AQGREEN" ? aqGreenJoinAction : onyxJoinAction}
                journey={programme}
                key={programme.programmeCode}
                paymentAction={programme.programmeCode === "AQGREEN" ? aqGreenPaymentAction : undefined}
              />
            ))}
          </div>
        ) : null}
      </div>
    </main>
  );
};
