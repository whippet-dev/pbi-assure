# Handover

Tactical entry point for an incoming coding agent. Read this first, then
[CURRENT_STATE.md](CURRENT_STATE.md), then [DECISIONS.md](DECISIONS.md).

## What just happened

The **Web newcomer-orientation** slice is implemented and must remain **uncommitted for review**.
Analyse `/` has a compact introduction and four outcomes above the unchanged preparation/picker flow.
The new `/about` page explains the product, supported analysis, outputs, workflow, limitations and privacy.
`Shared/AppNavigation.razor` uses ordinary links with current-page semantics; information opens in a
clearly labelled new tab when Home has a selection, inventory or busy state. No persistence or new state
service. `App.razor` uses standard heading focus on route changes.

Focused Web **15/15**, full Core **562/562**, privacy/navigation **4/4**, Release **0 warnings / 0 errors**
and diff check passed. Desktop/mobile visual review of the published local Web build is complete at
1440×1000 and 390×844. Privacy evidence: zero scan/export requests and no unexpected/external requests
or canary leaks; online HTML viewing retains its three static viewer requests. The new navigation tests
also check static-only information-page requests, keyboard focus and retention of selected/scanned state.
The test-only host's SPA fallback must remain after static-file handling, or it intercepts the viewer.
Use `MSBUILDDISABLENODEREUSE=1` and `DOTNET_CLI_USE_MSBUILD_SERVER=0` in the local test process if publish
output streams stall; these are invocation settings, not repository configuration changes.

**Next task to decide separately:** Web summary-count/order alignment. Home still includes accessibility
in its aggregate findings and uses Assurance-first ordering, unlike generated HTML. This is deliberately
unchanged, as are post-scan actions, all exports, Core/Reporting and JSON `0.26`. No formatter cleanup.

The approved **HTML information-architecture** slice was committed and pushed at `8f09c27`. Navigation
and body now prioritize Summary -> Semantic model -> Power Query -> Model relationships -> Report pages
-> Findings -> Analysis coverage -> Theme Review -> Accessibility review. Conditional Security roles
remains after relationships; coverage remains conditional. Summary groups are Semantic usage -> Project
-> Power Query -> Assurance, with balanced metric sizing and no Assurance-only panel emphasis.

The six scope caveats are preserved in a Summary disclosure, **Important limits before acting on this
report**; the apparently-unused caution remains visible outside it. Desktop navigation uses three
columns for the current nine tiles. Existing IDs, finding content/counts, specialist boundaries, mobile
auto-fit, filters, JSON and exports remain unchanged. Exports remain app actions, not report sections.

The ignored local artifact is `artifacts/sales-returns-ia-review.html`. Code/source inspection and an old
Release comparison confirmed identical non-Summary section bodies and IDs; sample counts still match
65/15/7/9/34 semantic usage and 2/12/13/27 Assurance. **Visual review is still needed:** the Browser tool
blocked the local-file URL, and no alternate route was used. Do not claim this sample was visually reviewed.

Validation: focused reporting **52/52**, full Core **559/559**, privacy E2E **2/2**, Release **0 warnings /
0 errors**, `git diff --check` passed. Privacy coverage now exercises the Summary disclosure with Enter
in the existing online/offline report workflow. No formatter-baseline cleanup.

The **description retention** slice was approved and committed at `998adb0`. The sanitised model-only
fixture `tests/fixtures/desktop-descriptions-sanitized` preserves Desktop-authored Table/Column/Measure
description blocks and neighbouring undescribed controls. Phil confirmed normal-UI authoring and a
successful save/close/reopen/save/close; no first-save snapshot exists. Descriptions use contiguous `/// `
lines immediately preceding the declaration at its indentation, including a blank measure-description
line and an authored trailing space. The dedicated Core reader retains spaces, normalizes logical
newlines to LF, and populates nullable `[JsonIgnore]` inventory properties only. No JSON (`0.26`),
classification, provenance, HTML, UI or CSV contract changes occurred in that slice. See the fixture README.

The approved follow-up now adds optional **Description** to Data Catalogue only, using retained
Column/Measure inventory metadata keyed by model/table/name/type. No inference or table rows; null
becomes blank, and the shared CSV writer preserves/quotes multiline content. Defaults are unchanged.
Usage Mapping, legacy CSV, JSON `0.26`, semantic analysis and UserFacing are unchanged. Both Export
Builder shells consume Reporting's allowed/default columns automatically; no frontend changes were
needed. This follow-up was committed at `a2e8c6b`; do not make Description default-on.

