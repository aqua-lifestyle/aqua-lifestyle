import { beforeEach, describe, expect, it, vi } from "vitest";

const { deleteSessionCookies } = vi.hoisted(() => ({
  deleteSessionCookies: vi.fn(),
}));

vi.mock("@/src/shared/auth/session", () => ({
  deleteSessionCookies,
}));

import { POST } from "./route";

const logoutRequest = (origin: string) =>
  new Request("http://localhost:3100/api/auth/logout", {
    method: "POST",
    headers: { origin },
  });

describe("POST /api/auth/logout", () => {
  beforeEach(() => {
    vi.resetAllMocks();
  });

  it("deletes every session cookie for a same-origin request", async () => {
    const response = await POST(logoutRequest("http://localhost:3100"));

    expect(response.status).toBe(204);
    expect(deleteSessionCookies).toHaveBeenCalled();
  });

  it("rejects a cross-origin logout request", async () => {
    const response = await POST(logoutRequest("https://evil.example"));

    expect(response.status).toBe(403);
    expect(deleteSessionCookies).not.toHaveBeenCalled();
  });
});