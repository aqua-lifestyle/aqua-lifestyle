"use client";

import { useEffect, useState } from "react";

import {
  type Enquiry,
  useCustomersActions,
  useCustomersState,
  useEnquiriesActions,
  useEnquiriesState,
  useProductsActions,
  useProductsState,
} from "@/src/providers";
import {
  Badge,
  Card,
  LinkButton,
  SelectField,
  StatusMessage,
} from "@/src/shared/ui";
import { EnquiryCard } from "./enquiry-card";

type PipelineFilter = "all" | "pending" | "sales-ready" | "converted" | "closed";

const pipelineFilterLabels: Record<PipelineFilter, string> = {
  all: "All enquiries",
  closed: "Closed",
  converted: "Converted",
  pending: "Pending",
  "sales-ready": "Sales ready",
};

const getCustomerName = (
  customerId: number,
  customers: { id: number; name: string }[],
) => {
  return (
    customers.find((customer) => customer.id === customerId)?.name ??
    `Customer ${customerId}`
  );
};

const getProductName = (
  productId: number,
  products: { id: number; name: string }[],
) => {
  return (
    products.find((product) => product.id === productId)?.name ??
    `Product ${productId}`
  );
};

const matchesPipelineFilter = (
  enquiry: Enquiry,
  pipelineFilter: PipelineFilter,
) => {
  switch (pipelineFilter) {
    case "closed":
      return enquiry.isClosed;
    case "converted":
      return enquiry.isConverted;
    case "pending":
      return enquiry.isPending;
    case "sales-ready":
      return enquiry.isSalesReady;
    default:
      return true;
  }
};

export const EnquiriesList = () => {
  const [pipelineFilter, setPipelineFilter] =
    useState<PipelineFilter>("all");
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

  const filteredEnquiries = enquiries.filter((enquiry) =>
    matchesPipelineFilter(enquiry, pipelineFilter),
  );
  const pendingCount = enquiries.filter((enquiry) => enquiry.isPending).length;
  const salesReadyCount = enquiries.filter(
    (enquiry) => enquiry.isSalesReady,
  ).length;
  const convertedCount = enquiries.filter(
    (enquiry) => enquiry.isConverted,
  ).length;
  const closedCount = enquiries.filter((enquiry) => enquiry.isClosed).length;

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
            <LinkButton href="/enquiries/sales-ready">
              Sales ready
            </LinkButton>
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
          <section className="grid gap-4 lg:grid-cols-[1fr_18rem]">
            <Card>
              <div className="grid gap-4 sm:grid-cols-4">
                <div>
                  <p className="text-sm text-zinc-600">Pending</p>
                  <p className="mt-2 text-2xl font-semibold">{pendingCount}</p>
                </div>
                <div>
                  <p className="text-sm text-zinc-600">Sales ready</p>
                  <p className="mt-2 text-2xl font-semibold">
                    {salesReadyCount}
                  </p>
                </div>
                <div>
                  <p className="text-sm text-zinc-600">Converted</p>
                  <p className="mt-2 text-2xl font-semibold">
                    {convertedCount}
                  </p>
                </div>
                <div>
                  <p className="text-sm text-zinc-600">Closed</p>
                  <p className="mt-2 text-2xl font-semibold">{closedCount}</p>
                </div>
              </div>
            </Card>

            <Card>
              <SelectField
                label="Pipeline view"
                name="pipelineFilter"
                onChange={(event) =>
                  setPipelineFilter(event.target.value as PipelineFilter)
                }
                value={pipelineFilter}
              >
                {Object.entries(pipelineFilterLabels).map(([value, label]) => (
                  <option key={value} value={value}>
                    {label}
                  </option>
                ))}
              </SelectField>
              <div className="mt-4">
                <Badge>
                  Showing {filteredEnquiries.length} of {enquiries.length}
                </Badge>
              </div>
            </Card>
          </section>
        ) : null}

        {enquiries.length > 0 && filteredEnquiries.length === 0 ? (
          <StatusMessage>
            No enquiries match this pipeline view yet.
          </StatusMessage>
        ) : null}

        {filteredEnquiries.length > 0 ? (
          <section className="grid gap-4 lg:grid-cols-2">
            {filteredEnquiries.map((enquiry) => (
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
