import { TenantSelfRegistrationGate } from "@/src/components/auth/tenant-self-registration-gate";

type SignupPageProps = {
  searchParams: Promise<{
    area?: string | string[];
    redirect?: string | string[];
  }>;
};

export default async function SignupPage({ searchParams }: SignupPageProps) {
  const resolvedSearchParams = await searchParams;
  const area = resolvedSearchParams.area;
  const redirect = resolvedSearchParams.redirect;
  const requestedTenancyName = typeof area === "string" ? area : undefined;
  const requestedRedirect =
    typeof redirect === "string" &&
    redirect.startsWith("/") &&
    !redirect.startsWith("//")
      ? redirect
      : undefined;

  return (
    <TenantSelfRegistrationGate
      redirectPath={requestedRedirect}
      requestedTenancyName={requestedTenancyName}
    />
  );
}
