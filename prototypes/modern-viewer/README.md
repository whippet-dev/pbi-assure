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
5. Open **Report pages**, select **Core coverage**, then follow a visual’s object reference into the model inspector. **Back to visual** restores the report investigation.

Keyboard: Tab reaches native controls and the selected collection row; ↑/↓ and Home/End move through the object collection; Enter selects; `/` focuses search outside editable controls; ←/→ and Home/End navigate inspector tabs. Dependency links are ordinary buttons and move focus to the new object heading. Usage states have text labels, not colour alone. Narrow screens stack the collection above the inspector.

## Report pages comparison

The existing Model workspace and palette are retained. Report pages adds a report/page navigator, a searchable visual collection and a persistent visual inspector. Selecting a visual updates its detail pane without replacing the page context.

Try these investigations using the same bundled scanner snapshot:

1. **PbiAssureCoverage → Core coverage → Projection visual**: inspect object names, types and **Used as** roles. Open `DirectlyUsedMeasure`, follow a model dependency, then choose **Back to visual**. Report/page/visual selection, inspector tab and collection/detail scroll positions are restored. The Model workspace’s earlier filters and selection are restored when you return.
2. **Filter only visual**: two evidence paths are presented as one column used as a **Visual filter**. **Sort only visual** shows a **Sort** role. **Formatting only visual** shows the saved formatting reference.
3. **Selector control visual**: the saved reference is separate from scanner-confirmed direct usage. `SelectorOnlyColumn` remains Apparently unused in the model; a persisted selector is not converted into active usage.
4. **Valid actions → Behaviour**: inspect enabled actions, bookmark/page/URL targets and their evidence without executing them. **Projection visual → Behaviour** shows its tooltip target and explicit page interaction; the interaction’s visual link changes only the inspector selection.
5. Select **Diagnostics → Accessibility diagnostics**, then use the findings/reviews count on **Access missing alt**. The inspector shows saved accessibility settings and the original scanner finding/recommendation. Duplicate tab ranks are clearly page-level context. **Access excluded** retains the distinction between an informational review request and a finding.
6. **Page details** separates page/report filters, drillthrough settings, page/group/report findings and report limitations from visual-specific evidence. Empty and hidden pages remain browsable. **ReportMeasureLimited** exposes its recorded report-measure limitation in Evidence.

The visual list and tabs support the same arrow/Home/End and `/` keyboard conventions as Model. Search can hide the selected visual from the collection without discarding its inspector. Previously selected visuals are remembered per page.

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
- Report/page navigation, visual selection, object types/usage roles, saved behaviour/configuration, accessibility metadata and findings/reviews with source evidence.
- An explicit report-to-model return path and separate page/report context; a scope/limitation view.
- The scanner’s five usage states and separate confidence, including explicit review language for absence-based states.

No report rendering, project picker/scanner integration, review management, full Power Query workspace, relationship/security workspace or production export workflow is implemented.

## Data provenance

`data/coverage.json` is output from the existing CLI/Core scanner over [`tests/fixtures/pbi-assure-coverage`](../../tests/fixtures/pbi-assure-coverage), generated against the base commit above. This is real **PBI Assure analysis of a synthetic repository fixture**, not an operational customer report and not claimed to be Desktop-authored. The fixture’s [`coverage-manifest.json`](../../tests/fixtures/pbi-assure-coverage/coverage-manifest.json) documents its origin and independent classification expectations.

The snapshot contains 5 semantic models, 5 reports, 19 saved pages and 88 usage objects. The default model has 77 objects with system-generated items excluded. Object states, confidence, direct locations, expressions, graph edges and limitations are preserved. Only `RootPath` is replaced with the repository-relative fixture path to avoid shipping a developer checkout path. Refresh from the repository scanner with `npm run fixture` (requires the repository’s .NET SDK).

## Architecture and adoption trade-offs

Native browser ES modules, semantic HTML and CSS; no frontend framework, backend or runtime packages. `data.js` is a presentation adapter over the existing JSON inventory. `app.js` manages explicit in-memory collection, inspector and navigation state and renders only the active collection/detail context. `styles.css` provides a separate visual language scoped to this experiment.

Report pages adds `report-data.js` for scoped projections and `report-view.js` for report/visual presentation. The small `ui.js` module shares escaped text, icons and object-type glyphs with Model. There is no new data source, scanner change, dependency or production integration. Report/page identity uses the report-relative path and page name; visual selection is scoped to that page. The return context records the earlier Model state and retains the Report state independently while an object is investigated.

