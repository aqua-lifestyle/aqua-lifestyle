"use client";

import { Plus } from "lucide-react";
import { type FormEvent, useState } from "react";
import { z } from "zod";

import { useAuthState, useToast } from "@/src/providers";
import { httpClient } from "@/src/shared/api";
import { getRequestErrorMessage } from "@/src/shared/api/abp-error";
import { Button, Dialog, SelectField, StatusMessage, TextAreaField, TextField } from "@/src/shared/ui";

const schema = z.object({
  email: z.string().trim().email("Enter a valid email address.").max(256),
  firstName: z.string().trim().min(1, "First name is required.").max(64),
  isActive: z.boolean(),
  justification: z.string().trim().min(3, "Explain why this user is being created.").max(500),
  lastName: z.string().trim().min(1, "Last name is required.").max(64),
  password: z.string().min(8, "Use at least 8 characters.").max(128),
  role: z.coerce.number().int().min(0).max(4),
  tenantId: z.coerce.number().int().positive("Enter a valid tenant ID."),
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
      email: data.get("email"), firstName: data.get("firstName"), isActive: data.get("isActive") === "on",
      justification: data.get("justification"), lastName: data.get("lastName"), password: data.get("password"),
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
      toast({ message: "User account created successfully.", title: "Success", type: "success" });
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
        <TextField defaultValue={tenantId ?? ""} disabled={Boolean(tenantId)} errorMessage={fieldErrors.tenantId} label="Tenant ID" min={1} name="tenantId" required type="number" />
        {tenantId ? <input name="tenantId" type="hidden" value={tenantId} /> : null}
        <SelectField errorMessage={fieldErrors.role} label="Role" name="role">
          <option value="0">Guest</option><option value="1">Member</option><option value="2">Facilitator</option>
          <option value="3">Area leader</option><option value="4">System admin</option>
        </SelectField>
        <TextField errorMessage={fieldErrors.firstName} label="First name" name="firstName" required />
        <TextField errorMessage={fieldErrors.lastName} label="Last name" name="lastName" required />
        <TextField className="sm:col-span-2" errorMessage={fieldErrors.email} label="Email address" name="email" required type="email" />
        <TextField className="sm:col-span-2" errorMessage={fieldErrors.password} label="Temporary password" minLength={8} name="password" required type="password" />
        <TextAreaField className="sm:col-span-2" errorMessage={fieldErrors.justification} label="Audit justification" maxLength={500} name="justification" required rows={3} />
        <label className="flex items-center gap-2 text-sm font-medium"><input defaultChecked name="isActive" type="checkbox" /> Active account</label>
        {error ? <StatusMessage className="sm:col-span-2" tone="error">{error}</StatusMessage> : null}
        <div className="flex justify-end gap-3 sm:col-span-2"><Button onClick={close} variant="ghost">Cancel</Button><Button isLoading={isSubmitting} type="submit">Create user</Button></div>
      </form>
    </Dialog>
  </>;
};
