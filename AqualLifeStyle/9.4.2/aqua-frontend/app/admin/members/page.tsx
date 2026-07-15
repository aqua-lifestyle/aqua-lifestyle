import type { Metadata } from "next";

import { AdminMembers } from "@/src/components/admin/AdminMembers";

export const metadata: Metadata = { title: "Member Management | Aqua Lifestyle Club" };

export default function AdminMembersPage() {
  return <AdminMembers />;
}
