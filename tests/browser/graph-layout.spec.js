// The causal graph in a real browser (WDL-001..WDL-012): the synthetic graph fixtures that Causa's own projection
// produced (tests/browser/graph-fixtures, checked byte for byte by GraphTests.fs) are loaded into the real
// @echelon-foundry/forma-workflow frame, exactly as the application loads them (isolated mode, the site's vendored
// engine and Forma styles), and what was laid out is measured. Synthetic data only; no historical or R001 record.
// Each test title carries the verification-plan obligation ids (VPO-…) it proves.
import { readFileSync, readdirSync, existsSync } from "node:fs";
import { join } from "node:path";
import { expect, test } from "./support/fixtures.js";
import { renderGraph, measure, overlappingNodes, labelsOverNodes, crossings, detachedConnectors } from "./support/graph-harness.js";

const root = process.cwd();
const fixtureDir = join(root, "tests/browser/graph-fixtures");
const fixtures = readdirSync(fixtureDir)
  .filter((f) => f.endsWith(".json"))
  .sort()
  .map((f) => JSON.parse(readFileSync(join(fixtureDir, f), "utf8")));
const drawn = fixtures.filter((f) => f.document !== null);
const byName = Object.fromEntries(fixtures.map((f) => [f.name, f]));

// Edge crossings are minimised with the engine's documented capability (flow direction), never promised as zero:
// these are the measured counts for each fixture (WDL-006-O2). A regression above them fails.
const crossingBudget = { cycle: 0, "dense-evidence": 9, disconnected: 0, "four-roles": 0, "long-labels": 0, "single-node": 0 };

// The UI-016 palette and the tone each domain state carries (Presentation.tone in src/app/Presentation.fs).
const palette = { navy: "#14283f", teal: "#087f8c", amber: "#e9a23b", coral: "#d66b58", ivory: "#f5f3ed" };
const slotOfStatus = {
  Supported: "teal", Confirmed: "teal", Proposed: "amber", Plausible: "amber", Open: "amber",
  Weakened: "coral", Falsified: "coral", Superseded: "navy", Reopened: "navy",
};

test.describe("causal graph layout, measured in the browser", () => {
  test("every synthetic fixture lays out without overlapping nodes, clipped text, labels over nodes, detached or unlisted connectors, or hidden evidence counts, and within its crossing budget @responsive [VPO-9fe0158bf99720e2] [VPO-37a1821f8918e8a9]", { tag: ["@responsive"] }, async ({ page }, testInfo) => {
    expect(drawn.map((f) => f.name)).toEqual(["cycle", "dense-evidence", "disconnected", "four-roles", "long-labels", "single-node"]);
    for (const fixture of drawn) {
      const doc = fixture.document;
      const { frame } = await renderGraph(page, doc);
      const m = await measure(frame);
      const where = `${fixture.name} at ${page.viewportSize().width}px`;
      expect(m.nodes.length, where).toBe(doc.nodes.length);
      expect(overlappingNodes(m), `${where}: overlapping nodes`).toEqual([]);
      expect(m.nodes.filter((n) => n.overflowing || n.clippedParts.length > 0).map((n) => n.id), `${where}: clipped node text`).toEqual([]);
      expect(labelsOverNodes(m), `${where}: edge labels drawn over nodes`).toEqual([]);
      expect(detachedConnectors(m, doc), `${where}: connectors not attached to both nodes`).toEqual([]);
      // Every connector is also reachable as text (accessible connectors).
      expect(m.relations.length, `${where}: relationship list`).toBe((doc.edges ?? []).length);
      for (const e of doc.edges ?? []) expect(m.relations.some((r) => r.includes(e.label)), `${where}: ${e.id} listed`).toBe(true);
      // Evidence counts are on the hypothesis nodes themselves.
      for (const n of doc.nodes.filter((n) => n.kind === "org.causa.graph:hypothesis")) {
        const shown = m.nodes.find((x) => x.id === n.id);
        expect(shown.text, `${where}: ${n.id} evidence counts`).toContain(n.description);
      }
      const crossed = crossings(m);
      testInfo.annotations.push({ type: "crossings", description: `${where}: ${crossed}` });
      expect(crossed, `${where}: edge crossings`).toBeLessThanOrEqual(crossingBudget[fixture.name]);
    }
  });

  test("the same document lays out identically on every load [VPO-37a1821f8918e8a9]", async ({ page }) => {
    const doc = byName["dense-evidence"].document;
    const first = await measure((await renderGraph(page, doc)).frame);
    const second = await measure((await renderGraph(page, doc)).frame);
    expect(second.nodes.map((n) => [n.id, n.canvas])).toEqual(first.nodes.map((n) => [n.id, n.canvas]));
    expect(second.connectors).toEqual(first.connectors);
  });
});

