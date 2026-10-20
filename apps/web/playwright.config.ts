import { defineConfig, devices } from "@playwright/test";

const baseURL = process.env.BLINDMESH_WEB_URL ?? "http://127.0.0.1:3000";
const useExistingServer = Boolean(process.env.BLINDMESH_WEB_URL);

/**
 * The browser journeys of the product. The web application is started by
 * Playwright unless an address is provided through BLINDMESH_WEB_URL, so the
 * same suite runs against a local build and against the compose stack.
 */
export default defineConfig({
  testDir: "../../tests/e2e",
  fullyParallel: false,
  workers: 1,
  forbidOnly: Boolean(process.env.CI),
  retries: 0,
  timeout: 60_000,
  expect: { timeout: 10_000 },
  reporter: [["list"]],
  use: {
    baseURL,
    trace: "retain-on-failure",
    screenshot: "only-on-failure",
  },
  projects: [
    {
      name: "chromium",
      testMatch: "**/*.spec.ts",
      use: { ...devices["Desktop Chrome"] },
    },
    {
      name: "a11y",
      testMatch: "**/*.a11y.ts",
      use: { ...devices["Desktop Chrome"] },
    },
  ],
  webServer: useExistingServer
    ? undefined
    : {
        command: "npm run start -- --port 3100",
        url: "http://127.0.0.1:3100",
        reuseExistingServer: true,
        timeout: 180_000,
        env: {
          BLINDMESH_API_URL: process.env.BLINDMESH_API_URL ?? "http://127.0.0.1:8080",
        },
      },
});
