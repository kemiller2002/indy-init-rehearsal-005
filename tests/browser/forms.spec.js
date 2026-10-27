// The investigation forms through the real F# kernel (test build, in-memory workspace): every submission is a
// typed command the engine decides; refused submissions change nothing.
import { expect, test } from "./support/fixtures.js";

const SITE = `http://127.0.0.1:${process.env.CAUSA_TEST_PORT ?? 4322}/`;
const started = (page) => expect(page.locator("#causa-boot")).toHaveCount(0, { timeout: 60_000 });
const section = (page, name) => page.getByRole("navigation", { name: "Investigation sections" }).getByRole("link", { name }).click();
const version = (page, n) => expect(page.locator(".causa-inv-meta")).toContainText(`Version ${n}`);

test("evidence, links, hypothesis changes, causes and corrective actions are recorded through validated forms the engine decides [VPO-b9af1bcfc961c8bb] [VPO-ff2e466b7aaa3e12] [VPO-be99a964783c59b5]", { tag: ["@webkit"] }, async ({ page }) => {
  await page.goto(SITE + "index.html#/investigations/new");
  await started(page);
  await page.evaluate(() => window.__perceptaSetState({ name: "engine-memory", predicates: [], capabilities: {} }));
  await page.getByLabel("Title").fill("Overheating inverter");
  await page.getByLabel(/Problem statement/).fill("Inverter 7 tripped on over-temperature at noon.");
  await page.getByRole("button", { name: "Start the investigation" }).click();
  await page.getByRole("button", { name: "Activate the investigation" }).click();
  await page.getByLabel("Hypothesis statement").fill("The cooling fan failed.");
  await page.getByRole("button", { name: "Record a hypothesis" }).click();
  await version(page, 3);

  // Evidence: the form validates required fields before anything is sent.
  await section(page, "Evidence");
  await page.getByRole("button", { name: "Admit evidence" }).click();
  await version(page, 3);
  await page.getByLabel("What the evidence shows, quoted as recorded").fill("Fan tachometer read 0 rpm from 11:52.");
  await page.getByLabel("Kind of evidence").selectOption("measurement");
  await page.getByLabel("Source").fill("Inverter 7 telemetry export");
  await page.getByRole("button", { name: "Admit evidence" }).click();
  await version(page, 4);
  const item = page.locator(".causa-item--evidence").filter({ hasText: "Fan tachometer read 0 rpm" });
  await expect(item).toContainText("Measurement");
  await expect(item).toContainText("Inverter 7 telemetry export");
  await expect(item).toContainText("Recorded ");

  // Link it as supporting the hypothesis.
  await page.locator("#rel-evidence").selectOption({ label: "Fan tachometer read 0 rpm from 11:52." });
  await page.locator("#rel-hypothesis").selectOption({ label: "The cooling fan failed." });
  await page.getByLabel("Relationship").selectOption("supports");
  await page.getByRole("button", { name: "Link", exact: true }).click();
  await version(page, 5);

  // Hypothesis change: the engine lists the allowed changes; recording one moves the state.
  await section(page, "Hypotheses");
  const hyp = page.locator(".causa-list .causa-item--hypothesis").filter({ hasText: "The cooling fan failed." });
  await expect(hyp).toContainText("1 supporting");
  await expect(hyp).toContainText("Allowed changes:");
  await page.locator("#hyp-which").selectOption({ label: "The cooling fan failed." });
  await page.getByLabel("Change", { exact: true }).selectOption("markPlausible");
  await page.getByLabel("Reason").fill("Tachometer evidence supports it.");
  await page.getByRole("button", { name: "Record the change" }).click();
  await version(page, 6);
  await expect(hyp).toContainText("Plausible");

  // A causal relationship, then a corrective action addressing it.
  await section(page, "Causal analysis");
  await page.getByLabel("Outcome it explains").fill("Inverter 7 tripped on over-temperature.");
  await page.locator("#cause-hypothesis").selectOption({ label: "The cooling fan failed." });
  await page.getByLabel("Role").selectOption("rootCause");
  await page.getByRole("button", { name: "Record", exact: true }).click();
  await version(page, 7);
  await expect(page.getByRole("list", { name: "Causal relationships as text" })).toContainText("Root cause: The cooling fan failed.");

  await section(page, "Corrective actions");
  await page.getByLabel("What will be done").fill("Replace the fan and add a fan-failure alarm.");
  await page.getByLabel("Kind").selectOption("correction");
  await page.getByLabel("Cause it addresses").selectOption({ label: "Inverter 7 tripped on over-temperature." });
  await page.getByRole("button", { name: "Record", exact: true }).click();
  await version(page, 8);
  await expect(page.locator("[data-percepta-screen=investigation-corrective-actions] .causa-item")).toContainText("Not verified effective (0 of 0 criteria satisfied)");
});
