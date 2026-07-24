import type { Metadata } from "next";

import { AdminSavingsAccounts } from "@/src/components/admin/AdminSavingsAccounts";

export const metadata: Metadata = {
  title: "Savings Accounts | Aqua Lifestyle Club",
};

export default function AdminSavingsAccountsPage() {
  return <AdminSavingsAccounts />;
}
