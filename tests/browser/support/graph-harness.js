// Graph harness (WDL-006/007/008/012): loads a Forma workflow document into the real @echelon-foundry/forma-workflow
// frame exactly as the application does (isolated mode, the site's own vendored copy and Forma styles), and
// measures what was rendered. Measurements only; no decision about what the graph means.
export const PROTOCOL = "forma-workflow-host/1";

/** Mount the frame on the served site and load `doc`; resolves with the frame and the events it sent. */
export async function renderGraph(page, doc, { width = "100%", height = "720px", forma = true } = {}) {
  await page.goto("index.html");
  const events = await page.evaluate(
    ({ doc, width, height, forma, PROTOCOL }) =>
      new Promise((resolve, reject) => {
        const events = [];
        const frame = document.createElement("iframe");
        frame.id = "graph-under-test";
        frame.title = "Causal graph under test";
        frame.style.cssText = `display:block;width:${width};height:${height};border:0`;
        const src = new URL("vendor/forma-workflow/frame.html", location.href);
        src.searchParams.set("parent", location.origin);
        if (forma) src.searchParams.set("forma", new URL("vendor/forma/", location.href).href);
        src.searchParams.set("brand", "causa");
        frame.src = src.href;
        window.__graphEvents = events;
        window.addEventListener("message", (e) => {
          if (e.source !== frame.contentWindow || !e.data || e.data.protocol !== PROTOCOL) return;
          events.push(e.data);
          if (e.data.type === "frame-ready") {
            frame.contentWindow.postMessage({ protocol: PROTOCOL, id: "load", type: "load", document: doc }, location.origin);
            frame.contentWindow.postMessage({ protocol: PROTOCOL, id: "mode", type: "mode", mode: "inspect" }, location.origin);
          }
          const done = (pred) => events.some(pred);
          if (done((m) => m.type === "reply" && m.id === "mode") && done((m) => m.type === "reply" && m.id === "load") && done((m) => m.type === "event" && m.event && m.event.type === "load")) resolve(events);
          if (e.data.type === "reply" && e.data.ok === false) reject(new Error(e.data.error));
          if (e.data.type === "event" && e.data.event && e.data.event.type === "load" && e.data.event.valid === false)
            reject(new Error("the workflow engine refused the document: " + JSON.stringify(e.data.event.findings)));
        });
        document.body.replaceChildren(frame);
        setTimeout(() => reject(new Error("the workflow frame did not answer")), 60_000);
      }),
    { doc, width, height, forma, PROTOCOL },
  );
  const frame = page.frame({ url: /forma-workflow\/frame\.html/ });
  await frame.waitForSelector("article.ef-diagram-node");
  return { frame, events };
}

/** Everything the layout checks need, measured in the frame. */
export async function measure(frame) {
  return frame.evaluate(() => {
    const rem = parseFloat(getComputedStyle(document.documentElement).fontSize) || 16;
    const px = (el, name) => parseFloat(getComputedStyle(el).getPropertyValue(name)) * rem;
    const nodes = [...document.querySelectorAll("article.ef-diagram-node")].map((el) => {
      const r = el.getBoundingClientRect();
      const parts = [...el.querySelectorAll(".ef-diagram-node__kind, .ef-diagram-node__label, .ef-diagram-node__description, .ef-diagram-node__meta")];
      return {
        id: el.id.replace(/^workflow-n-/, ""),
        box: { x: r.left, y: r.top, w: r.width, h: r.height },
        canvas: { x: px(el, "--ef-diagram-x"), y: px(el, "--ef-diagram-y"), w: px(el, "--ef-diagram-w"), h: px(el, "--ef-diagram-h") },
        text: el.innerText,
        overflowing: el.scrollHeight > el.clientHeight + 1 || el.scrollWidth > el.clientWidth + 1,
        clippedParts: parts
          .filter((p) => {
            const pr = p.getBoundingClientRect();
            return p.scrollWidth > p.clientWidth + 1 || p.scrollHeight > p.clientHeight + 1 || pr.left < r.left - 1 || pr.right > r.right + 1 || pr.top < r.top - 1 || pr.bottom > r.bottom + 1;
          })
          .map((p) => p.className),
        stroke: getComputedStyle(el).getPropertyValue("--ef-diagram-stroke").trim(),
        status: el.getAttribute("data-ef-status"),
      };
    });
    const connectors = [...document.querySelectorAll("path.ef-diagram-connector")].map((p) => {
      const points = (p.getAttribute("d").match(/-?\d+(\.\d+)?/g) || []).map(Number);
      const pts = [];
      for (let i = 0; i + 1 < points.length; i += 2) pts.push([points[i], points[i + 1]]);
      return { id: p.id.replace(/^workflow-e-/, ""), points: pts, dash: getComputedStyle(p).strokeDasharray, width: getComputedStyle(p).strokeWidth };
    });
    const labels = [...document.querySelectorAll(".ef-diagram-connector__label")].map((l) => {
      const r = l.getBoundingClientRect();
      return { text: l.textContent, box: { x: r.left, y: r.top, w: r.width, h: r.height } };
    });
    const relations = [...document.querySelectorAll("ol.ef-diagram__relations li")].map((li) => li.textContent.trim());
    const objects = [...document.querySelectorAll(".ef-workflow-editor__list button")].map((b) => b.textContent.trim());
    const legend = [...document.querySelectorAll(".ef-diagram-legend dt")].map((d) => d.textContent.trim());
    const stylesheets = [...document.styleSheets].map((s) => s.href || "").filter(Boolean);
    return { nodes, connectors, labels, relations, objects, legend, stylesheets, pageWidth: document.documentElement.scrollWidth, viewportWidth: innerWidth };
  });
}

