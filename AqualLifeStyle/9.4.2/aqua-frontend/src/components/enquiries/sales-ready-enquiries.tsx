"use client";

import { useEffect } from "react";

import {
  useCustomersActions,
  useCustomersState,
  useEnquiriesActions,
  useEnquiriesState,
  useProductsActions,
  useProductsState,
} from "@/src/providers";
import { LinkButton, StatusMessage } from "@/src/shared/ui";
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
    <main className="min-h-dvh bg-zinc-50 px-6 py-8 text-zinc-950 sm:px-8 lg:px-12">
      <div className="mx-auto flex w-full max-w-6xl flex-col gap-8">
        <header className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
          <div className="flex flex-col gap-2">
            <p className="text-sm font-medium uppercase tracking-wide text-emerald-700">
              Aqua Lifestyle Club
            </p>
            <h1 className="text-3xl font-semibold tracking-tight">
              Sales-ready enquiries
            </h1>
            <p className="max-w-2xl text-base text-zinc-600">
              Backend-qualified enquiries with enough follow-up engagement to
              justify focused sales action.
            </p>
          </div>
          <div className="flex flex-col gap-3 sm:flex-row">
            <LinkButton href="/enquiries">All enquiries</LinkButton>
            <LinkButton href="/enquiries/create" variant="primary">
              Create enquiry
            </LinkButton>
          </div>
        </header>

        {isSalesReadyPending ? (
          <StatusMessage>Loading sales-ready enquiries...</StatusMessage>
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
          <StatusMessage>
            No enquiries are sales-ready yet. Record follow-ups with Interested
            or Considering outcomes after responding to an enquiry.
          </StatusMessage>
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
