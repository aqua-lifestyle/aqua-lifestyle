"use client";

import { useState } from "react";

import { getRequestErrorMessage } from "@/src/shared/api/abp-error";
import { Button, Dialog, StatusMessage, TextAreaField } from "@/src/shared/ui";
import { adminAuditJustificationSchema } from "./admin-action-validation";

type AdminJustificationDialogProps = {
  confirmLabel: string;
  description: string;
  onConfirm: (justification: string) => Promise<void>;
  title: string;
  triggerLabel: string;
  variant?: "primary" | "outline" | "danger";
};

export const AdminJustificationDialog = ({
  confirmLabel, description, onConfirm, title, triggerLabel, variant = "outline",
}: AdminJustificationDialogProps) => {
  const [open, setOpen] = useState(false);
  const [justification, setJustification] = useState("");
  const [validationError, setValidationError] = useState<string>();
  const [requestError, setRequestError] = useState<string>();
  const [submitting, setSubmitting] = useState(false);

  const close = () => {
    setOpen(false); setJustification(""); setValidationError(undefined); setRequestError(undefined);
  };
  const confirm = async () => {
    const parsed = adminAuditJustificationSchema.safeParse(justification);
    if (!parsed.success) { setValidationError(parsed.error.issues[0]?.message); return; }
    setValidationError(undefined); setRequestError(undefined); setSubmitting(true);
    try { await onConfirm(parsed.data); close(); }
    catch (error) { setRequestError(getRequestErrorMessage(error, "This action could not be completed.")); }
    finally { setSubmitting(false); }
  };

  return <>
    <Button onClick={() => setOpen(true)} size="sm" variant={variant}>{triggerLabel}</Button>
    <Dialog onClose={close} open={open} title={title}>
      <p className="text-sm text-muted-foreground">{description}</p>
      <TextAreaField errorMessage={validationError} label="Reason for action" maxLength={500} name="justification" onChange={(event) => setJustification(event.target.value)} required rows={3} value={justification} />
      {requestError ? <StatusMessage tone="error">{requestError}</StatusMessage> : null}
      <div className="flex justify-end gap-3"><Button onClick={close} variant="ghost">Cancel</Button><Button isLoading={submitting} onClick={confirm} variant={variant === "danger" ? "danger" : "primary"}>{confirmLabel}</Button></div>
    </Dialog>
  </>;
};
