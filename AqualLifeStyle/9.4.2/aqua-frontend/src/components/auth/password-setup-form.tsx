"use client";

import { startTransition, type FormEvent, useEffect, useState } from "react";
import { z } from "zod";

import { httpClient } from "@/src/shared/api";
import { AbpHttpError, getRequestErrorMessage } from "@/src/shared/api/abp-error";
import { completePasswordReset } from "@/src/shared/api/account-email-service";
import {
  acceptInternalAccountInvitation,
  validateInternalAccountInvitation,
  type InternalAccountInvitationPreview,
} from "@/src/shared/api/internal-account-invitation-service";
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
  invitationCode?: string;
  redirectPath?: string;
  resetToken?: string;
  tenantId?: number;
  userId: number;
};

export const PasswordSetupForm = ({ areaName, invitationCode = "", redirectPath, resetToken, tenantId = 0, userId }: PasswordSetupFormProps) => {
  const [linkToken, setLinkToken] = useState(resetToken ?? "");
  const [hasReadLinkToken, setHasReadLinkToken] = useState(Boolean(resetToken));
  const [invitationPreview, setInvitationPreview] = useState<InternalAccountInvitationPreview>();
  const [signInAreaName, setSignInAreaName] = useState(areaName);
  const [error, setError] = useState<string>();
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [isComplete, setIsComplete] = useState(false);
  const [isAlreadyAccepted, setIsAlreadyAccepted] = useState(false);
  const [isAcceptedLink, setIsAcceptedLink] = useState(false);
  const isInvitation = Boolean(invitationCode);
  const isEmailReset = !isInvitation && Number.isSafeInteger(tenantId) && tenantId > 0;
  const hasValidLink = isInvitation
    ? invitationPreview?.status === "Pending" && Boolean(linkToken)
    : Boolean((isEmailReset || areaName) && linkToken && Number.isSafeInteger(userId) && userId > 0);
  const isValidatingInvitation = isInvitation && (
    !hasReadLinkToken || (Boolean(linkToken) && !invitationPreview && !error)
  );

  useEffect(() => {
    if (resetToken) return;
    const token = window.location.hash
      ? new URLSearchParams(window.location.hash.slice(1)).get("token") ?? ""
      : "";
    startTransition(() => {
      setLinkToken(token);
      setHasReadLinkToken(true);
    });
    if (window.location.hash) {
      window.history.replaceState(null, "", window.location.pathname + window.location.search);
    }
  }, [resetToken]);
  useEffect(() => {
    if (!isInvitation || !linkToken) return;
    let active = true;
    void validateInternalAccountInvitation(invitationCode, linkToken)
      .then((preview) => {
        if (!active) return;
        setInvitationPreview(preview);
        setSignInAreaName(preview.areaName);
      })
      .catch((requestError) => {
        if (!active) return;
        setIsAcceptedLink(requestError instanceof AbpHttpError && requestError.message === "Invitation already accepted.");
        setError(getRequestErrorMessage(requestError, "This invitation could not be validated. Ask a Platform Administrator to send a new invitation."));
      });
    return () => { active = false; };
  }, [invitationCode, isInvitation, linkToken]);
  const safeRedirect = redirectPath?.startsWith("/") &&
    !redirectPath.startsWith("//") &&
    !redirectPath.includes("\\")
    ? redirectPath
    : undefined;
  const signInParameters = new URLSearchParams();
  if (signInAreaName) signInParameters.set("area", signInAreaName);
  if (safeRedirect) signInParameters.set("redirect", safeRedirect);
  const signInQuery = signInParameters.toString();
  const signInUrl = signInQuery ? `/login?${signInQuery}` : "/login";
  const recoveryParameters = new URLSearchParams();
  if (signInAreaName) recoveryParameters.set("area", signInAreaName);
  if (safeRedirect) recoveryParameters.set("redirect", safeRedirect);
  const recoveryQuery = recoveryParameters.toString();
  const forgotPasswordUrl = recoveryQuery
    ? `/forgot-password?${recoveryQuery}`
    : "/forgot-password";
  const invalidLinkMessage = isInvitation
    ? "This account invitation link is incomplete or invalid. Ask a Platform Administrator to send a new invitation."
    : isEmailReset
    ? "This password reset link is incomplete or invalid. Request a new link from the forgot-password page."
    : "This password setup link is incomplete. Ask an administrator for a new link.";
  const requestFailureMessage = isInvitation
    ? "Your account could not be set up. Ask a Platform Administrator to send a new invitation."
    : isEmailReset
    ? "Your password could not be reset. Request a new link from the forgot-password page."
    : "Your password could not be set. Ask an administrator for a new setup link.";

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
      if (isInvitation) {
        const result = await acceptInternalAccountInvitation(invitationCode, linkToken, parsed.data.password);
        setSignInAreaName(result.areaName);
        setIsAlreadyAccepted(result.wasAlreadyAccepted);
      } else if (isEmailReset) {
        const result = await completePasswordReset(tenantId, userId, linkToken, parsed.data.password);
        if (!result.ok) {
          setError(result.message);
          return;
        }
      } else {
        await httpClient.post("/api/services/app/Account/CompletePasswordSetup", {
          areaName,
          newPassword: parsed.data.password,
          resetToken: linkToken,
          userId,
        });
      }
      setIsComplete(true);
    } catch (requestError) {
      setError(getRequestErrorMessage(requestError, requestFailureMessage));
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <main className="flex min-h-dvh items-center justify-center bg-muted/30 px-4 py-12">
      <Card className="w-full max-w-md">
        <h1 className="text-2xl font-bold">{isInvitation ? "Accept your account invitation" : isEmailReset ? "Reset your password" : "Set up your password"}</h1>
        <p className="mt-2 text-sm text-muted-foreground">Choose a private password for your Aqua Lifestyle Club account.</p>
        {isValidatingInvitation ? (
          <StatusMessage className="mt-5" tone="info">Checking your invitation...</StatusMessage>
        ) : isComplete ? (
          <div className="mt-5 flex flex-col gap-4">
            <StatusMessage tone="success">{isAlreadyAccepted ? "This invitation has already been accepted. Your sign-in access is ready." : "Your password is set and your sign-in access is ready."}</StatusMessage>
            <LinkButton href={signInUrl} variant="primary">Continue to sign in</LinkButton>
          </div>
        ) : !hasValidLink ? (
          <div className="mt-5 flex flex-col gap-4">
            <StatusMessage tone="error">{error ?? invalidLinkMessage}</StatusMessage>
            {isAcceptedLink ? <LinkButton href={signInUrl}>Continue to sign in</LinkButton> : null}
            {isEmailReset ? <LinkButton href={forgotPasswordUrl}>Request a new reset link</LinkButton> : null}
          </div>
        ) : (
          <form className="mt-5 flex flex-col gap-4" noValidate onSubmit={submit}>
            {invitationPreview ? <div className="grid gap-3 rounded-xl border border-border bg-muted/30 p-4 text-sm"><div><p className="text-muted-foreground">Area</p><p className="font-semibold">{invitationPreview.areaDisplayName}</p></div><div><p className="text-muted-foreground">Access level</p><p className="font-semibold">{invitationPreview.accessLevel}</p></div><div><p className="text-muted-foreground">Username</p><p className="break-all font-semibold">{invitationPreview.username}</p></div><div><p className="text-muted-foreground">Invitation expires</p><p className="font-semibold">{new Intl.DateTimeFormat("en-ZA", { dateStyle: "long", timeStyle: "short" }).format(new Date(invitationPreview.expiresAt))}</p></div></div> : null}
            <TextField autoComplete="new-password" errorMessage={fieldErrors.password} label="New password" name="password" required type="password" />
            <TextField autoComplete="new-password" errorMessage={fieldErrors.confirmPassword} label="Confirm new password" name="confirmPassword" required type="password" />
            <p className="text-sm text-muted-foreground">{passwordPolicyDescription}</p>
            {error ? <StatusMessage tone="error">{error}</StatusMessage> : null}
            {error && isEmailReset ? <LinkButton href={forgotPasswordUrl} variant="ghost">Request a new reset link</LinkButton> : null}
            <Button isLoading={isSubmitting} type="submit">Set password</Button>
          </form>
        )}
      </Card>
    </main>
  );
};
