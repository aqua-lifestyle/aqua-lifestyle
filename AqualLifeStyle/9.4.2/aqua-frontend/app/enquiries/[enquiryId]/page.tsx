import { EnquiryDetails } from "@/src/components/enquiries/enquiry-details";

type EnquiryDetailsPageProps = {
  params: Promise<{
    enquiryId: string;
  }>;
};

export default async function EnquiryDetailsPage({
  params,
}: EnquiryDetailsPageProps) {
  const { enquiryId } = await params;

  return <EnquiryDetails enquiryId={Number(enquiryId)} />;
}