test.describe("causal graph accessibility", () => {
  test("desktop, iPad and phone: the page never scrolls sideways, every node and connector has a text equivalent, and meaning is never colour alone @responsive [VPO-39cfd9f8545f99b9] [VPO-170fa438de9df91f]", { tag: ["@responsive"] }, async ({ page }) => {
    for (const name of ["four-roles", "long-labels", "dense-evidence"]) {
      const doc = byName[name].document;
      const { frame } = await renderGraph(page, doc, { height: "600px" });
      const m = await measure(frame);
      expect(m.pageWidth, `${name}: no horizontal page scroll`).toBeLessThanOrEqual(m.viewportWidth);
      // Text equivalents: the object list names every node with its kind, the relationship list every edge.
      for (const n of doc.nodes) expect(m.objects.some((o) => o.startsWith(`${n.kindLabel}: `)), `${name}: ${n.id} in the object list`).toBe(true);
      for (const n of m.nodes) {
        // Each coloured node states its status in words; each coloured edge states its role in words.
        expect(n.text, `${name}: ${n.id} status text`).toMatch(/Status\s*\n?\s*\S/);
      }
      for (const e of doc.edges) expect(e.label.length, `${name}: ${e.id} label`).toBeGreaterThan(0);
      // Line style also carries the role (root cause solid and heavier; contributing dashed; enabling dotted).
      expect(m.legend.join(" | ")).toContain("Root cause");
      // The diagram viewport scrolls inside the frame rather than the page.
      const viewport = frame.locator(".ef-diagram__viewport");
      await expect(viewport).toHaveAttribute("tabindex", "0");
      await expect(viewport).toHaveAttribute("role", "group");
    }
  });

  test("keyboard: every node can be reached and selected without a pointer, focus is visible, and the selection reaches the host @responsive [VPO-39cfd9f8545f99b9]", { tag: ["@responsive"] }, async ({ page }) => {
    const doc = byName["four-roles"].document;
    const { frame } = await renderGraph(page, doc);
    const buttons = frame.locator(".ef-workflow-editor__list[aria-label='Steps'] button");
    await expect(buttons).toHaveCount(doc.nodes.length);
    // Tab from the start of the frame until the first node button has focus.
    await frame.locator("body").focus();
    let reached = false;
    for (let i = 0; i < 20 && !reached; i++) {
      await page.keyboard.press("Tab");
      reached = await buttons.first().evaluate((b) => b === document.activeElement);
    }
    expect(reached, "the first node is reachable with Tab").toBe(true);
    const focusStyle = await buttons.first().evaluate((b) => {
      const s = getComputedStyle(b);
      return { outline: s.outlineStyle, width: s.outlineWidth, shadow: s.boxShadow };
    });
    expect(focusStyle.outline !== "none" || focusStyle.shadow !== "none", `visible focus ${JSON.stringify(focusStyle)}`).toBe(true);
    await page.keyboard.press("Enter");
    await expect(buttons.first()).toHaveAttribute("aria-pressed", "true");
    await expect
      .poll(() => page.evaluate(() => window.__graphEvents.filter((e) => e.type === "event" && e.event.type === "selection").map((e) => e.event.objects)))
      .toContainEqual([{ type: "node", id: doc.nodes[0].id }]);
  });
});

