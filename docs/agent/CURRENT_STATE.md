# Current state

Factual snapshot for an incoming coding agent. Links to detail rather than repeating it. Update this
file whenever the factual state changes.

Evidence labels: **[verified]** checked by running or reading something · **[inferred]** reasoned but not
evidenced · **[design decision]** a choice, not a fact.

## Repository

| | |
|---|---|
| Remote | `whippet-dev/pbi-assure` |
| Branch | `master` (also the default branch) |
| Last verified product state | Description retention committed at `998adb0`; optional catalogue Description pending review |
| Working tree | Optional Data Catalogue Description slice is uncommitted; re-check before committing. |

`master` may have moved past that commit for documentation-only changes. Re-verify and update this
section whenever a commit changes build, test or behaviour — not for every commit.

The current evidence-led backlog is re-ranked in
[product-value-rerank-2026-08-21.md](../reviews/product-value-rerank-2026-08-21.md). Explicit TMDL
`alternateOf` aggregation mappings and measure KPI/Detail Rows expressions are now fixture-backed
dependencies. Table-owned Detail Rows remains evidence-gated; aggregation metadata is not
runtime-performance evidence.

## Semantic descriptions (in-process only)

**[verified by Power BI Desktop-authored fixture]** `desktop-descriptions-sanitized` banks the observed
same-indentation, contiguous `/// ` description lines immediately before Table/Column/Measure declarations.
Phil confirmed normal-UI authoring and save/close/reopen/save/close; no first-save byte snapshot exists.
Multiline measure text includes an empty line and a significant trailing space. Undescribed controls
have no description block. See [fixture evidence](../../tests/fixtures/desktop-descriptions-sanitized/README.md).

**[design decision]** Core retains nullable `Description` init properties on only `SemanticTableInventory`,
`SemanticColumnInventory` and `SemanticMeasureInventory`. A dedicated preceding-block reader preserves
content spaces and empty lines, joining logical lines with LF. All three properties are `[JsonIgnore]`:
JSON schema remains `0.26`; semantic classification, provenance and HTML are unchanged.
The retention slice was committed at `998adb0`. No missing-description rule is introduced.

The follow-up slice now exposes **Description** only as an optional Data Catalogue column, off by
default. Reporting resolves Column/Measure metadata by model/table/name/type without adding metadata to
usage records. Null descriptions become empty fields; LF and content spaces flow through the shared
CSV writer. Default catalogue columns/output, Usage Mapping and legacy CSV remain unchanged. Both Web
and Desktop discover the option through `ExportPresetCatalog`; no frontend code changes were needed.

Optional-column validation: focused description/export/surface tests **34/34**, full Core **557/557**,
Release **0 warnings / 0 errors**, and `git diff --check` passed. Pre/post Release-binary comparisons
on the description fixture confirm byte-identical default catalogue, Usage Mapping, legacy CSV and
JSON (scan timestamp fixed for comparison). The Description-selected local review CSV has six object
rows. Privacy E2E was not required: no browser-visible code changed. Formatter baseline untouched.

Retention-slice validation: description tests **14/14**, focused parser/export regressions **86/86** (including those
description tests), full Core **553/553**, Release build **0 warnings / 0 errors**, and `git diff --check`
passed. Tests compare JSON and legacy/Data Catalogue/Usage Mapping CSV output with changed description
metadata, including all optional export columns. Privacy E2E was not needed: browser-visible and
serialized output remain unchanged. The existing unrelated formatter baseline was not touched.

## Export Builder provenance foundation

The Export Builder provenance foundation starts in Core: `DirectUsageProvenanceAnalyzer` derives deterministic,
v1-eligible (**Columns** and **Measures**, excluding system-generated objects) normalized direct-report
provenance and object summaries from the existing `ProjectInventory`. It retains semantic model/table/object
identity, semantic usage/confidence, report name and path, persisted page/visual IDs, visual type,
usage context/role, artifact/evidence paths, direct-usage counts and distinct report/page/visual counts.
Reporting now consumes that in-process result through two fixed, non-UI CSV presets: **Data catalogue**
(one eligible object per row, including zero-use objects) and **Usage mapping** (one normalized direct
logical usage per row). Parser-level direct evidence remains retained but is grouped for export by semantic
identity plus `ReportPath`/`PageId`/`VisualId`/context/role; its sorted advanced `EvidenceCount`,
`ArtifactPaths` and `EvidencePaths` remain recoverable. Data-catalogue `DirectUsageCount` uses the same
logical usage count, while report/page/visual counts remain machine-identity based. Usage mapping also
has a presentation-only **Visual** label using the existing deterministic title/on-canvas-text/visual-type
fallback; it never replaces `VisualId` for identity. `ExportRequest`, `ExportPreset` and `ExportPresetCatalog` validate fixed allowed/default
columns; CSV writing is shared with the legacy renderer and preserves comma/CRLF/RFC quoting and
spreadsheet-formula neutralisation. The Web app now exposes a transient **Export CSV** action after a
successful scan: its compact, keyboard-operable panel takes presets and default/allowed columns directly
from Reporting, resets to defaults when presets/scans change, and downloads BOM-prefixed project-named
`data-catalogue.csv` or `usage-mapping.csv` files through the existing local browser mechanism. The legacy
semantic-usage CSV remains separately available as **Download semantic usage CSV**; its header and
CLI behaviour remain unchanged. JSON schema remains `0.26`; there is still no CLI switch.

## Desktop Export CSV

