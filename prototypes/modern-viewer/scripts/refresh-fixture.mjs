import { spawnSync } from "node:child_process";
import { readFile, writeFile, mkdir } from "node:fs/promises";
import { fileURLToPath } from "node:url";
const repository = new URL("../../../", import.meta.url);
const output = new URL("artifacts/modern-viewer/scan.json", repository);
await mkdir(new URL("./", output), { recursive: true });
const result = spawnSync(
  "dotnet",
  [
    "run",
    "--project",
    "src/PbiAssure.Cli",
    "--",
    "scan",
    "tests/fixtures/pbi-assure-coverage",
    "--format",
    "json",
    "--output",
    fileURLToPath(output),
  ],
  { cwd: fileURLToPath(repository), stdio: "inherit" },
);
if (result.error) throw result.error;
if (result.status !== 0) process.exit(result.status ?? 1);
const inventory = JSON.parse(await readFile(output, "utf8"));
// The only semantic-neutral scrub: do not ship a developer's absolute checkout path.
inventory.RootPath = "tests/fixtures/pbi-assure-coverage";
await writeFile(
  new URL("../data/coverage.json", import.meta.url),
  JSON.stringify(inventory, null, 2) + "\n",
);
console.log(
  "Refreshed the synthetic fixture from PbiAssure.Core through the existing CLI.",
);
