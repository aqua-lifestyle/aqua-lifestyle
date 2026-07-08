"use client";

import { FormEvent, useEffect, useMemo, useState } from "react";

import {
  type EnquiryFollowUpOutcome,
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
  SelectField,
  StatusMessage,
  TextAreaField,
} from "@/src/shared/ui";

const enquiryStatusLabels: Record<EnquiryStatus, string> = {
  0: "Pending",
  1: "Responded",
  2: "Closed",
};

const followUpOutcomeLabels: Record<EnquiryFollowUpOutcome, string> = {
  0: "Interested",
  1: "Considering",
  2: "Not interested",
  3: "Converted",
  4: "Lost",
};

const followUpOutcomeOptions = Object.entries(followUpOutcomeLabels).map(
  ([value, label]) => ({
    label,
    value: Number(value) as EnquiryFollowUpOutcome,
  }),
);

const formatDate = (date: string) => {
  return new Intl.DateTimeFormat("en-ZA", {
    dateStyle: "medium",
    timeStyle: "short",
  }).format(new Date(date));
};

const formatPercent = (value: number) => {
  return new Intl.NumberFormat("en-ZA", {
    maximumFractionDigits: 0,
    style: "percent",
  }).format(value / 100);
};

type EnquiryDetailsProps = {
  enquiryId: number;
};

export const EnquiryDetails = ({ enquiryId }: EnquiryDetailsProps) => {
  const [followUpNotes, setFollowUpNotes] = useState("");
  const [followUpOutcome, setFollowUpOutcome] =
    useState<EnquiryFollowUpOutcome>(0);
  const [response, setResponse] = useState("");
  const { getCustomers } = useCustomersActions();
  const { getProducts } = useProductsActions();
  const {
    closeEnquiry,
    convertEnquiryToCustomer,
    getEnquiry,
    recordFollowUp,
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

  const handleRecordFollowUp = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    const trimmedNotes = followUpNotes.trim();
    if (!selectedEnquiry || trimmedNotes.length === 0) {
      return;
    }

    const didRecord = await recordFollowUp(selectedEnquiry.id, {
      followUpByMemberId: null,
      followUpNotes: trimmedNotes,
      outcome: followUpOutcome,
    });

    if (didRecord) {
      setFollowUpNotes("");
      setFollowUpOutcome(0);
    }
  };

  const handleClose = async () => {
    if (selectedEnquiry) {
      await closeEnquiry(selectedEnquiry.id);
    }
  };

  const handleConvert = async () => {
    if (selectedEnquiry) {
      const didConvert = await convertEnquiryToCustomer(selectedEnquiry.id);

      if (didConvert) {
        void getCustomers();
      }
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
                    <dt className="text-zinc-600">Probability</dt>
                    <dd className="font-medium text-zinc-950">
                      {formatPercent(selectedEnquiry.conversionProbability)}
                    </dd>
                  </div>
                  <div className="flex justify-between gap-4">
                    <dt className="text-zinc-600">Last follow-up</dt>
                    <dd className="text-right font-medium text-zinc-950">
                      {selectedEnquiry.lastFollowUpDate
                        ? formatDate(selectedEnquiry.lastFollowUpDate)
                        : "Not yet"}
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
                  {selectedEnquiry.convertedAt ? (
                    <div className="flex justify-between gap-4">
                      <dt className="text-zinc-600">Converted at</dt>
                      <dd className="text-right font-medium text-zinc-950">
                        {formatDate(selectedEnquiry.convertedAt)}
                      </dd>
                    </div>
                  ) : null}
                </dl>

                <div className="mt-6 flex flex-col gap-3">
                  {!selectedEnquiry.isConverted ? (
                    <Button
                      disabled={isActionPending}
                      onClick={handleConvert}
                      type="button"
                    >
                      Mark converted in ABP
                    </Button>
                  ) : null}

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
                  <form
                    className="flex flex-col gap-4"
                    onSubmit={handleRecordFollowUp}
                  >
                    <SelectField
                      label="Follow-up outcome"
                      name="followUpOutcome"
                      onChange={(event) =>
                        setFollowUpOutcome(
                          Number(event.target.value) as EnquiryFollowUpOutcome,
                        )
                      }
                      value={followUpOutcome}
                    >
                      {followUpOutcomeOptions.map((option) => (
                        <option key={option.value} value={option.value}>
                          {option.label}
                        </option>
                      ))}
                    </SelectField>
                    <TextAreaField
                      label="Follow-up notes"
                      name="followUpNotes"
                      onChange={(event) => setFollowUpNotes(event.target.value)}
                      placeholder="Capture the customer conversation, next step, or decision"
                      required
                      rows={5}
                      value={followUpNotes}
                    />
                    <Button
                      disabled={
                        isActionPending || followUpNotes.trim().length === 0
                      }
                      type="submit"
                    >
                      Record follow-up
                    </Button>
                  </form>
                </Card>
              ) : null}

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

            <section className="lg:col-span-2">
              <Card>
                <div className="flex flex-col gap-2">
                  <h2 className="text-lg font-semibold">Follow-up timeline</h2>
                  <p className="text-sm leading-6 text-zinc-600">
                    Track the conversion journey from interest to a sales-ready
                    or resolved enquiry.
                  </p>
                </div>

                {selectedEnquiry.followUps.length === 0 ? (
                  <StatusMessage>No follow-ups recorded yet.</StatusMessage>
                ) : (
                  <ol className="mt-6 grid gap-4">
                    {selectedEnquiry.followUps.map((followUp) => (
                      <li
                        className="rounded-lg border border-zinc-200 bg-zinc-50 p-4"
                        key={followUp.id}
                      >
                        <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
                          <div>
                            <p className="font-medium text-zinc-950">
                              {followUpOutcomeLabels[followUp.outcome] ??
                                followUp.outcomeText}
                            </p>
                            <p className="mt-1 text-sm text-zinc-600">
                              {formatDate(followUp.followUpDate)}
                            </p>
                          </div>
                          <Badge tone={followUp.isResolved ? "neutral" : "success"}>
                            {formatPercent(followUp.conversionProbability)}
                          </Badge>
                        </div>
                        <p className="mt-4 whitespace-pre-line text-sm leading-6 text-zinc-700">
                          {followUp.followUpNotes}
                        </p>
                      </li>
                    ))}
                  </ol>
                )}
              </Card>
            </section>
          </section>
        ) : null}
      </div>
    </main>
  );
};
