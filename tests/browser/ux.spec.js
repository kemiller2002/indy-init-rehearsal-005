// Stream ux: the investigation catalog, the detail inspector, relationship traversal, next steps and the separation of
// investigation work from Administration and Settings. Investigations are synthetic and built through the real F#
// kernel in the test build's in-memory workspace (window.__perceptaSetState({ name: "engine-memory" })); nothing here
// reads a historical record. Each title carries the obligation ids (VPO-…) the test proves.
import { expect, test } from "./support/fixtures.js";

const SITE = `http://127.0.0.1:${process.env.CAUSA_TEST_PORT ?? 4322}/`;
const started = (page) => expect(page.locator("#causa-boot")).toHaveCount(0, { timeout: 60_000 });
const section = (page, name) => page.getByRole("navigation", { name: "Investigation sections" }).getByRole("link", { name, exact: true }).click();
const version = (page, n) => expect(page.locator(".causa-inv-meta")).toContainText(`Version ${n}`);
const main = (page) => page.locator("#causa-main");

/** Routine investigation copy must not carry these (UI-017-O2, UI-024-O5); Causa.App.Language.technicalTerms. */
const TECHNICAL = [/\bSHA\b/i, /sha256/i, /\bcommit\b/i, /\bschema\b/i, /\breplay(ed)?\b/i, /\bcatalog\b/i, /historical-v1/i, /\bmanifest\b/i, /ndjson/i, /WebAssembly/i, /\bWASM\b/, /\.NET\b/, /\bLimen\b/, /\bForma\b/, /GitHub API/i, /\bHTTP\b/, /CAUSA\./, /RESOLVE\./, /Integrity check/i];

const HOSTILE = "<img src=x onerror=\"window.__pwned=1\">Fan tachometer \u202Eread 0 rpm from 11:52.";

async function engineMemory(page, hash = "#/investigations/new") {
  await page.goto(SITE + "index.html" + hash);
  await started(page);
  await page.evaluate(() => window.__perceptaSetState({ name: "engine-memory", predicates: [], capabilities: {} }));
}

async function startCase(page, title, statement) {
  await page.goto(SITE + "index.html#/investigations/new");
  await page.getByLabel("Title").fill(title);
  await page.getByLabel(/Problem statement/).fill(statement);
  await page.getByRole("button", { name: "Start the investigation" }).click();
  await expect(page.getByRole("heading", { level: 1 })).toHaveText(title);
}

/** A synthetic case with evidence (untrusted text), a linked and an unlinked hypothesis, an open question, a cause and a correction. */
async function buildCase(page) {
  await engineMemory(page);
  await startCase(page, "Overheating inverter", "Inverter 7 tripped on over-temperature at noon.");
  await page.getByRole("button", { name: "Activate the investigation" }).click();
  await page.getByLabel("Hypothesis statement").fill("The cooling fan failed.");
  await page.getByRole("button", { name: "Record a hypothesis" }).click();
  await version(page, 3);
  await page.getByLabel("Hypothesis statement").fill("The temperature sensor reads high.");
  await page.getByRole("button", { name: "Record a hypothesis" }).click();
  await version(page, 4);
  await page.getByLabel("Question", { exact: true }).fill("Was the fan replaced at the last service?");
  await page.getByRole("button", { name: "Record an open question" }).click();
  await version(page, 5);
  await section(page, "Evidence");
  await page.getByLabel("What the evidence shows, quoted as recorded").fill(HOSTILE);
  await page.getByLabel("Kind of evidence").selectOption("measurement");
  await page.getByLabel("Source").fill("Inverter 7 telemetry export");
  await page.getByRole("button", { name: "Admit evidence" }).click();
  await version(page, 6);
  await page.locator("#rel-evidence").selectOption({ index: 1 });
  await page.locator("#rel-hypothesis").selectOption({ label: "The cooling fan failed." });
  await page.getByLabel("Relationship").selectOption("supports");
  await page.getByRole("button", { name: "Link", exact: true }).click();
  await version(page, 7);
  await section(page, "Causal analysis");
  await page.getByLabel("Outcome it explains").fill("Inverter 7 tripped on over-temperature.");
  await page.locator("#cause-hypothesis").selectOption({ label: "The cooling fan failed." });
  await page.getByLabel("Role").selectOption("rootCause");
  await page.getByRole("button", { name: "Record", exact: true }).click();
  await version(page, 8);
  await section(page, "Corrective actions");
  await page.getByLabel("What will be done").fill("Replace the fan and add a fan-failure alarm.");
  await page.getByLabel("Kind").selectOption("correction");
  await page.getByLabel("Cause it addresses").selectOption({ label: "Inverter 7 tripped on over-temperature." });
  await page.getByRole("button", { name: "Record", exact: true }).click();
  await version(page, 9);
  return page.url().match(/investigations\/([0-9a-f-]{36})/)[1];
}

