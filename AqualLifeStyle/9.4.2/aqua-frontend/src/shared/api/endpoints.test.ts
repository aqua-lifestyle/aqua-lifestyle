import { describe, expect, it } from "vitest";

import { apiEndpoints } from "./endpoints";

describe("apiEndpoints", () => {
  it("exposes static customer endpoints and builds id-based urls", () => {
    expect(apiEndpoints.customers.getAll).toBe(
      "/api/services/app/Customer/GetAll",
    );
    expect(apiEndpoints.customers.getById(7)).toBe(
      "/api/services/app/Customer/Get?id=7",
    );
  });

  it("builds enquiry action urls with the supplied id", () => {
    expect(apiEndpoints.enquiries.close(3)).toBe(
      "/api/services/app/Enquiry/Close?id=3",
    );
    expect(apiEndpoints.enquiries.convertToCustomer(3)).toBe(
      "/api/services/app/Enquiry/ConvertToCustomer?id=3",
    );
    expect(apiEndpoints.enquiries.recordFollowUp(3)).toBe(
      "/api/services/app/Enquiry/RecordFollowUp?id=3",
    );
    expect(apiEndpoints.enquiries.reopen(3)).toBe(
      "/api/services/app/Enquiry/Reopen?id=3",
    );
    expect(apiEndpoints.enquiries.respond(3)).toBe(
      "/api/services/app/Enquiry/Respond?id=3",
    );
  });

  it("builds membership urls", () => {
    expect(apiEndpoints.memberships.getById(5)).toBe(
      "/api/services/app/Membership/Get?id=5",
    );
    expect(apiEndpoints.memberships.getTierBenefits(5)).toBe(
      "/api/services/app/Membership/GetTierBenefits?id=5",
    );
    expect(apiEndpoints.memberships.getSavingsWindowStatuses).toBe(
      "/api/services/app/Membership/GetSavingsWindowStatuses",
    );
  });

  it("builds order intent urls", () => {
    expect(apiEndpoints.orderIntents.cancel(9)).toBe(
      "/api/services/app/OrderIntent/Cancel?id=9",
    );
    expect(apiEndpoints.orderIntents.complete(9)).toBe(
      "/api/services/app/OrderIntent/Complete?id=9",
    );
    expect(apiEndpoints.orderIntents.createFromEnquiry(9)).toBe(
      "/api/services/app/OrderIntent/CreateFromEnquiry?enquiryId=9",
    );
    expect(apiEndpoints.orderIntents.getById(9)).toBe(
      "/api/services/app/OrderIntent/Get?id=9",
    );
  });

  it("builds product urls", () => {
    expect(apiEndpoints.products.getAllForCustomer(4)).toBe(
      "/api/services/app/Product/GetAllForCustomer?customerId=4",
    );
    expect(apiEndpoints.products.getById(4)).toBe(
      "/api/services/app/Product/Get?id=4",
    );
  });
});
