import type { Membership, MembershipType } from "@/src/providers";

const membershipTypeLabels: Record<MembershipType, string> = {
  0: "Jasper",
  1: "Onyx",
  2: "AQGreen",
  3: "Business Premier",
};

export const getMembershipTypeLabel = (membershipType: MembershipType) => {
  return membershipTypeLabels[membershipType];
};

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
