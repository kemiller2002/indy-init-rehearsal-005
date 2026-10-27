// Local fixture mode (PF-003): the test build serves a synthetic investigation, generated in the page by the engine
// and answered by the recorded-behaviour GitHub through the same persistence program as real GitHub (PF-001). The
// network guard (support/fixtures.js) fails any test that reaches the network; fixture mode sends nothing at all.
//
// The documented command: `npm run build:test && node scripts/serve.mjs obj/site-test 4322`, then Connect to the
// repository `local/fixture` (branch `main`, workspace `synthetic-fixture`; the token field is not used).
import { readFileSync } from "node:fs";
import { expect, test } from "./support/fixtures.js";

const TEST_SITE = `http://127.0.0.1:${process.env.CAUSA_TEST_PORT ?? 4322}/`;

async function started(page) {
  await expect(page.locator("#causa-boot")).toHaveCount(0, { timeout: 90_000 });
}

async function connectFixture(page) {
  const main = page.getByRole("main");
  await main.getByLabel("Fine-grained GitHub token").fill("not-used-in-fixture-mode");
  await main.getByLabel("Repository (owner/name)").fill("local/fixture");
  await main.getByLabel("Branch").fill("main");
  await main.getByLabel("Workspace").fill("synthetic-fixture");
  await main.getByRole("button", { name: "Connect" }).click();
}