Optional-column validation: focused **34/34**, full Core **557/557**, Release **0 warnings / 0 errors**,
`git diff --check` passed. Default catalogue, Usage Mapping, legacy CSV and JSON were byte-compared
against the pre-change Release binaries on the fixture (fixed scan timestamp for JSON). The local,
ignored review CSV is `artifacts/description-catalogue-review/pbi-descriptions.data-catalogue.csv`.
No browser-visible code changed, so privacy E2E was not required. No formatter baseline changes.

Description-slice validation: **14/14** description tests; **86/86** focused parser/export tests (inclusive);
**553/553** full Core; Release **0 warnings / 0 errors**; `git diff --check` passed. JSON and all three CSV
contracts have equality regressions, including optional columns and a report-used fixture. No privacy
E2E run was needed because serialized/browser-visible output was unchanged in the retention slice.

All four **Export Builder** implementation slices are complete; Desktop UI was committed at `00c4fc9`.
Core derives normalized direct semantic usage records and v1 object summaries for non-system-generated Columns
and Measures only. They retain semantic identity/state/confidence, report path, persisted page/visual IDs,
visual type, context/role and raw source evidence without reconstructing data from HTML or legacy CSV text.
`UserFacing` is a separate export-only three-state value: active projections/tooltip data/drillthrough and
active rendered formatting are **Yes**; filter-only, sort-only and selector-supporting evidence are **No**;
direct `Other` is **Unclear**. Yes has object-level precedence over Unclear. Hidden canvas/group state is
intentionally irrelevant to this feature; the existing effective-visibility logic is accessibility-only.
Reporting now provides fixed `DataCatalogue` and `UsageMapping` requests/renderers over that Core result.
The Data catalogue emits one eligible object per row (including zero-use objects) with the documented
default counts/contexts and optional labels/roles/reason. Usage mapping emits one normalized direct record
logical usage per row and retains every direct context, including filter/sort/Other. Low-level parser evidence
is grouped by semantic identity plus report/page/visual IDs/context/role: `EvidenceCount`, `ArtifactPaths`
and `EvidencePaths` recover it in advanced columns; default rows are not duplicated just because one usage
has several JSON evidence paths. Data-catalogue `DirectUsageCount` uses that logical grouping; its other
location counts remain machine-identity based. Usage mapping has a friendly **Visual** label using the
established title/on-canvas-text/visual-type fallback, while `VisualId` remains its machine identity.
`ExportPresetCatalog` validates fixed allowed columns and defaults, rejects invalid/duplicate selections, and the shared CSV writer preserves
legacy comma/CRLF/RFC escaping/formula-neutralisation. The legacy `SemanticUsageCsvRenderer` remains
byte-compatible with its prior header and behaviour. No JSON schema, CLI or semantic
classification changed. The Web app provides a transient UI: after a successful scan,
**Export CSV** opens a compact fieldset-based panel for Data catalogue or Usage mapping. It gets all
allowed/default columns from `ExportPresetCatalog`, resets selection on preset or scan changes, calls
`ExportCsvRenderer`, and downloads BOM-prefixed project-named CSVs through the existing local browser
download path. **Download semantic usage CSV** remains separately labelled and unchanged. The Desktop shell
now retains the latest successful `ProjectInventory` only in memory and clears it immediately when a new
scan starts, a project changes or a scan fails. **Export CSV…** is disabled until a successful scan and opens
a compact modal: fixed presets/defaults/allowed columns come directly from Reporting; saving uses
`ExportRequest` and `ExportCsvRenderer` without re-scanning and writes BOM-prefixed CSV through the standard
Save dialog. The shared filename convention is `<project>.data-catalogue.csv` / `<project>.usage-mapping.csv`;
legacy **Open semantic CSV** remains unchanged. Do not duplicate Reporting contracts or move export
mechanics into Core.

Desktop-slice validation: focused export/Desktop surface tests **12/12**, full Core suite
**539/539**, privacy end-to-end **2/2**, and full Release build **0 warnings, 0 errors**. `git diff --check`
passes. The known unrelated formatter baseline remains 24 findings and was not changed.

