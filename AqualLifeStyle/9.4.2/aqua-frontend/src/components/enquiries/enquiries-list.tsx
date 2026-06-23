"use client";

import {
  Inbox,
  MessageSquare,
  Plus,
  Search,
} from "lucide-react";
import { useEffect, useMemo, useState } from "react";

import {
  useCustomersActions,
  useCustomersState,
  useEnquiriesActions,
  useEnquiriesState,
  useProductsActions,
  useProductsState,
} from "@/src/providers";
import {
  Badge,
  Breadcrumb,
  Card,
  EmptyState,
  LinkButton,
  SelectField,
  Skeleton,
  StatusMessage,
} from "@/src/shared/ui";

type EnquiryStatusFilter = "all" | "new" | "in-progress" | "resolved";
type Priority = "low" | "medium" | "high";

const statusLabelMap: Record<number, string> = {
  0: "New",
  1: "In progress",
  2: "Resolved",
};

const getPriority = (
  enquiry: { id: number; isSalesReady: boolean },
  isNew: boolean,
): Priority => {
  if (enquiry.isSalesReady && isNew) return "high";
  if (enquiry.isSalesReady) return "medium";
  if (Math.abs(enquiry.id) % 3 === 0) return "medium";
  if (Math.abs(enquiry.id) % 3 === 1) return "high";
  return "low";
};

const priorityBadgeTone = (priority: Priority): "info" | "warning" | "error" => {
  switch (priority) {
    case "low":
      return "info";
    case "medium":
      return "warning";
    case "high":
      return "error";
  }
};

const statusTone = (status: number): "success" | "warning" | "info" => {
  switch (status) {
    case 0:
      return "info";
    case 1:
      return "warning";
    case 2:
      return "success";
    default:
      return "info";
  }
};

const formatDate = (date: string) =>
  new Intl.DateTimeFormat("en-ZA", {
    dateStyle: "medium",
    timeStyle: "short",
  }).format(new Date(date));

const statusFilterMap: Record<EnquiryStatusFilter, number | null> = {
  all: null,
  new: 0,
  "in-progress": 1,
  resolved: 2,
};

