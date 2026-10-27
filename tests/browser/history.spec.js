// T28: history and time travel through the real F# kernel (Timeline.entries/view/changesAt/between) in the test
// build's in-memory workspace.
import { expect, test } from "./support/fixtures.js";

const SITE = `http://127.0.0.1:${process.env.CAUSA_TEST_PORT ?? 4322}/`;
const started = (page) => expect(page.locator("#causa-boot")).toHaveCount(0, { timeout: 60_000 });

async function threeVersions(page) {
  await page.goto(SITE + "index.html#/investigations/new");
  await started(page);
  await page.evaluate(() => window.__perceptaSetState({ name: "engine-memory", predicates: [], capabilities: {} }));
  await page.getByLabel("Title").fill("Conveyor stoppage");
  await page.getByLabel(/Problem statement/).fill("Line 4 conveyor stopped twice on Tuesday.");
  await page.getByRole("button", { name: "Start the investigation" }).click();
  await page.getByRole("button", { name: "Activate the investigation" }).click();
  await page.getByLabel("Hypothesis statement").fill("A worn drive belt slipped.");
  await page.getByRole("button", { name: "Record a hypothesis" }).click();
  await expect(page.locator(".causa-inv-meta")).toContainText("Version 3");
  await page.getByRole("navigation", { name: "Investigation sections" }).getByRole("link", { name: "Timeline" }).click();
}

test("the timeline lists events in sequence with event type, actor, event time and version; filtering by category works; an entry links to the record it touched [VPO-33324aeb860bce06] [VPO-c3326322b8ed473a] [VPO-7fe0d0f0d00df8a0] [VPO-db25c20e9414ab46]", { tag: ["@webkit"] }, async ({ page }) => {
  await threeVersions(page);
  const entries = page.locator(".causa-timeline__entry");
  await expect(entries).toHaveCount(3);
  await expect(entries.nth(0)).toContainText("#1");
  await expect(entries.nth(0)).toContainText("InvestigationStarted");
  await expect(entries.nth(1)).toContainText("LifecycleChanged");
  await expect(entries.nth(2)).toContainText("HypothesisRecorded");
  await expect(entries.nth(2)).toContainText("causa-user");
  await expect(entries.nth(2)).toContainText("Event time ");
  // Filter (UI-003-O2).
  await page.getByLabel("Show events about").selectOption("hypotheses");
  await expect(entries).toHaveCount(1);
  await page.getByLabel("Show events about").selectOption("all");
  await expect(entries).toHaveCount(3);
  // The hypothesis event links to the hypothesis it recorded (UI-003-O4).
  await entries.nth(2).getByRole("link", { name: /hypothesis: A worn drive belt slipped\./ }).click();
  await expect(page).toHaveURL(/\/hypotheses\?item=[0-9a-f-]{36}$/);
  await expect(page.getByText("A worn drive belt slipped.")).toBeVisible();
});

test("a past version is shown labelled historical with what it changed and what changed since; the latest is never mixed in [VPO-d08de632c7c1a4ab] [VPO-c3326322b8ed473a]", { tag: ["@webkit"] }, async ({ page }) => {
  await threeVersions(page);
  await page.locator(".causa-timeline__entry").nth(0).getByRole("link", { name: "#1" }).click();
  await expect(page).toHaveURL(/\/timeline\?version=1$/);
  const label = page.locator("[data-percepta-region=version-state]");
  await expect(label).toHaveText("Historical view: version 1 of 3. This is not the current state.");
  await expect(label).toHaveAttribute("data-historical", "true");
  const state = page.locator(".causa-history");
  await expect(state).toContainText("Status: Draft");
  await expect(state).toContainText("Hypotheses: 0");
  await expect(state.locator(".causa-diff").first()).toContainText("Status set to Draft");
  await expect(state.locator(".causa-diff").first()).toContainText("Added observation");
  await expect(state.locator(".causa-diff").nth(1)).toContainText("Status: Draft → Active");
  await expect(state.locator(".causa-diff").nth(1)).toContainText("Added hypothesis");
  // The picker moves to another version; the latest version is not labelled historical.
  await page.getByLabel("Show the investigation as it was at").selectOption("3");
  await expect(label).toHaveText("Showing the investigation as of version 3, the latest recorded version.");
  await expect(page.locator(".causa-history")).toContainText("Hypotheses: 1");
  await expect(page.locator(".causa-history")).toHaveAttribute("data-historical", "false");
});
