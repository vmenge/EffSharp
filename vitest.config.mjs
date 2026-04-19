import { defineConfig } from "vitest/config";

export default defineConfig({
  test: {
    environment: "node",
    include: [".fable-build/**/*.test.js"],
    watchExclude: [
      "**/node_modules/**",
      "**/.fable-build/**/fable_modules/**"
    ],
    coverage: {
      reporter: ["text", "html"]
    }
  }
});
