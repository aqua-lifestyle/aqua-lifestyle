const LOCALE = "en-ZA";
const CURRENCY = "ZAR";

/**
 * Formats a numeric amount as a ZAR currency string (e.g. "R1,234.00").
 */
export const formatCurrency = (amount: number): string =>
  new Intl.NumberFormat(LOCALE, {
    style: "currency",
    currency: CURRENCY,
  }).format(amount);

/**
 * Formats a percentage value expressed on a 0–100 scale (e.g. `12` -> "12%").
 */
export const formatPercent = (
  value: number,
  maximumFractionDigits = 0,
): string =>
  new Intl.NumberFormat(LOCALE, {
    style: "percent",
    maximumFractionDigits,
  }).format(value / 100);

type FormatDateOptions = {
  /** Include a short time component alongside the medium date. */
  withTime?: boolean;
  /** Value returned when the date is null/undefined/empty. */
  fallback?: string;
};

/**
 * Formats an ISO date string as a localized medium date, optionally with time.
 * Returns `fallback` (default "Not set") when the date is missing.
 */
export const formatDate = (
  date: string | null | undefined,
  { withTime = false, fallback = "Not set" }: FormatDateOptions = {},
): string => {
  if (!date) {
    return fallback;
  }

  return new Intl.DateTimeFormat(LOCALE, {
    dateStyle: "medium",
    ...(withTime ? { timeStyle: "short" } : {}),
  }).format(new Date(date));
};
