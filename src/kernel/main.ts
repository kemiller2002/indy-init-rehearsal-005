// Composition root: hands the .NET WebAssembly application to the Limen kernel. Browser mechanism only; no
// application decision is made here. Additions: the loading experience with a bounded start (WASM-LOAD-001..006,
// DC-006) and, in the test build only, the Percepta state hook.
import { BrowserKernel } from "@echelon-foundry/limen/kernel";
import { DotnetWasmTransport } from "@echelon-foundry/limen/hosts/dotnet-wasm";
// Folio's print elements (passive: registering the element names is all it does).
import "@echelon-foundry/print-components";

/** Replaced with `true` by scripts/build-site.mjs --test. Never true in the deployed site. */
const TEST_BUILD = false;
/** Bounded start: a runtime that never answers ends in an explained failure, never an endless spinner. */
const START_TIMEOUT_MS = 60_000;

const boot = document.getElementById("causa-boot");
const bootLabel = document.getElementById("causa-boot-label");
const bootDetail = document.getElementById("causa-boot-detail");
const ready = document.getElementById("causa-ready");

type DiagnosticEntry = { kind: string; detail?: string };
const diagnostics: DiagnosticEntry[] = [];
// Technical causes go to diagnostics only, never into the page.
Reflect.set(window, "causaDiagnostics", diagnostics);

function retryButton(label: string): HTMLButtonElement {
  const retry = document.createElement("button");
  retry.type = "button";
  retry.className = "ef-button";
  retry.dataset.efVariant = "primary";
  retry.textContent = label;
  // Retry reattempts initialization by reloading the page: nothing was saved or sent before start.
  retry.addEventListener("click", () => window.location.reload());
  return retry;
}

/** After start, an internal failure must not leave a frozen page without a word. */
function stoppedAfterStart(): void {
  if (document.getElementById("causa-stopped") !== null) return;
  const banner = document.createElement("div");
  banner.id = "causa-stopped";
  banner.className = "ef-alert causa-notice causa-stopped";
  banner.dataset.tone = "danger";
  banner.setAttribute("role", "alert");
  const text = document.createElement("p");
  text.textContent = "Causa stopped working because of an internal error. Anything not yet saved to GitHub cannot be saved from this page. Reload the page to start again.";
  const retry = retryButton("Reload the page");
  banner.append(text, retry);
  document.body.prepend(banner);
  retry.focus();
}

function startFailed(message: string, cause: unknown): void {
  diagnostics.push({ kind: "StartFailed", detail: String(cause) });
  if (boot === null || !boot.isConnected) {
    stoppedAfterStart();
    return;
  }
  if (bootLabel === null || boot.dataset.state === "failed") return;
  boot.setAttribute("role", "alert");
  boot.dataset.state = "failed";
  bootLabel.textContent = message;
  if (bootDetail !== null) bootDetail.textContent = "Nothing was saved or sent. Retry starts Causa again.";
  const retry = retryButton("Retry");
  boot.appendChild(retry);
  retry.focus();
}

// A framework file that fails to download rejects inside the runtime loader and may never settle the start promise.
window.addEventListener("unhandledrejection", (event) => {
  startFailed("Causa could not start: part of the application failed to load.", event.reason);
});

// .NET 10.0.14 Mono interpreter: its SSA and basic-block optimizer passes trap with "memory access out of bounds"
// once about a thousand events are replayed (R4 D-HIST-WASM). Both passes are off from the first build.
const INTERPRETER_OPTIONS = "-ssa,-bblocks";

type RuntimeBuilder = {
  withEnvironmentVariable(name: string, value: string): RuntimeBuilder;
  withRuntimeOptions(options: string[]): RuntimeBuilder;
  withDiagnosticTracing(enabled: boolean): { create(): Promise<Runtime> };
};
type Runtime = { getAssemblyExports(assembly: string): Promise<Record<string, Record<string, (...args: unknown[]) => unknown>>> };

