// S21 requirements proof and S20 factory view: every value shown comes from the evidence published with the build
// (dist/evidence/*.json, build-info.json); what is not proven is shown as not proven, never as passed.
import { readFileSync } from "node:fs";
import { expect, test } from "./support/fixtures.js";

const started = (page) => expect(page.locator("#causa-boot")).toHaveCount(0, { timeout: 60_000 });
const proof = () => JSON.parse(readFileSync("dist/evidence/proof.json", "utf8"));
const factory = () => JSON.parse(readFileSync("dist/evidence/factory.json", "utf8"));
const status = (r) => (r.obligations.length > 0 && r.obligations.every((o) => o.verification === "PASS") ? "Proven" : r.obligations.some((o) => o.verification === "FAIL") ? "Failing" : "Not proven");

test("the requirements proof states published counts exactly, and a requirement is proven only when every obligation passed [VPO-fd43b3e4e7e02f59]", async ({ page }) => {
  const p = proof();
  const obligations = p.requirements.flatMap((r) => r.obligations);
  const pass = obligations.filter((o) => o.verification === "PASS").length;
  const fail = obligations.filter((o) => o.verification === "FAIL").length;
  await page.goto("index.html#/administration/proof");
  await started(page);
  await expect(page.locator("[data-percepta-screen]")).toHaveAttribute("data-percepta-screen", "requirements-proof");
  await expect(page.getByText(`Of ${obligations.length} obligations`)).toContainText(`${pass} proven by passing tests, ${fail} failing, ${obligations.length - pass - fail} not proven`);
  // A requirement with an obligation not proven says so, with its source and the build commit.
  const open = p.requirements.find((r) => status(r) === "Not proven");
  await page.getByLabel("Requirement", { exact: true }).selectOption(open.id);
  await expect(page).toHaveURL(new RegExp(`\\?item=${open.id.replace(".", "\\.")}$`));
  await expect(page.locator("[data-percepta-region=requirement]")).toContainText("Not proven:");
  await expect(page.locator("[data-percepta-region=scenario] .causa-item")).toHaveCount(open.obligations.length);
  await expect(page.locator("[data-percepta-region=commit]")).toContainText(p.commit);
  if (open.document) await expect(page.locator("[data-percepta-region=source]")).toContainText(open.document);
  // A requirement whose obligations all passed is the only kind called proven (when the evidence has one).
  const proven = p.requirements.find((r) => status(r) === "Proven");
  if (proven) {
    await page.getByLabel("Requirement", { exact: true }).selectOption(proven.id);
    await expect(page.locator("[data-percepta-region=requirement]")).toContainText(`Proven: all ${proven.obligations.length} obligation(s) have passing tests.`);
  }
  // Never "all passed" by default.
  await expect(page.locator("body")).not.toContainText("All requirements passed");
});

test("the factory view shows only published values: ROS work, requirement and test counts, Praxis, Aegis and the deployed commit [VPO-fd43b3e4e7e02f59]", async ({ page, baseURL }) => {
  const f = factory();
  const info = await (await page.request.get(new URL("build-info.json", baseURL).href)).json();
  await page.goto("index.html#/administration/factory");
  await started(page);
  await expect(page.locator("[data-percepta-screen]")).toHaveAttribute("data-percepta-screen", "factory-view");
  const ros = page.locator("[data-percepta-region=ros]");
  await expect(ros).toContainText(`${f.ros.items.length} ROS work item(s)`);
  await expect(ros).toContainText(`ROS events recorded: ${f.ros.events}`);
  await expect(ros.locator(".causa-item")).toHaveCount(f.ros.items.length);
  await expect(page.locator("[data-percepta-region=praxis]")).toContainText(f.praxis.version);
  await expect(page.locator("[data-percepta-region=aegis]")).toContainText(`${f.aegis.boundaries} Aegis boundaries declared`);
  await expect(page.locator("[data-percepta-region=aegis]")).toContainText("Runtime faults: none are published with this build");
  await expect(page.locator("[data-percepta-region=deployment]")).toContainText(info.commit);
  const reqs = proof().requirements;
  await expect(page.locator("[data-percepta-region=requirements]")).toContainText(`${reqs.length} P0 requirements: ${reqs.filter((r) => status(r) === "Proven").length} proven`);
});
