"use client";

import { useEffect } from "react";

import {
  type Enquiry,
  type EnquiryStatus,
  useCustomersActions,
  useCustomersState,
  useEnquiriesActions,
  useEnquiriesState,
  useProductsActions,
  useProductsState,
} from "@/src/providers";
import { Badge, Card, LinkButton, StatusMessage } from "@/src/shared/ui";

const enquiryStatusLabels: Record<EnquiryStatus, string> = {
  0: "Pending",
  1: "Responded",
  2: "Closed",
};

const getCustomerName = (customerId: number, customers: { id: number; name: string }[]) => {
  return (
    customers.find((customer) => customer.id === customerId)?.name ??
    `Customer ${customerId}`
  );
};

const getProductName = (productId: number, products: { id: number; name: string }[]) => {
  return (
    products.find((product) => product.id === productId)?.name ??
    `Product ${productId}`
  );
};

const formatDate = (date: string) => {
  return new Intl.DateTimeFormat("en-ZA", {
    dateStyle: "medium",
    timeStyle: "short",
  }).format(new Date(date));
};

const EnquiryCard = ({
  customerName,
  enquiry,
  productName,
}: {
  customerName: string;
  enquiry: Enquiry;
  productName: string;
}) => {
  return (
    <Card>
      <div className="flex items-start justify-between gap-4">
        <div className="min-w-0">
          <h2 className="truncate text-lg font-semibold text-zinc-950">
            {customerName}
          </h2>
          <p className="mt-1 text-sm text-zinc-600">{productName}</p>
        </div>
        <Badge tone={enquiry.isClosed ? "neutral" : "success"}>
          {enquiryStatusLabels[enquiry.status]}
        </Badge>
      </div>

      <p className="mt-5 line-clamp-3 text-sm leading-6 text-zinc-700">
        {enquiry.message}
      </p>

      <dl className="mt-6 grid gap-3 text-sm">
        <div className="flex justify-between gap-4">
          <dt className="text-zinc-600">Created</dt>
          <dd className="font-medium text-zinc-950">
            {formatDate(enquiry.createdAt)}
          </dd>
        </div>
        <div className="flex justify-between gap-4">
          <dt className="text-zinc-600">Follow-ups</dt>
          <dd className="font-medium text-zinc-950">{enquiry.followUpCount}</dd>
        </div>
        <div className="flex justify-between gap-4">
          <dt className="text-zinc-600">Sales ready</dt>
          <dd className="font-medium text-zinc-950">
            {enquiry.isSalesReady ? "Yes" : "No"}
          </dd>
        </div>
      </dl>
    </Card>
  );
};

export const EnquiriesList = () => {
  const { getCustomers } = useCustomersActions();
  const { getEnquiries } = useEnquiriesActions();
  const { getProducts } = useProductsActions();
  const { customers } = useCustomersState();
  const {
    enquiries,
    isLoadError,
    isLoadPending,
    loadErrorMessage,
  } = useEnquiriesState();
  const { products } = useProductsState();

  useEffect(() => {
    void getCustomers();
    void getProducts();
    void getEnquiries();
  }, [getCustomers, getEnquiries, getProducts]);

  return (
    <main className="min-h-dvh bg-zinc-50 px-6 py-8 text-zinc-950 sm:px-8 lg:px-12">
      <div className="mx-auto flex w-full max-w-6xl flex-col gap-8">
        <header className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
          <div className="flex flex-col gap-2">
            <p className="text-sm font-medium uppercase tracking-wide text-emerald-700">
              Aqua Lifestyle Club
            </p>
            <h1 className="text-3xl font-semibold tracking-tight">Enquiries</h1>
            <p className="max-w-2xl text-base text-zinc-600">
              Enquiries loaded from the ABP backend. Email delivery will be
              added behind the backend workflow later.
            </p>
          </div>
          <div className="flex flex-col gap-3 sm:flex-row">
            <LinkButton href="/customers">View customers</LinkButton>
            <LinkButton href="/products">View products</LinkButton>
            <LinkButton href="/enquiries/create" variant="primary">
              Create enquiry
            </LinkButton>
          </div>
        </header>

        {isLoadPending ? (
          <StatusMessage>Loading enquiries...</StatusMessage>
        ) : null}

        {isLoadError ? (
          <StatusMessage tone="error">
            {loadErrorMessage ?? "Unable to load enquiries."}
          </StatusMessage>
        ) : null}

        {!isLoadPending && !isLoadError && enquiries.length === 0 ? (
          <StatusMessage>No enquiries are available yet.</StatusMessage>
        ) : null}

        {enquiries.length > 0 ? (
          <section className="grid gap-4 lg:grid-cols-2">
            {enquiries.map((enquiry) => (
              <EnquiryCard
                customerName={getCustomerName(enquiry.customerId, customers)}
                enquiry={enquiry}
                key={enquiry.id}
                productName={getProductName(enquiry.productId, products)}
              />
            ))}
          </section>
        ) : null}
      </div>
    </main>
  );
};
