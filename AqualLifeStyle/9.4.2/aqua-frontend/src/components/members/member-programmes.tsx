"use client";

import { useEffect, useRef, useState } from "react";

import {
  useAuthState,
  useSystemHealthActions,
  useSystemHealthState,
} from "@/src/providers";
import {
  isAQGreenV2DemoCheckout,
  isPaymentApiCompatible,
  isPilotAQGreenJoiningApiCompatible,
  isProgrammeJourneyApiCompatible,
} from "@/src/providers/SystemHealth/contract";
import {
  apiEndpoints,
  httpClient,
  refreshAccessToken,
} from "@/src/shared/api";
import {
  aqGreenAdmissionGuidance,
  getRequestErrorPresentation,
  type RequestErrorPresentation,
} from "@/src/shared/api/abp-error";
import { navigateToExternalUrl } from "@/src/shared/browser/navigation";
import { useMyProgrammeJourney } from "@/src/shared/hooks/use-my-programme-journey";
import { useMyProgrammeParticipations } from "@/src/shared/hooks/use-my-programme-participations";
import { usePilotStatus } from "@/src/shared/hooks/use-pilot-status";
import { useCheckoutReview, type JoiningCheckoutStage } from "@/src/shared/hooks/use-checkout-review";
import {
  Breadcrumb,
  Button,
  LinkButton,
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
  const { isPilot, modeKnown } = usePilotStatus();
  const { session } = useAuthState();
  const healthActions = useSystemHealthActions();
  const healthState = useSystemHealthState();
  const canView =
    session?.user?.permissions?.includes(VIEW_PERMISSION) ?? false;
  const canJoin = session?.user?.permissions?.includes("Aqua.ProgrammeParticipations.Join") ?? false;
  const {
    data: participations,
    errorMessage: loadError,
    isLoading: loading,
    reload: reloadParticipations,
  } = useMyProgrammeParticipations(canView);
  const journeyApiCompatible = isProgrammeJourneyApiCompatible(healthState.health);
  const isJourneyContractResolved = healthState.isSuccess || healthState.isError;
  const {
    data: journey,
    errorMessage: journeyError,
    isLoading: journeyLoading,
  } = useMyProgrammeJourney(
    canView && modeKnown && isJourneyContractResolved && journeyApiCompatible,
    isPilot,
  );
  const [actionError, setActionError] = useState<RequestErrorPresentation>();
  const [success, setSuccess] = useState<string>();
  const [startingAQGreenPayment, setStartingAQGreenPayment] = useState(false);
  const [accessRefreshFinished, setAccessRefreshFinished] = useState(false);
  const checkoutReview = useCheckoutReview(session?.user);
  const joiningStage: JoiningCheckoutStage | undefined = participations?.entry?.joiningPaidAmount === 0
    ? "unpaid" : participations?.entry?.joiningSchedule === 1 && participations.entry.joiningPaidAmount === 600
      ? "part-paid" : undefined;
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
  const demoCheckout = isAQGreenV2DemoCheckout(healthState.health);
  const paymentApiCompatible = isPilot
    ? isPilotAQGreenJoiningApiCompatible(healthState.health)
    : isPaymentApiCompatible(healthState.health);
  const paymentActionsUnavailable =
    !canJoin || !modeKnown || !isJourneyContractResolved || healthState.isPending || !paymentApiCompatible;
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
        setActionError({ title: "Refresh your account access", message:
          "Your programme is active, but your updated account access could not be loaded. Check your connection and try signing in again.",
        });
      });
  }, [hasActiveInvitationAccess, hasInvitationPermission]);

  useEffect(() => {
    const task = window.setTimeout(() => {
      const paymentResult = new URLSearchParams(window.location.search).get("payment");
      const programme = new URLSearchParams(window.location.search).get("programme");
      if (paymentResult === "success") {
        setSuccess(
          "You have returned from checkout. Your verified payment and membership status are shown below; returning here does not confirm payment or activation.",
        );
      } else if (paymentResult === "cancelled") {
        setActionError({ title: "Payment status not confirmed", reviewPaymentStatus: true, message:
          programme === "aqgreen"
            ? "The browser return does not confirm your payment result. Review your current joining status below. Do not start another payment unless the existing result has been confirmed."
            : "You returned without payment confirmation. No Onyx participation was created; the existing checkout remains pending.",
        });
      } else if (paymentResult === "failed") {
        setActionError({ title: "Payment status not confirmed", reviewPaymentStatus: true, message:
          programme === "aqgreen"
            ? "The browser return does not confirm payment failure. Review your current joining status below. Do not start another payment unless the existing result has been confirmed."
            : "The browser returned from payment, but this does not confirm failure. No Onyx participation was created and the checkout remains pending.",
        });
      }
    }, 0);
    return () => window.clearTimeout(task);
  }, []);

  const startAQGreenPayment = async (selectedSchedule?: 0 | 1) => {
    if (paymentActionsUnavailable) return;
    const schedule = selectedSchedule ?? participations?.entry?.joiningSchedule;
    if (schedule !== 0 && schedule !== 1) return;
    if (!joiningStage || !checkoutReview.start(joiningStage)) {
      setActionError({ title: "Check your payment status", message: "Your previous checkout or browser payment status needs to be checked. Refresh My programmes or contact the club team before starting another payment.", reviewPaymentStatus: true });
      return;
    }
    setStartingAQGreenPayment(true);
    setActionError(undefined);
    try {
      const checkout = await httpClient.post<
        { checkoutUrl: string; checkoutId: string },
        { schedule: 0 | 1 }
      >(
        apiEndpoints.programmeParticipations.createAQGreenJoiningCheckout,
        { schedule },
      );
      if (isPilot) await reloadParticipations();
      else navigateToExternalUrl(checkout.checkoutUrl);
    } catch (requestError) {
      setActionError(
        getRequestErrorPresentation(
          requestError,
          "Your AQGreen payment status could not be confirmed.",
          "checkout",
        ),
      );
    } finally {
      setStartingAQGreenPayment(false);
    }
  };

  const confirmTestPayment = async (checkoutId?: string) => {
    if (!isPilot || !checkoutId || paymentActionsUnavailable) return;
    setStartingAQGreenPayment(true);
    setActionError(undefined);
    try {
      await httpClient.post("/api/services/app/AQGreenPilot/ConfirmTestPayment", { checkoutId });
      setSuccess("Test joining payment verified. Area approval is still required for activation.");
      await reloadParticipations();
      window.location.reload();
    } catch (error) {
      setActionError(getRequestErrorPresentation(error, "Test payment could not be verified.", "checkout"));
    } finally { setStartingAQGreenPayment(false); }
  };

  const paymentStatusNotice = (
    <StatusMessage title="Check your payment status" tone="warning">
      <p>Your existing checkout or payment result needs to be checked before a new payment is started. Contact the club team if it remains unclear.</p>
      <Button className="mt-3" onClick={() => window.location.reload()} variant="outline">
        Refresh payment status
      </Button>
    </StatusMessage>
  );

  const aqGreenPaymentAction = journeyAcceptsAQGreenPayment &&
    participations?.entry &&
    entryCanAcceptJoiningPayment ? (
      participations.pendingAQGreenCheckout ? (
        participations.pendingAQGreenCheckout.status !== "Awaiting payment" ||
        !participations.pendingAQGreenCheckout.checkoutUrl ? paymentStatusNotice : (
        isPilot ? (
          <Button disabled={paymentActionsUnavailable || !participations.pendingAQGreenCheckout.checkoutId}
            isLoading={startingAQGreenPayment}
            onClick={() => void confirmTestPayment(participations.pendingAQGreenCheckout?.checkoutId)}>
            Confirm test joining payment
          </Button>
        ) : paymentActionsUnavailable ? (
          <Button className="w-full" disabled>{demoCheckout ? "Continue test checkout" : "Continue secure payment"}</Button>
        ) : (
          <a
            className="inline-flex min-h-11 w-full items-center justify-center rounded-lg bg-accent px-4 py-2 text-sm font-semibold text-white transition hover:bg-accent-dark focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent focus-visible:ring-offset-2"
            href={participations.pendingAQGreenCheckout.checkoutUrl}
          >
            {demoCheckout ? "Continue test checkout" : "Continue secure payment"}
          </a>
        ))
      ) : (actionError?.reviewPaymentStatus && !checkoutReview.advancedTo(joiningStage)) ||
        (!startingAQGreenPayment && checkoutReview.blockedFor(joiningStage)) ? paymentStatusNotice
      : participations.entry.joiningSchedule == null &&
        participations.entry.joiningPaidAmount === 0 ? (
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
      ) : (participations.entry.joiningSchedule === 0 || participations.entry.joiningSchedule === 1) &&
        participations.entry.nextPaymentAmount != null &&
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
        <StatusMessage title="Joining payment details unavailable" tone="error">
          We could not confirm your next joining payment. Refresh My programmes or contact the club team before trying to pay.
        </StatusMessage>
      )
    ) : undefined;

  const aqGreenJoinAction = !participations?.entry ? (
    <StatusMessage title={aqGreenAdmissionGuidance.title} tone="info">
      <p>{aqGreenAdmissionGuidance.message}</p>
      <LinkButton className="mt-3" href="/contact">Contact the club team</LinkButton>
    </StatusMessage>
  ) : undefined;

  const onyxJoinAction = isPilot ? undefined : participations?.pendingDirectOnyxCheckout ? (
    !participations.pendingDirectOnyxCheckout.checkoutUrl ? paymentStatusNotice : paymentActionsUnavailable ? (
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

        {[loadError, journeyError].filter(Boolean).map((message) => (
          <StatusMessage key={message} title="Programme details unavailable" tone="error">{message}</StatusMessage>
        ))}
        {actionError ? (
          <StatusMessage title={actionError.title} tone="error">{actionError.message}</StatusMessage>
        ) : null}
        {success ? (
          <StatusMessage tone="info">{success}</StatusMessage>
        ) : null}
        {!canJoin ? (
          <StatusMessage title="Joining access needs review" tone="warning">
            Your account does not currently have access to start a joining payment. Contact the club team to check your access before continuing.
          </StatusMessage>
        ) : null}

        {participations?.pendingDirectOnyxCheckout && !participations.onyx ? (
          <StatusMessage tone="info">
            <strong>Awaiting payment</strong>. Your Onyx participation and network
            place do not exist yet. Continue the existing secure checkout when
            you are ready.
          </StatusMessage>
        ) : null}

        {healthState.isSuccess && !paymentApiCompatible ? (
          <StatusMessage title="Payments are temporarily unavailable" tone="error">
            {healthState.health?.paymentAvailability === "Held" ? (
              <>Payments are temporarily on hold. Please check back later or contact the club team for an update.</>
            ) : (<>We could not confirm that payments are available. Refresh My programmes or contact the club team. If you have already tried to pay, check that payment&apos;s status before starting another one.</>)}
          </StatusMessage>
        ) : null}

        {healthState.isError ? (
          <StatusMessage title="Payment availability not confirmed" tone="error">
            We could not confirm that payments are available. Refresh the page or contact the club team before trying to pay.
          </StatusMessage>
        ) : null}

        {!healthState.isPending && healthState.isSuccess && !journeyApiCompatible ? (
          <StatusMessage title="Programme information unavailable" tone="error">
            Your programme information could not be confirmed. Refresh My programmes or contact the club team before joining or paying.
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
                demoTestMoney={Boolean(demoCheckout) && programme.programmeCode === "AQGREEN"}
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
