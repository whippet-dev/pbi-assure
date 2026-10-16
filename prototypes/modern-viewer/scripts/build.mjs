import { mkdir, copyFile, cp } from "node:fs/promises";
const root = new URL("../", import.meta.url);
await mkdir(new URL("dist/", root), { recursive: true });
for (const name of [
  "index.html",
  "app.js",
  "data.js",
  "ui.js",
  "report-data.js",
  "report-view.js",
  "styles.css",
  "mark.svg",
]) {
  await copyFile(new URL(name, root), new URL("dist/" + name, root));
}
await cp(new URL("data/", root), new URL("dist/data/", root), {
  recursive: true,
});
console.log(
  "Static viewer built in dist/. No server-side analysis or external assets.",
);