const inspector = (page) => page.locator("[data-percepta-region=inspector]");
const row = (page, group) => inspector(page).locator(".ef-relationship-index__list > div").filter({ has: page.locator("dt", { hasText: group }) });

test.describe("investigation catalog (UI-001, UI-022)", () => {
  test("cards show title, actual status, latest activity, domain counts, the policy's next action, an Open action and the reference id; no severity, owner or technical text [VPO-02b53bd16db4ee17] [VPO-05f9c2a398ed7454] [VPO-985886de7f012c74] [VPO-3fc89c9d18b678a4] [VPO-c6f789b832466ab9] [VPO-78852bf4d2da804b]", async ({ page }) => {
    await engineMemory(page);
    await startCase(page, "Conveyor stoppage", "Line 4 conveyor stopped twice on Tuesday.");
    const id = page.url().match(/investigations\/([0-9a-f-]{36})/)[1];
    await page.getByRole("button", { name: "Activate the investigation" }).click();
    await page.getByLabel("Question", { exact: true }).fill("Did the belt tension alarm fire?");
    await page.getByRole("button", { name: "Record an open question" }).click();
    await version(page, 3);
    await startCase(page, "Valve leak", "Coolant found under valve 3.");
    await page.getByRole("navigation", { name: "Primary" }).getByRole("link", { name: "Investigations" }).click();
    const cards = page.locator(".causa-card");
    await expect(cards).toHaveCount(2);
    const conveyor = cards.filter({ hasText: "Conveyor stoppage" });
    await expect(conveyor.getByRole("heading", { level: 2 })).toHaveText("Conveyor stoppage");
    await expect(conveyor.locator(".causa-card__lifecycle")).toContainText("Status: Active");
    await expect(conveyor.locator(".causa-card__activity")).toHaveText(/^Latest activity: Open question recorded, \d{4}-\d\d-\d\d \d\d:\d\d:\d\d UTC$/);
    await expect(conveyor.locator(".causa-card__counts")).toHaveText("1 open questions · 0 evidence items · 0 hypotheses · 0 unresolved contradictions");
    await expect(conveyor.getByRole("link", { name: "Open investigation: Conveyor stoppage" })).toHaveAttribute("href", `#/investigations/${id}`);
    await expect(conveyor.locator(".ef-identifier")).toHaveText(`Reference ${id}`);
    // The next action comes only from the F# policy: a draft may be activated; the active case offers its own.
    const valve = cards.filter({ hasText: "Valve leak" });
    await expect(valve.locator(".causa-card__lifecycle")).toContainText("Status: Draft");
    await expect(valve.locator(".causa-card__next")).toHaveText("Next step allowed now: Activate the investigation");
    await expect(conveyor.locator(".causa-card__next")).not.toHaveText(/Resolve the investigation|Close the investigation/);
    // Severity and owner are not in the domain contract: omitted, never invented.
    for (const card of [conveyor, valve]) {
      await expect(card).not.toContainText(/severity|owner|assigned|Expected lifecycle|Verified/i);
      const text = await card.innerText();
      for (const t of TECHNICAL) expect(text, `card leaks ${t}`).not.toMatch(t);
    }
    // The id is reference text, never the heading.
    await expect(conveyor.getByRole("heading", { level: 2 })).not.toContainText(id);
  });

  test("the catalog sorts, shows an empty state, keeps the selection, and opening keeps identity and context [VPO-1cd1f428fe646f06] [VPO-b3ea3d93850c4c8f]", async ({ page }) => {
    await engineMemory(page, "#/investigations");
    await expect(page.getByText("No investigations yet.")).toBeVisible();
    await startCase(page, "Alpha boiler alarm", "Boiler alarm sounded.");
    await page.getByRole("button", { name: "Activate the investigation" }).click();
    await version(page, 2);
    // Latest activity is recorded to the second; make the two cases' activity distinct.
    await page.waitForTimeout(1100);
    await startCase(page, "Bravo pump trip", "Pump B tripped.");
    await page.getByRole("navigation", { name: "Primary" }).getByRole("link", { name: "Investigations" }).click();
    const titles = page.locator(".causa-card h2");
    // Default: latest activity first.
    await expect(titles).toHaveText(["Bravo pump trip", "Alpha boiler alarm"]);
    await page.getByLabel("Sort investigations by").selectOption("title");
    await expect(titles).toHaveText(["Alpha boiler alarm", "Bravo pump trip"]);
    await page.getByLabel("Sort investigations by").selectOption("status");
    await expect(titles).toHaveText(["Bravo pump trip", "Alpha boiler alarm"]);
    await expect(page.locator(".causa-card").first()).toContainText("Status: Draft");
    await page.getByLabel("Sort investigations by").selectOption("activity");
    await expect(titles).toHaveText(["Bravo pump trip", "Alpha boiler alarm"]);
    // Opening keeps the investigation's identity in the address and the heading.
    await page.getByRole("link", { name: "Open investigation: Bravo pump trip" }).click();
    const id = page.url().match(/investigations\/([0-9a-f-]{36})/)[1];
    await expect(page.getByRole("heading", { level: 1 })).toHaveText("Bravo pump trip");
    await section(page, "Evidence");
    await expect(page).toHaveURL(new RegExp(`#/investigations/${id}/evidence$`));
    await page.reload();
    await started(page);
    // Back to the catalog: the card just left is selected and focused.
    await page.goto(SITE + "index.html#/investigations/new");
    await started(page);
    await page.evaluate(() => window.__perceptaSetState({ name: "engine-memory", predicates: [], capabilities: {} }));
    await startCase(page, "Charlie fan noise", "Fan noise.");
    await page.locator("a.causa-back").click();
    const selected = page.locator(".causa-card[aria-current=true]");
    await expect(selected).toHaveCount(1);
    await expect(selected).toContainText("Charlie fan noise");
    await expect(selected).toBeFocused();
  });
});

