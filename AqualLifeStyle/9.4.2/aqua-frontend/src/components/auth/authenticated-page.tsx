"use client";

import { ShieldCheck } from "lucide-react";
import { usePathname, useRouter } from "next/navigation";
import { type ReactNode, useEffect } from "react";

import { useAuthState } from "@/src/providers";
import { Skeleton } from "@/src/shared/ui";

type AuthenticatedPageProps = {
  children: ReactNode;
};

export const AuthenticatedPage = ({ children }: AuthenticatedPageProps) => {
  const pathname = usePathname();
  const router = useRouter();
  const { isAuthenticated, isReady } = useAuthState();

  useEffect(() => {
    if (isReady && !isAuthenticated) {
      router.replace(`/login?redirect=${encodeURIComponent(pathname)}`);
    }
  }, [isAuthenticated, isReady, pathname, router]);

  if (!isReady || !isAuthenticated) {
    return (
      <main className="min-h-dvh bg-muted/30 px-4 py-8 sm:px-6 lg:px-8">
        <div className="mx-auto max-w-3xl">
          <div className="mb-6 flex items-center gap-3 text-muted-foreground">
            <ShieldCheck className="size-5" />
            <span className="text-sm font-semibold">
              {isReady ? "Taking you to sign in…" : "Checking your account…"}
            </span>
          </div>
          <Skeleton className="h-48" />
        </div>
      </main>
    );
  }

  return children;
};