const overlapArea = (a, b) => Math.max(0, Math.min(a.x + a.w, b.x + b.w) - Math.max(a.x, b.x)) * Math.max(0, Math.min(a.y + a.h, b.y + b.h) - Math.max(a.y, b.y));

/** Pairs of nodes whose boxes overlap (more than a hairline). */
export function overlappingNodes(m) {
  const out = [];
  for (let i = 0; i < m.nodes.length; i++)
    for (let j = i + 1; j < m.nodes.length; j++) if (overlapArea(m.nodes[i].canvas, m.nodes[j].canvas) > 1) out.push([m.nodes[i].id, m.nodes[j].id]);
  return out;
}

/** Edge labels drawn over a node (screen coordinates). */
export function labelsOverNodes(m) {
  return m.labels.filter((l) => m.nodes.some((n) => overlapArea(l.box, n.box) > 1)).map((l) => l.text);
}

const orient = (a, b, c) => Math.sign((b[0] - a[0]) * (c[1] - a[1]) - (b[1] - a[1]) * (c[0] - a[0]));
const near = (p, q) => Math.abs(p[0] - q[0]) < 0.5 && Math.abs(p[1] - q[1]) < 0.5;

/** Proper crossings between connectors of different edges (shared end points and collinear overlaps excluded). */
export function crossings(m) {
  const segs = m.connectors.flatMap((c) => c.points.slice(1).map((p, i) => ({ id: c.id, a: c.points[i], b: p })));
  let count = 0;
  for (let i = 0; i < segs.length; i++)
    for (let j = i + 1; j < segs.length; j++) {
      const s = segs[i], t = segs[j];
      if (s.id === t.id) continue;
      if (near(s.a, t.a) || near(s.a, t.b) || near(s.b, t.a) || near(s.b, t.b)) continue;
      const o1 = orient(s.a, s.b, t.a), o2 = orient(s.a, s.b, t.b), o3 = orient(t.a, t.b, s.a), o4 = orient(t.a, t.b, s.b);
      if (o1 * o2 < 0 && o3 * o4 < 0) count++;
    }
  return count;
}

/** Whether each connector starts on its source box and ends on its target box (canvas coordinates). */
export function detachedConnectors(m, doc) {
  const box = Object.fromEntries(m.nodes.map((n) => [n.id, n.canvas]));
  const on = (p, b) => p[0] >= b.x - 2 && p[0] <= b.x + b.w + 2 && p[1] >= b.y - 2 && p[1] <= b.y + b.h + 2;
  return doc.edges
    .filter((e) => {
      const c = m.connectors.find((x) => x.id === e.id);
      if (!c || c.points.length < 2) return true;
      return !on(c.points[0], box[e.source.node]) || !on(c.points[c.points.length - 1], box[e.target.node]);
    })
    .map((e) => e.id);
}
