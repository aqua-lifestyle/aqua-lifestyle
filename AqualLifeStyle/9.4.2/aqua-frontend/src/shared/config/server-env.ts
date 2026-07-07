import "server-only";
import { z } from "zod";

const serverEnvSchema = z.object({
  NEXTAUTH_SECRET: z.string().min(32),
});

export const serverEnv = serverEnvSchema.parse({
  NEXTAUTH_SECRET: process.env.NEXTAUTH_SECRET,
});

export type ServerEnv = typeof serverEnv;
