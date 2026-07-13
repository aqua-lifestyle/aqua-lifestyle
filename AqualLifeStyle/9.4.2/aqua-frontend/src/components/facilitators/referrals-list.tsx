"use client";

import { useEffect, useMemo, useState } from "react";
import { DollarSign } from "lucide-react";

import {
  useReferralsActions,
  useReferralsState,
} from "@/src/providers";
import {
  Avatar,
  Badge,
  Breadcrumb,
  Card,
  DataTable,
  EmptyState,
  LinkButton,
  SelectField,
  Skeleton,
  StatusMessage,
} from "@/src/shared/ui";

type ReferralTypeFilter = "all" | "0" | "1";

const referralTypeLabel = (value: number) => (value === 0 ? "Direct" : "Indirect");

const referralTypeTone = (value: number): "neutral" | "success" | "info" => {
  if (value === 1) return "info";
  return "neutral";
};

export const ReferralsList = () => {
  const [typeFilter, setTypeFilter] = useState<ReferralTypeFilter>("all");
  const { getReferrals } = useReferralsActions();
  const {
    isLoadError,
    isLoadPending,
    loadErrorMessage,
    referrals,
  } = useReferralsState();
  // ALL hooks before early returns
  useEffect(() => {
    void getReferrals();
  }, [getReferrals]);

  const filteredReferrals = useMemo(() => {
    return referrals.filter((referral) => {
      const matchesType =
        typeFilter === "all" || referral.type === Number(typeFilter);
      return matchesType;
    });
  }, [referrals, typeFilter]);

  const tableColumns = [
    {
      header: "Referral",
      key: "id",
      render: (referral: typeof filteredReferrals[number]) => (
        <div className="flex items-center gap-3">
          <Avatar fallback={`R ${referral.id}`} size="sm" />
          <div>
            <p className="font-semibold text-foreground">
              Customer #{referral.referredCustomerId}
            </p>
            <p className="text-xs text-muted-foreground">
              Enquiry #{referral.sourceEnquiryId}
            </p>
          </div>
        </div>
      ),
      sortable: true,
    },
    {
      header: "Type",
      key: "type",
      render: (referral: typeof filteredReferrals[number]) => (
        <Badge tone={referralTypeTone(referral.type)}>
          {referralTypeLabel(referral.type)}
        </Badge>
      ),
      sortable: true,
    },
    {
      header: "Award Amount",
      key: "awardAmount",
      render: (referral: typeof filteredReferrals[number]) => (
        <span className="text-sm">{referral.awardAmount.toFixed(2)}</span>
      ),
      sortable: true,
    },
    {
      header: "Status",
      key: "awardIssued",
      render: (referral: typeof filteredReferrals[number]) => (
        <Badge tone={referral.awardIssued ? "success" : "neutral"}>
          {referral.awardIssued ? "Awarded" : "Pending"}
        </Badge>
      ),
      sortable: true,
    },
    {
      header: "Actions",
      key: "actions",
      render: (referral: typeof filteredReferrals[number]) => (
        <div className="flex items-center gap-2">
          <LinkButton href={`/facilitator/referrals/${referral.id}`} size="sm" variant="outline">
            Open
          </LinkButton>
        </div>
      ),
    },
  ];

  return (
    <main className="min-h-dvh bg-muted/30 px-4 py-6 text-foreground sm:px-6 lg:px-8">
      <div className="mx-auto flex w-full max-w-7xl flex-col gap-6">
        <header className="flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
          <div>
            <Breadcrumb
              items={[{ href: "/", label: "Dashboard" }, { label: "Referrals" }]}
            />
            <h1 className="mt-2 text-3xl font-bold tracking-tight">Referrals</h1>
            <p className="mt-2 max-w-2xl text-base text-muted-foreground">
              Track and manage referrals.
            </p>
          </div>
        </header>

        {isLoadPending ? (
          <Skeleton className="h-96" />
        ) : isLoadError ? (
          <StatusMessage tone="error">
            {loadErrorMessage ?? "Unable to load referrals."}
          </StatusMessage>
        ) : referrals.length === 0 ? (
          <EmptyState
            description="No referrals found."
            icon={DollarSign}
            title="No referrals"
          />
        ) : (
          <Card className="flex flex-col gap-4">
            <div className="flex flex-col gap-4 lg:flex-row lg:items-end lg:justify-between">
              <SelectField
                label="Type"
                name="typeFilter"
                onChange={(event) => setTypeFilter(event.target.value as ReferralTypeFilter)}
                value={typeFilter}
              >
                <option value="all">All types</option>
                <option value="0">Direct</option>
                <option value="1">Indirect</option>
              </SelectField>
            </div>

            <DataTable
              columns={tableColumns}
              data={filteredReferrals}
              emptyState="No referrals match these filters."
              keyExtractor={(referral) => referral.id}
              pageSize={10}
              searchFn={(referral, query) =>
                `Referral #${referral.id} Customer #${referral.referredCustomerId}`
                  .toLowerCase()
                  .includes(query.toLowerCase())
              }
            />
          </Card>
        )}
      </div>
    </main>
  );
};
