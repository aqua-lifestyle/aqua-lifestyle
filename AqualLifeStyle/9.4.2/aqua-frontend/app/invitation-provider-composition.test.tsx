import { render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

import { ProgrammeInvitationLanding } from "@/src/components/members/programme-invitation-landing";
import { apiEndpoints, httpClient } from "@/src/shared/api";
import { AppProviders } from "./providers";

const storedValues = new Map<string, string>();
const localStorage = {
  clear: () => storedValues.clear(),
  getItem: (key: string) => storedValues.get(key) ?? null,
  key: (index: number) => [...storedValues.keys()][index] ?? null,
  get length() {
    return storedValues.size;
  },
  removeItem: (key: string) => storedValues.delete(key),
  setItem: (key: string, value: string) => storedValues.set(key, value),
};
Object.defineProperty(window, "localStorage", {
  configurable: true,
  value: localStorage,
});

vi.mock("next/navigation", () => ({
  usePathname: () => "/i/AQ7G2X9KLMNP",
}));
vi.mock("@/src/shared/api", async () => {
  const actual = await vi.importActual<typeof import("@/src/shared/api")>(
    "@/src/shared/api",
  );
  return { ...actual, httpClient: { get: vi.fn(), post: vi.fn() } };
});

const health = {
  buildId: "test-build",
  checkedAtUtc: "2026-08-13T10:00:00Z",
  contractCapabilities: [
    "aqgreen-flexible-joining-v1",
    "programme-approval-queue-v1",
    "direct-onyx-checkout-v1",
  ],
  databaseStatus: "Healthy",
  environment: "Test",
  imageId: "test-image",
  isDatabaseReachable: true,
  paymentContractVersion: "aqua-payments-2026-08-09-flexible-payment-approval",
  releaseDate: "2026-08-13T00:00:00Z",
  status: "Healthy",
  traceId: "test-trace",
  version: "1.0.0",
};
const preview = {
  areaName: "Pretoria",
  inviteCode: "AQ7G2X9KLMNP",
  programmeKey: "AQGREEN" as const,
  programmeName: "AQGreen" as const,
  recruiterClubMemberNumber: "CLB-ABCDEFGH2345",
  recruiterEligible: true,
  recruiterName: "Ada Recruiter",
  tenancyName: "Default",
};

describe("invitation provider composition", () => {
  beforeEach(() => {
    vi.clearAllMocks();
    localStorage.clear();
    vi.mocked(httpClient.get).mockImplementation(async (url) =>
      url === apiEndpoints.health.get ? health : preview,
    );
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("renders an anonymous invitation and attempts preview with the real provider tree", async () => {
    render(
      <AppProviders>
        <ProgrammeInvitationLanding inviteCode="AQ7G2X9KLMNP" />
      </AppProviders>,
    );

    expect(await screen.findByText("Ada Recruiter")).toBeInTheDocument();
    expect(screen.getByText("Business Area: Pretoria")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: /create my account/i })).toHaveAttribute(
      "href",
      expect.stringContaining("area=Default&invite=AQ7G2X9KLMNP"),
    );
    expect(screen.getByRole("link", { name: /sign in to continue/i })).toHaveAttribute(
      "href",
      "/login?area=Default&invite=AQ7G2X9KLMNP&redirect=%2Fi%2FAQ7G2X9KLMNP",
    );
    expect(httpClient.get).toHaveBeenCalledWith(
      apiEndpoints.programmeParticipations.getInvitationPreview("AQ7G2X9KLMNP"),
    );
  });

  it("keeps authenticated payment fail-closed until compatible health is verified", async () => {
    // Sessions are server-authoritative: the AuthProvider restores authenticated
    // state from the /api/auth/session endpoint (encrypted HttpOnly cookies), so
    // the authenticated invitation path is exercised through that restore flow.
    vi.stubGlobal(
      "fetch",
      vi.fn(async () =>
        new Response(
          JSON.stringify({
            expiresAt: new Date(Date.now() + 60_000).toISOString(),
            tenant: null,
            user: {
              email: "invitee@example.test",
              id: 2,
              name: "Invitee",
              permissions: [],
              role: "Guest",
            },
          }),
          { status: 200, headers: { "content-type": "application/json" } },
        ),
      ),
    );

    render(
      <AppProviders>
        <ProgrammeInvitationLanding inviteCode="AQ7G2X9KLMNP" />
      </AppProviders>,
    );

    const button = await screen.findByRole("button", {
      name: /confirm and continue to payment/i,
    });
    await waitFor(() => expect(button).toBeEnabled());
    expect(httpClient.get).toHaveBeenCalledWith(apiEndpoints.health.get);
  });

  it("renders an invalid invitation as an inline public error", async () => {
    vi.mocked(httpClient.get).mockImplementation(async (url) => {
      if (url === apiEndpoints.health.get) return health;
      throw new Error("This invitation link is not valid.");
    });

    render(
      <AppProviders>
        <ProgrammeInvitationLanding inviteCode="AQ7G2X9KLMNP" />
      </AppProviders>,
    );

    expect(await screen.findByText("This invitation link is not valid."))
      .toBeInTheDocument();
    expect(screen.queryByText("Something went wrong")).not.toBeInTheDocument();
  });
});
