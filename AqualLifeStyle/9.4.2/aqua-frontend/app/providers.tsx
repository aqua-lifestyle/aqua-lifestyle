"use client";

import dynamic from "next/dynamic";
import { usePathname } from "next/navigation";
import type { ReactNode } from "react";

import { AuthProvider, useAuthState } from "@/src/providers/Auth";
import { SystemHealthProvider } from "@/src/providers/SystemHealth";
import { TenantProvider, useTenantState } from "@/src/providers/Tenant";
import { ToastProvider } from "@/src/providers/Toast";

type AppProvidersProps = {
  children: ReactNode;
};

type ProviderScope = "catalog" | "platform" | "shell";

const shellRoutes = new Set([
  "/",
  "/contact",
  "/forgot-password",
  "/login",
  "/signup",
  "/verify-email",
  "/verify-email-sent",
]);

export const getProviderScope = (pathname: string): ProviderScope => {
  if (
    shellRoutes.has(pathname) ||
    pathname.startsWith("/i/") ||
    pathname.startsWith("/reset-password")
  ) {
    return "shell";
  }

  if (pathname === "/catalog") {
    return "catalog";
  }

  return "platform";
};

const CatalogProvider = dynamic(() =>
  import("./catalog-provider").then((module) => module.CatalogProvider),
);
const PlatformProviders = dynamic(() =>
  import("./platform-providers").then((module) => module.PlatformProviders),
);

const RouteProviders = ({ children }: { children: ReactNode }) => {
  const pathname = usePathname();
  const { currentTenant } = useTenantState();
  const { session } = useAuthState();
  const tenant = currentTenant ?? "host";
  const dataScope = getDataScopeKey(tenant, session?.user?.id);
  const providerScope = getProviderScope(pathname);

  if (pathname.startsWith("/i/")) {
    return <SystemHealthProvider>{children}</SystemHealthProvider>;
  }

  if (providerScope === "shell") {
    return children;
  }

  if (providerScope === "catalog") {
    return <CatalogProvider dataScope={dataScope}>{children}</CatalogProvider>;
  }

  return <PlatformProviders dataScope={dataScope}>{children}</PlatformProviders>;
};

export const getDataScopeKey = (
  tenant: string | null,
  userId: number | undefined,
) => `${tenant ?? "host"}:${userId ?? "anonymous"}`;

export const AppProviders = ({ children }: AppProvidersProps) => {
  return (
    <AuthProvider>
      <TenantProvider>
        <ToastProvider>
          <RouteProviders>{children}</RouteProviders>
        </ToastProvider>
      </TenantProvider>
    </AuthProvider>
  );
};
