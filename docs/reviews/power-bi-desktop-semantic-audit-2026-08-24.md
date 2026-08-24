# Power BI Desktop semantic audit

Date: 2026-08-24

Repository starting commit: `e9def85d3c4d583ea710a6aafe01251a2cce2b8f`

Diagnostic branch: `desktop-semantic-assumption-audit`

## Executive conclusion

The installed Desktop runtime confirms Assure's core sign-based tab-order model: a missing rank and every non-negative rank are included, while every negative rank is excluded. It also reveals more detail than the current product exposes:

- forward keyboard navigation uses larger ranks first;
- equal ranks are not a harmless tie: the first eligible DOM item wins and another item at the same rank can be skipped;
- groups form hierarchical navigation scopes; they are structural containers, not report visuals;
- hidden items and descendants of hidden groups are not rendered as canvas components, so they cannot participate in runtime focus even if they retain a non-negative rank;
- normalization is conditional, sign-aware, recursive by group, and normally spaces ranks by 1,000.

Desktop also confirms that `__PBI_LocalDateTable` is a semantic marker, not just part of a naming convention. A name shaped like `LocalDateTable_<GUID>` is used only as a narrow reconstruction fallback in the inspected path. Genuine Desktop-authored TMDL establishes `__PBI_TemplateDateTable = true` for template tables and `__PBI_TimeIntelligenceEnabled = 1` for enabled Auto Date/Time. Desktop's TMDL apply path independently tests that model annotation and invokes an explicit date-table synchronization operation when an enabled model is changed to disabled.

The most important product implication is provenance. Assure currently makes every relationship endpoint a model-structure root. That is defensible for user-authored relationships, but it causes a user date column to be labelled `StructurallyRequired` solely because Desktop generated Auto Date/Time plumbing. Desktop itself identifies hidden-date-table relationships separately in its conceptual-schema path. Assure should retain the dependency evidence but distinguish system-only reachability from user-authored structural reachability in user-facing classification.

This branch deliberately makes no production-rule changes.

## Desktop build inspected

- Product: Microsoft Power BI Desktop (x64)
- Display and file version: `2.157.879.0`
- Product version: `2.157.879.0 (26.08)+da30fe74eb0b8a8786ab7326b69c400a0e951831`
- Executable: `C:\Program Files\Microsoft Power BI Desktop\bin\PBIDesktop.exe`
- Installation root: `C:\Program Files\Microsoft Power BI Desktop`

Resource locations inspected:

- `bin\WebView2Resources\minerva\scripts\desktop.min.js`
- `bin\WebView2Resources\minerva\scripts\desktopDaxQueryView.min.js`
- `bin\WebView2Resources\minerva\scripts\desktopTmdlView.min.js`
- `bin\WebView2Resources\minerva\scripts\desktop.schema.json.min.js`
- `bin\WebView2Resources\minerva\scripts\desktop.schema.json.0.min.js` through `.41.min.js`
- `bin\WebView2Resources\minerva\scripts\desktop.schema-validator*.min.js`
- `bin\WebView2Resources\minerva\scripts\desktop.reportThemeSchema.json.min.js`
- `bin\WebView2Resources\minerva\scripts\monacoWebWorkers\tmdl.worker.js`
- `bin\WebView2Resources\minerva\scripts\monacoWebWorkers\expression.worker.js`
- `bin\WebView2Resources\minerva\modelView.html`
- `bin\WebView2Resources\minerva\tmdlView.html`

No Desktop binary or resource was copied into the repository.

## Method and evidence grades

The audit combined four evidence sources:

1. Assure source, tests, decisions, and existing Desktop-evidence fixtures.
2. Targeted string search in installed WebView/Minerva resources.
3. Short-range inspection around useful hits, followed to their callers or consumers where practical.
4. Existing controlled Desktop experiments and genuine Desktop-serialized PBIP/TMDL fixtures already committed to this repository.

Grades are used strictly:

- **PROVEN-RUNTIME**: an exact Desktop decision branch and relevant call path were found.
- **PROVEN-EXPERIMENT**: a controlled Desktop open, navigation, or save established the behavior.
- **STRONGLY-SUPPORTED**: multiple independent sources agree, but the complete decision path was not found.
- **INFERENCE**: plausible, but not sufficient for a production rule.
- **UNKNOWN**: evidence is insufficient.

