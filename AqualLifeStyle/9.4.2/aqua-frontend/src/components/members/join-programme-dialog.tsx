"use client";

import { useState, type FormEvent } from "react";

import { apiEndpoints, httpClient } from "@/src/shared/api";
import { getRequestErrorPresentation, type RequestErrorPresentation } from "@/src/shared/api/abp-error";
import type { ProgrammeCheckout } from "@/src/shared/domain/programme-participations";
import { navigateToExternalUrl } from "@/src/shared/browser/navigation";
import { Button, Dialog, StatusMessage } from "@/src/shared/ui";

type JoinProgrammeDialogProps = {
  disabled?: boolean;
  // AQGreen admission is invitation-based or separately Host-authorised.
  programme: "Onyx";
};

export const JoinProgrammeDialog = ({ disabled = false, programme }: JoinProgrammeDialogProps) => {
  const [open, setOpen] = useState(false);
  const [error, setError] = useState<RequestErrorPresentation>();
  const [submitting, setSubmitting] = useState(false);
  const close = () => {
    if (submitting) return;
    setOpen(false);
    // Retain an uncertain outcome until the member checks the existing status.
  };

  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (disabled || submitting || error?.reviewPaymentStatus) return;
    setSubmitting(true);
    setError(undefined);
    try {
      const checkout = await httpClient.post<ProgrammeCheckout, { recruiterCustomerId: null }>(
        apiEndpoints.programmeParticipations.createDirectOnyxCheckout,
        { recruiterCustomerId: null },
      );
      navigateToExternalUrl(checkout.checkoutUrl);
    } catch (requestError) {
      setError(getRequestErrorPresentation(requestError, "Your Onyx checkout status could not be confirmed.", "checkout"));
    } finally { setSubmitting(false); }
  };

  return (
    <>
      <Button disabled={disabled} onClick={() => setOpen(true)}>Join {programme}</Button>
      <Dialog onClose={close} open={open} title={`Join ${programme}`}>
        <form className="flex flex-col gap-5" onSubmit={submit}>
          <div className="rounded-lg bg-muted/60 p-4 text-sm text-muted-foreground">
            Joining Onyx directly requires one full payment of R6,120. Provider confirmation is followed by Area Administrator review before activation.
          </div>
          <div className="rounded-lg border border-border p-4">
            <p className="font-medium">Start my own network</p>
            <p className="mt-1 text-sm text-muted-foreground">To join under another Club Member, open the secure invitation link they shared.</p>
          </div>
          {error ? <StatusMessage title={error.title} tone="error">{error.message}</StatusMessage> : null}
          {error?.reviewPaymentStatus ? <Button onClick={() => window.location.reload()} variant="outline">Check my payment status</Button> : null}
          <div className="flex justify-end gap-3">
            <Button disabled={submitting} onClick={close} variant="outline">Cancel</Button>
            <Button disabled={disabled || error?.reviewPaymentStatus} isLoading={submitting} type="submit">Continue to secure payment</Button>
          </div>
        </form>
      </Dialog>
    </>
  );
};
