// Checks against the DEPLOYED site at the exact source SHA (the deployed-smoke job): no local server, no
// credentials, no test hooks. DEPLOYED_URL is the Pages URL; EXPECTED_SHA the commit the run built; SITE_DIR the
// site artifact the run deployed.
import { defineConfig, devices } from "@playwright/test";

export default defineConfig({
  testDir: ".",
  testMatch: /deployed\.spec\.js$/,
  fullyParallel: false,
  workers: 1,
  retries: 0,
  timeout: 120_000,
  reporter: [["list"], ["json"]],
  use: { baseURL: (process.env.DEPLOYED_URL ?? "").replace(/\/?$/, "/"), trace: "off", screenshot: "off", video: "off" },
  projects: [{ name: "chromium", use: { ...devices["Desktop Chrome"] } }],
});
