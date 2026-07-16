import type { Metadata } from "next";

import { AdminCustomers } from "@/src/components/admin/AdminCustomers";

export const metadata: Metadata = {
  title: "Customer Management | Aqua Lifestyle Club",
};

export default function AdminCustomersPage() {
  return <AdminCustomers />;
}
