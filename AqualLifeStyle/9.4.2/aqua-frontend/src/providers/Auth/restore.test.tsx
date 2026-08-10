import { render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";

import type { AuthSession } from "./context";
import { AuthProvider } from "./index";
import { useAuthState } from "./index";

vi.mock("@/src/shared/api", async () => {
  const actual = await vi.importActual<typeof import("@/src/shared/api")>(
    "@/src/shared/api",
  );
  return {
    ...actual,
    setAccessTokenProvider: vi.fn(),
    setRefreshTokenProvider: vi.fn(),
  };
});

const futureSession: AuthSession = {
  accessToken: "restored-access-token",
  expiresAt: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
  user: {
    email: "member@example.com",
    id: 7,
    name: "Club Member",
    permissions: ["Aqua.ProgrammeParticipations.ViewSelf"],
    role: "Member",
  },
};

const Probe = () => {
  const { isAuthenticated, isReady, session } = useAuthState();
  return (
    <div>
      <span data-testid="ready">{String(isReady)}</span>
      <span data-testid="authenticated">{String(isAuthenticated)}</span>
      <span data-testid="email">{session?.user?.email ?? "none"}</span>
    </div>
  );
};

describe("AuthProvider cold-start session restoration", () => {
  beforeEach(() => {
    window.localStorage.clear();
  });

  it("restores a valid stored session and completes readiness", async () => {
    window.localStorage.setItem("aqua.authSession", JSON.stringify(futureSession));

    render(
      <AuthProvider>
        <Probe />
      </AuthProvider>,
    );

    expect(await screen.findByTestId("ready")).toHaveTextContent("true");
    expect(screen.getByTestId("authenticated")).toHaveTextContent("true");
    expect(screen.getByTestId("email")).toHaveTextContent("member@example.com");
  });

  it("clears an expired stored session", async () => {
    window.localStorage.setItem(
      "aqua.authSession",
      JSON.stringify({
        ...futureSession,
        expiresAt: new Date(Date.now() - 1000).toISOString(),
      }),
    );

    render(
      <AuthProvider>
        <Probe />
      </AuthProvider>,
    );

    expect(await screen.findByTestId("ready")).toHaveTextContent("true");
    expect(screen.getByTestId("authenticated")).toHaveTextContent("false");
    expect(window.localStorage.getItem("aqua.authSession")).toBeNull();
  });

  it("completes readiness when no session is stored", async () => {
    render(
      <AuthProvider>
        <Probe />
      </AuthProvider>,
    );

    expect(await screen.findByTestId("ready")).toHaveTextContent("true");
    expect(screen.getByTestId("authenticated")).toHaveTextContent("false");
  });
});
