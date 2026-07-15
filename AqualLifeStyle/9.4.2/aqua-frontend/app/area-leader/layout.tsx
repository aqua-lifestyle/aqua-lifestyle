import type { ReactNode } from "react";

import { AreaLeaderGuard } from "@/src/features/area-leader/ui/area-leader-guard";
import { AreaLeaderNav } from "@/src/features/area-leader/ui/area-leader-nav";

export default function AreaLeaderLayout({ children }: { children: ReactNode }) {
  return (
    <AreaLeaderGuard>
      <AreaLeaderNav />
      {children}
    </AreaLeaderGuard>
  );
}
