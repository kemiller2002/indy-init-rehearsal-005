// Builds the deployable static site in dist/: the .NET WebAssembly application, the vendored Limen kernel and
// Forma CSS, the compiled kernel composition root and the page. `--test` builds the test configuration (Percepta
// state hook enabled) into obj/site-test/ instead; the deployed site is never built with it.
// Refuses to emit a page that still imports an unmapped package specifier or that contains a token-shaped string.
import { execFileSync } from "node:child_process";
import { cpSync, existsSync, mkdirSync, readFileSync, readdirSync, rmSync, statSync, writeFileSync } from "node:fs";
import { join, relative } from "node:path";

const root = process.cwd();
const test = process.argv.includes("--test");
const site = join(root, test ? "obj/site-test" : "dist");
rmSync(site, { recursive: true, force: true });
mkdirSync(site, { recursive: true });

// The test configuration is its own .NET build (-p:CausaTestBuild=true): only it exports EnableTestHooks. Its output
// is kept apart from production's (the compiler's input cache sees the changed define and recompiles).
const wasmOutFor = join(root, test ? "obj/site-wasm-test" : "obj/site-wasm");
if (!process.argv.includes("--skip-dotnet") || !existsSync(wasmOutFor)) {
  rmSync(wasmOutFor, { recursive: true, force: true });
  execFileSync("dotnet", ["publish", "src/wasm/Causa.Wasm.csproj", "-c", "Release", "-o", wasmOutFor, "-p:DebugType=none", "-p:DebugSymbols=false",
    `-p:CausaTestBuild=${test}`], { stdio: "inherit" });
}
// GitHub Pages serves no precompressed variants, and symbols are not shipped.
cpSync(join(wasmOutFor, "wwwroot/_framework"), join(site, "wasm/_framework"), { recursive: true, filter: (src) => !/\.(br|gz|pdb)$/.test(src) });

const limen = join(root, "node_modules/@echelon-foundry/limen/dist");
for (const part of ["kernel", "hosts", "protocol.js", "generated", "capability-support"]) {
  const from = join(limen, part);
  if (existsSync(from)) cpSync(from, join(site, "vendor/limen", part), { recursive: true, filter: (src) => !/\.(map|d\.ts)$/.test(src) });
}
mkdirSync(join(site, "vendor/forma"), { recursive: true });
const forma = join(root, "node_modules/@echelon-foundry/design-system/dist");
for (const file of ["all.css", "tokens.css", "foundations.css", "components.css"]) cpSync(join(forma, file), join(site, "vendor/forma", file));

// The causal graph renders in @echelon-foundry/forma-workflow isolated (iframe) mode: its own .NET runtime in its
// own document. Forma's split stylesheets are what the frame loads (?forma=).
cpSync(join(root, "node_modules/@echelon-foundry/forma-workflow/dist"), join(site, "vendor/forma-workflow"), {
  recursive: true,
  filter: (src) => !/\.(br|gz|pdb|map)$/.test(src),
});
mkdirSync(join(site, "vendor/folio"), { recursive: true });
cpSync(join(root, "node_modules/@echelon-foundry/print-components/src/styles/print.css"), join(site, "vendor/folio/print.css"));
cpSync(join(root, "node_modules/@echelon-foundry/print-components/src/components/register.js"), join(site, "vendor/folio/register.js"));
for (const file of ["index.html", "causa.css", "causa-icon.svg"]) cpSync(join(root, "src/kernel", file), join(site, file));

