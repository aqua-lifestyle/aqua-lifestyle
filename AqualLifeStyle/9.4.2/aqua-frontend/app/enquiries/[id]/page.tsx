import { EnquiryDetails } from "@/src/components/enquiries/enquiry-details";

type EnquiryDetailsPageProps = {
  params: Promise<{
    id: string;
  }>;
};

export default async function EnquiryDetailsPage({
  params,
}: EnquiryDetailsPageProps) {
  const { id } = await params;

  return <EnquiryDetails enquiryId={Number(id)} />;
}
