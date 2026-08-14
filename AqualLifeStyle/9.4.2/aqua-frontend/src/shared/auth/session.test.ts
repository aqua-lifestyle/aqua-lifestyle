import { describe, expect, it } from "vitest";

import {
  deleteSessionCookies,
  readSessionCookie,
  SESSION_COOKIE,
  SESSION_COOKIE_CHUNK_SIZE,
  setSessionCookies,
} from "./session-cookie";

const cookieStore = () => {
  const values = new Map<string, string>();
  return {
    delete: (name: string) => { values.delete(name); },
    get: (name: string) => values.has(name) ? { value: values.get(name)! } : undefined,
    set: (name: string, value: string) => { values.set(name, value); },
    values,
  };
};

describe("server session cookies", () => {
  it("splits a real-world encrypted session into bounded cookies", () => {
    const store = cookieStore();
    const encrypted = `iv.${"p".repeat(7_400)}`;

    setSessionCookies(store, encrypted, { httpOnly: true });

    expect(Number(store.values.get(`${SESSION_COOKIE}.count`))).toBeGreaterThan(1);
    expect([...store.values.values()].every((value) => value.length <= SESSION_COOKIE_CHUNK_SIZE)).toBe(true);
    expect(readSessionCookie(store)).toBe(encrypted);
  });

  it("fails closed when a chunk is missing and deletes every session chunk", () => {
    const store = cookieStore();
    store.values.set(`${SESSION_COOKIE}.count`, "2");
    store.values.set(`${SESSION_COOKIE}.0`, "first");

    expect(readSessionCookie(store)).toBeNull();
    deleteSessionCookies(store);
    expect(store.values.size).toBe(0);
  });
});
