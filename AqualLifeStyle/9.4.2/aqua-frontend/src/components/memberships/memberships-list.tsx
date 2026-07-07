"use client";

import { useEffect } from "react";

import {
  type Membership,
  useMembershipsActions,
  useMembershipsState,
} from "@/src/providers";
import { getMembershipTypeLabel } from "@/src/shared/domain";
import { Badge, Card, LinkButton, StatusMessage } from "@/src/shared/ui";

const formatCurrency = (amount: number) =>
  new Intl.NumberFormat("en-ZA", {
    style: "currency",
    currency: "ZAR",
  }).format(amount);

const MembershipCard = ({ membership }: { membership: Membership }) => {
  const membershipTypeLabel = getMembershipTypeLabel(membership.membershipType);
  const shouldShowType =
    membership.name.trim().toLocaleLowerCase() !==
    membershipTypeLabel.toLocaleLowerCase();

  return (
    <Card>
      <div className="flex items-start justify-between gap-4">
        <div className="min-w-0">
          <h2 className="truncate text-lg font-semibold text-zinc-950">
            {membership.name}
          </h2>
          <p className="mt-1 line-clamp-2 text-sm text-zinc-600">
            {membership.description ?? "No description available."}
          </p>
        </div>
        <Badge tone={membership.isActive ? "success" : "neutral"}>
          {membership.isActive ? "Active" : "Inactive"}
        </Badge>
      </div>

      <dl className="mt-6 grid gap-3 text-sm">
        <div className="flex justify-between gap-4">
          <dt className="text-zinc-600">Monthly obligation</dt>
          <dd className="font-medium text-zinc-950">
            {formatCurrency(membership.monthlyObligationAmount)}
          </dd>
        </div>
        {shouldShowType ? (
          <div className="flex justify-between gap-4">
            <dt className="text-zinc-600">Type</dt>
            <dd className="font-medium text-zinc-950">
              {membershipTypeLabel}
            </dd>
          </div>
        ) : null}
      </dl>
    </Card>
  );
};

export const MembershipsList = () => {
  const { getMemberships } = useMembershipsActions();
  const { errorMessage, isError, isPending, memberships } =
    useMembershipsState();

  useEffect(() => {
    void getMemberships();
  }, [getMemberships]);

  return (
    <main className="min-h-dvh bg-zinc-50 px-6 py-8 text-zinc-950 sm:px-8 lg:px-12">
      <div className="mx-auto flex w-full max-w-6xl flex-col gap-8">
        <header className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
          <div className="flex flex-col gap-2">
            <p className="text-sm font-medium uppercase tracking-wide text-emerald-700">
              Aqua Lifestyle Club
            </p>
            <h1 className="text-3xl font-semibold tracking-tight">
              Memberships
            </h1>
            <p className="max-w-2xl text-base text-zinc-600">
              Membership tiers loaded from the ABP backend, including active
              status and monthly obligation data.
            </p>
          </div>
          <div className="flex flex-col gap-3 sm:flex-row">
            <LinkButton href="/customers">
              View customers
            </LinkButton>
            <LinkButton href="/products" variant="primary">
              View products
            </LinkButton>
          </div>
        </header>

        {isPending ? (
          <StatusMessage>Loading memberships...</StatusMessage>
        ) : null}

        {isError ? (
          <StatusMessage tone="error">
            {errorMessage ?? "Unable to load memberships."}
          </StatusMessage>
        ) : null}

        {!isPending && !isError && memberships.length === 0 ? (
          <StatusMessage>No memberships are available yet.</StatusMessage>
        ) : null}

        {memberships.length > 0 ? (
          <section className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
            {memberships.map((membership) => (
              <MembershipCard key={membership.id} membership={membership} />
            ))}
          </section>
        ) : null}
      </div>
    </main>
  );
};
