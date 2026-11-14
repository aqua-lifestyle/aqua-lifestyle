"use client";

import { Network, ShieldCheck } from "lucide-react";
import { useEffect, useRef, useState } from "react";

import {
  useAuthState,
  useSystemHealthActions,
  useSystemHealthState,
} from "@/src/providers";
import { isAQGreenV2DemoCheckout, isPaymentApiCompatible, isPilotAQGreenJoiningApiCompatible } from "@/src/providers/SystemHealth/contract";
import { apiEndpoints, httpClient } from "@/src/shared/api";
import { getRequestErrorPresentation, type RequestErrorPresentation } from "@/src/shared/api/abp-error";
import type { ProgrammeInvitationPreview } from "@/src/shared/domain/programme-invitations";
import type { ProgrammeCheckout, ProgrammeParticipation } from "@/src/shared/domain/programme-participations";
import { navigateToExternalUrl } from "@/src/shared/browser/navigation";
import { usePilotStatus } from "@/src/shared/hooks/use-pilot-status";
import {
  useCheckoutReview,
  type JoiningCheckoutStage,
} from "@/src/shared/hooks/use-checkout-review";
import { Button, Card, LinkButton, Skeleton, StatusMessage } from "@/src/shared/ui";

const unsupportedProgrammeMessage =
  "Invitations are not currently supported for this programme.";

const formatAmount = (amount: number, currency: string) =>
  new Intl.NumberFormat("en-ZA", {
    currency,
    style: "currency",
  }).format(amount);

const getProgrammeJoinEndpoint = (programmeKey: string) => {
  switch (programmeKey) {
    case "AQGREEN":
      return apiEndpoints.programmeParticipations.startEntry;
    case "ONYX":
      return apiEndpoints.programmeParticipations.createDirectOnyxCheckout;
    default:
      return undefined;
  }
};

const getProgrammePaymentExplanation = (programmeKey: string, demo: boolean) => {
  switch (programmeKey) {
    case "AQGREEN":
      return demo
        ? "Accepting records your sponsor attribution. AQGreen joining is R1,200 once or two R600 instalments; a separate R600 monthly obligation applies only after activation, but its due policy remains unresolved. The exact joining amount is shown at a simulated checkout. No real payment, insurance cover or earnings occur. Area approval and placement follow full test confirmation."
        : "Confirming records your AQGreen place under this inviting Club Member. AQGreen joining is R1,200, paid once or as two R600 instalments. Area Administrator approval is required after the full amount is confirmed.";
    case "ONYX":
      return "Confirming continues to Yoco for the full R6,120 payment. Your Onyx participation and network place are created after confirmation, then remain inactive until Area Administrator approval.";
    default:
      return undefined;
  }
};

