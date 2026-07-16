export type AbpValidationError = {
  message: string;
  members?: string[];
};

export type AbpErrorPayload = {
  code?: string;
  message?: string;
  details?: string;
  validationErrors?: AbpValidationError[];
  correlationId?: string;
};

export type AbpErrorEnvelope = {
  error?: AbpErrorPayload;
  code?: string;
  message?: string;
  details?: string;
  validationErrors?: AbpValidationError[];
  correlationId?: string;
};

export type AbpResponseEnvelope<TResponse> = {
  result: TResponse;
  targetUrl: string | null;
  success: boolean;
  error: AbpErrorPayload | null;
  unAuthorizedRequest: boolean;
  __abp: true;
};

const NETWORK_ERROR_MESSAGE =
  "Unable to reach the backend API. Confirm the backend is running, the local HTTPS certificate is trusted, and CORS allows this frontend origin.";

const CERTIFICATE_ERROR_MESSAGE =
  "Unable to reach the backend API because the local HTTPS certificate is not trusted. Open https://localhost:44311/swagger in this browser and accept the certificate, run `dotnet dev-certs https --trust`, or set NEXT_PUBLIC_ABP_API_URL=http://localhost:21021 for local HTTP development.";

export class AbpHttpError extends Error {
  readonly status: number;
  readonly code?: string;
  readonly details?: string;
  readonly validationErrors: AbpValidationError[];
  readonly correlationId?: string;

  constructor(status: number, payload: AbpErrorPayload) {
    super(payload.message ?? "The request failed.");
    this.name = "AbpHttpError";
    this.status = status;
    this.code = payload.code;
    this.details = payload.details;
    this.validationErrors = payload.validationErrors ?? [];
    this.correlationId = payload.correlationId;
  }
}

const GENERIC_SERVER_MESSAGES = new Set([
  "The request failed.",
  "An internal error occurred during your request!",
  "Internal server error.",
  "Login failed!",
]);

const getStatusMessage = (status: number, fallback: string): string => {
  switch (status) {
    case 400:
    case 422:
      return `Some information is missing or invalid. Review the form and try again. ${fallback}`;
    case 401:
      return "Your session has expired. Sign in again, then retry this action.";
    case 403:
      return "Your account does not have permission to complete this action. Ask a platform administrator to review your access level.";
    case 404:
      return "This record or service is no longer available. Refresh the page and try again.";
    case 405:
      return "This action is not supported by the current server version. Refresh the application and try again.";
    case 409:
      return "This change conflicts with an existing record or a more recent update. Refresh the page and review the latest information.";
    default:
      return fallback;
  }
};

const getSafePublicDetails = (details?: string): string | null => {
  const value = details
    ?.split(/\r?\n/)
    .filter((line) => !line.trim().startsWith("CorrelationId:"))
    .join("\n")
    .trim();
  if (!value || value.length > 500) return null;

  const normalizedValue = value.toLowerCase();
  const looksLikeTechnicalDiagnostics =
    value.includes("\n   at ") ||
    normalizedValue.includes("stack trace:") ||
    normalizedValue.includes("connection string") ||
    value.includes("/src/") ||
    value.includes("\\src\\");

  return looksLikeTechnicalDiagnostics ? null : value;
};

export const getRequestErrorMessage = (error: unknown, fallback: string): string => {
  if (error instanceof AbpHttpError) {
    const validationMessage = error.validationErrors
      .map((validationError) => validationError.message.trim())
      .filter(Boolean)
      .join(" ");
    if (validationMessage) return validationMessage;

    const publicDetails = getSafePublicDetails(error.details);
    if (publicDetails) return publicDetails;

    if (error.message && !GENERIC_SERVER_MESSAGES.has(error.message)) {
      return error.message;
    }

    return getStatusMessage(error.status, fallback);
  }

  if (error instanceof Error) {
    return error.message && !GENERIC_SERVER_MESSAGES.has(error.message)
      ? error.message
      : fallback;
  }

  return fallback;
};

export const normalizeAbpError = (
  status: number,
  data: AbpErrorEnvelope | undefined,
): AbpHttpError => {
  const payload = data?.error ?? data ?? {};

  return new AbpHttpError(status, {
    code: payload.code,
    message: payload.message,
    details: payload.details,
    validationErrors: payload.validationErrors,
    correlationId: payload.correlationId,
  });
};

const looksLikeCertificateError = (error: unknown): boolean => {
  if (!(error instanceof Error)) {
    return false;
  }

  const haystack = `${error.message} ${error.name}`.toLowerCase();
  return (
    haystack.includes("certificate") ||
    haystack.includes("ssl") ||
    haystack.includes("tls") ||
    haystack.includes("err_cert") ||
    haystack.includes("depth_zero_self_signed")
  );
};

export const normalizeNetworkError = (error?: unknown): AbpHttpError => {
  if (looksLikeCertificateError(error)) {
    return new AbpHttpError(0, {
      code: "Aqua:Tls",
      message: CERTIFICATE_ERROR_MESSAGE,
    });
  }

  return new AbpHttpError(0, {
    code: "Aqua:Network",
    message: NETWORK_ERROR_MESSAGE,
  });
};

const isRecord = (value: unknown): value is Record<string, unknown> =>
  typeof value === "object" && value !== null;

export const isAbpResponseEnvelope = <TResponse>(
  data: TResponse | AbpResponseEnvelope<TResponse>,
): data is AbpResponseEnvelope<TResponse> =>
  isRecord(data) && data.__abp === true && typeof data.success === "boolean";

export const unwrapAbpResponse = <TResponse>(
  data: TResponse | AbpResponseEnvelope<TResponse>,
): TResponse => {
  if (!isAbpResponseEnvelope(data)) {
    return data;
  }

  if (!data.success) {
    throw new AbpHttpError(200, data.error ?? { message: "The request failed." });
  }

  return data.result;
};
