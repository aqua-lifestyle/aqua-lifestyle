import { AxiosHeaders, type InternalAxiosRequestConfig } from "axios";
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