Accessibility findings are now a supporting review rather than part of the main assurance surface. The
renderer partitions existing `Accessibility` category findings in-process: the top-level **Accessibility
review** navigation/section starts with an existing-rule summary (affected visuals, items or pages) and
then preserves every individual finding's location, suggested action and technical evidence. The main
**Findings** section, its catalogue/filter surface and Assurance headline counts contain only
non-accessibility findings. Empty primary and accessibility states are independent. This is explicitly
not a WCAG compliance verdict. Rule IDs/versions, effective-visibility/group/tab-order semantics,
analyzers, JSON schema/output and CSV are unchanged.

Auto Date/Time relationship provenance is now distinct without weakening model structure. A relationship
whose target is an exactly annotated `__PBI_LocalDateTable` retains both endpoint edges and creates a
system-generated root path. An object reachable only from that path remains `StructurallyRequired`, but
its HTML explanation says **Required only by Power BI-generated Auto Date/Time structure**. Any ordinary
relationship or other structural root preserves normal structural presentation. Name-shaped, hidden or
template-only tables are not sufficient. The marker is internal-only; JSON schema/output shape, CSV and
the five usage states are unchanged. Model-level Auto Date/Time state, variations and `joinOnDateBehavior`
remain intentionally out of scope because the target table marker is enough for this bounded distinction.

Validation: the focused semantic dependency/date-fixture selection passed **84/84**, the full Core suite
passed **519/519**, both privacy end-to-end tests passed, and the Release build completed with 0 warnings
and 0 errors.

Accessibility findings now follow Power BI Desktop's effective canvas visibility. Desktop 2.157.879.0
runtime evidence showed that hidden items are filtered, their components are not retained, and descendants
of a hidden group are likewise unavailable for focus. The shared group hierarchy resolver now combines an
item's direct `isHidden` state with every resolved ancestor; missing, ambiguous and cyclic ancestry stays
conservatively ineligible. `PBI-ACCESS-001` 1.1.0 and `PBI-ACCESS-002` 1.2.0 consume that result, while
immediate group scoping and nested hierarchy are unchanged. Duplicate-rank copy now says only that an
equal-ranked item may be skipped. `PBI-ACCESS-003`, JSON schema `0.26`, CSV and rendering are unchanged.

Validation: the focused accessibility/tab-order selection passed **65/65**, the full core suite passed
**518/518**, both privacy end-to-end tests passed, and the Release build completed with 0 warnings and
0 errors.

The paired Desktop-authored incremental-refresh fixtures now prove the load-bearing distinction between
parameter filtering and a configured policy. Both tables use `RangeStart`/`RangeEnd`; only the configured
table has a persisted `refreshPolicy`. PBI Assure retains that explicit table-owned policy in additive JSON
schema `0.24`, shows it on the semantic table, and treats the one qualified polling column as structurally
required. It adds no Finding and makes no folding, Service refresh or partition-health claim. See
[the evidence review](../reviews/incremental-refresh-policy-evidence-2026-08-21.md).

The Desktop-authored `desktop-userelationship-evidence` fixture now proves one active relationship and
three inactive controls: shipping is **Activated by report-used DAX**, referral is **Referenced only by
unused DAX**, and legacy has **No USERELATIONSHIP call found in analysed DAX**. PBI Assure extracts only
the built-in call with exactly two explicit qualified columns, resolves one exact unique relationship pair
including reversed argument order, and reuses source reachability. The additive relationship inventory is
schema `0.25`; CSV, Findings and semantic usage states are unchanged. See
[the evidence review](../reviews/userelationship-inactive-relationship-evidence-2026-08-21.md).

TMDL triple-backtick fenced expressions are now read centrally rather than being retained as the opening
fence. Power BI Desktop's serializer can use this form to preserve expression whitespace, including for
RLS `tablePermission` filters. The shared declaration and assignment readers now consume through the
closing fence, remove only the fence's structural left boundary, preserve relative whitespace and stop
before following properties or objects. The generic unanalysed-child scan also skips fenced bodies, so
DAX such as `VAR` is not misreported as unsupported role metadata. This was verified with synthetic
RLS, measure, UDF, calculation-group and M-partition cases; the real work report that prompted it was
not copied into the repository.