The earlier controlled tab-order experiment is treated as existing evidence, as requested. The genuine Desktop-authored `tab-order-states`, `grouped-tab-order`, and `desktop-semantic-constructs` fixtures were re-inspected. No new GUI round trip was needed to settle the branches for which exact runtime code was found. Unsettled cases are listed explicitly rather than filled with synthetic expectations.

## Ranked assumption register

Priority is ranked across the whole register: P0 is the highest-value correction or pinning task.

| ID | Area | Current Assure behavior | Code location | Why Assure currently believes this / existing evidence | Risk if wrong | Likely FP impact | Likely FN impact | Can Desktop settle it? | Priority |
|---|---|---|---|---|---|---|---|---|---|
| TAB-005 | Hidden focus | `PBI-ACCESS-001` and duplicate-rank analysis do not exclude hidden visuals. | `MissingAltTextRule.cs:12-13`; `DuplicateTabOrderRule.cs:16-22` | The sign rule was applied independently of visibility. Desktop component creation filters `isHidden` and hidden ancestors. **PROVEN-RUNTIME** | High | Hidden or hidden-by-group objects can receive missing-alt-text or duplicate-rank findings although they cannot be focused. | Low | Yes; settled. | P0 |
| DATE-005 | Dependency provenance | Every relationship endpoint is a structural root, including Auto Date/Time relationships. | `SemanticDependencyAnalyzer.cs:169-195,1140-1167` | Relationships are valid model structure and need their endpoints. The code has no generated-relationship provenance. Desktop separately identifies hidden date-table relationships. **STRONGLY-SUPPORTED** | High | An unused user date column is reported as structurally required only because of generated plumbing. | A broad exclusion could hide a genuine user relationship if markers are weak. | Partly; marker and distinction settled, desired Assure product label is a product decision. | P0 |
| TAB-004 | Duplicate ranks | Warn when comparable page items share a non-negative rank within the same immediate group scope. | `DuplicateTabOrderRule.cs:16-22` | Group-aware Desktop experiment and hierarchy resolver. Runtime uses strict rank comparisons: equal items do not form a sequential tie. **PROVEN-RUNTIME** | High | Current rule also counts hidden items; malformed scopes are conservatively omitted. | Without the warning, one equal-ranked item may be skipped in sequential navigation. | Yes; settled except exact source-order fallback at identical geometry. | P0 |
| TAB-006 | Group hierarchy | Groups are inventory objects outside visual count; duplicate ranks are compared per immediate parent scope. | `PbirReportParser.cs:426-438`; `VisualGroupHierarchyResolver.cs:29-40`; `DuplicateTabOrderRule.cs:18` | Desktop-authored grouped fixture plus hierarchical focus and recursive normalization code. **PROVEN-RUNTIME / PROVEN-EXPERIMENT** | High | Wrong scoping would report valid ranks in different groups as duplicates. | Flat analysis could miss a duplicate inside one group. | Yes; settled. | P1 |
| TAB-001 | Missing rank | Missing `position.tabOrder` is included but has no explicit position. | `VisualInventory.cs:25-29`; `TabOrderStatesFixtureTests.cs:32-39` | Controlled Desktop experiment and exact included-list filter (`undefined || >= 0`). **PROVEN-RUNTIME / PROVEN-EXPERIMENT** | High | Treating all missing items as a known displayed position would misstate order. | Excluding missing items would suppress accessibility findings. | Yes; inclusion settled; all-missing ordering remains open. | P1 |
| TAB-008 | Save normalization | Assure displays persisted ranks and friendly positions but does not model normalization triggers. | Reporting position helpers; no production normalizer | Earlier save experiment showed `2000,1000,0,-1`. Runtime normalization service supplies the general algorithm. **PROVEN-RUNTIME** | Medium | Documentation can incorrectly imply values are stable canonical IDs. | Could miss suspicious non-canonical states if a future rule relies on raw gaps. | Yes; substantially settled. | P1 |
| DATE-001 | Local date marker | A table is system generated only with `annotation __PBI_LocalDateTable = true`. Name, hidden state, and private state alone are insufficient. | `TmdlSemanticModelParser.cs:262-279` | Genuine Desktop TMDL plus runtime annotation test; name is only a fallback in a separate reconstruction path. **PROVEN-RUNTIME / PROVEN-EXPERIMENT** | High | A name-based rule would exclude user tables that happen to match the convention. | An annotation spelling/value variant could be missed. | Yes; settled for inspected build. | P1 |
| DATE-002 | Template marker | A table is a generated date template only with `annotation __PBI_TemplateDateTable = true`. | `TmdlSemanticModelParser.cs:269-270` | Genuine Desktop-authored fixture includes the annotation, `isHidden`, `isPrivate`, `DefaultItem`, and date hierarchy. No decision branch was found in installed JS. **PROVEN-EXPERIMENT** | Medium | Name/private-only matching could suppress a user object. | A future marker change would count template objects as developer-owned. | Partly; persisted fact settled, runtime branch not found. | P1 |
| DATE-003 | Auto Date switch | Assure does not inventory model-level Auto Date/Time state. | No current parser field; source annotation is in `definition/model.tmdl` | Desktop tests enabled state using annotation name `__PBI_TimeIntelligenceEnabled` and string value `1`. **PROVEN-RUNTIME / PROVEN-EXPERIMENT** | Medium | Without state, explanations cannot distinguish disabled, enabled-with-no-eligible-column, and stale artifacts. | Same. | Yes; enabled test and disable synchronization path settled. | P1 |
| TAB-003 | Rank direction | Friendly position is derived with larger persisted ranks first. | Reporting ordering helpers and grouped-tab-order tests | Focus finder starts above all valid ranks and chooses the greatest eligible rank, then descends. **PROVEN-RUNTIME** | High | Reversing order gives misleading accessibility explanations. | Same. | Yes; settled. | P1 |
| DATE-004 | Source linkage | Assure sees the local-date relationship endpoints, but does not inventory the source column's `variation` / `defaultHierarchy` metadata or `joinOnDateBehavior`. | `TmdlSemanticModelParser.cs:796-827`; no variation parser | Desktop TMDL links `Sales.Date` to the local table through all three constructs. **PROVEN-EXPERIMENT** | Medium | Relationship-only explanations overstate user authorship and omit stronger provenance. | Missing a variation-only future shape could undercount dependency evidence. | Partly; current persisted shape settled. | P2 |
| TAB-002 | Negative ranks | Every negative value means excluded; zero and positive values mean explicit inclusion. | `VisualInventory.cs:25-29` | Exact included/excluded filters use `< 0`, not equality with `-1`; existing experiment includes `-9999000`. **PROVEN-RUNTIME / PROVEN-EXPERIMENT** | Low now | A `=== -1` regression would flag other negative values as included. | Same. | Yes; settled and already tested. | P2 |
| TAB-007 | Broken group metadata | Missing, ambiguous, or cyclic group ancestry is omitted from duplicate comparison. | `VisualGroupHierarchyResolver.cs:64-93` | Conservative Assure policy; Desktop behavior for hand-corrupted ancestry was not tested. **INFERENCE** | Medium | Corrupt objects might avoid an otherwise valid duplicate finding rather than create one. | Duplicate rank in a broken scope can be missed. | Yes, with a corruption experiment; Desktop may reject first. | P2 |
| REF-001 | Formatting selectors | A high-confidence persisted selector does not establish direct usage; active/ambiguous selectors do. | `PbirVisualReferenceClassifier.cs:30-81`; `SemanticReportReferencePolicy.cs:7-9` | Binding/queryRef correlation and passive-property heuristic; Desktop-formatted evidence fixtures. **PROVEN-EXPERIMENT / INFERENCE** | High | Stale selector identity can keep deleted fields apparently used or generate unresolved findings. | Over-aggressive stale classification can hide a live conditional-formatting dependency. | Potentially, with controlled format/rebind/save experiments. | P2 |
| BIND-001 | Report/model binding | `byPath` requires a locally available exact normalized path; remote connection is unresolved; unspecified connection falls back to same-name model. | `ReportModelBinder.cs:7-29` | PBIP definition behavior and backwards-compatible fallback. The same-name fallback is not a Desktop semantic proof. **INFERENCE** | Medium | Wrong model can absorb references in multi-model or renamed projects. | A valid legacy local pairing can be missed if fallback is removed. | Partly, with versioned PBIP experiments. | P2 |
| DEP-001 | Relationship defaults | Missing `isActive` means true; missing cross-filtering means `oneDirection`; cardinalities default many-to-one. | `TmdlSemanticModelParser.cs:818-827` | TMDL default-elision assumption. **STRONGLY-SUPPORTED** | Medium | Incorrect activation and dependency explanations for valid elided TMDL. | Same. | Yes, through TMDL schema/model-service round trips. | P2 |
| NAV-001 | Missing action `show` | A persisted visual link without `show` is treated as enabled. | `PbirVisualActionParser.cs:23-41` | Missing-vs-default assumption; genuine actions usually persist a literal. **INFERENCE** | Medium | A dormant/default-off action could create missing-target findings. | Treating it as off could miss a broken effective action. | Yes, with a minimal visual-link round trip. | P3 |
| INV-001 | Container kind | Presence of `visualGroup` makes a group; every other `visual.json` is treated as a visual, even with no `visual` object. | `PbirReportParser.cs:426-458` | Known PBIR group discriminator; unknown structural container kinds have not been exhaustively proved. **PROVEN-EXPERIMENT / INFERENCE** | Medium | A future structural container could inflate visual count and accessibility findings. | A new renderable type encoded differently could be missed. | Yes, when a new Desktop-authored kind is encountered. | P3 |
| PBIR-001 | Hidden default | Missing `isHidden` is treated as visible. | `PbirReportParser.cs:485-498` | Boolean default-elision assumption. Runtime only creates components when effective `isHidden` is false. **STRONGLY-SUPPORTED** | Medium | If omission inherited hidden state elsewhere, visibility-sensitive findings would be wrong. | Same. | Yes, with a save comparison. | P3 |
| SCHEMA-001 | Schema drift | Schema family/version is observed, but parsing continues property-wise for recognised unverified versions. | `PbirSchemaObservationFactory.cs:36-85`; parser call sites | Explicit Assure compatibility policy backed by exact Desktop fixtures. **PROVEN-EXPERIMENT** | Medium | New semantics can be parsed as old and produce confident findings. | Refusing all new versions would discard still-valid evidence. | Desktop can supply fixtures, not a universal compatibility guarantee. | P3 |
| QNA-001 | Generated Q&A references | Unresolved Q&A references under `.queryState.` or `.sortDefinition.` are ignored. | `SemanticUsageReconciler.cs:63-65,185-189` | Narrow evidence-path and visual-type heuristic for generated Q&A state. **INFERENCE** | Medium | A real stale binding in those locations can be suppressed. | Without it, generated Q&A metadata produces noisy unresolved findings. | Potentially, but Q&A retirement reduces return on investigation. | P4 |
| DAX-001 | DAX dependency extraction | DAX references are found by a bounded parser/extractor rather than the Desktop formula engine. | `DaxReferenceExtractor.cs`; `SemanticDependencyAnalyzer.cs:737-930` | Deliberately conservative implementation with limitations surfaced separately. **STRONGLY-SUPPORTED** | Medium | Ambiguity can retain unused fields or surface unresolved dependencies. | Unsupported syntax can leave used objects apparently unused. | Desktop can provide comparative fixtures, but not fully settle an independent parser. | P4 |
| CONF-001 | Limitation confidence | Unanalysed referential constructs qualify only absence-based states; established positive states remain established. | `SemanticUsageConfidenceQualifier.cs:21-34,63-68` | Additive-edge reasoning: skipped constructs can add evidence but not retract collected evidence. **INFERENCE (product policy)** | Medium | A future construct that changes interpretation, not just edges, could leave a false positive state overconfident. | Qualifying every positive state would make the report less actionable. | No single Desktop branch can settle the general policy. | P4 |
| THEME-001 | Theme defaults | Exact saved-vs-theme comparison is limited to unscoped clustered-column-chart `title.fontSize`; missing local value is not invented as a literal. | `ThemeFormattingComparisonAnalyzer.cs:8-62` | Intentionally bounded mapping backed by Desktop evidence. **PROVEN-EXPERIMENT** | Low | Broadening by analogy could misstate internal defaults. | Current narrow mapping misses other real theme overrides. | Yes, property by property. | P4 |

