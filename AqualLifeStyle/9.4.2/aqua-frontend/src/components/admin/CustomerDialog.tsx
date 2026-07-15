"use client";

import { Plus } from "lucide-react";
import { type FormEvent, useEffect, useState } from "react";
import { z } from "zod";

import {
  useAuthState,
  useMembershipsActions,
  useMembershipsState,
  useToast,
} from "@/src/providers";
import { httpClient } from "@/src/shared/api";
import { getRequestErrorMessage } from "@/src/shared/api/abp-error";
import {
  Button,
  Dialog,
  SelectField,
  StatusMessage,
  TextAreaField,
  TextField,
} from "@/src/shared/ui";

const CREATE_PERMISSION = "Aqua.Admin.Customers.Create";
const schema = z.object({
  email: z.string().trim().email("Enter a valid email address.").max(256),
  firstName: z.string().trim().min(1, "First name is required.").max(64),
  isActive: z.boolean(),
  justification: z.string().trim().min(3, "Explain why this account is being created.").max(500),
  lastName: z.string().trim().min(1, "Last name is required.").max(64),
  membershipId: z.union([z.literal(""), z.coerce.number().int().positive()])
    .transform((value) => value === "" ? null : value),
  tenantId: z.coerce.number().int().positive("Enter a valid tenant ID."),
});

type Fields = "email" | "firstName" | "justification" | "lastName" | "membershipId" | "tenantId";
type FieldErrors = Partial<Record<Fields, string>>;

type CustomerDialogProps = {
  onCreated?: () => void | Promise<void>;
};

export const CustomerDialog = ({ onCreated }: CustomerDialogProps) => {
  const { session } = useAuthState();
  const { getMemberships } = useMembershipsActions();
  const { memberships } = useMembershipsState();
  const { toast } = useToast();
  const [open, setOpen] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({});
  const canCreate = session?.user?.permissions?.includes(CREATE_PERMISSION) ?? false;
  const tenantId = session?.user?.tenantId;

  useEffect(() => {
    if (open) void getMemberships();
  }, [getMemberships, open]);

  const close = () => {
    setOpen(false);
    setError(null);
    setFieldErrors({});
    setIsSubmitting(false);
  };

  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const form = event.currentTarget;
    const data = new FormData(form);
    const parsed = schema.safeParse({
      email: data.get("email"),
      firstName: data.get("firstName"),
      isActive: data.get("isActive") === "on",
      justification: data.get("justification"),
      lastName: data.get("lastName"),
      membershipId: data.get("membershipId"),
      tenantId: data.get("tenantId"),
    });
    if (!parsed.success) {
      const errors: FieldErrors = {};
      for (const issue of parsed.error.issues) {
        const field = issue.path[0] as Fields;
        if (!errors[field]) errors[field] = issue.message;
      }
      setFieldErrors(errors);
      return;
    }

    setFieldErrors({});
    setError(null);
    setIsSubmitting(true);
    try {
      await httpClient.post("/api/services/app/AdminCustomer/Create", parsed.data);
      await onCreated?.();
      toast({ message: "Customer account created successfully.", title: "Success", type: "success" });
      form.reset();
      close();
    } catch (requestError) {
      setError(getRequestErrorMessage(requestError, "The customer could not be created."));
    } finally {
      setIsSubmitting(false);
    }
  };

  if (!canCreate) return null;

  return (
    <>
      <Button onClick={() => setOpen(true)}>
        <Plus className="size-4" /> Add customer
      </Button>
      <Dialog onClose={close} open={open} size="lg" title="Add customer">
        <form className="grid gap-4 sm:grid-cols-2" noValidate onSubmit={submit}>
          <TextField
            defaultValue={tenantId ?? ""}
            disabled={Boolean(tenantId)}
            errorMessage={fieldErrors.tenantId}
            label="Tenant ID"
            min={1}
            name="tenantId"
            required
            type="number"
          />
          {tenantId ? <input name="tenantId" type="hidden" value={tenantId} /> : null}
          <SelectField label="Membership" name="membershipId" errorMessage={fieldErrors.membershipId}>
            <option value="">No membership assigned</option>
            {memberships.map((membership) => (
              <option key={membership.id} value={membership.id}>{membership.name}</option>
            ))}
          </SelectField>
          <TextField autoComplete="given-name" errorMessage={fieldErrors.firstName} label="First name" name="firstName" required />
          <TextField autoComplete="family-name" errorMessage={fieldErrors.lastName} label="Last name" name="lastName" required />
          <TextField autoComplete="email" className="sm:col-span-2" errorMessage={fieldErrors.email} label="Email address" name="email" required type="email" />
          <TextAreaField className="sm:col-span-2" errorMessage={fieldErrors.justification} label="Audit justification" maxLength={500} name="justification" required rows={3} />
          <label className="flex items-center gap-2 text-sm font-medium">
            <input defaultChecked name="isActive" type="checkbox" /> Active account
          </label>
          {error ? <StatusMessage className="sm:col-span-2" tone="error">{error}</StatusMessage> : null}
          <div className="flex justify-end gap-3 sm:col-span-2">
            <Button onClick={close} variant="ghost">Cancel</Button>
            <Button isLoading={isSubmitting} type="submit">Create customer</Button>
          </div>
        </form>
      </Dialog>
    </>
  );
};
