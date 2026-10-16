# Modern viewer experiment

A separate, read-only PBI Assure investigation workspace. This experiment is contained in this directory; it does not change Core, Reporting, the production Web app, its publishing workflow, or the generated HTML experience.

Started from `master` at `93ae2c6d35aebf382a9be402d4a2c9cafa909f4a` (merged front-door analysis journey), on `modern-viewer-prototype`. Nothing is merged by this work.

## Run and compare

Requires Node.js 22 or newer. The viewer itself has **no npm runtime dependencies** and needs no .NET build to open the bundled fixture.

```powershell
cd C:\Projects\pbi-assure\prototypes\modern-viewer
npm run dev
```

Open <http://127.0.0.1:4173>. Overview is the starting workspace, including after a successful local JSON import. Stop the server with Ctrl+C. `PORT` can override 4173. Serve over HTTP; opening `index.html` directly cannot load the JSON module/data reliably.

Suggested five-minute comparison:

1. Open **Model objects** and select `DirectlyUsedMeasure` in the default model. Inspect Summary, DAX and its saved report location.
2. Filter to **Measures**, open **Dependencies**, then follow `IndirectlyUsedColumn`. The inspector changes while the collection remains filtered. Use inspector back/forward to retrace the investigation, or **Show in collection** to clear filters and reveal the object.
3. Select `BaseAmount`. Compare all immediate **Used by** sources with the reachable predecessors in **Why**. An unused source is not automatically the reason for an indirectly used state.
4. Filter to **Apparently unused** and **Checks limited**. Inspect the review language and model-level limitations under Evidence.
5. Open **Report pages**, select **Core coverage**, then follow a visual’s object reference into the model inspector. **Back to visual** restores the report investigation.

Keyboard: Tab reaches native controls and the selected collection row; ↑/↓ and Home/End move through the object collection; Enter selects; `/` focuses search outside editable controls; ←/→ and Home/End navigate inspector tabs. Dependency links are ordinary buttons and move focus to the new object heading. Usage states have text labels, not colour alone. Narrow screens stack the collection above the inspector.

## Overview comparison

Overview is a continuous investigation surface in the existing shell, ordered around Needs attention, Model usage, Checks, Project snapshot and Other reviews. It adds no overall assurance score, pass indicator or synthetic urgency. The Model/Report layouts and palette are retained.

Try the following with the bundled analysis:

1. **Needs attention** triages 28 non-accessibility findings through 11 errors, 10 warnings, 7 review-required records and **Review 28 findings**. Severity and review-required counts can overlap. Actual findings and category filtering belong to **Reviews & findings**; Overview does not surface category or sampled-finding previews.
2. **Apparently unused → Model objects** opens all 26 matching authored objects across all five models. The small **All models** extension keeps the destination count consistent with the Overview scope. Model identity appears in collection rows in that scope; selecting one model restores the existing table navigator. Other usage states work the same way. The 87 authored objects exclude one system-generated object.
3. **Checks limited** opens **What was checked**. The 29 qualified object results and 12 recorded limitations are counted independently: not every recorded limitation affects usage. No identified limitations is explicitly not an everything-checked claim.
4. **Project snapshot** shows report/page/visual inventory plus model and Power Query counts. A report row opens that report’s page context; **Explore reports** resumes the existing Report investigation. Counts describe inventories in the saved analysis, not live project state.
5. **Accessibility review** opens a separate collection of five original accessibility observations. Theme’s two saved observations are secondary, under a collapsed disclosure; an available source link opens the exact visual’s saved formatting. Theme is not added to Needs attention or given its own headline metric.
6. In **Reviews & findings**, select a finding, inspect its original recommendation and evidence, then open its source where a workspace exists. **Back to reviews** retains the collection filters and selected finding. Missing page/visual targets are identified as missing and offer only a broader report/page route; they are not silently replaced by another target.

Sidebar visits retain Model search/filters/selection and Report page/visual/tab context, as well as collection/detail scroll positions. Overview and Reviews keep their own scroll/selection state. Explicit Overview shortcuts deliberately set a new collection scope; they are not saved review operations. Reviews has search, category/severity filters, a separate Review required filter, and the same collection arrow/Home/End and `/` keyboard conventions.

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

## Full lineage comparison