test.describe("causal graph visuals and responsibilities", () => {
  test("Forma styles the graph with its tokens and components, and every colour is the UI-016 colour of the node's domain state [VPO-94576f95354806bd] [VPO-17ea30ea2f160b62]", async ({ page }) => {
    for (const name of ["four-roles", "disconnected", "dense-evidence"]) {
      const doc = byName[name].document;
      const { frame } = await renderGraph(page, doc);
      const m = await measure(frame);
      for (const sheet of ["tokens.css", "foundations.css", "components.css"]) expect(m.stylesheets.some((h) => h.endsWith(`/vendor/forma/${sheet}`)), `${sheet} loaded`).toBe(true);
      expect(await frame.locator("article.ef-diagram-node").count()).toBe(doc.nodes.length);
      for (const n of m.nodes) {
        const status = (n.text.match(/Status\s*\n?\s*([A-Za-z ]+)/) || [])[1]?.trim();
        const slot = slotOfStatus[status];
        expect(slot, `${name}: ${n.id} has a known status (${status})`).toBeDefined();
        expect(n.stroke.toLowerCase(), `${name}: ${n.id} colour for ${status}`).toBe(palette[slot]);
      }
      expect(Object.values(palette)).toEqual(expect.arrayContaining(Object.values(doc.palette)));
    }
  });

  test("Causa supplies semantics without geometry, the workflow engine alone places and routes (shown on a non-Causa document too), Forma alone styles [VPO-beeaf5b2a56c521d] [VPO-9ecccb3e2aa9f3f4]", async ({ page }) => {
    // Causa: no coordinates, sizes or routes in any document it produced.
    for (const f of drawn.filter((f) => f.name !== "cycle")) {
      const text = JSON.stringify(f.document);
      expect(text).not.toMatch(/"(x|y|height|waypoints|canvas)":\s*\d/);
      for (const item of [...f.document.nodes, ...(f.document.edges ?? [])]) expect(item.layout, `${f.name}: ${item.id} carries no layout`).toBeUndefined();
      expect(Object.keys(f.document.layout)).toEqual(["direction"]);
    }
    // The workflow engine: given a document Causa did not produce (a layout-only cycle), it computes placement and
    // routing on its own, without overlaps, and attaches every connector.
    const cycle = byName.cycle.document;
    const m = await measure((await renderGraph(page, cycle)).frame);
    expect(m.nodes.every((n) => Number.isFinite(n.canvas.x) && Number.isFinite(n.canvas.y) && n.canvas.w > 0)).toBe(true);
    expect(new Set(m.nodes.map((n) => `${n.canvas.x},${n.canvas.y}`)).size).toBe(3);
    expect(overlappingNodes(m)).toEqual([]);
    expect(detachedConnectors(m, cycle)).toEqual([]);
    // Forma: the visual layer (tokens and component styles) comes from Forma's stylesheets: the same document and
    // markup without them has no Forma tokens and no node styling.
    const look = (frame) => frame.locator("article.ef-diagram-node").first().evaluate((el) => ({
      token: getComputedStyle(document.documentElement).getPropertyValue("--ef-color-text-primary").trim(),
      radius: getComputedStyle(el).borderTopLeftRadius,
      border: getComputedStyle(el).borderTopStyle,
    }));
    const withForma = await look((await renderGraph(page, byName["four-roles"].document)).frame);
    const without = await look((await renderGraph(page, byName["four-roles"].document, { forma: false })).frame);
    expect(withForma.token).not.toBe("");
    expect(without.token).toBe("");
    expect(withForma).not.toEqual(without);
  });
});

test.describe("workflow layout engine capability record (WDL-001, WDL-002)", () => {
  test("the recorded engine is the installed one: owner, package, version, integrity, licence, API and constraints, with a working example [VPO-b34d65289bec1814] [VPO-35548bc529249967]", async ({ page }) => {
    const record = JSON.parse(readFileSync(join(root, "src/app/capabilities/workflow-layout-engine.json"), "utf8"));
    const installed = JSON.parse(readFileSync(join(root, "node_modules/@echelon-foundry/forma-workflow/package.json"), "utf8"));
    const lock = JSON.parse(readFileSync(join(root, "package-lock.json"), "utf8")).packages["node_modules/@echelon-foundry/forma-workflow"];
    const app = JSON.parse(readFileSync(join(root, "package.json"), "utf8"));
    expect(record.package.name).toBe(installed.name);
    expect(record.package.version).toBe(installed.version);
    expect(app.dependencies[installed.name]).toBe(installed.version);
    expect(record.package.integrity).toBe(lock.integrity);
    expect(record.license).toBe(installed.license);
    expect(installed.repository.url).toContain(record.owner.repository);
    expect(installed.repository.directory).toBe(record.owner.directory);
    for (const key of ["element", "isolatedMode", "documentFormat", "layout"]) expect(record.api[key]).toBeTruthy();
    expect(record.constraints.length).toBeGreaterThan(0);
    for (const file of ["frame.html", "frame.js", "forma-workflow.schema.json", "workflow-capabilities.json"]) expect(existsSync(join(root, "node_modules/@echelon-foundry/forma-workflow/dist", file)), file).toBe(true);
    expect(record.upstreamChangesNeeded).toBeDefined();
    // The working example: the installed engine, served by this site, lays out a Causa document.
    expect(record.workingExample).toContain("graph-layout.spec.js");
    const { events } = await renderGraph(page, byName["four-roles"].document);
    const load = events.find((e) => e.type === "event" && e.event.type === "load");
    expect(load.event.valid).toBe(true);
    expect(load.event.class).toBe("fully-supported");
  });

  test("there is no fallback layout: the record declares none and Causa has no layout or drawing code of its own [VPO-ebaf420b3a7538cd]", async () => {
    const record = JSON.parse(readFileSync(join(root, "src/app/capabilities/workflow-layout-engine.json"), "utf8"));
    expect(record.decision).toMatch(/^Reuse/);
    expect(record.fallback).toBe("none");
    // No coordinates computed, no SVG drawn, no second diagram renderer in the application or the kernel.
    const sources = [
      ...readdirSync(join(root, "src/app")).filter((f) => f.endsWith(".fs")).map((f) => join(root, "src/app", f)),
      join(root, "src/kernel/main.ts"),
      join(root, "src/kernel/index.html"),
    ];
    for (const file of sources) {
      const text = readFileSync(file, "utf8");
      expect(text, file).not.toMatch(/<svg|<path\b|createElementNS|getContext\(|translate\(/);
    }
  });
});
