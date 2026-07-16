"use client";

import { type FormEvent, useEffect, useState } from "react";
import { z } from "zod";

import { httpClient } from "@/src/shared/api";
import { getRequestErrorMessage } from "@/src/shared/api/abp-error";
import { Button, Dialog, SelectField, StatusMessage, TextAreaField } from "@/src/shared/ui";
import { adminAuditJustificationSchema } from "./admin-action-validation";

const changeMemberTierSchema = z.object({
  justification: adminAuditJustificationSchema,
  membershipId: z.coerce.number().int().positive("Select a membership plan."),
});

type ChangeMemberTierDialogProps = {
  currentMembershipId: number;
  memberId: number;
  memberName: string;
  onChanged: () => void | Promise<void>;
};
type MembershipOption = { id: number; membershipType: number; name: string };

export const ChangeMemberTierDialog = ({ currentMembershipId, memberId, memberName, onChanged }: ChangeMemberTierDialogProps) => {
  const [membershipOptions, setMembershipOptions] = useState<MembershipOption[]>([]);
  const [loadingOptions, setLoadingOptions] = useState(false);
  const [open, setOpen] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [requestError, setRequestError] = useState<string>();
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});

  useEffect(() => {
    if (!open) return;
    const task = window.setTimeout(() => {
      setLoadingOptions(true);
      void httpClient.get<MembershipOption[]>(`/api/services/app/AdminMember/GetMembershipOptions?Id=${memberId}`)
        .then(setMembershipOptions)
        .catch((error) => setRequestError(getRequestErrorMessage(error, "Membership plans could not be loaded.")))
        .finally(() => setLoadingOptions(false));
    }, 0);
    return () => window.clearTimeout(task);
  }, [memberId, open]);

  const close = () => {
    setOpen(false);
    setRequestError(undefined);
    setFieldErrors({});
  };
  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const form = event.currentTarget;
    const data = new FormData(form);
    const parsed = changeMemberTierSchema.safeParse({
      justification: data.get("justification"),
      membershipId: data.get("membershipId"),
    });
    if (!parsed.success) {
      setFieldErrors(Object.fromEntries(parsed.error.issues.map((issue) => [String(issue.path[0]), issue.message])));
      return;
    }
    setFieldErrors({});
    setRequestError(undefined);
    setSubmitting(true);
    try {
      await httpClient.post("/api/services/app/AdminMember/ChangeTier", { id: memberId, ...parsed.data });
      await onChanged();
      close();
    } catch (error) {
      setRequestError(getRequestErrorMessage(error, "The club member's membership plan could not be changed."));
    } finally {
      setSubmitting(false);
    }
  };

  return <>
    <Button onClick={() => setOpen(true)} size="sm" variant="outline">Change plan</Button>
    <Dialog onClose={close} open={open} title="Change membership plan">
      <form className="flex flex-col gap-4" noValidate onSubmit={submit}>
        <p className="text-sm text-muted-foreground">Select the new membership plan for {memberName}.</p>
        <SelectField disabled={loadingOptions} errorMessage={fieldErrors.membershipId} label="Membership plan" name="membershipId" required>
          <option value="">Select a plan</option>
          {membershipOptions.map((membership) => <option key={membership.id} value={membership.id}>{membership.name}{membership.id === currentMembershipId ? " (current plan)" : ""}</option>)}
        </SelectField>
        <TextAreaField errorMessage={fieldErrors.justification} label="Reason for change" maxLength={500} name="justification" required rows={3} />
        {requestError ? <StatusMessage tone="error">{requestError}</StatusMessage> : null}
        <div className="flex justify-end gap-3">
          <Button onClick={close} variant="ghost">Cancel</Button>
          <Button disabled={loadingOptions || Boolean(requestError)} isLoading={submitting} type="submit">Change plan</Button>
        </div>
      </form>
    </Dialog>
  </>;
};
