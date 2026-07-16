import type { Metadata } from "next";

import { AdminAccessLevels } from "@/src/components/admin/AdminAccessLevels";

export const metadata: Metadata = { title: "Access Levels | Aqua Lifestyle Club" };

export default function AdminAccessLevelsPage() {
  return <AdminAccessLevels />;
}
