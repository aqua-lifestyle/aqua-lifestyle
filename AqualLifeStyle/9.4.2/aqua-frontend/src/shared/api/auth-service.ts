import axios from "axios";

import { type AuthSession, type AuthUser } from "@/src/providers/Auth/context";
import { publicEnv } from "@/src/shared/config";

/**
 * Decode a JWT's payload (the middle base64 section) without validating the
 * signature. Used only to extract user claims that the ABP backend has already
 * signed inside the token.
 */
const decodeJwtPayload = (token: string): Record<string, unknown> | null => {
  try {
    const payloadBase64 = token.split(".")[1];
    if (!payloadBase64) return null;
    const json = atob(payloadBase64.replace(/-/g, "+").replace(/_/g, "/"));
    return JSON.parse(json) as Record<string, unknown>;
  } catch {
    return null;
  }
};

const claimTypes = {
  email: "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress",
  name: "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name",
  nameIdentifier:
    "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier",
  role: "http://schemas.microsoft.com/ws/2008/06/identity/claims/role",
  tenantId: "http://www.aspnetboilerplate.com/identity/claims/tenantId",
} as const;

const readStringClaim = (...values: unknown[]): string | null => {
  for (const value of values) {
    if (typeof value === "string" && value.trim()) return value;
    if (Array.isArray(value)) {
      const item = value.find(
        (candidate): candidate is string =>
          typeof candidate === "string" && Boolean(candidate.trim()),
      );
      if (item) return item;
    }
  }
  return null;
};

const normalizeRole = (role: string | null) => {
  const normalized = role?.replace(/[\s_-]/g, "").toLowerCase();
  return normalized === "admin" || normalized === "systemadmin"
    ? "SystemAdmin"
    : (role ?? "Member");
};

/** Map ABP and standard JWT claims to the application's AuthUser shape. */
export const claimsToUser = (
  claims: Record<string, unknown>,
): AuthUser | null => {
  const rawId = readStringClaim(
    claims.sub,
    claims[claimTypes.nameIdentifier],
  );
  const id = Number(rawId);

  if (!Number.isSafeInteger(id) || id <= 0) return null;

  const rawPermissions = claims.permissions ?? claims.rolePermissions ?? [];
  const role = readStringClaim(claims.role, claims[claimTypes.role]);
  const rawTenantId = readStringClaim(claims.tenantId, claims[claimTypes.tenantId]);
  const tenantId = Number(rawTenantId);

  return {
    id,
    email: readStringClaim(claims.email, claims[claimTypes.email]),
    name: readStringClaim(
      claims.name,
      claims.given_name,
      claims.unique_name,
      claims[claimTypes.name],
    ),
    role: normalizeRole(role),
    permissions: Array.isArray(rawPermissions)
      ? (rawPermissions as string[])
      : typeof rawPermissions === "string"
        ? rawPermissions.split(",")
        : [],
    ...(Number.isSafeInteger(tenantId) && tenantId > 0 ? { tenantId } : {}),
  };
};

export type LoginInput = {
  email: string;
  password: string;
  rememberMe?: boolean;
  tenant?: string | null;
};

export type LoginResult =
  | { ok: true; session: AuthSession }
  | { ok: false; message: string };

export type RegisterInput = {
  email: string;
  password: string;
  name: string;
  surname: string;
  tenant?: string | null;
};

export type RegisterResult =
  | { ok: true }
  | { ok: false; message: string; fieldErrors?: Record<string, string> };

const API_BASE = publicEnv.NEXT_PUBLIC_ABP_API_URL;

/**
 * Authenticate against the ABP / OpenIddict token endpoint using the Resource
 * Owner Password Credentials grant.
 *
 * Endpoint: POST {API_BASE}/connect/token
 * Request body (application/x-www-form-urlencoded):
 *   grant_type=password&username={email}&password={password}&client_id={client_id}&scope={scope}
 *
 * A dedicated axios instance is used here so the auth call itself is not
 * intercepted by the main apiClient's 403 handler or access-token injector.
 */