The register contains **24 assumptions**. The top ten by combined user impact and evidence opportunity are TAB-005, DATE-005, TAB-004, TAB-006, TAB-001, TAB-008, DATE-001, DATE-002, DATE-003, and TAB-003.

## Tab-order semantics

### Existing Assure behavior

`VisualInventory` uses three deliberately separate concepts:

- negative rank: explicitly excluded;
- zero or positive rank: explicitly included;
- missing rank: included using Power BI's default order, but not assigned an invented persisted position.

`DuplicateTabOrderRule` compares explicit non-negative ranks at the page root or within the same immediate group. `VisualGroupHierarchyResolver` excludes corrupt or unresolved ancestry from comparison. The renderer turns raw descending ranks into one-based friendly positions but still shows the raw PBIR value.

### Existing experiment evidence

The prior controlled Desktop experiment established:

- `3000`, `0`, and a missing property are included;
- `-9999000` is excluded;
- the missing property was navigated first in the mixed test;
- opening did not itself persist the missing property;
- a later save normalized ranks to values such as `2000`, `1000`, `0`, and `-1`;
- a large negative exclusion was normalized to `-1` when it was the only excluded item.

Those states are pinned by `tests/fixtures/tab-order-states` and `TabOrderStatesFixtureTests`.

### Desktop runtime evidence

