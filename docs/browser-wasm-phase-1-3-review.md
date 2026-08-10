# PBI Assure browser/WebAssembly implementation review

**Review date:** 10 August 2026

**Scope:** Browser/WebAssembly Phases 1–3

**Review type:** Architecture, privacy, security and public-beta readiness
**Implementation changes made during review:** None

## Executive verdict

**Sound architecture with a small hardening pass required.**

Phases 1–3 form a good foundation for the real browser product. The analysis and reporting engines remain shared, browser code is appropriately limited to file acquisition and downloads, and no backend or architectural redesign is needed.

The exact local-processing claim is supportable, with disclosure caveats noted below.

## Must fix before public beta

### 1. Neutralise spreadsheet formulas in CSV

**Classification:** Confirmed issue — medium severity.

`SemanticUsageCsvRenderer` correctly handles commas, quotes and line breaks, but does not neutralise values beginning with `=`, `+`, `-` or `@`.

A malicious or merely unusual object, page or report name could therefore become an Excel formula after export.

Smallest fix:

- Prefix dangerous text cells with an apostrophe before normal CSV quoting.
- Include leading tab and carriage-return cases.
- Leave numeric count columns unchanged.
- Add parameterised tests without changing the schema.

### 2. Clarify sensitive content in downloaded HTML

**Classification:** Confirmed privacy/transparency issue.

The report says “Connection values are withheld from this report”, but it also includes complete M expressions.

Those expressions can contain local paths, server names, URLs or hard-coded credentials. Nothing is uploaded, but a user could unknowingly share the downloaded report.

Smallest fix:

- Clarify that raw values are omitted from the data-source summary while full M expressions remain available elsewhere in the report.
- Warn before HTML download that the report may contain sensitive project metadata.
- Do not remove useful M evidence by default unless a later product decision introduces a redacted export mode.

### 3. Apply a production Content Security Policy

**Classification:** Hardening recommendation elevated to a public-beta requirement.

The browser application currently has no Content Security Policy (CSP). Current rendering appears injection-safe, but a CSP is especially valuable for a product whose trust proposition is that local project data cannot leave the browser.

Use the policy proposed later in this document and verify it in Edge and Chrome. In particular, `connect-src 'none'` turns the no-upload design into a browser-enforced restriction.

### 4. Update the browser runtime packages

**Classification:** Confirmed servicing issue.

The Web project pins both Blazor packages to `10.0.0`. The current .NET 10 servicing release at the time of review is `10.0.10`.

The vulnerability audit found no published vulnerability for the installed packages, but Microsoft requires supported installations to remain current with patches.

Update both Microsoft WebAssembly packages together and rerun browser and output validation.