export const EnquiriesList = () => {
  const [statusFilter, setStatusFilter] = useState<EnquiryStatusFilter>("all");
  const [priorityFilter, setPriorityFilter] = useState<string>("all");
  const [query, setQuery] = useState("");
  const { getEnquiries } = useEnquiriesActions();
  const { getCustomers } = useCustomersActions();
  const { getProducts } = useProductsActions();
  const {
    enquiries,
    isLoadError,
    isLoadPending,
    loadErrorMessage,
  } = useEnquiriesState();
  const { customers } = useCustomersState();
  const { products } = useProductsState();

  useEffect(() => {
    void getEnquiries();
    void getCustomers();
    void getProducts();
  }, [getEnquiries, getCustomers, getProducts]);

  const filteredEnquiries = useMemo(() => {
    return enquiries.filter((enquiry) => {
      const targetStatus = statusFilterMap[statusFilter];
      const matchesStatus = targetStatus === null || enquiry.status === targetStatus;
      const isNew = enquiry.status === 0;
      const priority = getPriority(enquiry, isNew);
      const matchesPriority =
        priorityFilter === "all" || priorityFilter === priority;
      const customer = customers.find((c) => c.id === enquiry.customerId);
      const product = products.find((p) => p.id === enquiry.productId);
      const matchesQuery =
        query.trim() === "" ||
        enquiry.message.toLowerCase().includes(query.toLowerCase()) ||
        (customer?.name ?? "").toLowerCase().includes(query.toLowerCase()) ||
        (product?.name ?? "").toLowerCase().includes(query.toLowerCase());

      return matchesStatus && matchesPriority && matchesQuery;
    });
  }, [customers, enquiries, priorityFilter, products, query, statusFilter]);

  const statusCounts = [0, 1, 2].map(
    (status) => enquiries.filter((e) => e.status === status).length,
  );

  return (
    <main className="min-h-dvh bg-muted/30 px-4 py-6 text-foreground sm:px-6 lg:px-8">
      <div className="mx-auto flex w-full max-w-7xl flex-col gap-6">
        <header className="flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
          <div>
            <Breadcrumb
              items={[
                { href: "/", label: "Dashboard" },
                { label: "Enquiries" },
              ]}
            />
            <h1 className="mt-2 text-3xl font-bold tracking-tight">Enquiries</h1>
            <p className="mt-2 max-w-2xl text-base text-muted-foreground">
              Manage inbound questions, track progress, and convert leads into
              orders.
            </p>
          </div>
          <LinkButton href="/enquiries/create" variant="primary">
            <Plus className="size-4" />
            New enquiry
          </LinkButton>
        </header>

        <section className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
          <Card className="flex items-center gap-4">
            <div className="rounded-full bg-accent/10 p-3 text-accent">
              <Inbox className="size-6" />
            </div>
            <div>
              <p className="text-sm text-muted-foreground">Total enquiries</p>
              <p className="text-2xl font-bold">{enquiries.length}</p>
            </div>
          </Card>
          <Card className="flex items-center gap-4">
            <div className="rounded-full bg-info/10 p-3 text-info">
              <MessageSquare className="size-6" />
            </div>
            <div>
              <p className="text-sm text-muted-foreground">New</p>
              <p className="text-2xl font-bold">{statusCounts[0]}</p>
            </div>
          </Card>
          <Card className="flex items-center gap-4">
            <div className="rounded-full bg-warning/10 p-3 text-warning">
              <MessageSquare className="size-6" />
            </div>
            <div>
              <p className="text-sm text-muted-foreground">In progress</p>
              <p className="text-2xl font-bold">{statusCounts[1]}</p>
            </div>
          </Card>
          <Card className="flex items-center gap-4">
            <div className="rounded-full bg-success/10 p-3 text-success">
              <MessageSquare className="size-6" />
            </div>
            <div>
              <p className="text-sm text-muted-foreground">Resolved</p>
              <p className="text-2xl font-bold">{statusCounts[2]}</p>
            </div>
          </Card>
        </section>

        {isLoadPending ? (
          <Skeleton className="h-96" />
        ) : isLoadError ? (
          <StatusMessage tone="error">
            {loadErrorMessage ?? "Unable to load enquiries."}
          </StatusMessage>
        ) : enquiries.length === 0 ? (
          <EmptyState
            action={
              <LinkButton href="/enquiries/create" variant="primary">
                Create the first enquiry
              </LinkButton>
            }
            description="Capture customer questions and route them to the right team."
            icon={MessageSquare}
            title="No enquiries yet"
          />
        ) : (
          <Card className="flex flex-col gap-4">
            <div className="flex flex-col gap-4 lg:flex-row lg:items-end lg:justify-between">
              <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
                <SelectField
                  label="Status"
                  name="statusFilter"
                  onChange={(event) =>
                    setStatusFilter(event.target.value as EnquiryStatusFilter)
                  }
                  value={statusFilter}
                >
                  <option value="all">All statuses</option>
                  <option value="new">New</option>
                  <option value="in-progress">In progress</option>
                  <option value="resolved">Resolved</option>
                </SelectField>
                <SelectField
                  label="Priority"
                  name="priorityFilter"
                  onChange={(event) => setPriorityFilter(event.target.value)}
                  value={priorityFilter}
                >
                  <option value="all">All priorities</option>
                  <option value="high">High</option>
                  <option value="medium">Medium</option>
                  <option value="low">Low</option>
                </SelectField>
                <div className="relative">
                  <Search className="absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
                  <input
                    className="w-full rounded-lg border border-border bg-muted py-2 pl-9 pr-3 text-sm text-foreground outline-none transition focus:border-accent focus:ring-2 focus:ring-accent/20"
                    onChange={(event) => setQuery(event.target.value)}
                    placeholder="Search customer or message..."
                    type="search"
                    value={query}
                  />
                </div>
              </div>
            </div>

            <div className="divide-y divide-border rounded-xl border border-border">
              {filteredEnquiries.length === 0 ? (
                <p className="py-8 text-center text-sm text-muted-foreground">
                  No enquiries match these filters.
                </p>
              ) : (
                filteredEnquiries.map((enquiry) => {
                  const isNew = enquiry.status === 0;
                  const priority = getPriority(enquiry, isNew);
                  const customer = customers.find((c) => c.id === enquiry.customerId);
                  const product = products.find((p) => p.id === enquiry.productId);

                  return (
                    <a
                      className="group flex flex-col gap-3 rounded-xl p-4 transition hover:bg-muted sm:flex-row sm:items-start sm:justify-between"
                      href={`/enquiries/${enquiry.id}`}
                      key={enquiry.id}
                    >
                      <div className="flex items-start gap-3 min-w-0">
                        <div className="rounded-full bg-accent/10 p-2.5 text-accent">
                          <MessageSquare className="size-4" />
                        </div>
                        <div className="min-w-0 flex-1">
                          <div className="flex flex-wrap items-center gap-2">
                            <span className="font-semibold text-foreground">
                              {customer?.name ?? `Customer ${enquiry.customerId}`}
                            </span>
                            <Badge tone={priorityBadgeTone(priority)}>
                              {priority}
                            </Badge>
                            {enquiry.isSalesReady ? (
                              <Badge tone="accent">sales ready</Badge>
                            ) : null}
                          </div>
                          <p className="mt-1 truncate text-sm text-muted-foreground">
                            {product?.name ?? `Product ${enquiry.productId}`} · {enquiry.message}
                          </p>
                          <p className="mt-1 text-xs text-muted-foreground">
                            {formatDate(enquiry.createdAt)}
                          </p>
                        </div>
                      </div>
                      <Badge tone={statusTone(enquiry.status)}>
                        {statusLabelMap[enquiry.status]}
                      </Badge>
                    </a>
                  );
                })
              )}
            </div>
          </Card>
        )}
      </div>
    </main>
  );
};
