// Real-browser checks for the Causa application.
//
// Two builds are served: the production site (dist/, exactly what GitHub Pages deploys) on 4321, and the test
// configuration (obj/site-test/, the same site with the Percepta state hook enabled) on 4322. Build both first:
// `npm run build && npm run build:test`. CAUSA_BASE_URL runs the production suites against a deployed site instead.
import { existsSync } from "node:fs";
import { defineConfig, devices } from "@playwright/test";

const port = 4321;
const testPort = 4322;
const origin = `http://127.0.0.1:${port}`;
const deployed = process.env.CAUSA_BASE_URL;

// Some environments ship a Chromium that Playwright did not download itself; use it where it exists.
const preinstalledChromium = "/opt/pw-browsers/chromium";
const executable = process.env.CAUSA_CHROMIUM_PATH ?? (existsSync(preinstalledChromium) ? preinstalledChromium : undefined);
const launchOptions = executable ? { executablePath: executable } : {};

export default defineConfig({
  testDir: "./tests/browser",
  fullyParallel: false,
  workers: 1,
  forbidOnly: !!process.env.CI,
  retries: 0,
  timeout: 90_000,
  reporter: process.env.CI ? [["github"], ["list"], ["json"]] : [["list"]],
  use: { baseURL: deployed ?? `${origin}/`, trace: "retain-on-failure" },
  // iPad portrait and landscape and a 320px phone run the tests tagged @responsive (UI-011, UI-020-O3, UI-029-O3).
  projects: [
    { name: "chromium", use: { ...devices["Desktop Chrome"], launchOptions } },
    { name: "ipad-portrait", grep: /@responsive/, use: { ...devices["Desktop Chrome"], viewport: { width: 820, height: 1180 }, hasTouch: true, launchOptions } },
    { name: "ipad-landscape", grep: /@responsive/, use: { ...devices["Desktop Chrome"], viewport: { width: 1180, height: 820 }, hasTouch: true, launchOptions } },
    { name: "phone-320", grep: /@responsive/, use: { ...devices["Desktop Chrome"], viewport: { width: 320, height: 640 }, hasTouch: true, launchOptions } },
  ],
  webServer: deployed ? undefined : [
    { command: `node scripts/serve.mjs dist ${port}`, url: `${origin}/index.html`, reuseExistingServer: !process.env.CI, timeout: 60_000 },
    { command: `node scripts/serve.mjs obj/site-test ${testPort}`, url: `http://127.0.0.1:${testPort}/index.html`, reuseExistingServer: !process.env.CI, timeout: 60_000 },
  ],
});
