"use client";

import { Copy, Plus, ShieldCheck } from "lucide-react";
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
  password: z.string().max(128).refine(
    (value) => value.length === 0 || value.length >= 8,
    "Use at least 8 characters.",
  ),
  tenantId: z.coerce.number().int().positive("Select a valid area."),
});

type Fields = "email" | "firstName" | "justification" | "lastName" | "membershipId" | "password" | "tenantId";
type FieldErrors = Partial<Record<Fields, string>>;

type CustomerDialogProps = {
  onCreated?: () => void | Promise<void>;
};

type CustomerOnboardingInput = z.infer<typeof schema>;
type RemovedCustomer = { customerId: number; email: string; name: string; removalTime: string | null };
type CustomerOnboardingResult = {
  customer: { id: number; name: string } | null;
  passwordSetupUrl: string | null;
  removedCustomer: RemovedCustomer | null;
  requiresRestoreConfirmation: boolean;
};

export const CustomerDialog = ({ onCreated }: CustomerDialogProps) => {
  const { session } = useAuthState();
  const { toast } = useToast();
  const [open, setOpen] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({});
  const [pendingRestore, setPendingRestore] = useState<{
    customer: RemovedCustomer;
    input: CustomerOnboardingInput;
  } | null>(null);
  const [passwordSetupUrl, setPasswordSetupUrl] = useState<string | null>(null);
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
    setPendingRestore(null);
    setPasswordSetupUrl(null);
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
      password: data.get("password"),
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
      const onboardingResult = await httpClient.post<CustomerOnboardingResult, typeof parsed.data>(
        "/api/services/app/AdminCustomer/Create",
        parsed.data,
      );
      if (onboardingResult.requiresRestoreConfirmation && onboardingResult.removedCustomer) {
        setPendingRestore({ customer: onboardingResult.removedCustomer, input: parsed.data });
        return;
      }
      await onCreated?.();
      toast({
        message: "Customer account created successfully.",
        title: "Customer created",
        type: "success",
      });
      form.reset();
      close();
    } catch (requestError) {
      setError(getRequestErrorMessage(requestError, "The customer could not be created."));
    } finally {
      setIsSubmitting(false);
    }
  };

  const restoreCustomer = async () => {
    if (!pendingRestore) return;
    setError(null);
    setIsSubmitting(true);
    try {
      const details = {
        email: pendingRestore.input.email,
        firstName: pendingRestore.input.firstName,
        isActive: pendingRestore.input.isActive,
        justification: pendingRestore.input.justification,
        lastName: pendingRestore.input.lastName,
        membershipId: pendingRestore.input.membershipId,
      };
      const restorationResult = await httpClient.post<CustomerOnboardingResult, typeof details & { customerId: number }>(
        "/api/services/app/AdminCustomer/Restore",
        { ...details, customerId: pendingRestore.customer.customerId },
      );
      if (!restorationResult.passwordSetupUrl) {
        throw new Error("The customer was restored, but a password setup link could not be generated.");
      }
      setPasswordSetupUrl(restorationResult.passwordSetupUrl);
      await onCreated?.();
      toast({
        message: "The original customer account and history were restored. A password reset is required before sign-in can continue.",
        title: "Customer access restored",
        type: "success",
      });
    } catch (requestError) {
      setError(getRequestErrorMessage(requestError, "The customer could not be restored."));
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
        {passwordSetupUrl ? (
          <div className="flex flex-col gap-4">
            <StatusMessage tone="success">
              The existing customer and their history are restored. They must set a new password before signing in.
            </StatusMessage>
            <div className="rounded-xl border border-border bg-muted/40 p-4">
              <div className="flex items-start gap-3">
                <ShieldCheck className="mt-0.5 size-5 text-success" />
                <div>
                  <h3 className="font-semibold">Customer password setup</h3>
                  <p className="mt-1 text-sm text-muted-foreground">
                    Send this one-time link to the customer securely. The administrator never sees their new password.
                  </p>
                </div>
              </div>
              <div className="mt-4 flex gap-2">
                <input aria-label="Password setup link" className="min-w-0 flex-1 rounded-lg border border-border bg-card px-3 py-2 text-sm" readOnly value={passwordSetupUrl} />
                <Button onClick={() => void navigator.clipboard.writeText(passwordSetupUrl)} variant="outline">
                  <Copy className="size-4" /> Copy link
                </Button>
              </div>
            </div>
            <div className="flex justify-end"><Button onClick={close}>Done</Button></div>
          </div>
        ) : pendingRestore ? (
          <div className="flex flex-col gap-4">
            <StatusMessage tone="warning">
              An existing removed customer was found. No account has been changed yet.
            </StatusMessage>
            <div className="rounded-xl border border-border bg-muted/40 p-4">
              <h3 className="font-semibold">{pendingRestore.customer.name}</h3>
              <p className="text-sm text-muted-foreground">{pendingRestore.customer.email}</p>
              <ul className="mt-4 list-disc space-y-1 pl-5 text-sm text-muted-foreground">
                <li>The original customer and user IDs will be preserved.</li>
                <li>Orders, enquiries, referrals, and audit history will remain unchanged.</li>
                <li>Current details and membership will be updated from this form.</li>
                <li>The existing password stays private, and sign-in requires the one-time password setup link.</li>
              </ul>
            </div>
            {error ? <StatusMessage tone="error">{error}</StatusMessage> : null}
            <div className="flex justify-end gap-3">
              <Button onClick={() => { setPendingRestore(null); setError(null); }} variant="ghost">Back</Button>
              <Button isLoading={isSubmitting} onClick={() => void restoreCustomer()}>Restore customer</Button>
            </div>
          </div>
        ) : <form className="grid gap-4 sm:grid-cols-2" noValidate onSubmit={submit}>
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
          <TextField autoComplete="new-password" className="sm:col-span-2" errorMessage={fieldErrors.password} label="Temporary password for a new customer" minLength={8} name="password" type="password" />
          <p className="-mt-2 text-xs text-muted-foreground sm:col-span-2">This is used only for a brand-new account. Restored customers receive a one-time password setup link instead.</p>
          <TextAreaField className="sm:col-span-2" errorMessage={fieldErrors.justification} label="Reason for creating this account" maxLength={500} name="justification" required rows={3} />
          <label className="flex items-center gap-2 text-sm font-medium">
            <input defaultChecked name="isActive" type="checkbox" /> Active account
          </label>
          {error ? <StatusMessage className="sm:col-span-2" tone="error">{error}</StatusMessage> : null}
          <div className="flex justify-end gap-3 sm:col-span-2">
            <Button onClick={close} variant="ghost">Cancel</Button>
            <Button isLoading={isSubmitting} type="submit">Create customer</Button>
          </div>
        </form>}
      </Dialog>
    </>
  );
};
