import { CustomerDetails } from "@/src/components/customers/customer-details";

type CustomerDetailsPageProps = {
  params: Promise<{
    id: string;
  }>;
};

export default async function CustomerDetailsPage({
  params,
}: CustomerDetailsPageProps) {
  const { id } = await params;

  return <CustomerDetails customerId={Number(id)} />;
}
