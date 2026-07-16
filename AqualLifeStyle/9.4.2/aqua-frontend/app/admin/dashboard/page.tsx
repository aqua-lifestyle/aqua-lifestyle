import type { Metadata } from "next";

import { AdminDashboard } from "@/src/features/admin/ui/admin-dashboard";

export const metadata: Metadata = {
  title: "Administration Dashboard | Aqua Lifestyle Club",
};

export default function AdminDashboardPage() {
  return <AdminDashboard />;
}
