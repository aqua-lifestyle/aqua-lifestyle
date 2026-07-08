export const apiEndpoints = {
  customers: {
    create: "/api/services/app/Customer/Create",
    getAll: "/api/services/app/Customer/GetAll",
    getById: (id: number) => `/api/services/app/Customer/Get?id=${id}`,
    update: "/api/services/app/Customer/Update",
  },
  enquiries: {
    close: (id: number) => `/api/services/app/Enquiry/Close?id=${id}`,
    create: "/api/services/app/Enquiry/Create",
    getAll: "/api/services/app/Enquiry/GetAll",
    getById: (id: number) => `/api/services/app/Enquiry/Get?id=${id}`,
    reopen: (id: number) => `/api/services/app/Enquiry/Reopen?id=${id}`,
    respond: (id: number) => `/api/services/app/Enquiry/Respond?id=${id}`,
  },
  memberships: {
    getAll: "/api/services/app/Membership/GetAll",
    getById: (id: number) => `/api/services/app/Membership/Get?id=${id}`,
  },
  products: {
    getAll: "/api/services/app/Product/GetAll",
    getById: (id: number) => `/api/services/app/Product/Get?id=${id}`,
  },
} as const;
