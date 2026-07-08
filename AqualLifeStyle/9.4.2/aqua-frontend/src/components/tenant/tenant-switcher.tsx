"use client";

import { FormEvent, useEffect, useState } from "react";
import { z } from "zod";

import { useTenantActions, useTenantState } from "@/src/providers";
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

export const TenantSwitcher = () => {
  const { clearTenant, setTenant } = useTenantActions();
  const { currentTenant, isHost } = useTenantState();
  const [tenantInput, setTenantInput] = useState(currentTenant ?? "");
  const [errorMessage, setErrorMessage] = useState<string | undefined>();

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
      <div className="mx-auto flex w-full max-w-7xl flex-col gap-3 lg:flex-row lg:items-end lg:justify-between">
        <div className="flex flex-col gap-1">
          <div className="flex flex-wrap items-center gap-2">
            <p className="text-sm font-semibold">Tenant context</p>
            <Badge tone={isHost ? "neutral" : "success"}>
              {isHost ? "Host mode" : currentTenant}
            </Badge>
          </div>
          <p className="text-sm text-zinc-600">
            Sets the ABP <span className="font-mono">__tenant</span> header for
            subsequent API requests.
          </p>
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
