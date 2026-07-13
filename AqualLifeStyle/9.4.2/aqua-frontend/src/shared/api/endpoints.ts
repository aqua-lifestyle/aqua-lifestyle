export const apiEndpoints = {
  areaLeaders: {
    apply: "/api/services/app/AreaLeader/Apply",
    getAll: "/api/services/app/AreaLeader/GetAll",
    getById: (id: number) => `/api/services/app/AreaLeader/Get?id=${id}`,
    promote: (id: number) => `/api/services/app/AreaLeader/Promote?id=${id}`,
    recordStartupOrder: (id: number) =>
      `/api/services/app/AreaLeader/RecordStartupOrder?id=${id}`,
  },
  areaSpaces: {
    approve: (id: number) => `/api/services/app/AreaSpace/Approve?id=${id}`,
    apply: "/api/services/app/AreaSpace/Apply",
    getAll: "/api/services/app/AreaSpace/GetAll",
    getById: (id: number) => `/api/services/app/AreaSpace/Get?id=${id}`,
    recordPresentation: (id: number) =>
      `/api/services/app/AreaSpace/RecordPresentation?id=${id}`,
    recordStartupOrder: (id: number) =>
      `/api/services/app/AreaSpace/RecordStartupOrder?id=${id}`,
    startReview: (id: number) =>
      `/api/services/app/AreaSpace/StartReview?id=${id}`,
    suspend: (id: number) => `/api/services/app/AreaSpace/Suspend?id=${id}`,
  },
  health: {
    get: "/api/health",
  },
  customers: {
    create: "/api/services/app/Customer/Create",
    getAll: "/api/services/app/Customer/GetAll",
    getById: (id: number) => `/api/services/app/Customer/Get?id=${id}`,
    update: "/api/services/app/Customer/Update",
  },
  enquiries: {
    close: (id: number) => `/api/services/app/Enquiry/Close?id=${id}`,
    convertToCustomer: (id: number) =>
      `/api/services/app/Enquiry/ConvertToCustomer?id=${id}`,
    create: "/api/services/app/Enquiry/Create",
    getAll: "/api/services/app/Enquiry/GetAll",
    getById: (id: number) => `/api/services/app/Enquiry/Get?id=${id}`,
    getSalesReady: "/api/services/app/Enquiry/GetSalesReadyEnquiries",
    recordFollowUp: (id: number) =>
      `/api/services/app/Enquiry/RecordFollowUp?id=${id}`,
    reopen: (id: number) => `/api/services/app/Enquiry/Reopen?id=${id}`,
    respond: (id: number) => `/api/services/app/Enquiry/Respond?id=${id}`,
  },
  facilitators: {
    getAll: "/api/services/app/Facilitator/GetAll",
    getByCustomer: (customerId: number) =>
      `/api/services/app/Facilitator/GetByCustomer?customerId=${customerId}`,
    getById: (id: number) => `/api/services/app/Facilitator/Get?id=${id}`,
    register: "/api/services/app/Facilitator/Register",
  },
  referrals: {
    confirmAward: (id: number) =>
      `/api/services/app/Referral/ConfirmAward?id=${id}`,
    getAll: "/api/services/app/Referral/GetAll",
    getByEnquiry: (enquiryId: number) =>
      `/api/services/app/Referral/GetByEnquiry?enquiryId=${enquiryId}`,
  },
  memberships: {
    getAll: "/api/services/app/Membership/GetAll",
    getById: (id: number) => `/api/services/app/Membership/Get?id=${id}`,
    getSavingsWindowStatuses:
      "/api/services/app/Membership/GetSavingsWindowStatuses",
    getTierBenefits: (id: number) =>
      `/api/services/app/Membership/GetTierBenefits?id=${id}`,
  },
  orderIntents: {
    cancel: (id: number) => `/api/services/app/OrderIntent/Cancel?id=${id}`,
    complete: (id: number) => `/api/services/app/OrderIntent/Complete?id=${id}`,
    createFromEnquiry: (enquiryId: number) =>
      `/api/services/app/OrderIntent/CreateFromEnquiry?enquiryId=${enquiryId}`,
    getAll: "/api/services/app/OrderIntent/GetAll",
    getById: (id: number) => `/api/services/app/OrderIntent/Get?id=${id}`,
  },
  products: {
    getAll: "/api/services/app/Product/GetAll",
    getAllForCustomer: (customerId: number) =>
      `/api/services/app/Product/GetAllForCustomer?customerId=${customerId}`,
    getById: (id: number) => `/api/services/app/Product/Get?id=${id}`,
  },
} as const;