// The kernel composition root is TypeScript; compile it, then point package specifiers at the vendored copies.
const kernelJs = join(root, "obj/site-kernel-js");
execFileSync(process.execPath, [join(root, "node_modules/typescript/bin/tsc"), "-p", "tsconfig.build.json", "--outDir", kernelJs], { stdio: "inherit" });
const specifiers = {
  "@echelon-foundry/limen/kernel": "./vendor/limen/kernel/browser-kernel.js",
  "@echelon-foundry/limen/hosts/dotnet-wasm": "./vendor/limen/hosts/dotnet-wasm-transport.js",
  "@echelon-foundry/print-components": "./vendor/folio/register.js",
};
let main = readFileSync(join(kernelJs, "main.js"), "utf8");
for (const [from, to] of Object.entries(specifiers)) main = main.replaceAll(`"${from}"`, `"${to}"`);
if (main.includes('"@echelon-foundry/')) throw new Error("main.js imports a package specifier that build-site.mjs does not map");
if (test) {
  if (!main.includes("const TEST_BUILD = false;")) throw new Error("main.js no longer declares TEST_BUILD");
  main = main.replace("const TEST_BUILD = false;", "const TEST_BUILD = true;");
}
writeFileSync(join(site, "main.js"), main);
writeFileSync(join(site, ".nojekyll"), "");

const version = (pkg) => JSON.parse(readFileSync(join(root, "node_modules", pkg, "package.json"), "utf8")).version;
const lockVersion = (pkg) => {
  const nuspec = readdirSync(join(root, "vendor/nuget")).find((f) => f.startsWith(pkg + "."));
  return nuspec ? nuspec.slice(pkg.length + 1, -".nupkg".length) : "unknown";
};
// VER-007: the versions of the components actually copied into this site, read from what was installed.
writeFileSync(join(site, "build-info.json"), JSON.stringify({
  commit: process.env.GITHUB_SHA ?? (() => { try { return execFileSync("git", ["rev-parse", "HEAD"], { encoding: "utf8" }).trim(); } catch { return "unknown"; } })(),
  builtAt: new Date().toISOString(),
  configuration: test ? "test" : "production",
  // Read from the page actually shipped, so Administration reports the policy in force rather than a claim.
  "security.contentSecurityPolicy": (readFileSync(join(site, "index.html"), "utf8").match(/http-equiv="Content-Security-Policy" content="([^"]*)"/) ?? [, "none"])[1],
  components: {
    "limen (kernel)": version("@echelon-foundry/limen"),
    "limen-fsharp (contract, guest)": lockVersion("EchelonFoundry.Limen.Contract"),
    "forma (design system)": version("@echelon-foundry/design-system"),
    "forma-workflow (causal graph layout)": version("@echelon-foundry/forma-workflow"),
    "folio (print components)": version("@echelon-foundry/print-components"),
  },
}, null, 1) + "\n");

// ---- Published evidence for Requirements proof (S21) and the factory view (S20) -------------------------------------
// Only values read from files in this checkout (or the CI run that builds it) are published, each with its source, so
// the page shows what is and is not proven without inventing anything. A missing source is published as missing.
const readJson = (path) => { try { return JSON.parse(readFileSync(path, "utf8")); } catch { return null; } };
const traceabilityPath = process.env.CAUSA_TRACEABILITY ?? join(root, "acceptance/traceability.json");
const trace = readJson(traceabilityPath);
const planPath = [process.env.CAUSA_PLAN, join(root, "../indy-init/requirements-compiled/v1/verification-plan.json"), join(root, "indy-init/requirements-compiled/v1/verification-plan.json")].find((p) => p && existsSync(p));
const plan = planPath ? readJson(planPath) : null;
const planReq = new Map((plan?.requirements ?? []).map((r) => [r.requirement, r]));
const commit = process.env.GITHUB_SHA ?? (() => { try { return execFileSync("git", ["rev-parse", "HEAD"], { encoding: "utf8" }).trim(); } catch { return null; } })();
const run = process.env.GITHUB_RUN_ID ? { id: process.env.GITHUB_RUN_ID, url: `${process.env.GITHUB_SERVER_URL ?? "https://github.com"}/${process.env.GITHUB_REPOSITORY ?? ""}/actions/runs/${process.env.GITHUB_RUN_ID}` } : null;
const byRequirement = new Map();
for (const o of trace?.obligations ?? []) {
  if (!byRequirement.has(o.requirement)) byRequirement.set(o.requirement, []);
  byRequirement.get(o.requirement).push({ id: o.id, source: o.sourceObligation, text: o.text, binding: o.binding, verification: o.verification,
    tests: (o.tests ?? []).map((t) => ({ name: t.name, suite: t.suite, outcome: t.outcome })) });
}
mkdirSync(join(site, "evidence"), { recursive: true });
writeFileSync(join(site, "evidence/proof.json"), JSON.stringify({
  schema: "causa.requirements-proof/1",
  commit,
  run,
  // The run and commit the traceability was produced from (often an earlier main run), kept apart from this build's.
  traceability: trace ? { source: relative(root, traceabilityPath), schema: trace.schema, generatedBy: trace.context?.generatedBy ?? null, run: trace.context?.runId ?? null, sourceSha: trace.context?.sourceSha ?? null, plan: trace.plan ?? null, summary: trace.summary ?? null } : null,
  planSource: planPath ? relative(root, planPath) : null,
  requirements: [...byRequirement.entries()].map(([id, obligations]) => {
    const r = planReq.get(id);
    return { id, title: r?.title ?? null, priority: r?.priority ?? null, document: r?.document ?? null, sectionSha256: r?.sectionSha256 ?? null, obligations };
  }),
}) + "\n");

