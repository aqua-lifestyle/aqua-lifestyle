import { z } from "zod";

const publicEnvSchema = z.object({
  NEXT_PUBLIC_ABP_API_URL: z.string().url(),
});

export const publicEnv = publicEnvSchema.parse({
  NEXT_PUBLIC_ABP_API_URL: process.env.NEXT_PUBLIC_ABP_API_URL,
});

export type PublicEnv = typeof publicEnv;
