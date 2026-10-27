// The Playwright `test` for every non-live browser spec: no request may leave for the real network. GitHub's API is
// answered by the recorded-behaviour fake (fake-github.js); any other non-local host is aborted and fails the test.
// Only @live specs (tests/browser/live-*.spec.js) import @playwright/test directly and reach real services.
import { test as base, expect } from "@playwright/test";
import { respond } from "./fake-github.js";

const local = /^https?:\/\/(127\.0\.0\.1|localhost)(:\d+)?\//;

export const test = base.extend({
  /** Requests the page sent to GitHub (method and URL only; headers carry the token and are never kept). */
  githubRequests: [
    async ({ context }, use) => {
      const seen = [];
      const escaped = [];
      await context.route((url) => !local.test(url.href) && !url.href.startsWith("data:") && !url.href.startsWith("blob:"), async (route) => {
        const request = route.request();
        const url = request.url();
        if (url.startsWith("https://api.github.com/")) {
          seen.push({ method: request.method(), url });
          if (request.method() === "OPTIONS") {
            return route.fulfill({ status: 204, headers: { "access-control-allow-origin": "*", "access-control-allow-headers": "*", "access-control-allow-methods": "GET, POST" } });
          }
          // An owner/name nobody configured: what real GitHub answers for an absent or invisible repository.
          return route.fulfill(respond("ref.read.missingBranch"));
        }
        escaped.push(url);
        return route.abort("blockedbyclient");
      });
      await use(seen);
      expect(escaped, "a mocked browser test tried to reach the real network").toEqual([]);
    },
    { auto: true },
  ],
});

export { expect };