The useful runtime modules were:

- `desktop.min.js`, module **692567**: canvas authoring, paste ranking, included/excluded list filter, group handling;
- module **455533**: `CanvasListOrderNormalizeService` and its rank bounds, spacing, and normalization algorithm;
- module **739782**: deserialization, hierarchy upgrade, recursive group/list normalization, serialization orchestration;
- module **928373**: `TabOrderFocusFinder`;
- module **364619**: DOM attributes and focus navigation modes;
- module **372032**: group component bindings, including group rank and hierarchical focus mode;
- canvas component-host code around byte offset 8.56M: hidden-item component creation/destruction and visible-sibling DOM ordering;
- visual container component code around byte offset 8.64M: `getVisualContainerTabindex`.

Only short predicates and constants were needed to establish behavior; none are copied here as application code.

### Proven semantic model

#### Inclusion and exclusion

**PROVEN-RUNTIME:** Desktop's authoring list filter places an item in the included list when its master-layout rank is missing or non-negative. It places an item in the excluded list when the rank is negative. The test is sign-based, not `=== -1`.

This exactly supports Assure's current `IsInTabOrder`, `HasExplicitTabOrder`, and `IsExplicitlyExcludedFromTabOrder` definitions.

#### Positive rank direction

**PROVEN-RUNTIME:** forward `TabOrderFocusFinder` traversal starts above the supported rank range and chooses the greatest eligible rank below the current rank. Forward order is therefore descending: `2000`, then `1000`, then `0`. Reverse navigation ascends.

