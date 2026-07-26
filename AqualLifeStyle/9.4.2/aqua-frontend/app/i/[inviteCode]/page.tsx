import { ProgrammeInvitationLanding } from "@/src/components/members/programme-invitation-landing";

export default async function InvitationPage({
  params,
}: {
  params: Promise<{ inviteCode: string }>;
}) {
  const { inviteCode } = await params;
  return <ProgrammeInvitationLanding inviteCode={inviteCode} />;
}