The architectural difference from the generated report is material: metadata is data, selection is application state, and following dependencies changes a detail pane rather than navigating through pre-rendered document sections. The read-only scanner and its classifications are unchanged. The adapter indexes the graph by model/table/name/type/hierarchy identity; object names alone are not keys.

This deliberately small implementation makes the interaction architecture testable without introducing a framework/toolchain migration. Before production adoption, consider typed/versioned viewer DTOs, component boundaries, incremental rendering or virtualisation for large models, persisted/deep-linked investigation state, a more complete accessible interaction audit, and integration with the browser’s existing in-memory scanner. No large-model performance claim is made.

Important JSON boundaries:

- Some fields are intentionally omitted by Core’s public JSON contract: object descriptions, dynamic format-string expressions, structural provenance, hidden projection provenance and precise per-limitation affected-object reach. The viewer does not reconstruct them.
- Each object’s `ClassificationConfidence` is authoritative. Evidence lists **model-level** limitations without claiming every listed limitation affects that object.
- Why supporting predecessors use the scanner’s published reachability. Used by lists all immediate sources, including unused branches. Report locations are **direct** locations only; no transitive report placements are invented.
- Tables, functions, relationships, roles, perspectives and report measures may be graph-only nodes. Their names and edge provenance remain visible, but they are not presented as selectable model usage objects with invented states.
- The public graph does not preserve report-measure ownership or report-scoped reachability. Same-named report-measure nodes may be ambiguous; the prototype documents that boundary rather than deriving precise owner-specific explanations.
- The collection re-renders on filtering and selection. Suitable for this small experiment; not validated at production inventory scale. Inspector history is a linear sequence, not a saved review or browser-navigation history.
- Visual **Uses** reads `SemanticObjectUsages.DirectReportReferences`, scoped by report, page, visual and artifact path. Raw `FieldReferences` without matching direct evidence are shown separately as other saved references; they do not change classifications. Repeated paths for one object are grouped while retaining all evidence and distinct roles.
- Report measures are inspected from their owning report’s `ReportMeasures` inventory, including saved DAX. They are not given a model usage state or a fabricated model object link. Report-measure lineage across models is outside this slice.
- Local model links require an explicit available `ByPath` model binding. Missing, remote or unspecified bindings are not rebound to a local model by name. This is a conservative prototype boundary for older/unbound inventories, not a change to Core’s analysis.
- Group visibility is omitted by JSON. Parent group paths and group-local coordinates are shown, but own hidden/tab-order settings are not represented as effective runtime accessibility or visibility. Unspecified settings remain unspecified; dynamic targets/text remain dynamic.
- Findings retain original severity, assessment type, recommendation and evidence. Page/group/report findings are labelled separately from visual findings; no findings is not a pass result. Actions, URLs, bookmarks and tooltip bindings are inspected only, not executed.
- There is no rendered page canvas, runtime filtering/navigation simulation, full theme review or complete Power BI formatting surface. Behaviour shows the metadata already published by the scanner. Page/report references are scoped evidence lists; model navigation in this slice starts from visual references. Report scroll/selection state is held in memory and is not a durable browser history or saved review.

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

Proportionate checks: nine data-adapter tests (including canonical fixture expectations, role/evidence grouping, saved selector/report-measure handling, model binding and finding scope); six browser workflows covering Model context/history, Report context/return/scroll, keyboard/tab operation, usage honesty, actions/tooltips, accessibility reviews, local import and escaped metadata. Desktop captures are written to ignored `artifacts/modern-viewer/`. Manually reviewed the 1440×900 and 1280×900 layouts and exercised Report selection, behaviour, keyboard tabs and the model return path. Production suites and exhaustive narrow-width/fixture/performance matrices were not run.

`npm run build` creates an ignored `dist/` with static files. This can be hosted at any static origin or subdirectory. No production site or hosting configuration is modified; this branch has not been remotely deployed.

## Assessment

Yes: this direction materially improves object/dependency investigation. The stable collection and reversible inspector navigation make it easier to keep an investigation’s starting point while exploring evidence. The benefit comes primarily from application state and a persistent layout; it does not require React or a replacement analysis engine. This prototype is enough to compare that direction, but does not establish that it should replace the portable generated report or that every feature will benefit equally.

The workspace model also holds up for Report pages: page context stays visible while visual details change, and the explicit return path makes crossing into the Model workspace reversible. A visual collection works better here than treating every visual as a new document section. The remaining question is how well this scales to larger real reports and richer visual metadata; this slice is sufficient for a hands-on comparison without rebuilding the whole report experience.
