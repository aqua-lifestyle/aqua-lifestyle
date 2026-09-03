import type { Metadata } from "next";

import { AdminWeeklySalesReviews } from "@/src/components/admin/AdminWeeklySalesReviews";

export const metadata: Metadata = {
  title: "Weekly Sales Reviews | Aqua Lifestyle Club",
};

export default function AdminWeeklySalesReviewsPage() {
  return <AdminWeeklySalesReviews />;
}
