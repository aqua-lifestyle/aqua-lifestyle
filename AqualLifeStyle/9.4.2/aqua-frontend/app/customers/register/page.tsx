import { CustomerRegistrationForm } from "@/src/components/customers/customer-registration-form";

type CustomerRegistrationPageProps = {
  searchParams: Promise<{
    membershipId?: string;
  }>;
};

const toPositiveNumber = (value: string | undefined) => {
  const numberValue = Number(value);

  return Number.isInteger(numberValue) && numberValue > 0 ? numberValue : null;
};

export default async function CustomerRegistrationPage({
  searchParams,
}: CustomerRegistrationPageProps) {
  const { membershipId } = await searchParams;

  return (
    <CustomerRegistrationForm
      initialMembershipId={toPositiveNumber(membershipId)}
    />
  );
}
