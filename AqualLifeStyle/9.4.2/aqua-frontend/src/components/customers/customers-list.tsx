"use client";

import { useEffect, useState } from "react";

import {
  type Customer,
  useCustomersActions,
  useCustomersState,
  useMembershipsActions,
  useMembershipsState,
} from "@/src/providers";
import { getMembershipNameById } from "@/src/shared/domain";
import {
  Badge,
  Card,
  LinkButton,
  SelectField,
  StatusMessage,
} from "@/src/shared/ui";

type CustomerStatusFilter = "all" | "active" | "inactive";

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

      <div className="mt-6 flex flex-col gap-3">
        <LinkButton href={`/customers/${customer.id}`}>Open customer</LinkButton>
        <LinkButton href={`/enquiries/create?customerId=${customer.id}`}>
          Create enquiry
        </LinkButton>
      </div>
    </Card>
  );
};

export const CustomersList = () => {
  const [membershipFilter, setMembershipFilter] = useState("all");
  const [statusFilter, setStatusFilter] = useState<CustomerStatusFilter>("all");
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

  const filteredCustomers = customers.filter((customer) => {
    const matchesStatus =
      statusFilter === "all" ||
      (statusFilter === "active" && customer.isActive) ||
      (statusFilter === "inactive" && !customer.isActive);

    const matchesMembership =
      membershipFilter === "all" ||
      (membershipFilter === "none" && customer.membershipId === null) ||
      customer.membershipId === Number(membershipFilter);

    return matchesStatus && matchesMembership;
  });

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
            <LinkButton href="/enquiries/create">
              Create enquiry
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
          <StatusMessage>
            <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
              <span>No customers are available yet.</span>
              <LinkButton href="/customers/register" variant="primary">
                Register customer
              </LinkButton>
            </div>
          </StatusMessage>
        ) : null}

        {customers.length > 0 ? (
          <section className="grid gap-4 rounded-lg border border-zinc-200 bg-white p-4 shadow-sm md:grid-cols-[1fr_14rem_18rem] md:items-end">
            <div>
              <h2 className="text-lg font-semibold text-zinc-950">
                Customer filters
              </h2>
              <p className="mt-2 text-sm leading-6 text-zinc-600">
                Filter live customer records by status and membership tier to
                validate activation quality during the demo.
              </p>
            </div>
            <SelectField
              label="Status"
              name="statusFilter"
              onChange={(event) =>
                setStatusFilter(event.target.value as CustomerStatusFilter)
              }
              value={statusFilter}
            >
              <option value="all">All statuses</option>
              <option value="active">Active</option>
              <option value="inactive">Inactive</option>
            </SelectField>
            <SelectField
              label="Membership"
              name="membershipFilter"
              onChange={(event) => setMembershipFilter(event.target.value)}
              value={membershipFilter}
            >
              <option value="all">All memberships</option>
              <option value="none">No membership assigned</option>
              {memberships.map((membership) => (
                <option key={membership.id} value={membership.id}>
                  {membership.name}
                </option>
              ))}
            </SelectField>
          </section>
        ) : null}

        {customers.length > 0 && filteredCustomers.length === 0 ? (
          <StatusMessage>
            <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
              <span>No customers match these filters.</span>
              <LinkButton href="/customers/register" variant="primary">
                Register customer
              </LinkButton>
            </div>
          </StatusMessage>
        ) : null}

        {filteredCustomers.length > 0 ? (
          <section className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
            {filteredCustomers.map((customer) => (
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
