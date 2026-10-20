import { defineConfig } from "@playwright/test";
export default defineConfig({
  testDir: ".",
  testMatch: "*.spec.js",
  workers: 1,
  timeout: 30000,
  use: {
    baseURL: "http://127.0.0.1:4176",
    channel: process.env.VIEWER_BROWSER_CHANNEL,
    viewport: { width: 1366, height: 768 },
  },
  webServer: {
    command: "node host.mjs",
    url: "http://127.0.0.1:4176/test/host.html",
    reuseExistingServer: false,
  },
  reporter: "list",
});