Open **Lineage** in the sidebar. Its first centre is the current Model selection; later visits resume the lineage investigation. Find a different object in the contextual sidebar or use **Start from Model selection** explicitly. The accepted workspaces and palette are retained.

This prototype uses two progressive adjacency lanes around a fixed centre: upstream dependencies and downstream dependents. Selecting a node changes the inspector; **Centre on this object** changes the centre. Previous/next centre restores each centre's expanded branches, selected node, tab and pane positions. The layout favours readable connections over a crossing-edge canvas; it does not require a graph library.

Try these with the unchanged real scanner snapshot:

1. Start at **DirectlyUsedMeasure**. Expand upstream **IndirectlyUsedMeasure** and **DynamicFormatStringMeasure**. `BaseAmount` appears once, marked **Shared · 2 connections**. Inspect it while keeping the centre and both lanes visible. Connections retains every immediate edge and a shortest orientation route; that route is not presented as the only lineage path.
2. **BaseAmount → Centre on this object** exposes 13 downstream nodes, initially seven immediate dependents. Expand additional levels individually. **Reports** aggregates all reachable downstream branches, independently of which are expanded: four dependent objects reach one confirmed projection-visual location. Reveal any owner to expose its connection without changing the centre, or inspect the saved visual and **Back to lineage**.
3. Select a model usage object and **Open in Model**. Follow other Model dependencies, then **Back to lineage**. Centre, selection, branches, inspector tab and scroll are retained; the earlier Model investigation is restored. Graph-only functions, relationships, roles and report measures stay inspectable in Lineage without being invented as selectable Model usage objects.
4. Centre **ReportRootedUdfSource**, expand downstream through **ReportRootedCoverageFunction**, and inspect its real function dependency and saved DAX evidence. Centre **ActiveReportMeasure** to see its published dependencies and report reach without inventing an exact visual placement.
5. Centre **UnresolvedSortColumn**. Its missing sort reference stays in **Evidence**, separate from established edges, with Checks limited retained. Centre **Fact · Table**, enable **Include table membership**, and reveal its 62 immediate dependents in batches of eight. Membership is explicit because it otherwise floods ordinary lineage with sibling context.

`lineage-data.js` indexes the existing `SemanticDependencies`, model usage objects and graph-only reachability nodes by model/table/name/type/hierarchy identity. An edge means **From depends on To**. Upstream traversal follows outgoing edges; downstream traversal follows incoming edges. Breadth-first traversal supplies distinct reachable nodes and shortest connections. Visible layers use shortest *visible* connections; each direction renders a node once, with all discovered parent links retained. Shared nodes are not copied into trees. Structural edges retain their kinds; they do not imply runtime filter propagation. Table membership is excluded by default and available explicitly, including its original evidence.

Strongly connected components identify cycles. Expansion keeps visited identities and back edges, so loops remain inspectable without repeating the centre or expanding forever. A node can appear once on each side if a cycle makes it both upstream and downstream. Each branch initially exposes eight neighbours; further batches are explicit. Inspector connections, endpoint locations and owners are also progressively bounded. No all-path enumeration or automatic whole-model canvas is generated.

Confirmed endpoints come only from `SemanticObjectUsages.DirectReportReferences`, collected across the centre and every reachable downstream node and grouped by saved location. Multiple owning objects and evidence roles are retained. Raw selectors, unresolved references and same-named visual guesses never create endpoints. Missing saved entities retain location evidence without an exact navigation link.

Important boundaries remain visible. Public JSON omits report-measure ownership, scoped reachability and exact report-measure placements. A report-measure projection is traversable only with one matching definition and an explicit local model binding; ambiguous/unbound ownership stops traversal, with the projected connections and candidates still inspectable. Recorded report reach without exact location evidence is separate from confirmed endpoint counts. Table/function direct report evidence is also absent from this contract. Unresolved dependency targets and candidate sets are not reconstructed. Counts describe the bounded saved graph in the chosen scope, not complete runtime lineage or Power Query/source-to-column flow.

The graph and centre sessions stay in memory. There is no durable/deep-linked investigation state, canvas layout, virtualisation or inventory-scale performance claim. The canonical snapshot has convergence and high fan-out; cycle handling is verified with clearly test-only added edges, not altered product fixture data. This is sufficient to judge multi-level investigation without changing scanner or production report semantics.