/** Test build only: once the exports are known, enable the application's Percepta state event. */
const withTestHooks = (builder: RuntimeBuilder) => ({
  withDiagnosticTracing: (enabled: boolean) => ({
    create: async () => {
      const runtime = await builder.withDiagnosticTracing(enabled).create();
      const getExports = runtime.getAssemblyExports.bind(runtime);
      runtime.getAssemblyExports = async (assembly: string) => {
        const exports = await getExports(assembly);
        exports.CausaEngine?.EnableTestHooks?.();
        return exports;
      };
      return runtime;
    },
  }),
});

const transport = new DotnetWasmTransport({
  loadRuntime: async () => {
    const runtime = await import(new URL("./wasm/_framework/dotnet.js", import.meta.url).href);
    const builder: RuntimeBuilder = runtime.dotnet
      .withEnvironmentVariable("MONO_INTERPRETER_OPTIONS", INTERPRETER_OPTIONS)
      .withRuntimeOptions(["--no-jiterpreter-traces-enabled", "--no-jiterpreter-interp-entry-enabled", "--no-jiterpreter-jit-call-enabled"]);
    return { dotnet: TEST_BUILD ? withTestHooks(builder) : builder };
  },
  exportPath: ["CausaEngine", "Dispatch"],
});

const kernel = new BrowserKernel(
  transport,
  document,
  {
    report: (entry: DiagnosticEntry) => {
      diagnostics.push(entry);
      if (entry.kind === "BridgeError") startFailed("Causa could not start because of an internal error.", entry.detail);
    },
  } as never,
  { requireHandshake: true } as never,
);

const timeout = new Promise((_, reject) => setTimeout(() => reject(new Error("start timed out")), START_TIMEOUT_MS));

Promise.race([kernel.start(), timeout]).then(
  () => {
    if (kernel.status !== "running") {
      startFailed("Causa could not start.", `kernel status ${kernel.status}`);
      return;
    }
    // WASM-LOAD-004: the shell leaves only once the application has rendered its first projection.
    // Test build only: `?percepta=default` shows the fixture investigation with no predicates, so a Percepta run
    // can establish the screen's identity before it injects its own fixtures.
    if (TEST_BUILD && new URLSearchParams(location.search).get("percepta") === "default") {
      void (Reflect.get(window, "__perceptaSetState") as (s: unknown) => Promise<void>)({ name: "default", predicates: [], capabilities: {} });
    }
    if (boot !== null && boot.dataset.state !== "failed") {
      boot.remove();
      if (ready !== null) {
        ready.textContent = "Causa is ready.";
        ready.focus();
      }
    }
  },
  (error) => startFailed("Causa took too long to start. Check your connection and retry.", error),
);

// ---- The causal graph (UI-026, T23) ----------------------------------------------------------------------------------
// The application projects the causal structure as a Forma workflow document into data-document on #causa-graph.
// This adapter runs @echelon-foundry/forma-workflow in isolated (iframe) mode: its own .NET runtime in its own
// document, never beside the Causa runtime. It forwards the projected document in and the selected node id out
// through a hidden control whose change event the application handles (graph-select). It decides nothing.
const WORKFLOW_PROTOCOL = "forma-workflow-host/1";
const workflowBase = new URL("./vendor/forma-workflow/", import.meta.url);
const formaBase = new URL("./vendor/forma/", import.meta.url).href;
let graph: { container: Element; frame: HTMLIFrameElement; ready: boolean; sent: string } | null = null;
let messageId = 0;

const selectionId = (object: unknown): string => {
  if (typeof object === "string") return object;
  if (object !== null && typeof object === "object") {
    const o = object as Record<string, unknown>;
    for (const key of ["id", "node", "object"]) if (typeof o[key] === "string") return o[key] as string;
  }
  return "";
};

const postToGraph = (message: Record<string, unknown>) => {
  if (graph === null || graph.frame.contentWindow === null) return;
  graph.frame.contentWindow.postMessage({ protocol: WORKFLOW_PROTOCOL, id: `causa-${++messageId}`, ...message }, location.origin);
};