#### Missing plus explicit ranks

**PROVEN-RUNTIME, with one bounded unknown:** when at least one item has a rank, list normalization includes missing items in the positive bucket. The normalizer sorts positive values ascending; Lodash places `undefined` after numeric values; it then assigns increasing ranks starting at zero. Since focus traversal runs from larger to smaller ranks, a single missing item is promoted ahead of all explicit items in the mixed state. This matches the existing experiment.

The exact relative order of several simultaneously missing siblings before a first save is not fully pinned. When every item is missing, hierarchy/list normalization returns without materializing ranks, so geometry/DOM ordering becomes relevant. That all-missing ordering is **UNKNOWN** and Assure should continue to say “Power BI default order” rather than inventing positions.

#### Duplicate ranks

**PROVEN-RUNTIME:** rank comparisons are strict. From the initial state, the first eligible DOM item at the greatest rank is selected. A later sibling with the same rank does not replace it. After focus moves from that item, another equal-ranked item does not satisfy the “next lower rank” predicate and can be skipped.

Visible sibling components are inserted in order of rounded absolute `y`, then rounded absolute `x`. Thus the practical first-DOM winner is normally the uppermost, then leftmost, sibling inside the same scope. Source order is still relevant if both rounded coordinates are identical. Duplicate ranks are therefore a real accessibility defect, not just non-canonical metadata.

#### Hidden visuals

**PROVEN-RUNTIME:** the canvas host filters `isHidden` items from shown siblings, refuses to create their visual/group components, destroys components when an item becomes hidden, and treats hidden ancestors similarly during relocation. A hidden visual can retain `tabOrder`, but there is no focusable canvas component for it in the effective view.

Assure's `PBI-ACCESS-001` missing-alt-text rule and `PBI-ACCESS-002` duplicate-rank rule should exclude effectively hidden items. The latter needs inherited group visibility, not only `visual.IsHidden`. `PBI-ACCESS-003` already filters directly hidden visuals before warning that an object is excluded from tab order.

#### Groups and nested groups

**PROVEN-RUNTIME / PROVEN-EXPERIMENT:** groups are structural navigation scopes. Their container carries a rank at its parent scope, while hierarchical focus traversal filters candidates to those whose tabbable parent is the current group. Child ranks are interpreted inside that group. Nested groups recurse the same way.