## Reviews & findings comparison

**Reviews & findings** opens the Findings collection. Accessibility and Theme are subordinate specialist views in the contextual sidebar, each remembering its own filters, selection, inspector tab and pane positions. The accepted Overview, Model, Report and Power Query layouts and palette are unchanged.

1. Filter **Diagnostics → Broken actions** by **Navigation** and **Warning**. Inspect `PBI-NAV-002`, its original recommendation and affected context. **Review required** is a separate, intersecting filter, not a severity. Report/page choices include missing targets recorded by the scanner; model filtering uses explicitly recorded `SemanticModel` values, not inferred report bindings.
2. **Review** shows the original message, suggested review and affected entity. **Evidence** holds source artifacts, evidence paths, rule version, reference links and limitation details. The source action stays visible in both tabs. Filtering can hide the selected finding without discarding its inspector; **Show in collection** clears filters to reveal it. Collection arrows/Home/End, `/` search and inspector tab arrows follow the existing workspace conventions.
3. Search `PBI-MODEL-005`. The selected model column retains **Checks limited**. Evidence distinguishes limitations at that source from other model context; it does not claim every model limitation affects that finding. Open the object, investigate, then **Back to reviews**. Selection, filters, tab, pane positions and the limitation disclosure are restored, along with the Model investigation that existed before the source visit.
4. Search `PBI-QUERY-001`, then **Inspect Power Query**. This opens `DynamicQuery` in its existing Summary inspector; M is still secondary. Following its loaded model-table connection keeps a direct **Back to reviews** route. The previous Power Query and Model investigations are restored on return. Visual and page findings use the existing report workspace; missing targets offer a labelled broader route, never an unrelated replacement entity.
5. **Accessibility** retains five original observations and their assessment types separately from the 28 main findings. **Theme** has two saved observations plus five report theme-status records. Compare saved/theme values and dominant-peer values, or inspect unavailable theme-resource states. Theme records receive no finding severity or review-required state. Theme accessibility observations, where published, remain Theme observations rather than being added to the accessibility finding count.

`review-data.js` and `review-view.js` now own review filtering, source resolution and presentation; their earlier Overview exports remain available to existing internal callers. Overview's rendered surface and counts are unchanged. Source visits capture the existing workspace states and restore them on return, including a deeper query-to-model investigation. No scanner, production code, runtime dependency or palette change was introduced.

This is inspection, not issue management: no acknowledgements, assignments or resolutions are saved. State remains in this browser tab. Theme review is a bounded presentation of saved scanner comparisons/statuses, not a theme editor or rendered accessibility check. A reference without a unique published query/object identity stays evidence or opens only labelled broader context. No complete checking, runtime behaviour or external usage is inferred from absent findings.

## Power Query comparison

Open **Power Query** in the sidebar. The same real scanner snapshot contains 27 M-backed query records: 9 loaded table partitions, 15 supporting expressions and 3 apparently unused expressions. The collection combines M table partitions and named expressions, rather than claiming to reproduce Desktop's query editor. Filter by semantic model, query usage or type, and search names without discarding the selected inspector.

1. Start at **LoadedFactQuery → Dependencies → AddColumnLineageQuery → Fact**. Both directions of the dependency relationship remain in the inspector. Back/forward retraces query selections while the collection keeps its filters; **Show in collection** reveals a selection outside them.
2. **Fact → Summary → Fact** opens that table's existing Model objects context. **Back to Power Query** restores the query, filters, tab and pane positions, and restores the earlier Model selection/filter state. The Power Query sidebar entry provides the same return while a query-origin model investigation is active.
3. **Fact → Dependencies → Column preparation evidence** exposes recognised operation/column evidence behind a disclosure. These are preparation operations, not report usage or a claim of complete column lineage.
4. **LoadedFactQuery → Sources** distinguishes directly recognised connectors from those reached through upstream query edges. Open the originating query in place. **DynamicSourceQuery** has a dynamic/unspecified source location; **DynamicQuery** has dynamic query references. Those are different kinds of uncertainty.
5. **DynamicQuery → M & evidence** shows original M and its original review-required finding. **LocalFileQuery** retains the original source warning. **EstablishedCoverage → Apparently unused → UnusedQuery** retains its orphan review; apparently unused never grants deletion permission. Full M/source literals are secondary, not shown in the default inspector.

