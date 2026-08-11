import { z } from "zod";

const systemHealthSchema = z.object({
  status: z.string().min(1),
  isDatabaseReachable: z.boolean(),
  databaseStatus: z.string().min(1),
  version: z.string().min(1),
  buildId: z.string().min(1),
  imageId: z.string().min(1),
  paymentContractVersion: z.string().min(1),
  contractCapabilities: z.array(z.string().min(1)),
  releaseDate: z.string().min(1),
  checkedAtUtc: z.string().min(1),
  environment: z.string().min(1),
  traceId: z.string().min(1),
});

export const REQUIRED_PAYMENT_CONTRACT_VERSION =
  "aqua-payments-2026-08-09-flexible-payment-approval";

const requiredPaymentCapabilities = [
  "aqgreen-flexible-joining-v1",
  "programme-approval-queue-v1",
  "direct-onyx-checkout-v1",
] as const;

export type SystemHealth = z.infer<typeof systemHealthSchema>;

export const parseSystemHealth = (value: unknown): SystemHealth => {
  return systemHealthSchema.parse(value);
};

export const isPaymentApiCompatible = (health: SystemHealth | null) =>
  health?.paymentContractVersion === REQUIRED_PAYMENT_CONTRACT_VERSION &&
  requiredPaymentCapabilities.every((capability) =>
    health.contractCapabilities.includes(capability),
  );

export const isProgrammeJourneyApiCompatible = (health: SystemHealth | null) =>
  health?.contractCapabilities.includes("member-programme-journey-v1") ?? false;

export const isSystemHealthContractError = (error: unknown) => {
  return error instanceof z.ZodError;
};
