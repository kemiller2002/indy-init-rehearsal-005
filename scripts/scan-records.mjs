// EXT-DATA-001 / EXT-DATA-013: proves that no investigation record from causa-data (the historical catalog, the
// frozen fixtures or any workspace) is bundled in the given directories. Every investigation id under any
// `investigations/` directory of the causa-data checkout is searched for in every file (as Latin-1 and UTF-16LE, so
// strings inside .wasm/.dll are covered), and no events.ndjson / snapshot.json may be present.
// Usage: node scripts/scan-records.mjs <causa-data dir> <dir> [<dir> ...]   (exit 1 on a finding)
import { existsSync, readdirSync, readFileSync, statSync } from "node:fs";
import { basename, join, relative } from "node:path";

const walk = (dir, skip = new Set([".git", "node_modules", "bin", "obj"])) =>
  readdirSync(dir).flatMap((name) => {
    if (skip.has(name)) return [];
    const path = join(dir, name);
    return statSync(path).isDirectory() ? walk(path, skip) : [path];
  });

export function recordIds(causaData) {
  const ids = new Set();
  const visit = (dir) => {
    for (const name of readdirSync(dir)) {
      const path = join(dir, name);
      if (!statSync(path).isDirectory() || name === ".git") continue;
      if (basename(dir) === "investigations" && /^[0-9a-f-]{36}$/.test(name)) ids.add(name);
      visit(path);
    }
  };
  visit(causaData);
  return [...ids].sort();
}

export function scan(causaData, dirs) {
  const ids = recordIds(causaData);
  const findings = [];
  let files = 0;
  for (const dir of dirs) {
    for (const file of walk(dir)) {
      files++;
      const name = basename(file);
      if (name === "events.ndjson" || name === "snapshot.json") findings.push(`${relative(process.cwd(), file)}: storage-contract record file`);
      const bytes = readFileSync(file);
      const text = bytes.toString("latin1") + "\n" + bytes.toString("utf16le");
      for (const id of ids) if (text.includes(id)) findings.push(`${relative(process.cwd(), file)}: investigation id ${id}`);
    }
  }
  return { ids: ids.length, files, findings };
}

if (process.argv[1] && process.argv[1].endsWith("scan-records.mjs")) {
  const [causaData, ...dirs] = process.argv.slice(2);
  if (!causaData || !existsSync(causaData) || dirs.length === 0) {
    console.error("usage: node scripts/scan-records.mjs <causa-data dir> <dir> [<dir> ...]");
    process.exit(2);
  }
  const result = scan(causaData, dirs);
  console.log(JSON.stringify({ schema: "causa.record-scan/1", ...result }, null, 1));
  process.exit(result.findings.length === 0 && result.ids > 0 ? 0 : 1);
}
