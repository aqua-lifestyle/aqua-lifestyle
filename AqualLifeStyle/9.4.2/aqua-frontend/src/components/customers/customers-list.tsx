"use client";

import {
  Grid3X3,
  Plus,
  Table as TableIcon,
  Users,
} from "lucide-react";
import { useEffect, useMemo, useState } from "react";

import {
  useCustomersActions,
  useCustomersState,
  useMembershipsActions,
  useMembershipsState,
  useAuthState,
} from "@/src/providers";
import { getMembershipNameById } from "@/src/shared/domain";
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
import { cn } from "@/src/shared/lib/utils";

type CustomerStatusFilter = "all" | "active" | "inactive";

const searchFn = (customer: { email: string; name: string }, query: string) => {
  return (
    customer.name.toLowerCase().includes(query) ||
    customer.email.toLowerCase().includes(query)
  );
};

export const CustomersList = () => {
  const [membershipFilter, setMembershipFilter] = useState("all");
  const [statusFilter, setStatusFilter] = useState<CustomerStatusFilter>("all");
  const [view, setView] = useState<"table" | "cards">("table");
  const { getCustomers } = useCustomersActions();
  const { getMemberships } = useMembershipsActions();
  const {
    customers,
    isLoadError,
    isLoadPending,
    loadErrorMessage,
  } = useCustomersState();
  const { memberships } = useMembershipsState();
  const { session } = useAuthState();

  const currentUserId = session?.user?.id ?? null;
  const canViewAllCustomers = session?.user?.permissions?.includes("Aqua.Members.View") ?? false;
  const canCreateCustomer = session?.user?.permissions?.includes("Aqua.Members.Create") ?? false;

  const canOpenCustomer = (customer: { userId: number }) => {
    if (canViewAllCustomers) return true;
    if (currentUserId !== null && customer.userId === currentUserId) return true;
    return false;
  };

  useEffect(() => {
    void getCustomers();
    void getMemberships();
  }, [getCustomers, getMemberships]);

  const filteredCustomers = useMemo(() => {
    return customers.filter((customer) => {
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
  }, [customers, membershipFilter, statusFilter]);

  const activeCount = customers.filter((c) => c.isActive).length;
  const inactiveCount = customers.length - activeCount;

  const tableCustomers = filteredCustomers;

  const tableColumns = [
    {
      header: "Customer",
      key: "name",
      render: (customer: typeof tableCustomers[number]) => (
        <div className="flex items-center gap-3">
          <Avatar fallback={customer.name} size="sm" />
          <div>
            <p className="font-semibold text-foreground">{customer.name}</p>
            <p className="text-xs text-muted-foreground">{customer.email}</p>
          </div>
        </div>
      ),
      sortable: true,
    },
    {
      header: "Email",
      key: "email",
      render: (customer: typeof tableCustomers[number]) => (
        <span className="text-muted-foreground">{customer.email}</span>
      ),
      sortable: true,
    },
    {
      header: "Membership",
      key: "membershipId",
      render: (customer: typeof tableCustomers[number]) => (
        <span className="text-sm">
          {getMembershipNameById(
            memberships,
            customer.membershipId,
            "No membership",
          )}
        </span>
      ),
      sortable: true,
    },
    {
      header: "Status",
      key: "isActive",
      render: (customer: typeof tableCustomers[number]) => (
        <Badge tone={customer.isActive ? "success" : "neutral"}>
          {customer.isActive ? "Active" : "Inactive"}
        </Badge>
      ),
      sortable: true,
    },
    {
      header: "Actions",
      key: "actions",
      render: (customer: typeof tableCustomers[number]) => (
        <div className="flex items-center gap-2">
          {canOpenCustomer(customer) ? (
            <LinkButton href={`/customers/${customer.id}`} size="sm" variant="outline">
              Open
            </LinkButton>
          ) : null}
          <LinkButton
            href={`/enquiries/create?customerId=${customer.id}`}
            size="sm"
            variant="ghost"
          >
            Enquiry
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
              items={[{ href: "/", label: "Dashboard" }, { label: "Customers" }]}
            />
            <h1 className="mt-2 text-3xl font-bold tracking-tight">Customers</h1>
            <p className="mt-2 max-w-2xl text-base text-muted-foreground">
              Manage club members, review membership assignment, and start
              enquiries.
            </p>
          </div>
          {canCreateCustomer ? (
            <LinkButton href="/customers/register" variant="primary">
              <Plus className="size-4" />
              Add customer
            </LinkButton>
          ) : null}
        </header>

        <section className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          <Card className="flex items-center gap-4">
            <div className="rounded-full bg-accent/10 p-3 text-accent">
              <Users className="size-6" />
            </div>
            <div>
              <p className="text-sm text-muted-foreground">Total customers</p>
              <p className="text-2xl font-bold">{customers.length}</p>
            </div>
          </Card>
          <Card className="flex items-center gap-4">
            <div className="rounded-full bg-success/10 p-3 text-success">
              <Users className="size-6" />
            </div>
            <div>
              <p className="text-sm text-muted-foreground">Active</p>
              <p className="text-2xl font-bold">{activeCount}</p>
            </div>
          </Card>
          <Card className="flex items-center gap-4">
            <div className="rounded-full bg-muted p-3 text-muted-foreground">
              <Users className="size-6" />
            </div>
            <div>
              <p className="text-sm text-muted-foreground">Inactive</p>
              <p className="text-2xl font-bold">{inactiveCount}</p>
            </div>
          </Card>
          <Card className="flex items-center gap-4">
            <div className="rounded-full bg-warning/10 p-3 text-warning">
              <Users className="size-6" />
            </div>
            <div>
              <p className="text-sm text-muted-foreground">Memberships</p>
              <p className="text-2xl font-bold">{memberships.length}</p>
            </div>
          </Card>
        </section>

        {isLoadPending ? (
          <Skeleton className="h-96" />
        ) : isLoadError ? (
          <StatusMessage tone="error">
            {loadErrorMessage ?? "Unable to load customers."}
          </StatusMessage>
        ) : customers.length === 0 ? (
          <EmptyState
            action={
              <LinkButton href="/customers/register" variant="primary">
                Add your first customer
              </LinkButton>
            }
            description="Get started by registering a customer against a membership tier."
            icon={Users}
            title="No customers yet"
          />
        ) : (
          <Card className="flex flex-col gap-4">
            <div className="flex flex-col gap-4 lg:flex-row lg:items-end lg:justify-between">
              <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
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
                  <option value="none">No membership</option>
                  {memberships.map((membership) => (
                    <option key={membership.id} value={membership.id}>
                      {membership.name}
                    </option>
                  ))}
                </SelectField>
              </div>
              <div className="flex items-center gap-2">
                <button
                  aria-label="Table view"
                  className={cn(
                    "rounded-lg p-2 transition",
                    view === "table" ? "bg-accent text-white" : "bg-muted text-muted-foreground hover:bg-muted-foreground/20",
                  )}
                  onClick={() => setView("table")}
                  type="button"
                >
                  <TableIcon className="size-5" />
                </button>
                <button
                  aria-label="Card view"
                  className={cn(
                    "rounded-lg p-2 transition",
                    view === "cards" ? "bg-accent text-white" : "bg-muted text-muted-foreground hover:bg-muted-foreground/20",
                  )}
                  onClick={() => setView("cards")}
                  type="button"
                >
                  <Grid3X3 className="size-5" />
                </button>
              </div>
            </div>

            {view === "table" ? (
              <DataTable
                columns={tableColumns}
                data={tableCustomers}
                emptyState="No customers match these filters."
                keyExtractor={(customer) => customer.id}
                pageSize={10}
                searchFn={searchFn}
              />
            ) : (
              <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
                {filteredCustomers.length === 0 ? (
                  <p className="col-span-full text-center text-sm text-muted-foreground py-8">
                    No customers match these filters.
                  </p>
                ) : (
                  filteredCustomers.map((customer) => (
                    <div
                      key={customer.id}
                      className="rounded-xl border border-border bg-card p-5 shadow-sm transition hover:shadow-md"
                    >
                      <div className="flex items-start justify-between gap-4">
                        <div className="flex items-center gap-3 min-w-0">
                          <Avatar fallback={customer.name} size="md" />
                          <div className="min-w-0">
                            <h3 className="truncate font-semibold text-foreground">
                              {customer.name}
                            </h3>
                            <p className="truncate text-sm text-muted-foreground">
                              {customer.email}
                            </p>
                          </div>
                        </div>
                        <Badge tone={customer.isActive ? "success" : "neutral"}>
                          {customer.isActive ? "Active" : "Inactive"}
                        </Badge>
                      </div>
                      <p className="mt-4 text-sm text-muted-foreground">
                        {getMembershipNameById(
                          memberships,
                          customer.membershipId,
                          "No membership assigned",
                        )}
                      </p>
                      <div className="mt-4 flex items-center gap-2">
                        {canOpenCustomer(customer) ? (
                          <LinkButton href={`/customers/${customer.id}`} size="sm" variant="outline">
                            Open
                          </LinkButton>
                        ) : null}
                        <LinkButton
                          href={`/enquiries/create?customerId=${customer.id}`}
                          size="sm"
                          variant="ghost"
                        >
                          Enquiry
                        </LinkButton>
                      </div>
                    </div>
                  ))
                )}
              </div>
            )}
          </Card>
        )}
      </div>
    </main>
  );
};
