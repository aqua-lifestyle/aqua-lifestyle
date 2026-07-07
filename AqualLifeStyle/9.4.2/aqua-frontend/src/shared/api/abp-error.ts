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
