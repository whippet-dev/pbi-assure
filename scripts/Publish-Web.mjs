import { createHash } from "node:crypto";
import { brotliCompressSync, gzipSync } from "node:zlib";
import { spawnSync } from "node:child_process";
import {
    existsSync,
    mkdirSync,
    readFileSync,
    readdirSync,
    rmSync,
    writeFileSync
} from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const scriptDirectory = dirname(fileURLToPath(import.meta.url));
const repositoryRoot = resolve(scriptDirectory, "..");
const outputDirectory = resolve(repositoryRoot, "artifacts", "web");
const expectedOutputDirectory = join(repositoryRoot, "artifacts", "web");
const assetVersionPlaceholder = "__PBIASSURE_ASSET_VERSION__";

if (outputDirectory.toLowerCase() !== expectedOutputDirectory.toLowerCase()) {
    throw new Error("The web publish output did not resolve to the expected generated directory.");
}

const options = parseArguments(process.argv.slice(2));
const revision = run("git", ["-C", repositoryRoot, "rev-parse", "--verify", "HEAD"]).trim();
assertRevision(revision, "The current Git revision could not be determined.");

if (options.sourceRevision !== null) {
    assertRevision(options.sourceRevision, "The supplied source revision is not a full Git commit SHA.");
    if (options.sourceRevision.toLowerCase() !== revision.toLowerCase()) {
        throw new Error("The supplied source revision does not match the checked-out Git revision.");
    }
}

const workingTreeChanged = hasGitChanges(["diff", "--quiet", "--"]);
const indexChanged = hasGitChanges(["diff", "--cached", "--quiet", "--"]);
const untrackedBuildInputs = run("git", [
    "-C", repositoryRoot,
    "ls-files", "--others", "--exclude-standard", "--",
    "src/PbiAssure.Web",
    "src/PbiAssure.Core",
    "src/PbiAssure.Reporting",
    "Directory.Build.props",
    "Directory.Build.targets",
    "global.json"
]).trim();
const buildInputsChanged = workingTreeChanged || indexChanged || untrackedBuildInputs.length > 0;

if (buildInputsChanged && !options.allowDirty) {
    throw new Error(
        "Tracked or untracked build-input changes are present. Commit them before a production publish, " +
        "or use --allow-dirty for a local review build.");
}

const sourceRevision = options.sourceRevision ?? revision;
const buildRevision = buildInputsChanged ? `${sourceRevision}-dirty` : sourceRevision;
// The entire production viewer module tree participates in the asset version.
function assetFiles(directory, prefix = "") {
    return readdirSync(directory, {withFileTypes: true}).sort((a,b)=>a.name.localeCompare(b.name)).flatMap(entry =>
        entry.isDirectory() ? assetFiles(join(directory,entry.name),prefix+entry.name+"/") : [prefix+entry.name]);
}
const investigationAssets = assetFiles(join(repositoryRoot,"src/PbiAssure.Web/wwwroot/investigation"));
const assetVersionInputs = [
    ...investigationAssets.map(path=>"src/PbiAssure.Web/wwwroot/investigation/"+path),
    "src/PbiAssure.Web/wwwroot/index.html",
    "src/PbiAssure.Web/wwwroot/project-picker.js",
    "src/PbiAssure.Web/wwwroot/download.js",
    "src/PbiAssure.Web/wwwroot/investigation.js",
    "src/PbiAssure.Web/wwwroot/css/site.css",
    "src/PbiAssure.Web/wwwroot/analysis-focus.js",
    "src/PbiAssure.Web/wwwroot/appearance.js",
    "src/PbiAssure.Web/wwwroot/favicon.svg",
    "src/PbiAssure.Web/wwwroot/css/core.css",
    "src/PbiAssure.Web/wwwroot/css/app.css",
    "src/PbiAssure.Web/wwwroot/report-viewer.html",
    "src/PbiAssure.Web/wwwroot/report-viewer.js"
];
const assetVersion = createHash("sha256")
    .update(assetVersionInputs
        .map(path => readFileSync(join(repositoryRoot, path), "utf8"))
        .join("\n"), "utf8")
    .digest("hex")
    .slice(0, 12);

rmSync(outputDirectory, { recursive: true, force: true });
mkdirSync(dirname(outputDirectory), { recursive: true });

run("dotnet", [
    "publish",
    join(repositoryRoot, "src", "PbiAssure.Web"),
    "-c", "Release",
    "-o", outputDirectory,
    `-p:SourceRevisionId=${buildRevision}`
], { inheritOutput: true });

