import type { Membership } from "@/src/providers";

export const getMembershipNameById = (
  memberships: Membership[],
  membershipId: number | null,
  emptyLabel: string,
) => {
  if (membershipId === null) {
    return emptyLabel;
  }

  return (
    memberships.find((membership) => membership.id === membershipId)?.name ??
    `Membership ${membershipId}`
  );
};
