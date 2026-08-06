import axios from "axios";
import { beforeEach, describe, expect, it, vi } from "vitest";

import {
  acceptInternalAccountInvitation,
  validateInternalAccountInvitation,
} from "./internal-account-invitation-service";

vi.mock("axios", () => ({
  default: { isAxiosError: vi.fn(), post: vi.fn() },
}));

const response = <T,>(result: T) => ({
  data: {
    __abp: true as const,
    error: null,
    result,
    success: true,
    targetUrl: null,
    unAuthorizedRequest: false,
  },
});

describe("internal-account-invitation-service", () => {
  beforeEach(() => vi.clearAllMocks());

  it("validates an invitation through the public invitation endpoint", async () => {
    const preview = { accessLevel: "Area Administrator", areaDisplayName: "Johannesburg", areaName: "Joburg", expiresAt: "2026-08-10T10:00:00Z", inviteeName: "New Admin", status: "Pending" as const, username: "admin@example.com" };
    vi.mocked(axios.post).mockResolvedValue(response(preview));

    await expect(validateInternalAccountInvitation("invite-code", "setup-token")).resolves.toEqual(preview);
    expect(axios.post).toHaveBeenCalledWith(
      expect.stringMatching(/\/api\/services\/app\/InternalAccountInvitation\/Validate$/),
      { invitationCode: "invite-code", setupToken: "setup-token" },
    );
  });

  it("accepts an invitation with the chosen password", async () => {
    vi.mocked(axios.post).mockResolvedValue(response({ areaName: "Joburg", wasAlreadyAccepted: false }));

    await expect(acceptInternalAccountInvitation("invite-code", "setup-token", "CustomerChosen123!"))
      .resolves.toEqual({ areaName: "Joburg", wasAlreadyAccepted: false });
    expect(axios.post).toHaveBeenCalledWith(
      expect.stringMatching(/\/api\/services\/app\/InternalAccountInvitation\/Accept$/),
      { invitationCode: "invite-code", newPassword: "CustomerChosen123!", setupToken: "setup-token" },
    );
  });
});