The encountered-PBIR-schema compatibility policy is now implemented for the report artifacts PBI Assure
parses. The parser still reads known properties and silently ignores unknown ones; a structured
`ReportSchemaObservation` records its schema evidence without gating parsing. The committed Desktop
fixtures establish an exact baseline for
`definitionProperties/2.0.0`, `report/3.3.0`, `pagesMetadata/1.1.0`, `page/2.1.0` and
`visualContainer/2.11.0` and `visualContainer/2.12.0`. They also contain `versionMetadata/1.0.0` with PBIR definition version `2.0.0`,
which is now retained separately from the existing `definition.pbir` version. The paired Desktop bookmark
fixtures add exact `bookmarksMetadata/1.0.0` and `bookmark/2.1.0` evidence; `reportExtension/1.0.0`
remains synthetic-only.

The adopted boundary is conservative: exact fixture-backed versions are the verified baseline; another
version in a recognised family is unverified, not automatically unsupported; missing, malformed and
unknown-family schema metadata are separate states; and PBIR-Legacy is a separate format rather than an
old modern-PBIR schema. These states describe PBI Assure's coverage, not defects in the user's project.
Non-exact declarations appear as neutral report-scoped **Analysis coverage** information, never as a
Finding. Exact declarations are silent. Bookmark schemas are exact-verified; report-extension schemas
remain recognised-unverified because there is no committed Desktop baseline. The JSON inventory gained
additive report `SchemaObservations`, `VersionMetadataPath` and `PbirDefinitionVersion` properties; CSV
is unchanged. See
[the compatibility review](../reviews/encountered-pbir-schema-compatibility-policy.md).

Report page cards now always start collapsed, including the page that was active when Desktop last saved
the report. A valid explicit `landingPageName` is instead surfaced quietly as a visible **Landing page**
badge on the matching collapsed page card, and is searchable as landing-page metadata. `activePageName`
remains inventory-only saved authoring state; it has no page-card label or automatic expansion. No page
is labelled when no landing page is configured or when the configured target is missing — that remains
the scoped `PBI-NAV-017` Finding. Validation: Release build clean, **436 core + 2 privacy E2E tests
passed**.

Configured custom-theme resources are now checked narrowly for integrity. `PBI-COMPAT-002` is a
Warning / Finding only where `definition/report.json` explicitly names `themeCollection.customTheme`
and its selected local resource cannot be resolved or read. It does not assess theme quality,
consistency, accessibility or completeness; sparse valid themes, base-only reports and unselected
registered resources remain silent. `ThemeSourceInventory.ResolutionOutcome` is an additive JSON field
that records the machine-readable resolution result, so the rule never relies on `ResolutionIssues`
diagnostic prose. The synthetic tests cover no configured theme, resolved sparse themes, unavailable
package items/files, invalid JSON, ambiguous resources, unselected resources, multi-report scoping and
HTML escaping. Validation: Release build clean, **433 core + 2 privacy E2E tests passed**.

Explicit report landing pages are now parsed and checked. Desktop writes the optional
`landingPageName` property only when a page is set as the landing page; it is separate from
`activePageName`, which remains saved authoring state. The paired Desktop fixtures preserve both valid
states: an explicit Page 3 landing page while Page 2 is active, and no landing-page property at all.

`PBI-NAV-017` is an Error / Finding for an explicit nonblank landing-page target that no longer exists.
It provides the internal target name and `pages.json` evidence, with a Desktop-oriented recommendation
to choose an existing landing page or reset the setting. No landing-page property is valid and silent.
The broken-target case is deliberately synthetic; Desktop persistence of stale landing-page metadata is
not claimed. `LandingPageName` is an additive JSON inventory property; semantic usage and CSV are
unchanged. Validation: Release build clean, **426 core + 2 privacy E2E tests passed**.

A compact **Security roles** review is now part of the generated HTML whenever a semantic model defines
roles. It groups roles by model, shows model permission, retained row-level filter DAX, table-level
metadata permissions and explicitly named column permissions, and keeps technical source paths behind
disclosure. Models, roles and filters are ordered deterministically; long and multiline expressions wrap
safely at desktop and mobile widths.

This is an inventory/review surface, not a security verdict. It adds no findings. Explicit column-level
OLS can legitimately make the named semantic object `StructurallyRequired`; table-level OLS never makes
all child columns used. The role/permission inventory is additive JSON and the semantic-usage CSV schema
is unchanged. The page explicitly says that PBI Assure cannot see Power BI Service role membership,
assess effective runtime identity, confirm the overall security design, or determine access through other
paths.