test.describe("detail inspector and relationships (UI-005, UI-033, UI-034, UI-038, UI-039)", () => {
  test("the evidence inspector shows provenance, timestamps, cleaned original text, missing provenance labelled, and links to its hypothesis [VPO-ff2e466b7aaa3e12] [VPO-34334672f9ff0014] [VPO-5fe930a650a08b04] [VPO-c18f969a9c8b65bf]", async ({ page }) => {
    await buildCase(page);
    await section(page, "Evidence");
    const ev = page.locator("[data-percepta-screen=investigation-evidence] .causa-item--evidence").first();
    // Untrusted text is shown as text: no element is created from it and direction overrides are removed.
    await expect(ev).toContainText('<img src=x onerror="window.__pwned=1">Fan tachometer read 0 rpm from 11:52.');
    await ev.getByRole("link", { name: /^Details:/ }).click();
    const box = inspector(page);
    await expect(box).toBeVisible();
    await expect(box.locator(".ef-record-header__type")).toHaveText("Evidence");
    await expect(box.locator("dt", { hasText: "Original content" }).locator("+ dd")).toHaveText('<img src=x onerror="window.__pwned=1">Fan tachometer read 0 rpm from 11:52.');
    await expect(box.locator("dt", { hasText: "Source" }).locator("+ dd")).toHaveText("Inverter 7 telemetry export");
    await expect(box.locator("dt", { hasText: "Category" }).locator("+ dd")).toHaveText("Measurement");
    await expect(box.locator("dt", { hasText: "Recorded by" }).locator("+ dd")).toHaveText("causa-user");
    await expect(box.locator("dt", { hasText: "Recorded at" }).locator("+ dd")).toHaveText(/^\d{4}-\d\d-\d\d \d\d:\d\d:\d\d UTC$/);
    const captured = box.locator("div[data-missing=true]").filter({ hasText: "Captured at" });
    await expect(captured).toContainText("Capture time not recorded");
    await expect(box.locator("dt", { hasText: "Assessment" }).locator("+ dd")).toHaveText("Not yet assessed");
    await expect(box.locator("img")).toHaveCount(0);
    expect(await page.evaluate(() => window.__pwned)).toBeUndefined();
    expect(await page.locator("body").innerHTML()).not.toContain("\u202E");
    // Explicit links to supporting and contradicting hypotheses; absence is stated.
    await expect(row(page, "Hypotheses it supports").getByRole("link")).toHaveText("The cooling fan failed.");
    await expect(row(page, "Hypotheses it contradicts")).toContainText("It is not linked as contradicting any hypothesis.");
  });

  test("traversal follows recorded relationships with stable addresses and explicit absent links, and closing restores focus @responsive [VPO-5a85f3ea85db30b2] [VPO-63d89380e8017050] [VPO-f3ebcdbe15cdf92d] [VPO-8f8156f91b64963b] [VPO-ad49e5ff43f1ace6]", async ({ page }) => {
    const id = await buildCase(page);
    await section(page, "Hypotheses");
    const fan = page.locator("[data-percepta-screen=investigation-hypotheses] .causa-item--hypothesis").filter({ hasText: "The cooling fan failed." });
    // Keyboard: the Details link is reachable and opens the inspector, which takes focus.
    await fan.getByRole("link", { name: "Details: The cooling fan failed." }).focus();
    await page.keyboard.press("Enter");
    await expect(page).toHaveURL(new RegExp(`#/investigations/${id}/hypotheses\\?item=[0-9a-f-]{36}$`));
    const hypId = page.url().split("item=")[1];
    await expect(inspector(page)).toBeFocused();
    await expect(inspector(page).locator("h2")).toHaveText("The cooling fan failed.");
    await expect(row(page, "Contradicting evidence")).toContainText("No contradicting evidence is linked.");
    // Hypothesis -> its cause -> the cause's corrective action -> back to the cause, each by its own stable address.
    await row(page, "Causal relationships").getByRole("link").click();
    await expect(page).toHaveURL(new RegExp(`/causal-analysis\\?item=[0-9a-f-]{36}$`));
    await expect(inspector(page).locator(".ef-record-header__type")).toHaveText("Causal relationship");
    await expect(row(page, "Hypotheses in it").getByRole("link")).toHaveAttribute("href", `#/investigations/${id}/hypotheses?item=${hypId}`);
    await row(page, "Corrective actions").getByRole("link").click();
    await expect(inspector(page).locator(".ef-record-header__type")).toHaveText("Corrective action");
    await expect(row(page, "Verification criteria")).toContainText("No verification criterion is recorded for it.");
    // Timeline events link to the records they touched, through the same inspector.
    await row(page, "Timeline events").getByRole("link").first().click();
    await expect(page).toHaveURL(/\/timeline\?item=event-\d+$/);
    await expect(inspector(page).locator(".ef-record-header__type")).toHaveText("Timeline event");
    await expect(row(page, "Records it touched").getByRole("link").first()).toBeVisible();
  });

  test("closing the inspector returns focus to the record's row, and the inspector reflows on every viewport @responsive [VPO-8f8156f91b64963b] [VPO-f3ebcdbe15cdf92d]", async ({ page }) => {
    await buildCase(page);
    await section(page, "Hypotheses");
    const other = page.locator("[data-percepta-screen=investigation-hypotheses] .causa-item--hypothesis").filter({ hasText: "The temperature sensor reads high." });
    await other.getByRole("link", { name: "Details: The temperature sensor reads high." }).click();
    await expect(row(page, "Supporting evidence")).toContainText("No supporting evidence is linked.");
    await expect(row(page, "Causal relationships")).toContainText("It is not part of any causal relationship.");
    const width = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
    expect(width, "no horizontal scrolling with the inspector open").toBeLessThanOrEqual(0);
    const close = inspector(page).getByRole("link", { name: "Close details" });
    const box = await close.boundingBox();
    expect(box.height).toBeGreaterThanOrEqual(44);
    await close.click();
    await expect(inspector(page)).toHaveCount(0);
    await expect(other).toHaveAttribute("data-selected", "true");
    await expect(other).toBeFocused();
  });

  test("a cause's explanation lists its hypotheses, supporting and conflicting evidence, history and why it is not confirmed; nothing is concluded [VPO-f8666daf6fdf79d7] [VPO-ffda9fea0324a42e] [VPO-885e4767b84371de]", async ({ page }) => {
    await buildCase(page);
    await section(page, "Causal analysis");
    await page.locator(".causa-item--cause").getByRole("link", { name: /^Details:/ }).click();
    const why = inspector(page).locator(".causa-inspector__why");
    await expect(why).toContainText("Status: Proposed, not confirmed.");
    await expect(why).toContainText('Root cause: "The cooling fan failed.", currently Proposed.');
    await expect(why).toContainText("Supporting evidence: \"<img src=x onerror=\"window.__pwned=1\">Fan tachometer read 0 rpm from 11:52.\" (source: Inverter 7 telemetry export).");
    await expect(why).toContainText("Conflicting evidence: none is linked.");
    await expect(why).toContainText("Why it is not confirmed:");
    await expect(why).toContainText("Mechanism: not stated.");
    await expect(why).not.toContainText("confirmed by the investigator");
    await expect(inspector(page).locator(".causa-inspector__state")).toHaveText("Proposed, not confirmed");
  });

  test("a selection that names no record of the investigation shows no inspector and says so [VPO-63d89380e8017050]", async ({ page }) => {
    const id = await buildCase(page);
    await page.evaluate((h) => { location.hash = h; }, `#/investigations/${id}/evidence?item=00000000-0000-4000-8000-0000000000aa`);
    await expect(page.getByText("The selected record is not part of this investigation.")).toBeVisible();
    await expect(inspector(page)).toHaveCount(0);
  });
});

