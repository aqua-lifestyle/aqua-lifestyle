import { EnquiryCreateForm } from "@/src/components/enquiries/enquiry-create-form";

type CreateEnquiryPageProps = {
  searchParams: Promise<{
    customerId?: string;
    productId?: string;
  }>;
};

const toPositiveNumber = (value: string | undefined) => {
  const numberValue = Number(value);

  return Number.isInteger(numberValue) && numberValue > 0 ? numberValue : null;
};

export default async function CreateEnquiryPage({
  searchParams,
}: CreateEnquiryPageProps) {
  const { customerId, productId } = await searchParams;

  return (
    <EnquiryCreateForm
      initialCustomerId={toPositiveNumber(customerId)}
      initialProductId={toPositiveNumber(productId)}
    />
  );
}
