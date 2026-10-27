// The deployed build (dist/) cannot turn on test hooks: the .NET export that enables them exists only in the test
// build, the runtime's global accessor is removed after start, and the hidden state control does nothing.
import { readdirSync, readFileSync, statSync } from "node:fs";
import { join } from "node:path";
import { expect, test } from "./support/fixtures.js";

const files = (dir) => readdirSync(dir).flatMap((n) => { const p = join(dir, n); return statSync(p).isDirectory() ? files(p) : [p]; });
const carries = (dir) => files(dir).filter((f) => { const b = readFileSync(f); return b.toString("latin1").includes("EnableTestHooks") || b.toString("utf16le").includes("EnableTestHooks"); });

test("the production .NET build has no EnableTestHooks export; the test build does (negative control) [VPO-c41f10e4ed8a086b] [VPO-fba8a61be6d12adf]", async () => {
  expect(carries("dist/wasm")).toEqual([]);
  expect(carries("obj/site-test/wasm").length).toBeGreaterThan(0);
});

test("in the production page neither the runtime global nor the page can enable the Percepta hook or fixture mode [VPO-fba8a61be6d12adf]", async ({ page }) => {
  await page.goto("index.html");
  await expect(page.locator("#causa-boot")).toHaveCount(0, { timeout: 60_000 });
  const exposed = await page.evaluate(() => ({ runtime: typeof globalThis.getDotnetRuntime, hook: typeof window.__perceptaSetState }));
  expect(exposed).toEqual({ runtime: "undefined", hook: "undefined" });
  // Driving the hidden state control directly is refused by the application.
  await page.evaluate(() => {
    const input = document.getElementById("percepta-state");
    input.value = JSON.stringify({ name: "forced", predicates: [], capabilities: {} });
    input.dispatchEvent(new Event("change", { bubbles: true }));
  });
  await page.waitForTimeout(500);
  expect(await page.locator("body").getAttribute("data-percepta-state")).toBe("");
  await expect(page.getByRole("heading", { level: 1, name: "Connect a data repository" })).toBeVisible();
  await expect(page.locator("text=Fixture data")).toHaveCount(0);
  // The application still works without the global: a round trip through the kernel.
  await page.getByRole("navigation", { name: "Primary" }).getByRole("link", { name: "Settings" }).click();
  await expect(page.getByRole("heading", { level: 1 })).toHaveText("Settings");
});