The first post-confidence feature slice is complete: `PBI-MODEL-005` surfaces **Reference not found**
Warnings for evidence-safe unresolved semantic dependencies. It does not surface every retained
`UnresolvedSemanticDependency`. The producer audit found that provenance quality differs materially:
structured sort-by, hierarchy, relationship, perspective and report-measure metadata can support the
claim that PBI Assure could not find an explicitly named target; DAX and field-parameter text extraction
cannot support the same claim without a caveat; and `TablePermission` currently mixes both forms under
one kind.

The public gate is therefore intentionally narrow: eligible structured kind **and** a structured
`NotFound` resolution outcome. `Reason` is retained as explanatory diagnostic text and must never decide
whether a finding is shown. Ambiguous matches are represented structurally and suppressed. Findings are
scoped and grouped by model and source object, ordered deterministically, encoded by the existing Findings
renderer, and appear through the normal Findings search, rule filter and rule catalogue. Analysis coverage
remains about PBI Assure limitations; this rule remains about the user's artifact.

Validation for this slice: Release build clean, **413 core + 2 privacy E2E tests passed**. Four deliberate
mutations proved prose independence, ambiguity classification, producer-evidence safety and the two-part
gate. Rendered HTML was compared before and after the remedial change. No committed Desktop fixture
contains a broken reference, so persistence of these malformed states through a Desktop save remains
unproven and is not claimed.

The semantic usage/classification workstream described below remains closed and unchanged.

Analysis limitations and classification confidence are now visible to a reader. They were recorded
internally for several slices and surfaced nowhere, so an object could show a flat "Apparently unused"
when the scan knew it was "apparently unused, given metadata nobody read".

The design is split by scope, because the hard constraint is noise rather than discoverability. One
unanalysed construct qualifies 21 of 27 objects in the Desktop fixture, so an **Analysis coverage**
section states the cause once per model, and each affected object carries only a small **Qualified**
link back to it. Limitations are grouped by construct rather than by file, since a model emits one file
per role.

Human review of the rendered output then found the architecture sound but the wording still written in
PBI Assure's own vocabulary, so a follow-up slice translated it. The object marker changed from
**Qualified** — precise, but it made readers ask "qualified how? is my column the problem?" — to
**Usage check incomplete**, which names what is incomplete and attributes it to PBI Assure rather than
to the user's object. One vocabulary now runs through the surface; see [CURRENT_STATE.md](CURRENT_STATE.md)
for the full mapping. Two visual defects were fixed with it: navigation wrapped 8 tiles into an orphaned
row, and the coverage disclosure lacked the report's standard `+`/`−` affordance.

Then the "Why" line was fixed. `Sales[Amount]` read *Indirectly used* with **"Referenced by [TotalOf]"**,
naming an uncalled function. The edge was real; the explanation was not. `DescribeReason` took the first
matching incoming edge, which answers "what references this?" rather than "what supports this state?".

The classifier already computed the answer and threw it away: it builds the sets reachable from report
roots and from model-structure roots, assigns states from them, then discarded both. They are now
published as `SemanticNodeReachability` and the reason is filtered by them. Classifications and the
dependency edge set are byte-identical before and after — only the shown evidence changed.

A closeout pass then audited every reason kind against that invariant. The one remaining case —
a relationship endpoint that a report also reaches — was confirmed by synthetic model and fixed: the
relationship explains `StructurallyRequired` only, and every other kind is gated by the same
reachability check. Wording and precedence order are unchanged.

The confidence and coverage presentation reads `ClassificationConfidence` and never re-derives it; the
reason selection reads published reachability and traverses nothing. Neither reimplements
classification.

## This workstream is complete

**The semantic-usage / analysis-confidence sequence is stable and closed.** Everything below is
implemented, fixture-backed where evidence was needed, and verified:

| | |
|---|---|
| `AnalysisLimitation` detection and the construct registry | done |
| `ClassificationConfidence` propagation | done |
| RLS table-permission dependencies | done |
| Artifact-sensitive role limitation precision | done |
| Perspective member dependencies | done |
| DAX user-defined function dependencies | done |
| UDF model-measure consumer fixture | done |
| User-facing **Analysis coverage** section | done |
| Plain-language confidence terminology | done |
| Classification-compatible usage reasons | done |
| Final reason-precedence consistency | done |

