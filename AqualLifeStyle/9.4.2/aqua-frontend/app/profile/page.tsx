"use client";

import { User } from "lucide-react";
import { useState } from "react";

import { useAuthActions, useAuthState } from "@/src/providers";
import {
  Avatar,
  Breadcrumb,
  Button,
  Card,
  StatusMessage,
  TextField,
} from "@/src/shared/ui";

export default function ProfilePage() {
  const { session } = useAuthState();
  const { setSession } = useAuthActions();
  const [isEditing, setIsEditing] = useState(false);
  const [name, setName] = useState(session?.user?.name ?? "");
  const [email, setEmail] = useState(session?.user?.email ?? "");
  const [isSaving, setIsSaving] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);
  const [saveSuccess, setSaveSuccess] = useState(false);

  if (!session) {
    return (
      <main className="min-h-dvh bg-muted/30 px-4 py-6 text-foreground sm:px-6 lg:px-8">
        <div className="mx-auto flex w-full max-w-7xl flex-col gap-6">
          <StatusMessage tone="error">
            You must be signed in to view your profile.
          </StatusMessage>
        </div>
      </main>
    );
  }

const handleSave = () => {
    setIsSaving(true);
    setSaveError(null);
    setSaveSuccess(false);

    // Update the session in-memory. A production app would also call a
    // profile-update endpoint before updating local state.
    setSession({
      ...session,
      user: session.user
        ? { ...session.user, name, email }
        : null,
    });
    setSaveSuccess(true);
    setIsEditing(false);
    setIsSaving(false);
  };

  const sessionUser = session.user;

  return (
    <main className="min-h-dvh bg-muted/30 px-4 py-6 text-foreground sm:px-6 lg:px-8">
      <div className="mx-auto flex w-full max-w-3xl flex-col gap-6">
        <header>
          <Breadcrumb
            items={[
              { href: "/", label: "Dashboard" },
              { label: "Profile" },
            ]}
          />
          <h1 className="mt-2 text-3xl font-bold tracking-tight">Profile</h1>
          <p className="mt-2 max-w-2xl text-base text-muted-foreground">
            Manage your personal information and account preferences.
          </p>
        </header>

        {sessionUser ? (
          <Card>
            <div className="flex flex-col gap-6">
              <div className="flex items-center gap-4">
                <Avatar
                  fallback={sessionUser.name ?? sessionUser.email ?? "U"}
                  size="lg"
                />
                <div>
                  <p className="text-lg font-semibold">{sessionUser.name ?? "User"}</p>
                  <p className="text-sm text-muted-foreground">{sessionUser.email}</p>
                  <p className="text-xs text-muted-foreground">
                    Role: {sessionUser.role}
                  </p>
                </div>
              </div>

              {isEditing ? (
                <div className="flex flex-col gap-4">
                  <TextField
                    label="Name"
                    name="name"
                    onChange={(e) => setName(e.target.value)}
                    value={name}
                  />
                  <TextField
                    label="Email"
                    name="email"
                    onChange={(e) => setEmail(e.target.value)}
                    type="email"
                    value={email}
                  />
                  {saveError ? (
                    <StatusMessage tone="error">{saveError}</StatusMessage>
                  ) : null}
                  {saveSuccess ? (
                    <StatusMessage tone="success">
                      Profile updated successfully.
                    </StatusMessage>
                  ) : null}
                  <div className="flex gap-2">
                    <Button
                      disabled={isSaving}
                      isLoading={isSaving}
                      onClick={handleSave}
                      variant="primary"
                    >
                      Save changes
                    </Button>
                    <Button
                      onClick={() => {
                        setIsEditing(false);
                        setName(sessionUser.name ?? "");
                        setEmail(sessionUser.email ?? "");
                      }}
                      variant="outline"
                    >
                      Cancel
                    </Button>
                  </div>
                </div>
              ) : (
                <div className="flex flex-col gap-4">
                  <div className="grid gap-4 sm:grid-cols-2">
                    <div>
                      <p className="text-sm text-muted-foreground">Name</p>
                      <p className="font-medium">{sessionUser.name ?? "—"}</p>
                    </div>
                    <div>
                      <p className="text-sm text-muted-foreground">Email</p>
                      <p className="font-medium">{sessionUser.email ?? "—"}</p>
                    </div>
                    <div>
                      <p className="text-sm text-muted-foreground">User ID</p>
                      <p className="font-medium">{sessionUser.id}</p>
                    </div>
                    <div>
                      <p className="text-sm text-muted-foreground">Role</p>
                      <p className="font-medium">{sessionUser.role}</p>
                    </div>
                  </div>
                  <div>
                    <Button
                      onClick={() => setIsEditing(true)}
                      variant="outline"
                    >
                      Edit profile
                    </Button>
                  </div>
                </div>
              )}
            </div>
          </Card>
        ) : (
          <Card>
            <div className="flex flex-col items-center gap-4 py-8 text-center">
              <User className="size-12 text-muted-foreground" />
              <p className="text-muted-foreground">
                No user information available.
              </p>
            </div>
          </Card>
        )}
      </div>
    </main>
  );
}
