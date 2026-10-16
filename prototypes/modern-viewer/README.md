# Modern viewer experiment

A separate, read-only PBI Assure investigation workspace. This experiment is contained in this directory; it does not change Core, Reporting, the production Web app, its publishing workflow, or the generated HTML experience.

Started from `master` at `93ae2c6d35aebf382a9be402d4a2c9cafa909f4a` (merged front-door analysis journey), on `modern-viewer-prototype`. Nothing is merged by this work.

## Run and compare

Requires Node.js 22 or newer. The viewer itself has **no npm runtime dependencies** and needs no .NET build to open the bundled fixture.

```powershell
cd C:\Projects\pbi-assure\prototypes\modern-viewer
npm run dev
```

Open <http://127.0.0.1:4173>. Stop the server with Ctrl+C. `PORT` can override 4173. Serve over HTTP; opening `index.html` directly cannot load the JSON module/data reliably.

Suggested five-minute comparison:

1. Select `DirectlyUsedMeasure` in the default model. Inspect Summary, DAX and its saved report location.
2. Filter to **Measures**, open **Dependencies**, then follow `IndirectlyUsedColumn`. The inspector changes while the collection remains filtered. Use inspector back/forward to retrace the investigation, or **Show in collection** to clear filters and reveal the object.
3. Select `BaseAmount`. Compare all immediate **Used by** sources with the reachable predecessors in **Why**. An unused source is not automatically the reason for an indirectly used state.
4. Filter to **Apparently unused** and **Checks limited**. Inspect the review language and model-level limitations under Evidence.
5. Open **Report pages**, select **Core coverage**, then follow a visual’s object reference back to the model inspector.

Keyboard: Tab reaches native controls and the selected collection row; ↑/↓ and Home/End move through the object collection; Enter selects; `/` focuses search outside editable controls; ←/→ and Home/End navigate inspector tabs. Dependency links are ordinary buttons and move focus to the new object heading. Usage states have text labels, not colour alone. Narrow screens stack the collection above the inspector.

## Use your own analysis

From the repository root, export with the existing scanner:

```powershell
dotnet run --project src/PbiAssure.Cli -- scan "C:\path\to\your\PBIP-project" --format json --output artifacts/viewer-analysis.json
```

Choose **Open analysis JSON** in the viewer. Schema `0.26` is supported, with a 30 MB prototype input limit. Files are read in this browser tab; there is no upload, persistence, telemetry or external font/asset request. Reloading restores the bundled fixture. Invalid imports retain the previous investigation. Do not put private inventory files in `data/` when sharing or hosting the static build.

## What is implemented

- Persistent application navigation, semantic-model selection and table filters.
- Scannable object collection with search, usage, confidence and type filters; system-generated objects are optional.
- Independent collection/inspector scrolling; contextual selection, dependency navigation and inspector history.
- Summary/Why, Used by, direct report locations, saved DAX with copy, dependency directions and expandable evidence paths.
- Lightweight saved report page/visual inspection and a scope/limitation view.
- The scanner’s five usage states and separate confidence, including explicit review language for absence-based states.

No report rendering, project picker/scanner integration, review management, full Power Query workspace, relationship/security workspace or production export workflow is implemented.

## Data provenance

`data/coverage.json` is output from the existing CLI/Core scanner over [`tests/fixtures/pbi-assure-coverage`](../../tests/fixtures/pbi-assure-coverage), generated against the base commit above. This is real **PBI Assure analysis of a synthetic repository fixture**, not an operational customer report and not claimed to be Desktop-authored. The fixture’s [`coverage-manifest.json`](../../tests/fixtures/pbi-assure-coverage/coverage-manifest.json) documents its origin and independent classification expectations.

The snapshot contains 5 semantic models, 5 reports, 19 saved pages and 88 usage objects. The default model has 77 objects with system-generated items excluded. Object states, confidence, direct locations, expressions, graph edges and limitations are preserved. Only `RootPath` is replaced with the repository-relative fixture path to avoid shipping a developer checkout path. Refresh from the repository scanner with `npm run fixture` (requires the repository’s .NET SDK).

## Architecture and adoption trade-offs

Native browser ES modules, semantic HTML and CSS; no frontend framework, backend or runtime packages. `data.js` is a presentation adapter over the existing JSON inventory. `app.js` manages explicit in-memory collection, inspector and navigation state and renders only the active collection/detail context. `styles.css` provides a separate visual language scoped to this experiment.

The architectural difference from the generated report is material: metadata is data, selection is application state, and following dependencies changes a detail pane rather than navigating through pre-rendered document sections. The read-only scanner and its classifications are unchanged. The adapter indexes the graph by model/table/name/type/hierarchy identity; object names alone are not keys.

This deliberately small implementation makes the interaction architecture testable without introducing a framework/toolchain migration. Before production adoption, consider typed/versioned viewer DTOs, component boundaries, incremental rendering or virtualisation for large models, persisted/deep-linked investigation state, a more complete accessible interaction audit, and integration with the browser’s existing in-memory scanner. No large-model performance claim is made.

Important JSON boundaries:

- Some fields are intentionally omitted by Core’s public JSON contract: object descriptions, dynamic format-string expressions, structural provenance, hidden projection provenance and precise per-limitation affected-object reach. The viewer does not reconstruct them.
- Each object’s `ClassificationConfidence` is authoritative. Evidence lists **model-level** limitations without claiming every listed limitation affects that object.
- Why supporting predecessors use the scanner’s published reachability. Used by lists all immediate sources, including unused branches. Report locations are **direct** locations only; no transitive report placements are invented.
- Tables, functions, relationships, roles, perspectives and report measures may be graph-only nodes. Their names and edge provenance remain visible, but they are not presented as selectable model usage objects with invented states.
- The public graph does not preserve report-measure ownership or report-scoped reachability. Same-named report-measure nodes may be ambiguous; the prototype documents that boundary rather than deriving precise owner-specific explanations.
- The collection re-renders on filtering and selection. Suitable for this small experiment; not validated at production inventory scale. Inspector history is a linear sequence, not a saved review or browser-navigation history.

## Validation and static build

```powershell
npm install
npm test
npm run build
# Only if Chromium is not already available to Playwright:
npx playwright install chromium
npm run test:browser
```

`PLAYWRIGHT_CHROMIUM_EXECUTABLE_PATH` optionally points the tests at an existing Chromium installation. The application needs no browser download; that step is only for automated tests.

Proportionate checks: five data-adapter tests (including canonical fixture expectations); three browser workflows covering context/history/scroll, keyboard/tab operation and usage honesty, report-to-object links, invalid/local import and escaped metadata. Desktop captures are written to ignored `artifacts/modern-viewer/`. Manually reviewed the 1440×900 and 1280×900 layouts. Production suites and exhaustive narrow-width/fixture/performance matrices were intentionally not run.

`npm run build` creates an ignored `dist/` with static files. This can be hosted at any static origin or subdirectory. No production site or hosting configuration is modified; this branch has not been remotely deployed.

## Assessment

Yes: this direction materially improves object/dependency investigation. The stable collection and reversible inspector navigation make it easier to keep an investigation’s starting point while exploring evidence. The benefit comes primarily from application state and a persistent layout; it does not require React or a replacement analysis engine. This prototype is enough to compare that direction, but does not establish that it should replace the portable generated report or that every feature will benefit equally.
