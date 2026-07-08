import { MembershipDetails } from "@/src/components/memberships/membership-details";

type MembershipDetailsPageProps = {
  params: Promise<{
    membershipId: string;
  }>;
};

export default async function MembershipDetailsPage({
  params,
}: MembershipDetailsPageProps) {
  const { membershipId } = await params;

  return <MembershipDetails membershipId={Number(membershipId)} />;
}
