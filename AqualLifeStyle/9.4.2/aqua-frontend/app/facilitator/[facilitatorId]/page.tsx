import { FacilitatorDetails } from "@/src/components/facilitators/facilitator-details";

type FacilitatorDetailsPageProps = {
  params: Promise<{
    facilitatorId: string;
  }>;
};

export default async function FacilitatorDetailsPage({
  params,
}: FacilitatorDetailsPageProps) {
  const { facilitatorId } = await params;

  return <FacilitatorDetails facilitatorId={Number(facilitatorId)} />;
}
