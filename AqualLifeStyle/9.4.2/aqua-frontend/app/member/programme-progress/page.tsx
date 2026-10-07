"use client";

import { useEffect } from "react";
import { useRouter } from "next/navigation";
import { MemberProgrammeProgress } from "@/src/components/members/member-programme-progress";
import { usePilotStatus } from "@/src/shared/hooks/use-pilot-status";
import { StatusMessage } from "@/src/shared/ui";

export default function MemberProgrammeProgressPage() {
  const { isPilot, modeKnown } = usePilotStatus();
  const router = useRouter();
  useEffect(() => {
    if (modeKnown && !isPilot) router.replace("/member/programmes");
  }, [isPilot, modeKnown, router]);
  if (modeKnown && isPilot) return <MemberProgrammeProgress />;
  return <StatusMessage tone="info">Establishing the programme progress boundary…</StatusMessage>;
}
