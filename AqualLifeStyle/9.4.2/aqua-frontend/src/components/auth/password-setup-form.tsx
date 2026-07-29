"use client";

import { type FormEvent, useState } from "react";
import { z } from "zod";

import { httpClient } from "@/src/shared/api";
import { getRequestErrorMessage } from "@/src/shared/api/abp-error";
import { completePasswordReset } from "@/src/shared/api/account-email-service";
import { passwordPolicyDescription, securePasswordSchema } from "@/src/shared/auth/password-policy";
import { Button, Card, LinkButton, StatusMessage, TextField } from "@/src/shared/ui";

const passwordSchema = z.object({
  confirmPassword: z.string(),
  password: securePasswordSchema,
}).refine((value) => value.password === value.confirmPassword, {
  message: "The passwords do not match.",
  path: ["confirmPassword"],
});

type PasswordSetupFormProps = {
  areaName: string;
  resetToken: string;
  tenantId?: number;
  userId: number;
};

export const PasswordSetupForm = ({ areaName, resetToken, tenantId = 0, userId }: PasswordSetupFormProps) => {
  const [error, setError] = useState<string>();
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [isComplete, setIsComplete] = useState(false);
  const isEmailReset = Number.isSafeInteger(tenantId) && tenantId > 0;
  const hasValidLink = Boolean((isEmailReset || areaName) && resetToken && Number.isSafeInteger(userId) && userId > 0);
  const signInUrl = areaName ? `/login?area=${encodeURIComponent(areaName)}` : "/login";

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
      if (isEmailReset) {
        const result = await completePasswordReset(tenantId, userId, resetToken, parsed.data.password);
        if (!result.ok) {
          setError(result.message);
          return;
        }
      } else {
        await httpClient.post("/api/services/app/Account/CompletePasswordSetup", {
          areaName,
          newPassword: parsed.data.password,
          resetToken,
          userId,
        });
      }
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
        <h1 className="text-2xl font-bold">{isEmailReset ? "Reset your password" : "Set up your password"}</h1>
        <p className="mt-2 text-sm text-muted-foreground">Choose a private password for your Aqua Lifestyle Club account.</p>
        {!hasValidLink ? (
          <StatusMessage className="mt-5" tone="error">This password setup link is incomplete. Ask an administrator for a new link.</StatusMessage>
        ) : isComplete ? (
          <div className="mt-5 flex flex-col gap-4">
            <StatusMessage tone="success">Your password is set and your sign-in access is ready.</StatusMessage>
            <LinkButton href={signInUrl} variant="primary">Continue to sign in</LinkButton>
          </div>
        ) : (
          <form className="mt-5 flex flex-col gap-4" noValidate onSubmit={submit}>
            <TextField autoComplete="new-password" errorMessage={fieldErrors.password} label="New password" name="password" required type="password" />
            <TextField autoComplete="new-password" errorMessage={fieldErrors.confirmPassword} label="Confirm new password" name="confirmPassword" required type="password" />
            <p className="text-sm text-muted-foreground">{passwordPolicyDescription}</p>
            {error ? <StatusMessage tone="error">{error}</StatusMessage> : null}
            <Button isLoading={isSubmitting} type="submit">Set password</Button>
          </form>
        )}
      </Card>
    </main>
  );
};
