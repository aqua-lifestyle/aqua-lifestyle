import type { Metadata } from "next";

import { AdminProgrammeParticipations } from "@/src/components/admin/AdminProgrammeParticipations";

export const metadata: Metadata = {
  title: "Programme Participation | Aqua Lifestyle Club",
};

export default function AdminProgrammeParticipationsPage() {
  return <AdminProgrammeParticipations />;
}
