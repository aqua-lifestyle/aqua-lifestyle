import { AbpHttpError } from "./abp-error";

/**
 * Resolves a human-readable message from an unknown error thrown by an API call.
 *
 * Prefers the ABP error details/message, falls back to a native Error message,
 * and finally uses the provided fallback for non-error values.
 */
export const getErrorMessage = (error: unknown, fallback: string): string => {
  if (error instanceof AbpHttpError) {
    return error.details ?? error.message;
  }

  if (error instanceof Error) {
    return error.message;
  }

  return fallback;
};
