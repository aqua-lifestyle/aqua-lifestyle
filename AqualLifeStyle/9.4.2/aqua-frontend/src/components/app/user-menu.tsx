"use client";

import { ChevronDown, LogOut, Settings, User } from "lucide-react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect, useRef, useState } from "react";

import { useAuthActions, useAuthState } from "@/src/providers";
import { Avatar } from "@/src/shared/ui";

export const UserMenu = ({ inverted = false }: { inverted?: boolean }) => {
  const router = useRouter();
  const menuRef = useRef<HTMLDivElement>(null);
  const triggerRef = useRef<HTMLButtonElement>(null);
  const [isOpen, setIsOpen] = useState(false);
  const { isAuthenticated, session } = useAuthState();
  const { clearSession } = useAuthActions();

  useEffect(() => {
    if (!isOpen) return;

    const handlePointerDown = (event: PointerEvent) => {
      if (!menuRef.current?.contains(event.target as Node)) setIsOpen(false);
    };
    const handleKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        setIsOpen(false);
        triggerRef.current?.focus();
      }
    };

    document.addEventListener("pointerdown", handlePointerDown);
    document.addEventListener("keydown", handleKeyDown);
    return () => {
      document.removeEventListener("pointerdown", handlePointerDown);
      document.removeEventListener("keydown", handleKeyDown);
    };
  }, [isOpen]);

  const handleSignOut = () => {
    clearSession();
    router.replace("/login");
  };

  const userLabel =
    session?.user?.name ?? session?.user?.email ?? "Demo user";

  if (!isAuthenticated) {
    return (
      <div className="flex items-center">
        <Link
          className={`inline-flex h-11 items-center rounded-aqua-control px-4 text-sm font-semibold text-white transition-colors focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 ${
            inverted
              ? "bg-aqua-violet hover:bg-aqua-violet-dark focus-visible:outline-aqua-lavender-strong"
              : "bg-accent-dark hover:bg-accent focus-visible:outline-accent"
          }`}
          href="/login"
        >
          Member access
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
      <div className="relative" ref={menuRef}>
        <button
          aria-controls="user-menu-panel"
          aria-expanded={isOpen}
          aria-label="Open user menu"
          className={`flex min-h-11 min-w-11 items-center justify-center gap-1.5 rounded-full transition hover:ring-2 hover:ring-accent/30 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 ${
            inverted ? "focus-visible:outline-aqua-lavender-strong" : "focus-visible:outline-accent"
          }`}
          onClick={() => setIsOpen((current) => !current)}
          ref={triggerRef}
          type="button"
        >
          <Avatar fallback={userLabel} size="md" />
          <ChevronDown
            className={`hidden size-3.5 transition-transform sm:block ${
              inverted ? "text-white/65" : "text-muted-foreground"
            } ${isOpen ? "rotate-180" : ""}`}
          />
        </button>
        {isOpen ? (
          <div
            aria-label="User menu"
            className="absolute right-0 z-50 mt-2 w-48 origin-top-right rounded-xl border border-border bg-card p-1 shadow-lg animate-fade-in"
            id="user-menu-panel"
          >
            <Link
              className="flex min-h-11 items-center gap-2 rounded-lg px-3 py-2 text-sm font-medium text-foreground transition hover:bg-muted focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
              href="/profile"
              onClick={() => setIsOpen(false)}
            >
              <User className="size-4" />
              Profile
            </Link>
            <Link
              className="flex min-h-11 items-center gap-2 rounded-lg px-3 py-2 text-sm font-medium text-foreground transition hover:bg-muted focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
              href="/settings"
              onClick={() => setIsOpen(false)}
            >
              <Settings className="size-4" />
              Settings
            </Link>
            <button
              className="flex min-h-11 w-full items-center gap-2 rounded-lg px-3 py-2 text-sm font-medium text-error transition hover:bg-error/10 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-error"
              onClick={handleSignOut}
              type="button"
            >
              <LogOut className="size-4" />
              Sign out
            </button>
          </div>
        ) : null}
      </div>
    </div>
  );
};
