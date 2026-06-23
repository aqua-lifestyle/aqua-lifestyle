"use client";

import { MessageSquare } from "lucide-react";
import { useEffect } from "react";

import {
  useCustomersActions,
  useCustomersState,
  useEnquiriesActions,
  useEnquiriesState,
  useProductsActions,
  useProductsState,
} from "@/src/providers";
import {
  Breadcrumb,
  EmptyState,
  LinkButton,
  Skeleton,
  StatusMessage,
} from "@/src/shared/ui";
import { EnquiryCard } from "./enquiry-card";

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

export const SalesReadyEnquiries = () => {
  const { getCustomers } = useCustomersActions();
  const { getSalesReadyEnquiries } = useEnquiriesActions();
  const { getProducts } = useProductsActions();
  const { customers } = useCustomersState();
  const {
    isSalesReadyError,
    isSalesReadyPending,
    salesReadyEnquiries,
    salesReadyErrorMessage,
  } = useEnquiriesState();
  const { products } = useProductsState();

  useEffect(() => {
    void getCustomers();
    void getProducts();
    void getSalesReadyEnquiries();
  }, [getCustomers, getProducts, getSalesReadyEnquiries]);

  return (
    <main className="min-h-dvh bg-muted/30 px-4 py-6 text-foreground sm:px-6 lg:px-8">
      <div className="mx-auto flex w-full max-w-6xl flex-col gap-6">
        <header className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
          <div>
            <Breadcrumb
              items={[
                { href: "/", label: "Dashboard" },
                { href: "/enquiries", label: "Enquiries" },
                { label: "Sales ready" },
              ]}
            />
            <h1 className="mt-2 text-3xl font-bold tracking-tight">
              Sales-ready enquiries
            </h1>
            <p className="mt-2 max-w-2xl text-base text-muted-foreground">
              Backend-qualified enquiries with enough follow-up engagement to
              justify focused sales action.
            </p>
          </div>
          <div className="flex flex-col gap-3 sm:flex-row">
            <LinkButton href="/enquiries" variant="outline">
              All enquiries
            </LinkButton>
            <LinkButton href="/enquiries/create" variant="primary">
              Create enquiry
            </LinkButton>
          </div>
        </header>

        {isSalesReadyPending ? (
          <Skeleton className="h-96" />
        ) : null}

        {isSalesReadyError ? (
          <StatusMessage tone="error">
            {salesReadyErrorMessage ??
              "Unable to load sales-ready enquiries."}
          </StatusMessage>
        ) : null}

        {!isSalesReadyPending &&
        !isSalesReadyError &&
        salesReadyEnquiries.length === 0 ? (
          <EmptyState
            action={
              <LinkButton href="/enquiries" variant="primary">
                Open pipeline
              </LinkButton>
            }
            description="No enquiries are sales-ready yet. Record follow-ups after responding to an enquiry."
            icon={MessageSquare}
            title="No sales-ready enquiries"
          />
        ) : null}

        {salesReadyEnquiries.length > 0 ? (
          <section className="grid gap-4 lg:grid-cols-2">
            {salesReadyEnquiries.map((enquiry) => (
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
