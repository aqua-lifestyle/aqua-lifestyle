export const apiEndpoints = {
  customers: {
    create: "/api/services/app/Customer/Create",
    getAll: "/api/services/app/Customer/GetAll",
  },
  products: {
    getAll: "/api/services/app/Product/GetAll",
  },
} as const;
