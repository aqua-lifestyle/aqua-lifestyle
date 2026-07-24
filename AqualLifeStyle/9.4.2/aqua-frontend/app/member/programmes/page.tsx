import type { Metadata } from "next";

import { MemberProgrammes } from "@/src/components/members/member-programmes";

export const metadata: Metadata = {
  title: "My Programmes | Aqua Lifestyle Club",
};

export default function MemberProgrammesPage() {
  return <MemberProgrammes />;
}
