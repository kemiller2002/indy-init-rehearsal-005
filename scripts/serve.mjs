// A minimal static file server for the browser tests (no Python in the execution path).
import { createServer } from "node:http";
import { readFile } from "node:fs/promises";
import { extname, join, normalize } from "node:path";

const root = process.argv[2] ?? "dist/site";
const port = Number(process.argv[3] ?? 4321);
const types = { ".html": "text/html", ".js": "text/javascript", ".mjs": "text/javascript", ".css": "text/css", ".json": "application/json", ".wasm": "application/wasm", ".dat": "application/octet-stream", ".png": "image/png", ".svg": "image/svg+xml" };

// A request the server cannot decode is answered 400; it must never take the server down, or every later
// test on this port fails with "connection refused".
const decodedPath = (url) => {
  try { return decodeURIComponent(new URL(url ?? "/", "http://x").pathname); } catch { return undefined; }
};

createServer(async (request, response) => {
  const path = decodedPath(request.url);
  if (path === undefined) { response.writeHead(400).end(); return; }
  const file = normalize(join(root, path.endsWith("/") ? path + "index.html" : path));
  if (!file.startsWith(normalize(root))) { response.writeHead(403).end(); return; }
  try {
    const body = await readFile(file);
    response.writeHead(200, { "content-type": types[extname(file)] ?? "application/octet-stream", "cache-control": "no-store" }).end(body);
  } catch {
    response.writeHead(404).end();
  }
}).listen(port, "127.0.0.1", () => console.log(`serving ${root} on http://127.0.0.1:${port}`));
