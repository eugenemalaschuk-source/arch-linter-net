import { cloudflareTest } from "@cloudflare/vitest-pool-workers";
import { defineConfig } from "vitest/config";

export default defineConfig({
  plugins: [cloudflareTest({ wrangler: { configPath: "./wrangler.test.jsonc" } })],
  test: {
    include: ["tests/**/*.test.ts"],
    coverage: {
      exclude: ["tests/**"],
      include: ["src/**/*.ts"],
      provider: "istanbul",
      reporter: ["lcov"]
    }
  }
});
