import { z } from "zod";

const systemHealthSchema = z.object({
  status: z.string().min(1),
  isDatabaseReachable: z.boolean(),
  databaseStatus: z.string().min(1),
  version: z.string().min(1),
  releaseDate: z.string().min(1),
  checkedAtUtc: z.string().min(1),
  environment: z.string().min(1),
  traceId: z.string().min(1),
});

export type SystemHealth = z.infer<typeof systemHealthSchema>;

export const parseSystemHealth = (value: unknown): SystemHealth => {
  return systemHealthSchema.parse(value);
};

export const isSystemHealthContractError = (error: unknown) => {
  return error instanceof z.ZodError;
};
