"use client";

import { useEffect } from "react";

import { useMembershipsActions, useMembershipsState } from "@/src/providers";
import { getMembershipTypeLabel } from "@/src/shared/domain";
import { Badge, Card, LinkButton, StatusMessage } from "@/src/shared/ui";

type MembershipDetailsProps = {
  membershipId: number;
};

const formatCurrency = (amount: number) =>
  new Intl.NumberFormat("en-ZA", {
    style: "currency",
    currency: "ZAR",
  }).format(amount);

const formatPercent = (value: number) =>
  new Intl.NumberFormat("en-ZA", {
    maximumFractionDigits: 1,
    style: "percent",
  }).format(value / 100);

const formatDate = (date: string | null) => {
  if (!date) {
    return "Not set";
  }

  return new Intl.DateTimeFormat("en-ZA", {
    dateStyle: "medium",
  }).format(new Date(date));
};

export const MembershipDetails = ({ membershipId }: MembershipDetailsProps) => {
  const { getMembership, getTierBenefits } = useMembershipsActions();
  const {
    isSelectedError,
    isSelectedPending,
    isTierBenefitsError,
    isTierBenefitsPending,
    selectedErrorMessage,
    selectedMembership,
    tierBenefits,
    tierBenefitsErrorMessage,
  } = useMembershipsState();

  useEffect(() => {
    if (!Number.isInteger(membershipId) || membershipId <= 0) {
      return;
    }

    void getMembership(membershipId);
    void getTierBenefits(membershipId);
  }, [getMembership, getTierBenefits, membershipId]);

  return (
    <main className="min-h-dvh bg-zinc-50 px-6 py-8 text-zinc-950 sm:px-8 lg:px-12">
      <div className="mx-auto flex w-full max-w-5xl flex-col gap-8">
        <header className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
          <div className="flex flex-col gap-2">
            <p className="text-sm font-medium uppercase tracking-wide text-emerald-700">
              Aqua Lifestyle Club
            </p>
            <h1 className="text-3xl font-semibold tracking-tight">
              Membership details
            </h1>
            <p className="max-w-2xl text-base text-zinc-600">
              Review tier status and obligation data before assigning it to a
              customer.
            </p>
          </div>
          <LinkButton href="/memberships">Back to memberships</LinkButton>
        </header>

        {!Number.isInteger(membershipId) || membershipId <= 0 ? (
          <StatusMessage tone="error">
            This membership id is invalid.
          </StatusMessage>
        ) : null}

        {isSelectedPending ? (
          <StatusMessage>Loading membership...</StatusMessage>
        ) : null}

        {isSelectedError ? (
          <StatusMessage tone="error">
            {selectedErrorMessage ?? "Unable to load this membership."}
          </StatusMessage>
        ) : null}

        {selectedMembership ? (
          <section className="grid gap-6 lg:grid-cols-[1fr_22rem]">
            <Card>
              <div className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
                <div>
                  <h2 className="text-2xl font-semibold tracking-tight">
                    {selectedMembership.name}
                  </h2>
                  <p className="mt-2 text-sm text-zinc-600">
                    {getMembershipTypeLabel(selectedMembership.membershipType)}
                  </p>
                </div>
                <Badge tone={selectedMembership.isActive ? "success" : "neutral"}>
                  {selectedMembership.isActive ? "Active" : "Inactive"}
                </Badge>
              </div>

              <p className="mt-8 whitespace-pre-line text-sm leading-6 text-zinc-700">
                {selectedMembership.description ?? "No description available."}
              </p>
            </Card>

            <aside className="flex flex-col gap-6">
              <Card>
                <h2 className="text-lg font-semibold">Obligation</h2>
                <dl className="mt-5 grid gap-3 text-sm">
                  <div className="flex justify-between gap-4">
                    <dt className="text-zinc-600">Monthly amount</dt>
                    <dd className="font-medium text-zinc-950">
                      {formatCurrency(selectedMembership.monthlyObligationAmount)}
                    </dd>
                  </div>
                  <div className="flex justify-between gap-4">
                    <dt className="text-zinc-600">Activation date</dt>
                    <dd className="text-right font-medium text-zinc-950">
                      {formatDate(selectedMembership.activationDate)}
                    </dd>
                  </div>
                  <div className="flex justify-between gap-4">
                    <dt className="text-zinc-600">Last met</dt>
                    <dd className="text-right font-medium text-zinc-950">
                      {formatDate(selectedMembership.lastObligationMetDate)}
                    </dd>
                  </div>
                </dl>
              </Card>

              <Card>
                <h2 className="text-lg font-semibold">Demo actions</h2>
                <p className="mt-3 text-sm leading-6 text-zinc-600">
                  Assign this membership while registering or editing a customer,
                  then verify product access from the catalog.
                </p>
                <div className="mt-6 flex flex-col gap-3">
                  <LinkButton
                    href={`/customers/register?membershipId=${selectedMembership.id}`}
                    variant="primary"
                  >
                    Register customer
                  </LinkButton>
                  <LinkButton href="/customers">View customers</LinkButton>
                  <LinkButton href="/products">View products</LinkButton>
                </div>
              </Card>
            </aside>

            <section className="lg:col-span-2">
              <Card>
                <div className="flex flex-col gap-2">
                  <h2 className="text-lg font-semibold">Tier benefits</h2>
                  <p className="text-sm leading-6 text-zinc-600">
                    Backend-calculated club access rules for this membership
                    tier, including buying windows, savings windows, discounts,
                    and participation benefits.
                  </p>
                </div>

                {isTierBenefitsPending ? (
                  <StatusMessage>Loading tier benefits...</StatusMessage>
                ) : null}

                {isTierBenefitsError ? (
                  <StatusMessage tone="error">
                    {tierBenefitsErrorMessage ??
                      "Unable to load tier benefits for this membership."}
                  </StatusMessage>
                ) : null}

                {tierBenefits ? (
                  <div className="mt-6 grid gap-4 md:grid-cols-2 xl:grid-cols-3">
                    <div className="rounded-lg border border-zinc-200 bg-zinc-50 p-4">
                      <p className="text-sm text-zinc-600">Monthly obligation</p>
                      <p className="mt-2 text-xl font-semibold">
                        {formatCurrency(tierBenefits.monthlyObligation)}
                      </p>
                    </div>

                    <div className="rounded-lg border border-zinc-200 bg-zinc-50 p-4">
                      <div className="flex items-start justify-between gap-3">
                        <div>
                          <p className="text-sm text-zinc-600">Order window</p>
                          <p className="mt-2 text-xl font-semibold">
                            Day {tierBenefits.orderWindowStartDay}-
                            {tierBenefits.orderWindowEndDay}
                          </p>
                        </div>
                        <Badge
                          tone={
                            tierBenefits.isOrderWindowOpen ? "success" : "neutral"
                          }
                        >
                          {tierBenefits.isOrderWindowOpen ? "Open" : "Closed"}
                        </Badge>
                      </div>
                    </div>

                    <div className="rounded-lg border border-zinc-200 bg-zinc-50 p-4">
                      <div className="flex items-start justify-between gap-3">
                        <div>
                          <p className="text-sm text-zinc-600">
                            Savings contribution window
                          </p>
                          <p className="mt-2 text-xl font-semibold">
                            Day {tierBenefits.savingsWindowOpenDay}-
                            {tierBenefits.savingsWindowCloseDay}
                          </p>
                        </div>
                        <Badge
                          tone={
                            tierBenefits.isSavingsWindowOpen
                              ? "success"
                              : "neutral"
                          }
                        >
                          {tierBenefits.isSavingsWindowOpen ? "Open" : "Closed"}
                        </Badge>
                      </div>
                    </div>

                    <div className="rounded-lg border border-zinc-200 bg-zinc-50 p-4">
                      <p className="text-sm text-zinc-600">Product discount</p>
                      <p className="mt-2 text-xl font-semibold">
                        {formatPercent(tierBenefits.productPricingDiscount)}
                      </p>
                    </div>

                    <div className="rounded-lg border border-zinc-200 bg-zinc-50 p-4">
                      <p className="text-sm text-zinc-600">
                        Referral commission
                      </p>
                      <p className="mt-2 text-xl font-semibold">
                        {formatPercent(tierBenefits.referralCommissionRate)}
                      </p>
                    </div>

                    <div className="rounded-lg border border-zinc-200 bg-zinc-50 p-4">
                      <p className="text-sm text-zinc-600">Profit share</p>
                      <p className="mt-2 text-xl font-semibold">
                        {formatPercent(tierBenefits.profitSharePercentage)}
                      </p>
                    </div>

                    <div className="rounded-lg border border-zinc-200 bg-zinc-50 p-4 md:col-span-2 xl:col-span-3">
                      <dl className="grid gap-3 text-sm sm:grid-cols-3">
                        <div>
                          <dt className="text-zinc-600">Tier</dt>
                          <dd className="mt-1 font-medium text-zinc-950">
                            {tierBenefits.tierName}
                          </dd>
                        </div>
                        <div>
                          <dt className="text-zinc-600">
                            12-month savings interest
                          </dt>
                          <dd className="mt-1 font-medium text-zinc-950">
                            {formatPercent(
                              tierBenefits.savingsMaturityInterestRate,
                            )}
                          </dd>
                        </div>
                        <div>
                          <dt className="text-zinc-600">Max concurrent orders</dt>
                          <dd className="mt-1 font-medium text-zinc-950">
                            {tierBenefits.maxConcurrentOrders}
                          </dd>
                        </div>
                      </dl>
                    </div>
                  </div>
                ) : null}
              </Card>
            </section>
          </section>
        ) : null}
      </div>
    </main>
  );
};
