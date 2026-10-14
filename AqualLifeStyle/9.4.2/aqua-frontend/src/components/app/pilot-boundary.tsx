"use client";

import { usePathname } from "next/navigation";
import Link from "next/link";
import { usePilotStatus } from "@/src/shared/hooks/use-pilot-status";
import { StatusMessage } from "@/src/shared/ui";
import { RecoveryContext } from "@/src/shared/hooks/recovery-context";

const pilotRoutes = new Set([
  "/", "/login", "/signup", "/verify-email", "/verify-email-sent", "/profile", "/member", "/member/programmes", "/member/programme-progress",
  "/member/invitations", "/member/profile", "/admin", "/admin/dashboard",
  "/admin/programme-participations", "/admin/weekly-sales-reviews", "/admin/weekly-earnings",
]);

export function PilotBoundary({ children }: { children: React.ReactNode }) {
  const { isPilot, modeKnown, isRecovery } = usePilotStatus();
  const path = usePathname();
  // Authentication remains available when recovery closes capability discovery.
  // Business routes require an authoritative capability result or explicit recovery handling.
  if (!modeKnown && path === "/login") return children;
  if (isRecovery) {
    if (path === "/admin/dashboard")
      return <RecoveryContext.Provider value={true}>{children}</RecoveryContext.Provider>;
    return <main className="p-6"><StatusMessage tone="warning">
      Restricted recovery mode is active. This operation is unavailable.
      <Link className="ml-2 underline" href="/admin/dashboard">Return to the administration overview</Link>
    </StatusMessage></main>;
  }
  if (!modeKnown) return <main className="p-6"><StatusMessage tone="info">Establishing the application capability boundary…</StatusMessage></main>;
  if (!isPilot) return children;
  return <>
    <div role="note" className="bg-amber-100 px-4 py-3 text-center font-semibold text-amber-950">
      ISOLATED NON-PRODUCTION PILOT · TEST MONEY ONLY · NO PAYOUT
    </div>
    {["/dashboard", "/member", "/member/dashboard", "/admin", "/admin/dashboard", "/"].includes(path) ?
      <main className="p-6 flex flex-col gap-4"><h1 className="text-2xl font-bold">AQGreen prospective pilot</h1>
        <Link href="/member/programmes">My joining and activation</Link>
        <Link href="/member/invitations">Sponsor invitations</Link>
        <Link href="/member/programme-progress">My structural progress</Link>
        <Link href="/admin/programme-participations">Authorized Area approvals and root admission</Link>
        <Link href="/admin/weekly-sales-reviews">Weekly sales evidence review</Link>
        <Link href="/admin/weekly-earnings">Closed-week test ledger</Link>
      </main> : pilotRoutes.has(path) || path.startsWith("/i/") ? children :
      <main className="p-6"><StatusMessage tone="error">
        This capability is closed in the isolated pilot.
      </StatusMessage></main>}
  </>;
}
