# Browser/WebAssembly feasibility assessment

> **Status: superseded as an implementation plan.** The project-file abstraction and the browser
> proof-of-concept phases described here were completed in August 2026. This document is retained as
> the original feasibility record. See [Browser application](browser-app.md) for current behaviour,
> [Browser privacy](browser-privacy.md) for the local-processing boundary, and
> [Browser static hosting](browser-hosting.md) for production deployment requirements.

## Executive verdict

**Feasible with moderate refactoring.**

PBI Assure is a strong candidate for client-side browser execution. Most of the valuable code—the domain model, semantic dependency analysis, Power Query lineage, assurance rules, HTML renderer, and CSV renderer—is ordinary managed .NET code with no native dependencies or automatic network access.

The main obstacle is concentrated rather than systemic: project discovery and parsing currently receive physical paths and call `File`, `Directory`, and host-dependent `Path` APIs directly. WebAssembly cannot use those APIs to browse an arbitrary local directory such as `C:\Projects`.

The minimum credible change is:

1. Represent the selected project as a canonical relative file tree.
2. Introduce a small read-only project-file source abstraction.
3. Adapt discovery and parsers to read through that abstraction.
4. Preserve the existing physical-filesystem entry point for CLI and Windows.
5. Add a standalone Blazor WebAssembly shell that populates the abstraction from a browser directory picker.

No analysis rewrite, JavaScript port, backend, or database is justified.

## Reuse matrix

| Component | Browser suitability | Required change | Risk |
|---|---|---|---|
| Inventory/domain records | Browser-ready | None beyond defining what `RootPath` means in a browser | Low |
| Semantic usage reconciliation | Browser-ready | None | Low |
| DAX dependency analysis | Browser-ready | None | Low |
| Assurance rules | Browser-ready | None; evidence paths remain virtual relative paths | Low |
| Query-level Power Query lineage | Browser-ready | None after project files have been parsed | Low |
| Column-level Power Query lineage | Browser-ready | None after project files have been parsed | Low |
| M/DAX/reference extractors | Browser-ready | One host-independent path-classification correction | Low |
| PBIR parser | Reusable with small abstraction | Replace direct file existence, enumeration, and stream opening | Moderate |
| Bookmark parser | Reusable with small abstraction | Use the same project-file abstraction | Low |
| TMDL parser | Reusable with small abstraction | Replace directory enumeration and `File.ReadAllLines` | Moderate |
| `ProjectScanner` | Requires meaningful but contained refactor | Add a file-source entry point and separate physical-path discovery | Moderate |
| HTML renderer | Almost browser-ready unchanged | Use a safe project display name rather than an absolute path | Low |
| CSV renderer | Browser-ready unchanged | Download through a browser `Blob` | Low |
| CLI output naming/writing | Not appropriate for browser | Retain for CLI/Windows only | None |
| Windows WPF app | Desktop/OS-specific | Retain unchanged as a separate frontend | None |
| Tests | Mostly reusable | Add in-memory file-source contract tests and browser spike tests | Low |

The processing pipeline after parsing is already separated appropriately. The browser issue is the path-based entry and parser I/O before that point.

## Filesystem coupling

### Project discovery

`ProjectScanner` currently combines:

- absolute-root resolution;
- directory existence checking;
- top-level `.Report` and `.SemanticModel` discovery;
- `.pbip` discovery;
- recursive definition-file counting;
- parsing and analysis.

This is the principal portability boundary.

### PBIR and bookmark parsing

The PBIR parsers directly perform page and visual directory enumeration, `File.Exists` checks, `File.OpenRead`, relative/full-path calculation, and report-to-model path resolution. The JSON parsing itself uses `System.Text.Json` and is portable. Only acquisition and path resolution need abstraction.

### TMDL parsing

The TMDL parser enumerates TMDL files and loads each one through `File.ReadAllLines`. Its actual parser is string/line based and readily reusable.

### Analysis and rules