const publishedRoot = join(outputDirectory, "wwwroot");
const requiredFiles = [
    ...investigationAssets.map(path=>"investigation/"+path),
    "index.html",
    "_headers",
    "download.js",
    "investigation.js",
    "css/site.css",
    "analysis-focus.js",
    "appearance.js",
    "favicon.svg",
    join("css", "core.css"),
    join("css", "app.css"),
    "report-viewer.html",
    "report-viewer.js",
    join("_framework", "blazor.webassembly.js")
];

for (const relativePath of requiredFiles) {
    const requiredFile = join(publishedRoot, relativePath);
    if (!existsSync(requiredFile)) {
        throw new Error(`The clean web publish is incomplete: ${requiredFile} was not produced.`);
    }
}

for (const relativePath of ["index.html", "download.js", "investigation.js", "report-viewer.html"]) {
    const versionedFile = join(publishedRoot, relativePath);
    const content = readFileSync(versionedFile, "utf8");
    if (!content.includes(assetVersionPlaceholder)) {
        throw new Error(`The web publish could not version browser asset references in ${versionedFile}.`);
    }

    writeVersionedAsset(versionedFile, content.replaceAll(assetVersionPlaceholder, assetVersion));
}

for (const relativePath of ["index.html", "download.js", "investigation.js", "report-viewer.html"]) {
    const output = readFileSync(join(publishedRoot, relativePath), "utf8");
    if (output.includes(assetVersionPlaceholder)) {
        throw new Error(`An asset-version placeholder remains in published ${relativePath}.`);
    }
}

// Version relative ES-module imports as well as the entry stylesheet/script.
// This prevents a new entry module from combining with stale cached children.
for (const relativePath of investigationAssets.filter(path=>/\.(js|html)$/.test(path))) {
    const file=join(publishedRoot,"investigation",relativePath);
    const content=readFileSync(file,"utf8");
    writeVersionedAsset(file,content.replace(/(["'])\.\/([\w./-]+\.(?:js|css|svg))\1/g,
        (_,quote,path)=>quote+"./"+path+"?v="+assetVersion+quote));
}

function writeVersionedAsset(file, content) {
    const bytes = Buffer.from(content, "utf8");
    writeFileSync(file, bytes);
    // Blazor precompresses static assets before this post-publish versioning step.
    if (existsSync(file + ".gz")) writeFileSync(file + ".gz", gzipSync(bytes));
    if (existsSync(file + ".br")) writeFileSync(file + ".br", brotliCompressSync(bytes));
}

console.log(`Published clean web application: ${publishedRoot}`);
console.log(`Embedded build revision: ${buildRevision}`);
console.log(`Browser asset version: ${assetVersion}`);

function parseArguments(argumentsToParse) {
    let allowDirty = false;
    let sourceRevision = null;

    for (let index = 0; index < argumentsToParse.length; index++) {
        const argument = argumentsToParse[index];
        if (argument.toLowerCase() === "--allow-dirty" || argument.toLowerCase() === "-allowdirty") {
            allowDirty = true;
            continue;
        }

        if (argument.toLowerCase() === "--source-revision" || argument.toLowerCase() === "-sourcerevision") {
            sourceRevision = argumentsToParse[++index] ?? null;
            if (sourceRevision === null) {
                throw new Error(`${argument} requires a full Git commit SHA.`);
            }
            continue;
        }

        throw new Error(`Unknown publish argument: ${argument}`);
    }

    return { allowDirty, sourceRevision };
}

function assertRevision(value, message) {
    if (!/^[0-9a-f]{40}$/i.test(value)) {
        throw new Error(message);
    }
}

function hasGitChanges(argumentsAfterGit) {
    const result = spawnSync("git", ["-C", repositoryRoot, ...argumentsAfterGit], {
        encoding: "utf8",
        windowsHide: true
    });
    if (result.status === 0) {
        return false;
    }
    if (result.status === 1) {
        return true;
    }

    throw new Error(result.stderr?.trim() || "The Git working-tree state could not be determined.");
}

function run(command, argumentsToRun, { inheritOutput = false } = {}) {
    const result = spawnSync(command, argumentsToRun, {
        cwd: repositoryRoot,
        encoding: "utf8",
        stdio: inheritOutput ? "inherit" : "pipe",
        windowsHide: true
    });

    if (result.error) {
        throw result.error;
    }
    if (result.status !== 0) {
        throw new Error(result.stderr?.trim() || `${command} failed with exit code ${result.status}.`);
    }

    return result.stdout ?? "";
}