Power Query adds `query-data.js` for query identities, bounded graph traversal and evidence scope, plus `query-view.js` for collection and inspector presentation. It reuses the shell, shared primitives and original finding renderer. Each query is identified by model, name, source kind, table and partition. No scanner, production renderer, runtime dependency or palette changes were needed.

The JSON does not publish dependency target partition identity or parameter flags. Ambiguous same-name targets remain separate candidates and stop traversal; the viewer does not pick one. A missing role remains unspecified. Entity partitions can contribute recorded model connections without being invented as M expressions. Source views show recognised connector functions and coarse location kinds, never inferred connectivity, refresh success or complete lineage. Query-dependency limitations remain distinct from semantic-object confidence, and limitations elsewhere in a model are labelled as model context.

Query navigation is in memory. Sources are inspectable per query; there is no separate cross-project source catalogue or canvas graph. Explicit model-table links open the existing filtered object workspace, not a new table-detail design. This is sufficient to compare the workflow without expanding into a query editor.

## Relationships, Security and What was checked comparison

The sidebar now groups **Model objects, Relationships, Security and Lineage** under Model; **Report pages and Power Query** under Report & data; **Reviews & findings** under Review; and **What was checked** under Confidence & scope. Overview remains the entry point. Existing workspace layouts and palette are retained.

1. **Relationships → InactiveRelationship**: compare both ends, cardinalities, saved state and cross-filter direction. Follow the recorded `Fact[InactiveRelationshipMeasure]` activation source into Model, then **Back to Relationships**. This is the scanner's bounded `USERELATIONSHIP` evidence; the relationship remains inactive and no runtime activation is inferred. **Evidence** holds original structural edges and a model-limitation link. Names embedded only in finding prose are not treated as exact relationship identities.
2. **Security → CoverageRlsRole**: inspect the saved `[RlsColumn] = "Allowed"` filter. **Objects** shows explicit security dependencies and model links. Compare **CoverageOlsRole** for column `OlsColumn` and table `TableOlsProtected` permissions. Empty filters are described as no stored row filter, never as verified unrestricted access. **Evidence** retains source paths, unanalysed constructs and matching role-file limitations. Saved definitions do not establish Service membership or effective user access.
3. **What was checked → Recorded limitations**: compare the 12 original records with the separate 29 object results carrying Checks limited. Filter by area/model/reason, inspect Meaning, then open Evidence for source paths and scanner fields. `UnresolvedSortColumn` opens an exact model object; report records open broader report/page context. Imported query-domain limitations route into Power Query and explicitly do not themselves qualify semantic usage. Missing or ambiguous identities offer only broader context.
4. **Evidence available** inventories what the JSON actually contains. **Outside scope** explains permanent static-analysis and JSON boundaries. Neither is an invented checklist of successful checks. No identified limitations remains distinct from everything being checked.

Each new collection keeps the selected item inspectable when filters exclude it. Source visits preserve collection filters, selection, tab and pane scroll positions, including nested visits such as Relationship → model limitations → object. Returning also restores the earlier Model/Report/Query investigation. Sidebar navigation resumes workspace state; the explicit Overview Checks shortcut resets to all recorded limitations.

## Use your own analysis

From the repository root, export with the existing scanner:

```powershell
dotnet run --project src/PbiAssure.Cli -- scan "C:\path\to\your\PBIP-project" --format json --output artifacts/viewer-analysis.json
```

Choose **Open analysis JSON** in the viewer. Schema `0.26` is supported, with a 30 MB prototype input limit. Files are read in this browser tab; there is no upload, persistence, telemetry or external font/asset request. Reloading restores the bundled fixture. Invalid imports retain the previous investigation. Do not put private inventory files in `data/` when sharing or hosting the static build.

## What is implemented

- Persistent application navigation, semantic-model selection and table filters.
- Overview with attention/usage/checks/inventory routes and a small read-only findings/accessibility inspector.
- Scannable object collection with search, usage, confidence and type filters; system-generated objects are optional.
- Independent collection/inspector scrolling; contextual selection, dependency navigation and inspector history.
- Summary/Why, Used by, direct report locations, saved DAX with copy, dependency directions and expandable evidence paths.
- Report/page navigation, visual selection, object types/usage roles, saved behaviour/configuration, accessibility metadata and findings/reviews with source evidence.
- An explicit report-to-model return path and separate page/report context; a scope/limitation view.
- The scanner’s five usage states and separate confidence, including explicit review language for absence-based states.