The semantic, Power Query, and assurance layers operate on inventories and strings. Their occasional `Path.Combine` calls generally construct evidence labels rather than access the filesystem. These should eventually use canonical project paths, but they are not browser blockers.

### Output generation

The HTML and semantic-usage CSV renderers are already inventory-to-string functions. Physical writes are isolated in the CLI and should remain outside the browser.

### Windows and CLI

The confirmed OS-specific operations are appropriately frontend-oriented:

- WPF `OpenFolderDialog`;
- `Process.Start` to open files and folders;
- physical output paths;
- console output;
- file writing.

They do not need browser equivalents inside Core.

## Minimum file-source abstraction

### Phase 1 implementation status

Phase 1 is now implemented in Core. `IProjectFileSource` exposes `DisplayName`, an optional
physical `SourceRoot`, canonical `Files`, and `OpenRead(relativePath)`. Its companion
`ProjectFileEntry` carries a canonical project-relative path and length. `PhysicalProjectFileSource`
adapts the existing local-directory workflow; `InMemoryProjectFileSource` proves callers need not
invent a physical root.

`ProjectScanner.Scan(string)` remains the compatibility entry point and constructs a physical
source. New callers can use `ProjectScanner.Scan(IProjectFileSource)`. All Core acquisition paths
use `/`-separated project-relative paths; the physical adapter is the only place that translates to
host filesystem paths.

Avoid a general virtual-filesystem framework. Core only needs a read-only project tree.

An appropriate shape is:

```csharp
public interface IProjectFileSource
{
    string DisplayName { get; }
    IReadOnlyCollection<ProjectFileEntry> Files { get; }
    Stream OpenRead(string relativePath);
}

public sealed record ProjectFileEntry(
    string RelativePath,
    long Length);
```

A small indexed helper can provide:

- `FileExists(relativePath)`;
- child-file enumeration;
- immediate child-directory discovery inferred from file paths;
- extension filtering;
- canonical path resolution.

Use `/` as the canonical internal separator. The physical adapter converts between canonical relative paths and host paths at its boundary.

Then expose:

```csharp
ProjectScanner.Scan(IProjectFileSource source)
```

while retaining:

```csharp
ProjectScanner.Scan(string physicalRootPath)
```

as a compatibility wrapper for CLI, Windows, tests, and CI.

One additional correction is advisable: M source-path classification currently uses the host platform's `Path.IsPathFullyQualified`. A browser or Unix host could therefore misclassify a Windows `C:\...` path. Windows drive and UNC recognition should be host-independent.

## Browser directory ingestion

### Phase 2 proof-of-concept status

`PbiAssure.Web` is now a standalone Blazor WebAssembly proof of concept. It has one folder-selection
action, a local-processing disclosure, accepted-file diagnostics, and an assurance action that calls
`ProjectScanner.Scan(IProjectFileSource)` with an `InMemoryProjectFileSource`. It displays only
headline inventory counts and timing diagnostics; it does not render or download the full report.

The local JavaScript interop prefers `showDirectoryPicker()` and recursively collects selected files.
It falls back to a `webkitdirectory` file input when the picker API is unavailable. Both routes retain
only `.pbip`, `.pbir`, `.json`, `.tmdl`, `.bim`, and `.pbism` files within recognised `.Report` and
`.SemanticModel` project paths, normalising them to `/` before .NET reads their bytes on demand.

To publish the static spike:

```powershell
dotnet publish src/PbiAssure.Web -c Release -o artifacts/web
```

Serve `artifacts/web/wwwroot` from any ordinary static web server. There is no ASP.NET backend and
the application contains no project upload, telemetry, analytics, or remote-processing code.

### Phase 3 output status

After a scan, the proof of concept now displays concise finding, project, Power Query, and semantic-usage
counts from the same `ProjectInventory` used by the desktop and CLI workflows. It reuses
`HtmlReportRenderer.Render` and `SemanticUsageCsvRenderer.Render` directly, then downloads their output
from browser memory through one local Blob helper. The CSV download is UTF-8 with a BOM, matching the
desktop/CLI file-writing convention. Browser filenames are derived from the selected display name and do
not contain local paths. Inline HTML viewing remains deliberately deferred; the generated HTML is not
injected into the Blazor DOM.