test(
  "fixture mode in the test build: the synthetic investigation loads through the same program, the mode is shown conspicuously and never as GitHub, and nothing is sent [VPO-6eaf42e86c5ffad7] [VPO-d4b0d50550578aeb] [VPO-0a23bb42b8fb58b4] [VPO-ad958e7a60d7fd9b] [VPO-b148b239fc1c24b0]",
  async ({ page, githubRequests }) => {
    await page.goto(TEST_SITE + "index.html");
    await started(page);
    await connectFixture(page);
    // Conspicuous and honest: a masthead tag and the connection label say it is synthetic, not GitHub.
    await expect(page.locator(".causa-fixture-mode")).toHaveText("Fixture data: synthetic, not GitHub", { timeout: 60_000 });
    // Administration names the provider plainly: synthetic data generated in this page, nothing from or to GitHub.
    await page.goto(TEST_SITE + "index.html#/administration");
    await expect(page.getByRole("row", { name: /Data provider/ })).toContainText("Local fixture: synthetic data generated in this page");
    await page.goto(TEST_SITE + "index.html#/investigations");
    const card = page.locator(".causa-card");
    await expect(card).toHaveCount(1, { timeout: 60_000 });
    await expect(card).toContainText("Synthetic fixture: checkout service returned errors");
    await card.locator("a").first().click();
    await expect(page.getByRole("heading", { level: 1, name: /Synthetic fixture/ })).toBeVisible();
    // The causal analysis is drawn from the fixture through the same domain-to-graph projection as GitHub data.
    await page.goto(page.url().replace(/(#\/investigations\/[0-9a-f-]{36}).*/, "$1/causal-analysis"));
    await expect(page.getByRole("list", { name: "Causal relationships as text" })).toContainText("Checkout returned HTTP 500", { timeout: 60_000 });
    // A change is kept in this page only and never shown as saved to GitHub.
    const observation = page.locator('input[aria-labelledby="input-label-RecordObservation"]');
    await page.goto(page.url().replace(/(#\/investigations\/[0-9a-f-]{36}).*/, "$1"));
    await observation.fill("An observation made in fixture mode.");
    await page.locator('button[data-percepta-capability="RecordObservation"]').first().click();
    await expect(page.locator(".causa-sync [role=status]")).toContainText("kept in this page only; nothing is saved to GitHub", { timeout: 60_000 });
    await expect(page.locator("body")).not.toContainText("Saved to GitHub");
    // No network, no credential: GitHub was never contacted, and nothing was stored in the browser.
    expect(githubRequests).toEqual([]);
    const stored = await page.evaluate(() => JSON.stringify({ ...localStorage }));
    expect(stored).not.toContain("Synthetic fixture");
  },
);

test(
  "the production build has no fixture mode: Connect to local/fixture is refused before anything is sent, and neither the URL, storage nor a page global switches it on [VPO-00c1408c9f31d4a2] [VPO-0a23bb42b8fb58b4]",
  async ({ page, githubRequests }) => {
    await page.goto("index.html?fixture=1#/?mode=fixture");
    await started(page);
    // Storage and the URL are not switches.
    await page.evaluate(() => { try { localStorage.setItem("causa.fixture", "1"); sessionStorage.setItem("causa.fixture", "1"); } catch {} });
    // No page global switches fixture mode or the test hooks on.
    const hooks = await page.evaluate(() => Object.keys(globalThis).filter((k) => /test.?hook|fixture|percepta.?state/i.test(k)));
    expect(hooks).toEqual([]);
    await connectFixture(page);
    await expect(page.getByRole("alert").filter({ hasText: "Fixture mode exists only in the test build" })).toHaveCount(1, { timeout: 60_000 });
    await expect(page.locator(".causa-fixture-mode")).toHaveCount(0);
    expect(githubRequests).toEqual([]);
    // The shipped site's composition root has the test switch compiled off; only the test build turns it on.
    expect(readFileSync("dist/main.js", "utf8")).toContain("const TEST_BUILD = false");
  },
);

/** Whether the page claims a live write to GitHub anywhere (the check UI-015-O3 relies on). */
async function claimsLiveWrite(page) {
  return (await page.locator("body").innerText()).includes("Saved to GitHub");
}

async function openFixtureInvestigation(page, workspace) {
  await page.goto(TEST_SITE + "index.html");
  await started(page);
  const main = page.getByRole("main");
  await main.getByLabel("Fine-grained GitHub token").fill("not-used-in-fixture-mode");
  await main.getByLabel("Repository (owner/name)").fill("local/fixture");
  await main.getByLabel("Branch").fill("main");
  await main.getByLabel("Workspace").fill(workspace);
  await main.getByRole("button", { name: "Connect" }).click();
  await expect(page.locator(".causa-fixture-mode")).toBeVisible({ timeout: 60_000 });
  await page.goto(TEST_SITE + "index.html#/investigations");
  await page.locator(".causa-card a").first().click();
  await expect(page.getByRole("heading", { level: 1, name: /Synthetic fixture/ })).toBeVisible({ timeout: 60_000 });
}

async function recordObservation(page, text) {
  await page.locator('input[aria-labelledby="input-label-RecordObservation"]').fill(text);
  await page.locator('button[data-percepta-capability="RecordObservation"]').first().click();
}

test(
  "UI-015-O3 mocked persistence is never represented as a live write, and the check is shown to catch a live claim (negative control) [VPO-5c8d5917d17f8960] [VPO-0a23bb42b8fb58b4]",
  async ({ page, githubRequests }) => {
    await openFixtureInvestigation(page, "synthetic-fixture");
    await recordObservation(page, "A change made against the fixture.");
    await expect(page.locator(".causa-sync [role=status]")).toContainText("nothing is saved to GitHub", { timeout: 60_000 });
    expect(await claimsLiveWrite(page)).toBe(false);
    for (const route of ["#/investigations", "#/administration"]) {
      await page.goto(TEST_SITE + "index.html" + route);
      await started(page).catch(() => {});
      expect(await claimsLiveWrite(page)).toBe(false);
    }
    expect(githubRequests).toEqual([]);
    // Negative control: the same check does detect a live-write claim when one is on the page.
    await page.evaluate(() => {
      const p = document.createElement("p");
      p.textContent = "Saved to GitHub (confirmed 2026-10-27T12:00:00.000Z)";
      document.body.appendChild(p);
    });
    expect(await claimsLiveWrite(page)).toBe(true);
  },
);

test(
  "a conflict is resolved in the browser: Reload keeps this device's change beside the repository's copy, Discard (confirmed) adopts the repository's copy [VPO-34731ac9a977b818] [VPO-cd9dd8ec730c3d3e]",
  async ({ page, githubRequests }) => {
    // The synthetic-conflict workspace: another writer commits just before this page's first save (deterministic).
    await openFixtureInvestigation(page, "synthetic-conflict");
    await recordObservation(page, "My change, decided before the other writer's commit.");
    const conflict = page.locator(".causa-conflict");
    await expect(conflict).toContainText("Someone else changed this investigation", { timeout: 60_000 });
    await expect(page.locator(".causa-sync [role=status]")).toContainText("someone else changed the repository");
    // Reload: the repository's copy is read again; the local change is kept and the conflict stands.
    await conflict.getByRole("button", { name: "Reload the version in GitHub (keep my change)" }).click();
    await expect(conflict).toBeVisible();
    await expect(page.getByText("My change, decided before the other writer's commit.").first()).toBeVisible();
    // Discard asks first; Cancel changes nothing.
    await conflict.getByRole("button", { name: "Discard my local change" }).click();
    const dialog = page.getByRole("alertdialog");
    await expect(dialog).toContainText("cannot be recovered");
    await dialog.getByRole("button", { name: "Cancel" }).click();
    await expect(conflict).toBeVisible();
    await expect(page.getByText("My change, decided before the other writer's commit.").first()).toBeVisible();
    // Confirmed: the repository's copy is adopted, the other writer's work appears and the local change is gone.
    await conflict.getByRole("button", { name: "Discard my local change" }).click();
    await page.getByRole("alertdialog").getByRole("button", { name: "Yes, continue" }).click();
    await expect(conflict).toHaveCount(0, { timeout: 60_000 });
    await expect(page.getByText("Another writer's hypothesis: a certificate rotation.").first()).toBeVisible();
    await expect(page.getByText("My change, decided before the other writer's commit.")).toHaveCount(0);
    expect(githubRequests).toEqual([]);
  },
);
