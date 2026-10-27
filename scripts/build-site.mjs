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
const wasmOut = join(root, "obj/site-wasm");
rmSync(site, { recursive: true, force: true });
mkdirSync(site, { recursive: true });

if (!process.argv.includes("--skip-dotnet") || !existsSync(wasmOut)) {
  rmSync(wasmOut, { recursive: true, force: true });
  execFileSync("dotnet", ["publish", "src/wasm/Causa.Wasm.csproj", "-c", "Release", "-o", wasmOut, "-p:DebugType=none", "-p:DebugSymbols=false"], { stdio: "inherit" });
}

// GitHub Pages serves no precompressed variants, and symbols are not shipped.
cpSync(join(wasmOut, "wwwroot/_framework"), join(site, "wasm/_framework"), { recursive: true, filter: (src) => !/\.(br|gz|pdb)$/.test(src) });

const limen = join(root, "node_modules/@echelon-foundry/limen/dist");
for (const part of ["kernel", "hosts", "protocol.js", "generated", "capability-support"]) {
  const from = join(limen, part);
  if (existsSync(from)) cpSync(from, join(site, "vendor/limen", part), { recursive: true, filter: (src) => !/\.(map|d\.ts)$/.test(src) });
}
mkdirSync(join(site, "vendor/forma"), { recursive: true });
const forma = join(root, "node_modules/@echelon-foundry/design-system/dist");
for (const file of ["all.css", "tokens.css", "foundations.css", "components.css"]) cpSync(join(forma, file), join(site, "vendor/forma", file));

for (const file of ["index.html", "causa.css", "causa-icon.svg"]) cpSync(join(root, "src/kernel", file), join(site, file));

// The kernel composition root is TypeScript; compile it, then point package specifiers at the vendored copies.
const kernelJs = join(root, "obj/site-kernel-js");
execFileSync(process.execPath, [join(root, "node_modules/typescript/bin/tsc"), "-p", "tsconfig.build.json", "--outDir", kernelJs], { stdio: "inherit" });
const specifiers = {
  "@echelon-foundry/limen/kernel": "./vendor/limen/kernel/browser-kernel.js",
  "@echelon-foundry/limen/hosts/dotnet-wasm": "./vendor/limen/hosts/dotnet-wasm-transport.js",
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
  components: {
    "limen (kernel)": version("@echelon-foundry/limen"),
    "limen-fsharp (contract, guest)": lockVersion("EchelonFoundry.Limen.Contract"),
    "forma (design system)": version("@echelon-foundry/design-system"),
  },
}, null, 1) + "\n");

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
console.log(`built ${relative(root, site)} (${walk(site).length} files)`);
