// A fake GitHub for mocked browser tests, served only from the sanitized recordings of real GitHub behaviour
// (tests/Persistence.Tests/recordings/*.ndjson, T18). It never invents a status or a response form: a request it has
// no recording for fails the test. Mocked suites must not reach the network (PF-005-O1, PF-012-O2).
import { readdirSync, readFileSync } from "node:fs";
import { join } from "node:path";

const dir = new URL("../../Persistence.Tests/recordings/", import.meta.url).pathname;

/** scenario -> the latest recorded observation (status, headers, body shape). */
export const recordings = (() => {
  const out = new Map();
  for (const file of readdirSync(dir).filter((f) => f.endsWith(".ndjson")).sort()) {
    for (const line of readFileSync(join(dir, file), "utf8").split("\n")) {
      if (!line.startsWith("{")) continue;
      const o = JSON.parse(line);
      if (o.scenario && typeof o.status === "number") out.set(o.scenario, o);
    }
  }
  return out;
})();

/** A body of exactly the recorded shape, with the values a conversation needs overlaid. */
function instantiate(shape, overlay) {
  if (Array.isArray(shape)) return Array.isArray(overlay) && shape.length ? overlay.map((x) => instantiate(shape[0], x)) : [];
  if (shape && typeof shape === "object") {
    const o = {};
    for (const [k, s] of Object.entries(shape)) o[k] = instantiate(s, overlay && typeof overlay === "object" ? overlay[k] : undefined);
    return o;
  }
  if (overlay !== undefined) return overlay;
  return { string: "", integer: 0, boolean: false, null: null }[shape] ?? null;
}

export function respond(scenario, overlay) {
  const o = recordings.get(scenario);
  if (!o) throw new Error(`no recording for scenario ${scenario}: the fake does not invent provider behaviour`);
  return { status: o.status, headers: { ...o.headers, "access-control-allow-origin": "*" }, body: JSON.stringify(instantiate(o.bodyShape, overlay)) };
}

/**
 * Serve api.github.com for a page from recordings. With no repositories configured, every repository is absent or
 * invisible to the credential (the recorded 404), which is what an unknown owner/name gets from real GitHub.
 * Returns the list of requests seen (method and URL; headers are not kept: they carry the token).
 */
export async function serveRecordedGitHub(page) {
  const seen = [];
  await page.route("https://api.github.com/**", async (route) => {
    const request = route.request();
    seen.push({ method: request.method(), url: request.url() });
    if (request.method() === "OPTIONS") return route.fulfill({ status: 204, headers: { "access-control-allow-origin": "*", "access-control-allow-headers": "*", "access-control-allow-methods": "GET, POST" } });
    return route.fulfill(respond("ref.read.missingBranch"));
  });
  return seen;
}
