"use client";

import { useEffect, useState } from "react";

import {
  isAQGreenV2DemoCheckout,
  type SystemHealth,
} from "@/src/providers/SystemHealth/contract";
import { apiEndpoints, httpClient } from "@/src/shared/api";
import { getRequestErrorMessage } from "@/src/shared/api/abp-error";
import { Breadcrumb, Button, Card, LinkButton, Skeleton, StatusMessage } from "@/src/shared/ui";

type TestCheckout = {
  checkoutId: string;
  amount: number;
  currency: string;
  confirmed: boolean;
  participationStatus: string;
};

type ConfirmationResult = {
  participationStatus: string;
};

const formatAmount = (amount: number, currency: string) =>
  new Intl.NumberFormat("en-ZA", { style: "currency", currency }).format(amount);

export const AQGreenV2TestCheckout = () => {
  const [checkout, setCheckout] = useState<TestCheckout>();
  const [loading, setLoading] = useState(true);
  const [confirming, setConfirming] = useState(false);
  const [error, setError] = useState<string>();

  useEffect(() => {
    void (async () => {
      try {
        const health = await httpClient.get<SystemHealth>(apiEndpoints.health.get);
        if (!isAQGreenV2DemoCheckout(health)) {
          setError("Simulated checkout is unavailable outside the isolated test environment.");
          return;
        }
        setCheckout(await httpClient.get<TestCheckout>(apiEndpoints.aqgreenV2DemoCheckout.get));
      } catch (requestError) {
        setError(getRequestErrorMessage(requestError,
          "No AQGreen test checkout is available for your account."));
      } finally {
        setLoading(false);
      }
    })();
  }, []);

  const confirm = async () => {
    if (!checkout || checkout.confirmed) return;
    setConfirming(true);
    setError(undefined);
    try {
      const result = await httpClient.post<ConfirmationResult, { checkoutId: string; amount: number }>(apiEndpoints.aqgreenV2DemoCheckout.confirm, {
        checkoutId: checkout.checkoutId,
        amount: checkout.amount,
      });
      setCheckout({ ...checkout, confirmed: true, participationStatus: result.participationStatus });
    } catch (requestError) {
      setError(getRequestErrorMessage(requestError,
        "The test checkout could not be confirmed. No real payment was taken."));
    } finally {
      setConfirming(false);
    }
  };

  return (
    <main className="min-h-dvh bg-muted/30 px-4 py-8 sm:px-6">
      <div className="mx-auto max-w-2xl space-y-6">
        <Breadcrumb items={[{ href: "/member/programmes", label: "My programmes" }, { label: "Test checkout" }]} />
        <Card className="space-y-6">
          <div>
            <p className="text-sm font-bold uppercase tracking-wide text-amber-900">
              TEST ENVIRONMENT — SIMULATED MONEY — NO REAL PAYMENT OR EARNINGS
            </p>
            <h1 className="mt-3 text-3xl font-bold">AQGreen simulated checkout</h1>
            <p className="mt-2 text-muted-foreground">
              This action records test money only. It does not contact a payment provider,
              charge you, activate participation or create payable earnings.
            </p>
          </div>
          {loading ? <Skeleton className="h-40" /> : null}
          {error ? <StatusMessage tone="error">{error}</StatusMessage> : null}
          {checkout ? (
            <div className="space-y-5">
              <div className="rounded-xl border border-border p-5">
                <p className="text-sm text-muted-foreground">Exact simulated amount</p>
                <p className="mt-1 text-3xl font-bold">{formatAmount(checkout.amount, checkout.currency)}</p>
                <p className="mt-1 text-sm">No bank card or payment details are required.</p>
              </div>
              {checkout.confirmed && checkout.participationStatus === "PaymentConfirmedAwaitingApproval" ? (
                <StatusMessage tone="info">
                  Test payment confirmed. Your AQGreen participation is pending Area approval;
                  it is not active yet. Return later to check your status.
                </StatusMessage>
              ) : checkout.confirmed && checkout.participationStatus === "Active" ? (
                <StatusMessage tone="info">
                  Your test participation is active after Area approval. View your placement and progress below.
                </StatusMessage>
              ) : checkout.confirmed ? (
                <StatusMessage tone="info">
                  This test instalment is confirmed. Check your programme page for the remaining joining amount.
                </StatusMessage>
              ) : (
                <Button isLoading={confirming} onClick={() => void confirm()}>
                  Confirm simulated payment
                </Button>
              )}
              <LinkButton href="/member/programmes" variant="outline">
                View my programme status
              </LinkButton>
            </div>
          ) : null}
        </Card>
      </div>
    </main>
  );
};
