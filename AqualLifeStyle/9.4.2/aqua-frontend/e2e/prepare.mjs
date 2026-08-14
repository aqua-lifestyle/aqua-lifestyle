import { cpSync, existsSync } from "node:fs";
import { spawnSync } from "node:child_process";

const env = {
  ...process.env,
  NEXT_PUBLIC_ABP_API_URL: "http://127.0.0.1:3200",
  NEXT_PUBLIC_DEFAULT_TENANT_NAME: "Default",
  NEXTAUTH_SECRET: "e2e-only-placeholder-secret-at-least-32-characters",
};
const build = spawnSync("npm", ["run", "build"], { env, stdio: "inherit" });
if (build.status !== 0) process.exit(build.status ?? 1);

if (existsSync("public")) cpSync("public", ".next/standalone/public", { recursive: true });
cpSync(".next/static", ".next/standalone/.next/static", { recursive: true });
