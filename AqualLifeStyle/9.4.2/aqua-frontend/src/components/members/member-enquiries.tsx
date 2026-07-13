"use client";

import { MessageSquare } from "lucide-react";
import { useEffect, useMemo, useState } from "react";

import {
  useAuthState,
  useCustomersActions,
  useCustomersState,
  useEnquiriesActions,
  useEnquiriesState,
} from "@/src/providers";
import {
  Avatar,
  Badge,
  Breadcrumb,
  Card,
  DataTable,
  EmptyState,
  SelectField,
  Skeleton,
  StatusMessage,
} from "@/src/shared/ui";

type EnquiryStatusFilter = "all" | "0" | "1" | "2";

const statusLabel = (value: number) => {
  const labels = ["New", "In progress", "Resolved"];
  return labels[value] ?? `Status ${value}`;
};

const statusTone = (value: number): "success" | "warning" | "info" => {
  switch (value) {
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

export const MemberEnquiries = () => {
  const [statusFilter, setStatusFilter] = useState<EnquiryStatusFilter>("all");
  const { session } = useAuthState();
  const { getEnquiries } = useEnquiriesActions();
  const { getCustomers } = useCustomersActions();
  const { enquiries, isLoadError, isLoadPending, loadErrorMessage } = useEnquiriesState();
  const { customers } = useCustomersState();

  // ALL hooks before early returns
  useEffect(() => {
    void getEnquiries();
    void getCustomers();
  }, [getEnquiries, getCustomers]);

  const currentUserId = session?.user?.id ?? null;

  const myEnquiries = useMemo(() => {
    if (!currentUserId) return [];
    return enquiries.filter((enquiry) => enquiry.customerId === currentUserId);
  }, [enquiries, currentUserId]);

  const filteredEnquiries = useMemo(() => {
    return myEnquiries.filter((enquiry) => {
      const matchesStatus = statusFilter === "all" || enquiry.status === Number(statusFilter);
      return matchesStatus;
    });
  }, [myEnquiries, statusFilter]);

  const tableColumns = [
    {
      header: "Enquiry",
      key: "id",
      render: (enquiry: typeof filteredEnquiries[number]) => {
        const customer = customers.find((c) => c.id === enquiry.customerId);
        return (
          <div className="flex items-center gap-3">
            <Avatar fallback={`E ${enquiry.id}`} size="sm" />
            <div>
              <p className="font-semibold text-foreground">Enquiry #{enquiry.id}</p>
              <p className="text-xs text-muted-foreground">
                {customer?.name ?? `Customer ${enquiry.customerId}`}
              </p>
            </div>
          </div>
        );
      },
      sortable: true,
    },
    {
      header: "Status",
      key: "status",
      render: (enquiry: typeof filteredEnquiries[number]) => (
        <Badge tone={statusTone(enquiry.status)}>
          {statusLabel(enquiry.status)}
        </Badge>
      ),
      sortable: true,
    },
    {
      header: "Message",
      key: "message",
      render: (enquiry: typeof filteredEnquiries[number]) => (
        <span className="text-sm line-clamp-1">{enquiry.message}</span>
      ),
      sortable: false,
    },
    {
      header: "Created",
      key: "createdAt",
      render: (enquiry: typeof filteredEnquiries[number]) => (
        <span className="text-sm">
          {new Date(enquiry.createdAt).toLocaleDateString()}
        </span>
      ),
      sortable: true,
    },
  ];

  return (
    <main className="min-h-dvh bg-muted/30 px-4 py-6 text-foreground sm:px-6 lg:px-8">
      <div className="mx-auto flex w-full max-w-7xl flex-col gap-6">
        <header>
          <Breadcrumb
            items={[
              { href: "/", label: "Dashboard" },
              { href: "/member", label: "Member" },
              { label: "My enquiries" },
            ]}
          />
          <h1 className="mt-2 text-3xl font-bold tracking-tight">My enquiries</h1>
          <p className="mt-2 max-w-2xl text-base text-muted-foreground">
            View and track your enquiries.
          </p>
        </header>

        {isLoadPending ? (
          <Skeleton className="h-96" />
        ) : isLoadError ? (
          <StatusMessage tone="error">
            {loadErrorMessage ?? "Unable to load enquiries."}
          </StatusMessage>
        ) : filteredEnquiries.length === 0 ? (
          <EmptyState
            description="You have no enquiries yet."
            icon={MessageSquare}
            title="No enquiries"
          />
        ) : (
          <Card className="flex flex-col gap-4">
            <div className="flex flex-col gap-4 lg:flex-row lg:items-end lg:justify-between">
              <SelectField
                label="Status"
                name="statusFilter"
                onChange={(event) => setStatusFilter(event.target.value as EnquiryStatusFilter)}
                value={statusFilter}
              >
                <option value="all">All statuses</option>
                <option value="0">New</option>
                <option value="1">In progress</option>
                <option value="2">Resolved</option>
              </SelectField>
            </div>

            <DataTable
              columns={tableColumns}
              data={filteredEnquiries}
              emptyState="No enquiries match these filters."
              keyExtractor={(enquiry) => enquiry.id}
              pageSize={10}
              searchFn={(enquiry, query) =>
                `Enquiry #${enquiry.id} ${enquiry.message}`
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
