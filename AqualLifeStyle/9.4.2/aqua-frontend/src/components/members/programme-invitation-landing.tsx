"use client";

import { Network, ShieldCheck } from "lucide-react";
import { useEffect, useRef, useState } from "react";

import {
  useAuthState,
  useSystemHealthActions,
  useSystemHealthState,
} from "@/src/providers";
import { isPaymentApiCompatible } from "@/src/providers/SystemHealth/contract";
import { apiEndpoints, httpClient } from "@/src/shared/api";
import { getRequestErrorMessage } from "@/src/shared/api/abp-error";
import type { ProgrammeInvitationPreview } from "@/src/shared/domain/programme-invitations";
import type { ProgrammeCheckout } from "@/src/shared/domain/programme-participations";
import { navigateToExternalUrl } from "@/src/shared/browser/navigation";
import { Button, Card, LinkButton, Skeleton, StatusMessage } from "@/src/shared/ui";

const unsupportedProgrammeMessage =
  "Invitations are not currently supported for this programme.";

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

const getProgrammePaymentExplanation = (programmeKey: string) => {
  switch (programmeKey) {
    case "AQGREEN":
      return "Confirming records your AQGreen place under this inviting Club Member. AQGreen joining is R1,200, paid once or as two R600 instalments. Area Administrator approval is required after the full amount is confirmed.";
    case "ONYX":
      return "Confirming continues to Yoco for the full R6,120 payment. Your Onyx participation and network place are created after confirmation, then remain inactive until Area Administrator approval.";
    default:
      return undefined;
  }
};

export const ProgrammeInvitationLanding = ({ inviteCode }: { inviteCode: string }) => {
  const { session } = useAuthState();
  const healthActions = useSystemHealthActions();
  const healthState = useSystemHealthState();
  const contractCheckAttempted = useRef(false);
  const [preview, setPreview] = useState<ProgrammeInvitationPreview>();
  const [loading, setLoading] = useState(true);
  const [joining, setJoining] = useState(false);
  const [error, setError] = useState<string>();
  const [aqGreenSchedule, setAQGreenSchedule] = useState<0 | 1>(0);
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
    void httpClient
      .get<ProgrammeInvitationPreview>(
        apiEndpoints.programmeParticipations.getInvitationPreview(inviteCode),
      )
      .then(setPreview)
      .catch((requestError) =>
        setError(
          getRequestErrorMessage(requestError, "This invitation could not be opened."),
        ),
      )
      .finally(() => setLoading(false));
  }, [inviteCode]);

  const confirm = async () => {
    if (!preview || paymentActionsUnavailable) return;
    const endpoint = getProgrammeJoinEndpoint(preview.programmeKey);
    if (!endpoint) {
      setError(unsupportedProgrammeMessage);
      return;
    }
    setJoining(true);
    setError(undefined);
    try {
      if (preview.programmeKey === "ONYX") {
        const checkout = await httpClient.post<
          ProgrammeCheckout,
          { inviteCode: string }
        >(endpoint, {
          inviteCode: preview.inviteCode,
        });
        navigateToExternalUrl(checkout.checkoutUrl);
      } else {
        await httpClient.post(endpoint, { inviteCode: preview.inviteCode });
        const checkout = await httpClient.post<
          ProgrammeCheckout,
          { schedule: 0 | 1 }
        >(
          apiEndpoints.programmeParticipations.createAQGreenJoiningCheckout,
          { schedule: aqGreenSchedule },
        );
        navigateToExternalUrl(checkout.checkoutUrl);
      }
    } catch (requestError) {
      setError(
        getRequestErrorMessage(
          requestError,
          "The programme invitation could not be accepted. No payment has been taken.",
        ),
      );
    } finally {
      setJoining(false);
    }
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
    ? getProgrammePaymentExplanation(preview.programmeKey)
    : undefined;

  return (
    <main className="min-h-dvh bg-muted/30 px-4 py-10 sm:px-6">
      <div className="mx-auto max-w-2xl">
        {loading ? (
          <Skeleton className="h-96" />
        ) : error && !preview ? (
          <StatusMessage tone="error">{error}</StatusMessage>
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

            {preview.programmeKey === "AQGREEN" && session ? (
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
              <StatusMessage tone="error">
                Payment is unavailable because this frontend cannot verify a
                compatible payment API deployment. No payment has been taken.
              </StatusMessage>
            ) : null}

            {!programmeJoinEndpoint ? (
              <StatusMessage tone="error">
                {unsupportedProgrammeMessage}
              </StatusMessage>
            ) : session ? (
              <Button
                disabled={
                  !preview.recruiterEligible || paymentActionsUnavailable
                }
                isLoading={joining}
                onClick={() => void confirm()}
              >
                Confirm and continue to payment
              </Button>
            ) : (
              <div className="grid gap-3 sm:grid-cols-2">
                <LinkButton href={signupUrl}>Create my account</LinkButton>
                <LinkButton href={loginUrl} variant="outline">Sign in to continue</LinkButton>
              </div>
            )}

            {error ? <StatusMessage tone="error">{error}</StatusMessage> : null}
          </Card>
        ) : null}
      </div>
    </main>
  );
};
