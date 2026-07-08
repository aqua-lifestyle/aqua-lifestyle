import { CustomerRegistrationForm } from "@/src/components/customers/customer-registration-form";
import { toPositiveNumberOrNull } from "@/src/shared/routing";

type CustomerRegistrationPageProps = {
  searchParams: Promise<{
    membershipId?: string;
  }>;
};

export default async function CustomerRegistrationPage({
  searchParams,
}: CustomerRegistrationPageProps) {
  const { membershipId } = await searchParams;

  return (
    <CustomerRegistrationForm
      initialMembershipId={toPositiveNumberOrNull(membershipId)}
    />
  );
}
