import axios, {
  AxiosError,
  type AxiosInstance,
  type InternalAxiosRequestConfig,
} from "axios";

import { publicEnv } from "@/src/shared/config";
import {
  normalizeAbpError,
  normalizeNetworkError,
  type AbpErrorEnvelope,
} from "./abp-error";

// Local ABP authorization can take longer while the development database is
// cold, especially when a dashboard loads several permission-protected APIs.
const DEFAULT_TIMEOUT_MS = 30_000;
const TENANT_HEADER = "__tenant";

type RequestContextProvider = () => string | null | Promise<string | null>;

let accessTokenProvider: RequestContextProvider | null = null;
let tenantProvider: RequestContextProvider | null = null;
let refreshTokenProvider: (() => Promise<string | null>) | null = null;
let activeRefreshProvider: (() => Promise<string | null>) | null = null;
let activeRefreshRequest: Promise<string | null> | null = null;

export const setAccessTokenProvider = (provider: RequestContextProvider) => {
  accessTokenProvider = provider;
};

export const setTenantProvider = (provider: RequestContextProvider) => {
  tenantProvider = provider;
};

export const setRefreshTokenProvider = (provider: () => Promise<string | null>) => {
  refreshTokenProvider = provider;
};

export const refreshAccessToken = async () => {
  const provider = refreshTokenProvider;
  if (!provider) return null;
  if (activeRefreshRequest && activeRefreshProvider === provider) {
    return activeRefreshRequest;
  }

  const request = Promise.resolve().then(provider);
  activeRefreshProvider = provider;
  activeRefreshRequest = request;
  try {
    return await request;
  } finally {
    if (activeRefreshRequest === request) {
      activeRefreshProvider = null;
      activeRefreshRequest = null;
    }
  }
};

export const getExpiredSessionLoginUrl = (path: string) => {
  const safePath = /^\/(?![\\/])/.test(path) ? path : "/dashboard";
  return `/login?reason=session-ended&redirect=${encodeURIComponent(safePath)}`;
};

export const apiClient: AxiosInstance = axios.create({
  baseURL: publicEnv.NEXT_PUBLIC_ABP_API_URL,
  timeout: DEFAULT_TIMEOUT_MS,
  headers: {
    Accept: "application/json",
    "Content-Type": "application/json",
  },
});

export const applyRequestContext = async (
  config: InternalAxiosRequestConfig,
) => {
  const [accessToken, tenant] = await Promise.all([
    accessTokenProvider?.() ?? null,
    tenantProvider?.() ?? null,
  ]);

  if (accessToken) {
    config.headers.Authorization = `Bearer ${accessToken}`;
  }

  if (tenant) {
    config.headers[TENANT_HEADER] = tenant;
  }

  return config;
};

apiClient.interceptors.request.use(applyRequestContext);

apiClient.interceptors.response.use(
  (response) => response,
  async (error: AxiosError<AbpErrorEnvelope>) => {
    if (error.response) {
      const { status } = error.response;
      const originalRequest = error.config as InternalAxiosRequestConfig & {
        _retry?: boolean;
      };

      // Handle 401 Unauthorized — try token refresh before failing
      if (status === 401 && !originalRequest._retry && refreshTokenProvider) {
        originalRequest._retry = true;

        try {
          const newToken = await refreshAccessToken();
          if (newToken) {
            originalRequest.headers.Authorization = `Bearer ${newToken}`;
            return apiClient(originalRequest);
          }
        } catch (refreshError) {
          // A temporary network or server failure must not invalidate a valid
          // session or present it as expired. Let the caller's normal error
          // path handle the refresh failure and allow a later retry.
          throw refreshError;
        }

        // A null result means the refresh credential was definitively rejected.
        if (typeof window !== "undefined") {
          const returnPath = `${window.location.pathname}${window.location.search}`;
          window.location.href = getExpiredSessionLoginUrl(returnPath);
        }
      }

      // Handle 403 Forbidden — let the component surface the error instead
      // of hard-redirecting to a missing /forbidden route.
      if (status === 403) {
        throw normalizeAbpError(status, error.response.data);
      }

      throw normalizeAbpError(status, error.response.data);
    }

    // No HTTP response: DNS/port down, CORS blocked, or untrusted HTTPS cert.
    if (process.env.NODE_ENV === "development") {
      console.error(
        "[apiClient] Network error reaching",
        publicEnv.NEXT_PUBLIC_ABP_API_URL,
        error.code ?? error.message,
      );
    }

    throw normalizeNetworkError(error);
  },
);
