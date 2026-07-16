"use client";

import { type FormEvent, useState } from "react";
import { z } from "zod";

import { httpClient } from "@/src/shared/api";
import { getRequestErrorMessage } from "@/src/shared/api/abp-error";
import { Button, Dialog, StatusMessage, TextAreaField, TextField } from "@/src/shared/ui";
import { adminAuditJustificationSchema } from "./admin-action-validation";

const editMemberProfileSchema = z.object({
  email: z.string().trim().email("Enter a valid email address.").max(256),
  firstName: z.string().trim().min(1, "First name is required.").max(64),
  justification: adminAuditJustificationSchema,
  lastName: z.string().trim().min(1, "Last name is required.").max(64),
});

type EditableMemberProfile = {
  email: string;
  firstName: string;
  id: number;
  lastName: string;
};

type EditMemberProfileDialogProps = {
  member: EditableMemberProfile;
  onUpdated: () => void | Promise<void>;
};

export const EditMemberProfileDialog = ({ member, onUpdated }: EditMemberProfileDialogProps) => {
  const [open, setOpen] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [requestError, setRequestError] = useState<string>();
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});

  const close = () => {
    setOpen(false);
    setRequestError(undefined);
    setFieldErrors({});
  };
  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const form = event.currentTarget;
    const data = new FormData(form);
    const parsed = editMemberProfileSchema.safeParse({
      email: data.get("email"),
      firstName: data.get("firstName"),
      justification: data.get("justification"),
      lastName: data.get("lastName"),
    });
    if (!parsed.success) {
      setFieldErrors(Object.fromEntries(parsed.error.issues.map((issue) => [String(issue.path[0]), issue.message])));
      return;
    }
    setFieldErrors({});
    setRequestError(undefined);
    setSubmitting(true);
    try {
      await httpClient.post("/api/services/app/AdminMember/EditProfile", { id: member.id, ...parsed.data });
      await onUpdated();
      close();
    } catch (error) {
      setRequestError(getRequestErrorMessage(error, "The club member profile could not be updated."));
    } finally {
      setSubmitting(false);
    }
  };

  return <>
    <Button onClick={() => setOpen(true)} size="sm" variant="outline">Edit profile</Button>
    <Dialog onClose={close} open={open} size="lg" title="Edit club member profile">
      <form className="grid gap-4 sm:grid-cols-2" noValidate onSubmit={submit}>
        <TextField defaultValue={member.firstName} errorMessage={fieldErrors.firstName} label="First name" name="firstName" required />
        <TextField defaultValue={member.lastName} errorMessage={fieldErrors.lastName} label="Last name" name="lastName" required />
        <TextField className="sm:col-span-2" defaultValue={member.email} errorMessage={fieldErrors.email} label="Email address" name="email" required type="email" />
        <TextAreaField className="sm:col-span-2" errorMessage={fieldErrors.justification} label="Reason for change" maxLength={500} name="justification" required rows={3} />
        {requestError ? <StatusMessage className="sm:col-span-2" tone="error">{requestError}</StatusMessage> : null}
        <div className="flex justify-end gap-3 sm:col-span-2">
          <Button onClick={close} variant="ghost">Cancel</Button>
          <Button isLoading={submitting} type="submit">Save profile</Button>
        </div>
      </form>
    </Dialog>
  </>;
};
