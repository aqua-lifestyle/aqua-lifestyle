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

export const normalizeNetworkError = (): AbpHttpError => {
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