test.describe("next steps (UI-032, UI-040)", () => {
  test("next steps come from the domain, link to the record and carry the policy's verdict; none is invented [VPO-29cc81617de241fb] [VPO-141be05c7de03e17] [VPO-8a5de7e5afae98cb]", async ({ page }) => {
    const id = await buildCase(page);
    await section(page, "Overview");
    const steps = page.locator("[data-percepta-region=next-steps] .causa-step");
    await expect(steps.filter({ hasText: "Open question: Was the fan replaced at the last service?" })).toHaveCount(1);
    await expect(steps.filter({ hasText: 'Missing evidence: no evidence is linked to the hypothesis "The temperature sensor reads high."' })).toHaveCount(1);
    // The hypothesis with linked evidence is not listed as missing evidence (negative control).
    await expect(steps.filter({ hasText: '"The cooling fan failed."' })).toHaveCount(0);
    await expect(steps.filter({ hasText: "Before resolving: No root cause has been confirmed." })).toHaveCount(1);
    const question = steps.filter({ hasText: "Open question:" });
    await expect(question.locator(".causa-step__action")).toHaveText(/^(Allowed now: Answer the question\.|Answer the question is not available now: .+)$/);
    await question.getByRole("link").click();
    await expect(page).toHaveURL(new RegExp(`#/investigations/${id}\\?item=[0-9a-f-]{36}$`));
    await expect(inspector(page).locator(".ef-record-header__type")).toHaveText("Open question");
    for (const t of TECHNICAL) expect(await page.locator("[data-percepta-region=next-steps]").innerText()).not.toMatch(t);
  });

  test("a fresh draft lists only what blocks resolution, never an empty list that reads as no work [VPO-141be05c7de03e17]", async ({ page }) => {
    await engineMemory(page);
    await startCase(page, "Empty case", "Something happened.");
    const steps = page.locator("[data-percepta-region=next-steps] .causa-step");
    await expect(steps.first()).toBeVisible();
    await expect(page.getByText("The investigation records nothing open.")).toHaveCount(0);
    for (const text of await steps.allInnerTexts()) expect(text).toMatch(/^Before resolving: /);
  });
});

