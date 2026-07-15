"use client";

import { type FormEvent, useEffect, useState } from "react";
import { z } from "zod";

import { useMembershipsActions, useMembershipsState } from "@/src/providers";
import { httpClient } from "@/src/shared/api";
import { getRequestErrorMessage } from "@/src/shared/api/abp-error";
import { Button, Dialog, SelectField, StatusMessage, TextAreaField } from "@/src/shared/ui";
import { adminAuditJustificationSchema } from "./admin-action-validation";

const changeMemberTierSchema = z.object({
  justification: adminAuditJustificationSchema,
  membershipId: z.coerce.number().int().positive("Select a membership tier."),
});

type ChangeMemberTierDialogProps = {
  currentMembershipId: number;
  memberId: number;
  memberName: string;
  onChanged: () => void | Promise<void>;
};

export const ChangeMemberTierDialog = ({ currentMembershipId, memberId, memberName, onChanged }: ChangeMemberTierDialogProps) => {
  const { getActiveTiers } = useMembershipsActions();
  const { errorMessage, isError, isPending, memberships } = useMembershipsState();
  const [open, setOpen] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [requestError, setRequestError] = useState<string>();
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});

  useEffect(() => {
    if (open) void getActiveTiers();
  }, [getActiveTiers, open]);

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
      setRequestError(getRequestErrorMessage(error, "The member tier could not be changed."));
    } finally {
      setSubmitting(false);
    }
  };

  return <>
    <Button onClick={() => setOpen(true)} size="sm" variant="outline">Change tier</Button>
    <Dialog onClose={close} open={open} title="Change member tier">
      <form className="flex flex-col gap-4" noValidate onSubmit={submit}>
        <p className="text-sm text-muted-foreground">Select the new membership tier for {memberName}.</p>
        <SelectField disabled={isPending} errorMessage={fieldErrors.membershipId} label="Membership tier" name="membershipId" required>
          <option value="">Select a tier</option>
          {memberships.map((membership) => <option key={membership.id} value={membership.id}>{membership.name}{membership.id === currentMembershipId ? " (current)" : ""}</option>)}
        </SelectField>
        <TextAreaField errorMessage={fieldErrors.justification} label="Audit justification" maxLength={500} name="justification" required rows={3} />
        {isError ? <StatusMessage tone="error">{errorMessage ?? "Membership tiers could not be loaded."}</StatusMessage> : null}
        {requestError ? <StatusMessage tone="error">{requestError}</StatusMessage> : null}
        <div className="flex justify-end gap-3">
          <Button onClick={close} variant="ghost">Cancel</Button>
          <Button disabled={isPending || isError} isLoading={submitting} type="submit">Change tier</Button>
        </div>
      </form>
    </Dialog>
  </>;
};
