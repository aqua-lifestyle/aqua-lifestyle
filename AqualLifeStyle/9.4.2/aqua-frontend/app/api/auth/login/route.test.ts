import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

const { encryptSession, projectSession, setSessionCookies } = vi.hoisted(() => ({
  encryptSession: vi.fn(),
  projectSession: vi.fn(),
  setSessionCookies: vi.fn(),
}));

vi.mock("@/src/shared/auth/session", () => ({
  encryptSession,
  projectSession,
  setSessionCookies,
}));

import { POST } from "./route";

const loginRequest = (origin: string, tenant: string | null = null) =>
  new Request("http://localhost:3100/api/auth/login", {
    method: "POST",
    headers: {
      "content-type": "application/json",
      origin,
    },
    body: JSON.stringify({
      email: "member@example.test",
      password: "test-password",
      rememberMe: false,
      tenant,
    }),
  });

describe("POST /api/auth/login", () => {
  beforeEach(() => {
    vi.resetAllMocks();
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(
        new Response(
          JSON.stringify({ result: { accessToken: "token", expireInSeconds: 3600 } }),
          { status: 200, headers: { "Content-Type": "application/json" } },
        ),
      ),
    );
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("rejects a cross-origin login request before contacting the backend", async () => {
    const response = await POST(loginRequest("https://evil.example"));

    expect(response.status).toBe(403);
    expect(fetch).not.toHaveBeenCalled();
  });

  it("issues session cookies for a login with a readable user projection", async () => {
    vi.mocked(projectSession).mockReturnValue({
      expiresAt: "2026-08-13T00:00:00.000Z",
      tenant: null,
      user: {
        email: "member@example.test",
        id: 42,
        name: "Test Member",
        permissions: [],
        role: "Member",
      },
    });
    vi.mocked(encryptSession).mockResolvedValue("encrypted");

    const response = await POST(loginRequest("http://localhost:3100"));

    expect(response.status).toBe(200);
    expect(fetch).toHaveBeenCalledOnce();
    expect(setSessionCookies).toHaveBeenCalledWith(
      expect.anything(),
      "encrypted",
      expect.any(String),
    );
  });

  it("fails closed when the token projection has no user profile and issues no cookies", async () => {
    vi.mocked(projectSession).mockReturnValue({
      expiresAt: "2026-08-13T00:00:00.000Z",
      tenant: null,
      user: null,
    });

    const response = await POST(loginRequest("http://localhost:3100"));

    expect(response.status).toBe(401);
    expect(setSessionCookies).not.toHaveBeenCalled();
  });
});