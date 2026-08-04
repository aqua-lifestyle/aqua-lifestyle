"use client";

import { Plus } from "lucide-react";
import { type FormEvent, useState } from "react";
import { z } from "zod";

import { useAuthState, useToast } from "@/src/providers";
import { httpClient } from "@/src/shared/api";
import { getRequestErrorMessage } from "@/src/shared/api/abp-error";
import { Button, Dialog, SelectField, StatusMessage, TextAreaField, TextField } from "@/src/shared/ui";
import { AdminAreaSelectionField } from "./AdminAreaSelectionField";

const schema = z.object({
  email: z.string().trim().email("Enter a valid email address.").max(256),
  firstName: z.string().trim().min(1, "First name is required.").max(64),
  justification: z.string().trim().min(3, "Explain why this user is being created.").max(500),
  lastName: z.string().trim().min(1, "Last name is required.").max(64),
  role: z.coerce.number().int().min(0).max(4),
  tenantId: z.coerce.number().int().positive("Select a valid area."),
});

type UserDialogProps = { onCreated?: () => void | Promise<void> };

export const UserDialog = ({ onCreated }: UserDialogProps) => {
  const { session } = useAuthState();
  const { toast } = useToast();
  const [open, setOpen] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});
  const tenantId = session?.user?.tenantId;
  const canCreate = session?.user?.permissions?.includes("Aqua.Admin.Users.Create") ?? false;

  const close = () => { setOpen(false); setError(null); setFieldErrors({}); };
  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const form = event.currentTarget;
    const data = new FormData(form);
    const parsed = schema.safeParse({
      email: data.get("email"), firstName: data.get("firstName"),
      justification: data.get("justification"), lastName: data.get("lastName"),
      role: data.get("role"), tenantId: data.get("tenantId"),
    });
    if (!parsed.success) {
      setFieldErrors(Object.fromEntries(parsed.error.issues.map((issue) => [String(issue.path[0]), issue.message])));
      return;
    }
    setFieldErrors({}); setError(null); setIsSubmitting(true);
    try {
      await httpClient.post("/api/services/app/AdminUser/Create", parsed.data);
      await onCreated?.();
      toast({ message: "The account was created and an invitation email was queued.", title: "Invitation sent", type: "success" });
      form.reset(); close();
    } catch (requestError) {
      setError(getRequestErrorMessage(requestError, "The user could not be created."));
    } finally { setIsSubmitting(false); }
  };

  if (!canCreate) return null;
  return <>
    <Button onClick={() => setOpen(true)}><Plus className="size-4" /> Add user</Button>
    <Dialog onClose={close} open={open} size="lg" title="Add user">
      <form className="grid gap-4 sm:grid-cols-2" noValidate onSubmit={submit}>
        <AdminAreaSelectionField errorMessage={fieldErrors.tenantId} fixedAreaId={tenantId ?? undefined} />
        <SelectField errorMessage={fieldErrors.role} label="Access level" name="role">
          <option value="0">Customer</option><option value="1">Club member</option><option value="2">Facilitator</option>
          <option value="3">Area leader</option><option value="4">Area administrator</option>
        </SelectField>
        <TextField errorMessage={fieldErrors.firstName} label="First name" name="firstName" required />
        <TextField errorMessage={fieldErrors.lastName} label="Last name" name="lastName" required />
        <TextField className="sm:col-span-2" errorMessage={fieldErrors.email} label="Email address" name="email" required type="email" />
        <p className="sm:col-span-2 text-sm text-muted-foreground">We will email this person a secure invitation. They will confirm their details and choose their own password before the account becomes active.</p>
        <TextAreaField className="sm:col-span-2" errorMessage={fieldErrors.justification} label="Reason for creating this account" maxLength={500} name="justification" required rows={3} />
        {error ? <StatusMessage className="sm:col-span-2" tone="error">{error}</StatusMessage> : null}
        <div className="flex justify-end gap-3 sm:col-span-2"><Button onClick={close} variant="ghost">Cancel</Button><Button isLoading={isSubmitting} type="submit">Create and invite</Button></div>
      </form>
    </Dialog>
  </>;
};
