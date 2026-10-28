// UI-016 / PF-016 / UI-011-O3 / UI-014-O3: the five Causa colours appear in populated states with their text
// labels and measured contrast, use the same token semantics across views, survive grayscale print, and no
// essential operation needs drag-and-drop or hover. Runs the real F# kernel in the test build's memory workspace.
import { readFileSync } from "node:fs";
import { expect, test } from "./support/fixtures.js";

const SITE = `http://127.0.0.1:${process.env.CAUSA_TEST_PORT ?? 4822}/`;
const started = (page) => expect(page.locator("#causa-boot")).toHaveCount(0, { timeout: 60_000 });
const section = (page, name) => page.getByRole("navigation", { name: "Investigation sections" }).getByRole("link", { name }).click();

const rgb = (s) => (s.match(/\d+(\.\d+)?/g) ?? []).slice(0, 3).map(Number);
const luminance = ([r, g, b]) => {
  const c = [r, g, b].map((v) => v / 255).map((x) => (x <= 0.03928 ? x / 12.92 : ((x + 0.055) / 1.055) ** 2.4));
  return 0.2126 * c[0] + 0.7152 * c[1] + 0.0722 * c[2];
};
const contrast = (a, b) => { const [x, y] = [luminance(a), luminance(b)].sort((p, q) => q - p); return (x + 0.05) / (y + 0.05); };
/** Text colour and the first opaque background behind an element. */
const colours = (locator) => locator.evaluate((el) => {
  let bg = "rgba(0, 0, 0, 0)";
  for (let n = el; n && bg.includes("rgba(0, 0, 0, 0)"); n = n.parentElement) bg = getComputedStyle(n).backgroundColor;
  const s = getComputedStyle(el);
  return { fg: s.color, bg, border: s.borderLeftColor, text: el.textContent.trim() };
});

async function engine(page) {
  await page.goto(SITE + "index.html#/investigations/new");
  await started(page);
  await page.evaluate(() => window.__perceptaSetState({ name: "engine-memory", predicates: [], capabilities: {} }));
  await page.getByLabel("Title").fill("Valve chatter");
  await page.getByLabel(/Problem statement/).fill("Valve 12 chattered during start-up.");
  await page.getByRole("button", { name: "Start the investigation" }).click();
  await expect(page.locator(".causa-inv-meta")).toContainText("Version 1");
}

