import { describe, expect, it } from "vitest";

import {
  getOrderIntentsPending,
  getOrderIntentsSuccess,
  orderIntentActionSuccess,
} from "./actions";
import { initialOrderIntentsState, type OrderIntent } from "./context";
import { orderIntentsReducer } from "./reducer";

const existingOrderIntent: OrderIntent = {
  cancelledAt: null,
  completedAt: null,
  createdAt: "2026-07-08T12:00:00Z",
  customerId: 1,
  enquiryId: 3,
  id: 10,
  productId: 2,
  reservedAt: "2026-07-08T12:00:00Z",
  reservedPrice: 95,
  status: 1,
  statusText: "Reserved",
  unitPrice: 100,
};

describe("orderIntentsReducer", () => {
  it("sets load pending state without discarding existing order intents", () => {
    const state = orderIntentsReducer(
      {
        ...initialOrderIntentsState,
        orderIntents: [existingOrderIntent],
      },
      getOrderIntentsPending(),
    );

    expect(state.isLoadPending).toBe(true);
    expect(state.orderIntents).toEqual([existingOrderIntent]);
  });

  it("stores loaded order intents", () => {
    const state = orderIntentsReducer(
      initialOrderIntentsState,
      getOrderIntentsSuccess([existingOrderIntent]),
    );

    expect(state.isLoadSuccess).toBe(true);
    expect(state.orderIntents).toEqual([existingOrderIntent]);
  });

  it("upserts an order intent after an action succeeds", () => {
    const completedOrderIntent: OrderIntent = {
      ...existingOrderIntent,
      completedAt: "2026-07-08T12:05:00Z",
      status: 3,
      statusText: "Completed",
    };

    const state = orderIntentsReducer(
      {
        ...initialOrderIntentsState,
        orderIntents: [existingOrderIntent],
      },
      orderIntentActionSuccess(completedOrderIntent),
    );

    expect(state.isActionSuccess).toBe(true);
    expect(state.orderIntents).toEqual([completedOrderIntent]);
  });
});