Desktop's normalization path first ensures groups have ranks. When upgrading a flat state, a group receives a rank derived from its descendants, then each scope is normalized recursively. This supports Assure's immediate-parent duplicate scopes and its choice not to count groups as visuals. A group can participate in the navigation structure without being a user-facing report visual/card.

#### Normalization and save behavior

**PROVEN-RUNTIME:** normalization is not an unconditional rewrite. In module 455533 the relevant constants are:

- positive start: `0`;
- negative start: `-1`;
- supported bounds: approximately `-10,000,000` to `10,000,000`;
- preferred maximum step magnitude: `1,000`;
- minimum tolerated same-sign gap: `5`.

Normalization is requested when:

- a rank is missing in a bucket that otherwise participates;
- a rank exceeds the supported extremity;
- adjacent same-sign ranks are less than five apart.

For included items, Desktop sorts ascending and assigns values from `0` upward, normally in steps of `1,000`; runtime navigation then visits them in descending order. For excluded items, it sorts in the opposite direction and assigns `-1`, then progressively more-negative values, normally in steps of `-1,000`. Therefore “all exclusions canonicalize to `-1`” is false when several excluded items need distinct list-order values. Their shared semantic meaning remains exclusion.

If every item has a missing tab rank, the list normalizer returns without materializing ranks. Groups are normalized recursively. Opening may normalize the in-memory contract without immediately changing PBIR; persistence occurs when a later save serializes the changed model, consistent with the existing experiment.

### Recommended Assure changes

1. Preserve the current sign and missing-value classification; it is now runtime-proven.
2. Add effective-visibility filtering to missing-alt-text and duplicate-rank evaluation, including hidden group ancestry.
3. Keep duplicate ranks as a warning and explain that one equal-ranked item can be skipped.
4. Document descending raw-rank direction and conditional normalization; do not treat `-1` as the only exclusion encoding.
5. Keep “Power BI default order” for all-missing and multiple-missing cases until their exact stable ordering is pinned.

## Microsoft-generated date artefacts

### Existing Assure behavior

`TmdlSemanticModelParser.SystemGeneratedKind` recognizes only exact true-valued annotations:

- `__PBI_LocalDateTable` → `AutoDateTimeLocalTable`;
- `__PBI_TemplateDateTable` → `AutoDateTimeTemplateTable`.

System tables remain in inventory and dependency analysis, but are excluded from developer-owned object counts. Relationship analysis has no provenance flag, so both endpoints of every relationship become structural roots.

### Desktop and fixture evidence

The genuine Desktop-authored `desktop-semantic-constructs` fixture establishes the complete persisted shape:

- `Sales.Date` is `dateTime` and has a `variation` whose `relationship` names the generated relationship and whose `defaultHierarchy` points to `LocalDateTable_<GUID>.'Date Hierarchy'`;
- the relationship has `joinOnDateBehavior: datePartOnly`, from `Sales.Date` to the local table's `Date` key;
- the local table is hidden, variation-only, calculated from the minimum/maximum of `Sales.Date`, and annotated `__PBI_LocalDateTable = true`;
- the template table is hidden and private, has a calculated one-day calendar, the standard date hierarchy, `__PBI_TemplateDateTable = true`, and `DefaultItem = DateHierarchy`;
- `model.tmdl` contains `annotation __PBI_TimeIntelligenceEnabled = 1`.

In `desktopDaxQueryView.min.js`, module **23315** builds a conceptual schema and contains two different tests:

1. a table is treated as private/local-date infrastructure when it is private or carries the exact `__PBI_LocalDateTable` annotation;
2. a `LocalDateTable_<GUID>`-shaped name is accepted only in a fallback that reconstructs a missing hidden date table and date key from relationship rows.

That separation is important. A matching name is evidence about a fallback serialization/reconstruction shape, not a sufficient general classifier for a persisted TMDL table.

The same module identifies many-to-one relationships from ordinary date columns to hidden marked local date tables and records them in a dedicated `tableToColumnsRelatedToHiddenDateMapping`. It also produces a distinct `onlyUsesHiddenDateTable` condition. **STRONGLY-SUPPORTED:** Desktop itself treats these relationships as a special system category for conceptual-schema reduction, rather than blindly equating them with ordinary user model relationships.

