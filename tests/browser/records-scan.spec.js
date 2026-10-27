// EXT-DATA-001-O1 / EXT-DATA-013-O3: no investigation record (historical catalog, frozen fixtures, workspaces) is in
// the application source, the built bundle or the Pages output. Needs a causa-data checkout: CAUSA_DATA_DIR, or
// causa-data/ beside or inside this repository (CI checks it out into ./causa-data).
import { existsSync } from "node:fs";
import { expect, test } from "./support/fixtures.js";
import { scan } from "../../scripts/scan-records.mjs";

const causaData = [process.env.CAUSA_DATA_DIR, "causa-data", "../causa-data"].find((d) => d && existsSync(d));

test("no causa-data investigation record is in the source, the test build or the deployable site [VPO-f8cbaa452cd4eb0b] [VPO-77f6e22e08d5bba0]", async () => {
  expect(causaData, "a causa-data checkout is required (CAUSA_DATA_DIR)").toBeTruthy();
  const dirs = ["src", "dist", "obj/site-test", ".percepta"].filter((d) => existsSync(d));
  expect(dirs).toContain("dist");
  const result = scan(causaData, dirs);
  expect(result.ids).toBeGreaterThanOrEqual(3);
  expect(result.files).toBeGreaterThan(100);
  expect(result.findings).toEqual([]);
});

test("the scan finds a planted record id (negative control)", async () => {
  expect(causaData).toBeTruthy();
  const { recordIds } = await import("../../scripts/scan-records.mjs");
  const { mkdtempSync, writeFileSync } = await import("node:fs");
  const { join } = await import("node:path");
  const { tmpdir } = await import("node:os");
  const dir = mkdtempSync(join(tmpdir(), "causa-scan-"));
  const id = recordIds(causaData)[0];
  writeFileSync(join(dir, "bundle.js"), `const x = "${id}";`);
  expect(scan(causaData, [dir]).findings).toHaveLength(1);
});
