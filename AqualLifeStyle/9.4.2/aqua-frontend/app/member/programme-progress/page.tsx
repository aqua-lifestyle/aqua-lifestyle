import type { Metadata } from "next";

import { MemberProgrammeProgress } from "@/src/components/members/member-programme-progress";

export const metadata: Metadata = {
  title: "AQGreen Progress | Aqua Lifestyle Club",
};

export default function MemberProgrammeProgressPage() {
  return <MemberProgrammeProgress />;
}
