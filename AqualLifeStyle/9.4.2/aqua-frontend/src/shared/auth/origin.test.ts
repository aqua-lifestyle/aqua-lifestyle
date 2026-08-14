import { describe, expect, it } from "vitest";

import { isSameOrigin } from "@/src/shared/auth/origin";

const request = (
  url: string,
  headers: Record<string, string>,
) =>
  ({
    headers: new Headers(headers),
    url,
  }) as unknown as Request;

describe("isSameOrigin", () => {
  it("allows requests without an origin header", () => {
    expect(isSameOrigin(request("http://localhost:3100/api/auth/login", {}))).toBe(true);
  });

  it("allows an origin matching the normalized request URL", () => {
    expect(
      isSameOrigin(
        request("http://localhost:3100/api/auth/login", {
          origin: "http://localhost:3100",
        }),
      ),
    ).toBe(true);
  });

  it("allows an origin matching the Host header when the request URL is normalized differently", () => {
    expect(
      isSameOrigin(
        request("http://localhost:3100/api/auth/login", {
          host: "127.0.0.1:3100",
          origin: "http://127.0.0.1:3100",
        }),
      ),
    ).toBe(true);
  });

  it("rejects a cross-site origin", () => {
    expect(
      isSameOrigin(
        request("http://localhost:3100/api/auth/login", {
          host: "127.0.0.1:3100",
          origin: "http://evil.example",
        }),
      ),
    ).toBe(false);
  });

  it("rejects an origin when no host header is present", () => {
    expect(
      isSameOrigin(
        request("http://localhost:3100/api/auth/login", {
          origin: "http://127.0.0.1:3100",
        }),
      ),
    ).toBe(false);
  });

  it("rejects a malformed origin", () => {
    expect(
      isSameOrigin(
        request("http://localhost:3100/api/auth/login", {
          host: "127.0.0.1:3100",
          origin: "not-a-url",
        }),
      ),
    ).toBe(false);
  });

  it("rejects the null origin sent by sandboxed iframes", () => {
    expect(
      isSameOrigin(
        request("http://localhost:3100/api/auth/login", {
          host: "127.0.0.1:3100",
          origin: "null",
        }),
      ),
    ).toBe(false);
  });

  it("compares hostnames without trusting the scheme, which the Host header cannot express", () => {
    expect(
      isSameOrigin(
        request("http://localhost:3100/api/auth/login", {
          host: "aqua.example",
          origin: "https://aqua.example",
        }),
      ),
    ).toBe(true);
  });

  it("rejects an origin that matches the host on a different port", () => {
    expect(
      isSameOrigin(
        request("http://localhost:3100/api/auth/login", {
          host: "aqua.example:443",
          origin: "https://aqua.example:8443",
        }),
      ),
    ).toBe(false);
  });

  it("does not trust forwarded host headers to override the origin decision", () => {
    expect(
      isSameOrigin(
        request("http://localhost:3100/api/auth/login", {
          host: "127.0.0.1:3100",
          origin: "http://evil.example",
          "x-forwarded-host": "127.0.0.1:3100",
        }),
      ),
    ).toBe(false);
  });
});
