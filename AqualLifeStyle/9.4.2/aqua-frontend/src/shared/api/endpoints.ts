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
  account: {
    getTenantSelfRegistrationAvailability:
      "/api/services/app/Account/GetTenantSelfRegistrationAvailability",
    register: "/api/services/app/Account/Register",
  },
  customers: {
    create: "/api/services/app/Customer/Create",
    getAll: "/api/services/app/Customer/GetAll",
    getById: (id: number) => `/api/services/app/Customer/Get?id=${id}`,
    getMyCustomer: "/api/services/app/Customer/GetMyCustomer",
    changeMembership: "/api/services/app/Customer/ChangeMembership",
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
    getActiveTiers: "/api/services/app/Membership/GetActiveTiers",
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
    createForCurrentCustomer: (productId: number) =>
      `/api/services/app/OrderIntent/CreateForCurrentCustomer?productId=${productId}`,
    getAll: "/api/services/app/OrderIntent/GetAll",
    getById: (id: number) => `/api/services/app/OrderIntent/Get?id=${id}`,
  },
  products: {
    getAll: "/api/services/app/Product/GetAll",
    getAllForCustomer: (customerId: number) =>
      `/api/services/app/Product/GetAllForCustomer?customerId=${customerId}`,
    getById: (id: number) => `/api/services/app/Product/Get?id=${id}`,
  },
  programmeParticipations: {
    getAdminParticipations:
      "/api/services/app/AdminProgrammeParticipation/GetAll",
    getMyParticipations:
      "/api/services/app/ClubMemberProgrammeParticipation/GetMyParticipations",
    startDirectOnyx:
      "/api/services/app/ClubMemberProgrammeParticipation/StartDirectOnyx",
    startEntry:
      "/api/services/app/ClubMemberProgrammeParticipation/StartEntry",
  },
  savings: {
    getAdminAccounts: "/api/services/app/AdminSavings/GetAll",
    getMyAccount: "/api/services/app/ClubMemberSavings/GetMyAccount",
  },
  weeklyEarnings: {
    calculateLatestClosedWeek:
      "/api/services/app/AdminCommission/CalculateLatestClosedWeek",
    getAll: "/api/services/app/AdminCommission/GetAll",
    recordPayment: "/api/services/app/AdminCommission/RecordPayment",
    release: "/api/services/app/AdminCommission/Release",
  },
} as const;
