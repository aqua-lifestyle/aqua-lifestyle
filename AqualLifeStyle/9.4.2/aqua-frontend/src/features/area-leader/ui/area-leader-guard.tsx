"use client";

import { ShieldCheck } from "lucide-react";
import { usePathname, useRouter } from "next/navigation";
import { type ReactNode, useEffect } from "react";

import { useAuthState } from "@/src/providers";
import { Skeleton } from "@/src/shared/ui";

type AreaLeaderGuardProps = {
  children: ReactNode;
};

const normalizeRole = (role: string | null | undefined) =>
  role?.replace(/[\s_-]/g, "").toLowerCase() ?? "";

export const isAreaLeader = (role: string | null | undefined) =>
  normalizeRole(role) === "arealeader";

export const AreaLeaderGuard = ({ children }: AreaLeaderGuardProps) => {
  const pathname = usePathname();
  const router = useRouter();
  const { isAuthenticated, isReady, session } = useAuthState();
  const hasAreaLeaderRole = isAreaLeader(session?.user?.role);
  const canViewAreaLeaders =
    session?.user?.permissions?.includes("Aqua.AreaLeaders.View") ?? false;
  const hasAccess = hasAreaLeaderRole && canViewAreaLeaders;

  useEffect(() => {
    if (!isReady) return;

    if (!isAuthenticated) {
      router.replace(`/login?redirect=${encodeURIComponent(pathname)}`);
      return;
    }

    if (!hasAccess) {
      router.replace("/dashboard");
    }
  }, [hasAccess, isAuthenticated, isReady, pathname, router]);

  if (!isReady || !isAuthenticated || !hasAccess) {
    return (
      <main className="min-h-[calc(100dvh-4rem)] bg-muted/30 px-4 py-8 sm:px-6 lg:px-8">
        <div className="mx-auto max-w-7xl">
          <div className="mb-6 flex items-center gap-3 text-muted-foreground">
            <ShieldCheck className="size-5" />
            <span className="text-sm font-semibold">Verifying Area Leader access…</span>
          </div>
          <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-5">
            {[0, 1, 2, 3, 4].map((item) => (
              <Skeleton className="h-28" key={item} />
            ))}
          </div>
        </div>
      </main>
    );
  }

  return children;
};
