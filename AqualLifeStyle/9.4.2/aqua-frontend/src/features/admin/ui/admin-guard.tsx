"use client";

import { ShieldCheck } from "lucide-react";
import { usePathname, useRouter } from "next/navigation";
import { useEffect, type ReactNode } from "react";

import { useAuthState } from "@/src/providers";
import { isSystemAdmin } from "@/src/shared/auth/roles";
import { Skeleton } from "@/src/shared/ui";

type AdminGuardProps = {
  children: ReactNode;
};

export const AdminGuard = ({ children }: AdminGuardProps) => {
  const pathname = usePathname();
  const router = useRouter();
  const { isAuthenticated, isReady, session } = useAuthState();
  const isAdmin = isSystemAdmin(session?.user?.role);

  useEffect(() => {
    if (!isReady) return;

    if (!isAuthenticated) {
      router.replace(`/login?redirect=${encodeURIComponent(pathname)}`);
      return;
    }

    if (!isAdmin) {
      router.replace("/");
    }
  }, [isAdmin, isAuthenticated, isReady, pathname, router]);

  if (!isReady || !isAuthenticated || !isAdmin) {
    return (
      <main className="min-h-[calc(100dvh-4rem)] bg-muted/30 px-4 py-10">
        <div className="mx-auto max-w-7xl">
          <div className="mb-6 flex items-center gap-3 text-muted-foreground">
            <ShieldCheck className="size-5" />
            <span className="text-sm font-semibold">Verifying administrator access…</span>
          </div>
          <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-5">
            {[0, 1, 2, 3, 4].map((item) => <Skeleton className="h-32" key={item} />)}
          </div>
        </div>
      </main>
    );
  }

  return children;
};
