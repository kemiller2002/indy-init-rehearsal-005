// Navigation from the causal graph to the records it shows (UI-005-O4), in the application itself (test build,
// synthetic injected state). The graph frame reports a node's "Open" intent; the kernel follows only the record
// links Causa's graph wrote, and only within "#/investigations/" (allow-list).
import { expect, test } from "./support/fixtures.js";

const TEST_SITE = `http://127.0.0.1:${process.env.CAUSA_TEST_PORT ?? 4322}/`;
const FIXTURE = "00000000-0000-4000-8000-0000000000f1";
const PROTOCOL = "forma-workflow-host/1";

async function openGraph(page) {
  await page.goto(`${TEST_SITE}index.html#/investigations/${FIXTURE}/causal-analysis`);
  await expect(page.locator("#causa-boot")).toHaveCount(0, { timeout: 60_000 });
  await page.evaluate(() => window.__perceptaSetState({ name: "graph", predicates: [], capabilities: {} }));
  const frame = page.frameLocator("iframe.causa-graph__frame");
  await expect(frame.locator("article.ef-diagram-node").first()).toBeVisible({ timeout: 60_000 });
  return frame;
}

/** Make the frame itself report a navigate intent for `node`/`reference`, as the engine does. */
async function intentFromFrame(page, node, reference) {
  const frame = page.frames().find((f) => /forma-workflow\/frame\.html/.test(f.url()));
  await frame.evaluate(({ node, reference, PROTOCOL }) => {
    window.parent.postMessage({ protocol: PROTOCOL, type: "event", event: { type: "intent", object: { type: "node", id: node }, action: "navigate", target: { reference } } }, location.origin);
  }, { node, reference, PROTOCOL });
}

test.describe("navigation from the causal graph (UI-005-O4)", () => {
  test("a hypothesis node's Open action lands on its evidence record in the evidence view [VPO-c7c84a08e62427e4]", async ({ page }) => {
    const frame = await openGraph(page);
    const doc = JSON.parse(await page.locator("#causa-graph").getAttribute("data-document"));
    const hypothesis = doc.nodes.find((n) => n.id === "h-h-1");
    const target = hypothesis.references.find((r) => r.id === hypothesis.interaction.target.reference);
    expect(target.href).toBe(`#/investigations/${FIXTURE}/evidence?item=e-1`);
    // The engine renders the node's Open action; activating it (a real click in the frame) navigates the app.
    await frame.locator("article#workflow-n-h-h-1 button.ef-workflow-editor__intent").click();
    await expect(page).toHaveURL(new RegExp(`#/investigations/${FIXTURE}/evidence\\?item=e-1$`));
    await expect(page.getByText("Launch-time temperature was recorded below the qualified range.").first()).toBeVisible();
    // Keyboard works too: back to the graph, focus the outcome's Open action and press Enter.
    await page.goBack();
    await expect(page).toHaveURL(/causal-analysis$/);
    const frame2 = page.frameLocator("iframe.causa-graph__frame");
    const open = frame2.locator("article#workflow-n-c-c-1 button.ef-workflow-editor__intent");
    await expect(open).toHaveCount(1);
    await open.focus();
    await page.keyboard.press("Enter");
    await expect(page).toHaveURL(new RegExp(`#/investigations/${FIXTURE}/causal-analysis\\?item=c-1$`));
  });

  test("a link outside #/investigations/ is never followed, even when the graph document carries it [VPO-c7c84a08e62427e4]", async ({ page }) => {
    await openGraph(page);
    const start = page.url();
    const doc = JSON.parse(await page.locator("#causa-graph").getAttribute("data-document"));
    const crafted = ["https://example.invalid/steal", "javascript:alert(1)", "//example.invalid/x", "#/administration", "#/investigationsX/y", "data:text/html,x"];
    // Positive control first: the same mechanism with an allowed href navigates, so a pass below is not vacuous.
    const withRefs = (hrefs) => ({
      ...doc,
      nodes: doc.nodes.map((n) => (n.id !== "h-h-1" ? n : { ...n, references: hrefs.map((href, i) => ({ id: `r${i}`, system: "org.causa", type: "test", key: `k${i}`, label: `L${i}`, href })) })),
    });
    const allowed = `#/investigations/${FIXTURE}/hypotheses?item=h-1`;
    await page.locator("#causa-graph").evaluate((el, text) => el.setAttribute("data-document", text), JSON.stringify(withRefs([allowed, ...crafted])));
    await page.waitForTimeout(500);
    await intentFromFrame(page, "h-h-1", "r0");
    await expect(page).toHaveURL(new RegExp("hypotheses\\?item=h-1$"));
    await page.goto(start);
    await openGraph(page);
    await page.locator("#causa-graph").evaluate((el, text) => el.setAttribute("data-document", text), JSON.stringify(withRefs([allowed, ...crafted])));
    await page.waitForTimeout(500);
    const before = page.url();
    for (let i = 1; i <= crafted.length; i++) {
      await intentFromFrame(page, "h-h-1", `r${i}`);
      await page.waitForTimeout(300);
      expect(page.url(), `crafted href ${crafted[i - 1]} was followed`).toBe(before);
    }
    // An intent for a node or reference the document does not contain is ignored as well.
    await intentFromFrame(page, "h-missing", "r0");
    await intentFromFrame(page, "h-h-1", "nope");
    await page.waitForTimeout(300);
    expect(page.url()).toBe(before);
  });
});
