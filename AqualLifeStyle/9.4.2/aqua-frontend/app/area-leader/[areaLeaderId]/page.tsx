import { AreaLeaderDetails } from "@/src/components/area-leaders/area-leader-details";

type AreaLeaderDetailsPageProps = {
  params: Promise<{
    areaLeaderId: string;
  }>;
};

export default async function AreaLeaderDetailsPage({
  params,
}: AreaLeaderDetailsPageProps) {
  const { areaLeaderId } = await params;

  return <AreaLeaderDetails areaLeaderId={Number(areaLeaderId)} />;
}
