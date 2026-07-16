import { PasswordSetupForm } from "@/src/components/auth/password-setup-form";

type ResetPasswordPageProps = {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

const firstValue = (value: string | string[] | undefined) =>
  Array.isArray(value) ? value[0] ?? "" : value ?? "";

export default async function ResetPasswordPage({ searchParams }: ResetPasswordPageProps) {
  const values = await searchParams;
  return (
    <PasswordSetupForm
      areaName={firstValue(values.area)}
      resetToken={firstValue(values.token)}
      userId={Number(firstValue(values.userId))}
    />
  );
}
