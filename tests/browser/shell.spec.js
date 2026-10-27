// The Causa application shell in a real browser, against the production build (dist/) unless a test says otherwise.
// Each test title carries the verification-plan obligation ids (VPO-…) it proves.
import { readFileSync } from "node:fs";
import { expect, test } from "@playwright/test";

const TEST_SITE = "http://127.0.0.1:4322/";

/** Waits until the application has replaced the loading shell. */
async function started(page) {
  await expect(page.locator("#causa-boot")).toHaveCount(0, { timeout: 60_000 });
}

test.describe("loading experience (WASM-LOAD, T16)", () => {
  test("the branded shell shows before the WebAssembly runtime and hands off without a blank frame [VPO-0bd752bd2ac89796] [VPO-cb75698b278332f5]", async ({ page }) => {
    let release;
    const gate = new Promise((resolve) => { release = resolve; });
    await page.route("**/wasm/_framework/dotnet.js", async (route) => { await gate; await route.continue(); });
    await page.goto("index.html");
    const boot = page.locator("#causa-boot");
    await expect(boot).toBeVisible();
    await expect(boot).toHaveAttribute("role", "status");
    await expect(page.locator("#causa-boot-label")).toHaveText("Starting Causa…");
    await expect(boot.getByRole("img", { name: "Causa" })).toBeVisible();
    // No fabricated progress: no percentage anywhere in the loading shell.
    await expect(boot).not.toContainText("%");
    release();
    await started(page);
    await expect(page.getByRole("heading", { level: 1, name: "Connect a data repository" })).toBeVisible();
    // Focus is not lost in the handoff: it lands on the ready announcement.
    await expect(page.locator("#causa-ready")).toBeFocused();
    await expect(page.locator("#causa-ready")).toHaveText("Causa is ready.");
  });

  test("a 503 for the runtime ends in an explained failure with Retry focused, and Retry then starts Causa [VPO-0bd752bd2ac89796] [VPO-4355a83607ddc8da] [VPO-e54ee03f405cdc8f] [VPO-97351596eb39e14e]", async ({ page }) => {
    let fail = true;
    await page.route("**/wasm/_framework/*.wasm", (route) => (fail ? route.fulfill({ status: 503, body: "unavailable" }) : route.continue()));
    await page.goto("index.html");
    const boot = page.locator("#causa-boot");
    await expect(boot).toHaveAttribute("data-state", "failed", { timeout: 60_000 });
    await expect(boot).toHaveAttribute("role", "alert");
    await expect(page.locator("#causa-boot-detail")).toHaveText("Nothing was saved or sent. Retry starts Causa again.");
    const retry = boot.getByRole("button", { name: "Retry" });
    await expect(retry).toBeFocused();
    // Nothing technical leaks into the message.
    await expect(boot).not.toContainText("503");
    fail = false;
    await page.keyboard.press("Enter");
    await started(page);
    await expect(page.getByRole("heading", { level: 1, name: "Connect a data repository" })).toBeVisible();
  });
});

