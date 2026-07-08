import { CustomerDetails } from "@/src/components/customers/customer-details";

type CustomerDetailsPageProps = {
  params: Promise<{
    customerId: string;
  }>;
};

export default async function CustomerDetailsPage({
  params,
}: CustomerDetailsPageProps) {
  const { customerId } = await params;

  return <CustomerDetails customerId={Number(customerId)} />;
}