The Desktop shell now retains only the latest successfully scanned `ProjectInventory` in memory. Starting
another scan, selecting another project or a failed scan clears that inventory and disables **Export CSV…**,
so an export cannot be generated from stale results. After a successful scan, the action opens a compact,
keyboard-operable modal dialog with the same two fixed presets, descriptions, reporting-owned allowed/default
columns and reset behaviour as the Web UI. It builds `ExportRequest` and calls `ExportCsvRenderer` directly;
it never reruns the scanner or reconstructs provenance. A standard Save dialog writes the renderer's CSV
unchanged with a UTF-8 BOM, using the shared safe filenames `<project>.data-catalogue.csv` and
`<project>.usage-mapping.csv`. The automatic legacy semantic CSV remains separately available as **Open
semantic CSV** and is unchanged. The Desktop slice was approved and committed at `00c4fc9`.

Validation for the Desktop slice: focused export/Desktop surface tests **12/12**, full Core suite
**539/539**, privacy end-to-end **2/2**, and full Release build **0 warnings, 0 errors**. `git diff --check`
passes. The known unrelated formatter baseline remains 24 findings and was not changed.

`UserFacing` is export-only provenance, never a sixth semantic usage state. Its values are **Yes**, **No**
and **Unclear**. Active projections, tooltip data, drillthrough and active rendered formatting are Yes;
filter-only, sort-only and selector-supporting evidence are No; direct `Other` evidence is Unclear. Yes
wins over Unclear at object level. Hidden visual/group state is deliberately not applied: the existing
effective-visibility rule is accessibility/focus evidence, not a report-runtime visibility conclusion.
`No` means only that no qualifying direct evidence was found, never that an object cannot be exposed to a
user. See `DirectUsageProvenanceAnalyzerTests` for the exact tested boundary.

## Mobile semantic references

Desktop can persist a mobile-only formatting expression in a visual's sibling `mobile.json`. PBI Assure
now passes that root through the existing field-reference extractor and merges its references into the
same visual's normal direct-usage path. Position-only mobile state has no effect. The exact observed
`visualContainerMobileState/2.7.0` declaration is recorded as a verified schema observation; no mobile
presentation inventory, JSON schema change, CSV change or Finding was added. See
[the evidence review](../reviews/mobile-semantic-reference-desktop-evidence-2026-08-22.md).

## Desktop formatting semantic references

The Desktop-authored formatting/analytics evidence fixture confirms that dynamic title/subtitle,
conditional colours/background, reference lines, error-bar bounds and rule-based icons all already use
the generic PBIR extraction path correctly. The sanitised regression fixture pins eight `DirectlyUsed`
measures and one unused control; no implementation change was needed. See
[the evidence review](../reviews/desktop-formatting-semantic-reference-evidence-2026-08-22.md).

## KPI and measure Detail Rows dependencies

The round-tripped Desktop fixture `desktop-kpi-detailrows-evidence-final` proves measure-owned `kpi`
`targetExpression`, `statusExpression` and `trendExpression`, plus a measure-owned multiline
`detailRowsDefinition`. PBI Assure retains those exact expression shapes only in process and sends them
through the existing DAX dependency path using the owner measure as source [verified by Desktop evidence].

The resulting ordinary `Dax` edges make metadata-only targets **Indirectly used** when the owner is
report-used; the existing **Why: Referenced by …** path explains them. There is no new usage state,
structural root, report section, JSON schema or CSV change [design decision]. The parsed expressions are
`JsonIgnore` implementation data; the existing dependency inventory holds the resolved edge evidence.
Only measure-owned Detail Rows is supported. Table-owned Detail Rows remains unobserved and unsupported.
See [the evidence review](../reviews/kpi-detail-rows-desktop-evidence-2026-08-24.md).

## Effective visibility in accessibility findings

Power BI Desktop 2.157.879.0 runtime evidence shows that a hidden canvas item is filtered from its shown
siblings, does not retain a rendered component, and cannot receive focus. The same behavior applies to
every descendant of a hidden group [verified]. PBI Assure therefore defines an item as effectively visible
only when the item itself and every resolved ancestor group are not hidden [design decision]. Missing,
ambiguous or cyclic ancestry remains conservatively ineligible rather than being treated as page-root
content.

The shared group hierarchy resolver now computes effective visibility for both groups and visuals.
`PBI-ACCESS-001` 1.1.0 checks missing alt text only for effectively visible visuals in keyboard
navigation. `PBI-ACCESS-002` 1.2.0 compares explicit non-negative ranks only among effectively visible
items in the same immediate scope, and cautiously explains that an equal-ranked item may be skipped.
`PBI-ACCESS-003` is unchanged. Group `isHidden` is retained only for this in-process analysis, so JSON
schema `0.26`, CSV and report rendering are unchanged.

## Accessibility review presentation

Accessibility rules, rule IDs, versions, finding data and effective-visibility semantics are unchanged.
The HTML renderer now presents findings whose existing category is `Accessibility` in a separate
top-level **Accessibility review** section. It groups current observations by existing rule before
showing the same location, suggested-action and technical-evidence details for each one. The main
**Findings** section and Assurance summary counts include only non-accessibility findings; the separate
review states that it supports manual WCAG and assistive-technology testing and is not a compliance
verdict [design decision].

This is a presentation-only partition performed while rendering. JSON schema/output, JSON finding data,
CSV, the analyzer and the existing accessibility checks are unchanged.

## Auto Date/Time structural provenance

Desktop evidence establishes that `__PBI_LocalDateTable = true` is the reliable marker for a generated
local date table, and that its relationship endpoints are real model structure with distinct system
provenance [verified by Desktop evidence]. PBI Assure therefore retains every relationship edge and the
existing `StructurallyRequired` state. When a relationship's **target** is an exactly marked local date
table, its roots are additionally tracked as `SystemGeneratedAutoDateTime`; table names, hidden state,
template markers and model-level Auto Date/Time state are not used for this slice.

