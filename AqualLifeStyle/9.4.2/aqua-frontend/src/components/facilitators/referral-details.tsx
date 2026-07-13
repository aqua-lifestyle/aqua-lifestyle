"use client";

import { useEffect, useState } from "react";

import {
  type Referral,
  useReferralsActions,
  useReferralsState,
} from "@/src/providers";
import {
  Breadcrumb,
  Button,
  Card,
  LinkButton,
  Skeleton,
  StatusMessage,
  Tabs,
} from "@/src/shared/ui";

type ReferralDetailsProps = {
  referralId: number;
};

const referralTypeLabel = (value: number) => (value === 0 ? "Direct" : "Indirect");

const ReferralOverview = ({
  referral,
  confirmAward,
  isConfirmPending,
}: {
  referral: Referral;
  confirmAward: (id: number) => Promise<boolean>;
  isConfirmPending: boolean;
}) => {
  return (
    <div className="grid gap-6 lg:grid-cols-[1fr_22rem]">
      <Card>
        <h2 className="text-lg font-semibold">Referral details</h2>
        <dl className="mt-4 grid gap-3 text-sm">
          <div className="flex justify-between gap-4">
            <dt className="text-muted-foreground">Referred Customer</dt>
            <dd className="font-medium">Customer #{referral.referredCustomerId}</dd>
          </div>
          <div className="flex justify-between gap-4">
            <dt className="text-muted-foreground">Source Enquiry</dt>
            <dd className="font-medium">Enquiry #{referral.sourceEnquiryId}</dd>
          </div>
          <div className="flex justify-between gap-4">
            <dt className="text-muted-foreground">Type</dt>
            <dd className="font-medium">{referralTypeLabel(referral.type)}</dd>
          </div>
          <div className="flex justify-between gap-4">
            <dt className="text-muted-foreground">Award Amount</dt>
            <dd className="font-medium">{referral.awardAmount.toFixed(2)}</dd>
          </div>
          <div className="flex justify-between gap-4">
            <dt className="text-muted-foreground">Award Issued</dt>
            <dd className="font-medium">{referral.awardIssued ? "Yes" : "No"}</dd>
          </div>
          {referral.confirmedAt ? (
            <div className="flex justify-between gap-4">
              <dt className="text-muted-foreground">Confirmed At</dt>
              <dd className="font-medium">
                {new Date(referral.confirmedAt).toLocaleString()}
              </dd>
            </div>
          ) : null}
          {referral.convertedAt ? (
            <div className="flex justify-between gap-4">
              <dt className="text-muted-foreground">Converted At</dt>
              <dd className="font-medium">
                {new Date(referral.convertedAt).toLocaleString()}
              </dd>
            </div>
          ) : null}
        </dl>
      </Card>

      <aside className="flex flex-col gap-6">
        <Card>
          <h3 className="text-lg font-semibold">Actions</h3>
          <div className="mt-4 flex flex-col gap-2">
            {!referral.awardIssued ? (
              <Button
                disabled={isConfirmPending}
                onClick={() => confirmAward(referral.id)}
                type="button"
                variant="primary"
              >
                Confirm Award
              </Button>
            ) : null}
          </div>
        </Card>
      </aside>
    </div>
  );
};

export const ReferralDetails = ({ referralId }: ReferralDetailsProps) => {
  const { getReferral, confirmAward } = useReferralsActions();
  const {
    isConfirmError,
    isConfirmPending,
    isConfirmSuccess,
    isSelectedError,
    isSelectedPending,
    selectedReferral,
    selectedErrorMessage,
    confirmErrorMessage,
  } = useReferralsState();
  const [activeTab, setActiveTab] = useState("overview");

  useEffect(() => {
    if (!Number.isInteger(referralId) || referralId <= 0) {
      return;
    }

    void getReferral(referralId);
  }, [referralId, getReferral]);

  const isInvalid = !Number.isInteger(referralId) || referralId <= 0;

  return (
    <main className="min-h-dvh bg-muted/30 px-4 py-6 text-foreground sm:px-6 lg:px-8">
      <div className="mx-auto flex w-full max-w-5xl flex-col gap-6">
        <header className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
          <div>
            <Breadcrumb
              items={[
                { href: "/", label: "Dashboard" },
                { href: "/facilitator/referrals", label: "Referrals" },
                { label: "Referral details" },
              ]}
            />
            <h1 className="mt-2 text-3xl font-bold tracking-tight">Referral details</h1>
            <p className="mt-2 max-w-2xl text-base text-muted-foreground">
              Review referral information and manage awards.
            </p>
          </div>
          <LinkButton href="/facilitator/referrals" variant="outline">
            Back to referrals
          </LinkButton>
        </header>

        {isInvalid ? (
          <StatusMessage tone="error">This referral id is invalid.</StatusMessage>
        ) : null}
        {isSelectedPending ? (
          <Skeleton className="h-96" />
        ) : null}
        {isSelectedError ? (
          <StatusMessage tone="error">
            {selectedErrorMessage ?? "Unable to load this referral."}
          </StatusMessage>
        ) : null}

        {selectedReferral ? (
          <Tabs
            onChange={setActiveTab}
            tabs={[
              {
                content: (
                  <ReferralOverview
                    confirmAward={confirmAward}
                    isConfirmPending={isConfirmPending}
                    referral={selectedReferral}
                  />
                ),
                id: "overview",
                label: "Overview",
              },
            ]}
            value={activeTab}
          />
        ) : null}

        {isConfirmSuccess ? (
          <StatusMessage tone="success">Referral award confirmed.</StatusMessage>
        ) : null}
        {isConfirmError ? (
          <StatusMessage tone="error">
            {confirmErrorMessage ?? "Unable to confirm this referral award."}
          </StatusMessage>
        ) : null}
      </div>
    </main>
  );
};