### Recommended approach

Use a small JavaScript interop adapter with capability detection:

1. Prefer `showDirectoryPicker()` in Edge and Chromium.
2. Recursively enumerate the selected `FileSystemDirectoryHandle`.
3. Produce canonical relative paths.
4. Read only files relevant to PBI Assure.
5. Populate an in-memory `IProjectFileSource`.
6. Fall back to `<input type="file" webkitdirectory multiple>` where necessary.

`showDirectoryPicker()` requires HTTPS, a direct user gesture, and explicit permission. It remains limited across browsers but is well suited to managed Edge/Chromium environments.

The directory-input fallback preserves selected hierarchy through `webkitRelativePath`, although that API remains non-standard.

### Practical considerations

- WebAssembly cannot discover arbitrary paths or silently revisit `C:\Projects`.
- Permission and file handles may not survive reloads; analyse promptly after selection.
- Preserve the selected directory name separately from internal relative paths.
- Reject or flag relative paths that escape the selected root through `..`.
- Detect case-insensitive path collisions because current matching mostly uses `OrdinalIgnoreCase`.
- Filter out data files, caches, static resources, and local settings before crossing JS interop.
- Apply per-file and total-project size limits with clear errors.
- Avoid one expensive JS call per file where practical: enumerate once, then read relevant files with bounded concurrency.
- Set deliberate browser-stream size limits rather than relying on Blazor's default 500 KB limit.

Useful browser references:

