import { afterEach, describe, expect, it } from "vitest";

import { getServerEnv } from "./server-env-validation";

const VALID_SECRET = "test-secret-with-at-least-thirty-two-characters";

afterEach(() => {
  delete process.env.NEXTAUTH_SECRET;
});

describe("getServerEnv", () => {
  it("returns the validated runtime secret when NEXTAUTH_SECRET is set", () => {
    process.env.NEXTAUTH_SECRET = VALID_SECRET;

    expect(getServerEnv()).toEqual({ NEXTAUTH_SECRET: VALID_SECRET });
  });

  it("fails closed when NEXTAUTH_SECRET is not set", () => {
    expect(() => getServerEnv()).toThrow("NEXTAUTH_SECRET");
  });

  it("fails closed when NEXTAUTH_SECRET is shorter than 32 characters", () => {
    process.env.NEXTAUTH_SECRET = "too-short-secret";

    expect(() => getServerEnv()).toThrow("NEXTAUTH_SECRET");
  });
});
