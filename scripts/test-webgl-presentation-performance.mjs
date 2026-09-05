import { createReadStream, existsSync, statSync } from "node:fs";
import { createServer } from "node:http";
import { extname, join, normalize } from "node:path";
import { chromium } from "playwright-core";

const root = join(process.cwd(), "apps/game-unity/Build/WebGL");
const chromePath = process.env.SEA_CHROME_PATH ??
  "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome";
if (!existsSync(join(root, "index.html"))) {
  throw new Error("Build the WebGL player before running its performance probe.");
}
if (!existsSync(chromePath)) {
  throw new Error(`Chrome executable not found at ${chromePath}.`);
}

const contentTypes = new Map([
  [".data", "application/octet-stream"],
  [".html", "text/html; charset=utf-8"],
  [".js", "text/javascript; charset=utf-8"],
  [".json", "application/json"],
  [".wasm", "application/wasm"],
]);

const contentEncodings = new Map([
  [".br", "br"],
  [".gz", "gzip"],
]);

function sendFile(request, response) {
  const pathname = new URL(request.url, "http://127.0.0.1").pathname;
  const relative = pathname === "/" ? "index.html" : decodeURIComponent(pathname.slice(1));
  const path = normalize(join(root, relative));
  if (!path.startsWith(root) || !existsSync(path) || !statSync(path).isFile()) {
    response.writeHead(404).end("Not found");
    return;
  }

  // Unity ships the player pre-compressed and the loader will not decompress it itself: a
  // .gz or .br payload served without its Content-Encoding arrives as bytes the runtime
  // cannot parse, and the player never boots. Content-Length goes with it because the
  // loader's progress bar and its cache both ask for it.
  const encoding = contentEncodings.get(extname(path));
  const sourceExtension = encoding ? extname(path.slice(0, -3)) : extname(path);
  response.setHeader("Content-Type", contentTypes.get(sourceExtension) ?? "application/octet-stream");
  response.setHeader("Content-Length", statSync(path).size);
  if (encoding) response.setHeader("Content-Encoding", encoding);
  createReadStream(path).pipe(response);
}

const server = createServer(sendFile);
await new Promise((resolve, reject) => {
  server.once("error", reject);
  server.listen(0, "127.0.0.1", resolve);
});

let browser;
try {
  const address = server.address();
  browser = await chromium.launch({
    executablePath: chromePath,
    headless: true,
    args: ["--enable-webgl", "--ignore-gpu-blocklist", "--use-angle=metal"],
  });
  const page = await browser.newPage({ viewport: { width: 1920, height: 1080 } });
  const browserErrors = [];
  let resolveEvidence;
  let rejectEvidence;
  const evidencePromise = new Promise((resolve, reject) => {
    resolveEvidence = resolve;
    rejectEvidence = reject;
  });
  const timeout = setTimeout(
    () => rejectEvidence(new Error("WebGL performance evidence timed out.")),
    180_000,
  );
  page.on("pageerror", error => browserErrors.push(error.message));
  page.on("console", message => {
    const text = message.text();
    const marker = "SEA_EVIDENCE_JSON=";
    const offset = text.indexOf(marker);
    if (offset >= 0) {
      try {
        resolveEvidence(JSON.parse(text.slice(offset + marker.length)));
      } catch (error) {
        rejectEvidence(error);
      }
    } else if (message.type() === "error") {
      browserErrors.push(text);
    }
  });

  await page.goto(
    `http://127.0.0.1:${address.port}/?seaPresentationPerformanceTest=1`,
    { waitUntil: "load", timeout: 60_000 },
  );
  const evidence = await evidencePromise;
  clearTimeout(timeout);
  const passed = evidence.schemaVersion === 1 &&
    evidence.platform === "WebGLPlayer" &&
    evidence.visibleShips >= 100 &&
    evidence.frameP95Milliseconds <= 16.7 &&
    evidence.frameP99Milliseconds <= 25 &&
    evidence.idleBytesPerFrame === 0 &&
    evidence.poolsStable === true &&
    evidence.runtimeErrors === 0 &&
    evidence.missingAssets === 0 &&
    browserErrors.length === 0;
  if (!passed) {
    throw new Error(JSON.stringify({ evidence, browserErrors }, null, 2));
  }

  console.log(`WebGL performance passed: ${JSON.stringify(evidence)}`);
} finally {
  if (browser) await browser.close();
  await new Promise(resolve => server.close(resolve));
}