Do not reopen any of it without new evidence. The invariants that hold it together are in
[DECISIONS.md](DECISIONS.md) — read them before changing presentation of usage, confidence or reasons.

A remedial consistency pass audited the finished sequence — CI bookkeeping, domain comments, fixture
provenance, the new JSON field, terminology, reason invariants and fixture hygiene. It found three stale
documentation items and no correctness defect, so **the workstream stays closed**. Details in
[CURRENT_STATE.md](CURRENT_STATE.md); the notable one is that
`tests/fixtures/desktop-udf-references/README.md` had claimed its model was written at compatibility
level 1702 with no upgrade. That was inferred from a snapshot taken *after* the functions were authored
and could not have shown the starting level. Corrected to the 1606 → 1702 upgrade the sibling fixture
records.

## State

- **Last verified product state:** `b47881424e4b320cefa45161f3a3c72ab39b2fe8` — semantic aggregation mappings.
- **Working tree:** expected clean apart from untracked local review documents; no tracked modifications
- **Verified locally:** Release build succeeded with 0 warnings; **508 core + 2 privacy tests passed**
- **Known exception:** `dotnet format --verify-no-changes` fails with 24 pre-existing whitespace errors
  in two Theme Review files. Unrelated to current work, deliberately not fixed. See
  [CURRENT_STATE.md](CURRENT_STATE.md).

## Immediate next task

The product backlog was freshly re-ranked on 2026-08-21 after the schema, navigation, RLS, UDF and connector
evidence work. The decision and the scored top five are in
[the product-value re-rank](../reviews/product-value-rerank-2026-08-21.md).

The role/perspective structural-usage explanation is complete: the existing graph/reachability evidence
now supplies concise `Why` text naming the persisted role filter or perspective. It is presentation-only;
usage states, reachability and confidence remain unchanged.

Bookmark-only graph edges also remain parked: paired Desktop fixtures prove that stale/inert bookmark
snapshots can retain field references after the live carrier is removed, and effective carriers were already
found through normal live metadata. See
[the bookmark evidence review](../reviews/bookmark-semantic-usage-evidence-2026-08-21.md).

The TMSL `model.bim` input boundary is complete: paired Desktop fixtures prove a current local TMSL
project and its explicitly upgraded TMDL companion. PBI Assure does not parse TMSL. The shared scanner
rejects a local `model.bim` semantic model before rules or output generation, avoiding a false
`PBI-MODEL-001`; a model containing both TMSL and TMDL definitions also stops as ambiguous. This does not
change existing remote `byConnection` handling. See [the evidence review](../reviews/tmsl-model-bim-desktop-evidence-2026-08-21.md).

Aggregation mappings are now complete. A Desktop-authored evidence project established column-owned
`alternateOf` metadata; the committed sanitised fixture pins exact qualified resolution, structural usage
and direct-use precedence. The feature proves deliberate model structure only — not runtime aggregation
selection, performance or Service behaviour. See [the evidence review](../reviews/aggregation-alternateof-desktop-evidence-2026-08-21.md).

**Mobile semantic-reference support is complete.** A sibling `mobile.json` is read only for active
semantic expressions, which merge into the visual's existing direct-usage evidence. Mobile layout remains
outside the product surface: there is no mobile inventory, output, CSV or Finding. The exact
`visualContainerMobileState/2.7.0` schema is fixture-backed; see
[the evidence review](../reviews/mobile-semantic-reference-desktop-evidence-2026-08-22.md). Do not begin
the next backlog candidate without new evidence.

**Desktop formatting semantic-reference audit is evidence-banked.** Dynamic titles/subtitles, conditional
colours/background, analytics lines/error bars and rule-based icons already flow through existing PBIR
field-reference extraction. The sanitised fixture and review confirm no implementation is required; do
not reopen these tested shapes without a new persisted expression form.

**KPI and measure Detail Rows dependencies are complete.** The round-tripped Desktop fixture establishes
all three measure KPI expression properties and a measure-owned multiline `detailRowsDefinition`.
PBI Assure keeps those expressions in process only and feeds their references through the ordinary DAX
graph path, so metadata-only targets become `IndirectlyUsed` from a report-used owner with the normal
**Why: Referenced by …** explanation. JSON schema `0.26`, JSON shape, CSV, Findings and the five usage
states are unchanged. The sanitised fixture and boundary are in [the evidence review](../reviews/kpi-detail-rows-desktop-evidence-2026-08-24.md). Do not add table-owned Detail Rows without new Desktop evidence.

