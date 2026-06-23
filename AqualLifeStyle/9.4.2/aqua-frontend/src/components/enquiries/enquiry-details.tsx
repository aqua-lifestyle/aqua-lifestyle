"use client";

import { FormEvent, useEffect, useMemo, useState } from "react";

import {
  useCustomersActions,
  useCustomersState,
  useEnquiriesActions,
  useEnquiriesState,
  useMembershipsActions,
  useMembershipsState,
  useProductsActions,
  useProductsState,
  useToast,
} from "@/src/providers";
import { getMembershipNameById } from "@/src/shared/domain";
import {
  Avatar,
  Badge,
  Breadcrumb,
  Button,
  Card,
  LinkButton,
  SelectField,
  Skeleton,
  StatusMessage,
  TextAreaField,
} from "@/src/shared/ui";

type EnquiryDetailsProps = {
  enquiryId: number;
};

type Priority = "low" | "medium" | "high";

const statusOptions = [
  { label: "New", value: 0 },
  { label: "In progress", value: 1 },
  { label: "Resolved", value: 2 },
];

const statusTone = (status: number): "info" | "warning" | "success" => {
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

const statusLabel = (status: number) => statusOptions[status]?.label ?? "Unknown";

const getPriority = (enquiry: { id: number; isSalesReady: boolean; status: number }): Priority => {
  const isNew = enquiry.status === 0;
  if (enquiry.isSalesReady && isNew) return "high";
  if (enquiry.isSalesReady) return "medium";
  if (Math.abs(enquiry.id) % 3 === 0) return "medium";
  if (Math.abs(enquiry.id) % 3 === 1) return "high";
  return "low";
};

const priorityTone = (priority: Priority): "info" | "warning" | "error" => {
  switch (priority) {
    case "low":
      return "info";
    case "medium":
      return "warning";
    case "high":
      return "error";
  }
};

const formatDate = (date: string) =>
  new Intl.DateTimeFormat("en-ZA", {
    dateStyle: "medium",
    timeStyle: "short",
  }).format(new Date(date));

const ConversationThread = ({
  enquiry,
  customerName,
}: {
  customerName: string;
  enquiry: { createdAt: string; message: string; response: string | null };
}) => {
  return (
    <div className="flex flex-col gap-4">
      <div className="flex gap-3">
        <Avatar fallback={customerName} size="sm" />
        <div className="max-w-3xl rounded-2xl rounded-tl-none bg-muted px-4 py-3 text-sm text-foreground">
          <p className="font-semibold">{customerName}</p>
          <p className="mt-1">{enquiry.message}</p>
          <p className="mt-2 text-xs text-muted-foreground">{formatDate(enquiry.createdAt)}</p>
        </div>
      </div>

      {enquiry.response ? (
        <div className="flex flex-row-reverse gap-3">
          <div className="flex size-8 shrink-0 items-center justify-center rounded-full bg-accent text-xs font-bold text-white">
            A
          </div>
          <div className="max-w-3xl rounded-2xl rounded-tr-none bg-accent px-4 py-3 text-sm text-white">
            <p className="font-semibold">Agent</p>
            <p className="mt-1">{enquiry.response}</p>
          </div>
        </div>
      ) : null}
    </div>
  );
};

const CustomerSidebar = ({
  customer,
  productName,
  membershipName,
}: {
  customer: { email: string; isActive: boolean; name: string } | undefined;
  membershipName: string;
  productName: string;
}) => {
  if (!customer) {
    return (
      <Card>
        <p className="text-sm text-muted-foreground">Customer details not available.</p>
      </Card>
    );
  }

  return (
    <Card>
      <h3 className="text-lg font-semibold">Customer</h3>
      <div className="mt-4 flex items-center gap-3">
        <Avatar fallback={customer.name} size="md" />
        <div>
          <p className="font-semibold text-foreground">{customer.name}</p>
          <p className="text-sm text-muted-foreground">{customer.email}</p>
        </div>
      </div>
      <dl className="mt-6 space-y-3 text-sm">
        <div className="flex justify-between gap-4">
          <dt className="text-muted-foreground">Status</dt>
          <dd>
            <Badge tone={customer.isActive ? "success" : "neutral"}>
              {customer.isActive ? "Active" : "Inactive"}
            </Badge>
          </dd>
        </div>
        <div className="flex justify-between gap-4">
          <dt className="text-muted-foreground">Membership</dt>
          <dd className="font-medium">{membershipName}</dd>
        </div>
        <div className="flex justify-between gap-4">
          <dt className="text-muted-foreground">Product</dt>
          <dd className="font-medium">{productName}</dd>
        </div>
      </dl>
    </Card>
  );
};

type ResponseFormProps = {
  enquiry: {
    id: number;
    isClosed: boolean;
    response: string | null;
    status: number;
  };
  isActionError: boolean;
  isActionPending: boolean;
  actionErrorMessage: string | null;
  onSubmit: (status: number, response: string) => Promise<void>;
};

const ResponseForm = ({
  enquiry,
  isActionError,
  isActionPending,
  actionErrorMessage,
  onSubmit,
}: ResponseFormProps) => {
  const [responseText, setResponseText] = useState(enquiry.response ?? "");
  const [status, setStatus] = useState<string>(String(enquiry.status));

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    await onSubmit(Number(status), responseText.trim());
  };

  return (
    <Card>
      <h3 className="text-lg font-semibold">Respond & update</h3>
      <form className="mt-4 flex flex-col gap-4" onSubmit={handleSubmit}>
        <SelectField
          label="Status"
          name="status"
          onChange={(event) => setStatus(event.target.value)}
          value={status}
        >
          {statusOptions.map((option) => (
            <option key={option.value} value={option.value}>
              {option.label}
            </option>
          ))}
        </SelectField>
        <TextAreaField
          label="Response message"
          name="responseMessage"
          onChange={(event) => setResponseText(event.target.value)}
          placeholder="Enter a reply to the customer..."
          rows={4}
          value={responseText}
        />
        {isActionError ? (
          <StatusMessage tone="error">
            {actionErrorMessage ?? "Unable to update this enquiry."}
          </StatusMessage>
        ) : null}
        <div className="flex justify-end">
          <Button disabled={isActionPending} isLoading={isActionPending} type="submit">
            Save response
          </Button>
        </div>
      </form>
    </Card>
  );
};

