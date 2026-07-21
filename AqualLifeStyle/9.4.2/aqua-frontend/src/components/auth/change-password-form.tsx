"use client";

import { type FormEvent, useState } from "react";
import { useRouter } from "next/navigation";
import { z } from "zod";

import { useAuthActions, useToast } from "@/src/providers";
import { httpClient } from "@/src/shared/api";
import { getRequestErrorMessage } from "@/src/shared/api/abp-error";
import { Button, StatusMessage, TextField } from "@/src/shared/ui";

const changePasswordSchema = z
  .object({
    confirmPassword: z.string().min(1, "Confirm your new password."),
    currentPassword: z.string().min(1, "Enter your current password."),
    newPassword: z
      .string()
      .min(8, "Use at least 8 characters.")
      .regex(/[A-Z]/, "Add an uppercase letter.")
      .regex(/[a-z]/, "Add a lowercase letter.")
      .regex(/[0-9]/, "Add a number.")
      .regex(/[^A-Za-z0-9]/, "Add a special character."),
  })
  .refine((value) => value.newPassword === value.confirmPassword, {
    message: "The new passwords do not match.",
    path: ["confirmPassword"],
  })
  .refine((value) => value.newPassword !== value.currentPassword, {
    message: "Choose a password different from your current password.",
    path: ["newPassword"],
  });

type PasswordField = "confirmPassword" | "currentPassword" | "newPassword";
type PasswordFieldErrors = Partial<Record<PasswordField, string>>;

export const ChangePasswordForm = () => {
  const router = useRouter();
  const { clearSession } = useAuthActions();
  const { toast } = useToast();
  const [fieldErrors, setFieldErrors] = useState<PasswordFieldErrors>({});
  const [requestError, setRequestError] = useState<string>();
  const [isSubmitting, setIsSubmitting] = useState(false);

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    const parsed = changePasswordSchema.safeParse({
      confirmPassword: form.get("confirmPassword"),
      currentPassword: form.get("currentPassword"),
      newPassword: form.get("newPassword"),
    });

    if (!parsed.success) {
      const nextErrors: PasswordFieldErrors = {};
      for (const issue of parsed.error.issues) {
        const field = issue.path[0];
        if (typeof field === "string" && !(field in nextErrors)) {
          nextErrors[field as PasswordField] = issue.message;
        }
      }
      setFieldErrors(nextErrors);
      return;
    }

    setFieldErrors({});
    setRequestError(undefined);
    setIsSubmitting(true);
    try {
      await httpClient.post<void, { currentPassword: string; newPassword: string }>(
        "/api/services/app/MyAccount/ChangePassword",
        {
          currentPassword: parsed.data.currentPassword,
          newPassword: parsed.data.newPassword,
        },
      );
      toast({
        message: "Your password was changed. Sign in again with your new password.",
        title: "Password updated",
        type: "success",
      });
      clearSession();
      router.replace("/login");
    } catch (error) {
      setRequestError(getRequestErrorMessage(
        error,
        "Your password could not be changed. No changes were made.",
      ));
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <form className="mt-4 flex flex-col gap-4" noValidate onSubmit={handleSubmit}>
      <TextField autoComplete="current-password" errorMessage={fieldErrors.currentPassword} label="Current password" name="currentPassword" required type="password" />
      <TextField autoComplete="new-password" errorMessage={fieldErrors.newPassword} label="New password" minLength={8} name="newPassword" required type="password" />
      <TextField autoComplete="new-password" errorMessage={fieldErrors.confirmPassword} label="Confirm new password" minLength={8} name="confirmPassword" required type="password" />
      <p className="text-sm text-muted-foreground">
        Use at least 8 characters with uppercase, lowercase, number, and special characters.
        You will be asked to sign in again on every device.
      </p>
      {requestError ? <StatusMessage tone="error">{requestError}</StatusMessage> : null}
      <div><Button isLoading={isSubmitting} type="submit">Change password</Button></div>
    </form>
  );
};
