import { act, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";

import { apiEndpoints, httpClient } from "@/src/shared/api";
import type { Enquiry } from "./Enquiries/context";
import {
  EnquiriesProvider,
  useEnquiriesActions,
  useEnquiriesState,
} from "./Enquiries";
import type { OrderIntent } from "./OrderIntents/context";
import {
  OrderIntentsProvider,
  useOrderIntentsActions,
  useOrderIntentsState,
} from "./OrderIntents";

vi.mock("@/src/shared/api", async () => {
  const actual = await vi.importActual<typeof import("@/src/shared/api")>(
    "@/src/shared/api",
  );
  return { ...actual, httpClient: { get: vi.fn(), post: vi.fn() } };
});

const deferred = <T,>() => {
  let resolve!: (value: T) => void;
  const promise = new Promise<T>((resolvePromise) => {
    resolve = resolvePromise;
  });
  return { promise, resolve };
};

const enquiry = (id: number): Enquiry => ({
  assignedToMemberId: null,
  conversionProbability: 0,
  convertedAt: null,
  createdAt: "2026-07-30T00:00:00Z",
  customerId: id,
  followUpCount: 0,
  followUps: [],
  id,
  isClosed: false,
  isConverted: false,
  isPending: true,
  isSalesReady: false,
  lastFollowUpDate: null,
  message: "Question",
  productId: 1,
  response: null,
  status: 0,
});

const order = (id: number): OrderIntent => ({
  cancelledAt: null,
  completedAt: null,
  createdAt: "2026-07-30T00:00:00Z",
  customerId: id,
  enquiryId: null,
  id,
  productId: 1,
  reservedAt: "2026-07-30T00:00:00Z",
  reservedPrice: 100,
  status: 1,
  statusText: "Reserved",
  unitPrice: 100,
});

const EnquiryProbe = () => {
  const { getEnquiries, getMyEnquiries } = useEnquiriesActions();
  const { enquiries } = useEnquiriesState();
  return (
    <>
      <button onClick={() => void getEnquiries()}>All enquiries</button>
      <button onClick={() => void getMyEnquiries()}>My enquiries</button>
      <output>{enquiries.map((item) => item.id).join(",")}</output>
    </>
  );
};

const OrderProbe = () => {
  const { getMyOrderIntents, getOrderIntents } = useOrderIntentsActions();
  const { orderIntents } = useOrderIntentsState();
  return (
    <>
      <button onClick={() => void getOrderIntents()}>All orders</button>
      <button onClick={() => void getMyOrderIntents()}>My orders</button>
      <output>{orderIntents.map((item) => item.id).join(",")}</output>
    </>
  );
};

describe("scoped provider loaders", () => {
  it("ignores an older all-enquiries response after the self request starts", async () => {
    const all = deferred<Enquiry[]>();
    const mine = deferred<Enquiry[]>();
    vi.mocked(httpClient.get).mockImplementation((endpoint) =>
      endpoint === apiEndpoints.enquiries.getMine ? mine.promise : all.promise,
    );
    render(
      <EnquiriesProvider>
        <EnquiryProbe />
      </EnquiriesProvider>,
    );

    fireEvent.click(screen.getByRole("button", { name: "All enquiries" }));
    fireEvent.click(screen.getByRole("button", { name: "My enquiries" }));
    mine.resolve([enquiry(2)]);
    await waitFor(() => expect(screen.getByText("2")).toBeInTheDocument());
    await act(async () => all.resolve([enquiry(1)]));

    expect(screen.queryByText("1")).not.toBeInTheDocument();
  });

  it("ignores an older all-orders response after the self request starts", async () => {
    const all = deferred<OrderIntent[]>();
    const mine = deferred<OrderIntent[]>();
    vi.mocked(httpClient.get).mockImplementation((endpoint) =>
      endpoint === apiEndpoints.orderIntents.getMine ? mine.promise : all.promise,
    );
    render(
      <OrderIntentsProvider>
        <OrderProbe />
      </OrderIntentsProvider>,
    );

    fireEvent.click(screen.getByRole("button", { name: "All orders" }));
    fireEvent.click(screen.getByRole("button", { name: "My orders" }));
    mine.resolve([order(2)]);
    await waitFor(() => expect(screen.getByText("2")).toBeInTheDocument());
    await act(async () => all.resolve([order(1)]));

    expect(screen.queryByText("1")).not.toBeInTheDocument();
  });
});
