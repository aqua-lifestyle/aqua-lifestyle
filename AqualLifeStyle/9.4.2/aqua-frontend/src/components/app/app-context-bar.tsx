"use client";

import { FormEvent, useEffect, useState } from "react";
import { z } from "zod";

import {
  useAuthState,
  useSystemHealthActions,
  useSystemHealthState,
  useTenantActions,
  useTenantState,
} from "@/src/providers";
import { Badge, Button, TextField } from "@/src/shared/ui";

const TENANT_STORAGE_KEY = "aqua.currentTenant";

const tenantSchema = z
  .string()
  .trim()
  .max(64, "Tenant name must be 64 characters or fewer.")
  .regex(
    /^[a-zA-Z0-9][a-zA-Z0-9._-]*$/,
    "Use letters, numbers, dots, underscores, or hyphens.",
  );

const readStoredTenant = () => {
  try {
    return window.localStorage.getItem(TENANT_STORAGE_KEY);
  } catch {
    return null;
  }
};

const writeStoredTenant = (tenant: string | null) => {
  try {
    if (tenant) {
      window.localStorage.setItem(TENANT_STORAGE_KEY, tenant);
      return;
    }

    window.localStorage.removeItem(TENANT_STORAGE_KEY);
  } catch {
    // Tenant selection remains usable for the current session without storage.
  }
};

export const AppContextBar = () => {
  const { isAuthenticated, session } = useAuthState();
  const { checkHealth } = useSystemHealthActions();
  const {
    errorMessage: healthErrorMessage,
    health,
    isError: isHealthError,
    isPending: isHealthPending,
    isSuccess: isHealthSuccess,
  } = useSystemHealthState();
  const { clearTenant, setTenant } = useTenantActions();
  const { currentTenant, isHost } = useTenantState();
  const [tenantInput, setTenantInput] = useState(currentTenant ?? "");
  const [errorMessage, setErrorMessage] = useState<string | undefined>();

  const userLabel =
    session?.user?.name ?? session?.user?.email ?? session?.user?.id ?? null;

  useEffect(() => {
    const storedTenant = readStoredTenant();

    if (storedTenant) {
      const parsedTenant = tenantSchema.safeParse(storedTenant);

      if (parsedTenant.success) {
        setTenant(parsedTenant.data);
      } else {
        writeStoredTenant(null);
      }
    }
  }, [setTenant]);

  useEffect(() => {
    writeStoredTenant(currentTenant);
  }, [currentTenant]);

  useEffect(() => {
    void checkHealth();
  }, [checkHealth]);

  const handleSubmit = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    const parsedTenant = tenantSchema.safeParse(tenantInput);

    if (!parsedTenant.success) {
      setErrorMessage(parsedTenant.error.issues[0]?.message);
      return;
    }

    setErrorMessage(undefined);
    setTenant(parsedTenant.data);
  };

  const handleClear = () => {
    setErrorMessage(undefined);
    setTenantInput("");
    clearTenant();
  };

  return (
    <aside className="border-b border-zinc-200 bg-white px-6 py-3 text-zinc-950 sm:px-8 lg:px-12">
      <div className="mx-auto grid w-full max-w-7xl gap-4 xl:grid-cols-[1fr_auto] xl:items-end">
        <div className="grid gap-3 md:grid-cols-3 md:items-start">
          <div className="rounded-lg border border-emerald-200 bg-emerald-50 p-3">
            <div className="flex flex-wrap items-center gap-2">
              <p className="text-sm font-semibold text-emerald-950">Backend</p>
              <Badge
                tone={
                  isHealthSuccess
                    ? "success"
                    : isHealthError
                      ? "danger"
                      : "neutral"
                }
              >
                {isHealthPending
                  ? "Checking"
                  : isHealthSuccess
                    ? health?.status ?? "Reachable"
                    : "Unavailable"}
              </Badge>
            </div>
            <p className="mt-1 text-sm leading-6 text-emerald-900">
              {isHealthSuccess
                ? `${health?.environment ?? "Backend"} API ${health?.version ?? "version unknown"} is reachable.`
                : healthErrorMessage ??
                  "Checking whether the frontend can reach ABP."}
            </p>
          </div>

          <div className="rounded-lg border border-amber-200 bg-amber-50 p-3">
            <div className="flex flex-wrap items-center gap-2">
              <p className="text-sm font-semibold text-amber-950">
                Authentication
              </p>
              <Badge tone={isAuthenticated ? "success" : "neutral"}>
                {isAuthenticated ? "Signed in" : "Anonymous demo"}
              </Badge>
            </div>
            <p className="mt-1 text-sm leading-6 text-amber-900">
              {isAuthenticated
                ? `Bearer token active for ${userLabel ?? "the active user"}.`
                : "OIDC login is not wired yet; requests run without a bearer token."}
            </p>
          </div>

          <div className="rounded-lg border border-zinc-200 bg-zinc-50 p-3">
            <div className="flex flex-wrap items-center gap-2">
              <p className="text-sm font-semibold text-zinc-950">Tenant</p>
              <Badge tone={isHost ? "neutral" : "success"}>
                {isHost ? "Host mode" : currentTenant}
              </Badge>
            </div>
            <p className="mt-1 text-sm leading-6 text-zinc-600">
              Tenant mode sends ABP{"'"}s{" "}
              <span className="font-mono">__tenant</span> header on API calls.
            </p>
          </div>
        </div>

        <form
          className="grid gap-3 sm:grid-cols-[minmax(12rem,20rem)_auto_auto] sm:items-start"
          onSubmit={handleSubmit}
        >
          <TextField
            errorMessage={errorMessage}
            label="Tenant name"
            name="tenant"
            onChange={(event) => {
              setTenantInput(event.target.value);
              setErrorMessage(undefined);
            }}
            placeholder="tenant-a"
            value={tenantInput}
          />
          <Button className="sm:mt-7" type="submit">
            Use tenant
          </Button>
          <Button
            className="bg-zinc-900 hover:bg-zinc-700 sm:mt-7"
            onClick={handleClear}
            type="button"
          >
            Use host
          </Button>
        </form>
      </div>
    </aside>
  );
};
