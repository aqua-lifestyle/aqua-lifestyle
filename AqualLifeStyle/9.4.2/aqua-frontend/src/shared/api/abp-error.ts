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
  "We couldn't connect to Aqua Lifestyle Club. Check your connection, or contact the club team if the problem continues.";

const CERTIFICATE_ERROR_MESSAGE =
  "We couldn't establish a secure connection to Aqua Lifestyle Club. Please contact the club team if the problem continues.";

export type RequestErrorPresentation = {
  title: string;
  message: string;
  reviewPaymentStatus?: boolean;
};

export const aqGreenAdmissionGuidance: RequestErrorPresentation = {
  title: "Before you join AQGreen",
  message: "Open a valid AQGreen invitation from an eligible Club Member to join their network. If you are being considered as the first network founder, contact the club team: an authorised Host administrator must confirm your founder eligibility and authorise the qualifying registered, email-verified account before you can pay. After authorisation, refresh My programmes to see your joining options.",
};

const getDomainError = (error: unknown): RequestErrorPresentation | undefined => {
  if (!(error instanceof Error)) return;
  const abp = error instanceof AbpHttpError ? error : undefined;
  const text = `${abp?.code ?? ""} ${error.message} ${abp?.details ?? ""} ${abp?.validationErrors.map(item => item.message).join(" ") ?? ""}`;
  if (/Independent admission and recruiter-only joining|joining requires a sponsor invitation|Root admission is a separate privileged action|D10_CLEAN_START_ROOT_CUSTOMER_REQUIRED_OR_MISMATCHED/.test(text))
    return aqGreenAdmissionGuidance;
  if (/inviting Club Member.*not currently eligible|recruiter.*not.*eligible|active.*recruiter.*required/i.test(text))
    return { title: "This invitation is not ready to use", message: "The inviting member is not currently eligible to invite you to this programme. Ask them or the club team to check their participation before you continue." };
  if (/D10_CLEAN_START_ROOT_VERIFIED_REGISTRATION_REQUIRED/.test(text))
    return { title: "Founder eligibility needs review", message: "This account does not meet the current first-founder registration requirements. Contact the club team to check your eligibility and the approved founder scope. To join an existing network instead, use a valid member invitation." };
  if (/email.*not confirmed|verify your email|email verification.*required/i.test(text))
    return { title: "Verify your email to continue", message: "Open the verification email sent to your address, then sign in again. If you need a new email, use Resend verification email on the sign-in page." };
  if (/invitation (?:code was not found|could not be accepted|is for a different programme|belongs to a different organisation|organisation is unavailable|link is not valid)|Enter a valid invitation code|field InviteCode|cannot accept your own invitation/i.test(text))
    return { title: "Invitation unavailable", message: "This invitation cannot be used with your account for this programme. Open the original link from your inviting member, or ask them for a valid invitation. Contact the club team if you still need help." };
  if (/still being prepared|checkout is still active|payment is already awaiting completion|competing checkout|authoritative terminal evidence/i.test(text))
    return { title: "Check your payment status", message: "A joining checkout is already being prepared or awaiting confirmation. Check My programmes for its current status or continue the existing checkout when available. Do not start a second payment; contact the club team if the status does not update.", reviewPaymentStatus: true };
  if (/YOCO_CUTOVER_BUSINESS_WRITES_FENCED|CLEAN_START_COMMISSIONING_HELD|Online payment is temporarily unavailable|payment.*(?:temporarily unavailable|not available)|Aqua:PaymentUnavailable/i.test(text))
    return { title: "Payments are temporarily unavailable", message: "Please check My programmes again later or contact the club team. If you have already tried to pay, check that payment's status before starting another one." };
  if (/Aqua:PaymentFailed|PAYMENT_FAILED/i.test(text) ||
      [error.message, abp?.details].some(value => /^\s*Payment failed[.!]?\s*$/i.test(value ?? "")))
    return { title: "Payment could not be completed", message: "Check the recorded payment status in My programmes before starting another payment. If money left your account or the result is unclear, contact the club team for help.", reviewPaymentStatus: true };
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
    /\b(?:D10|YOCO|AQGREEN)_[A-Z0-9_]+\b|\b(?:Npgsql|SqlException|InvalidOperationException|TypeError|ReferenceError)\b/.test(value) ||
    normalizedValue.includes("cannot read properties") ||
    value.includes("/src/") ||
    value.includes("\\src\\");

  return looksLikeTechnicalDiagnostics ? null : value;
};

export const getRequestErrorMessage = (error: unknown, fallback: string): string => {
  const domainError = getDomainError(error);
  if (domainError) return domainError.message;
  if (error instanceof AbpHttpError) {
    const validationMessage = error.validationErrors
      .map((validationError) => getSafePublicDetails(validationError.message))
      .filter(Boolean)
      .join(" ");
    if (validationMessage) return validationMessage;

    const publicDetails = getSafePublicDetails(error.details);
    if (publicDetails) return publicDetails;

    const publicMessage = getSafePublicDetails(error.message);
    if (publicMessage && !GENERIC_SERVER_MESSAGES.has(publicMessage)) {
      return publicMessage;
    }

    return getStatusMessage(error.status, fallback);
  }

  if (error instanceof Error) {
    if (["ZodError", "TypeError", "ReferenceError", "SyntaxError"].includes(error.name)) return fallback;
    const publicMessage = getSafePublicDetails(error.message);
    return publicMessage && !GENERIC_SERVER_MESSAGES.has(publicMessage)
      ? publicMessage
      : fallback;
  }

  return fallback;
};

/** Presentation belongs to the existing ABP error path; diagnostics stay on the error. */
export const getRequestErrorPresentation = (
  error: unknown,
  fallback: string,
  context: "general" | "admission" | "invitation" | "checkout" = "general",
): RequestErrorPresentation => {
  const domainError = getDomainError(error);
  if (domainError) return { ...domainError, reviewPaymentStatus: context === "checkout" || domainError.reviewPaymentStatus };
  if (context === "checkout") return {
    title: "Payment status not confirmed",
    message: "We couldn't confirm the result of your checkout request. Check My programmes before trying to pay again. If money left your account or the status remains unclear, contact the club team. Do not start a second payment while the outcome is being checked.",
    reviewPaymentStatus: true,
  };
  if (context === "invitation") return {
    title: "Invitation unavailable",
    message: "We couldn't open this invitation. Check your connection and open the original invitation link again, or contact the club team for help.",
  };
  if (context === "admission") return {
    title: "Joining status not confirmed",
    message: "We couldn't confirm your joining status. Check My programmes before continuing, or contact the club team for help.",
    reviewPaymentStatus: true,
  };
  return { title: "We couldn't complete that action", message: getRequestErrorMessage(error, fallback) };
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
