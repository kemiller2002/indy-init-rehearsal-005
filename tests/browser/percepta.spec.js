// Percepta screen contracts against the served test build: runs `percepta verify-app` with the committed bindings
// (.percepta/bindings.json) and requires every bound screen to PASS. Unbound contracts stay NOT-BOUND in the report
// (never counted as passed here). Requires the percepta CLI (0.3.0) on PATH.
import { execFileSync } from "node:child_process";
import { mkdirSync, readFileSync } from "node:fs";
import { expect, test } from "./support/fixtures.js";

test("every bound screen satisfies its Percepta contract (verify-app) [VPO-59c5cc78868b7d74] [VPO-447fd64210e3d9e3] [VPO-0684fde6ced06208] [VPO-9659118aa1ef95ff]", async () => {
  test.setTimeout(600_000);
  mkdirSync("obj", { recursive: true });
  const out = "obj/percepta-application-verification.json";
  try {
    execFileSync("percepta", ["verify-app", "--base-url", `http://127.0.0.1:${process.env.CAUSA_TEST_PORT ?? 4322}/`, "--bindings", ".percepta/bindings.json", "--contracts", ".percepta/contracts", "--out", out], { stdio: "pipe", timeout: 580_000 });
  } catch (error) {
    // Exit 3 means "incomplete" (unbound contracts remain); the per-screen verdicts below decide this test.
    if (error.status !== 3) throw error;
  }
  const report = JSON.parse(readFileSync(out, "utf8"));
  const bindings = JSON.parse(readFileSync(".percepta/bindings.json", "utf8"));
  const bound = bindings.screens.filter((s) => !s.notApplicable).map((s) => s.contract);
  expect(bound.length).toBeGreaterThan(0);
  const verdicts = Object.fromEntries(report.screens.map((s) => [s.contract, s.verdict]));
  for (const contract of bound) expect.soft(verdicts[contract], `${contract}: ${report.screens.find((s) => s.contract === contract)?.reason}`).toBe("PASS");
  expect(report.orphanBindings ?? []).toEqual([]);
});
