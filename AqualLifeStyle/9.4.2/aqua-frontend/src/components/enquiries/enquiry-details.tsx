"use client";

import { FormEvent, useEffect, useMemo, useState } from "react";

import {
  type EnquiryStatus,
  useCustomersActions,
  useCustomersState,
  useEnquiriesActions,
  useEnquiriesState,
  useProductsActions,
  useProductsState,
} from "@/src/providers";
import {
  Badge,
  Button,
  Card,
  LinkButton,
  StatusMessage,
  TextAreaField,
} from "@/src/shared/ui";

const enquiryStatusLabels: Record<EnquiryStatus, string> = {
  0: "Pending",
  1: "Responded",
  2: "Closed",
};

const formatDate = (date: string) => {
  return new Intl.DateTimeFormat("en-ZA", {
    dateStyle: "medium",
    timeStyle: "short",
  }).format(new Date(date));
};

type EnquiryDetailsProps = {
  enquiryId: number;
};

export const EnquiryDetails = ({ enquiryId }: EnquiryDetailsProps) => {
  const [response, setResponse] = useState("");
  const { getCustomers } = useCustomersActions();
  const { getProducts } = useProductsActions();
  const {
    closeEnquiry,
    getEnquiry,
    reopenEnquiry,
    respondToEnquiry,
  } = useEnquiriesActions();
  const { customers } = useCustomersState();
  const { products } = useProductsState();
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

  useEffect(() => {
    if (!Number.isInteger(enquiryId) || enquiryId <= 0) {
      return;
    }

    void getCustomers();
    void getProducts();
    void getEnquiry(enquiryId);
  }, [enquiryId, getCustomers, getEnquiry, getProducts]);

  const customerName = useMemo(() => {
    if (!selectedEnquiry) {
      return null;
    }

    return (
      customers.find((customer) => customer.id === selectedEnquiry.customerId)
        ?.name ?? `Customer ${selectedEnquiry.customerId}`
    );
  }, [customers, selectedEnquiry]);

  const productName = useMemo(() => {
    if (!selectedEnquiry) {
      return null;
    }

    return (
      products.find((product) => product.id === selectedEnquiry.productId)
        ?.name ?? `Product ${selectedEnquiry.productId}`
    );
  }, [products, selectedEnquiry]);

  const handleRespond = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    const trimmedResponse = response.trim();
    if (!selectedEnquiry || trimmedResponse.length === 0) {
      return;
    }

    const didRespond = await respondToEnquiry(selectedEnquiry.id, {
      response: trimmedResponse,
    });

    if (didRespond) {
      setResponse("");
    }
  };

  const handleClose = async () => {
    if (selectedEnquiry) {
      await closeEnquiry(selectedEnquiry.id);
    }
  };

  const handleReopen = async () => {
    if (selectedEnquiry) {
      await reopenEnquiry(selectedEnquiry.id);
    }
  };

  return (
    <main className="min-h-dvh bg-zinc-50 px-6 py-8 text-zinc-950 sm:px-8 lg:px-12">
      <div className="mx-auto flex w-full max-w-5xl flex-col gap-8">
        <header className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
          <div className="flex flex-col gap-2">
            <p className="text-sm font-medium uppercase tracking-wide text-emerald-700">
              Aqua Lifestyle Club
            </p>
            <h1 className="text-3xl font-semibold tracking-tight">
              Enquiry details
            </h1>
            <p className="max-w-2xl text-base text-zinc-600">
              Review the customer request and perform the backend enquiry
              workflow actions.
            </p>
          </div>
          <LinkButton href="/enquiries">Back to enquiries</LinkButton>
        </header>

        {!Number.isInteger(enquiryId) || enquiryId <= 0 ? (
          <StatusMessage tone="error">This enquiry id is invalid.</StatusMessage>
        ) : null}

        {isSelectedPending ? (
          <StatusMessage>Loading enquiry...</StatusMessage>
        ) : null}

        {isSelectedError ? (
          <StatusMessage tone="error">
            {selectedErrorMessage ?? "Unable to load this enquiry."}
          </StatusMessage>
        ) : null}

        {selectedEnquiry ? (
          <section className="grid gap-6 lg:grid-cols-[1fr_24rem]">
            <Card>
              <div className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
                <div>
                  <h2 className="text-2xl font-semibold tracking-tight">
                    {customerName}
                  </h2>
                  <p className="mt-1 text-sm text-zinc-600">{productName}</p>
                </div>
                <Badge tone={selectedEnquiry.isClosed ? "neutral" : "success"}>
                  {enquiryStatusLabels[selectedEnquiry.status]}
                </Badge>
              </div>

              <div className="mt-8 space-y-6">
                <div>
                  <h3 className="text-sm font-semibold uppercase tracking-wide text-zinc-500">
                    Customer message
                  </h3>
                  <p className="mt-3 whitespace-pre-line text-sm leading-6 text-zinc-800">
                    {selectedEnquiry.message}
                  </p>
                </div>

                {selectedEnquiry.response ? (
                  <div>
                    <h3 className="text-sm font-semibold uppercase tracking-wide text-zinc-500">
                      Response
                    </h3>
                    <p className="mt-3 whitespace-pre-line text-sm leading-6 text-zinc-800">
                      {selectedEnquiry.response}
                    </p>
                  </div>
                ) : null}
              </div>
            </Card>

            <aside className="flex flex-col gap-6">
              <Card>
                <h2 className="text-lg font-semibold">Workflow</h2>
                <dl className="mt-5 grid gap-3 text-sm">
                  <div className="flex justify-between gap-4">
                    <dt className="text-zinc-600">Created</dt>
                    <dd className="font-medium text-zinc-950">
                      {formatDate(selectedEnquiry.createdAt)}
                    </dd>
                  </div>
                  <div className="flex justify-between gap-4">
                    <dt className="text-zinc-600">Follow-ups</dt>
                    <dd className="font-medium text-zinc-950">
                      {selectedEnquiry.followUpCount}
                    </dd>
                  </div>
                  <div className="flex justify-between gap-4">
                    <dt className="text-zinc-600">Sales ready</dt>
                    <dd className="font-medium text-zinc-950">
                      {selectedEnquiry.isSalesReady ? "Yes" : "No"}
                    </dd>
                  </div>
                  <div className="flex justify-between gap-4">
                    <dt className="text-zinc-600">Converted</dt>
                    <dd className="font-medium text-zinc-950">
                      {selectedEnquiry.isConverted ? "Yes" : "No"}
                    </dd>
                  </div>
                </dl>

                <div className="mt-6 flex flex-col gap-3">
                  {selectedEnquiry.isClosed ? (
                    <Button
                      disabled={isActionPending}
                      onClick={handleReopen}
                      type="button"
                    >
                      Reopen enquiry
                    </Button>
                  ) : (
                    <Button
                      disabled={isActionPending}
                      className="bg-zinc-800 hover:bg-zinc-900"
                      onClick={handleClose}
                      type="button"
                    >
                      Close enquiry
                    </Button>
                  )}
                </div>
              </Card>

              {!selectedEnquiry.isClosed ? (
                <Card>
                  <form className="flex flex-col gap-4" onSubmit={handleRespond}>
                    <TextAreaField
                      label="Response"
                      name="response"
                      onChange={(event) => setResponse(event.target.value)}
                      placeholder="Write the response sent to this customer"
                      required
                      rows={5}
                      value={response}
                    />
                    <Button
                      disabled={isActionPending || response.trim().length === 0}
                      type="submit"
                    >
                      Save response
                    </Button>
                  </form>
                </Card>
              ) : null}

              {isActionSuccess ? (
                <StatusMessage tone="success">Enquiry updated.</StatusMessage>
              ) : null}

              {isActionError ? (
                <StatusMessage tone="error">
                  {actionErrorMessage ?? "Unable to update this enquiry."}
                </StatusMessage>
              ) : null}
            </aside>
          </section>
        ) : null}
      </div>
    </main>
  );
};
