// @live: the causal graph of the real historical investigations (WDL-012), in a real browser after a real Connect
// to kemiller2002/causa-data. Needs CAUSA_GITHUB_TOKEN; a missing secret fails as BLOCKED, never skips or passes.
// The cases are read from the data repository's own catalog at run time (no id is written here), and what the
// graph must show is derived independently, in this test process, from the cases' event histories.
// Traces, screenshots and video are off: the token is typed into the page.
import { expect, test } from "@playwright/test";
import { measure, overlappingNodes, labelsOverNodes, crossings, detachedConnectors } from "./support/graph-harness.js";

const token = process.env.CAUSA_GITHUB_TOKEN ?? "";
const catalogRepo = "kemiller2002/causa-data";
const api = "https://api.github.com";
const roleLabel = { rootCause: "Root cause", trigger: "Trigger", contributingFactor: "Contributing factor", enablingCondition: "Enabling condition" };

test.use({ trace: "off", screenshot: "off", video: "off" });

async function gh(path) {
  const response = await fetch(`${api}${path}`, {
    headers: { Authorization: `Bearer ${token}`, Accept: "application/vnd.github+json", "X-GitHub-Api-Version": "2022-11-28" },
  });
  expect(response.status, path).toBe(200);
  return response.json();
}

async function text(path) {
  const entry = await gh(`/repos/${catalogRepo}/contents/${path}`);
  if (entry.content) return Buffer.from(entry.content, "base64").toString("utf8");
  const blob = await gh(`/repos/${catalogRepo}/git/blobs/${entry.sha}`);
  return Buffer.from(blob.content, "base64").toString("utf8");
}

/** What the graph must show, derived from the authoritative events alone. */
function expectedGraph(events) {
  const causes = new Map();
  const relations = new Map();
  for (const e of events) {
    if (e.eventType === "CausalAssertionRecorded") causes.set(e.payload.causeId, e.payload.members);
    if (e.eventType === "EvidenceRelated") {
      const r = relations.get(e.payload.hypothesisId) ?? { supports: 0, contradicts: 0 };
      if (e.payload.kind === "supports") r.supports++;
      if (e.payload.kind === "contradicts") r.contradicts++;
      relations.set(e.payload.hypothesisId, r);
    }
  }
  const members = new Set([...causes.values()].flat().map((m) => m.hypothesisId));
  return {
    nodes: [...[...members].map((h) => `h-${h}`), ...[...causes.keys()].map((c) => `c-${c}`)].sort(),
    edges: [...causes.entries()].flatMap(([c, ms]) => ms.map((m) => `${m.hypothesisId}->${c}:${roleLabel[m.role]}`)).sort(),
    counts: Object.fromEntries([...members].map((h) => [h, relations.get(h) ?? { supports: 0, contradicts: 0 }])),
  };
}

test(
  "WDL-012 the graph of each real historical investigation shows exactly its recorded causal structure and evidence counts, laid out without overlap or clipping, stable, with text equivalents",
  { tag: ["@live", "@VPO-3ec21931ff613a67", "@VPO-f635d053f07b64fe"] },
  async ({ page }) => {
    test.setTimeout(600_000);
    if (!token) throw new Error("BLOCKED: CAUSA_GITHUB_TOKEN is not available (a missing secret is never a pass)");
    const active = JSON.parse(await text("catalog/active.json"));
    const manifest = JSON.parse(await text(active.manifest));
    const branch = (await gh(`/repos/${catalogRepo}`)).default_branch;

    await page.goto("index.html");
    await expect(page.locator("#causa-boot")).toHaveCount(0, { timeout: 90_000 });
    const main = page.getByRole("main");
    await main.getByLabel("Fine-grained GitHub token").fill(token);
    await main.getByLabel("Repository (owner/name)").fill(catalogRepo);
    await main.getByLabel("Branch").fill(branch);
    await main.getByLabel("Workspace").fill(active.workspace.replace("workspaces/demo/", ""));
    await main.getByRole("button", { name: "Connect" }).click();
    // The connection line shows the state in words, not the repository name.
    await expect(page.locator(".causa-connection")).toHaveText(/Data repository: Connected$/, { timeout: 120_000 });

    for (const c of manifest.cases) {
      const events = (await text(`${c.path}/events.ndjson`)).split("\n").filter((l) => l.length > 0).map((l) => JSON.parse(l));
      const expected = expectedGraph(events);
      await page.goto(`index.html#/investigations/${c.investigationId}/causal-analysis`);
      const graph = page.locator("#causa-graph");
      await expect(graph).toHaveAttribute("data-document", /forma-workflow/, { timeout: 120_000 });
      const documentText = await graph.getAttribute("data-document");
      const doc = JSON.parse(documentText);

      // Expected nodes, roles and relationships: exactly the recorded causal structure.
      expect(doc.nodes.map((n) => n.id).sort(), `${c.caseKey}: nodes`).toEqual(expected.nodes);
      expect(doc.edges.map((e) => `${e.source.node.slice(2)}->${e.target.node.slice(2)}:${e.label}`).sort(), `${c.caseKey}: edges`).toEqual(expected.edges);
      // Evidence counts as the events record them.
      for (const n of doc.nodes.filter((n) => n.id.startsWith("h-"))) {
        const k = expected.counts[n.id.slice(2)];
        expect(n.description, `${c.caseKey}: ${n.id} counts`).toBe(`${k.supports} supporting, ${k.contradicts} contradicting evidence`);
      }

      // Laid out by the real engine in the app's frame: no overlap, clipping, labels over nodes or detached connectors.
      const frame = page.frames().find((f) => /forma-workflow\/frame\.html/.test(f.url()));
      await frame.waitForSelector("article.ef-diagram-node", { timeout: 120_000 });
      await expect.poll(async () => (await measure(frame)).nodes.length, { timeout: 120_000 }).toBe(doc.nodes.length);
      const m = await measure(frame);
      expect(overlappingNodes(m), `${c.caseKey}: overlapping nodes`).toEqual([]);
      expect(m.nodes.filter((n) => n.overflowing || n.clippedParts.length > 0).map((n) => n.id), `${c.caseKey}: clipped`).toEqual([]);
      expect(labelsOverNodes(m), `${c.caseKey}: labels over nodes`).toEqual([]);
      expect(detachedConnectors(m, doc), `${c.caseKey}: detached connectors`).toEqual([]);
      test.info().annotations.push({ type: "crossings", description: `${c.caseKey}: ${crossings(m)} crossings, ${doc.edges.length} edges` });
      // Text equivalents for keyboard and screen-reader users.
      expect(m.relations.length, `${c.caseKey}: relationship list`).toBe(doc.edges.length);
      await expect(page.getByRole("list", { name: "Causal relationships as text" })).toBeVisible();

      // Stable output: the same investigation projects the same document after a reload of the route.
      await page.goto(`index.html#/investigations/${c.investigationId}/overview`);
      await page.goto(`index.html#/investigations/${c.investigationId}/causal-analysis`);
      await expect(graph).toHaveAttribute("data-document", documentText, { timeout: 120_000 });
    }
  },
);