In `desktopTmdlView.min.js`, the TMDL apply service's `isTimeIntelligenceEnabled` returns true only when model annotations contain name `__PBI_TimeIntelligenceEnabled` with value `1`. When applying TMDL changes transitions a previously enabled model to disabled, Desktop prompts to synchronize date tables and, on confirmation, sends a modeling change with `setTimeIntelligence.enabled = false`.

### Reliable generated-object markers

| Object/state | Reliable evidence | Grade | Assure policy |
|---|---|---|---|
| Local generated date table | `annotation __PBI_LocalDateTable = true` | PROVEN-RUNTIME / PROVEN-EXPERIMENT | Keep exact annotation as primary classifier. Do not classify by name or hidden state alone. |
| Date template table | `annotation __PBI_TemplateDateTable = true` | PROVEN-EXPERIMENT | Keep exact annotation. Name/private/hidden shape is supporting evidence only. |
| Auto Date/Time enabled | model annotation `__PBI_TimeIntelligenceEnabled = 1` | PROVEN-RUNTIME / PROVEN-EXPERIMENT | Add optional model-level inventory for explanations and consistency checks. |
| Source column linkage | variation relationship + default hierarchy; date-part-only many-to-one relationship; local table Calendar expression references source column | PROVEN-EXPERIMENT | Capture variation and `joinOnDateBehavior` as provenance/evidence rather than relying on generated table name. |

### Dependency implication

The generated relationship is real model structure: Desktop needs its endpoints while that generated object exists. It is therefore incorrect to erase the edge or call it nonexistent. It is also misleading to present the source user column as structurally required in the same user-authored sense as an ordinary relationship, hierarchy, sort-by rule, security expression, or measure dependency.

Recommended model:

- retain the dependency edge;
- attach provenance such as `SystemGeneratedAutoDateTime` to the relationship/variation path;
- compute/report both “reachable from any model structure” and “reachable from user-authored model structure”;
- when the only structural path is generated Auto Date/Time, label the object as system-required or generated-only rather than `StructurallyRequired` without qualification;
- never suppress a user relationship merely because one endpoint resembles a generated name.

This change should be fixture-driven. The annotation is reliable enough to identify the generated table; provenance of the relationship can be established when it targets that marked table and is corroborated by `joinOnDateBehavior`, variation metadata, or the local-date calculated expression.

### Auto Date/Time disabled and save behavior

The inspected runtime settles the state test and the synchronization command, but not every physical mutation made by the native modeling service.

- Enabled is exact annotation value `1`. **PROVEN-RUNTIME.**
- Removing/changing that enabled annotation through TMDL can trigger a prompt followed by `setTimeIntelligence.enabled = false`. **PROVEN-RUNTIME.**
- The enabled Desktop-authored fixture contains template/local tables, variations, and the relationship. **PROVEN-EXPERIMENT.**
- The exact ordering and atomicity with which every generated table, relationship, and variation is removed after disabling remains **UNKNOWN** because that work occurs behind the modeling-service call and no new off-state round trip was committed in this pass.

Assure should not yet emit an integrity finding merely because the enabled annotation and generated objects appear inconsistent. It may safely inventory the state and use it in explanations.

### Recommended Assure changes

1. Add relationship provenance sufficient to identify generated Auto Date/Time paths without name-only matching.
2. Capture model-level `__PBI_TimeIntelligenceEnabled` state.
3. Capture column variation/default-hierarchy and `joinOnDateBehavior` evidence.
4. Split user-facing structural classification from system-only structural reachability while retaining all edges.
5. Add an enabled→disabled controlled fixture before enforcing consistency/removal rules.

## Other high-value assumptions discovered

### TMDL defaults

Relationship parsing supplies defaults for elided `isActive`, cross-filtering, and cardinality. These are plausible and consistent with normal Desktop serialization, but exact schema/model-service evidence should be pinned before those defaults are used for higher-severity behavioral findings.

### Actions and missing values

`PbirVisualActionParser` treats a missing `show` property as enabled. This is a classic missing-vs-default assumption and should get a small Desktop round-trip fixture. Dynamic properties are handled more safely: Assure reports review-required and avoids definitive target validation when the effective enabled state is unknown.

### Container classification

The `visualGroup` discriminator is well established. The fallback “not a group means a visual” can become fragile if PBIR introduces another structural `visual.json` container. Schema observations help diagnose version drift but do not change parsing behavior.

### Reference reconciliation

Formatting-selector relevance remains the highest-risk non-tab/report assumption. The current rule is intentionally bounded, but “passive properties plus no property semantic reference” is an Assure heuristic, not a located Desktop liveness predicate. Continue to prefer exact Desktop-authored before/after fixtures for each formatting shape.

