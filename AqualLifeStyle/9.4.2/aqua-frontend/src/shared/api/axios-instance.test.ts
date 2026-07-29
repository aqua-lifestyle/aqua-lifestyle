import {
  AxiosError,
  AxiosHeaders,
  type InternalAxiosRequestConfig,
} from "axios";
import { beforeEach, describe, expect, it, vi } from "vitest";

const createConfig = (): InternalAxiosRequestConfig => ({
  headers: new AxiosHeaders(),
});

const importAxiosInstance = async () => {
  vi.stubEnv("NEXT_PUBLIC_ABP_API_URL", "https://localhost:44311");
  vi.resetModules();

  return import("./axios-instance");
};

describe("applyRequestContext", () => {
  beforeEach(() => {
    vi.unstubAllEnvs();
  });

  it("leaves auth and tenant headers unset by default", async () => {
    const { applyRequestContext, setAccessTokenProvider, setTenantProvider } =
      await importAxiosInstance();

    setAccessTokenProvider(() => null);
    setTenantProvider(() => null);

    const config = await applyRequestContext(createConfig());

    expect(config.headers.Authorization).toBeUndefined();
    expect(config.headers.__tenant).toBeUndefined();
  });

  it("adds a bearer token when auth context is available", async () => {
    const { applyRequestContext, setAccessTokenProvider, setTenantProvider } =
      await importAxiosInstance();

    setTenantProvider(() => null);
    setAccessTokenProvider(() => "access-token");

    const config = await applyRequestContext(createConfig());

    expect(config.headers.Authorization).toBe("Bearer access-token");
  });

  it("adds the ABP tenant header when tenant context is available", async () => {
    const { applyRequestContext, setAccessTokenProvider, setTenantProvider } =
      await importAxiosInstance();

    setAccessTokenProvider(() => null);
    setTenantProvider(() => "national-club-aqgreen");

    const config = await applyRequestContext(createConfig());

    expect(config.headers.__tenant).toBe("national-club-aqgreen");
  });

  it("waits for async auth and tenant providers", async () => {
    const { applyRequestContext, setAccessTokenProvider, setTenantProvider } =
      await importAxiosInstance();

    setAccessTokenProvider(async () => "async-token");
    setTenantProvider(async () => "area-space-1");

    const config = await applyRequestContext(createConfig());

    expect(config.headers.Authorization).toBe("Bearer async-token");
    expect(config.headers.__tenant).toBe("area-space-1");
  });
});

describe("getExpiredSessionLoginUrl", () => {
  it("preserves a safe return path and explains why sign-in is required", async () => {
    const { getExpiredSessionLoginUrl } = await importAxiosInstance();

    expect(
      getExpiredSessionLoginUrl("/member/programmes?payment=success&programme=aqgreen"),
    ).toBe(
      "/login?reason=session-ended&redirect=%2Fmember%2Fprogrammes%3Fpayment%3Dsuccess%26programme%3Daqgreen",
    );
  });

  it("rejects an unsafe return path", async () => {
    const { getExpiredSessionLoginUrl } = await importAxiosInstance();

    expect(getExpiredSessionLoginUrl("//untrusted.example")).toBe(
      "/login?reason=session-ended&redirect=%2Fdashboard",
    );
    expect(getExpiredSessionLoginUrl("/\\untrusted.example")).toBe(
      "/login?reason=session-ended&redirect=%2Fdashboard",
    );
    expect(getExpiredSessionLoginUrl("/\\/untrusted.example")).toBe(
      "/login?reason=session-ended&redirect=%2Fdashboard",
    );
  });
});

describe("session refresh failures", () => {
  it("does not turn a transient refresh failure into a login redirect", async () => {
    const { apiClient, setRefreshTokenProvider } = await importAxiosInstance();
    const transientFailure = new Error("The authentication server is unavailable.");
    setRefreshTokenProvider(async () => {
      throw transientFailure;
    });

    await expect(
      apiClient.request({
        adapter: async (config) => {
          throw new AxiosError(
            "Unauthorized",
            "ERR_BAD_REQUEST",
            config,
            undefined,
            {
              config,
              data: {},
              headers: new AxiosHeaders(),
              status: 401,
              statusText: "Unauthorized",
            },
          );
        },
        url: "/protected",
      }),
    ).rejects.toBe(transientFailure);
  });
});
