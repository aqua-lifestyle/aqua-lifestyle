export type ProgrammeInvitation = {
  clubMemberNumber: string;
  code: string;
  programmeKey: "AQGREEN" | "ONYX";
  programmeName: "AQGreen" | "Onyx";
};

export type MyProgrammeInvitations = {
  invitations: ProgrammeInvitation[];
};

export type ProgrammeInvitationPreview = {
  areaName: string | null;
  inviteCode: string;
  programmeKey: "AQGREEN" | "ONYX";
  programmeName: "AQGreen" | "Onyx";
  recruiterClubMemberNumber: string;
  recruiterEligible: boolean;
  recruiterName: string;
};