export const EnquiryDetails = ({ enquiryId }: EnquiryDetailsProps) => {
  const { getEnquiry, respondToEnquiry, closeEnquiry, reopenEnquiry } = useEnquiriesActions();
  const { getCustomers } = useCustomersActions();
  const { getProducts } = useProductsActions();
  const { toast } = useToast();
  const {
    actionErrorMessage,
    isActionError,
    isActionPending,
    isActionSuccess,
    isSelectedError,
    isSelectedPending,
    selectedEnquiry,
    selectedErrorMessage,
  } = useEnquiriesState();
  const { customers } = useCustomersState();
  const { products } = useProductsState();
  const { getMemberships } = useMembershipsActions();
  const { memberships } = useMembershipsState();

  useEffect(() => {
    if (!Number.isInteger(enquiryId) || enquiryId <= 0) return;
    void getEnquiry(enquiryId);
    void getCustomers();
    void getProducts();
    void getMemberships();
  }, [enquiryId, getEnquiry, getCustomers, getProducts, getMemberships]);

  useEffect(() => {
    if (isActionSuccess) {
      toast({
        message: "Enquiry updated successfully.",
        title: "Success",
        type: "success",
      });
    }
  }, [isActionSuccess, toast]);

  const isInvalid = !Number.isInteger(enquiryId) || enquiryId <= 0;

  const customer = useMemo(
    () => customers.find((c) => c.id === selectedEnquiry?.customerId),
    [customers, selectedEnquiry],
  );
  const product = useMemo(
    () => products.find((p) => p.id === selectedEnquiry?.productId),
    [products, selectedEnquiry],
  );

  const handleSubmit = async (newStatus: number, response: string) => {
    if (!selectedEnquiry) return;

    await respondToEnquiry(selectedEnquiry.id, { response });

    if (newStatus === 2 && !selectedEnquiry.isClosed) {
      await closeEnquiry(selectedEnquiry.id);
    } else if (newStatus !== 2 && selectedEnquiry.isClosed) {
      await reopenEnquiry(selectedEnquiry.id);
    }
  };

  return (
    <main className="min-h-dvh bg-muted/30 px-4 py-6 text-foreground sm:px-6 lg:px-8">
      <div className="mx-auto flex w-full max-w-6xl flex-col gap-6">
        <header className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
          <div>
            <Breadcrumb
              items={[
                { href: "/", label: "Dashboard" },
                { href: "/enquiries", label: "Enquiries" },
                { label: "Enquiry details" },
              ]}
            />
            <h1 className="mt-2 text-3xl font-bold tracking-tight">Enquiry details</h1>
            <p className="mt-2 max-w-2xl text-base text-muted-foreground">
              Review the conversation, update status, and respond to the customer.
            </p>
          </div>
          <LinkButton href="/enquiries" variant="outline">
            Back to enquiries
          </LinkButton>
        </header>

        {isInvalid ? (
          <StatusMessage tone="error">This enquiry id is invalid.</StatusMessage>
        ) : null}
        {isSelectedPending ? (
          <Skeleton className="h-96" />
        ) : null}
        {isSelectedError ? (
          <StatusMessage tone="error">
            {selectedErrorMessage ?? "Unable to load this enquiry."}
          </StatusMessage>
        ) : null}

        {selectedEnquiry ? (
          <div className="grid gap-6 lg:grid-cols-[1fr_22rem]">
            <div className="flex flex-col gap-6">
              <Card>
                <div className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
                  <div>
                    <div className="flex flex-wrap items-center gap-2">
                      <h2 className="text-lg font-semibold">
                        Enquiry #{selectedEnquiry.id}
                      </h2>
                      <Badge tone={statusTone(selectedEnquiry.status)}>
                        {statusLabel(selectedEnquiry.status)}
                      </Badge>
                      <Badge tone={priorityTone(getPriority(selectedEnquiry))}>
                        {getPriority(selectedEnquiry)} priority
                      </Badge>
                      {selectedEnquiry.isSalesReady ? (
                        <Badge tone="accent">sales ready</Badge>
                      ) : null}
                    </div>
                    <p className="text-sm text-muted-foreground">
                      {product?.name ?? `Product ${selectedEnquiry.productId}`} ·{" "}
                      {formatDate(selectedEnquiry.createdAt)}
                    </p>
                  </div>
                  {selectedEnquiry.isConverted ? (
                    <Badge tone="success">Converted</Badge>
                  ) : null}
                </div>

                <div className="mt-6">
                  <ConversationThread
                    customerName={customer?.name ?? `Customer ${selectedEnquiry.customerId}`}
                    enquiry={selectedEnquiry}
                  />
                </div>
              </Card>

              <ResponseForm
                actionErrorMessage={actionErrorMessage}
                enquiry={selectedEnquiry}
                isActionError={isActionError}
                isActionPending={isActionPending}
                key={`${selectedEnquiry.id}-${selectedEnquiry.status}-${selectedEnquiry.response ?? ""}`}
                onSubmit={handleSubmit}
              />
            </div>

            <aside className="flex flex-col gap-6">
              <CustomerSidebar
                customer={customer}
                membershipName={getMembershipNameById(
                  memberships,
                  customer?.membershipId ?? null,
                  "No membership",
                )}
                productName={product?.name ?? `Product ${selectedEnquiry.productId}`}
              />
            </aside>
          </div>
        ) : null}
      </div>
    </main>
  );
};
