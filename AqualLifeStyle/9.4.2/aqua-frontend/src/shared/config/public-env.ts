import { z } from "zod";

const publicEnvSchema = z.object({
  NEXT_PUBLIC_ABP_API_URL: z.string().url(),
  NEXT_PUBLIC_DEFAULT_TENANT_NAME: z.string().default("Default"),
  NEXT_PUBLIC_MONITORING_ENDPOINT: z.string().url().optional(),
  NEXT_PUBLIC_SELF_REGISTRATION_ENABLED: z
    .enum(["true", "false"])
    .default("false")
    .transform((value) => value === "true"),
});

export const publicEnv = publicEnvSchema.parse({
  NEXT_PUBLIC_ABP_API_URL: process.env.NEXT_PUBLIC_ABP_API_URL,
  NEXT_PUBLIC_DEFAULT_TENANT_NAME: process.env.NEXT_PUBLIC_DEFAULT_TENANT_NAME,
  NEXT_PUBLIC_MONITORING_ENDPOINT:
    process.env.NEXT_PUBLIC_MONITORING_ENDPOINT || undefined,
  NEXT_PUBLIC_SELF_REGISTRATION_ENABLED:
    process.env.NEXT_PUBLIC_SELF_REGISTRATION_ENABLED,
});

export type PublicEnv = typeof publicEnv;
