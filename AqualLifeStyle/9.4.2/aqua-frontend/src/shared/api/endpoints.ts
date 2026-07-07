export const apiEndpoints = {
  customers: {
    create: "/api/services/app/Customer/Create",
    getAll: "/api/services/app/Customer/GetAll",
  },
  enquiries: {
    create: "/api/services/app/Enquiry/Create",
    getAll: "/api/services/app/Enquiry/GetAll",
  },
  memberships: {
    getAll: "/api/services/app/Membership/GetAll",
  },
  products: {
    getAll: "/api/services/app/Product/GetAll",
  },
} as const;
