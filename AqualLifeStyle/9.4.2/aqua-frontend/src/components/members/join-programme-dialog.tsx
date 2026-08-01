"use client";

import { useState, type FormEvent } from "react";

import { apiEndpoints, httpClient } from "@/src/shared/api";
import { getRequestErrorMessage } from "@/src/shared/api/abp-error";
import type { ProgrammeCheckout } from "@/src/shared/domain/programme-participations";
import { navigateToExternalUrl } from "@/src/shared/browser/navigation";
import { Button, Dialog, StatusMessage } from "@/src/shared/ui";
import { AQGreenPaymentSchedule } from "./aqgreen-payment-schedule";

type Programme = "AQGreen" | "Onyx";

type JoinProgrammeDialogProps = {
  disabled?: boolean;
  programme: Programme;
};

export const JoinProgrammeDialog = ({
  disabled = false,
  programme,
}: JoinProgrammeDialogProps) => {
  const [open, setOpen] = useState(false);
  const [error, setError] = useState<string>();
  const [submitting, setSubmitting] = useState(false);
  const [aqGreenSchedule, setAQGreenSchedule] = useState<0 | 1>(0);

  const close = () => {
    if (submitting) return;
    setOpen(false);
    setError(undefined);
  };

  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    if (disabled) return;
    setSubmitting(true);
    setError(undefined);
    try {
      if (programme === "AQGreen") {
        await httpClient.post(apiEndpoints.programmeParticipations.startEntry, {
          recruiterCustomerId: null,
        });
        const checkout = await httpClient.post<
          ProgrammeCheckout,
          { schedule: 0 | 1 }
        >(
          apiEndpoints.programmeParticipations.createAQGreenJoiningCheckout,
          { schedule: aqGreenSchedule },
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
      <Button disabled={disabled} onClick={() => setOpen(true)}>
        Join {programme}
      </Button>
      <Dialog onClose={close} open={open} title={`Join ${programme}`}>
        <form className="flex flex-col gap-5" onSubmit={submit}>
          <div className="rounded-lg bg-muted/60 p-4 text-sm text-muted-foreground">
            {programme === "AQGreen"
              ? "AQGreen joining costs R1,200. Choose one full payment or two R600 instalments. Participation activates only after the full joining fee is verified."
              : "Joining Onyx directly requires one full payment of R6,120. AQGreen participation is not required."}
          </div>

          {programme === "AQGreen" ? (
            <AQGreenPaymentSchedule
              disabled={submitting}
              onChange={setAQGreenSchedule}
              value={aqGreenSchedule}
            />
          ) : null}

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
            <Button disabled={disabled} isLoading={submitting} type="submit">
              Continue to secure payment
            </Button>
          </div>
        </form>
      </Dialog>
    </>
  );
};
