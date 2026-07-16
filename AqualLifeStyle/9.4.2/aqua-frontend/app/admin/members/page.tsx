import type { Metadata } from "next";

import { AdminMembers } from "@/src/components/admin/AdminMembers";

export const metadata: Metadata = { title: "Club Member Management | Aqua Lifestyle Club" };

export default function AdminMembersPage() {
  return <AdminMembers />;
}