Connector expansion and report-level-measure → UDF traversal remain parked on the evidence already recorded.
The tested **Add a local model** path migrated valid report measures into local model measures rather than
preserving a local/report-measure mixed state; `EXTERNALMEASURE` is retained only as future evidence.
`PBI-ACCESS-001`'s decorative-intent handling remains unchanged pending independently authored intent
evidence. Its effective-visibility applicability is now settled separately. Visual-calculation parsing is
not a current top-five task.

## Do not do yet

- Block-level or property-level limitation detection
- Further registry classification or impact changes without new evidence — the always-present files were
  corrected on the evidence recorded in `SemanticDefinitionFileRegistry`; `dataSources.tmdl` stays
  `DependencyEffectUnknown` until it is actually observed
- Malformed-TMDL recovery
- CSV or browser-app surfaces for limitations and confidence — HTML has one; the CSV header is a
  fixed contract and widening it deserves its own decision
- Report-level measure expression parsing or report-measure → UDF traversal without trustworthy source
  model metadata. A persisted expression in a remote report is not proof of a valid dependency.
- Decorative visual-type exemptions in `PBI-ACCESS-001` without independently authored, author-labelled
  evidence. The local sample measurement found only 13 plausible decorative candidates among 216
  representative findings; 22 text boxes remain metadata-uncertain. See
  [the measurement](../reviews/access-001-alt-text-measurement.md).
- The pre-existing `dotnet format` whitespace cleanup

Reasons are recorded in [CURRENT_STATE.md](CURRENT_STATE.md) and [DECISIONS.md](DECISIONS.md). Two are
blocking rather than merely sequenced:

1. **The culture impact value rests on a design decision**, not an observation — translations are treated
   as describing objects rather than consuming them. Everything built on qualification inherits that.
2. **`PBI-ACCESS-001` needs better intent evidence before its decorative handling changes.** The local
   sample measurement did not support a blanket visual-type exemption. Collect independently authored,
   author-labelled decorative examples before changing that part of the rule.

## Missing evidence

- Role-security forms beyond the committed Desktop fixtures — cross-table filters, other OLS permission shapes
- Trustworthy source-model metadata for a `byConnection` report, or a separately observed Desktop state
  that genuinely retains valid report measures alongside a bound local model. The tested **Add a local
  model** transition did not do so: it migrated valid measures into local TMDL measures. See
  [the evidence review](../reviews/report-level-measure-udf-fixture-design.md).
- Whether a *translated* culture file names model objects, and whether Q&A synonyms constitute usage
- Whether `dataSources.tmdl` is ever emitted by current Desktop
- Independently authored, author-labelled `PBI-ACCESS-001` examples, particularly decorative shapes,
  images and text boxes

## Reading order

1. This file
2. [CURRENT_STATE.md](CURRENT_STATE.md) — what is true now
3. [DECISIONS.md](DECISIONS.md) — what not to reopen
4. `../../tests/fixtures/desktop-semantic-constructs/README.md` and
   `../../tests/fixtures/desktop-udf-references/README.md` — the Desktop evidence, and its limits
5. [../reviews/unsupported-construct-slice1-registry-correction.md](../reviews/unsupported-construct-slice1-registry-correction.md)
   — what slice 1 does and why the registry looks as it does
6. [../design/unsupported-construct-design.md](../design/unsupported-construct-design.md) — only if
   deeper architectural context is needed

Everything needed is in this repository; no external document is required. Do not read every historical
audit before starting a small task — [../reviews/](../reviews/) is there when you need it, not before.

## Before you finish a task

1. Run the build and the tests appropriate to the change; for anything touching scanning or output, run
   the whole suite including the privacy end-to-end tests.
2. Commit logical changes separately, with a message explaining the reasoning, not just the diff.
3. Update [CURRENT_STATE.md](CURRENT_STATE.md) if the factual state changed — build, tests, CI, what is
   implemented, or an evidence gap. A documentation-only commit does not require an update.
4. Update this file with the next task.
5. Update [DECISIONS.md](DECISIONS.md) **only** when a durable decision or established semantic actually
   changed — not for transient observations.
6. Write a task-specific document only when the task genuinely merits one.
7. Never leave a decision recorded only in chat history. Chat is disposable; this repository is the
   project memory.
