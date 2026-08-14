import { describe, expect, it } from "vitest";

import { clearAuthSession, setAuthSession } from "./actions";
import { initialAuthState, type AuthSession } from "./context";
import { authReducer } from "./reducer";

const session: AuthSession = {
  accessToken: "access-token",
  expiresAt: "2026-01-01T00:00:00Z",
  user: {
    email: "user@example.com",
    id: 1,
    name: "Demo User",
    role: "Member",
    permissions: ["Aqua.Members.ViewSelf"],
  },
};

describe("authReducer", () => {
  it("sets an authenticated session", () => {
    const state = authReducer(initialAuthState, setAuthSession(session));

    expect(state.isAuthenticated).toBe(true);
    expect(state.session).toEqual(session);
  });

  it("treats a session without a user profile as anonymous", () => {
    const state = authReducer(initialAuthState, setAuthSession({ ...session, user: null }));

    expect(state.isAuthenticated).toBe(false);
    expect(state.isReady).toBe(true);
    expect(state.session).toBeNull();
    expect(state.status).toBe("anonymous");
  });

  it("clears an authenticated session", () => {
    const authenticatedState = authReducer(
      initialAuthState,
      setAuthSession(session),
    );
    const state = authReducer(authenticatedState, clearAuthSession());

    expect(state.isAuthenticated).toBe(false);
    expect(state.session).toBeNull();
  });
});
