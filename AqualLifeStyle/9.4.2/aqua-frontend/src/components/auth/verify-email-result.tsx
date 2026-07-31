"use client";

import { startTransition, useEffect, useState } from "react";

import { confirmEmail } from "@/src/shared/api/account-email-service";
import { Button, Card, LinkButton, StatusMessage } from "@/src/shared/ui";

type Props = { areaName?: string; redirectPath?: string; tenantId: number; token?: string; userId: number };

export const VerifyEmailResult = ({ areaName, redirectPath, tenantId, token, userId }: Props) => {
  const [linkToken, setLinkToken] = useState(token ?? "");
  const hasValidRequest = tenantId > 0 && userId > 0 && Boolean(linkToken);
  const [state, setState] = useState<"error" | "idle" | "loading" | "success">("loading");

  useEffect(() => {
    const fragmentToken = token ?? new URLSearchParams(window.location.hash.slice(1)).get("token") ?? "";
    startTransition(() => {
      setLinkToken(fragmentToken);
      setState(tenantId > 0 && userId > 0 && Boolean(fragmentToken) ? "idle" : "error");
    });
    if (window.location.hash) {
      window.history.replaceState(null, "", window.location.pathname + window.location.search);
    }
  }, [tenantId, token, userId]);

  const verifyEmail = async () => {
    if (!hasValidRequest || state === "loading") return;
    setState("loading");
    const result = await confirmEmail(tenantId, userId, linkToken);
    setState(result.ok ? "success" : "error");
  };

  const safeRedirect = redirectPath?.startsWith("/") &&
    !redirectPath.startsWith("//") &&
    !redirectPath.includes("\\")
    ? redirectPath
    : undefined;
  const loginQuery = new URLSearchParams();
  if (areaName) loginQuery.set("area", areaName);
  if (safeRedirect) loginQuery.set("redirect", safeRedirect);
  const loginUrl = loginQuery.size ? `/login?${loginQuery.toString()}` : "/login";
  const resendQuery = new URLSearchParams();
  if (areaName) resendQuery.set("area", areaName);
  if (safeRedirect) resendQuery.set("redirect", safeRedirect);
  const resendUrl = resendQuery.size ? `/verify-email-sent?${resendQuery.toString()}` : "/verify-email-sent";

  return <main className="flex min-h-dvh items-center justify-center bg-muted/30 px-4 py-12">
    <Card className="w-full max-w-md">
      <h1 className="text-2xl font-bold">Email verification</h1>
      <div className="mt-5 flex flex-col gap-4">
        {state === "idle" ? (
          <>
            <StatusMessage tone="info">
              Confirm that you want to verify the email address for your Club Member account.
            </StatusMessage>
            <Button onClick={() => void verifyEmail()} variant="primary">Verify email</Button>
          </>
        ) : null}
        {state === "loading" ? <StatusMessage tone="info">Verifying your email address…</StatusMessage> : null}
        {state === "success" ? <StatusMessage tone="success">Your email is verified. You can now sign in.</StatusMessage> : null}
        {state === "error" ? <StatusMessage tone="error">This verification link is invalid or has expired. Request a new email and try again.</StatusMessage> : null}
        {state === "success" || state === "error" ? (
          <LinkButton href={state === "success" ? loginUrl : resendUrl} variant="primary">
            {state === "success" ? "Continue to sign in" : "Request a new email"}
          </LinkButton>
        ) : null}
      </div>
    </Card>
  </main>;
};
