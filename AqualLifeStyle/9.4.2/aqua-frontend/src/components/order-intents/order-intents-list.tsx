"use client";

import { useEffect, useMemo } from "react";

import {
  type OrderIntent,
  type OrderIntentStatus,
  useCustomersActions,
  useCustomersState,
  useOrderIntentsActions,
  useOrderIntentsState,
  useProductsActions,
  useProductsState,
} from "@/src/providers";
import { Badge, Button, Card, LinkButton, StatusMessage } from "@/src/shared/ui";

const orderIntentStatusLabels: Record<OrderIntentStatus, string> = {
  0: "Draft",
  1: "Reserved",
  2: "Cancelled",
  3: "Completed",
};

const formatCurrency = (value: number) => {
  return new Intl.NumberFormat("en-ZA", {
    currency: "ZAR",
    style: "currency",
  }).format(value);
};

const formatDate = (date: string) => {
  return new Intl.DateTimeFormat("en-ZA", {
    dateStyle: "medium",
    timeStyle: "short",
  }).format(new Date(date));
};

const getStatusTone = (status: OrderIntentStatus) => {
  return status === 1 ? "success" : "neutral";
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

type OrderIntentCardProps = {
  customerName: string;
  isActionPending: boolean;
  onCancel: (id: number) => Promise<boolean>;
  onComplete: (id: number) => Promise<boolean>;
  orderIntent: OrderIntent;
  productName: string;
};

const OrderIntentCard = ({
  customerName,
  isActionPending,
  onCancel,
  onComplete,
  orderIntent,
  productName,
}: OrderIntentCardProps) => {
  const isReserved = orderIntent.status === 1;

  return (
    <Card>
      <div className="flex items-start justify-between gap-4">
        <div className="min-w-0">
          <h2 className="truncate text-lg font-semibold text-zinc-950">
            {customerName}
          </h2>
          <p className="mt-1 text-sm text-zinc-600">{productName}</p>
        </div>
        <Badge tone={getStatusTone(orderIntent.status)}>
          {orderIntentStatusLabels[orderIntent.status] ??
            orderIntent.statusText}
        </Badge>
      </div>

      <dl className="mt-6 grid gap-3 text-sm">
        <div className="flex justify-between gap-4">
          <dt className="text-zinc-600">Unit price</dt>
          <dd className="font-medium text-zinc-950">
            {formatCurrency(orderIntent.unitPrice)}
          </dd>
        </div>
        <div className="flex justify-between gap-4">
          <dt className="text-zinc-600">Reserved price</dt>
          <dd className="font-medium text-zinc-950">
            {formatCurrency(orderIntent.reservedPrice)}
          </dd>
        </div>
        <div className="flex justify-between gap-4">
          <dt className="text-zinc-600">Created</dt>
          <dd className="text-right font-medium text-zinc-950">
            {formatDate(orderIntent.createdAt)}
          </dd>
        </div>
        <div className="flex justify-between gap-4">
          <dt className="text-zinc-600">Source enquiry</dt>
          <dd className="font-medium text-zinc-950">
            {orderIntent.enquiryId ? `#${orderIntent.enquiryId}` : "Manual"}
          </dd>
        </div>
      </dl>

      <div className="mt-6 flex flex-col gap-3 sm:flex-row">
        {orderIntent.enquiryId ? (
          <LinkButton href={`/enquiries/${orderIntent.enquiryId}`}>
            Open enquiry
          </LinkButton>
        ) : null}
        {isReserved ? (
          <>
            <Button
              disabled={isActionPending}
              onClick={() => void onComplete(orderIntent.id)}
              type="button"
            >
              Mark completed
            </Button>
            <Button
              className="bg-zinc-800 hover:bg-zinc-900"
              disabled={isActionPending}
              onClick={() => void onCancel(orderIntent.id)}
              type="button"
            >
              Cancel
            </Button>
          </>
        ) : null}
      </div>
    </Card>
  );
};

export const OrderIntentsList = () => {
  const { getCustomers } = useCustomersActions();
  const { getProducts } = useProductsActions();
  const { cancelOrderIntent, completeOrderIntent, getOrderIntents } =
    useOrderIntentsActions();
  const { customers } = useCustomersState();
  const { products } = useProductsState();
  const {
    actionErrorMessage,
    isActionError,
    isActionPending,
    isActionSuccess,
    isLoadError,
    isLoadPending,
    loadErrorMessage,
    orderIntents,
  } = useOrderIntentsState();

  useEffect(() => {
    void getCustomers();
    void getProducts();
    void getOrderIntents();
  }, [getCustomers, getOrderIntents, getProducts]);

  const reservedCount = useMemo(
    () =>
      orderIntents.filter((orderIntent) => orderIntent.status === 1).length,
    [orderIntents],
  );
  const completedCount = useMemo(
    () =>
      orderIntents.filter((orderIntent) => orderIntent.status === 3).length,
    [orderIntents],
  );

  return (
    <main className="min-h-dvh bg-zinc-50 px-6 py-8 text-zinc-950 sm:px-8 lg:px-12">
      <div className="mx-auto flex w-full max-w-7xl flex-col gap-8">
        <header className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
          <div className="flex flex-col gap-2">
            <p className="text-sm font-medium uppercase tracking-wide text-emerald-700">
              Aqua Lifestyle Club
            </p>
            <h1 className="text-3xl font-semibold tracking-tight">
              Order intents
            </h1>
            <p className="max-w-2xl text-base leading-7 text-zinc-600">
              Reservation-ready records created from converted enquiries. This
              proves the commerce handoff without introducing payments before
              the demo has validated demand.
            </p>
          </div>
          <div className="flex flex-col gap-3 sm:flex-row">
            <LinkButton href="/enquiries">Open enquiries</LinkButton>
            <LinkButton href="/" variant="primary">
              Dashboard
            </LinkButton>
          </div>
        </header>

        <section className="grid gap-4 sm:grid-cols-3">
          <Card>
            <p className="text-sm text-zinc-600">Total intents</p>
            <p className="mt-3 text-3xl font-semibold">{orderIntents.length}</p>
          </Card>
          <Card>
            <p className="text-sm text-zinc-600">Reserved</p>
            <p className="mt-3 text-3xl font-semibold">{reservedCount}</p>
          </Card>
          <Card>
            <p className="text-sm text-zinc-600">Completed</p>
            <p className="mt-3 text-3xl font-semibold">{completedCount}</p>
          </Card>
        </section>

        {isLoadPending ? (
          <StatusMessage>Loading order intents...</StatusMessage>
        ) : null}

        {isLoadError ? (
          <StatusMessage tone="error">
            {loadErrorMessage ?? "Unable to load order intents."}
          </StatusMessage>
        ) : null}

        {isActionSuccess ? (
          <StatusMessage tone="success">Order intent updated.</StatusMessage>
        ) : null}

        {isActionError ? (
          <StatusMessage tone="error">
            {actionErrorMessage ?? "Unable to update this order intent."}
          </StatusMessage>
        ) : null}

        {!isLoadPending && !isLoadError && orderIntents.length === 0 ? (
          <StatusMessage>
            <span>No order intents exist yet.</span>{" "}
            <LinkButton href="/enquiries">Convert an enquiry first</LinkButton>
          </StatusMessage>
        ) : null}

        {orderIntents.length > 0 ? (
          <section className="grid gap-4 lg:grid-cols-2 xl:grid-cols-3">
            {orderIntents.map((orderIntent) => (
              <OrderIntentCard
                customerName={getCustomerName(
                  orderIntent.customerId,
                  customers,
                )}
                isActionPending={isActionPending}
                key={orderIntent.id}
                onCancel={cancelOrderIntent}
                onComplete={completeOrderIntent}
                orderIntent={orderIntent}
                productName={getProductName(orderIntent.productId, products)}
              />
            ))}
          </section>
        ) : null}
      </div>
    </main>
  );
};
