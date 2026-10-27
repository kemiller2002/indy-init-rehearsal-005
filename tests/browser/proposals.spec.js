// T27: the AI proposal loop in the browser. The provider is a scripted response (page.route) so every result state
// is deterministic; the live provider is not exercised here and nothing in this file claims it. The real F# kernel
// runs over the test build's in-memory workspace.
import { expect, test } from "./support/fixtures.js";

const SITE = `http://127.0.0.1:${process.env.CAUSA_TEST_PORT ?? 4822}/`;
const KEY = "sk-ant-test-" + "K".repeat(40);
const ENDPOINT = "https://api.anthropic.com/v1/messages";
const CORS = { "access-control-allow-origin": "*", "access-control-allow-headers": "*", "access-control-expose-headers": "retry-after" };

const started = (page) => expect(page.locator("#causa-boot")).toHaveCount(0, { timeout: 60_000 });

const answer = (proposals) => JSON.stringify({
  content: [{ type: "text", text: JSON.stringify({ proposals }) }],
  stop_reason: "end_turn",
  usage: { input_tokens: 100, output_tokens: 50 },
});

/** Settings: model and key; then a started investigation, on its AI proposals section. */
async function setUp(page) {
  // No real AI call from a non-live test: anything else sent to the provider host is refused here.
  await page.route(/^https:\/\/api\.anthropic\.com\/(?!v1\/messages$)/, (route) => route.abort());
  await page.goto(SITE + "index.html#/settings");
  await started(page);
  await page.evaluate(() => window.__perceptaSetState({ name: "engine-memory", predicates: [], capabilities: {} }));
  await page.getByLabel("AI model id").fill("model-under-test");
  await page.getByLabel("Provider key").fill(KEY);
  await page.getByRole("button", { name: "Use this key" }).click();
  await expect(page.getByText("A key is held in this page's memory")).toBeVisible();
  await expect(page.getByLabel("Provider key")).toHaveValue("");
  await page.getByRole("navigation", { name: "Primary" }).getByRole("link", { name: "Investigations" }).click();
  await page.getByRole("link", { name: "New investigation" }).click();
  await page.getByLabel("Title").fill("Brake fade on the test track");
  await page.getByLabel(/Problem statement/).fill("Braking distance doubled after the third lap.");
  await page.getByRole("button", { name: "Start the investigation" }).click();
  await expect(page.locator(".causa-inv-meta")).toContainText("Version 1");
  await page.getByRole("navigation", { name: "Investigation sections" }).getByRole("link", { name: "AI proposals" }).click();
  await page.getByLabel("What should the assistant propose?").fill("Propose hypotheses.");
}

test("proposals are drafted, recorded only on request, and applied only when a person accepts one; the key goes only in the auth header [VPO-be99a964783c59b5] [VPO-04eb901270c2994a] [VPO-f97eb988899c23af] [VPO-bb08b9a414ba4cd7] [VPO-c41f10e4ed8a086b]", { tag: ["@webkit"] }, async ({ page }) => {
  const requests = [];
  await page.route(ENDPOINT, async (route) => {
    const r = route.request();
    if (r.method() === "OPTIONS") return route.fulfill({ status: 204, headers: CORS });
    requests.push({ url: r.url(), headers: r.headers(), body: r.postData() ?? "" });
    await route.fulfill({ status: 200, headers: { ...CORS, "content-type": "application/json" }, body: answer([{ kind: "hypothesis", text: "Brake fluid boiled under repeated heavy braking.", rationale: "Fade appeared after repeated stops." }]) });
  });
  await setUp(page);
  await page.getByRole("button", { name: "Request proposals" }).click();
  await expect(page.getByText("1 proposal(s) to review.")).toBeVisible();
  await expect(page.getByRole("heading", { name: "Drafted proposals (not recorded)" })).toBeVisible();
  await expect(page.locator(".causa-inv-meta")).toContainText("Version 1"); // drafting changed nothing
  // The key travelled only in the provider's authentication header.
  expect(requests).toHaveLength(1);
  expect(requests[0].headers["x-api-key"]).toBe(KEY);
  expect(requests[0].url).not.toContain(KEY);
  expect(requests[0].body).not.toContain(KEY);
  expect(requests[0].body).toContain("model-under-test");
  await page.getByRole("button", { name: "Record as proposals for review" }).click();
  await expect(page.locator(".causa-inv-meta")).toContainText("Version 2");
  const proposal = page.locator(".causa-list .causa-item--proposal").filter({ hasText: "Brake fluid boiled" });
  await expect(proposal).toContainText("Awaiting your decision");
  await expect(proposal).toContainText("Proposed by an AI agent (anthropic, model-under-test)");
  // Not applied: no hypothesis exists until a person accepts.
  await page.getByRole("navigation", { name: "Investigation sections" }).getByRole("link", { name: "Hypotheses" }).click();
  await expect(page.getByText("No hypotheses recorded yet.")).toBeVisible();
  await page.getByRole("navigation", { name: "Investigation sections" }).getByRole("link", { name: "AI proposals" }).click();
  await proposal.getByRole("button", { name: "Accept" }).click();
  await expect(proposal).toContainText("Accepted");
  await page.getByRole("navigation", { name: "Investigation sections" }).getByRole("link", { name: "Hypotheses" }).click();
  await expect(page.locator(".causa-list .causa-item--hypothesis").getByText("Brake fluid boiled under repeated heavy braking.")).toBeVisible();
  // The key is nowhere in the page, storage or diagnostics.
  expect(await page.content()).not.toContain(KEY);
  expect(await page.evaluate(() => JSON.stringify({ ...localStorage }) + JSON.stringify({ ...sessionStorage }) + JSON.stringify(window.causaDiagnostics))).not.toContain(KEY);
});

