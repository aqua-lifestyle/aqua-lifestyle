import { AuthenticatedPage } from "@/src/components/auth/authenticated-page";
import { InviteClubMembers } from "@/src/components/members/invite-club-members";

export default function InviteClubMembersPage() {
  return (
    <AuthenticatedPage>
      <InviteClubMembers />
    </AuthenticatedPage>
  );
}
