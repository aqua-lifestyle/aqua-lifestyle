import { AreaSpaceDetails } from "@/src/components/area-leaders/area-space-details";

type AreaSpaceDetailsPageProps = {
  params: Promise<{
    areaSpaceId: string;
  }>;
};

export default async function AreaSpaceDetailsPage({
  params,
}: AreaSpaceDetailsPageProps) {
  const { areaSpaceId } = await params;

  return <AreaSpaceDetails areaSpaceId={Number(areaSpaceId)} />;
}
