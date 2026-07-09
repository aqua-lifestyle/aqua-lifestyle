import { describe, expect, it } from "vitest";

import {
  createCustomerError,
  createCustomerPending,
  createCustomerSuccess,
  getCustomerError,
  getCustomerPending,
  getCustomerSuccess,
  getCustomersError,
  getCustomersPending,
  getCustomersSuccess,
  updateCustomerError,
  updateCustomerPending,
  updateCustomerSuccess,
} from "./actions";
import { type Customer, initialCustomersState } from "./context";
import { customersReducer } from "./reducer";

const customer: Customer = {
  id: 1,
  name: "Ada Lovelace",
  email: "ada@example.com",
  membershipId: 2,
  isActive: true,
};

describe("customersReducer", () => {
  it("returns the current state for unknown actions", () => {
    const state = customersReducer(initialCustomersState, {
      type: "customers/unknown",
    } as never);

    expect(state).toBe(initialCustomersState);
  });

  it("tracks the customers list request lifecycle", () => {
    const pendingState = customersReducer(
      initialCustomersState,
      getCustomersPending(),
    );

    expect(pendingState.isLoadPending).toBe(true);
    expect(pendingState.isLoadError).toBe(false);
    expect(pendingState.loadErrorMessage).toBeNull();

    const successState = customersReducer(
      pendingState,
      getCustomersSuccess([customer]),
    );

    expect(successState.isLoadPending).toBe(false);
    expect(successState.isLoadSuccess).toBe(true);
    expect(successState.customers).toEqual([customer]);

    const errorState = customersReducer(
      successState,
      getCustomersError("Unable to load customers."),
    );

    expect(errorState.isLoadError).toBe(true);
    expect(errorState.isLoadPending).toBe(false);
    expect(errorState.isLoadSuccess).toBe(false);
    expect(errorState.loadErrorMessage).toBe("Unable to load customers.");
  });

  it("tracks the create customer lifecycle", () => {
    const pendingState = customersReducer(
      initialCustomersState,
      createCustomerPending(),
    );

    expect(pendingState.isCreatePending).toBe(true);
    expect(pendingState.createErrorMessage).toBeNull();

    const successState = customersReducer(
      pendingState,
      createCustomerSuccess(),
    );

    expect(successState.isCreatePending).toBe(false);
    expect(successState.isCreateSuccess).toBe(true);

    const errorState = customersReducer(
      pendingState,
      createCustomerError("Email already in use."),
    );

    expect(errorState.isCreateError).toBe(true);
    expect(errorState.isCreatePending).toBe(false);
    expect(errorState.createErrorMessage).toBe("Email already in use.");
  });

  it("tracks the selected customer lifecycle", () => {
    const pendingState = customersReducer(
      initialCustomersState,
      getCustomerPending(),
    );

    expect(pendingState.isSelectedPending).toBe(true);

    const successState = customersReducer(
      pendingState,
      getCustomerSuccess(customer),
    );

    expect(successState.isSelectedSuccess).toBe(true);
    expect(successState.selectedCustomer).toEqual(customer);

    const errorState = customersReducer(
      successState,
      getCustomerError("Customer not found."),
    );

    expect(errorState.isSelectedError).toBe(true);
    expect(errorState.selectedCustomer).toBeNull();
    expect(errorState.selectedErrorMessage).toBe("Customer not found.");
  });

  it("replaces the matching customer on update success", () => {
    const seededState = customersReducer(
      initialCustomersState,
      getCustomersSuccess([customer, { ...customer, id: 2, name: "Grace" }]),
    );

    const pendingState = customersReducer(seededState, updateCustomerPending());
    expect(pendingState.isUpdatePending).toBe(true);

    const updated: Customer = { ...customer, name: "Ada L." };
    const successState = customersReducer(
      pendingState,
      updateCustomerSuccess(updated),
    );

    expect(successState.isUpdateSuccess).toBe(true);
    expect(successState.selectedCustomer).toEqual(updated);
    expect(successState.customers).toEqual([
      updated,
      { ...customer, id: 2, name: "Grace" },
    ]);
  });

  it("records update failures without touching the list", () => {
    const seededState = customersReducer(
      initialCustomersState,
      getCustomersSuccess([customer]),
    );

    const errorState = customersReducer(
      seededState,
      updateCustomerError("Update rejected."),
    );

    expect(errorState.isUpdateError).toBe(true);
    expect(errorState.updateErrorMessage).toBe("Update rejected.");
    expect(errorState.customers).toEqual([customer]);
  });
});
