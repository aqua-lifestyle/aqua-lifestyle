"use client";

import { type FormEvent, useState } from "react";
import { z } from "zod";

import { httpClient } from "@/src/shared/api";
import { getRequestErrorMessage } from "@/src/shared/api/abp-error";
import { Button, Dialog, SelectField, StatusMessage, TextAreaField, TextField } from "@/src/shared/ui";
import { adminAuditJustificationSchema } from "./admin-action-validation";

export type ManagedUserAccount = { email: string; firstName: string; id: number; isActive: boolean; lastName: string; role: number };
type DialogProps = { onUpdated: () => void | Promise<void>; user: ManagedUserAccount };

const profileSchema = z.object({ email: z.string().trim().email("Enter a valid email address."), firstName: z.string().trim().min(1, "First name is required."), isActive: z.boolean(), justification: adminAuditJustificationSchema, lastName: z.string().trim().min(1, "Last name is required.") });

export const EditUserAccountDialog = ({ onUpdated, user }: DialogProps) => {
  const [open, setOpen] = useState(false); const [submitting, setSubmitting] = useState(false); const [error, setError] = useState<string>(); const [errors, setErrors] = useState<Record<string, string>>({});
  const close = () => { setOpen(false); setError(undefined); setErrors({}); };
  const submit = async (event: FormEvent<HTMLFormElement>) => { event.preventDefault(); const data = new FormData(event.currentTarget); const parsed = profileSchema.safeParse({ email: data.get("email"), firstName: data.get("firstName"), isActive: data.get("isActive") === "on", justification: data.get("justification"), lastName: data.get("lastName") }); if (!parsed.success) { setErrors(Object.fromEntries(parsed.error.issues.map((issue) => [String(issue.path[0]), issue.message]))); return; } setSubmitting(true); try { await httpClient.post("/api/services/app/AdminUser/Update", { id: user.id, ...parsed.data }); await onUpdated(); close(); } catch (requestError) { setError(getRequestErrorMessage(requestError, "The account details could not be updated.")); } finally { setSubmitting(false); } };
  return <><Button onClick={() => setOpen(true)} size="sm" variant="outline">Edit details</Button><Dialog onClose={close} open={open} size="lg" title="Edit user account"><form className="grid gap-4 sm:grid-cols-2" noValidate onSubmit={submit}><TextField defaultValue={user.firstName} errorMessage={errors.firstName} label="First name" name="firstName" required /><TextField defaultValue={user.lastName} errorMessage={errors.lastName} label="Last name" name="lastName" required /><TextField className="sm:col-span-2" defaultValue={user.email} errorMessage={errors.email} label="Email address" name="email" required type="email" /><TextAreaField className="sm:col-span-2" errorMessage={errors.justification} label="Reason for change" maxLength={500} name="justification" required rows={3} /><label className="flex items-center gap-2 text-sm font-medium"><input defaultChecked={user.isActive} name="isActive" type="checkbox" /> Account is active</label>{error ? <StatusMessage className="sm:col-span-2" tone="error">{error}</StatusMessage> : null}<div className="flex justify-end gap-3 sm:col-span-2"><Button onClick={close} variant="ghost">Cancel</Button><Button isLoading={submitting} type="submit">Save changes</Button></div></form></Dialog></>;
};

export const ChangeUserAccessLevelDialog = ({ onUpdated, user }: DialogProps) => {
  const [open, setOpen] = useState(false); const [submitting, setSubmitting] = useState(false); const [error, setError] = useState<string>();
  const close = () => { setOpen(false); setError(undefined); };
  const submit = async (event: FormEvent<HTMLFormElement>) => { event.preventDefault(); const data = new FormData(event.currentTarget); const role = Number(data.get("role")); const reason = adminAuditJustificationSchema.safeParse(data.get("justification")); if (!Number.isInteger(role) || role < 0 || role > 4 || !reason.success) { setError("Choose an access level and provide a clear reason for the change."); return; } setSubmitting(true); try { await httpClient.post("/api/services/app/AdminUser/AssignRole", { id: user.id, justification: reason.data, role }); await onUpdated(); close(); } catch (requestError) { setError(getRequestErrorMessage(requestError, "The access level could not be changed.")); } finally { setSubmitting(false); } };
  return <><Button onClick={() => setOpen(true)} size="sm" variant="outline">Change access</Button><Dialog onClose={close} open={open} title="Change access level"><form className="flex flex-col gap-4" onSubmit={submit}><SelectField defaultValue={user.role} label="Access level" name="role"><option value="0">Customer</option><option value="1">Club member</option><option value="2">Facilitator</option><option value="3">Area leader</option><option value="4">Area administrator</option></SelectField><TextAreaField label="Reason for change" maxLength={500} name="justification" required rows={3} />{error ? <StatusMessage tone="error">{error}</StatusMessage> : null}<div className="flex justify-end gap-3"><Button onClick={close} variant="ghost">Cancel</Button><Button isLoading={submitting} type="submit">Update access</Button></div></form></Dialog></>;
};

export const ResetUserPasswordDialog = ({ onUpdated, user }: DialogProps) => {
  const [open, setOpen] = useState(false); const [submitting, setSubmitting] = useState(false); const [error, setError] = useState<string>();
  const close = () => { setOpen(false); setError(undefined); };
  const submit = async (event: FormEvent<HTMLFormElement>) => { event.preventDefault(); const data = new FormData(event.currentTarget); const password = String(data.get("newPassword") ?? ""); const reason = adminAuditJustificationSchema.safeParse(data.get("justification")); if (password.length < 8 || !reason.success) { setError("Use a temporary password of at least 8 characters and provide a reason."); return; } setSubmitting(true); try { await httpClient.post("/api/services/app/AdminUser/ResetPassword", { id: user.id, justification: reason.data, newPassword: password }); await onUpdated(); close(); } catch (requestError) { setError(getRequestErrorMessage(requestError, "The temporary password could not be set.")); } finally { setSubmitting(false); } };
  return <><Button onClick={() => setOpen(true)} size="sm" variant="outline">Set temporary password</Button><Dialog onClose={close} open={open} title="Set temporary password"><form className="flex flex-col gap-4" onSubmit={submit}><TextField label="Temporary password" minLength={8} name="newPassword" required type="password" /><TextAreaField label="Reason for reset" maxLength={500} name="justification" required rows={3} />{error ? <StatusMessage tone="error">{error}</StatusMessage> : null}<div className="flex justify-end gap-3"><Button onClick={close} variant="ghost">Cancel</Button><Button isLoading={submitting} type="submit">Set password</Button></div></form></Dialog></>;
};