export const ProgrammeInvitationLanding = ({ inviteCode }: { inviteCode: string }) => {
  const { isPilot, modeKnown } = usePilotStatus();
  const { session } = useAuthState();
  const canJoin = session?.user?.permissions?.includes("Aqua.ProgrammeParticipations.Join") ?? false;
  const healthActions = useSystemHealthActions();
  const healthState = useSystemHealthState();
  const contractCheckAttempted = useRef(false);
  const [loadedPreview, setPreview] = useState<ProgrammeInvitationPreview>();
  const preview = loadedPreview?.inviteCode.trim().toUpperCase() === inviteCode.trim().toUpperCase()
    ? loadedPreview : undefined;
  const [loading, setLoading] = useState(true);
  const [joining, setJoining] = useState(false);
  const [error, setError] = useState<RequestErrorPresentation>();
  const [aqGreenSchedule, setAQGreenSchedule] = useState<0 | 1>(0);
  const [entryRecord, setEntryRecord] = useState<ProgrammeParticipation>();
  const entryStage: JoiningCheckoutStage | undefined = entryRecord
    ? entryRecord.joiningPaidAmount === 0
      ? "unpaid"
      : entryRecord.joiningSchedule === 1 && entryRecord.joiningPaidAmount === 600
        ? "part-paid"
        : undefined
    : undefined;
  const recordedPlan: {
    stage: JoiningCheckoutStage;
    schedule: 0 | 1;
    amount: number;
    currency: string;
  } | null = entryRecord && entryStage != null &&
      entryRecord.nextPaymentAmount != null && entryRecord.nextPaymentAmount > 0 &&
      (entryStage === "part-paid" ||
        entryRecord.joiningSchedule === 0 || entryRecord.joiningSchedule === 1)
    ? {
        stage: entryStage,
        schedule: entryStage === "part-paid" ? 1 : entryRecord.joiningSchedule as 0 | 1,
        amount: entryRecord.nextPaymentAmount,
        currency: entryRecord.currency,
      }
    : null;
  const instalmentEvidence = {
    firstInstalmentConfirmed: (entryRecord?.joiningPaidAmount ?? 0) > 0,
    joiningFullyPaid: ["PaymentConfirmedAwaitingApproval", "Active"].includes(
      entryRecord?.statusCode ?? "",
    ),
  };
  const checkoutReview = useCheckoutReview(session?.user, instalmentEvidence);
  const demoCheckout = isAQGreenV2DemoCheckout(healthState.health);
  const paymentApiCompatible = isPilot
    ? isPilotAQGreenJoiningApiCompatible(healthState.health)
    : isPaymentApiCompatible(healthState.health);
  const paymentActionsUnavailable =
    !canJoin || !modeKnown || healthState.isPending || !paymentApiCompatible || (isPilot && preview?.programmeKey !== "AQGREEN");

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
    let current = true;
    void httpClient
      .get<ProgrammeInvitationPreview>(
        apiEndpoints.programmeParticipations.getInvitationPreview(inviteCode),
      )
      .then((value) => {
        if (!current) return;
        if (value.inviteCode?.trim().toUpperCase() !== inviteCode.trim().toUpperCase())
          throw new Error("INVITATION_CONTEXT_MISMATCH");
        setPreview(value);
        setError(undefined);
      })
      .catch((requestError) => {
        if (current) setError(getRequestErrorPresentation(requestError, "This invitation could not be opened.", "invitation"));
      })
      .finally(() => { if (current) setLoading(false); });
    return () => { current = false; };
  }, [inviteCode]);

  const confirmAQGreenInvitation = async () => {
    if (!preview || !preview.recruiterEligible || paymentActionsUnavailable || joining || error)
      return;
    setJoining(true);
    setError(undefined);
    let checkoutAttempted = false;
    try {
      const participation = await httpClient.post<ProgrammeParticipation, { inviteCode: string }>(
        apiEndpoints.programmeParticipations.startEntry,
        { inviteCode: preview.inviteCode },
      );
      setEntryRecord(participation);
      if (participation.joiningPaidAmount === 0 && participation.joiningSchedule == null) {
        // A newly recorded participation has no schedule yet; the selected
        // schedule starts the first checkout.
        checkoutAttempted = true;
        const checkout = await httpClient.post<ProgrammeCheckout, { schedule: 0 | 1 }>(
          apiEndpoints.programmeParticipations.createAQGreenJoiningCheckout,
          { schedule: aqGreenSchedule },
        );
        if (isPilot) window.location.assign("/member/programmes");
        else navigateToExternalUrl(checkout.checkoutUrl);
      }
      // An existing participation keeps its recorded schedule and confirmed
      // instalments; the recorded-plan action below continues them without a
      // schedule change.
    } catch (requestError) {
      setError(
        getRequestErrorPresentation(
          requestError,
          "Your joining status could not be confirmed.",
          checkoutAttempted ? "checkout" : "admission",
        ),
      );
    } finally {
      setJoining(false);
    }
  };

  const continueRecordedInstalment = async (
    schedule: 0 | 1,
    stage: JoiningCheckoutStage,
  ) => {
    if (!preview || !preview.recruiterEligible || paymentActionsUnavailable || joining || error)
      return;
    if (!checkoutReview.start(stage)) {
      setError({ title: "Check your payment status", message: "Review your existing joining or payment status before starting another checkout.", reviewPaymentStatus: true });
      return;
    }
    setJoining(true);
    setError(undefined);
    try {
      const checkout = await httpClient.post<ProgrammeCheckout, { schedule: 0 | 1 }>(
        apiEndpoints.programmeParticipations.createAQGreenJoiningCheckout,
        { schedule },
      );
      if (isPilot) window.location.assign("/member/programmes");
      else navigateToExternalUrl(checkout.checkoutUrl);
    } catch (requestError) {
      setError(
        getRequestErrorPresentation(
          requestError,
          "Your joining status could not be confirmed.",
          "checkout",
        ),
      );
    } finally {
      setJoining(false);
    }
  };

  const confirm = async () => {
    if (!preview || !preview.recruiterEligible || paymentActionsUnavailable || joining || error)
      return;
    const endpoint = getProgrammeJoinEndpoint(preview.programmeKey);
    if (!endpoint) {
      setError({ title: "Programme unavailable", message: unsupportedProgrammeMessage });
      return;
    }
    if (preview.programmeKey === "ONYX") {
      setJoining(true);
      setError(undefined);
      try {
        const checkout = await httpClient.post<ProgrammeCheckout, { inviteCode: string }>(
          endpoint,
          { inviteCode: preview.inviteCode },
        );
        navigateToExternalUrl(checkout.checkoutUrl);
      } catch (requestError) {
        setError(
          getRequestErrorPresentation(
            requestError,
            "Your joining status could not be confirmed.",
            "checkout",
          ),
        );
      } finally {
        setJoining(false);
      }
      return;
    }
    await confirmAQGreenInvitation();
  };

  const redirect = `/i/${encodeURIComponent(inviteCode)}`;
  const signupUrl = preview && typeof preview.tenancyName === "string"
    ? `/signup?area=${encodeURIComponent(preview.tenancyName)}&invite=${encodeURIComponent(preview.inviteCode)}&redirect=${encodeURIComponent(redirect)}`
    : "/signup";
  const loginUrl = preview && typeof preview.tenancyName === "string"
    ? `/login?area=${encodeURIComponent(preview.tenancyName)}&invite=${encodeURIComponent(preview.inviteCode)}&redirect=${encodeURIComponent(redirect)}`
    : `/login?redirect=${encodeURIComponent(redirect)}`;
  const programmeJoinEndpoint = preview
    ? getProgrammeJoinEndpoint(preview.programmeKey)
    : undefined;
  const programmePaymentExplanation = preview
    ? getProgrammePaymentExplanation(preview.programmeKey, Boolean(demoCheckout))
    : undefined;

  return (
    <main className="min-h-dvh bg-muted/30 px-4 py-10 sm:px-6">
      <div className="mx-auto max-w-2xl">
        {loading || (!preview && !error) ? (
          <Skeleton className="h-96" />
        ) : error && !preview ? (
          <StatusMessage title={error.title} tone="error">{error.message}</StatusMessage>
        ) : preview ? (
          <Card className="flex flex-col gap-6">
            <div className="text-center">
              <div className="mx-auto flex size-14 items-center justify-center rounded-2xl bg-accent/10 text-accent">
                <Network className="size-7" />
              </div>
              <p className="mt-4 text-sm font-semibold uppercase tracking-wider text-accent">
                Aqua Lifestyle Club invitation
              </p>
              <h1 className="mt-2 text-3xl font-bold">Join {preview.programmeName}</h1>
            </div>

            <div className="rounded-xl border border-border p-5">
              <p className="text-sm text-muted-foreground">Inviting Club Member</p>
              <p className="mt-1 text-xl font-bold">{preview.recruiterName}</p>
              <p className="mt-1 font-mono text-sm text-muted-foreground">
                {preview.recruiterClubMemberNumber}
              </p>
              {preview.areaName ? (
                <p className="mt-2 text-sm text-muted-foreground">
                  Business Area: {preview.areaName}
                </p>
              ) : null}
              <div className="mt-4 flex items-center gap-2 text-sm font-medium text-success">
                <ShieldCheck className="size-4" />
                {preview.recruiterEligible
                  ? `Eligible to invite Club Members to ${preview.programmeName}`
                  : "Invitation access is currently unavailable"}
              </div>
            </div>

            {programmePaymentExplanation ? (
              <StatusMessage tone="info">
                {programmePaymentExplanation}
              </StatusMessage>
            ) : null}

            {preview.programmeKey === "AQGREEN" && session && preview.recruiterEligible && !entryRecord ? (
              <fieldset className="grid gap-3">
                <legend className="text-sm font-semibold">Payment schedule</legend>
                <label className="flex cursor-pointer items-center gap-3 rounded-lg border border-border p-4">
                  <input
                    checked={aqGreenSchedule === 0}
                    name="invitation-aqgreen-payment-schedule"
                    onChange={() => setAQGreenSchedule(0)}
                    type="radio"
                  />
                  <span className="font-medium">Pay R1,200 once</span>
                </label>
                <label className="flex cursor-pointer items-center gap-3 rounded-lg border border-border p-4">
                  <input
                    checked={aqGreenSchedule === 1}
                    name="invitation-aqgreen-payment-schedule"
                    onChange={() => setAQGreenSchedule(1)}
                    type="radio"
                  />
                  <span className="font-medium">Pay two R600 instalments</span>
                </label>
              </fieldset>
            ) : null}

            {!healthState.isPending && !paymentApiCompatible && session ? (
              <StatusMessage title="Payments are temporarily unavailable" tone="error">
                {healthState.health?.paymentAvailability === "Held" ? (
                  <>Payments are temporarily on hold. Please check back later or contact the club team for an update.</>
                ) : (<>We could not confirm that payments are available. Refresh the page or contact the club team before trying to pay.</>)}
              </StatusMessage>
            ) : null}
            {session && !canJoin ? (
              <StatusMessage title="Joining access needs review" tone="warning">
                Contact the club team to check your joining access before continuing. Your invitation does not replace account permissions.
              </StatusMessage>
            ) : null}

            {session && preview.programmeKey === "AQGREEN" && preview.recruiterEligible ? (
              recordedPlan ? (
                error || checkoutReview.blockedFor(recordedPlan.stage) ? (
                  error ? null : (
                    <StatusMessage title="Check your payment status" tone="warning">
                      Review your existing joining or payment status before starting another checkout.
                      <LinkButton className="mt-3" href="/member/programmes">Check my payment status</LinkButton>
                    </StatusMessage>
                  )
                ) : (
                  <>
                    <StatusMessage tone="info">
                      {recordedPlan.stage === "part-paid"
                        ? "Your first joining instalment is confirmed. Complete the remaining joining payment to finish joining."
                        : "Your joining payment plan is already recorded. Continue with it below."}
                    </StatusMessage>
                    <Button
                      disabled={paymentActionsUnavailable || !!error}
                      isLoading={joining}
                      onClick={() => void continueRecordedInstalment(recordedPlan.schedule, recordedPlan.stage)}
                    >
                      {demoCheckout
                        ? "Continue simulated checkout"
                        : `Complete joining: ${formatAmount(recordedPlan.amount, recordedPlan.currency)}`}
                    </Button>
                  </>
                )
              ) : entryRecord ? (
                error ? null : entryStage === "unpaid" ? (
                  <Button disabled isLoading={joining}>
                    {demoCheckout
                      ? "Accept and open simulated checkout"
                      : "Confirm and continue to payment"}
                  </Button>
                ) : (
                  <StatusMessage title="Check your joining status" tone="warning">
                    Your invitation has been processed. Check My programmes for your current joining and payment status before continuing.
                    <LinkButton className="mt-3" href="/member/programmes">Check my payment status</LinkButton>
                  </StatusMessage>
                )
              ) : (
                <Button
                  disabled={
                    !preview.recruiterEligible || paymentActionsUnavailable || !!error
                  }
                  isLoading={joining}
                  onClick={() => void confirm()}
                >
                  {preview.programmeKey === "AQGREEN" && demoCheckout
                    ? "Accept and open simulated checkout"
                    : "Confirm and continue to payment"}
                </Button>
              )
            ) : !preview.recruiterEligible ? (
              <StatusMessage title="This invitation is not ready to use" tone="warning">
                The inviting member is not currently eligible to invite you to this programme. Ask them or the club team to check their participation before you continue.
                <LinkButton className="mt-3" href="/contact">Contact the club team</LinkButton>
              </StatusMessage>
            ) : !programmeJoinEndpoint ? (
              <StatusMessage tone="error">
                {unsupportedProgrammeMessage}
              </StatusMessage>
            ) : session ? (
              <Button
                disabled={
                  !preview.recruiterEligible || paymentActionsUnavailable || !!error
                }
                isLoading={joining}
                onClick={() => void confirm()}
              >
                {preview.programmeKey === "AQGREEN" && demoCheckout
                  ? "Accept and open simulated checkout"
                  : "Confirm and continue to payment"}
              </Button>
            ) : (
              <div className="grid gap-3 sm:grid-cols-2">
                <LinkButton href={signupUrl}>Create my account</LinkButton>
                <LinkButton href={loginUrl} variant="outline">Sign in to continue</LinkButton>
              </div>
            )}

            {error ? (
              <StatusMessage title={error.title} tone="error">
                <p>{error.message}</p>
                <LinkButton className="mt-3" href="/member/programmes">
                  {error.reviewPaymentStatus ? "Check my payment status" : "Return to My programmes"}
                </LinkButton>
              </StatusMessage>
            ) : null}
          </Card>
        ) : null}
      </div>
    </main>
  );
};
