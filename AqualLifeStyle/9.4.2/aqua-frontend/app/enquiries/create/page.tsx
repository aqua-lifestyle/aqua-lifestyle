import { EnquiryCreateForm } from "@/src/components/enquiries/enquiry-create-form";
import { toPositiveNumberOrNull } from "@/src/shared/routing";

type CreateEnquiryPageProps = {
  searchParams: Promise<{
    customerId?: string;
    productId?: string;
  }>;
};

export default async function CreateEnquiryPage({
  searchParams,
}: CreateEnquiryPageProps) {
  const { customerId, productId } = await searchParams;

  return (
    <EnquiryCreateForm
      initialCustomerId={toPositiveNumberOrNull(customerId)}
      initialProductId={toPositiveNumberOrNull(productId)}
    />
  );
}