### Dependency types already covered

The wider hunt confirmed that Assure already models more than ordinary DAX and relationships, including:

- calculated columns, measures, calculated table partitions, calculation items, and calculation-item format-string expressions;
- hierarchy levels and sort-by columns;
- field parameters and their generated hidden fields column;
- aggregation `alternateOf` mappings;
- refresh-policy change-detection columns;
- roles, object-level permissions, and perspectives;
- report measures and bounded `USERELATIONSHIP` activation evidence;
- Power Query table and column lineage.

The clearest missing date-specific dependency evidence is TMDL column variation/default hierarchy plus `joinOnDateBehavior`. The clearest general risk remains syntax coverage in independent DAX/M extractors; existing analysis limitations are the correct mechanism for absence-based confidence.

## Accessibility findings

Desktop's runtime baseline supports the following Assure positions:

- missing tab rank does not mean excluded;
- every negative rank means excluded;
- duplicate explicit ranks are actionable because equal-ranked items can be skipped;
- group hierarchy is part of navigation semantics;
- hidden items should not receive runtime-focus findings;
- raw ranks are order metadata, not user-facing ordinal positions or stable identifiers.

No Desktop checker rule was copied or treated as automatically desirable. The recommendations come from effective focus behavior, not from matching a Microsoft warning catalog.

## Remaining unknowns

1. Stable relative ordering when every visual has no tab rank.
2. Stable relative ordering of several missing-rank siblings before first save.
3. Final fallback when duplicate-ranked siblings also have identical rounded `x`/`y` geometry.
4. Desktop behavior for manually corrupted missing, ambiguous, or cyclic group ancestry.
5. Exact native-model mutations and ordering after Auto Date/Time is disabled.
6. Whether `__PBI_TemplateDateTable` has an independently visible JS/native decision branch in this build; the persisted marker itself is proven.
7. Whether a missing visual-action `show` property is effective-on in every supported PBIR version.
8. Exact default semantics for future or unverified PBIR schema versions and new container kinds.
9. A Desktop-native liveness rule for all formatting selector forms.

## Ranked implementation tasks

1. **Effective hidden accessibility scope:** teach accessibility rules to exclude directly hidden visuals and descendants of hidden groups; add Desktop-evidence tests for both.
2. **Generated structural provenance:** retain Auto Date edges but separate system-only from user-authored structural reachability and wording.
3. **Date metadata inventory:** parse model Auto Date/Time state, column variations/default hierarchy, and relationship `joinOnDateBehavior`.
4. **Duplicate-rank explanation:** clarify that one item can be skipped and that comparisons are within hierarchical sibling scope.
5. **Normalization documentation/tests:** pin descending runtime order, conditional 1,000 spacing, multiple negative normalization, and all-missing non-materialization without reproducing Desktop code.
6. **Auto Date off fixture:** create a minimal Desktop-authored enabled/off pair and inspect the generated-object diff.
7. **Action missing-default fixture:** settle missing `visualLink.properties.show` before changing `NAV` findings.
8. **Formatting-selector matrix:** extend live/stale before/after Desktop fixtures by selector kind and dynamic property shape.
9. **Legacy binding matrix:** pin when same-name report/model fallback is valid versus ambiguous.
10. **Corrupt hierarchy fixture:** only if Desktop accepts the project sufficiently to expose deterministic repair/rejection behavior.

## Validation

- `dotnet restore PbiAssure.slnx`: passed; all projects were up to date.
- `dotnet build PbiAssure.slnx --no-restore`: passed with 0 warnings and 0 errors.
- `dotnet test PbiAssure.slnx --no-build`: passed with 513 core tests and 2 privacy end-to-end tests.
- `dotnet format PbiAssure.slnx --verify-no-changes --no-restore`: reports the existing 24 whitespace findings in `ThemeReviewAnalyzer.cs` and `HtmlReportRenderer.ThemeReview.cs`; neither file is changed by this branch.
- `git diff --check`: passed.

No test was added because the production semantics established here are already pinned where appropriate, and the new corrective cases require a reviewed behavior change or a new Desktop-authored fixture.

## Branch scope

This is a public-safe diagnostic document. It contains no report/customer identifiers, copied binaries, vendored resources, or substantial Microsoft source. No production behavior was changed and no speculative expectation was added to tests.
