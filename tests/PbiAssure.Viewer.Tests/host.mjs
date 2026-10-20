// Isolated development/test host. Not published with the application.
import http from "node:http";
import { readFile } from "node:fs/promises";
import { resolve, extname, sep } from "node:path";
import { fileURLToPath } from "node:url";
const repository = resolve(fileURLToPath(new URL("../..", import.meta.url)));
const web = process.env.VIEWER_WEB_ROOT
  ? resolve(process.env.VIEWER_WEB_ROOT)
  : resolve(repository, "src/PbiAssure.Web/wwwroot");
const here = fileURLToPath(new URL(".", import.meta.url));
const types = {
  ".js": "text/javascript",
  ".mjs": "text/javascript",
  ".json": "application/json",
  ".html": "text/html",
  ".css": "text/css",
  ".svg": "image/svg+xml",
};
http
  .createServer(async (req, res) => {
    const url = new URL(req.url, "http://localhost");
    const base = url.pathname.startsWith("/snapshots/")
      ? resolve(repository, "artifacts/viewer-test-snapshots")
      : url.pathname.startsWith("/test/")
        ? here
        : web;
    const relative = url.pathname
      .replace(/^\/(?:snapshots|test)\//, "")
      .replace(/^\//, "");
    const path = resolve(
      base,
      relative.endsWith("/") ? relative + "index.html" : relative,
    );
    if (!path.startsWith(resolve(base) + sep)) {
      res.writeHead(403).end();
      return;
    }
    try {
      const body = await readFile(path);
      res.writeHead(200, {
        "Content-Type": types[extname(path)] || "application/octet-stream",
        "Cache-Control": "no-store",
        "Content-Security-Policy":
          "default-src 'self'; script-src 'self'; style-src 'self'; connect-src 'self'; img-src 'self' data:; object-src 'none'; base-uri 'self'; frame-ancestors 'none'",
      });
      res.end(body);
    } catch {
      res.writeHead(404).end();
    }
  })
  .listen(4176, "127.0.0.1", () =>
    console.log(
      `Viewer test host: http://127.0.0.1:4176/test/host.html (${web})`,
    ),
  );