test("fixture scenarios show navy, teal, amber, coral and ivory in the states they mean, each with a text label and measured contrast [VPO-8cd7943440a55f6f] [VPO-b3cab23e517cd0d3] [VPO-7fd9b69b4aa5b401] [VPO-f4f9d09fc20df79d] [VPO-5a993f5a761fd6ee]", async ({ page }) => {
  // Amber: an open material question (Percepta fixture state, rendered by the normal view).
  await page.goto(SITE + "index.html?percepta=default#/investigations/00000000-0000-4000-8000-0000000000f1");
  await started(page);
  await page.evaluate(() => window.__perceptaSetState({ name: "blocked", predicates: ["material-blocker-exists"], capabilities: { RecordObservation: "legal", ConfirmCause: "illegal" } }));
  const attention = await colours(page.locator(".causa-attention"));
  expect(rgb(attention.border)).toEqual([233, 162, 59]);
  expect(attention.text).toContain("1 material open question.");
  expect(contrast(rgb(attention.fg), rgb(attention.bg))).toBeGreaterThanOrEqual(4.5);
  // Navy masthead and ivory surface, with text on them.
  const masthead = await colours(page.locator(".causa-masthead .ef-site-header__name"));
  expect(rgb(masthead.bg)).toEqual([20, 40, 63]);
  expect(masthead.text).toBe("Causa");
  expect(contrast(rgb(masthead.fg), rgb(masthead.bg))).toBeGreaterThanOrEqual(4.5);
  expect(rgb(await page.evaluate(() => getComputedStyle(document.body).backgroundColor))).toEqual([245, 243, 237]);
  // Teal: the primary investigation action, with its label (Forma ef-button).
  const action = page.locator(".ef-button[data-ef-variant=primary]").first();
  await expect(action).toHaveClass(/ef-button/);
  const teal = await colours(action);
  expect(rgb(teal.bg)).toEqual([6, 100, 110]);
  expect(teal.text.length).toBeGreaterThan(0);
  expect(contrast(rgb(teal.fg), rgb(teal.bg))).toBeGreaterThanOrEqual(4.5);
  // Focus treatment uses the teal token.
  await action.focus();
  expect(rgb(await action.evaluate((el) => getComputedStyle(el).outlineColor))).toEqual([8, 127, 140]);

  // Coral: a falsified hypothesis and the confirmation of an irreversible action, both in words.
  await engine(page);
  await page.getByRole("button", { name: "Activate the investigation" }).click();
  await page.getByLabel("Hypothesis statement").fill("Supply pressure oscillated.");
  await page.getByRole("button", { name: "Record a hypothesis" }).click();
  await section(page, "Evidence");
  await page.getByLabel("What the evidence shows, quoted as recorded").fill("Pressure trace flat at 6.1 bar.");
  await page.getByLabel("Source").fill("Plant historian");
  await page.getByRole("button", { name: "Admit evidence" }).click();
  await section(page, "Hypotheses");
  await page.locator("#hyp-which").selectOption({ label: "Supply pressure oscillated." });
  await page.locator("#hyp-evidence").selectOption({ label: "Pressure trace flat at 6.1 bar." });
  await page.getByLabel("Change", { exact: true }).selectOption("falsify");
  await page.getByLabel("Reason").fill("The trace is flat.");
  await page.getByRole("button", { name: "Record the change" }).click();
  const dialog = await colours(page.getByRole("alertdialog"));
  expect(rgb(dialog.border)).toEqual([214, 107, 88]);
  expect(dialog.text).toContain("Falsify this hypothesis?");
  await page.getByRole("button", { name: "Yes, continue" }).click();
  const tag = page.locator(".causa-list .causa-item--hypothesis .causa-tag");
  await expect(tag).toHaveText("Falsified");
  const coral = await colours(tag);
  expect(contrast(rgb(coral.fg), rgb(coral.bg))).toBeGreaterThanOrEqual(4.5);
  expect(await tag.getAttribute("data-tone")).toBe("conflict");
});

test("absence of data does not force a status colour, and the same state has the same colours in every view [VPO-b3cab23e517cd0d3] [VPO-de371f74930347b8]", async ({ page }) => {
  await engine(page);
  // No open questions: the summary is neutral (navy edge), not amber.
  expect(rgb((await colours(page.locator(".causa-attention"))).border)).toEqual([20, 40, 63]);
  // "Draft" in the investigation header and on the catalog card: same tone, same colours.
  const header = await colours(page.locator(".causa-inv-meta .causa-tag"));
  await page.getByRole("navigation", { name: "Primary" }).getByRole("link", { name: "Investigations" }).click();
  const card = await colours(page.locator(".causa-card .causa-tag"));
  expect(card.text).toBe(header.text);
  expect([card.fg, card.bg]).toEqual([header.fg, header.bg]);
});

test("the printed report stays legible in grayscale [VPO-406b16e7341d28c2]", async ({ page }) => {
  await engine(page);
  await section(page, "Report");
  await page.emulateMedia({ media: "print" });
  const samples = await page.locator("ef-print-document").evaluate((root) =>
    [...root.querySelectorAll("p, li, h3, span")].filter((e) => e.textContent.trim() !== "" && e.offsetParent !== null).slice(0, 40).map((e) => {
      let bg = "rgba(0, 0, 0, 0)";
      for (let n = e; n && bg.includes("rgba(0, 0, 0, 0)"); n = n.parentElement) bg = getComputedStyle(n).backgroundColor;
      return { fg: getComputedStyle(e).color, bg: bg.includes("rgba(0, 0, 0, 0)") ? "rgb(255, 255, 255)" : bg };
    }));
  expect(samples.length).toBeGreaterThan(5);
  const gray = (c) => { const l = luminance(c); const v = l <= 0.0031308 ? l * 12.92 : 1.055 * l ** (1 / 2.4) - 0.055; const g = Math.round(v * 255); return [g, g, g]; };
  for (const s of samples) expect(contrast(gray(rgb(s.fg)), gray(rgb(s.bg)))).toBeGreaterThanOrEqual(4.5);
});

