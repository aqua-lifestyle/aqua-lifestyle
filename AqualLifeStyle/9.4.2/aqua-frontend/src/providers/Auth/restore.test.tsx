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
    setRefreshTokenProvider: vi.fn(),
  };
});

const futureSession: AuthSession = {
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
  const { isAuthenticated, isReady, session, status } = useAuthState();
  return (
    <div>
      <span data-testid="ready">{String(isReady)}</span>
      <span data-testid="authenticated">{String(isAuthenticated)}</span>
      <span data-testid="email">{session?.user?.email ?? "none"}</span>
      <span data-testid="status">{status}</span>
    </div>
  );
};

describe("AuthProvider cold-start session restoration", () => {
  beforeEach(() => {
    vi.stubGlobal("fetch", vi.fn());
  });

  it("restores the server-mediated session without reading browser credentials", async () => {
    vi.mocked(fetch).mockResolvedValue(new Response(JSON.stringify(futureSession), { status: 200 }));

    render(
      <AuthProvider>
        <Probe />
      </AuthProvider>,
    );

    expect(await screen.findByTestId("ready")).toHaveTextContent("true");
    expect(screen.getByTestId("authenticated")).toHaveTextContent("true");
    expect(screen.getByTestId("email")).toHaveTextContent("member@example.com");
    expect(fetch).toHaveBeenCalledWith("/api/auth/session", { cache: "no-store" });
  });

  it("becomes anonymous when the server session is absent", async () => {
    vi.mocked(fetch).mockResolvedValue(new Response("null", {
      headers: { "Content-Type": "application/json" },
      status: 200,
    }));

    render(
      <AuthProvider>
        <Probe />
      </AuthProvider>,
    );

    expect(await screen.findByTestId("ready")).toHaveTextContent("true");
    expect(screen.getByTestId("authenticated")).toHaveTextContent("false");
  });

  it("distinguishes session resolution failure from an anonymous session", async () => {
    vi.mocked(fetch).mockRejectedValue(new Error("network unavailable"));
    render(
      <AuthProvider>
        <Probe />
      </AuthProvider>,
    );

    expect(await screen.findByTestId("ready")).toHaveTextContent("true");
    expect(screen.getByTestId("authenticated")).toHaveTextContent("false");
    expect(screen.getByTestId("status")).toHaveTextContent("error");
  });

  it("treats a session without a user profile as anonymous", async () => {
    vi.mocked(fetch).mockResolvedValue(new Response(JSON.stringify({
      expiresAt: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
      user: null,
    }), { status: 200 }));

    render(
      <AuthProvider>
        <Probe />
      </AuthProvider>,
    );

    expect(await screen.findByTestId("ready")).toHaveTextContent("true");
    expect(screen.getByTestId("authenticated")).toHaveTextContent("false");
    expect(screen.getByTestId("email")).toHaveTextContent("none");
  });
});
