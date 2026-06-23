"use client";

import { useEffect, useState } from "react";

import {
  type Membership,
  type MembershipType,
  useMembershipsActions,
  useMembershipsState,
} from "@/src/providers";
import { getMembershipTypeLabel } from "@/src/shared/domain";
import {
  Badge,
  Card,
  LinkButton,
  SelectField,
  StatusMessage,
} from "@/src/shared/ui";

type MembershipStatusFilter = "all" | "active" | "inactive";

const membershipTypes: MembershipType[] = [0, 1, 2, 3];

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

      <div className="mt-6 flex flex-col gap-3">
        <LinkButton href={`/memberships/${membership.id}`}>
          Open membership
        </LinkButton>
        <LinkButton href={`/customers/register?membershipId=${membership.id}`}>
          Register customer
        </LinkButton>
      </div>
    </Card>
  );
};

export const MembershipsList = () => {
  const [statusFilter, setStatusFilter] =
    useState<MembershipStatusFilter>("all");
  const [tierFilter, setTierFilter] = useState("all");
  const { getMemberships, getSavingsWindowStatuses } = useMembershipsActions();
  const {
    errorMessage,
    isError,
    isPending,
    isSavingsWindowStatusesError,
    isSavingsWindowStatusesPending,
    memberships,
    savingsWindowStatuses,
    savingsWindowStatusesErrorMessage,
  } = useMembershipsState();

  useEffect(() => {
    void getMemberships();
    void getSavingsWindowStatuses();
  }, [getMemberships, getSavingsWindowStatuses]);

  const filteredMemberships = memberships.filter((membership) => {
    const matchesStatus =
      statusFilter === "all" ||
      (statusFilter === "active" && membership.isActive) ||
      (statusFilter === "inactive" && !membership.isActive);

    const matchesTier =
      tierFilter === "all" || membership.membershipType === Number(tierFilter);

    return matchesStatus && matchesTier;
  });

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
            <LinkButton href="/enquiries">
              View enquiries
            </LinkButton>
            <LinkButton href="/enquiries/create">
              Create enquiry
            </LinkButton>
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

        <section className="rounded-lg border border-zinc-200 bg-white p-4 shadow-sm">
          <div className="flex flex-col gap-2">
            <h2 className="text-lg font-semibold text-zinc-950">
              Savings window readiness
            </h2>
            <p className="max-w-3xl text-sm leading-6 text-zinc-600">
              Backend-calculated monthly savings activity windows by tier. This
              is a read-only demo signal, not full savings account management.
            </p>
          </div>

          {isSavingsWindowStatusesPending ? (
            <div className="mt-4">
              <StatusMessage>Loading savings window readiness...</StatusMessage>
            </div>
          ) : null}

          {isSavingsWindowStatusesError ? (
            <div className="mt-4">
              <StatusMessage tone="error">
                {savingsWindowStatusesErrorMessage ??
                  "Unable to load savings window readiness."}
              </StatusMessage>
            </div>
          ) : null}

          {savingsWindowStatuses.length > 0 ? (
            <div className="mt-5 grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
              {savingsWindowStatuses.map((status) => (
                <div
                  className="rounded-lg border border-zinc-200 bg-zinc-50 p-4"
                  key={status.tier}
                >
                  <div className="flex items-start justify-between gap-3">
                    <div>
                      <p className="font-semibold text-zinc-950">
                        {status.tierName}
                      </p>
                      <p className="mt-1 text-sm text-zinc-600">
                        Day {status.savingsWindowOpenDay}-
                        {status.savingsWindowCloseDay}
                      </p>
                    </div>
                    <Badge
                      tone={status.isSavingsWindowOpen ? "success" : "neutral"}
                    >
                      {status.statusLabel}
                    </Badge>
                  </div>
                  <p className="mt-4 text-sm leading-6 text-zinc-600">
                    Checked for day {status.currentDay} on {status.asOfDate}.
                  </p>
                </div>
              ))}
            </div>
          ) : null}
        </section>

        {!isPending && !isError && memberships.length === 0 ? (
          <StatusMessage>
            <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
              <span>No memberships are available yet.</span>
              <LinkButton href="/products">View products</LinkButton>
            </div>
          </StatusMessage>
        ) : null}

        {memberships.length > 0 ? (
          <section className="grid gap-4 rounded-lg border border-zinc-200 bg-white p-4 shadow-sm md:grid-cols-[1fr_14rem_18rem] md:items-end">
            <div>
              <h2 className="text-lg font-semibold text-zinc-950">
                Membership filters
              </h2>
              <p className="mt-2 text-sm leading-6 text-zinc-600">
                Filter backend tiers by active status or club level before
                assigning access to a customer.
              </p>
            </div>
            <SelectField
              label="Status"
              name="membershipStatusFilter"
              onChange={(event) =>
                setStatusFilter(event.target.value as MembershipStatusFilter)
              }
              value={statusFilter}
            >
              <option value="all">All statuses</option>
              <option value="active">Active</option>
              <option value="inactive">Inactive</option>
            </SelectField>
            <SelectField
              label="Tier"
              name="tierFilter"
              onChange={(event) => setTierFilter(event.target.value)}
              value={tierFilter}
            >
              <option value="all">All tiers</option>
              {membershipTypes.map((membershipType) => (
                <option key={membershipType} value={membershipType}>
                  {getMembershipTypeLabel(membershipType)}
                </option>
              ))}
            </SelectField>
          </section>
        ) : null}

        {memberships.length > 0 && filteredMemberships.length === 0 ? (
          <StatusMessage>
            <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
              <span>No memberships match these filters.</span>
              <LinkButton href="/products">Review products</LinkButton>
            </div>
          </StatusMessage>
        ) : null}

        {filteredMemberships.length > 0 ? (
          <section className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
            {filteredMemberships.map((membership) => (
              <MembershipCard key={membership.id} membership={membership} />
            ))}
          </section>
        ) : null}
      </div>
    </main>
  );
};
