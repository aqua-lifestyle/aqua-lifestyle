import { z } from "zod";

const serverEnvSchema = z.object({
  NEXTAUTH_SECRET: z.string().min(32),
});

export type ServerEnv = z.infer<typeof serverEnvSchema>;

export const getServerEnv = (): ServerEnv =>
  serverEnvSchema.parse({
    NEXTAUTH_SECRET: process.env.NEXTAUTH_SECRET,
  });