No report rendering, project picker/scanner integration, review management, runtime access testing or production export workflow is implemented.

## Data provenance

`data/coverage.json` is output from the existing CLI/Core scanner over [`tests/fixtures/pbi-assure-coverage`](../../tests/fixtures/pbi-assure-coverage), generated against the base commit above. This is real **PBI Assure analysis of a synthetic repository fixture**, not an operational customer report and not claimed to be Desktop-authored. The fixture’s [`coverage-manifest.json`](../../tests/fixtures/pbi-assure-coverage/coverage-manifest.json) documents its origin and independent classification expectations.

The snapshot contains 5 semantic models, 5 reports, 19 saved pages and 88 usage objects. The default model has 77 objects with system-generated items excluded. Object states, confidence, direct locations, expressions, graph edges and limitations are preserved. Only `RootPath` is replaced with the repository-relative fixture path to avoid shipping a developer checkout path. Refresh from the repository scanner with `npm run fixture` (requires the repository’s .NET SDK).

## Architecture and adoption trade-offs

Native browser ES modules, semantic HTML and CSS; no frontend framework, backend or runtime packages. `data.js` is a presentation adapter over the existing JSON inventory. `app.js` manages explicit in-memory collection, inspector and navigation state and renders only the active collection/detail context. `styles.css` provides a separate visual language scoped to this experiment.

Report pages adds `report-data.js` for scoped projections and `report-view.js` for report/visual presentation. The small `ui.js` module shares escaped text, icons and object-type glyphs with Model. There is no new data source, scanner change, dependency or production integration. Report/page identity uses the report-relative path and page name; visual selection is scoped to that page. The return context records the earlier Model state and retains the Report state independently while an object is investigated.

Overview adds `overview-data.js` for counts, finding scopes and bounded source routes, plus `overview-view.js` for Overview and its findings destination. It reuses the existing finding-detail renderer. Counts are derived from the loaded scanner arrays rather than stale serialized summary counters. Sidebar navigation records pane scroll positions; explicit shortcuts set ordinary existing workspace state. No backend, runtime dependency or analysis changes were introduced.

`model-area-data.js` and `model-area-view.js` share the Relationship/Security collection and inspector shell. They retain model-scoped identities, original inventories, structural/security edge kinds and original activation annotations. `scope-data.js` and `scope-view.js` replace the initial coverage placeholder with limitation investigation and continuous evidence/scope views. A small shared source-visit snapshot in `app.js` restores nested investigation context; existing workspace-specific navigation remains intact. New inventories are validated before transactional import. The static build includes these modules; no framework, runtime package, Core or public JSON change is introduced.

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
- Overview's authored usage scope excludes system-generated-table objects. Checks limited counts all published object results; recorded limitations are a separate inventory and do not imply every limitation qualifies every object. Other reviews retain separate accessibility and theme meanings.
- The findings collection is an inspection destination, not review management: no acknowledgement, assignment, resolution or modification is saved. Source routing uses the existing Model/Report/Power Query workspaces where a published identity supports it; other findings retain evidence in the inspector. Theme is a specialist saved-observation/status view; its Overview disclosure remains unchanged. Large-project pagination, persistent state and a full accessibility audit remain outside this prototype.

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

Proportionate checks: thirteen data tests and nine browser workflows. These cover the existing Model/Report behaviours plus Overview count reconciliation, authored scope, severity/review overlap, separate accessibility, filtered routes, source return, retained investigation state, keyboard interaction and zero-findings/zero-limitations honesty. Desktop captures are written to ignored `artifacts/modern-viewer/`. Manually reviewed 1440×900 and 1280×900 captures and exercised Overview routes in the browser. Production suites and exhaustive narrow-width/fixture/performance matrices were not run.

The Power Query slice adds four focused data checks (real evidence, ambiguous partitions, cyclic/missing edges, limitation/column scope) and three browser workflows (query/model return, source/dynamic evidence, keyboard/scroll/import context). Its targeted run also checks one existing Overview and one Report/Model navigation workflow, plus the static build. Desktop captures at 1440×900 and 1280×900 are reviewed; no production regression suite or size benchmark is run.

