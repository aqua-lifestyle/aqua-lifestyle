import type { Metadata } from "next";

import { CustomersList } from "@/src/components/customers/customers-list";

export const metadata: Metadata = {
  title: "Customer Management | Aqua Lifestyle Club",
};

export default function AdminCustomersPage() {
  return <CustomersList showAdminImport />;
}
