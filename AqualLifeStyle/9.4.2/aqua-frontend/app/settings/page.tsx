"use client";

import { useState } from "react";

import { useAuthState, useTenantActions, useTenantState } from "@/src/providers";
import { Breadcrumb, Button, Card, StatusMessage } from "@/src/shared/ui";

export default function SettingsPage() {
  const { session } = useAuthState();
  const { currentTenant } = useTenantState();
  const { clearTenant, setTenant } = useTenantActions();
  const [notificationsEnabled, setNotificationsEnabled] = useState(true);
  const [saveSuccess, setSaveSuccess] = useState(false);

  if (!session) {
    return (
      <main className="min-h-dvh bg-muted/30 px-4 py-6 text-foreground sm:px-6 lg:px-8">
        <div className="mx-auto flex w-full max-w-7xl flex-col gap-6">
          <StatusMessage tone="error">
            You must be signed in to view settings.
          </StatusMessage>
        </div>
      </main>
    );
  }

  const handleSaveNotifications = () => {
    setSaveSuccess(true);
    setTimeout(() => setSaveSuccess(false), 3000);
  };

  return (
    <main className="min-h-dvh bg-muted/30 px-4 py-6 text-foreground sm:px-6 lg:px-8">
      <div className="mx-auto flex w-full max-w-3xl flex-col gap-6">
        <header>
          <Breadcrumb
            items={[
              { href: "/", label: "Dashboard" },
              { label: "Settings" },
            ]}
          />
          <h1 className="mt-2 text-3xl font-bold tracking-tight">Settings</h1>
          <p className="mt-2 max-w-2xl text-base text-muted-foreground">
            Manage your application preferences.
          </p>
        </header>

        <Card>
          <h2 className="text-lg font-semibold">Notifications</h2>
          <div className="mt-4 flex items-center justify-between">
            <div>
              <p className="font-medium">Email notifications</p>
              <p className="text-sm text-muted-foreground">
                Receive email updates about orders and enquiries.
              </p>
            </div>
            <label className="relative inline-flex cursor-pointer items-center">
              <input
                checked={notificationsEnabled}
                className="peer sr-only"
                onChange={() => setNotificationsEnabled((prev) => !prev)}
                type="checkbox"
              />
              <div className="h-6 w-11 rounded-full bg-muted after:absolute after:left-[2px] after:top-[2px] after:h-5 after:w-5 after:rounded-full after:bg-card after:transition-all after:content-[''] peer-checked:bg-accent peer-checked:after:translate-x-full" />
            </label>
          </div>
          <div className="mt-4">
            <Button onClick={handleSaveNotifications} variant="primary">
              Save preferences
            </Button>
          </div>
          {saveSuccess ? (
            <div className="mt-4">
              <StatusMessage tone="success">
                Preferences saved successfully.
              </StatusMessage>
            </div>
          ) : null}
        </Card>

        <Card>
          <h2 className="text-lg font-semibold">Tenant</h2>
          <p className="mt-1 text-sm text-muted-foreground">
            Current tenant: {currentTenant ?? "Host"}
          </p>
          <div className="mt-4 flex gap-2">
            <Button
              onClick={() => setTenant("default")}
              variant="outline"
            >
              Switch to default tenant
            </Button>
            {currentTenant ? (
              <Button
                onClick={clearTenant}
                variant="outline"
              >
                Switch to host mode
              </Button>
            ) : null}
          </div>
        </Card>
      </div>
    </main>
  );
}
