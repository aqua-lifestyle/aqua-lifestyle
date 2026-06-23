"use client";

import {
  Building2,
  Check,
  ChevronDown,
  History,
  Loader2,
  Search,
  X,
} from "lucide-react";
import { FormEvent, useEffect, useMemo, useRef, useState } from "react";
import { z } from "zod";

import { useTenantActions, useTenantState, useToast } from "@/src/providers";
import { Button } from "@/src/shared/ui";
import { cn } from "@/src/shared/lib/utils";

const TENANT_STORAGE_KEY = "aqua.currentTenant";
const RECENT_TENANTS_KEY = "aqua.recentTenants";

const tenantSchema = z
  .string()
  .trim()
  .max(64, "Tenant name must be 64 characters or fewer.")
  .regex(
    /^[a-zA-Z0-9][a-zA-Z0-9._-]*$/,
    "Use letters, numbers, dots, underscores, or hyphens.",
  );

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

const readRecentTenants = (): string[] => {
  try {
    const raw = window.localStorage.getItem(RECENT_TENANTS_KEY);
    const parsed = raw ? (JSON.parse(raw) as unknown) : [];
    return Array.isArray(parsed)
      ? parsed.filter((item): item is string => typeof item === "string")
      : [];
  } catch {
    return [];
  }
};

const writeRecentTenants = (tenants: string[]) => {
  try {
    window.localStorage.setItem(
      RECENT_TENANTS_KEY,
      JSON.stringify(tenants.slice(0, 5)),
    );
  } catch {
    // Ignore storage errors.
  }
};

const demoTenants = ["tenant-a", "tenant-b", "demo-club"];

