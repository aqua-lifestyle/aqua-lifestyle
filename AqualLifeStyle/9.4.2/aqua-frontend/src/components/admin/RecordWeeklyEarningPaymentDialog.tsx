"use client";

import { type FormEvent, useId, useState } from "react";
import { z } from "zod";

import { apiEndpoints, httpClient } from "@/src/shared/api";
import { getRequestErrorMessage } from "@/src/shared/api/abp-error";
import {
  Button,
  Dialog,
  StatusMessage,
  TextAreaField,
  TextField,
} from "@/src/shared/ui";
import { adminAuditJustificationSchema } from "./admin-action-validation";

const paymentRecordSchema = z.object({
  justification: adminAuditJustificationSchema,
  paymentReference: z.string().trim()
    .min(3, "Enter the external payment reference.")
    .max(128, "The payment reference cannot exceed 128 characters."),
});

type RecordWeeklyEarningPaymentDialogProps = {
  earning: {
    customerName: string;
    id: string;
    programme: number;
  };
  onRecorded: () => void | Promise<void>;
};

export const RecordWeeklyEarningPaymentDialog = ({
  earning,
  onRecorded,
}: RecordWeeklyEarningPaymentDialogProps) => {
  const [open, setOpen] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [requestError, setRequestError] = useState<string>();
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});
  const paymentReferenceId = useId();
  const justificationId = useId();

  const close = () => {
    setOpen(false);
    setRequestError(undefined);
    setFieldErrors({});
  };

  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    const parsed = paymentRecordSchema.safeParse({
      justification: data.get("justification"),
      paymentReference: data.get("paymentReference"),
    });
    if (!parsed.success) {
      setFieldErrors(Object.fromEntries(
        parsed.error.issues.map((issue) => [
          String(issue.path[0]),
          issue.message,
        ]),
      ));
      return;
    }

    setFieldErrors({});
    setRequestError(undefined);
    setSubmitting(true);
    try {
      await httpClient.post(
        apiEndpoints.weeklyEarnings.recordPayment,
        {
          id: earning.id,
          programme: earning.programme,
          ...parsed.data,
        },
      );
      await onRecorded();
      close();
    } catch (error) {
      setRequestError(
        getRequestErrorMessage(
          error,
          "The external payment could not be recorded.",
        ),
      );
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <>
      <Button onClick={() => setOpen(true)} size="sm" variant="outline">
        Record external payment
      </Button>
      <Dialog
        onClose={close}
        open={open}
        title="Record external payment"
      >
        <p className="text-sm text-muted-foreground">
          Use this only after {earning.customerName} has been paid outside the
          platform. This records the completed payment and does not send money.
        </p>
        <form className="grid gap-4" noValidate onSubmit={submit}>
          <TextField
            errorMessage={fieldErrors.paymentReference}
            id={paymentReferenceId}
            label="External payment reference"
            maxLength={128}
            name="paymentReference"
            required
          />
          <TextAreaField
            errorMessage={fieldErrors.justification}
            id={justificationId}
            label="Reason for recording this payment"
            maxLength={500}
            name="justification"
            required
            rows={3}
          />
          {requestError ? (
            <StatusMessage tone="error">{requestError}</StatusMessage>
          ) : null}
          <div className="flex justify-end gap-3">
            <Button onClick={close} variant="ghost">Cancel</Button>
            <Button isLoading={submitting} type="submit">
              Confirm payment record
            </Button>
          </div>
        </form>
      </Dialog>
    </>
  );
};