The Reviews slice adds four focused data checks and four browser workflows covering context-filter intersections, unique source identities, specialist semantics, secondary/escaped evidence, source qualifications, direct/nested source return, earlier workspace restoration and keyboard controls. The targeted run includes the existing Overview checks and one Power Query plus one Report navigation workflow, and the static build. Desktop captures at 1440×900 and 1280×900 are reviewed. Production regression suites and size benchmarks are not run.

The Lineage slice adds five graph/data checks and four browser workflows covering real multi-level traversal, shared-node convergence/collapse, function/structural/report-measure nodes, endpoint grouping, unresolved/ambiguous boundaries, actual table fan-out, synthetic cycles, keyboard navigation, recenter history and source returns. Its targeted run includes one existing Overview, Power Query, Report and Reviews workflow, plus the static build. Desktop captures at 1440×900 and 1280×900 are reviewed; no exhaustive repository suite, mobile matrix or size benchmark is run.

The remaining-core slice adds six focused data checks and four browser workflows for relationship activation/identity, row versus object permissions, confidence/limitation counts, scoped routes, nested returns, keyboard interaction, no-limit honesty and transactional imports. Imported query/role boundary cases are test-only and do not alter the bundled scanner snapshot. Its targeted run includes one existing workflow each for Overview, Model/Report, Power Query, Reviews and Lineage, plus the static build. Desktop captures are reviewed; no repository-wide regression, benchmark or mobile matrix is run.

`npm run build` creates an ignored `dist/` with static files. This can be hosted at any static origin or subdirectory. No production site or hosting configuration is modified; this branch has not been remotely deployed.

## Assessment

Yes: this direction materially improves object/dependency investigation. The stable collection and reversible inspector navigation make it easier to keep an investigation’s starting point while exploring evidence. The benefit comes primarily from application state and a persistent layout; it does not require React or a replacement analysis engine. This prototype is enough to compare that direction, but does not establish that it should replace the portable generated report or that every feature will benefit equally.

The workspace model also holds up for Report pages: page context stays visible while visual details change, and the explicit return path makes crossing into the Model workspace reversible. A visual collection works better here than treating every visual as a new document section. The remaining question is how well this scales to larger real reports and richer visual metadata; this slice is sufficient for a hands-on comparison without rebuilding the whole report experience.

Overview strengthens this direction by providing a project-level starting point with consistent routes into the existing investigation workspaces. Its value comes from scope-correct navigation and retained context, not more headline metrics. The findings inspector is intentionally small, and the lower-priority snapshot/other-review material may require a short scroll at desktop sizes. This is sufficient to compare the analytical home-screen direction without turning the prototype into a full review application.

Power Query also fits the workspace model. Keeping query selection separate from list filters supports comparison and dependency investigation, while the explicit model return avoids a chain of pages. The extra inspector width helps relationships and source evidence more than a wide metadata table would. Inventory-scale performance and a full query graph remain unproven; this slice is enough for hands-on judgement.

Reviews strengthens the workspace model: findings provide an evidence-led entry into existing investigations, with a single return path that preserves both review and earlier workspace context. A persistent inspector accommodates original recommendations and secondary evidence without making the collection a technical report. Findings stay primary while specialist semantics remain distinct. This slice is enough for hands-on comparison without extending into issue tracking.

Lineage is materially better than immediate Uses/Used by plus one report path for multi-level investigation: the centre stays fixed, converging dependencies remain shared, every recorded branch is explorable and report locations aggregate across all downstream branches. The trade-off is less global spatial topology than a canvas graph, and the JSON cannot support precise report-scoped lineage everywhere. Those limitations are exposed rather than filled with inferred edges or placements.

Relationships and Security fit the same collection/inspector approach without needing a diagram or permission matrix. What was checked benefits from a limitation inspector plus continuous evidence/scope views rather than forcing everything into a list. The weaker area is still confidence breadth: this JSON exposes limitations and available evidence, not an exhaustive successful-check register or per-limitation affected-object mapping. Security also depends on a small saved-definition inventory, with no runtime access proof. These are explicit product/data limits, not filled with inferred assurance.
