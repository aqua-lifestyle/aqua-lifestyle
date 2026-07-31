"use client";

import { User } from "lucide-react";
import { type FormEvent, useEffect, useState } from "react";
import { z } from "zod";

import { AuthenticatedPage } from "@/src/components/auth/authenticated-page";
import { useAuthActions, useAuthState } from "@/src/providers";
import { apiEndpoints, httpClient } from "@/src/shared/api";
import { getRequestErrorMessage } from "@/src/shared/api/abp-error";
import {
  Avatar,
  Breadcrumb,
  Button,
  Card,
  StatusMessage,
  TextAreaField,
  TextField,
} from "@/src/shared/ui";
import { customerContactNumberSchema, customerEmailSchema, customerFirstNameSchema, customerHomeAddressSchema, customerSurnameSchema } from "@/src/shared/validation/customer-personal-details";

type CustomerProfile = {
  contactNumber: string | null;
  emailAddress: string;
  firstName: string;
  homeAddress: string | null;
  surname: string;
};

const profileSchema = z.object({
  contactNumber: customerContactNumberSchema,
  emailAddress: customerEmailSchema,
  firstName: customerFirstNameSchema,
  homeAddress: customerHomeAddressSchema,
  surname: customerSurnameSchema,
});

export default function ProfilePage() {
  return (
    <AuthenticatedPage>
      <ProfileContent />
    </AuthenticatedPage>
  );
}

function ProfileContent() {
  const { session } = useAuthState();
  const { setSession } = useAuthActions();
  const authenticatedUserId = session?.user?.id;
  const [isEditing, setIsEditing] = useState(false);
  const [profile, setProfile] = useState<CustomerProfile | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [isSaving, setIsSaving] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);
  const [saveSuccess, setSaveSuccess] = useState(false);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});

  useEffect(() => {
    if (!authenticatedUserId) return;
    let active = true;
    void httpClient.get<CustomerProfile>(apiEndpoints.myAccount.getProfile)
      .then((result) => { if (active) { setProfile(result); setSaveError(null); } })
      .catch((error) => { if (active) setSaveError(getRequestErrorMessage(error, "Your profile could not be loaded.")); })
      .finally(() => { if (active) setIsLoading(false); });
    return () => { active = false; };
  }, [authenticatedUserId]);

  if (!session) {
    return null;
  }

  const handleSave = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const data = new FormData(event.currentTarget);
    const parsed = profileSchema.safeParse(Object.fromEntries(data));
    if (!parsed.success) {
      setFieldErrors(Object.fromEntries(parsed.error.issues.map((issue) => [String(issue.path[0]), issue.message])));
      return;
    }
    setIsSaving(true);
    setFieldErrors({});
    setSaveError(null);
    setSaveSuccess(false);
    try {
      const updated = await httpClient.put<CustomerProfile, typeof parsed.data>(apiEndpoints.myAccount.updateProfile, parsed.data);
      setProfile(updated);
      setSession({ ...session, user: session.user ? { ...session.user, email: updated.emailAddress, name: `${updated.firstName} ${updated.surname}` } : null });
      setSaveSuccess(true);
      setIsEditing(false);
    } catch (error) {
      setSaveError(getRequestErrorMessage(error, "Your profile could not be updated. No changes were saved."));
    } finally {
      setIsSaving(false);
    }
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

              {isLoading ? <p className="text-sm text-muted-foreground">Loading your profile…</p> : null}
              {!isLoading && profile && isEditing ? (
                <form className="grid gap-4 sm:grid-cols-2" noValidate onSubmit={handleSave}>
                  <TextField
                    autoComplete="given-name"
                    defaultValue={profile.firstName}
                    errorMessage={fieldErrors.firstName}
                    label="First name"
                    name="firstName"
                    required
                  />
                  <TextField
                    autoComplete="family-name"
                    defaultValue={profile.surname}
                    errorMessage={fieldErrors.surname}
                    label="Surname"
                    name="surname"
                    required
                  />
                  <TextField
                    autoComplete="email"
                    className="sm:col-span-2"
                    defaultValue={profile.emailAddress}
                    errorMessage={fieldErrors.emailAddress}
                    label="Email address (contact support to change)"
                    name="emailAddress"
                    readOnly
                    required
                    type="email"
                  />
                  <TextField autoComplete="tel" className="sm:col-span-2" defaultValue={profile.contactNumber ?? ""} errorMessage={fieldErrors.contactNumber} label="Contact number" name="contactNumber" required type="tel" />
                  <TextAreaField autoComplete="street-address" className="sm:col-span-2" defaultValue={profile.homeAddress ?? ""} errorMessage={fieldErrors.homeAddress} label="Home address" name="homeAddress" required rows={3} />
                  {saveError ? (
                    <StatusMessage className="sm:col-span-2" tone="error">{saveError}</StatusMessage>
                  ) : null}
                  <div className="flex gap-2 sm:col-span-2">
                    <Button
                      disabled={isSaving}
                      isLoading={isSaving}
                      type="submit"
                      variant="primary"
                    >
                      Save changes
                    </Button>
                    <Button
                      onClick={() => {
                        setIsEditing(false);
                        setFieldErrors({});
                        setSaveError(null);
                      }}
                      variant="outline"
                    >
                      Cancel
                    </Button>
                  </div>
                </form>
              ) : !isLoading && profile ? (
                <div className="flex flex-col gap-4">
                  {saveSuccess ? (
                    <StatusMessage tone="success">
                      Profile updated successfully.
                    </StatusMessage>
                  ) : null}
                  <div className="grid gap-4 sm:grid-cols-2">
                    <div>
                      <p className="text-sm text-muted-foreground">First name</p>
                      <p className="font-medium">{profile.firstName}</p>
                    </div>
                    <div>
                      <p className="text-sm text-muted-foreground">Surname</p>
                      <p className="font-medium">{profile.surname}</p>
                    </div>
                    <div>
                      <p className="text-sm text-muted-foreground">Email</p>
                      <p className="font-medium">{profile.emailAddress}</p>
                    </div>
                    <div>
                      <p className="text-sm text-muted-foreground">Contact number</p>
                      <p className="font-medium">{profile.contactNumber ?? "Not provided"}</p>
                    </div>
                    <div>
                      <p className="text-sm text-muted-foreground">Home address</p>
                      <p className="whitespace-pre-line font-medium">{profile.homeAddress ?? "Not provided"}</p>
                    </div>
                  </div>
                  <div>
                    <Button
                      onClick={() => {
                        setIsEditing(true);
                        setSaveSuccess(false);
                      }}
                      variant="outline"
                    >
                      Edit profile
                    </Button>
                  </div>
                </div>
              ) : !isLoading && saveError ? (
                <StatusMessage tone="error">{saveError}</StatusMessage>
              ) : null}
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
