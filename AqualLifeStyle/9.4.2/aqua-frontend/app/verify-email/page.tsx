import { VerifyEmailResult } from "@/src/components/auth/verify-email-result";

type Props = { searchParams: Promise<Record<string, string | string[] | undefined>> };
const first = (value: string | string[] | undefined) => Array.isArray(value) ? value[0] ?? "" : value ?? "";

export default async function VerifyEmailPage({ searchParams }: Props) {
  const values = await searchParams;
  return <VerifyEmailResult
    tenantId={Number(first(values.tenantId))}
    userId={Number(first(values.userId))}
    areaName={first(values.area)}
    redirectPath={first(values.redirect)}
  />;
}
