import { fileURLToPath } from "node:url";
import { defineConfig } from "vitest/config";

export default defineConfig({
  resolve: {
    alias: {
      "@": fileURLToPath(new URL(".", import.meta.url)),
    },
  },
  test: {
    environment: "jsdom",
    exclude: ["e2e/**", "e2e-real/**", "node_modules/**"],
    globals: true,
    setupFiles: ["./vitest.setup.ts"],
  },
});