Reference: [.NET support policy](https://dotnet.microsoft.com/en-us/platform/support/policy)

### 5. Publish only from identifiable committed source

**Classification:** Confirmed release-process issue.

At review time, `master` and `origin/master` both pointed to `345b9ad`, while all Phase 1–3 work remained uncommitted and the Web project was untracked.

Publishing that working copy would mean the public repository did not reproduce the hosted application.

Before beta:

- Commit the reviewed implementation.
- Build from a tagged or otherwise recorded commit.
- Display the version or short commit SHA in the browser UI.
- Link to the public repository.

## Should fix before public beta

### Browser ingestion boundaries

**Classification:** Meaningful risk.

`project-picker.js` recursively visits every directory and obtains a `File` object for every file before filtering. It also accepts relevant files from nested projects, so selecting a broad folder such as `C:\Projects` could merge several PBIPs into one misleading analysis.

For the primary picker path:

- Validate that the chosen folder looks like one project root.
- Restrict ingestion to its root `.pbip` and immediate `.Report` and `.SemanticModel` trees.
- Test zero and multiple project roots.
- Filter paths before calling `getFile()`.
- Detect case-insensitive path collisions explicitly.

Add proportionate initial limits:

- 10,000 visited entries.
- 5,000 accepted metadata files.
- 25 MiB per accepted file.
- 100 MiB accepted in total.
- 64 directory levels.

These figures can be adjusted after benchmarking. Their purpose is to return a clear error rather than leave an unresponsive or out-of-memory browser tab.

The browser cannot construct access outside the granted directory: files are opened from browser-provided handles retained in the local map, and both JavaScript and Core reject escaping relative paths. That part is sound.

### Physical adapter enumeration

**Classification:** Meaningful maintenance and performance risk, not a browser escape.

`PhysicalProjectFileSource` eagerly indexes every file below the selected root, including unrelated outputs and potentially reparse-point trees. This is broader than the scanner needs and can make CLI or Windows scans fail on an irrelevant inaccessible directory.

Limit physical enumeration to project metadata areas, or deliberately skip reparse points and irrelevant trees. Browser privacy is unaffected.

### Browser errors and fallbacks

**Classification:** Hardening recommendation.

Current normal errors are understandable and do not expose stack traces, but:

- A folder containing accepted-looking files without a recognisable project can still produce a zero-value “successful” scan.
- Out-of-memory and unexpected exceptions fall through to Blazor’s generic fatal-error UI.
- Fallback picker cancellation timing is brittle.
- A managed browser that exposes `showDirectoryPicker` but blocks it by policy is not offered the directory-input fallback.
- The Blob helper cannot tell whether enterprise policy blocked the actual download.
- The current generic picker failure advises Chromium even though a fallback exists.

Add project validation, a top-level error boundary, clearer limit messages and an explicit fallback action.

### Documentation and public trust material

**Classification:** Confirmed stale documentation.

The existing feasibility document still lists the completed Phase 1 filesystem work as an unresolved blocker and finishes by recommending the proof of concept that is now complete.

Its performance numbers are also stale: the current picker accepts 299 files and 6.68 MiB from Sales & Returns, including one 3.39 MiB file, rather than the documented 239 files, 2.1 MiB and 43 KiB maximum file.

It also recommends `connect-src 'self'`, while Microsoft’s current standalone client-side CSP starting point uses `connect-src 'none'`.

Before beta:

- Convert the feasibility document into an updated architecture/status decision, or clearly mark its original feasibility sections as superseded.
- Add a short privacy architecture page.
- Add a licence before describing the repository as open source; none was present at review time.
- Consider a concise `SECURITY.md`.

### Static deployment hygiene

**Classification:** Hardening recommendation.

Repeated publishing into `artifacts/web` has left several old fingerprinted assemblies. They are ignored by Git and not normally loaded, but a production deployment should come from a clean, versioned directory.

Configure the eventual host to:

- Serve HTTPS.
- Serve `.wasm` as `application/wasm`.
- Serve published Brotli or Gzip assets with the correct `Content-Encoding`.
- Cache fingerprinted assets immutably.
- Revalidate or avoid long caching for `index.html`.
- Deploy atomically from a clean publish directory.
- Rewrite unknown application routes to `index.html` if more routes are added.
- Adjust `<base href="/">` if hosted below the domain root.

## Can wait

- Web Worker, WebAssembly threading or AOT.
- Streaming or asynchronous redesign of `IProjectFileSource`.
- Inline report viewing or a full browser-native report UI.
- Universal Firefox and Safari support.
- Moving Desktop output orchestration out of CLI.
- Separating Web tests into a new test project.
- Redesigning `RootPath` before browser JSON export exists.
- PWA, service worker or offline installation.
- Removing the two small JavaScript globals.
- Bit-for-bit reproducible builds beyond a pinned SDK, tagged source and release checksums.
- The known CSV `ReportLocationCount` versus friendly-location grouping issue.

## Privacy verdict

**Accurate with caveats.**

The application can currently say:

> Your Power BI project is processed locally in your browser. Project files and analysis results are not uploaded to PBI Assure.

The code supports that statement:

- Startup loads only same-origin HTML, CSS, JavaScript, WebAssembly and .NET assets.
- Directory selection and enumeration use browser-local APIs.
- File contents cross only from JavaScript into the local WebAssembly runtime.
- Analysis, HTML rendering and CSV rendering call shared in-browser .NET code.
- Downloads use local Blob URLs.
- No `HttpClient`, `fetch`, XHR, WebSocket, SignalR, telemetry, analytics or error-reporting endpoint exists.
- Microsoft guidance URLs in the report are passive and transmit nothing until clicked.
- There are no remote fonts, scripts, CSS or images.

Caveats to disclose:

- The static host sees ordinary requests for application assets, including normal IP and browser request logs.
- Browser extensions and organisation-managed browser services are outside PBI Assure’s control.
- Downloaded HTML may contain sensitive M expressions and metadata.
- A CSP has not yet been applied.

## HTML and JavaScript security verdict

Downloaded HTML is reasonably safe under the current threat model.

Dynamic values consistently pass through `HtmlEncoder.Default`, reference URLs are restricted to absolute HTTP or HTTPS URLs, and the report’s inline script is static rather than assembled from project data. The existing renderer test already exercises a `<script>` display name. No unsafe `innerHTML`, `eval`, `document.write` or similar mechanism was found.

Opening the downloaded report locally is therefore acceptable, subject to its sensitive-content warning.

If inline preview is introduced later:

- Use a sandboxed iframe, not injection into the Blazor DOM.
- Prefer `sandbox="allow-scripts"` without `allow-same-origin`.
- Add `frame-src blob:` only then.
- Give the generated document its own restrictive CSP.
- Carefully decide whether external-link popups are permitted.

The current zero-delay object-URL revocation works in the manually tested browsers, but a slightly delayed revocation would be safer before Safari support is claimed.

## Architecture verdict

Phases 1–3 are a good foundation for the real public UI.

The dependency shape remains healthy:

```text
PbiAssure.Core
       |
       v
PbiAssure.Reporting
       |
       +-- CLI
       +-- Windows
       +-- Web
```

More precisely, Reporting depends on Core, and each frontend consumes the shared layers. Web has not duplicated scanning, classification, assurance or rendering logic.

`IProjectFileSource` is small and useful rather than browser-specific. The scanner retains `Scan(string)` for existing frontends, while browser ingestion supplies an in-memory source. Filesystem access is isolated to the physical adapter.

The main architectural cautions are:

- `RootPath` means an absolute physical root on desktop but a display name in Web.
- Physical file indexing is broader than necessary.
- `FileExists` and enumeration repeatedly scan the file collection.
- A few evidence paths still use host-dependent `Path.Combine`.

None justifies a redesign before UI work. Clarify `RootPath` before exposing browser JSON or creating external schema consumers.

Browser and CLI findings should remain functionally equivalent because they use the same scanner and renderers. Expected differences are the source-root display, filenames and browser download behaviour. Current tests prove headline parity, but not yet complete inventory parity.

The known Desktop-to-CLI output-orchestration dependency can wait. It does not affect the browser architecture.

## Static hosting and dependency verdict

The application genuinely supports static-only hosting. Microsoft documents standalone Blazor WebAssembly as a set of static files served by an ordinary static server. The application executes in the browser and does not require an ASP.NET backend.

Reference: [Host and deploy ASP.NET Core Blazor WebAssembly](https://learn.microsoft.com/en-us/aspnet/core/blazor/host-and-deploy/webassembly/?view=aspnetcore-10.0)

Production dependencies are unusually small:

- Core and Reporting have no external package references.
- Web has only Microsoft’s Blazor WebAssembly package.
- The DevServer package is marked `PrivateAssets="all"`.
- No npm packages or third-party JavaScript libraries exist.
- No additional native dependency exists beyond Microsoft’s published .NET WebAssembly runtime.

The NuGet vulnerability audit reported no known vulnerable package from the configured sources. This small footprint materially strengthens the privacy and supply-chain story.

## Browser-support recommendation

For the first public release:

- **Supported:** current desktop Edge and Chrome, served over HTTPS.
- **Beta or best effort:** current Firefox and Safari through the directory-input fallback, only after explicit end-to-end testing.
- **Unsupported initially:** mobile browsers and older enterprise browser versions.

Keep `showDirectoryPicker` as the primary path where available. It requires HTTPS and a user gesture and remains unavailable in some major browsers. The directory-input fallback should remain because `webkitdirectory` is now broadly available in current browsers, although older managed versions may differ.

References:

- [MDN: `showDirectoryPicker`](https://developer.mozilla.org/en-US/docs/Web/API/Window/showDirectoryPicker)
- [MDN: `webkitdirectory`](https://developer.mozilla.org/en-US/docs/Web/API/HTMLInputElement/webkitdirectory)

On a managed enterprise device, test:

- File System Access API allowed and blocked.
- Directory-input fallback.
- WebAssembly and `wasm-unsafe-eval` policy.
- HTML and CSV download restrictions.
- Defender or SmartScreen behaviour for downloaded HTML.
- OneDrive and cloud-placeholder files.
- Corporate proxy handling of `.wasm`, compression and CSP.
- Clear behaviour when data-loss-prevention policy blocks exports.

## CSP recommendation

Use this as the initial response-header policy:

```text
default-src 'self';
base-uri 'self';
object-src 'none';
script-src 'self' 'wasm-unsafe-eval';
style-src 'self';
connect-src 'none';
img-src 'self' data:;
font-src 'self';
media-src 'none';
frame-src 'none';
worker-src 'none';
form-action 'none';
frame-ancestors 'none';
upgrade-insecure-requests;
```

`'wasm-unsafe-eval'` is required for the client-side Blazor Mono runtime. Microsoft’s current CSP guidance uses it and shows `connect-src 'none'` for a standalone client-side app.

Reference: [Microsoft Blazor CSP guidance](https://learn.microsoft.com/en-us/aspnet/core/blazor/security/content-security-policy?view=aspnetcore-10.0)

Important details:

- Apply `frame-ancestors` as an HTTP response header; it is not enforced from a CSP meta tag.
- Blob downloads do not require `connect-src blob:`.
- If report preview is added, change only `frame-src` to include `blob:` and sandbox the iframe.
- Add `worker-src 'self' blob:` only if a worker is actually introduced.
- Also use `X-Content-Type-Options: nosniff` and `Referrer-Policy: no-referrer`.
- Smoke-test the published build under the exact policy before release.

## Performance verdict

| Project size | Current suitability |
|---|---|
| Small PBIP | Suitable |
| Medium or Sales & Returns scale | Suitable; manually validated |
| Large enterprise PBIP | Not yet a supported promise |
| Extreme or unusual PBIP | Unsuitable without limits |

For a correctly selected large project, the first likely constraint is UI responsiveness: files are read sequentially and the synchronous scanner runs on the browser UI thread. For an incorrectly broad selection, unbounded recursive directory enumeration is likely to fail first.

Memory use includes:

- JavaScript `File` objects.
- One retained .NET byte array per accepted file.
- Parsed inventories and retained DAX and M strings.
- Complete HTML and CSV strings during export.
- A Blob copy during download.

The Sales & Returns input—299 files and 6.68 MiB accepted—is comfortable. No worker or threading work is justified until bounded benchmarks show a real need.

## Testing gaps

Prioritised additions:

1. CSV formula-injection cases for `=`, `+`, `-`, `@`, tab and carriage return.
2. Full physical-versus-in-memory inventory comparison, excluding expected root and timestamp differences—not just headline counts.
3. Hostile HTML metadata in table, object, query, M, relationship and attribute contexts.
4. Case-insensitive collisions, duplicate paths, nested projects and root-recognition tests.
5. Input-limit tests using synthetic manifests without allocating huge files.
6. Browser integration coverage for picker cancellation, policy denial, fallback selection, Blob downloads and CSP.
7. A release network test asserting that scanning and exporting issue no non-static network requests.
8. Managed Edge manual validation checklist.
9. Reserved Windows download filenames such as `CON` and `PRN`.

## Verification performed

- `dotnet test PbiAssure.slnx --no-restore -m:1`: **61 passed, 0 failed**.
- NuGet vulnerability audit: **no known vulnerable packages**.
- NuGet outdated audit: Web `10.0.0` to current `10.0.10`.
- `git diff --check`: clean.
- Generated, build and local sample artefacts remain correctly ignored.
- User manually validated browser headline figures in Edge and Chrome.
- User manually reviewed the downloaded HTML and CSV outputs at a glance.

## Suggested next implementation task

Implement only the **CSV formula-injection hardening** next:

- Add one central cell-sanitisation helper.
- Neutralise dangerous leading characters.
- Add focused parameterised tests.
- Preserve the current schema, quoting, Unicode and BOM behaviour.

That is the smallest confirmed security fix and does not require production UI work or architectural change.
