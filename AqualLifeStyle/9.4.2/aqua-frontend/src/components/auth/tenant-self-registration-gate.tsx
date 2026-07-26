"use client";

import { SignupForm } from "@/src/components/auth/signup-form";
import { useTenantState } from "@/src/providers";
import { useTenantSelfRegistrationAvailability } from "@/src/shared/auth/use-tenant-self-registration-availability";
import { publicEnv } from "@/src/shared/config";
import { Card, LinkButton } from "@/src/shared/ui";

type TenantSelfRegistrationGateProps = {
  redirectPath?: string;
  requestedTenancyName?: string;
};

export const TenantSelfRegistrationGate = ({
  redirectPath,
  requestedTenancyName,
}: TenantSelfRegistrationGateProps) => {
  const { currentTenant } = useTenantState();
  const tenancyName = requestedTenancyName?.trim() ||
    currentTenant ||
    publicEnv.NEXT_PUBLIC_DEFAULT_TENANT_NAME;
  const availability = useTenantSelfRegistrationAvailability(tenancyName);

  if (availability === "enabled") {
    return <SignupForm redirectPath={redirectPath} tenancyName={tenancyName} />;
  }

  const isLoading = availability === "loading";
  const isUnavailable = availability === "unavailable";

  return (
    <main className="min-h-dvh bg-muted/30 px-4 py-12 text-foreground sm:px-6">
      <Card className="mx-auto max-w-lg">
        <h1 className="text-2xl font-bold tracking-tight">Account registration</h1>
        <p className="mt-3 text-muted-foreground" role="status">
          {isLoading
            ? `Checking registration availability for the ${tenancyName} Area…`
            : isUnavailable
              ? "Registration availability could not be confirmed. Please try again or contact the club team for assistance."
              : "New Club Member accounts are created by an authorised Aqua Lifestyle Club administrator. Contact the club team if you need access or return to sign in if your account already exists."}
        </p>
        {!isLoading ? (
          <div className="mt-6">
            <LinkButton href="/login" variant="primary">Return to sign in</LinkButton>
          </div>
        ) : null}
      </Card>
    </main>
  );
};
