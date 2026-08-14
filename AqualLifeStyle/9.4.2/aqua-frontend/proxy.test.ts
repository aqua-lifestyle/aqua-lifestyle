import { NextRequest, NextResponse } from "next/server";
import { beforeEach, describe, expect, it, vi } from "vitest";
import * as sessionModule from "@/src/shared/auth/session";

import type { ServerSession } from "@/src/shared/auth/session";

vi.mock("@/src/shared/auth/session", async () => {
  const cookie = await vi.importActual<typeof import("@/src/shared/auth/session-cookie")>(
    "@/src/shared/auth/session-cookie",
  );
  const api = await vi.importActual<typeof import("@/src/shared/api/auth-service")>(
    "@/src/shared/api/auth-service",
  );

  const sessionStore = new Map<string, ServerSession>();

  return {
    decryptSession: async (value?: string | null) =>
      value ? (sessionStore.get(value) ?? null) : null,
    projectSession: (session: ServerSession) => {
      const claims = api.decodeJwtPayload(session.accessToken);
      return {
        expiresAt: session.expiresAt,
        tenant: session.tenant,
        user: claims ? api.claimsToUser(claims) : null,
      };
    },
    readSessionCookie: cookie.readSessionCookie,
    __sessionStore: sessionStore,
  };
});

import { proxy } from "./proxy";

const sessionStore = (sessionModule as typeof sessionModule & {
  __sessionStore: Map<string, ServerSession>;
}).__sessionStore;

const encode = (value: object) =>
  Buffer.from(JSON.stringify(value)).toString("base64url");

const token = (claims: object) => `t.${encode(claims)}.s`;

const memberClaims = {
  email: "member@example.test",
  name: "Test Member",
  role: "Member",
  sub: "42",
  tenantId: "7",
};

const adminClaims = {
  email: "admin@example.test",
  name: "Test Admin",
  role: "Admin",
  sub: "1",
};

const userlessClaims = { role: "Member" };

const future = () => new Date(Date.now() + 3_600_000).toISOString();

const session = (accessToken: string): ServerSession => ({
  accessToken,
  expiresAt: future(),
  tenant: null,
});

const cookieHeader = (key: string) => `aqua.session.count=1; aqua.session.0=${key}`;

const request = (path: string, cookieKey?: string) => {
  const init = cookieKey ? { headers: { cookie: cookieHeader(cookieKey) } } : undefined;
  return new NextRequest(`http://localhost:3100${path}`, init);
};

const locationOf = (response: NextResponse) => response.headers.get("location");

describe("proxy navigation boundary", () => {
  beforeEach(() => {
    sessionStore.clear();
    sessionStore.set("member", session(token(memberClaims)));
    sessionStore.set("admin", session(token(adminClaims)));
    sessionStore.set("userless", session(token(userlessClaims)));
  });

  it("redirects guests away from protected routes with the original destination", async () => {
    const response = await proxy(request("/dashboard"));
    expect(response.status).toBe(307);
    expect(locationOf(response)).toBe("http://localhost:3100/login?redirect=%2Fdashboard");
  });

  it("preserves the query string in the login redirect", async () => {
    const response = await proxy(request("/dashboard?tab=profile"));
    expect(locationOf(response)).toBe("http://localhost:3100/login?redirect=%2Fdashboard%3Ftab%3Dprofile");
  });

  it("redirects guests from every protected prefix", async () => {
    for (const path of ["/admin/users", "/area-leader", "/customers", "/enquiries", "/facilitator", "/member", "/memberships", "/order-intents", "/products", "/profile", "/settings"]) {
      expect((await proxy(request(path))).status).toBe(307);
    }
  });

  it("leaves guests on public pages", async () => {
    for (const path of ["/", "/login", "/signup", "/contact"]) {
      expect(locationOf(await proxy(request(path)))).toBeNull();
    }
  });

  it("lets authenticated users through to protected routes", async () => {
    expect(locationOf(await proxy(request("/dashboard", "member")))).toBeNull();
  });

  it("sends authenticated users home from guest-only pages", async () => {
    for (const path of ["/login", "/signup"]) {
      expect(locationOf(await proxy(request(path, "member")))).toBe(
        "http://localhost:3100/dashboard",
      );
    }
    expect(locationOf(await proxy(request("/", "member")))).toBe(
      "http://localhost:3100/dashboard",
    );
  });

  it("routes system admins to the admin home", async () => {
    expect(locationOf(await proxy(request("/login", "admin")))).toBe(
      "http://localhost:3100/admin/dashboard",
    );
  });

  it("leaves authenticated users on non-protected public pages", async () => {
    expect(locationOf(await proxy(request("/contact", "member")))).toBeNull();
  });

  it("treats an unreadable cookie like an absent session", async () => {
    expect((await proxy(request("/dashboard", "missing"))).status).toBe(307);
  });

  it("redirects userless sessions away from protected routes", async () => {
    const response = await proxy(request("/dashboard", "userless"));
    expect(response.status).toBe(307);
    expect(locationOf(response)).toBe("http://localhost:3100/login?redirect=%2Fdashboard");
    expect(locationOf(await proxy(request("/admin/users", "userless")))).toBe(
      "http://localhost:3100/login?redirect=%2Fadmin%2Fusers",
    );
  });

  it("keeps userless sessions in guest mode on public pages", async () => {
    for (const path of ["/", "/login", "/signup", "/contact"]) {
      expect(locationOf(await proxy(request(path, "userless")))).toBeNull();
    }
  });
});