export const login = async (input: LoginInput): Promise<LoginResult> => {
  try {
    const headers: Record<string, string> = {
      "Content-Type": "application/json",
    };

    if (input.tenant) {
      headers["__tenant"] = input.tenant;
    }

    const response = await axios.post<{
      result: {
        accessToken: string;
        expireInSeconds: number;
        userId: number;
      };
    }>(`${API_BASE}/api/TokenAuth/Authenticate`, {
      userNameOrEmailAddress: input.email,
      password: input.password,
      rememberClient: input.rememberMe ?? false,
    }, { headers });

    const { accessToken, expireInSeconds } = response.data.result;

    const claims = decodeJwtPayload(accessToken);
    const user = claims ? claimsToUser(claims) : null;

    const session: AuthSession = {
      accessToken,
      expiresAt: new Date(Date.now() + expireInSeconds * 1000).toISOString(),
      refreshToken: null,
      user,
    };

    return { ok: true, session };
  } catch (error: unknown) {
    if (axios.isAxiosError(error) && error.response) {
      const data = error.response.data as Record<string, unknown>;
      const errorObj = (data.error ?? data) as Record<string, unknown>;
      const message =
        (errorObj.message as string) ??
        (errorObj.details as string) ??
        (data.message as string) ??
        "Invalid email or password.";

      return { ok: false, message };
    }

    return { ok: false, message: "Unable to reach the authentication server. Check your connection and try again." };
  }
};

/**
 * Register a new user via the ABP AccountAppService Web API endpoint.
 *
 * Endpoint: POST {API_BASE}/api/services/app/Account/Register
 */
export const register = async (input: RegisterInput): Promise<RegisterResult> => {
  try {
    const headers: Record<string, string> = {
      "Content-Type": "application/json",
      __tenant: input.tenant ?? publicEnv.NEXT_PUBLIC_DEFAULT_TENANT_NAME,
    };

    await axios.post(
      `${API_BASE}/api/services/app/Account/Register`,
      {
        name: input.name,
        surname: input.surname,
        userName: input.email.split("@")[0],
        emailAddress: input.email,
        password: input.password,
      },
      {
        headers,
      },
    );

    return { ok: true };
  } catch (error: unknown) {
    if (axios.isAxiosError(error) && error.response) {
      const data = error.response.data as Record<string, unknown>;

      if (error.response.status === 400) {
        const fieldErrors: Record<string, string> = {};
        const validationErrors = (
          (data as { validationErrors?: { memberNames?: string[]; message: string }[] })
            .validationErrors ?? []
        );

        for (const ve of validationErrors) {
          for (const member of ve.memberNames ?? []) {
            fieldErrors[member] = ve.message;
          }
        }

        return {
          ok: false,
          message: (data.message as string) ?? "Validation failed.",
          fieldErrors: Object.keys(fieldErrors).length > 0 ? fieldErrors : undefined,
        };
      }

      return {
        ok: false,
        message: (data.message as string) ?? "Registration failed. Please try again.",
      };
    }

    return { ok: false, message: "Unable to reach the server. Check your connection and try again." };
  }
};

/**
 * Refresh an access token.
 *
 * Note: ABP TokenAuth does not issue refresh tokens. This function is
 * preserved for compatibility but will fail if called against the current
 * backend.
 */
export const refreshToken = async (
  currentRefreshToken: string,
  tenant?: string | null,
): Promise<LoginResult> => {
  try {
    const params = new URLSearchParams();
    params.append("grant_type", "refresh_token");
    params.append("refresh_token", currentRefreshToken);
    params.append("client_id", "Aqua_App");

    const headers: Record<string, string> = {
      "Content-Type": "application/x-www-form-urlencoded",
    };

    if (tenant) {
      headers["__tenant"] = tenant;
    }

    const response = await axios.post<{
      access_token: string;
      expires_in: number;
      token_type: string;
      refresh_token?: string;
      scope: string;
    }>(`${API_BASE}/connect/token`, params.toString(), { headers });

    const { access_token, expires_in, refresh_token } = response.data;

    const claims = decodeJwtPayload(access_token);
    const user = claims ? claimsToUser(claims) : null;

    const session: AuthSession = {
      accessToken: access_token,
      expiresAt: new Date(Date.now() + expires_in * 1000).toISOString(),
      refreshToken: refresh_token ?? currentRefreshToken,
      user,
    };

    return { ok: true, session };
  } catch (error: unknown) {
    if (axios.isAxiosError(error) && error.response) {
      return {
        ok: false,
        message: (error.response.data as { error_description?: string }).error_description ?? "Session expired. Please sign in again.",
      };
    }

    return { ok: false, message: "Unable to refresh your session." };
  }
};