An object reached only from those generated roots now shows **Why: Required only by Power BI-generated
Auto Date/Time structure** [design decision]. Any user-authored relationship or other structural root
still wins and leaves the normal structural presentation intact. The edge-level and usage-level provenance
are in-process only, so JSON schema `0.26`, JSON output shape, CSV and the five usage states are unchanged.

## Verified at the current product state

- `dotnet build PbiAssure.slnx` — **succeeded, 0 warnings, 0 errors** [verified]. `TreatWarningsAsErrors`
  is on, so warnings fail the build.
- Core and privacy validation — **524 core + 2 privacy end-to-end tests passed**, 0 failed [verified].
  The focused HTML renderer/theme/accessibility selection passed 62/62.
- CI (`.github/workflows/ci.yml`) — **green** [verified], confirmed complete (not queued) for the
  pre-feature baseline `0c1af3c`. Runs restore, build, a Playwright Chromium install, then the whole
  solution test suite on `windows-latest`.
- The privacy end-to-end tests are part of the normal solution test run; they need Node.js and a
  Playwright Chromium build. See [Build and test](../../README.md#build-and-test).

## Known defect not fixed here

`dotnet format PbiAssure.slnx --verify-no-changes` **fails with 24 whitespace errors** [verified], split
across two files:

- `src/PbiAssure.Reporting/HtmlReportRenderer.ThemeReview.cs` — 21
- `src/PbiAssure.Core/Scanning/ThemeReviewAnalyzer.cs` — 3

All pre-existing and unrelated to recent work. `CONTRIBUTING.md` tells contributors to run this command,
so it fails on a clean checkout. Deliberately left alone so it is not mixed into unrelated commits; fix
it as its own change.

## Bounded object-level security inventory

Desktop-authored `desktop-ols-evidence` proves that Power BI Desktop persists inline
`columnPermission Salary = none` and table `metadataPermission: none` inside a role's
`tablePermission` block. PBI Assure retains both as structured role inventory. An explicitly named
column permission makes only that column structurally required; table-level OLS is visible in the
Security roles review but never makes every child column used. That role/permission inventory was
introduced in JSON schema `0.23`; Findings and the semantic-usage CSV were unchanged.

## Aggregation mapping structural usage

Desktop's **Manage aggregations** UI persists explicit column-owned `alternateOf` blocks. PBI Assure now
retains the authored `baseColumn` reference and optional `summarization`, resolves only exact qualified
model columns, and records an `AggregationMapping` dependency from the aggregation-side column to its
detail column. A resolved mapping makes its source a structural root, so both endpoints are protected
from `ApparentlyUnused`; direct report usage keeps its existing higher precedence.

The safe regression fixture is explicitly sanitised because the external Desktop evidence project contains
environment-specific connection metadata. The mapping proves configured model structure only: it does not
prove query acceleration, a runtime hit, refresh success or Power BI Service behaviour. JSON is additive
in schema `0.26`; CSV columns and Findings are unchanged. See
[the evidence review](../reviews/aggregation-alternateof-desktop-evidence-2026-08-21.md).

## Incremental-refresh policy inventory

The paired Desktop-authored `desktop-incremental-refresh-evidence-baseline` and
`desktop-incremental-refresh-evidence` fixtures prove that `RangeStart`/`RangeEnd` filtering can exist
without a configured policy. Desktop adds an explicit table-owned `refreshPolicy` only after policy
configuration. PBI Assure now retains its basic policy type, rolling/archive window, incremental window,
offset, polling M, source M and optional mode on `SemanticTableInventory.RefreshPolicy`.

The affected semantic-table card shows a compact **Incremental refresh** block and states that saved
settings do not confirm query folding or a successful Service refresh. The filter-only control has no
policy block. A polling expression creates a structural dependency only when it contains one explicit
qualified reference to the owning table; this makes `FactEvents_Policy[LastModified]` structurally
required independently of Auto Date/Time. Custom polling M is retained without guessed object usage.

The JSON inventory is additively versioned at `0.24`; CSV and Findings are unchanged. No folding,
refresh-health, Service partition or real-time default is inferred. See
[the evidence review](../reviews/incremental-refresh-policy-evidence-2026-08-21.md). The bounded
`USERELATIONSHIP` extractor is now complete.

## Inactive relationship / USERELATIONSHIP evidence

Desktop-authored `desktop-userelationship-evidence` retains one active relationship and three inactive
relationships. A report-used measure calls `USERELATIONSHIP` for the shipping relationship, an unused
measure calls it for the referral relationship, and the legacy relationship has no local activating call.
The current flat DAX scanner retains the referenced columns but discards built-in function identity,
argument boundaries and endpoint pairing. Relationship activation therefore cannot yet be inferred safely.

PBI Assure now extracts only the built-in `USERELATIONSHIP` shape with exactly two explicit qualified
column references. It resolves the pair only when it uniquely and exactly matches a relationship,
including reversed argument order, then uses the source calculation's existing reachability. Inactive
relationships therefore show **Activated by report-used DAX**, **Referenced only by unused DAX**, or
**No USERELATIONSHIP call found in analysed DAX**; active relationships remain visually normal. The
metadata is additive in JSON schema `0.25`; CSV, Findings and semantic usage states are unchanged. See
[the evidence review](../reviews/userelationship-inactive-relationship-evidence-2026-08-21.md). The next
report-level measure DAX dependency gap is now evidence-closed and remains parked.

## Bookmark semantic-usage evidence

The paired Desktop-authored bookmark fixtures establish that persisted `People[Region]` and
`People[SecretCategory]` references can remain after their live filter/slicer carriers are removed and
become inert. When the carriers remain effective, normal page/visual parsing already classifies both
fields as directly used. Bookmark snapshots are therefore **not** semantic-usage roots by default; graph
edges are parked pending a unique, behaviourally effective durable carrier. Exact
`bookmarksMetadata/1.0.0` and `bookmark/2.1.0` are now verified schema baselines. See
[the evidence review](../reviews/bookmark-semantic-usage-evidence-2026-08-21.md).

## Evidence-gated unresolved-reference findings

`PBI-MODEL-005` now presents a **Reference not found** Warning when PBI Assure has both an explicit,
structured model reference and a resolution result establishing that its target was not found. The safe
initial kinds are `SortBy`, `HierarchyLevel`, `RelationshipEndpoint`, `PerspectiveMember` and
`ReportMeasure`.

The resolution outcome is structured evidence on every `UnresolvedSemanticDependency`: current values
are `NotFound` and `Ambiguous`. `Reason` remains human-readable diagnostic context only; `MODEL-005`
must not inspect its wording to decide eligibility. A `NotFound` outcome does not override producer
evidence safety: DAX, field-parameter and mixed `TablePermission` records remain suppressed.

Best-effort DAX references are deliberately suppressed. `FieldParameter` is also suppressed because its
reference comes from specialised DAX-text extraction, and `TablePermission` is suppressed because that
single dependency kind currently mixes an explicit missing role table with references extracted from a
role's DAX filter. Ambiguous structured references are not described as missing.

The rule groups only identical model/source/kind/reference/reason evidence and preserves every source
path. Model identity and source-object identity are part of the key. Findings are deterministically
ordered. The unresolved-dependency JSON contract has one additive `ResolutionOutcome` property; existing
fields, including `Reason`, `ReferenceText` and `EvidencePath`, are unchanged. The semantic-usage CSV
remains an object-usage export and is unchanged. Analysis coverage and semantic usage classification are
not involved.

No committed Desktop-authored fixture currently contains an unresolved semantic dependency. Six
Desktop fixtures and four local sample projects were measured and contained zero. The missing-reference
finding is therefore proven against accurately labelled synthetic malformed metadata, not claimed as a
Desktop-persisted broken state. Desktop may repair, reject or remove such metadata when saving.

## Explicit report landing page

`ReportInventory.LandingPageName` is an additive JSON inventory property. It retains the exact optional
`landingPageName` from `definition/pages/pages.json`; `ActivePageName` is unchanged. The paired
Desktop-authored fixtures `desktop-landing-page` and `desktop-landing-page-no-explicit` establish that
Desktop writes `landingPageName` only after **Set as landing page** is used, and that it can name a
different page from the saved authoring-state `activePageName`.

`PBI-NAV-017` (**Configured landing page missing**) is an Error / Finding only when the explicit,
nonblank `LandingPageName` cannot be found among the report's internal page names. It carries the report,
the retained internal target name, `pages.json` and `$.landingPageName` evidence. No explicit landing
page is valid and never produces a finding. The positive malformed-target case remains synthetic: there
is no claim that Desktop preserves a missing target after saving.

The former proposed active-page-missing rule is not currently recommended. `activePageName` is saved
authoring state, whereas `landingPageName` is the explicit landing-page setting; current evidence does
not establish user impact from a stale authoring-state value. Revisit only if a real report or Desktop
experiment demonstrates a meaningful integrity consequence.

In the generated HTML, Report page cards always start collapsed. The page whose internal name matches a
valid explicit `LandingPageName` displays a visible **Landing page** badge in its collapsed summary.
`ActivePageName` remains retained inventory evidence for saved Desktop authoring state and has no visible
page-card label or automatic-expansion behaviour. The landing-page badge is included in page search text;
there is no separate landing-page facet.

## Configured custom-theme resource integrity

`PBI-COMPAT-002` (**Configured custom theme unavailable**) is a Warning / Finding when
`definition/report.json` explicitly configures `themeCollection.customTheme` but the selected local
resource cannot be resolved or read. It is intentionally separate from Theme Review: it does not assess
theme quality, formatting consistency, accessibility or theme completeness.

The theme parser retains an additive public `ThemeSourceInventory.ResolutionOutcome` field. Current
outcomes distinguish resolved resources from a missing reference name, no matching package item,
ambiguous package items, invalid package paths, missing files, invalid JSON and unreadable resources.
The rule uses that structured value, not `ResolutionIssues` prose. Valid sparse themes, reports with no
custom theme or only a base theme, and unselected registered resources do not produce a finding.

## Active workstream — unsupported-construct detection

PBI Assure analyses only some of the files in a semantic model. Before this workstream the rest were
skipped silently, so a column referenced only by a row-level security filter was reported as apparently
unused with no indication that security metadata had been skipped.

### Implemented

- **`AnalysisLimitation`** record on `ProjectInventory.AnalysisLimitations` — records metadata that was
  encountered but not analysed. Distinct from `UnresolvedSemanticDependency`.
- **`SemanticDefinitionFileRegistry`** — the single source of truth classifying every semantic-model
  definition artifact as `Analyzed`, `SemanticNotYetAnalyzed`, `Packaging` or `Unrecognized`.
- **`AnalysisLimitationDetector`** — compares the artifacts the scanner enumerates against what the
  parser actually opens, and emits one limitation per unanalysed or unrecognised file.
- Registry paths corrected against current Microsoft-documented TMDL/PBIP structure: roles and
  perspectives are directory-per-object; `dataSources.tmdl`, `functions.tmdl` and `model.bim` are
  recognised; `TMDLScripts/` editor scripts are excluded so they do not become false limitations.
- Those paths are now confirmed against real Power BI Desktop output by the
  `desktop-semantic-constructs` fixture. No path needed correcting.
- **`ClassificationConfidence`** on `SemanticObjectUsage` — `Established` or
  `QualifiedByLimitation`. `SemanticUsageConfidenceQualifier` marks absence-state objects in a model
  that holds a limitation whose impact is `MayCreateDependencies` or `DependencyEffectUnknown`.
- **Bounded role-security dependencies.** The Desktop-authored `desktop-ols-evidence` fixture pins the
  supported Power BI serialization. `definition/roles/<role>.tmdl` is parsed; each
  `tablePermission` filter resolves against its owning table and becomes a model-structure root. Desktop's
  explicit `columnPermission <column> = <permission>` form is also retained and roots only that named
  column. Table-level `metadataPermission` is retained for security inventory but does not root every
  child column. Roles remain `PartiallyAnalyzed` when other role metadata is not accounted for.
- **Compact Security roles review in HTML.** When a model defines roles, a conditional review section
  shows model permission, row-level filter DAX, table-level metadata permissions and explicitly named
  column permissions, grouped by semantic model and ordered deterministically. It presents retained facts
  only: there are no findings or Service membership/effective-runtime verdicts. The role/permission
  inventory is additive JSON; semantic-usage CSV schema is unchanged.
- **Perspective member dependencies.** `definition/perspectives/<name>.tmdl` is parsed;
  `perspectiveTable`, `perspectiveColumn`, `perspectiveMeasure`, `perspectiveHierarchy` and
  `includeAll` become model-structure roots, so an object a perspective exposes is
  `StructurallyRequired`. Membership is narrow: naming a table does not expose its fields unless
  `includeAll` is set. Perspectives are `PartiallyAnalyzed` — presentation meaning and perspective sets
  are not analysed.
- **DAX user-defined function dependencies.** `definition/functions.tmdl` is parsed; each function's
  name, parameter list and body are read, and the body's references become dependency edges. A function
  is a graph **node, not a root** — nothing in the model requires a definition to exist — so an uncalled
  function's references land on `UsedOnlyByUnusedBranch` rather than being kept alive. Functions join
  `knownNodes` the way report-level measures do: visible to traversal, absent from the usage rows.
  Parameters are local symbols that shadow model objects, and an unqualified name resolves as a measure
  because a function has no owning table. Functions remain `PartiallyAnalyzed` with
  `MayCreateDependencies` — see the gaps table.
- **Artifact-sensitive limitation impact.** The registry gives the conservative construct-type default;
  where the scanner proves a *particular* role file contains nothing unanalysed that could reference a
  model object, the emitted limitation is narrowed to `NoKnownDependencyEffect`. The limitation is still
  emitted and the support state is unchanged. Coverage is affirmative: only constructs known to carry no
  object reference count as accounted for, so anything unrecognised keeps the conservative impact.
- **User-facing presentation of limitations and confidence, in HTML.** Two levels, deliberately split by
  scope. An **Analysis coverage** section states per model what was not fully analysed, grouping
  limitations by construct rather than by file, showing the ones that can affect classification and
  disclosing the ones that cannot inside a `details` element. Each affected object then carries a small
  **Usage check incomplete** link beside its status, pointing at that model's coverage block. Counts
  only; there is no score, percentage or severity.
- **Plain-language vocabulary for that surface.** Domain enum names are engineering terms and are not
  automatically user-facing words. One vocabulary runs through the whole surface, built on the verb
  *check* and the phrase *used or unused result*:

  | Domain | Shown |
  |---|---|
  | `QualifiedByLimitation` | **Usage check incomplete** |
  | `MayCreateDependencies` | Could hide extra usage |
  | `NoKnownDependencyEffect` | Does not change any used or unused result |
  | `DependencyEffectUnknown` | Not known whether it hides extra usage |
  | `MayInvalidateExistingEvidence` | Could change how other results should be read |
  | `PartiallyAnalyzed` / `NotYetAnalyzed` / `Unrecognized` | Partially checked / Not checked yet / Not recognised |

  `NoKnownDependencyEffect` is deliberately **not** rendered as "fully checked": the construct is still
  only partly read, and what is established is narrower — that the unread part cannot add usage. The
  support state beside it carries the other half of that distinction, and a test pins both halves.
  The marker phrase is a single constant in the renderer, so the marker, the model headline, the summary
  sentence and the usage guide cannot drift apart.
- **Classification-compatible usage reasons.** The "Why" line under an object now names a predecessor
  whose own reachability matches the state being explained, not whichever incoming edge came first.
  `SemanticNodeReachability` on `ProjectInventory` publishes what the classifier already computed —
  which nodes are reachable from a report root and which from a model-structure root — for **every** node
  the graph touches, including report measures and DAX functions that have no usage row. Selection by
  state: `IndirectlyUsed` needs a report-reachable predecessor, `StructurallyRequired` a
  structure-reachable one, `UsedOnlyByUnusedBranch` one reachable from neither; `DirectlyUsed` keeps its
  report locations and `ApparentlyUnused` still gets no reason. Where several are eligible, the one with
  the alphabetically first qualified name is shown, so parse order cannot change the explanation.
  Reporting reads the published flags and traverses nothing.

  **`SemanticNodeReachability` is part of the JSON inventory contract** [verified in repository]. The CLI
  serialises `ProjectInventory` wholesale, so every public property on it is emitted — the same route by
  which `AnalysisLimitations` became public. Audited and deliberately left exposed: all 34 rows on the
  Desktop fixture name identities **already published elsewhere** in the same document (tables, columns,
  measures, roles, relationships, functions, report measures — 0 novel names), it carries no data values
  or machine paths, and the two booleans are derivable from the edge list and roots that are already
  emitted. It is not *required* in JSON — Reporting receives the in-process object — but it answers "why
  does this object have this state?" for a JSON consumer, which is the same question the HTML answers.
  Hiding it would need `[JsonIgnore]`, a pattern this inventory does not otherwise use. **[design
  decision]**
- **Reason precedence matches the displayed state.** A relationship endpoint is the one reason kind
  whose edge *creates* the requirement instead of carrying reachability, and whose source is a
  relationship rather than a model object, so it explains `StructurallyRequired` and nothing else. Every
  other kind cites a real predecessor and is gated by the same reachability check, with wording and
  precedence order unchanged. A column that is both a relationship endpoint and report-reachable
  displays "Indirectly used" and is explained by the live dependency; the relationship edge is untouched
  and explains it again if its state is ever `StructurallyRequired`.

  Two findings from that audit are worth not rediscovering. **"Available through field parameter X" is a
  live explanation, not a structural one** — when the report uses the field parameter, its choices are
  report-reachable, so an early attempt to gate all structurally-worded reasons on
  `StructurallyRequired` wrongly deleted it. And the four **"Sorts X" reasons on unused-branch objects in
  `desktop-semantic-constructs` are compatible evidence**, not mismatches: they cite a predecessor that
  is itself unreachable, which is what that state means.

Limitation **detection** is file level only. Confidence is an orthogonal additive field. HTML consumes
both; **CSV and the browser app do not** — see the gaps table.

Usage states change only through real dependency evidence entering the graph — as RLS parsing now does.
Nothing derives a usage state from the presence of a limitation.

### Not implemented — do not assume otherwise

- Block-level detection (`kpi`, `detailRows`, `alternateOf`, model-side `variation`)
- Property-level detection
- Malformed-TMDL recovery
- `AnalysisScopeBoundary` catalog
- A CSV or browser-app surface for limitations or confidence. HTML has one; `SemanticUsageCsvRenderer`
  has a fixed header contract that was deliberately not widened in that slice
- Report-side PBIR limitation detection

## Resolved — the propagation blocker

Power BI Desktop emits `model.tmdl`, `database.tmdl` and a culture file for **every** semantic model, so
while all three carried `DependencyEffectUnknown` the proposed propagation rule would have caveated every
object in every model.

All three now carry `NoKnownDependencyEffect` while remaining classified `SemanticNotYetAnalyzed` — still
reported as unanalysed, but unable to caveat a usage conclusion [verified].

- `database.tmdl` — every Desktop-authored fixture contains only a compatibility level, no object
  references [verified by Power BI Desktop-authored fixture]
- `model.tmdl` — names objects only through `ref` collection-ordering declarations, which list every
  member regardless of use; treating them as usage would mark every object in every model as used
  [verified by Power BI Desktop-authored fixture, and documented as round-trip ordering by Microsoft]
- `cultures/*` — the default emitted culture is empty [verified by fixture]. A translated culture would
  name objects, but a translation describes an object and is deleted with it, so it cannot keep an
  unused object alive [design decision]. **Open sub-case:** Q&A linguistic metadata also lives in culture
  files and is closer to a consumer; settling it needs a fixture containing translations and synonyms

Propagation is implemented and, because of this correction, does not fire on a model whose only
unanalysed files are the always-present three — verified against three Desktop-authored fixtures.

## Current evidence gaps

| Gap | Status |
|---|---|
| Whether `dataSources.tmdl` is ever emitted by current Desktop | Not observed in any fixture [verified]. Impact left `DependencyEffectUnknown`; costs nothing while absent |
| Whether a *translated* culture file names model objects, and whether Q&A synonyms constitute usage | **Open.** Needs a Desktop fixture containing translations and synonyms |
| Whether table-owned Detail Rows metadata creates model dependencies in current Desktop | **Open.** The committed Desktop evidence establishes measure-owned `detailRowsDefinition` only. Do not infer or implement a table-owned form without a separately saved/reopened fixture. |
| **Where a UDF is called from outside the model definition** | **Report-measure path evidence-closed and parked.** In Desktop 2.157.879.0 a live-connected report exposed no source UDFs and rejected `Doubled()` [reported manual observation], although the rejected expression persisted in `reportExtensions.json` with no references or unrecognised marker [verified in Desktop-authored bytes]. The valid `[Total Amount]` control had a structured measure reference. In the later tested **Add a local model** transition, valid report measures migrated into ordinary local TMDL measures rather than remaining report-owned alongside a local model; the remote measure appeared as `EXTERNALMEASURE`. Do not implement synthetic traversal or expression parsing without trustworthy bound source metadata. Visual calculations remain unread. See [the evidence review](../reviews/report-level-measure-udf-fixture-design.md). |
| Whether a UDF name can be namespaced with dots | Not observed. `DaxReferenceExtractor` does not treat `.` as an identifier character, so a dotted name would not tokenise as one identifier |
| Multi-parameter UDFs, other parameter type hints, `VAR`/`RETURN` or multi-line bodies | Not observed. Every function in `desktop-udf-references` is one line, and only one takes a parameter at all |
| Perspective `includeAll`, `perspectiveHierarchy` and perspective sets in real Desktop output | Implemented from Microsoft-documented syntax for the first two; no fixture emits any of them |
| Whether current Desktop can still produce TMSL `model.bim` | **Settled.** The committed paired Desktop fixtures prove TMSL can persist through save/reopen and can be explicitly upgraded to TMDL. PBI Assure rejects local TMSL before analysis; no TMSL parser is implemented. See [the evidence review](../reviews/tmsl-model-bim-desktop-evidence-2026-08-21.md) |
| Role-security forms beyond the committed evidence — cross-table filters, other OLS permission shapes, DirectQuery/Direct Lake roles | **Open.** Desktop evidence proves same-table RLS filters, inline column permissions and table metadata permissions; parser tests cover further bounded shapes synthetically |
| `PBI-ACCESS-001` sample finding volume | **Measured in 12 local PBIP projects (216 representative findings after deliberate test-format duplicates are not double-counted).** Only 13 are plausibly decorative; 22 text boxes are uncertain because on-canvas text is not exposed. Do not change the rule from visual type alone. See [alt-text measurement](../reviews/access-001-alt-text-measurement.md) [verified] |

### Settled, so it does not need re-investigating

- **Emitted paths for roles, perspectives, cultures and functions** — confirmed against real Desktop
  output by `DesktopSemanticConstructsFixtureTests`, and again by `DesktopUdfReferencesFixtureTests`. No
  registry path needed correcting.
- **A semantic-model measure can call a UDF, and PBI Assure already follows it**
  [verified by Power BI Desktop-authored fixture]. Desktop accepted `UDF Result = Doubled()` and emitted
  it as an ordinary measure expression. With a Card bound to it, PBI Assure produces
  `UDF Result` → DirectlyUsed, `Total Amount` → IndirectlyUsed, `Sales[Amount]` → IndirectlyUsed,
  `Region` → ApparentlyUnused with the coverage marker. Previously recorded as manual-only evidence; it
  is now the committed `tests/fixtures/desktop-udf-measure-consumer` fixture and guards against
  regression.
- **A model object can be reached by a live and a dead branch at once**
  [verified by Power BI Desktop-authored fixture]. In that fixture `Sales[Amount]` is reached both by
  `Total Amount` (live, through `Doubled()`) and by the uncalled `TotalOf()`. That is what makes an
  explanation chosen from "any incoming edge" wrong, and it is the reason-selection regression guard.
- **How a UDF body writes a reference** — a qualified column as `Table[Column]`, an unqualified `[Name]`
  as a measure, a bare identifier as a table, and a call to another function as `Name()`. All five
  functions serialise into one `definition/functions.tmdl` and `model.tmdl` carries **no `ref function`
  line** [verified by Power BI Desktop-authored fixture].
- **Re-saving does not normalise semantic-model files** — every definition file is byte-identical across
  a close/reopen/save round trip at Desktop 2.156.951.0 [verified by fixture].
- **Property-level precondition is cleared for four properties.** `summarizeBy` (enum), `isKey`
  (boolean) and `dataCategory` (fixed-vocabulary string) cannot reference model objects [verified by
  Microsoft API reference]. `lineageTag` is the object's own stable identity, consumed by *other* models
  for composite binding, so it is not an ordinary intra-model dependency [verified by documentation]. The
  remaining nuance is two semantic models bound compositely inside one project. **This clearance does not
  generalise to properties not on that list.**

## Immediate task

**Completed — report-side PBIR schema observations.** `ReportSchemaObservation` now records source path,
raw declaration, parsed family/version, expected family, fixture-backed verified version and a structured
coverage state for each parsed report artifact. It distinguishes `VerifiedExact`,
`RecognisedUnverifiedVersion`, `UnknownFamily`, `MetadataMissing` and `MetadataMalformed`; property-wise
parsing never branches on those states. The exact Desktop baseline is centralised for
`definitionProperties/2.0.0`, `versionMetadata/1.0.0`, `report/3.3.0`, `pagesMetadata/1.1.0`,
`page/2.1.0` and `visualContainer/2.11.0` plus `visualContainer/2.12.0`. `definition/version.json` now retains its PBIR definition
version separately from both its schema URI and `definition.pbir`'s existing version.

Non-exact observations appear as neutral, report-scoped **Analysis coverage** information, grouped by
artifact/type/state/version with raw paths behind technical details. Exact observations are silent, no
Finding is created and CSV is unchanged. The public JSON inventory changed additively through report
`SchemaObservations`, `VersionMetadataPath` and `PbirDefinitionVersion`; the current inventory schema is
`0.24`.
Bookmark declarations are exact-verified at `bookmarksMetadata/1.0.0` and `bookmark/2.1.0`; report-extension
declarations remain recognised-unverified because no committed Desktop fixture establishes their exact
baseline. The policy and evidence inventory are in
[the encountered PBIR schema compatibility review](../reviews/encountered-pbir-schema-compatibility-policy.md).

**Completed — connector coverage measurement.** A local, aggregate-only scan of 40 fixture/sample
projects (77 M expressions, 268 dotted calls) found two recognised source calls and four unrecognised
library/reader calls only: `Binary.Decompress`, `Binary.FromText`, `Json.Document` and `Table.FromRows`.
No unrecognised class-A external source function was observed. `Json.Document` is source-adjacent, not a
connector: it consumes supplied text/binary and cannot safely identify a source or location by itself.
No production change is recommended. See
[the measurement](../reviews/power-query-connector-coverage-measurement.md).

**Current recommendation: no connector implementation.** Repeat the aggregate measurement only when a
redistribution-safe fixture or an existing local sample contains an unrecognised class-A source function.

The compatibility investigation established that current parsers retain several schema URIs but never
branch on them. Exact versions in committed Desktop fixtures are `definitionProperties/2.0.0`,
`report/3.3.0`, `pagesMetadata/1.1.0`, `page/2.1.0`, `visualContainer/2.11.0`, `visualContainer/2.12.0` and
`versionMetadata/1.0.0` (the last is encountered in `version.json` but is not currently retained). A
different known-family version is unverified rather than automatically unsupported. Compatibility state
belongs first in Analysis coverage; no `PBI-COMPAT-003` finding is justified yet.

### Completed — fenced TMDL expression parsing

The shared TMDL expression readers now recognise a triple-backtick value immediately after `=` as a
fenced expression, rather than treating the opening delimiter as inline DAX or M. The closing delimiter
sets the structural left boundary; it is removed along with the opening fence, while relative indentation
and blank lines inside the expression are retained. The parser stops at that closing delimiter, so a
following property, annotation or object is not consumed as expression text.

This applies to current parser surfaces using `ReadExpression` or `ReadAssignmentExpression`: calculated
columns, measures, named expressions, DAX functions, calculation items, calculation-group selection and
format-string expressions, calculated partitions, M partitions and RLS table-permission filters. The
generic unread-child detection skips fenced bodies too, so expression keywords are not mistaken for
unsupported role metadata. A real Desktop-authored work report showed a fenced `tablePermission` on
2026-08-20; only redistribution-safe synthetic cases are committed.

Observable corrections are intentional: fenced expressions now retain their actual bodies; dependencies
inside them can reach semantic usage classification; fenced M reaches connector analysis; and an RLS
role whose only apparent unread content was its fence no longer displays the spurious role coverage
note. No JSON or CSV shape changed.

The compact row-level security HTML review is complete. It is conditional on retained roles, shows model
permission and table-filter DAX, and keeps its project-only security boundary explicit. See
[HANDOVER.md](HANDOVER.md) for the remaining user-value ranking.

The presentation work below is done. What follows is the record of why it was next, kept because it
explains the shape of the HTML surface.

### Completed — limitation and confidence presentation

The expectation recorded here previously — that parsing UDF references would take the Desktop fixture's
qualified count to zero — turned out to be **wrong, and the measurement is what settled it**. Reading
function definitions does not retire the function limitation, because the unread part was never the
definitions: it is where a function is *called from*. Visual calculations and report-level measures can
call one and neither is parsed, so the impact stays `MayCreateDependencies` and
`desktop-semantic-constructs` still shows 21 of 27 objects `QualifiedByLimitation`, unchanged.

That is the correct outcome, not a shortfall. It also meant that waiting for zero qualification before
designing presentation would have been waiting for something that is not close, which is why
presentation was taken next.

Two things the visual review established that are worth not rediscovering:

- **All 21 qualified objects in `desktop-semantic-constructs` are Power BI-generated**, and the semantic
  model section defaults to developer-authored objects. On the default filter a reader sees the model
  headline saying 21 classifications are qualified and no markers at all, which is why the headline
  describes the marker rather than promising where it appears.
- **A model emits one limitation file per role**, so grouping by construct rather than by artifact is
  load-bearing, not cosmetic.

The proposal in [../design/unsupported-construct-design.md](../design/unsupported-construct-design.md)
§5 predates the implemented behaviour and was not followed literally.

Behaviour worth knowing before changing this surface [verified by rendered HTML]:

- **Nothing renders when nothing was left unanalysed.** The section, its navigation entry and the usage
  guide's explanation of the marker all disappear. The guide's note is gated on a qualified object
  actually existing, not merely on a limitation existing, so a model with only harmless limitations
  explains a marker it does not show.
- **Limitations exist but none qualify** is a distinct, common case — `grouped-tab-order` and the
  `model-reference-context` fixtures. The section renders with "None of them affect usage
  classification" and the disclosure reads "What was not fully analysed" rather than "other files".
- **Scoping is per model.** A model with no limitations gets no coverage block at all, and each object's
  marker links to its own model's anchor. `ClassificationConfidence` does not name the limitation that
  caused it, so object-level copy says "qualified by analysis limitations in this model" and never
  attributes a specific file.
- **Accessibility:** the marker is an `<a>`, so it is keyboard operable with no scripting; its meaning is
  visible text plus a `visually-hidden` expansion, never colour or hover alone; the harmless-limitation
  disclosure is a native `<details>` carrying the report's existing `+`/`−` affordance. Muted marker text
  measures 6.46:1 against the card background and the disclosure summary 8.44:1, and at a 375px viewport
  the section fits with no horizontal overflow.
- **Navigation wraps deliberately.** Eight tiles never fit one row inside the 82rem content width without
  cramping — they measure ~146px and their subtitles wrap — so four explicit columns from 64rem give a
  balanced 4+4 at every desktop width. Below 64rem the original auto-fit rule is untouched. Verified live
  at 1440, 1280, 900 and 375px [verified by rendered HTML].
- **Rendered results:** `desktop-semantic-constructs` 21 markers / 1 qualifying cause / 6 disclosed;
  `desktop-udf-references` 3 markers, both absence states represented; `grouped-tab-order` 0 markers,
  3 disclosed; `privacy-canary` no section at all.

## Reference documents

Everything referenced here is in this repository. No external document is required.

- [Architecture overview](../architecture.md) · [Usage classification](../usage-classification.md) ·
  [Rule catalog](../rule-catalog.md) · [Roadmap](../roadmap.md)
- [Design documents](../design/) — the current task plan and the unsupported-construct design
- [Reviews and evidence](../reviews/) — audits, verification passes and implementation handovers
- [Decision records](../decisions/) — architectural ADRs
- `tests/fixtures/tab-order-states/README.md` — the model for how a Desktop-authored fixture is
  documented

Read [../reviews/README.md](../reviews/README.md) before acting on any single review: later documents
correct earlier ones rather than the earlier ones being edited.
