// Every page configuration starts: the F# application answers Limen's first message without an engine fault.
// Fails on the Mono interpreter's "locals size too big" (a method grew past its limit) or any internal start failure.
import { expect, test } from "./support/fixtures.js";

const TEST_SITE = `http://127.0.0.1:${process.env.CAUSA_TEST_PORT ?? 4822}/`;
const routes = ["", "#/investigations", "#/administration", "#/settings", "#/investigations/new", "#/no/such/page"];

for (const [name, base] of [["production", ""], ["test build", TEST_SITE]]) {
  test(`the ${name} starts on every primary route with no engine fault [VPO-0bd752bd2ac89796]`, { tag: ["@stack-smoke-limen", "@webkit"] }, async ({ page }) => {
    for (const route of routes) {
      await page.goto(base + "index.html" + route);
      await expect(page.locator("#causa-boot")).toHaveCount(0, { timeout: 60_000 });
      const faults = await page.evaluate(() => (window.causaDiagnostics ?? []).filter((d) => d.kind === "BridgeError" || d.kind === "StartFailed" || /locals size too big|CAUSA\\.INTERNAL/.test(JSON.stringify(d))));
      expect(faults, route).toEqual([]);
      await expect(page.locator("[data-percepta-screen]")).toHaveCount(1);
    }
  });
}
