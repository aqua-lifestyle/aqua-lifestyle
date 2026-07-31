import { AccountEmailRequestForm } from "@/src/components/auth/account-email-request-form";
import { publicEnv } from "@/src/shared/config";

type Props = { searchParams: Promise<Record<string, string | string[] | undefined>> };
const first = (value: string | string[] | undefined) => Array.isArray(value) ? value[0] ?? "" : value ?? "";

export default async function ForgotPasswordPage({ searchParams }: Props) {
  const values = await searchParams;
  return <AccountEmailRequestForm
    areaName={first(values.area) || publicEnv.NEXT_PUBLIC_DEFAULT_TENANT_NAME}
    purpose="password-reset"
    redirectPath={first(values.redirect) || undefined}
  />;
}
