import { describe, expect, it } from "vitest";

import { enquiryActionSuccess, getEnquiriesPending } from "./actions";
import type { Enquiry } from "./context";
import { initialEnquiriesState } from "./context";
import { enquiriesReducer } from "./reducer";

const createEnquiry = (overrides: Partial<Enquiry> = {}): Enquiry => ({
  assignedToMemberId: null,
  conversionProbability: 25,
  convertedAt: null,
  createdAt: "2026-01-01T00:00:00Z",
  customerId: 1,
  followUpCount: 0,
  followUps: [],
  id: 1,
  isClosed: false,
  isConverted: false,
  isPending: true,
  isSalesReady: false,
  lastFollowUpDate: null,
  message: "I want to know more.",
  productId: 1,
  response: null,
  status: 0,
  ...overrides,
});

describe("enquiriesReducer", () => {
  it("sets load pending state without discarding existing enquiries", () => {
    const existingEnquiry = createEnquiry();

    const state = enquiriesReducer(
      {
        ...initialEnquiriesState,
        enquiries: [existingEnquiry],
        isLoadError: true,
        loadErrorMessage: "Previous error",
      },
      getEnquiriesPending(),
    );

    expect(state.enquiries).toEqual([existingEnquiry]);
    expect(state.isLoadPending).toBe(true);
    expect(state.isLoadError).toBe(false);
    expect(state.loadErrorMessage).toBeNull();
  });

  it("updates the matching enquiry and selected enquiry after an action", () => {
    const originalEnquiry = createEnquiry();
    const updatedEnquiry = createEnquiry({
      conversionProbability: 100,
      isConverted: true,
      status: 2,
    });

    const state = enquiriesReducer(
      {
        ...initialEnquiriesState,
        enquiries: [originalEnquiry],
      },
      enquiryActionSuccess(updatedEnquiry),
    );

    expect(state.enquiries).toEqual([updatedEnquiry]);
    expect(state.selectedEnquiry).toEqual(updatedEnquiry);
    expect(state.isActionSuccess).toBe(true);
    expect(state.isActionPending).toBe(false);
  });
});
