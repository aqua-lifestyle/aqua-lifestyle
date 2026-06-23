import { z } from "zod";

const publicEnvSchema = z.object({
  NEXT_PUBLIC_ABP_API_URL: z.string().url(),
  NEXT_PUBLIC_DEFAULT_TENANT_NAME: z.string().default("Default"),
  NEXT_PUBLIC_MONITORING_ENDPOINT: z.string().url().optional(),
});

export const publicEnv = publicEnvSchema.parse({
  NEXT_PUBLIC_ABP_API_URL: process.env.NEXT_PUBLIC_ABP_API_URL,
  NEXT_PUBLIC_DEFAULT_TENANT_NAME: process.env.NEXT_PUBLIC_DEFAULT_TENANT_NAME,
  NEXT_PUBLIC_MONITORING_ENDPOINT:
    process.env.NEXT_PUBLIC_MONITORING_ENDPOINT || undefined,
});

export type PublicEnv = typeof publicEnv;
