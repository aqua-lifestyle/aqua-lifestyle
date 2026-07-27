"use client";

import { useState, type FormEvent } from "react";

import { apiEndpoints, httpClient } from "@/src/shared/api";
import { getRequestErrorMessage } from "@/src/shared/api/abp-error";
import type { ProgrammeCheckout } from "@/src/shared/domain/programme-participations";
import { navigateToExternalUrl } from "@/src/shared/browser/navigation";
import { Button, Dialog, StatusMessage } from "@/src/shared/ui";

type Programme = "AQGreen" | "Onyx";

type JoinProgrammeDialogProps = {
  programme: Programme;
};

export const JoinProgrammeDialog = ({
  programme,
}: JoinProgrammeDialogProps) => {
  const [open, setOpen] = useState(false);
  const [error, setError] = useState<string>();
  const [submitting, setSubmitting] = useState(false);

  const close = () => {
    if (submitting) return;
    setOpen(false);
    setError(undefined);
  };

  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setSubmitting(true);
    setError(undefined);
    try {
      if (programme === "AQGreen") {
        await httpClient.post(apiEndpoints.programmeParticipations.startEntry, {
          recruiterCustomerId: null,
        });
        const checkout = await httpClient.post<ProgrammeCheckout>(
          apiEndpoints.programmeParticipations.createAQGreenJoiningCheckout,
        );
        navigateToExternalUrl(checkout.checkoutUrl);
      } else {
        const checkout = await httpClient.post<
          ProgrammeCheckout,
          { recruiterCustomerId: null }
        >(
          apiEndpoints.programmeParticipations.createDirectOnyxCheckout,
          { recruiterCustomerId: null },
        );
        navigateToExternalUrl(checkout.checkoutUrl);
      }
    } catch (requestError) {
      setError(
        getRequestErrorMessage(
          requestError,
          `${programme} participation could not be started. No payment has been taken.`,
        ),
      );
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <>
      <Button onClick={() => setOpen(true)}>Join {programme}</Button>
      <Dialog onClose={close} open={open} title={`Join ${programme}`}>
        <form className="flex flex-col gap-5" onSubmit={submit}>
          <div className="rounded-lg bg-muted/60 p-4 text-sm text-muted-foreground">
            {programme === "AQGreen"
              ? "AQGreen is the feeder programme. One full R1,200 payment is required before participation becomes active, after which you can work toward graduating to Onyx."
              : "Joining Onyx directly requires one full payment of R6,120. AQGreen participation is not required."}
          </div>

          <div className="rounded-lg border border-border p-4">
            <p className="font-medium">Start my own network</p>
            <p className="mt-1 text-sm text-muted-foreground">
              You will be the starting point of your own network. To join under
              another Club Member, open the secure invitation link they shared.
            </p>
          </div>

          {error ? <StatusMessage tone="error">{error}</StatusMessage> : null}

          <div className="flex justify-end gap-3">
            <Button disabled={submitting} onClick={close} variant="outline">
              Cancel
            </Button>
            <Button isLoading={submitting} type="submit">
              Continue to secure payment
            </Button>
          </div>
        </form>
      </Dialog>
    </>
  );
};
