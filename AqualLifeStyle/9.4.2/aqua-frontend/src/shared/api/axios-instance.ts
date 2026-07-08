import axios, {
  AxiosError,
  type AxiosInstance,
  type InternalAxiosRequestConfig,
} from "axios";

import { publicEnv } from "@/src/shared/config";
import { normalizeAbpError, type AbpErrorEnvelope } from "./abp-error";

const DEFAULT_TIMEOUT_MS = 15_000;
const TENANT_HEADER = "__tenant";

type RequestContextProvider = () => string | null | Promise<string | null>;

let accessTokenProvider: RequestContextProvider | null = null;
let tenantProvider: RequestContextProvider | null = null;

export const setAccessTokenProvider = (provider: RequestContextProvider) => {
  accessTokenProvider = provider;
};

export const setTenantProvider = (provider: RequestContextProvider) => {
  tenantProvider = provider;
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
  (error: AxiosError<AbpErrorEnvelope>) => {
    if (error.response) {
      throw normalizeAbpError(error.response.status, error.response.data);
    }

    throw error;
  },
);
