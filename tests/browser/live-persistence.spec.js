// @live: the real write path in a real browser against the real GitHub test repository (T21, DCT-LIVE-A).
//
// Needs CAUSA_GITHUB_TOKEN and CAUSA_TEST_REPO (the live job passes them). A missing secret fails the test as
// BLOCKED; it is never skipped and never passes. The token is typed into the Connect form exactly as a person would,
// is never printed, never written to a file, and traces, screenshots and video are off for this file. Every claim
// the page makes is checked by an independent read of GitHub from this test process (SHA re-reads).
import { expect, test } from "@playwright/test";

const token = process.env.CAUSA_GITHUB_TOKEN ?? "";
const testRepo = process.env.CAUSA_TEST_REPO ?? "";
const catalogRepo = "kemiller2002/causa-data";
const api = "https://api.github.com";

test.use({ trace: "off", screenshot: "off", video: "off" });

function blockedUnlessConfigured() {
  if (!token || !testRepo) throw new Error("BLOCKED: CAUSA_GITHUB_TOKEN / CAUSA_TEST_REPO are not available (a missing secret is never a pass)");
}

/** An independent GitHub read from the test process. Mutable reads carry a unique query value. */
async function gh(path, init = {}) {
  const sep = path.includes("?") ? "&" : "?";
  const url = `${api}${path}${sep}live_check=${Date.now()}-${Math.random().toString(36).slice(2)}`;
  const response = await fetch(url, {
    ...init,
    headers: { Authorization: `Bearer ${token}`, Accept: "application/vnd.github+json", "X-GitHub-Api-Version": "2022-11-28", ...(init.headers ?? {}) },
  });
  const body = await response.text();
  return { status: response.status, json: body ? JSON.parse(body) : null };
}

async function defaultBranch(repo) {
  const r = await gh(`/repos/${repo}`);
  expect(r.status).toBe(200);
  return r.json.default_branch;
}

async function head(repo, branch) {
  const r = await gh(`/repos/${repo}/git/ref/heads/${branch}`);
  expect(r.status).toBe(200);
  return r.json.object.sha;
}

async function commit(repo, sha) {
  const r = await gh(`/repos/${repo}/git/commits/${sha}`);
  expect(r.status).toBe(200);
  return r.json;
}

/** The investigation's files at a commit, independently: name -> text. */
async function filesAt(repo, dir, sha) {
  const listing = await gh(`/repos/${repo}/contents/${dir}?ref=${sha}`);
  if (listing.status !== 200) return null;
  const out = {};
  for (const entry of listing.json) {
    const blob = await gh(`/repos/${repo}/git/blobs/${entry.sha}`);
    out[entry.name] = Buffer.from(blob.json.content, "base64").toString("utf8");
  }
  return out;
}

const eventLines = (files) => files["events.ndjson"].split("\n").filter((l) => l.length > 0);

/** Another writer: one commit to the branch, outside the investigation's files. */
async function otherWriter(repo, branch, expectedHead, path) {
  const query = "mutation($input: CreateCommitOnBranchInput!) { createCommitOnBranch(input: $input) { commit { oid } } }";
  const input = {
    branch: { repositoryNameWithOwner: repo, branchName: branch },
    message: { headline: "live check: another writer" },
    expectedHeadOid: expectedHead,
    fileChanges: { additions: [{ path, contents: Buffer.from(`another writer ${Date.now()}\n`).toString("base64") }] },
  };
  const r = await fetch(`${api}/graphql`, {
    method: "POST",
    headers: { Authorization: `Bearer ${token}`, "Content-Type": "application/json" },
    body: JSON.stringify({ query, variables: { input } }),
  });
  const json = await r.json();
  expect(json.errors ?? []).toEqual([]);
  return json.data.createCommitOnBranch.commit.oid;
}

async function started(page) {
  await expect(page.locator("#causa-boot")).toHaveCount(0, { timeout: 90_000 });
}

async function connect(page, repo, branch, workspace, credential = token) {
  const main = page.getByRole("main");
  await main.getByLabel("Fine-grained GitHub token").fill(credential);
  await main.getByLabel("Repository (owner/name)").fill(repo);
  await main.getByLabel("Branch").fill(branch);
  await main.getByLabel("Workspace").fill(workspace);
  await main.getByRole("button", { name: "Connect" }).click();
}

/** Every request the page sends to GitHub: method and URL (never headers, which carry the token). */
function recordRequests(page, into) {
  page.on("request", (r) => {
    if (r.url().startsWith(api)) into.push({ method: r.method(), url: r.url() });
  });
}

const syncStatus = (page) => page.locator(".causa-sync [role=status]");