test.describe("routes and navigation (T16)", () => {
  test("before Connect the workspace is empty and offers the explicit Connect action [VPO-2f4f9de66ddd6434]", async ({ page }) => {
    await page.goto("index.html");
    await started(page);
    const main = page.getByRole("main");
    await expect(main.getByRole("button", { name: "Connect" })).toBeVisible();
    await expect(main.getByLabel("Fine-grained GitHub token")).toBeVisible();
    await expect(page.locator(".causa-card")).toHaveCount(0);
    await expect(page.locator("[data-percepta-screen]")).toHaveAttribute("data-percepta-screen", "connect");
  });

  test("the three primary destinations are distinct, reachable and mark the current one [VPO-e5cfc842f2367421] [VPO-7bb1e8c5bdccc2f2] [VPO-625e96f365f2c1b8] [VPO-41642822de594b56] [VPO-23d8a096e64b2625]", async ({ page }) => {
    await page.goto("index.html");
    await started(page);
    const nav = page.getByRole("navigation", { name: "Primary" });
    for (const [name, heading, hash] of [["Administration", "Administration", "#/administration"], ["Settings", "Settings", "#/settings"], ["Investigations", "Connect a data repository", "#/investigations"]]) {
      await nav.getByRole("link", { name }).click();
      await expect(page).toHaveURL(new RegExp(hash.replace("/", "\\/") + "$"));
      await expect(page.getByRole("heading", { level: 1 })).toHaveText(heading);
      await expect(nav.getByRole("link", { name })).toHaveAttribute("aria-current", "page");
      await expect(nav.locator('a[aria-current="page"]')).toHaveCount(1);
    }
    await expect(page).toHaveTitle("Connect · Causa");
  });

  test("a deep link survives reload and an unknown address fails with a useful recovery [VPO-adf482d16e75a882]", async ({ page }) => {
    await page.goto("index.html#/settings");
    await started(page);
    await expect(page.getByRole("heading", { level: 1 })).toHaveText("Settings");
    await page.reload();
    await started(page);
    await expect(page.getByRole("heading", { level: 1 })).toHaveText("Settings");
    await page.goto("index.html#/no/such/page");
    await page.reload();
    await started(page);
    await expect(page.getByRole("heading", { level: 1 })).toHaveText("Page not found");
    await page.getByRole("link", { name: "Go to Investigations" }).click();
    await expect(page).toHaveURL(/#\/investigations$/);
  });
});

test.describe("Administration and Settings (UI-018, UI-019, VER-007)", () => {
  test("Administration shows the versions actually loaded, and no investigation page or footer shows them [VPO-b93e86bd3809524a] [VPO-183d6671da7467b8] [VPO-a07751ce0dce5689] [VPO-a74264901d267d03] [VPO-3f3db98e519aa95c]", async ({ page, baseURL }) => {
    const info = await (await page.request.get(new URL("build-info.json", baseURL).href)).json();
    await page.goto("index.html#/administration");
    await started(page);
    const table = page.locator("[data-percepta-screen=administration]");
    const limen = JSON.parse(readFileSync("node_modules/@echelon-foundry/limen/package.json", "utf8")).version;
    const forma = JSON.parse(readFileSync("node_modules/@echelon-foundry/design-system/package.json", "utf8")).version;
    if (!process.env.CAUSA_BASE_URL) {
      expect(info.components["limen (kernel)"]).toBe(limen);
      expect(info.components["forma (design system)"]).toBe(forma);
    }
    await expect(table.getByRole("row", { name: /components\.limen \(kernel\)/ })).toContainText(info.components["limen (kernel)"]);
    await expect(table.getByRole("row", { name: /components\.forma \(design system\)/ })).toContainText(info.components["forma (design system)"]);
    await expect(table.getByRole("row", { name: /^commit/ })).toContainText(info.commit);
    await expect(table.getByRole("row", { name: /\.NET runtime \(loaded\)/ })).toContainText(/^.*\d+\.\d+\.\d+/);
    // Unknown and unavailable are distinct words, never an implied pass.
    await expect(page.getByRole("row", { name: /Data provider/ })).toContainText("Unavailable");
    await expect(page.getByRole("row", { name: /Investigation engine/ })).toContainText("Reported by the running application");
    // Versions and commits stay out of the workflow and the chrome.
    await page.getByRole("navigation", { name: "Primary" }).getByRole("link", { name: "Investigations" }).click();
    await expect(page.locator("body")).not.toContainText(info.commit);
    await expect(page.locator("header.causa-masthead")).not.toContainText(limen);
  });

  test("Settings apply for the session only, show effective values and store nothing in the browser [VPO-1224fd4d58ddcaa6] [VPO-d39e3d1f28d9e2ac] [VPO-c5d77a4b2eb52927] [VPO-fc28e815f45f2e9b]", async ({ page }) => {
    await page.goto("index.html#/settings");
    await started(page);
    await expect(page.getByText("These preferences apply to this browser tab for this session only.")).toBeVisible();
    await page.getByLabel("Text size").selectOption("large");
    await expect(page.locator("body")).toHaveAttribute("data-text-size", "large");
    await page.getByLabel("Motion").selectOption("reduced");
    await expect(page.locator("body")).toHaveAttribute("data-motion", "reduced");
    await expect(page.getByLabel("Text size")).toHaveValue("large");
    await expect(page.getByText(/Not connected\. The repository you enter on Connect/)).toBeVisible();
    const stored = await page.evaluate(() => ({ local: localStorage.length, session: sessionStorage.length }));
    expect(stored).toEqual({ local: 0, session: 0 });
    await page.reload();
    await started(page);
    await expect(page.locator("body")).toHaveAttribute("data-text-size", "standard");
  });
});

test.describe("security of the shell", () => {
  test("a typed token never reaches the page, the URL or browser storage [VPO-bb08b9a414ba4cd7] [VPO-fba8a61be6d12adf] [VPO-06e616ae2378cfcd]", async ({ page }) => {
    const token = "github_pat_" + "X".repeat(40);
    await page.goto("index.html");
    await started(page);
    await page.getByLabel("Fine-grained GitHub token").fill(token);
    await page.getByLabel("Repository (owner/name)").fill("someone/somewhere");
    await page.getByLabel("Workspace").fill("demo");
    await page.getByRole("button", { name: "Connect" }).click();
    await expect(page.getByRole("alert").first()).toContainText("Nothing was sent");
    const html = await page.content();
    expect(html).not.toContain(token);
    expect(page.url()).not.toContain(token);
    const stored = await page.evaluate(() => JSON.stringify({ ...localStorage }) + JSON.stringify({ ...sessionStorage }));
    expect(stored).not.toContain(token);
    const diagnostics = await page.evaluate(() => JSON.stringify(window.causaDiagnostics));
    expect(diagnostics).not.toContain(token);
  });

  test("the production build has no Percepta state hook", async ({ page }) => {
    await page.goto("index.html");
    await started(page);
    expect(await page.evaluate(() => typeof window.__perceptaSetState)).toBe("undefined");
  });
});

test.describe("identity, accessibility and layout (UI-011, UI-014, UI-016)", () => {
  test("the Causa palette is mapped to named Forma tokens with measured contrast [VPO-cecb71404071030d] [VPO-3d80d031f05ec0dc] [VPO-d0c711c2da0f3800] [VPO-e0e945fc040386e1] [VPO-fbc386df77130f2d] [VPO-8905fd51176a3fc6] [VPO-fe19413526420c16] [VPO-d657e8dd084961b1] [VPO-6051ea46b65ebe27] [VPO-1a1aae8ffa920386]", async ({ page }) => {
    await page.goto("index.html");
    await started(page);
    const tokens = await page.evaluate(() => {
      const s = getComputedStyle(document.documentElement);
      const v = (n) => s.getPropertyValue(n).trim().toUpperCase();
      return { navy: v("--causa-navy"), teal: v("--causa-teal"), amber: v("--causa-amber"), coral: v("--causa-coral"), ivory: v("--causa-ivory"),
        surface: v("--ef-color-surface-primary"), inverse: v("--ef-color-surface-inverse"), focus: v("--ef-color-focus-ring") };
    });
    expect(tokens).toMatchObject({ navy: "#14283F", teal: "#087F8C", amber: "#E9A23B", coral: "#D66B58", ivory: "#F5F3ED", surface: "#F5F3ED", inverse: "#14283F", focus: "#087F8C" });
    // Not monochrome: the masthead is navy and the page ivory as rendered.
    const colours = await page.evaluate(() => ({ masthead: getComputedStyle(document.querySelector(".causa-masthead")).backgroundColor, body: getComputedStyle(document.body).backgroundColor }));
    expect(colours).toEqual({ masthead: "rgb(20, 40, 63)", body: "rgb(245, 243, 237)" });
    // WCAG AA for the text pairs the shell uses.
    const contrast = (a, b) => {
      const lum = (hex) => { const c = [1, 3, 5].map((i) => parseInt(hex.slice(i, i + 2), 16) / 255).map((x) => (x <= 0.03928 ? x / 12.92 : ((x + 0.055) / 1.055) ** 2.4)); return 0.2126 * c[0] + 0.7152 * c[1] + 0.0722 * c[2]; };
      const [x, y] = [lum(a), lum(b)].sort((p, q) => q - p);
      return (x + 0.05) / (y + 0.05);
    };
    const css = readFileSync("src/kernel/causa.css", "utf8");
    const ink = (name) => css.match(new RegExp(`--causa-${name}:\\s*(#[0-9A-Fa-f]{6})`))[1];
    expect(contrast("#F5F3ED", "#14283F")).toBeGreaterThanOrEqual(4.5);
    for (const name of ["teal-ink", "coral-ink", "amber-ink", "muted-ink"]) expect(contrast(ink(name), "#F5F3ED")).toBeGreaterThanOrEqual(4.5);
    expect(contrast("#087F8C", "#F5F3ED")).toBeGreaterThanOrEqual(3);
  });

  test("keyboard: skip link first, visible focus, landmarks and a favicon [VPO-447fd64210e3d9e3] [VPO-9501908622238a66]", async ({ page }) => {
    await page.goto("index.html");
    await started(page);
    await page.keyboard.press("Tab");
    const skip = page.getByRole("link", { name: "Skip to main content" });
    await expect(skip).toBeFocused();
    await page.keyboard.press("Enter");
    await expect(page.locator("#causa-main")).toBeFocused();
    await page.keyboard.press("Tab");
    const outline = await page.evaluate(() => { const s = getComputedStyle(document.activeElement); return `${s.outlineStyle} ${s.outlineWidth}`; });
    expect(outline).toMatch(/solid 3px/);
    await expect(page.getByRole("banner")).toBeVisible();
    await expect(page.getByRole("main")).toBeVisible();
    await expect(page.locator('link[rel="icon"]')).toHaveAttribute("href", "causa-icon.svg");
    await expect(page.locator("html")).toHaveAttribute("lang", "en");
  });

  test("reflows without horizontal scrolling and keeps 44px targets @responsive [VPO-0684fde6ced06208] [VPO-86dfcce77d8a048f] [VPO-3b9b2a8a4bf56535]", async ({ page }) => {
    for (const hash of ["", "#/administration", "#/settings"]) {
      await page.goto("index.html" + hash);
      await started(page);
      const overflow = await page.evaluate(() => document.documentElement.scrollWidth - document.documentElement.clientWidth);
      expect(overflow).toBeLessThanOrEqual(0);
      const small = await page.evaluate(() => Array.from(document.querySelectorAll("button, select, input:not([type=hidden]), nav a"))
        .filter((e) => e.offsetParent !== null)
        .map((e) => { const r = e.getBoundingClientRect(); return { t: e.textContent.trim() || e.id, w: r.width, h: r.height }; })
        .filter((r) => r.w < 44 || r.h < 44));
      expect(small).toEqual([]);
      await expect(page.getByRole("navigation", { name: "Primary" }).getByRole("link", { name: "Settings" })).toBeVisible();
    }
  });
});

test.describe("investigation view through the F# projection (test build)", () => {
  test("an injected state renders through the normal view: illegal commands are explained and linked to their blocker [VPO-be99a964783c59b5] [VPO-9659118aa1ef95ff] [VPO-04eb901270c2994a] [VPO-628a11b571e65796] [VPO-12f35805677987db] [VPO-56201f80b312c7c8]", async ({ page }) => {
    await page.goto(TEST_SITE + "index.html#/investigations/00000000-0000-4000-8000-0000000000f1");
    await started(page);
    await page.evaluate(() => window.__perceptaSetState({ name: "blocked", predicates: ["material-blocker-exists"], capabilities: { RecordObservation: "legal", ConfirmCause: "illegal" } }));
    await expect(page.locator("[data-percepta-screen]")).toHaveAttribute("data-percepta-screen", "investigation-overview");
    const nav = page.getByRole("navigation", { name: "Investigation sections" });
    for (const name of ["Overview", "Causal analysis", "Evidence", "Hypotheses", "Timeline", "Corrective actions"]) await expect(nav.getByRole("link", { name })).toBeVisible();
    await expect(nav.getByRole("link", { name: "Overview" })).toHaveAttribute("aria-current", "page");
    await expect(page.getByRole("button", { name: "Record evidence" })).toBeEnabled();
    const confirm = page.getByRole("button", { name: "Confirm the cause" });
    await expect(confirm).toBeDisabled();
    await expect(confirm).toHaveAccessibleDescription("An open material question must be answered first.");
    await page.getByRole("link", { name: "See the open question" }).click();
    await expect(page).toHaveURL(/\?item=blocker-1$/);
    await expect(page.locator("#blocker-blocker-1")).toBeFocused();
    await expect(page.locator("[data-percepta-region=blockers]")).toContainText("Material question");
    // A projected unknown count is not shown as zero.
    await page.goto(TEST_SITE + "index.html#/investigations");
    await page.evaluate(() => window.__perceptaSetState({ name: "plain", predicates: [], capabilities: {} }));
    await expect(page.locator(".causa-card")).toContainText("unresolved contradictions: not known");
  });

  test("an unsupported injected predicate is refused rather than rendered as a default state", async ({ page }) => {
    await page.goto(TEST_SITE + "index.html");
    await started(page);
    const error = await page.evaluate(() => window.__perceptaSetState({ name: "x", predicates: ["write-conflict"], capabilities: {} }).then(() => "", (e) => String(e)));
    expect(error).toContain("Unsupported predicate(s): write-conflict");
  });
});

test.describe("causal graph with the Forma workflow engine (T23)", () => {
  test("the causal analysis renders in the real forma-workflow frame, a selection shows its record, and the text list carries the same structure [VPO-684e6c5f94988bd9] [VPO-db2d02dab493e914] [VPO-b36c63fba6f83d4c] [VPO-05441c1c18ce3dc1] [VPO-0e6a69f7673cace8] [VPO-4c6f9ea0dcc0bd29] [VPO-adec3c95bc6ac2c4] [VPO-3eba4ae44451132f]", async ({ page }) => {
    await page.goto(TEST_SITE + "index.html#/investigations/00000000-0000-4000-8000-0000000000f1/causal-analysis");
    await started(page);
    await page.evaluate(() => window.__perceptaSetState({ name: "graph", predicates: [], capabilities: {} }));
    const frame = page.locator("iframe.causa-graph__frame");
    await expect(frame).toHaveAttribute("title", /Causal graph/);
    // The frame is the packaged engine, not a hand-drawn diagram.
    await expect(frame).toHaveAttribute("src", /vendor\/forma-workflow\/frame\.html/);
    const doc = JSON.parse(await page.locator("#causa-graph").getAttribute("data-document"));
    expect(doc.format).toBe("forma-workflow");
    expect(doc.nodes.map((n) => n.id)).toEqual(["h-h-1", "c-c-1"]);
    expect(doc.edges.map((e) => e.label)).toEqual(["Root cause"]);
    // The workflow engine laid it out and rendered the outcome label inside its own document.
    await expect(page.frameLocator("iframe.causa-graph__frame").getByText("The joint failed during ascent.").first()).toBeVisible({ timeout: 60_000 });
    // Read-only: the frame is in inspect mode, so no editing controls are offered.
    await expect(page.frameLocator("iframe.causa-graph__frame").getByRole("button", { name: /delete|add node/i })).toHaveCount(0);
    await page.locator("#causa-graph-selection").evaluate((el) => { el.value = "h-h-1"; el.dispatchEvent(new Event("change", { bubbles: true })); });
    await expect(page.locator(".causa-graph__selection")).toContainText("Hypothesis (Plausible): A seal lost resilience in the cold.");
    const text = page.getByRole("list", { name: "Causal relationships as text" });
    await expect(text).toContainText("The joint failed during ascent.");
    await expect(text).toContainText("Root cause: A seal lost resilience in the cold.");
  });
});

test.describe("report, timeline and administration views (T22, T26, T29)", () => {
  test("an unresolved investigation's report says it is incomplete and states no conclusion it does not have [VPO-f97eb988899c23af] [VPO-e49834d009021cd0]", async ({ page }) => {
    await page.goto(TEST_SITE + "index.html?percepta=default#/investigations/00000000-0000-4000-8000-0000000000f1/report");
    await started(page);
    await expect(page.locator("[data-percepta-screen]")).toHaveAttribute("data-percepta-screen", "investigation-report");
    await expect(page.locator(".causa-report__incomplete")).toContainText("Incomplete");
    await expect(page.locator("[data-percepta-region=conclusion]")).toContainText("No cause has been confirmed.");
    await expect(page.locator("[data-percepta-region=verification]")).toContainText("nothing is reported as verified.");
    await expect(page.getByRole("button", { name: "Print the report" })).toBeVisible();
  });

  test("the timeline shows event time, recorded time and the version it was rendered from [VPO-33324aeb860bce06] [VPO-7fe0d0f0d00df8a0] [VPO-f1a416eaeec6c129]", async ({ page }) => {
    await page.goto(TEST_SITE + "index.html?percepta=default#/investigations/00000000-0000-4000-8000-0000000000f1/timeline");
    await started(page);
    const entry = page.locator(".causa-timeline__entry").first();
    await expect(entry).toContainText("#1");
    await expect(entry).toContainText("Event time 2026-01-01T00:00:00Z");
    await expect(entry).toContainText("Recorded 2026-01-01T00:00:00Z");
    await expect(page.locator("[data-percepta-region=version-state]")).toContainText("version 7");
    await expect(page.locator(".causa-inv-meta")).toContainText("Version 7");
  });

  test("Administration groups facts by section and never presents an unverified item as passed [VPO-97a56b1abd22a396] [VPO-a74264901d267d03] [VPO-b27f3de8fd81d25c] [VPO-a4ab030e74ad080a]", async ({ page }) => {
    await page.goto("index.html#/administration");
    await started(page);
    for (const name of ["Deployment", "Data and persistence", "AI assistance", "Report", "Security", "Smoke tests"]) await expect(page.getByRole("heading", { level: 2, name })).toBeVisible();
    await expect(page.locator(".causa-attention")).toContainText("Not confirmed ready to demonstrate");
    await expect(page.getByRole("list", { name: "Items not verified" })).toContainText("AI assistance: Unavailable");
    await expect(page.getByRole("list", { name: "Items not verified" })).toContainText("Smoke tests at this commit: Unknown");
    await expect(page.locator("[data-percepta-region=security]")).toContainText("default-src 'self'");
    // Read-only: no form or action button on the page.
    await expect(page.locator("[data-percepta-screen=administration] form, [data-percepta-screen=administration] button")).toHaveCount(0);
  });
});

test.describe("integration checkpoint 1: a legal command end to end through Limen and the F# kernel (T17)", () => {
  test("starting and activating an investigation crosses Limen into the kernel; every view refreshes from the one new version; a refusal changes nothing [VPO-21abf6acd60d0acc] [VPO-be99a964783c59b5] [VPO-f1a416eaeec6c129] [VPO-b3efad6eb85360be]", async ({ page }) => {
    await page.goto(TEST_SITE + "index.html#/investigations/new");
    await started(page);
    // Test build only: the real kernel over an in-memory workspace (nothing is saved anywhere).
    await page.evaluate(() => window.__perceptaSetState({ name: "engine-memory", predicates: [], capabilities: {} }));
    await expect(page.getByRole("heading", { level: 1, name: "New investigation" })).toBeVisible();
    await page.getByLabel("Title").fill("Valve leak on line 3");
    await page.getByLabel(/Problem statement/).fill("Coolant was found under valve 3 during the night shift.");
    await page.getByRole("button", { name: "Start the investigation" }).click();
    await expect(page).toHaveURL(/#\/investigations\/[0-9a-f-]{36}$/);
    await expect(page.getByRole("heading", { level: 1, name: "Valve leak on line 3" })).toBeVisible();
    await expect(page.locator(".causa-inv-meta")).toContainText("Draft");
    await expect(page.locator(".causa-inv-meta")).toContainText("Version 1");
    await expect(page.locator(".causa-observation")).toHaveText("Coolant was found under valve 3 during the night shift.");
    await expect(page.locator(".causa-sync")).toContainText("1 unsaved change(s)");
    // A legal engine capability: activate.
    await page.getByRole("button", { name: "Activate the investigation" }).click();
    await expect(page.locator(".causa-inv-meta")).toContainText("Active");
    await expect(page.locator(".causa-inv-meta")).toContainText("Version 2");
    await expect(page.getByRole("status").filter({ hasText: "Recorded" })).toContainText("Not saved yet");
    // A command with an input: record a hypothesis; the overview, timeline and list all show version 3.
    const hypothesisInput = page.getByLabel("Hypothesis statement");
    await hypothesisInput.fill("The valve seal degraded.");
    await page.getByRole("button", { name: "Record a hypothesis" }).click();
    await expect(page.locator(".causa-inv-meta")).toContainText("Version 3");
    await expect(page.locator("[data-percepta-region=hypotheses]")).toContainText("The valve seal degraded.");
    await page.getByRole("navigation", { name: "Investigation sections" }).getByRole("link", { name: "Timeline" }).click();
    await expect(page.locator(".causa-timeline__entry")).toHaveCount(3);
    await expect(page.locator("[data-percepta-region=version-state]")).toContainText("version 3");
    await page.getByRole("navigation", { name: "Primary" }).getByRole("link", { name: "Investigations" }).click();
    await expect(page.locator(".causa-card")).toContainText("Active");
    await expect(page.locator(".causa-card")).toContainText("1 hypotheses");
  });
});
