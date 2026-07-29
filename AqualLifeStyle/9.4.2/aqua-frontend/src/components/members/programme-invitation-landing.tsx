"use client";

import { Network, ShieldCheck } from "lucide-react";
import { useEffect, useState } from "react";

import { useAuthState } from "@/src/providers";
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
      return "Confirming records your AQGreen place under this inviting Club Member and continues to Yoco for one full R1,200 payment. Participation activates only after Yoco confirms payment.";
    case "ONYX":
      return "Confirming continues to Yoco for the full R6,120 payment. Your Onyx participation and network place are created only after Yoco confirms payment.";
    default:
      return undefined;
  }
};

export const ProgrammeInvitationLanding = ({ inviteCode }: { inviteCode: string }) => {
  const { session } = useAuthState();
  const [preview, setPreview] = useState<ProgrammeInvitationPreview>();
  const [loading, setLoading] = useState(true);
  const [joining, setJoining] = useState(false);
  const [error, setError] = useState<string>();

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
    if (!preview) return;
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
        const checkout = await httpClient.post<ProgrammeCheckout>(
          apiEndpoints.programmeParticipations.createAQGreenJoiningCheckout,
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
  const signupUrl = preview && typeof preview.areaName === "string"
    ? `/signup?area=${encodeURIComponent(preview.areaName)}&redirect=${encodeURIComponent(redirect)}`
    : "/signup";
  const loginUrl = `/login?redirect=${encodeURIComponent(redirect)}`;
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

            {!programmeJoinEndpoint ? (
              <StatusMessage tone="error">
                {unsupportedProgrammeMessage}
              </StatusMessage>
            ) : session ? (
              <Button
                disabled={!preview.recruiterEligible}
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
