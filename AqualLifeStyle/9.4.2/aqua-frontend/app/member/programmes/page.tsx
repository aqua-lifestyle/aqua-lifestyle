import type { Metadata } from "next";

import { AuthenticatedPage } from "@/src/components/auth/authenticated-page";
import { MemberProgrammes } from "@/src/components/members/member-programmes";

export const metadata: Metadata = {
  title: "My Programmes | Aqua Lifestyle Club",
};

export default function MemberProgrammesPage() {
  return (
    <AuthenticatedPage>
      <MemberProgrammes />
    </AuthenticatedPage>
  );
}