- [MDN: `showDirectoryPicker`](https://developer.mozilla.org/en-US/docs/Web/API/Window/showDirectoryPicker)
- [MDN: `webkitRelativePath`](https://developer.mozilla.org/en-US/docs/Web/API/File/webkitRelativePath)
- [Microsoft: Blazor file uploads and browser streams](https://learn.microsoft.com/aspnet/core/blazor/file-uploads?view=aspnetcore-10.0)

## Confirmed blockers, risks, and non-issues

### Confirmed blockers

- Direct physical filesystem discovery in `ProjectScanner`.
- Direct `File` and `Directory` calls inside PBIR, bookmark, and TMDL parsers.
- Host-dependent path resolution for report/model bindings.
- Physical output writing in the CLI.
- WPF, folder dialogs, shell launching, and `Process.Start`.
- The desktop project's `net10.0-windows` target.

### Likely risks

- The scanner is synchronous and could temporarily block the browser UI.
- Large HTML reports are assembled as complete strings.
- CSV generation performs repeated lineage filtering per semantic object.
- Inventory retains DAX and M expressions, increasing memory use.
- Very large projects may eventually require a Web Worker or WebAssembly threading.
- Trimming must be tested, although current parsers are not reflection-heavy.
- Canonical path behaviour must be tested across Windows-style PBIP references.

### Non-issues found

- No production NuGet dependencies.
- No native binaries.
- No database drivers.
- No Power BI Service calls.
- No XMLA/TOM dependency.
- No automatic telemetry or analytics.
- No cryptography dependency.
- No runtime process execution in Core or Reporting.
- No reason to rewrite parsers in JavaScript.

## .NET WebAssembly suitability

A standalone Blazor WebAssembly frontend is the most realistic reuse path.

The production projects target ordinary `net10.0`; only WPF targets Windows. Core and Reporting have no external package references. The test packages—xUnit, the test SDK, and the coverage collector—are not part of the runtime path.

A standalone Blazor WebAssembly app can be published as static files and hosted by a static web server or CDN without an ASP.NET backend.

Useful reference:

- [Microsoft: host and deploy standalone Blazor WebAssembly](https://learn.microsoft.com/aspnet/core/blazor/host-and-deploy/webassembly/?view=aspnetcore-10.0)

## HTML and CSV generation

The current renderers should be retained.

```text
ProjectInventory
   ├── display summary in Blazor UI
   ├── HtmlReportRenderer.Render()
   │      └── Blob download: assurance.pbiassure.html
   └── SemanticUsageCsvRenderer.Render()
          └── Blob download: semantic-usage.csv
```

For initial work:

- render basic counts directly in Blazor;
- create downloadable HTML and CSV using JavaScript `Blob` URLs;
- do not parse or scrape generated HTML.

For later inline viewing, use a sandboxed iframe backed by a Blob URL. The existing HTML is self-contained, but it contains inline JavaScript and user-derived report metadata, so rendering it directly into the main application DOM is less isolated.

One browser adaptation is needed: `ProjectInventory.RootPath` currently represents an absolute source path and is displayed in HTML. In the browser it should hold—or be complemented by—a safe display name such as `MyReport`, never a browser handle or invented physical path.

## Windows and CLI separation

The Windows app is already a thin shell: it selects a directory, calls `ProjectScanner`, invokes shared output generation, and opens results.

One layering debt exists: the desktop project references `PbiAssure.Cli` to reuse output planning and writing. Longer term, application-level orchestration could move into a small shared project:

```text
PbiAssure.Core
    ├── inventory
    ├── parsers
    ├── analysis
    └── rules

PbiAssure.Reporting
    ├── HTML
    └── CSV

PbiAssure.Application
    ├── scan orchestration
    └── output planning

CLI        Windows        Browser
```

That move is not required for the browser proof of concept. The browser can call Core and Reporting directly.

## Server and privacy assessment

No backend is required for the current product.

```text
Static host/CDN
    └── HTML, CSS, JS, WebAssembly and .NET assemblies
                 |
                 v
Browser
    ├── user grants directory access
    ├── files are read locally
    ├── analysis runs locally
    ├── results are rendered locally
    └── HTML/CSV are downloaded locally
```

Confirmed current network behaviour:

- no `HttpClient`, `fetch`, telemetry, analytics, or upload code;
- no remote fonts, scripts, CSS, images, or CDN assets in generated HTML;
- Microsoft and W3C guidance URLs are passive links that transmit nothing until clicked;
- system fonts are used.

To credibly state that processing is local:

- self-host all runtime and application assets;
- add no upload endpoint;
- use no telemetry by default;
- use a restrictive CSP, likely `default-src 'self'` and `connect-src 'self'`;
- avoid remote asset domains;
- make external links visibly external;
- test the browser Network panel while scanning a fixture;
- document that the host receives normal static-asset requests but no selected project files;
- provide an obvious "processed locally" disclosure near directory selection.

Use `connect-src 'self'`, rather than `'none'`, because Blazor must fetch its own runtime and application assets.

Useful references:

- [Microsoft: Blazor WebAssembly hosting models](https://learn.microsoft.com/aspnet/core/blazor/hosting-models?view=aspnetcore-10.0)
- [MDN: CSP `connect-src`](https://developer.mozilla.org/en-US/docs/Web/HTTP/Reference/Headers/Content-Security-Policy/connect-src)

## Performance assessment

The current Sales & Returns sample contains approximately:

- 239 files likely read by the scanner;
- 2.1 MB of relevant metadata;
- a largest likely-read file of roughly 43 KB.

That is comfortably browser-suitable.

Current memory behaviour:

- PBIR JSON files are parsed one at a time and disposed.
- TMDL files are loaded as line arrays.
- Extracted DAX and M expressions remain in inventory.
- Semantic and Power Query graphs are held in memory.
- HTML and CSV are each built as complete strings.
- No temporary files are used.
- Core analysis is single-threaded.
- No heavy parallelism or native work is required.

Expected suitability:

- Small reports: low risk.
- Medium real-world PBIP projects: likely suitable with an in-memory file source.
- Large enterprise projects: technically feasible, but UI responsiveness and peak memory need measurement.
- Extremely large models: may eventually need background worker execution, progress reporting, cancellation, and renderer optimisation.

Do not introduce threading or AOT optimisation before the proof of concept provides measurements.

## Proposed proof of concept

Create one minimal standalone `PbiAssure.Web` project.

### Scope

- One **Choose Power BI project** button.
- JavaScript `showDirectoryPicker()` integration.
- Recursive relative-path manifest.
- In-memory project-file source.
- Existing scanner invoked through the new abstraction.
- Display only:
  - selected project name;
  - accepted file count and total bytes;
  - report count;
  - page count;
  - visual count;
  - semantic-model count;
  - table count.
- No polished report UI.
- No authentication, backend, telemetry, or deployment automation.

### Success means

- The same synthetic fixture produces matching CLI and browser counts.
- Sales & Returns produces matching headline counts.
- Report-to-model `../Model.SemanticModel` references resolve correctly.
- The browser Network panel shows no requests containing project files or metadata.
- The spike runs from static hosting in current Edge.
- Memory and elapsed time are recorded.
- Directory cancellation and permission denial produce understandable messages.

### Failure means

A true architectural failure would be:

- Core or Reporting cannot publish under browser WebAssembly without replacing major dependencies;
- file content cannot be supplied without a server;
- realistic projects exceed browser memory before analysis starts;
- path abstraction requires rewriting analysis rather than only discovery and parsing.

A temporarily blocked UI or slow first run is a performance finding, not proof that the architecture is unworkable.

## Relative effort

- Project-file abstraction and canonical paths: **moderate**.
- Physical filesystem adapter and regression coverage: **moderate**.
- Browser directory-selection spike: **moderate**.
- Full analysis in WebAssembly after the abstraction: **small**, subject to testing.
- HTML/CSV downloads: **small**.
- Production privacy, compatibility, and performance hardening: **moderate**.
- Large-project worker/threading support, if needed: **potentially substantial**.

## Migration sequence

1. Add canonical project-relative path handling.
2. Add `IProjectFileSource` and a physical adapter.
3. Refactor scanner/PBIR/TMDL/bookmark acquisition to use it.
4. Keep existing `Scan(string)` behaviour and all CLI/Windows tests passing.
5. Add in-memory-source contract tests.
6. Create the minimal standalone Blazor WebAssembly spike.
7. Validate discovery and counts against CLI.
8. Run full analysis and assurance rules.
9. Add local HTML/CSV downloads.
10. Measure real projects.
11. Harden CSP, privacy disclosure, browser fallback, cancellation, and limits.
12. Only then decide whether inline HTML viewing or a browser-native results UI is worthwhile.

## Things not to change yet

- Semantic usage states or precedence.
- Assurance rules.
- DAX or M parsing algorithms.
- Inventory schema, except possibly separating root display name from physical root identity.
- HTML or CSV content.
- CLI output conventions.
- Windows packaging.
- WPF application.
- JavaScript beyond directory/file interop and downloads.
- Server authentication, APIs, databases, or telemetry.
- AOT, multithreading, service workers, or offline caching.

## Architectural debt affecting other frontends

- `ProjectScanner` combines acquisition, analysis orchestration, and scan timestamp creation.
- Absolute source path and project display identity share `RootPath`.
- Host-dependent `Path` semantics affect report binding and M source classification.
- PBIR and bookmark parsers duplicate JSON-file opening and error handling.
- Scanner execution has no cancellation or progress contract.
- Desktop depends on CLI for output services.
- CSV and parts of HTML perform repeated collection scans that may matter for very large projects.

Only the first three materially affect the browser spike.

## Final recommendation

PBI Assure should pursue the client-side browser architecture.

The evidence is favourable:

- production analysis has no third-party or native dependencies;
- the useful engine is already independent of WPF;
- HTML and CSV renderers are already pure;
- no current feature requires a backend;
- the privacy claim is technically credible;
- the portability work is concentrated in filesystem acquisition and canonical paths.

The recommended next step is the narrow directory-selection and counts proof of concept, preceded by the small read-only project-file abstraction. That spike will answer the remaining real questions—path fidelity, browser memory, responsiveness, and managed Edge compatibility—without committing the project to a full web UI.