test.describe("investigation work apart from Administration and Settings (UI-017, UI-018, UI-020, UI-021, UI-023, UI-029, UI-030, UI-031)", () => {
  test("routine investigation views carry no implementation prose, codes or versions; Administration keeps them [VPO-7b0ba1d1f6f663a5] [VPO-78541ad7e7bfc045] [VPO-0b37bb6a955165e2] [VPO-18e6fc4dd0730354] [VPO-2ad36bd062191d2b] [VPO-026da8a495857549] [VPO-5c147c83f19f2c8c] [VPO-bff050b54a481780] [VPO-e1fb14be4ca10f5f]", async ({ page }) => {
    const info = await (await page.request.get(SITE + "build-info.json")).json();
    await buildCase(page);
    for (const name of ["Overview", "Causal analysis", "Evidence", "Hypotheses", "Corrective actions"]) {
      await section(page, name);
      const text = await main(page).innerText();
      expect(text).not.toContain("The active catalog historical-v1");
      expect(text).not.toContain(info.commit);
      for (const t of TECHNICAL) expect(text, `${name} shows ${t}`).not.toMatch(t);
      // User tasks and actionable status are there instead.
      await expect(page.locator(".causa-inv-meta")).toContainText("Status: Active");
    }
    await expect(page.locator("header.causa-masthead")).not.toContainText(info.commit);
    await expect(page.locator("header.causa-masthead")).not.toContainText(/sha|commit|\.NET|Limen/i);
    // Operational facts were moved, not deleted: Administration shows the build, runtime and provider.
    await page.getByRole("navigation", { name: "Primary" }).getByRole("link", { name: "Administration" }).click();
    const admin = page.locator("[data-percepta-screen=administration]");
    await expect(admin.getByRole("row", { name: /^commit/ })).toContainText(info.commit);
    await expect(admin.getByRole("row", { name: /\.NET runtime \(loaded\)/ })).toBeVisible();
    await expect(admin.getByRole("row", { name: /Data provider/ })).toBeVisible();
    // And Settings holds only the preferences, which apply for this session.
    await page.getByRole("navigation", { name: "Primary" }).getByRole("link", { name: "Settings" }).click();
    const settings = page.locator("[data-percepta-screen=settings]");
    await expect(settings).not.toContainText(info.commit);
    await expect(settings).not.toContainText(/\.NET runtime|build-info|components\./);
  });

  test("evidence views keep source attribution and the record's provenance after technical details moved out [VPO-5c147c83f19f2c8c] [VPO-5fe930a650a08b04] [VPO-c18f969a9c8b65bf] [VPO-7fff10ce82e7c162]", async ({ page }) => {
    await buildCase(page);
    await section(page, "Evidence");
    const ev = page.locator("[data-percepta-screen=investigation-evidence] .causa-item--evidence").first();
    await expect(ev).toContainText("Inverter 7 telemetry export");
    await expect(ev).toContainText("Measurement");
    await expect(ev).toContainText("Recorded ");
    await section(page, "Causal analysis");
    await expect(page.getByRole("list", { name: "Causal relationships as text" })).toContainText("Root cause: The cooling fan failed.");
  });

  test("an investigation that loaded correctly but has unresolved findings is never labelled Verified; findings come from the domain [VPO-d0e33c8aaaf705f2] [VPO-c8114e2b7bd759fa] [VPO-f0256bc31283be14] [VPO-58231f4725864068] [VPO-885e4767b84371de] [VPO-6f0a2e18f9a19003]", async ({ page }) => {
    await buildCase(page);
    for (const name of ["Overview", "Causal analysis", "Evidence", "Hypotheses", "Timeline", "Corrective actions", "Report"]) {
      await section(page, name);
      await expect(page.locator(".causa-inv-header")).not.toContainText(/\bVerified\b(?! effective)/);
      await expect(page.locator(".causa-tag, .ef-status-lozenge").filter({ hasText: /^Verified$/ })).toHaveCount(0);
    }
    await section(page, "Overview");
    // Unknown is not zero, unsaved is not saved: this tab's workspace says what it is.
    await expect(page.locator("[data-percepta-region=persistence-state]")).not.toContainText(/Saved to GitHub|Saved and confirmed/);
    await section(page, "Corrective actions");
    await expect(page.locator("[data-percepta-screen=investigation-corrective-actions] .causa-item")).toContainText("Not verified effective (0 of 0 criteria satisfied)");
  });

  test("workflow actions stay in the investigation, operational facts in Administration, preferences in Settings; two-level navigation [VPO-9eb8f87800355c73] [VPO-e5017d9f8ae702fb] [VPO-dba405d7ed58872b] [VPO-7fe69b1e93fa0c7f] [VPO-2ad36bd062191d2b] [VPO-fcf404b4a7afa635]", async ({ page }) => {
    const id = await buildCase(page);
    await section(page, "Overview");
    // Two navigation levels, distinct: three global destinations, and the investigation's own sections.
    const primary = page.getByRole("navigation", { name: "Primary" }).getByRole("link");
    await expect(primary).toHaveText(["Investigations", "Administration", "Settings"]);
    const sections = page.getByRole("navigation", { name: "Investigation sections" }).getByRole("link");
    for (const name of ["Overview", "Causal analysis", "Evidence", "Hypotheses", "Timeline", "Corrective actions", "Report"]) {
      await expect(sections.filter({ hasText: name }).first()).toHaveAttribute("href", new RegExp(`^#/investigations/${id}`));
    }
    await expect(page.getByRole("navigation", { name: "Investigation sections" })).not.toContainText(/Administration|Settings/);
    // Workflow actions are on the investigation, not in Administration or Settings.
    await expect(main(page).getByRole("button", { name: "Record a hypothesis" })).toBeVisible();
    await page.getByRole("navigation", { name: "Primary" }).getByRole("link", { name: "Administration" }).click();
    await expect(main(page).getByRole("button", { name: /Record|Activate|Resolve|Confirm/ })).toHaveCount(0);
    await expect(main(page)).not.toContainText("The cooling fan failed.");
    await page.getByRole("navigation", { name: "Primary" }).getByRole("link", { name: "Settings" }).click();
    await expect(main(page).getByRole("button", { name: /Record|Activate|Resolve|Confirm/ })).toHaveCount(0);
    // Settings has only adjustable options, and they change the page.
    await expect(page.locator("[data-percepta-screen=settings] select, [data-percepta-screen=settings] input[type=checkbox], [data-percepta-screen=settings] input[type=radio]").first()).toBeVisible();
    // No browser storage holds anything from this session.
    expect(await page.evaluate(() => localStorage.length + sessionStorage.length)).toBe(0);
  });
});