const queue = readJson(join(root, ".ros/work/queue.json"));
const eventsFile = join(root, ".ros/events/events.jsonl");
const praxis = readJson(join(root, ".praxis/instance.json"));
const aegis = readJson(join(root, "aegis-boundaries.json"));
const executions = existsSync(join(root, ".ros/executions")) ? readdirSync(join(root, ".ros/executions")).length : null;
writeFileSync(join(site, "evidence/factory.json"), JSON.stringify({
  schema: "causa.factory/1",
  commit,
  run,
  ros: queue ? {
    source: ".ros/work/queue.json",
    items: queue.items.map((i) => ({ id: i.id, title: i.title, status: i.status, priority: i.priority ?? null, updatedAt: i.updatedAt ?? null })),
    events: existsSync(eventsFile) ? readFileSync(eventsFile, "utf8").split("\n").filter((l) => l.trim() !== "").length : null,
    executions,
  } : null,
  praxis: praxis ? { source: ".praxis/instance.json", version: praxis.createdWith ?? null, instanceId: praxis.instanceId ?? null, repository: praxis.repository?.repository ?? null } : null,
  aegis: aegis ? { source: "aegis-boundaries.json", boundaries: Array.isArray(aegis.boundaries) ? aegis.boundaries.length : Object.keys(aegis.boundaries ?? {}).length } : null,
}) + "\n");

// No credential may ship. .wasm and .dll files carry string literals as UTF-16LE, so scan both encodings.
const pattern = /(gh[pousr]_[A-Za-z0-9]{36,}|github_pat_[A-Za-z0-9_]{22,})/;
const walk = (dir) => readdirSync(dir).flatMap((name) => {
  const path = join(dir, name);
  return statSync(path).isDirectory() ? walk(path) : [path];
});
for (const file of walk(site)) {
  const bytes = readFileSync(file);
  if (pattern.test(bytes.toString("latin1")) || pattern.test(bytes.toString("utf16le"))) throw new Error(`token-shaped string in ${relative(root, file)}`);
}
// The production .NET build must not carry the test-only export (main.js names it only in code that the production
// constant TEST_BUILD = false never runs).
if (!test) {
  for (const file of walk(join(site, "wasm"))) {
    const bytes = readFileSync(file);
    if (bytes.toString("latin1").includes("EnableTestHooks") || bytes.toString("utf16le").includes("EnableTestHooks")) throw new Error(`test-only export in production file ${relative(root, file)}`);
  }
}
console.log(`built ${relative(root, site)} (${walk(site).length} files)`);