export const TenantSwitcher = () => {
  const { clearTenant, setTenant } = useTenantActions();
  const { currentTenant, isHost } = useTenantState();
  const { toast } = useToast();
  const [isOpen, setIsOpen] = useState(false);
  const [isSwitching, setIsSwitching] = useState(false);
  const [tenantInput, setTenantInput] = useState(currentTenant ?? "");
  const [errorMessage, setErrorMessage] = useState<string | undefined>();
  const [mounted, setMounted] = useState(false);
  const containerRef = useRef<HTMLDivElement>(null);

  const recentTenants = useMemo(() => {
    if (currentTenant) {
      const all = [
        currentTenant,
        ...readRecentTenants().filter((t) => t !== currentTenant),
      ];
      return all.slice(0, 5);
    }
    return readRecentTenants();
  }, [currentTenant]);

  useEffect(() => {
    writeStoredTenant(currentTenant);
    writeRecentTenants(recentTenants);
  }, [currentTenant, recentTenants]);

  useEffect(() => {
    const handleClickOutside = (event: MouseEvent) => {
      if (
        containerRef.current &&
        !containerRef.current.contains(event.target as Node)
      ) {
        setIsOpen(false);
      }
    };
    document.addEventListener("mousedown", handleClickOutside);
    return () => document.removeEventListener("mousedown", handleClickOutside);
  }, []);

  const openSwitcher = () => {
    setTenantInput(currentTenant ?? "");
    setErrorMessage(undefined);
    setIsOpen(true);
  };

  const toggleSwitcher = () => {
    if (!isOpen) {
      openSwitcher();
    } else {
      setIsOpen(false);
    }
  };

  const handleSubmit = (event: FormEvent) => {
    event.preventDefault();

    const parsed = tenantSchema.safeParse(tenantInput);
    if (!parsed.success) {
      setErrorMessage(parsed.error.issues[0]?.message);
      return;
    }

    setErrorMessage(undefined);
    setIsOpen(false);
    setIsSwitching(true);
    toast({ message: "Switching tenant...", type: "info" });

    setTimeout(() => {
      setTenant(parsed.data);
      setIsSwitching(false);
      toast({
        message: `Switched to tenant ${parsed.data}`,
        title: "Tenant updated",
        type: "success",
      });
    }, 600);
  };

  const handleUseHost = () => {
    setIsOpen(false);
    setIsSwitching(true);
    toast({ message: "Switching to host mode...", type: "info" });

    setTimeout(() => {
      clearTenant();
      setIsSwitching(false);
      toast({
        message: "Switched to host mode",
        title: "Tenant updated",
        type: "success",
      });
    }, 600);
  };

  const selectTenant = (tenant: string) => {
    setTenantInput(tenant);
    setErrorMessage(undefined);
  };

  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setMounted(true);
  }, []);

  const currentLabel = isHost ? "Host mode" : currentTenant ?? "Host mode";

  return (
    <>
      <div ref={containerRef} className="relative">
        <button
          aria-expanded={isOpen}
          aria-haspopup="true"
          className={cn(
            "flex items-center gap-2 rounded-full border border-border bg-card px-3 py-1.5 text-sm font-semibold transition hover:border-accent/50 hover:bg-muted",
            isOpen && "border-accent/50 bg-muted",
          )}
          onClick={toggleSwitcher}
          type="button"
        >
          <Building2 className="size-4 text-accent" />
          {mounted ? (
            <span className="hidden sm:inline">{currentLabel}</span>
          ) : (
            <span className="hidden sm:inline">Host mode</span>
          )}
          <ChevronDown
            className={cn(
              "size-4 text-muted-foreground transition-transform",
              isOpen && "rotate-180",
            )}
          />
        </button>

        {isOpen ? (
          <div className="absolute right-0 z-50 mt-2 w-80 overflow-hidden rounded-xl border border-border bg-card p-4 shadow-lg animate-fade-in">
            <div className="flex items-center justify-between gap-2">
              <h3 className="text-sm font-semibold">Switch tenant</h3>
              <button
                aria-label="Close tenant switcher"
                className="rounded-md p-1 text-muted-foreground transition hover:bg-muted"
                onClick={() => setIsOpen(false)}
                type="button"
              >
                <X className="size-4" />
              </button>
            </div>
            <p className="mt-1 text-xs text-muted-foreground">
              Sends ABP{"'"}s <span className="font-mono">__tenant</span> header
              on API calls.
            </p>

            <form className="mt-4 space-y-3" onSubmit={handleSubmit}>
              <div className="relative">
                <Search className="absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
                <input
                  className={cn(
                    "w-full rounded-lg border border-border bg-muted py-2 pl-9 pr-3 text-sm text-foreground outline-none transition",
                    "focus:border-accent focus:ring-2 focus:ring-accent/20",
                    errorMessage && "border-error",
                  )}
                  onChange={(event) => {
                    setTenantInput(event.target.value);
                    setErrorMessage(undefined);
                  }}
                  placeholder="tenant-a"
                  value={tenantInput}
                />
              </div>
              {errorMessage ? (
                <p className="text-xs text-error">{errorMessage}</p>
              ) : null}

              <div className="flex gap-2">
                <Button
                  className="flex-1"
                  disabled={tenantInput.trim().length === 0 || isSwitching}
                  size="sm"
                  type="submit"
                  variant="primary"
                >
                  {isSwitching ? (
                    <Loader2 className="size-4 animate-spin" />
                  ) : (
                    "Switch"
                  )}
                </Button>
                <Button
                  className="flex-1"
                  disabled={isHost || isSwitching}
                  onClick={handleUseHost}
                  size="sm"
                  type="button"
                  variant="outline"
                >
                  Use host
                </Button>
              </div>
            </form>

            {recentTenants.length > 0 ? (
              <div className="mt-4">
                <div className="flex items-center gap-2 text-xs font-semibold text-muted-foreground">
                  <History className="size-3" />
                  <span>Recent tenants</span>
                </div>
                <div className="mt-2 space-y-1">
                  {recentTenants.map((tenant) => (
                    <button
                      key={tenant}
                      className="flex w-full items-center justify-between rounded-lg px-2 py-1.5 text-sm text-foreground transition hover:bg-muted"
                      onClick={() => selectTenant(tenant)}
                      type="button"
                    >
                      {tenant}
                      {tenant === currentTenant ? (
                        <Check className="size-4 text-success" />
                      ) : null}
                    </button>
                  ))}
                </div>
              </div>
            ) : null}

            <div className="mt-4">
              <div className="text-xs font-semibold text-muted-foreground">
                Suggestions
              </div>
              <div className="mt-2 flex flex-wrap gap-1.5">
                {demoTenants.map((tenant) => (
                  <button
                    key={tenant}
                    className="rounded-full bg-muted px-2.5 py-1 text-xs font-medium text-foreground transition hover:bg-muted-foreground/20"
                    onClick={() => selectTenant(tenant)}
                    type="button"
                  >
                    {tenant}
                  </button>
                ))}
              </div>
            </div>
          </div>
        ) : null}
      </div>

      {isSwitching ? (
        <div className="fixed inset-0 z-[90] flex flex-col items-center justify-center bg-background/80 backdrop-blur-sm transition-all">
          <Loader2 className="size-10 animate-spin text-accent" />
          <p className="mt-4 text-sm font-semibold text-foreground">
            Switching tenant...
          </p>
        </div>
      ) : null}
    </>
  );
};
