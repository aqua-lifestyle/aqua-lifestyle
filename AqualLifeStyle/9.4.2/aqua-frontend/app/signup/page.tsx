import { SignupForm } from "@/src/components/auth/signup-form";
import { TenantSelfRegistrationGate } from "@/src/components/auth/tenant-self-registration-gate";

type SignupPageProps = {
  searchParams: Promise<{ area?: string | string[] }>;
};

export default async function SignupPage({ searchParams }: SignupPageProps) {
  const area = (await searchParams).area;
  const requestedTenancyName = typeof area === "string" ? area : undefined;

  return (
    <TenantSelfRegistrationGate requestedTenancyName={requestedTenancyName}>
      {(tenancyName) => <SignupForm tenancyName={tenancyName} />}
    </TenantSelfRegistrationGate>
  );
}
