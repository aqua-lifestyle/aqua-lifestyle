"use client";

import { useEffect } from "react";

import {
  type Customer,
  useCustomersActions,
  useCustomersState,
  useMembershipsActions,
  useMembershipsState,
} from "@/src/providers";
import { getMembershipNameById } from "@/src/shared/domain";
import { Badge, Card, LinkButton, StatusMessage } from "@/src/shared/ui";

const CustomerCard = ({
  customer,
  membershipName,
}: {
  customer: Customer;
  membershipName: string;
}) => {
  return (
    <Card>
      <div className="flex items-start justify-between gap-4">
        <div className="min-w-0">
          <h2 className="truncate text-lg font-semibold text-zinc-950">
            {customer.name}
          </h2>
          <p className="mt-1 break-words text-sm text-zinc-600">
            {customer.email}
          </p>
        </div>
        <Badge tone={customer.isActive ? "success" : "neutral"}>
          {customer.isActive ? "Active" : "Inactive"}
        </Badge>
      </div>

      <p className="mt-6 text-sm font-medium text-zinc-700">
        {membershipName}
      </p>
    </Card>
  );
};

export const CustomersList = () => {
  const { getCustomers } = useCustomersActions();
  const { getMemberships } = useMembershipsActions();
  const {
    customers,
    isLoadError,
    isLoadPending,
    loadErrorMessage,
  } = useCustomersState();
  const { memberships } = useMembershipsState();

  useEffect(() => {
    void getCustomers();
    void getMemberships();
  }, [getCustomers, getMemberships]);

  return (
    <main className="min-h-dvh bg-zinc-50 px-6 py-8 text-zinc-950 sm:px-8 lg:px-12">
      <div className="mx-auto flex w-full max-w-6xl flex-col gap-8">
        <header className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
          <div className="flex flex-col gap-2">
            <p className="text-sm font-medium uppercase tracking-wide text-emerald-700">
              Aqua Lifestyle Club
            </p>
            <h1 className="text-3xl font-semibold tracking-tight">Customers</h1>
            <p className="max-w-2xl text-base text-zinc-600">
              Customer records loaded from the ABP backend. Use this page to
              verify registrations without leaving the frontend.
            </p>
          </div>
          <div className="flex flex-col gap-3 sm:flex-row">
            <LinkButton href="/enquiries">
              View enquiries
            </LinkButton>
            <LinkButton href="/memberships">
              View memberships
            </LinkButton>
            <LinkButton href="/products">
              View products
            </LinkButton>
            <LinkButton href="/customers/register" variant="primary">
              Register customer
            </LinkButton>
          </div>
        </header>

        {isLoadPending ? (
          <StatusMessage>Loading customers...</StatusMessage>
        ) : null}

        {isLoadError ? (
          <StatusMessage tone="error">
            {loadErrorMessage ?? "Unable to load customers."}
          </StatusMessage>
        ) : null}

        {!isLoadPending && !isLoadError && customers.length === 0 ? (
          <StatusMessage>No customers are available yet.</StatusMessage>
        ) : null}

        {customers.length > 0 ? (
          <section className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
            {customers.map((customer) => (
              <CustomerCard
                customer={customer}
                key={customer.id}
                membershipName={getMembershipNameById(
                  memberships,
                  customer.membershipId,
                  "No membership assigned",
                )}
              />
            ))}
          </section>
        ) : null}
      </div>
    </main>
  );
};
