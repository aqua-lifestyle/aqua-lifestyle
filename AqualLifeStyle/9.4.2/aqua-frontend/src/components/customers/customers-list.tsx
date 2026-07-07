"use client";

import Link from "next/link";
import { useEffect } from "react";

import {
  type Customer,
  useCustomersActions,
  useCustomersState,
} from "@/src/providers";
import { Badge, Card, StatusMessage } from "@/src/shared/ui";

const membershipLabels: Record<number, string> = {
  1: "Jasper",
  2: "Onyx",
  3: "AQGreen",
  4: "Business Premier",
};

const getMembershipLabel = (membershipId: number | null) => {
  if (membershipId === null) {
    return "No membership assigned";
  }

  return membershipLabels[membershipId] ?? `Membership ${membershipId}`;
};

const CustomerCard = ({ customer }: { customer: Customer }) => {
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
        {getMembershipLabel(customer.membershipId)}
      </p>
    </Card>
  );
};

export const CustomersList = () => {
  const { getCustomers } = useCustomersActions();
  const {
    customers,
    isLoadError,
    isLoadPending,
    loadErrorMessage,
  } = useCustomersState();

  useEffect(() => {
    void getCustomers();
  }, [getCustomers]);

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
            <Link
              className="rounded-lg border border-zinc-300 bg-white px-4 py-2 text-center text-sm font-semibold text-zinc-800 transition hover:bg-zinc-100"
              href="/memberships"
            >
              View memberships
            </Link>
            <Link
              className="rounded-lg border border-zinc-300 bg-white px-4 py-2 text-center text-sm font-semibold text-zinc-800 transition hover:bg-zinc-100"
              href="/products"
            >
              View products
            </Link>
            <Link
              className="rounded-lg bg-emerald-700 px-4 py-2 text-center text-sm font-semibold text-white transition hover:bg-emerald-800"
              href="/customers/register"
            >
              Register customer
            </Link>
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
              <CustomerCard customer={customer} key={customer.id} />
            ))}
          </section>
        ) : null}
      </div>
    </main>
  );
};
