"use client";

import { type FormEvent, useState } from "react";
import { z } from "zod";

import { httpClient } from "@/src/shared/api";
import { getRequestErrorMessage } from "@/src/shared/api/abp-error";
import { Button, Card, LinkButton, StatusMessage, TextField } from "@/src/shared/ui";

const passwordSchema = z.object({
  confirmPassword: z.string(),
  password: z.string()
    .min(8, "Use at least 8 characters.")
    .regex(/[A-Z]/, "Include an uppercase letter.")
    .regex(/[a-z]/, "Include a lowercase letter.")
    .regex(/\d/, "Include a number.")
    .regex(/^[0-9a-zA-Z!@#$%^&*()]*$/, "Use letters, numbers, or !@#$%^&*()."),
}).refine((value) => value.password === value.confirmPassword, {
  message: "The passwords do not match.",
  path: ["confirmPassword"],
});

type PasswordSetupFormProps = {
  areaName: string;
  resetToken: string;
  userId: number;
};

export const PasswordSetupForm = ({ areaName, resetToken, userId }: PasswordSetupFormProps) => {
  const [error, setError] = useState<string>();
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [isComplete, setIsComplete] = useState(false);
  const hasValidLink = Boolean(areaName && resetToken && Number.isSafeInteger(userId) && userId > 0);

  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    const parsed = passwordSchema.safeParse({
      confirmPassword: data.get("confirmPassword"),
      password: data.get("password"),
    });
    if (!parsed.success) {
      setFieldErrors(Object.fromEntries(parsed.error.issues.map((issue) => [String(issue.path[0]), issue.message])));
      return;
    }

    setFieldErrors({});
    setError(undefined);
    setIsSubmitting(true);
    try {
      await httpClient.post("/api/services/app/Account/CompletePasswordSetup", {
        areaName,
        newPassword: parsed.data.password,
        resetToken,
        userId,
      });
      setIsComplete(true);
    } catch (requestError) {
      setError(getRequestErrorMessage(requestError, "Your password could not be set. Ask an administrator for a new setup link."));
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <main className="flex min-h-dvh items-center justify-center bg-muted/30 px-4 py-12">
      <Card className="w-full max-w-md">
        <h1 className="text-2xl font-bold">Set up your password</h1>
        <p className="mt-2 text-sm text-muted-foreground">Choose a private password for your Aqua Lifestyle Club account.</p>
        {!hasValidLink ? (
          <StatusMessage className="mt-5" tone="error">This password setup link is incomplete. Ask an administrator for a new link.</StatusMessage>
        ) : isComplete ? (
          <div className="mt-5 flex flex-col gap-4">
            <StatusMessage tone="success">Your password is set and your sign-in access is ready.</StatusMessage>
            <LinkButton href="/login" variant="primary">Continue to sign in</LinkButton>
          </div>
        ) : (
          <form className="mt-5 flex flex-col gap-4" noValidate onSubmit={submit}>
            <TextField autoComplete="new-password" errorMessage={fieldErrors.password} label="New password" name="password" required type="password" />
            <TextField autoComplete="new-password" errorMessage={fieldErrors.confirmPassword} label="Confirm new password" name="confirmPassword" required type="password" />
            {error ? <StatusMessage tone="error">{error}</StatusMessage> : null}
            <Button isLoading={isSubmitting} type="submit">Set password</Button>
          </form>
        )}
      </Card>
    </main>
  );
};
