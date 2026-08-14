import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

const { deleteSessionCookies, readSession } = vi.hoisted(() => ({
  deleteSessionCookies: vi.fn(),
  readSession: vi.fn(),
}));

vi.mock("@/src/shared/auth/session", () => ({
  deleteSessionCookies,
  readSession,
}));

import { NextRequest } from "next/server";
import { GET, POST } from "./route";

const session = {
  accessToken: "session-token",
  expiresAt: "2026-08-14T00:00:00.000Z",
  tenant: "area-1",
};

type RouteInit = {
  body?: BodyInit | null;
  headers?: HeadersInit;
  method?: string;
};

const routeRequest = (path: string[], init: RouteInit = {}) => {
  const url = new URL(`/api/backend/${path.join("/")}`, "http://localhost:3100");
  return {
    request: new NextRequest(url, init),
    handlerArgs: { params: Promise.resolve({ path }) },
  };
};

const forwardedFetch = () => {
  const call = vi.mocked(fetch).mock.calls[0];
  const [url, init] = call;
  return [url as string | URL, init ?? {}] as const;
};

describe("app/api/backend/[...path]", () => {
  beforeEach(() => {
    vi.resetAllMocks();
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(
        new Response(JSON.stringify({ result: {} }), {
          status: 200,
          headers: { "Content-Type": "application/json" },
        }),
      ),
    );
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("fails closed without a session and deletes stale session cookies", async () => {
    vi.mocked(readSession).mockResolvedValue(null);
    const { request, handlerArgs } = routeRequest(["api", "services", "app", "Customer", "GetMyCustomer"]);

    const response = await GET(request, handlerArgs);

    expect(response.status).toBe(401);
    expect(deleteSessionCookies).toHaveBeenCalled();
    expect(fetch).not.toHaveBeenCalled();
  });

  it("rejects a cross-origin mutation before contacting the backend", async () => {
    vi.mocked(readSession).mockResolvedValue(session);
    const { request, handlerArgs } = routeRequest(["api", "services", "app", "Customer", "Update"], {
      method: "POST",
      headers: { origin: "https://evil.example" },
      body: "{}",
    });

    const response = await POST(request, handlerArgs);

    expect(response.status).toBe(403);
    expect(fetch).not.toHaveBeenCalled();
  });

  it("forwards the bearer credential and the session tenant when the client sends no tenant header", async () => {
    vi.mocked(readSession).mockResolvedValue(session);
    const { request, handlerArgs } = routeRequest(
      ["api", "services", "app", "Customer", "GetMyCustomer"],
      { method: "GET" },
    );

    const response = await GET(request, handlerArgs);

    expect(response.status).toBe(200);
    const [url, init] = forwardedFetch();
    expect(new URL(url).pathname).toBe("/api/services/app/Customer/GetMyCustomer");
    expect(new Headers(init.headers).get("Authorization")).toBe("Bearer session-token");
    expect(new Headers(init.headers).get("__tenant")).toBe("area-1");
  });

  it("honours the client tenant header so Area switching keeps working", async () => {
    vi.mocked(readSession).mockResolvedValue(session);
    const { request, handlerArgs } = routeRequest(
      ["api", "services", "app", "Customer", "GetMyCustomer"],
      { method: "GET", headers: { __tenant: "area-2" } },
    );

    const response = await GET(request, handlerArgs);

    expect(response.status).toBe(200);
    expect(new Headers(forwardedFetch()[1].headers).get("__tenant")).toBe("area-2");
  });

  it("deletes session cookies when the backend rejects the session with 401", async () => {
    vi.mocked(readSession).mockResolvedValue(session);
    vi.mocked(fetch).mockResolvedValue(
      new Response(JSON.stringify({ error: { message: "Authentication required." } }), {
        status: 401,
        headers: { "Content-Type": "application/json" },
      }),
    );
    const { request, handlerArgs } = routeRequest(["api", "services", "app", "Customer", "GetMyCustomer"]);

    const response = await GET(request, handlerArgs);

    expect(response.status).toBe(401);
    expect(deleteSessionCookies).toHaveBeenCalled();
  });
});