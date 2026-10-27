// The deployed site at the exact SHA this run built and verified (deployed-smoke job). No credentials are present
// in this job; nothing here signs in. Screenshots are written to SCREENSHOT_DIR and published with the run's
// acceptance evidence.
import { createHash } from "node:crypto";
import { mkdirSync, readFileSync, readdirSync, statSync, writeFileSync } from "node:fs";
import { join, relative } from "node:path";
import { expect, test } from "@playwright/test";

const expectedSha = process.env.EXPECTED_SHA ?? "";
const siteDir = process.env.SITE_DIR ?? "";
const shots = process.env.SCREENSHOT_DIR ?? "TestResults/deployed/screenshots";

function files(dir) {
  return readdirSync(dir).flatMap((name) => {
    const path = join(dir, name);
    return statSync(path).isDirectory() ? files(path) : [path];
  });
}

const sha256 = (bytes) => createHash("sha256").update(bytes).digest("hex");

test("EXT-DATA-009 EXT-DATA-001 VER-006 the Pages deployment serves exactly the verified build of this SHA, byte for byte, and it starts in a browser [VPO-b50ee1d4e3e78f25] [VPO-f8cbaa452cd4eb0b] [VPO-86b8bffb323a80b7]", async ({ page, request }) => {
  expect(expectedSha).toMatch(/^[0-9a-f]{40}$/);
  const version = await (await request.get(`version.json?ts=${Date.now()}`)).json();
  expect(version.sha).toBe(expectedSha);
  // Every file of the artifact this run built and scanned is what Pages serves (so the scan of the artifact is a
  // scan of the deployment), and nothing in the artifact is missing from Pages.
  const all = files(siteDir).filter((f) => !f.endsWith("version.json"));
  expect(all.length).toBeGreaterThan(50);
  const mismatches = [];
  for (const file of all) {
    const rel = relative(siteDir, file).split("\\").join("/");
    const response = await request.get(`${rel}?ts=${Date.now()}`);
    if (response.status() !== 200) { mismatches.push(`${rel}: HTTP ${response.status()}`); continue; }
    if (sha256(await response.body()) !== sha256(readFileSync(file))) mismatches.push(`${rel}: content differs`);
  }
  expect(mismatches).toEqual([]);
  // The deployed application starts in a real browser, with no test hook.
  await page.goto("index.html");
  await expect(page.locator("#causa-boot")).toHaveCount(0, { timeout: 90_000 });
  await expect(page.getByRole("heading", { level: 1 }).first()).toBeVisible();
  expect(await page.evaluate(() => typeof window.__perceptaSetState)).toBe("undefined");
});

const sizes = [
  { name: "desktop", width: 1440, height: 900 },
  { name: "ipad-portrait", width: 820, height: 1180 },
  { name: "ipad-landscape", width: 1180, height: 820 },
  { name: "mobile", width: 390, height: 844 },
];

test("PF-016 deployed exact-SHA screenshots at desktop, iPad portrait and landscape and mobile sizes [VPO-727b07dc99ecb7c5]", async ({ browser, request }) => {
  const version = await (await request.get(`version.json?ts=${Date.now()}`)).json();
  expect(version.sha).toBe(expectedSha);
  mkdirSync(shots, { recursive: true });
  const record = { schema: "causa.deployed-screenshots/1", url: process.env.DEPLOYED_URL, sha: version.sha, credentials: "none", screenshots: [] };
  for (const size of sizes) {
    const context = await browser.newContext({ viewport: { width: size.width, height: size.height }, hasTouch: size.name !== "desktop" });
    const page = await context.newPage();
    await page.goto("index.html");
    await expect(page.locator("#causa-boot")).toHaveCount(0, { timeout: 90_000 });
    await expect(page.getByRole("heading", { level: 1 }).first()).toBeVisible();
    // The page fills the viewport without horizontal scrolling.
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth + 1)).toBe(true);
    const file = join(shots, `${size.name}-${size.width}x${size.height}-${version.sha.slice(0, 12)}.png`);
    const png = await page.screenshot({ path: file, fullPage: true });
    expect(png.length).toBeGreaterThan(10_000);
    record.screenshots.push({ size: size.name, width: size.width, height: size.height, file: file.split("/").pop(), sha256: sha256(png) });
    await context.close();
  }
  writeFileSync(join(shots, "screenshots.json"), JSON.stringify(record, null, 2) + "\n");
});