test(
  "DCT-LIVE-A create, write, reload, conflict and rejected token against the real test repository, each confirmed by an independent SHA re-read",
  { tag: ["@live", "@VPO-05eeddad528e2cd4", "@VPO-c6c90d9f4acc5fe6", "@VPO-37d860d18826cf0a", "@VPO-d87bf2493f9268ae", "@VPO-6292ea3728a2532b", "@VPO-7672a3ef81f74ac5", "@VPO-903077d337faf198", "@VPO-d7fbd84e40ded145", "@VPO-61b2e81f99761d55", "@VPO-b3efad6eb85360be", "@VPO-34731ac9a977b818", "@VPO-89ed6170f531961e", "@VPO-4c06d10238d15c5c", "@VPO-54b0eef91ceeada4", "@VPO-2de744fb2c8bdc70", "@VPO-bb08b9a414ba4cd7", "@VPO-ca58030e3eecdf9b", "@VPO-c135d16c59f5baf8", "@VPO-57fe23e7c821450f", "@VPO-fbcff0eece408886", "@VPO-5581f11810c02191"] },
  async ({ page, browser }) => {
    test.setTimeout(300_000);
    blockedUnlessConfigured();
    const branch = await defaultBranch(testRepo);
    const workspace = `live-${Date.now()}`;
    const requests = [];
    recordRequests(page, requests);
    const timings = {};

    // Connect: an empty workspace; never "connected" because a token was typed.
    await page.goto("index.html");
    await started(page);
    await connect(page, testRepo, branch, workspace);
    await expect(page.locator(".causa-connection")).toContainText(testRepo, { timeout: 60_000 });
    const headAtConnect = await head(testRepo, branch);

    // Create: the person starts an investigation; the program saves it and claims "saved" only after the re-read.
    let t0 = Date.now();
    await page.goto("index.html#/investigations/new");
    const title = `Live write path ${workspace}`;
    await page.getByLabel("Title").fill(title);
    await page.getByLabel(/Problem statement/).fill("The live check observed that the write path must be proven against GitHub.");
    await page.locator('button[data-percepta-capability="StartInvestigation"]').first().click();
    await expect(page).toHaveURL(/#\/investigations\/[0-9a-f-]{36}/, { timeout: 60_000 });
    const id = page.url().match(/#\/investigations\/([0-9a-f-]{36})/)[1];
    await expect(syncStatus(page)).toContainText("Saved to GitHub", { timeout: 90_000 });
    timings.firstVerifiedWriteMs = Date.now() - t0;
    const dir = `workspaces/demo/${workspace}/investigations/${id}`;
    const afterCreate = await head(testRepo, branch);
    expect(afterCreate).not.toBe(headAtConnect);
    const created = await filesAt(testRepo, dir, afterCreate);
    expect(Object.keys(created).sort()).toEqual(["events.ndjson", "manifest.json", "snapshot.json"]);
    const createdManifest = JSON.parse(created["manifest.json"]);
    expect(createdManifest.aggregateVersion).toBe(eventLines(created).length);
    expect(created["events.ndjson"]).toContain(title);
    const createCommit = await commit(testRepo, afterCreate);
    expect(createCommit.parents.map((p) => p.sha)).toEqual([headAtConnect]); // parented by the head it was decided at
    expect(createCommit.message).toContain("Causa-Marker: ");

    // Write: record an observation; saved, and the remote history grows by exactly that command.
    const observation = page.locator('input[aria-labelledby="input-label-RecordObservation"]');
    await observation.fill("A second observation, written through the browser.");
    await page.locator('button[data-percepta-capability="RecordObservation"]').first().click();
    await expect(syncStatus(page)).toContainText("Saved to GitHub", { timeout: 90_000 });
    const afterWrite = await head(testRepo, branch);
    const written = await filesAt(testRepo, dir, afterWrite);
    expect(eventLines(written).length).toBeGreaterThan(eventLines(created).length);
    expect(written["events.ndjson"].startsWith(created["events.ndjson"])).toBe(true); // no rewrite of history
    expect((await commit(testRepo, afterWrite)).parents.map((p) => p.sha)).toEqual([afterCreate]);

    // Reload: memory (and the token) is gone; the token must be entered again; the investigation comes back.
    await page.reload();
    await started(page);
    await expect(page.getByRole("main").getByLabel("Fine-grained GitHub token")).toHaveValue("");
    await connect(page, testRepo, branch, workspace);
    await expect(page.locator(".causa-connection")).toContainText(testRepo, { timeout: 60_000 });
    await page.goto(`index.html#/investigations/${id}`);
    await expect(page.getByRole("heading", { level: 1, name: title })).toBeVisible({ timeout: 60_000 });
    await expect(syncStatus(page)).not.toContainText("unsaved");

    // Conflict: another writer moves the branch; the page's next save is refused, nothing is overwritten, the
    // change stays on this device.
    t0 = Date.now();
    const moved = await otherWriter(testRepo, branch, afterWrite, `workspaces/demo/${workspace}-other-writer/note.txt`);
    await observation.fill("A change decided before the other writer's commit.");
    await page.locator('button[data-percepta-capability="RecordObservation"]').first().click();
    await expect(syncStatus(page)).toContainText("someone else changed the repository", { timeout: 90_000 });
    timings.conflictHandledMs = Date.now() - t0;
    expect(await head(testRepo, branch)).toBe(moved);
    const afterConflict = await filesAt(testRepo, dir, moved);
    expect(afterConflict["events.ndjson"]).toBe(written["events.ndjson"]);

    // Rejected token: a second tab with a wrong token is refused and writes nothing.
    const other = await browser.newPage();
    const otherRequests = [];
    recordRequests(other, otherRequests);
    await other.goto("index.html");
    await started(other);
    await connect(other, testRepo, branch, workspace, "causa-live-check-rejected-token");
    await expect(other.getByRole("alert")).toContainText("rejected the credential", { timeout: 60_000 });
    await expect(other.locator(".causa-connection")).toContainText("Not connected");
    expect(otherRequests.filter((r) => r.method !== "GET")).toEqual([]);
    expect(await head(testRepo, branch)).toBe(moved);

    // DC-005 in the browser: every mutable read (branch head, repository) has a URL never used before, across the
    // reload and the second tab.
    const mutable = [...requests, ...otherRequests].filter((r) => r.method === "GET" && (/\/git\/ref\/heads\//.test(r.url) || /\/repos\/[^/]+\/[^/?]+\?/.test(r.url)));
    expect(mutable.length).toBeGreaterThan(3);
    expect(mutable.every((r) => r.url.includes("causa_fresh="))).toBe(true);
    expect(new Set(mutable.map((r) => r.url)).size).toBe(mutable.length);

    // The token is nowhere but the Authorization header: not in a URL, the page, or browser storage.
    for (const r of [...requests, ...otherRequests]) expect(r.url.includes(token)).toBe(false);
    for (const p of [page, other]) {
      expect((await p.content()).includes(token)).toBe(false);
      const stored = await p.evaluate(() => JSON.stringify({ ...localStorage }) + JSON.stringify({ ...sessionStorage }));
      expect(stored.includes(token)).toBe(false);
    }
    await other.close();

    // Evidence (PF-014-O3): repository-neutral ids only; never the token.
    test.info().annotations.push({ type: "live-evidence", description: JSON.stringify({ workspace, investigation: id, headAtConnect, afterCreate, afterWrite, moved, ...timings }) });
  },
);

test(
  "EXT-DATA-013 after Connect the browser loads exactly the catalog's three historical cases, verified and read-only",
  { tag: ["@live", "@VPO-c81484cbcf203d4f", "@VPO-4b4747eaff96e385", "@VPO-d60e3a3a25cb106d", "@VPO-dc48d066cfa2309d", "@VPO-acd13bfa08dba51a", "@VPO-557743cb4997c484", "@VPO-c81b5785b2ffa1c2", "@VPO-903077d337faf198"] },
  async ({ page }) => {
    test.setTimeout(300_000);
    blockedUnlessConfigured();
    // The expected cases come from the data repository itself, at run time (nothing bundled, no hardcoded ids).
    const pointer = await gh(`/repos/${catalogRepo}/contents/catalog/active.json`);
    const active = JSON.parse(Buffer.from(pointer.json.content, "base64").toString("utf8"));
    const manifestFile = await gh(`/repos/${catalogRepo}/contents/${active.manifest}`);
    const manifest = JSON.parse(Buffer.from(manifestFile.json.content, "base64").toString("utf8"));
    expect(manifest.cases.length).toBe(3);
    const branch = await defaultBranch(catalogRepo);
    const writes = [];
    page.on("request", (r) => { if (r.url().startsWith(api) && r.method() !== "GET") writes.push(r.url()); });

    await page.goto("index.html");
    await started(page);
    await connect(page, catalogRepo, branch, active.workspace.replace("workspaces/demo/", ""));
    await expect(page.locator(".causa-connection")).toContainText(catalogRepo, { timeout: 120_000 });
    await page.goto("index.html#/investigations");
    const cards = page.locator(".causa-card");
    await expect(cards).toHaveCount(3, { timeout: 120_000 });
    for (const c of manifest.cases) await expect(page.locator(".causa-card__title", { hasText: c.title })).toHaveCount(1);
    expect(writes).toEqual([]);
  },
);