test("a rejected proposal records the person's reason and is never applied [VPO-9659118aa1ef95ff]", async ({ page }) => {
  await page.route(ENDPOINT, (route) => route.request().method() === "OPTIONS" ? route.fulfill({ status: 204, headers: CORS })
    : route.fulfill({ status: 200, headers: { ...CORS, "content-type": "application/json" }, body: answer([{ kind: "unknown", text: "Was the pad compound changed?" }]) }));
  await setUp(page);
  await page.getByRole("button", { name: "Request proposals" }).click();
  await page.getByRole("button", { name: "Record as proposals for review" }).click();
  const proposal = page.locator(".causa-list .causa-item--proposal").filter({ hasText: "pad compound" });
  await proposal.getByLabel("Why it is rejected").fill("The pads were not changed.");
  await proposal.getByRole("button", { name: "Reject" }).click();
  // Irreversible: the rejection waits for confirmation, which takes focus.
  await expect(page.getByRole("alertdialog")).toContainText("Reject this proposal?");
  await expect(page.getByRole("button", { name: "Yes, continue" })).toBeFocused();
  await page.getByRole("button", { name: "Yes, continue" }).click();
  await expect(proposal).toContainText("Rejected");
  await expect(proposal.getByRole("button")).toHaveCount(0);
});

const cases = [
  { name: "overloaded (529) offers Try again", status: 529, body: { type: "error", error: { type: "overloaded_error", message: "Overloaded" } }, text: "The provider is overloaded; try again shortly.", retry: true },
  { name: "rate limited (429) offers Try again", status: 429, headers: { "retry-after": "7" }, body: { type: "error", error: { type: "rate_limit_error", message: "slow down" } }, text: "rate limiting", retry: true },
  { name: "a rejected key (401) offers no Try again", status: 401, body: { type: "error", error: { type: "authentication_error", message: "invalid x-api-key" } }, text: "The provider rejected the key", retry: false },
  { name: "an unavailable model (404) offers no Try again", status: 404, body: { type: "error", error: { type: "not_found_error", message: "model: model-under-test" } }, text: "The configured model is not available.", retry: false },
  { name: "a malformed answer is refused whole and offers Try again", status: 200, raw: JSON.stringify({ content: [{ type: "text", text: "{\"proposals\":[{\"kind\":\"command\",\"text\":\"Confirm the cause\"}]}" }], stop_reason: "end_turn" }), text: "not in the expected proposal format", retry: true },
];

for (const c of cases) {
  test(`AI result state: ${c.name}; nothing is recorded [VPO-e54ee03f405cdc8f] [VPO-97351596eb39e14e]`, async ({ page }) => {
    await page.route(ENDPOINT, (route) => route.request().method() === "OPTIONS" ? route.fulfill({ status: 204, headers: CORS })
      : route.fulfill({ status: c.status, headers: { ...CORS, "content-type": "application/json", ...(c.headers ?? {}) }, body: c.raw ?? JSON.stringify(c.body) }));
    await setUp(page);
    await page.getByRole("button", { name: "Request proposals" }).click();
    await expect(page.locator(".causa-notice[role=status]").filter({ hasText: c.text })).toBeVisible();
    await expect(page.getByRole("button", { name: "Try again" })).toHaveCount(c.retry ? 1 : 0);
    await expect(page.locator(".causa-inv-meta")).toContainText("Version 1");
  });
}

test("drafts made from an older version are labelled stale, never mixed in as current [VPO-d08de632c7c1a4ab]", async ({ page }) => {
  let release;
  const gate = new Promise((r) => { release = r; });
  await page.route(ENDPOINT, async (route) => {
    if (route.request().method() === "OPTIONS") return route.fulfill({ status: 204, headers: CORS });
    await gate;
    await route.fulfill({ status: 200, headers: { ...CORS, "content-type": "application/json" }, body: answer([{ kind: "action", text: "Measure rotor temperature after each lap." }]) });
  });
  await setUp(page);
  await page.getByRole("button", { name: "Request proposals" }).click();
  await expect(page.getByText("Waiting for the provider…")).toBeVisible();
  // The investigation changes while the request is out.
  await page.getByRole("navigation", { name: "Investigation sections" }).getByRole("link", { name: "Overview" }).click();
  await page.getByRole("button", { name: "Activate the investigation" }).click();
  await expect(page.locator(".causa-inv-meta")).toContainText("Version 2");
  await page.getByRole("navigation", { name: "Investigation sections" }).getByRole("link", { name: "AI proposals" }).click();
  release();
  await expect(page.locator(".causa-stale")).toContainText("Stale: drafted from version 1; the investigation is now at version 2.");
});

test("cancelling a request settles it as cancelled and its late answer changes nothing", async ({ page }) => {
  let release;
  const gate = new Promise((r) => { release = r; });
  await page.route(ENDPOINT, async (route) => {
    if (route.request().method() === "OPTIONS") return route.fulfill({ status: 204, headers: CORS });
    await gate;
    await route.fulfill({ status: 200, headers: { ...CORS, "content-type": "application/json" }, body: answer([{ kind: "hypothesis", text: "Late." }]) });
  });
  await setUp(page);
  await page.getByRole("button", { name: "Request proposals" }).click();
  await page.getByRole("button", { name: "Cancel the request" }).click();
  await expect(page.getByText("The request was cancelled; nothing was recorded.")).toBeVisible();
  release();
  await page.waitForTimeout(1000);
  await expect(page.getByText("Drafted proposals (not recorded)")).toHaveCount(0);
  await expect(page.getByText("The request was cancelled; nothing was recorded.")).toBeVisible();
});
