"use client";

import { Plus } from "lucide-react";
import { type FormEvent, useEffect, useState } from "react";
import { z } from "zod";

import {
  useAuthState,
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
import { AdminAreaSelectionField } from "./AdminAreaSelectionField";

const CREATE_PERMISSION = "Aqua.Admin.Customers.Create";
const schema = z.object({
  email: z.string().trim().email("Enter a valid email address.").max(256),
  firstName: z.string().trim().min(1, "First name is required.").max(64),
  isActive: z.boolean(),
  justification: z.string().trim().min(3, "Explain why this account is being created.").max(500),
  lastName: z.string().trim().min(1, "Last name is required.").max(64),
  membershipId: z.union([z.literal(""), z.coerce.number().int().positive()])
    .transform((value) => value === "" ? null : value),
  tenantId: z.coerce.number().int().positive("Select a valid area."),
});

type Fields = "email" | "firstName" | "justification" | "lastName" | "membershipId" | "tenantId";
type FieldErrors = Partial<Record<Fields, string>>;

type CustomerDialogProps = {
  onCreated?: () => void | Promise<void>;
};

export const CustomerDialog = ({ onCreated }: CustomerDialogProps) => {
  const { session } = useAuthState();
  const { toast } = useToast();
  const [open, setOpen] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({});
  const [membershipOptions, setMembershipOptions] = useState<{ id: number; name: string }[]>([]);
  const [selectedTenantId, setSelectedTenantId] = useState(String(session?.user?.tenantId ?? ""));
  const canCreate = session?.user?.permissions?.includes(CREATE_PERMISSION) ?? false;
  const tenantId = session?.user?.tenantId;

  useEffect(() => {
    const parsedTenantId = Number(selectedTenantId);
    if (!open || !Number.isInteger(parsedTenantId) || parsedTenantId <= 0) return;
    void httpClient.get<{ id: number; name: string }[]>(`/api/services/app/AdminCustomer/GetMembershipOptions?TenantId=${parsedTenantId}`)
      .then(setMembershipOptions)
      .catch((requestError) => setError(getRequestErrorMessage(requestError, "Membership plans could not be loaded for this area.")));
  }, [open, selectedTenantId]);

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
          <AdminAreaSelectionField
            errorMessage={fieldErrors.tenantId}
            fixedAreaId={tenantId ?? undefined}
            value={selectedTenantId}
            onChange={(areaId) => { setSelectedTenantId(areaId); if (!areaId) setMembershipOptions([]); }}
          />
          <SelectField label="Membership plan" name="membershipId" errorMessage={fieldErrors.membershipId}>
            <option value="">Not yet enrolled</option>
            {membershipOptions.map((membership) => (
              <option key={membership.id} value={membership.id}>{membership.name}</option>
            ))}
          </SelectField>
          <TextField autoComplete="given-name" errorMessage={fieldErrors.firstName} label="First name" name="firstName" required />
          <TextField autoComplete="family-name" errorMessage={fieldErrors.lastName} label="Last name" name="lastName" required />
          <TextField autoComplete="email" className="sm:col-span-2" errorMessage={fieldErrors.email} label="Email address" name="email" required type="email" />
          <TextAreaField className="sm:col-span-2" errorMessage={fieldErrors.justification} label="Reason for creating this account" maxLength={500} name="justification" required rows={3} />
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
