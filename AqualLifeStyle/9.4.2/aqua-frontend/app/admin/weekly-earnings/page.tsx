import type { Metadata } from "next";

import { AdminWeeklyEarnings } from "@/src/components/admin/AdminWeeklyEarnings";

export const metadata: Metadata = {
  title: "Weekly Earnings | Aqua Lifestyle Club",
};

export default function AdminWeeklyEarningsPage() {
  return <AdminWeeklyEarnings />;
}
