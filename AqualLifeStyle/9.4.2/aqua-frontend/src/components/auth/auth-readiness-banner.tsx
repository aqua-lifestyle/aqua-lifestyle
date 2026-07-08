"use client";

import { useAuthState } from "@/src/providers";
import { Badge } from "@/src/shared/ui";

export const AuthReadinessBanner = () => {
  const { isAuthenticated, session } = useAuthState();
  const userLabel =
    session?.user?.name ?? session?.user?.email ?? session?.user?.id ?? null;

  return (
    <aside className="border-b border-amber-200 bg-amber-50 px-6 py-3 text-amber-950 sm:px-8 lg:px-12">
      <div className="mx-auto flex w-full max-w-7xl flex-col gap-2 lg:flex-row lg:items-center lg:justify-between">
        <div className="flex flex-col gap-1">
          <div className="flex flex-wrap items-center gap-2">
            <p className="text-sm font-semibold">Authentication context</p>
            <Badge tone={isAuthenticated ? "success" : "neutral"}>
              {isAuthenticated ? "Signed in" : "Anonymous demo"}
            </Badge>
          </div>
          <p className="text-sm leading-6 text-amber-900">
            {isAuthenticated
              ? `Requests include a bearer token for ${userLabel ?? "the active user"}.`
              : "OIDC login is not wired yet, so current demo requests run without a bearer token."}
          </p>
        </div>

        <p className="max-w-xl text-sm leading-6 text-amber-900">
          Keep protected workflows behind this boundary until Authorization Code
          Flow with PKCE and token refresh are connected.
        </p>
      </div>
    </aside>
  );
};
