"use client";

import { ChevronDown, LogOut, Settings, User } from "lucide-react";
import Link from "next/link";
import { useRouter } from "next/navigation";

import { useAuthActions, useAuthState } from "@/src/providers";
import { Avatar } from "@/src/shared/ui";

export const UserMenu = ({ inverted = false }: { inverted?: boolean }) => {
  const router = useRouter();
  const { isAuthenticated, session } = useAuthState();
  const { clearSession } = useAuthActions();

  const handleSignOut = () => {
    clearSession();
    router.replace("/login");
  };

  const userLabel =
    session?.user?.name ?? session?.user?.email ?? "Demo user";

  if (!isAuthenticated) {
    return (
      <div className="flex items-center gap-2">
        <Link
          className={`hidden rounded-lg px-3 py-2 text-sm font-semibold transition sm:inline ${
            inverted ? "text-white/70 hover:bg-white/10 hover:text-white" : "text-muted-foreground hover:text-foreground"
          }`}
          href="/login"
        >
          Sign in
        </Link>
        <Link
          className={`inline-flex h-9 items-center rounded-full px-4 text-sm font-semibold text-white transition ${
            inverted ? "bg-[#7540e8] hover:bg-[#8655ef]" : "bg-accent hover:bg-accent-dark"
          }`}
          href="/signup"
        >
          Sign up
        </Link>
      </div>
    );
  }

  return (
    <div className="flex items-center gap-3">
      <div className="hidden text-right sm:block">
        <p className={`text-sm font-semibold ${inverted ? "text-white" : ""}`}>{userLabel}</p>
        <p className={`text-xs ${inverted ? "text-white/60" : "text-muted-foreground"}`}>{session?.user?.email}</p>
      </div>
      <div className="group relative">
        <button
          className="flex items-center gap-1.5 rounded-full transition hover:ring-2 hover:ring-accent/30"
          type="button"
          aria-label="Open user menu"
        >
          <Avatar fallback={userLabel} size="md" />
          <ChevronDown className="hidden size-3.5 text-muted-foreground group-hover:text-foreground sm:block" />
        </button>
        <div className="invisible absolute right-0 z-50 mt-2 w-48 origin-top-right rounded-xl border border-border bg-card p-1 shadow-lg transition-all group-hover:visible group-hover:opacity-100">
          <Link
            className="flex items-center gap-2 rounded-lg px-3 py-2 text-sm font-medium text-foreground transition hover:bg-muted"
            href="/profile"
          >
            <User className="size-4" />
            Profile
          </Link>
          <Link
            className="flex items-center gap-2 rounded-lg px-3 py-2 text-sm font-medium text-foreground transition hover:bg-muted"
            href="/settings"
          >
            <Settings className="size-4" />
            Settings
          </Link>
          <button
            className="flex w-full items-center gap-2 rounded-lg px-3 py-2 text-sm font-medium text-error transition hover:bg-error/10"
            onClick={handleSignOut}
            type="button"
          >
            <LogOut className="size-4" />
            Sign out
          </button>
        </div>
      </div>
    </div>
  );
};