const sendDocument = () => {
  if (graph === null || !graph.ready) return;
  const text = graph.container.getAttribute("data-document") ?? "";
  if (text === "" || text === graph.sent) return;
  graph.sent = text;
  try {
    postToGraph({ type: "load", document: JSON.parse(text) });
    postToGraph({ type: "mode", mode: "inspect" });
  } catch (error) {
    diagnostics.push({ kind: "GraphDocument", detail: String(error) });
  }
};

const syncGraph = () => {
  const container = document.getElementById("causa-graph");
  if (container === null) {
    graph = null;
    return;
  }
  if (graph === null || graph.container !== container) {
    const frame = document.createElement("iframe");
    const src = new URL("frame.html", workflowBase);
    src.searchParams.set("parent", location.origin);
    src.searchParams.set("forma", formaBase);
    src.searchParams.set("brand", "causa");
    frame.src = src.href;
    frame.title = "Causal graph: hypotheses and the outcomes they explain";
    frame.className = "causa-graph__frame";
    container.replaceChildren(frame);
    graph = { container, frame, ready: false, sent: "" };
  }
  sendDocument();
};

window.addEventListener("message", (event) => {
  if (graph === null || event.source !== graph.frame.contentWindow || event.origin !== location.origin) return;
  const message = event.data;
  if (message === null || typeof message !== "object" || message.protocol !== WORKFLOW_PROTOCOL) return;
  if (message.type === "frame-ready") {
    graph.ready = true;
    graph.sent = "";
    sendDocument();
  } else if (message.type === "event" && message.event && message.event.type === "selection") {
    const objects: unknown[] = Array.isArray(message.event.objects) ? message.event.objects : [];
    const input = document.getElementById("causa-graph-selection");
    if (input instanceof HTMLInputElement) {
      input.value = objects.length > 0 ? selectionId(objects[0]) : "";
      input.dispatchEvent(new Event("change", { bubbles: true }));
    }
  } else if (message.type === "reply" && message.ok === false) {
    diagnostics.push({ kind: "GraphRefused", detail: String(message.error) });
  }
});

new MutationObserver(syncGraph).observe(document.body, { subtree: true, childList: true, attributes: true, attributeFilter: ["data-document"] });

// ---- Linked records (UI-033) ----------------------------------------------------------------------------------------
// When the projection marks a record selected (a link with ?item=), move focus to it so keyboard and screen-reader
// users land where the link pointed. Mechanism only: which record is selected is the application's decision.
new MutationObserver((records) => {
  for (const record of records) {
    const el = record.target;
    if (el instanceof HTMLElement && el.dataset.selected === "true" && el.tabIndex === -1) el.focus();
  }
}).observe(document.body, { subtree: true, attributes: true, attributeFilter: ["data-selected"] });

// ---- Report printing (T26) -------------------------------------------------------------------------------------------
// Printing is a browser presentation action with no domain effect; the application renders the report it prints.
document.addEventListener("click", (event) => {
  const target = event.target instanceof Element ? event.target.closest("#causa-print") : null;
  if (target !== null) window.print();
});

// ---- Percepta state injection (test build only) ----------------------------------------------------------------------
// The state travels through the normal Limen event path (a hidden control) to the F# application, which projects it
// with the same View code the person sees. Resolves once the DOM shows the state; throws when it was refused.
if (TEST_BUILD) {
  Reflect.set(window, "__perceptaSetState", async (state: { name?: string }) => {
    const input = document.getElementById("percepta-state");
    if (!(input instanceof HTMLInputElement)) throw new Error("Percepta state control is missing");
    const name = typeof state?.name === "string" ? state.name : "unnamed";
    document.body.removeAttribute("data-percepta-state");
    input.value = JSON.stringify(state);
    input.dispatchEvent(new Event("change", { bubbles: true }));
    const deadline = Date.now() + 10_000;
    for (;;) {
      const shown = document.body.getAttribute("data-percepta-state") ?? "";
      if (shown === name) return;
      if (shown.startsWith("refused: ")) throw new Error(shown.substring("refused: ".length));
      if (Date.now() > deadline) throw new Error("The injected state was not rendered in time");
      await new Promise((resolve) => setTimeout(resolve, 20));
    }
  });
}
