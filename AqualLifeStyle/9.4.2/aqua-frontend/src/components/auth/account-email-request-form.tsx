"use client";

import { type FormEvent, useState } from "react";
import { z } from "zod";

import { requestPasswordReset, resendEmailVerification } from "@/src/shared/api/account-email-service";
import { Button, Card, LinkButton, StatusMessage, TextField } from "@/src/shared/ui";

type AccountEmailRequestFormProps = {
  areaName: string;
  initialEmail?: string;
  purpose: "password-reset" | "verification";
  redirectPath?: string;
};

export const AccountEmailRequestForm = ({ areaName, initialEmail = "", purpose, redirectPath }: AccountEmailRequestFormProps) => {
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [message, setMessage] = useState<string>();
  const [error, setError] = useState<string>();

  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setMessage(undefined);
    const email = String(new FormData(event.currentTarget).get("email") ?? "").trim();
    if (!z.string().email().safeParse(email).success) {
      setError("Enter a valid email address.");
      return;
    }
    setError(undefined);
    setIsSubmitting(true);
    const result = purpose === "verification"
      ? await resendEmailVerification(areaName, email, redirectPath)
      : await requestPasswordReset(areaName, email, redirectPath);
    setIsSubmitting(false);
    if (!result.ok) {
      setError(result.message);
      return;
    }
    setMessage(purpose === "verification"
      ? "If the account is eligible, a new verification email is on its way."
      : "If the account is eligible, password reset instructions are on their way.");
  };

  return (
    <main className="flex min-h-dvh items-center justify-center bg-muted/30 px-4 py-12">
      <Card className="w-full max-w-md">
        <h1 className="text-2xl font-bold">{purpose === "verification" ? "Check your email" : "Reset your password"}</h1>
        <p className="mt-2 text-sm text-muted-foreground">
          {purpose === "verification"
            ? "Use the secure link in your email before signing in. You can request another message below."
            : "Enter the email address for your Club Member account."}
        </p>
        <form className="mt-5 flex flex-col gap-4" onSubmit={submit}>
          <TextField defaultValue={initialEmail} label="Email address" name="email" required type="email" />
          {error ? <StatusMessage tone="error">{error}</StatusMessage> : null}
          {message ? <StatusMessage tone="success">{message}</StatusMessage> : null}
          <Button isLoading={isSubmitting} type="submit">
            {purpose === "verification" ? "Send verification email" : "Send reset instructions"}
          </Button>
          <LinkButton href="/login" variant="ghost">Return to sign in</LinkButton>
        </form>
      </Card>
    </main>
  );
};
