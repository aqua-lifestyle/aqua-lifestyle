import { ReferralDetails } from "@/src/components/facilitators/referral-details";

type ReferralDetailsPageProps = {
  params: Promise<{
    referralId: string;
  }>;
};

export default async function ReferralDetailsPage({
  params,
}: ReferralDetailsPageProps) {
  const { referralId } = await params;

  return <ReferralDetails referralId={Number(referralId)} />;
}