test("no essential operation needs drag-and-drop or hover: hover only restyles, nothing is draggable, and a command completes by keyboard [VPO-40f80428200fc71c] [VPO-3b9b2a8a4bf56535]", async ({ page }) => {
  const css = readFileSync("src/kernel/causa.css", "utf8");
  for (const rule of css.match(/[^{}]*:hover[^{]*\{[^}]*\}/g) ?? []) {
    expect(rule, rule).not.toMatch(/\b(display|visibility|opacity|height|max-height|content)\s*:/);
  }
  await engine(page);
  expect(await page.locator("[draggable=true]").count()).toBe(0);
  // Keyboard only: focus the activation button and press Enter; then type and submit a hypothesis.
  await page.getByRole("button", { name: "Activate the investigation" }).focus();
  await page.keyboard.press("Enter");
  await expect(page.locator(".causa-inv-meta")).toContainText("Version 2");
  await page.getByLabel("Hypothesis statement").focus();
  await page.keyboard.type("A sticking pilot valve.");
  await page.keyboard.press("Tab");
  await page.keyboard.press("Enter");
  await expect(page.locator(".causa-inv-meta")).toContainText("Version 3");
});

test("navigation tells a read-only demonstration from a writable copy and keeps the current investigation in view [VPO-014f5e1139ed692b]", async ({ page }) => {
  await engine(page);
  await expect(page.locator(".causa-kind")).toHaveText("Your working copy");
  await expect(page).toHaveTitle("Valve chatter · Overview · Causa");
  await section(page, "Timeline");
  await expect(page.locator(".causa-inv-header h1")).toHaveText("Valve chatter");
  await expect(page).toHaveTitle("Valve chatter · Timeline · Causa");
  await page.getByRole("navigation", { name: "Primary" }).getByRole("link", { name: "Investigations" }).click();
  await expect(page.locator(".causa-card")).toContainText("Your working copy");
});

test("headings follow Forma's fixed application type steps at every width, not the marketing display scale [VPO-9501908622238a66]", { tag: ["@responsive"] }, async ({ page }) => {
  for (const width of [1440, 1180, 820, 320]) {
    await page.setViewportSize({ width, height: 900 });
    await page.goto("index.html");
    await expect(page.locator("#causa-boot")).toHaveCount(0, { timeout: 60_000 });
    const h1 = page.getByRole("heading", { level: 1, name: "Connect a data repository" });
    const size = await h1.evaluate((el) => ({ font: parseFloat(getComputedStyle(el).fontSize), root: parseFloat(getComputedStyle(document.documentElement).fontSize), height: el.getBoundingClientRect().height }));
    expect(size.font).toBe(2 * size.root); // --ef-primitive-font-size-2xl
    // One line on desktop and tablet; never more than two at 320px.
    expect(size.height).toBeLessThanOrEqual(size.font * 1.2 * (width <= 320 ? 2 : 1) + 1);
  }
});

test("global and investigation navigation are two separate lists; settings never appear inside an investigation [VPO-dba405d7ed58872b] [VPO-628a11b571e65796]", async ({ page }) => {
  await engine(page);
  const primary = page.getByRole("navigation", { name: "Primary" });
  const sections = page.getByRole("navigation", { name: "Investigation sections" });
  await expect(primary.getByRole("link")).toHaveText(["Investigations", "Administration", "Settings"]);
  await expect(sections.getByRole("link")).toHaveText(["Overview", "Causal analysis", "Evidence", "Hypotheses", "Timeline", "Corrective actions", "Report", "AI proposals", "Actions"]);
  // Neither list contains the other's destinations, and no investigation view carries settings controls.
  await expect(sections.getByRole("link", { name: /Settings|Administration/ })).toHaveCount(0);
  await expect(primary.getByRole("link", { name: /Overview|Timeline|Evidence/ })).toHaveCount(0);
  for (const name of ["Overview", "Timeline", "Report"]) {
    await sections.getByRole("link", { name }).click();
    await expect(page.getByLabel("Text size")).toHaveCount(0);
    await expect(page.getByLabel("AI model id")).toHaveCount(0);
  }
});
