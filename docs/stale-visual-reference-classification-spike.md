# Stale visual reference classification spike

## Status

Research and classification only. No parser, semantic-usage, assurance-rule, HTML, or CSV behaviour was changed during this investigation.

## Executive recommendation

PBI Assure should classify a reference's PBIR context and current relevance independently from whether the referenced semantic-model object resolves.

The safest implementation is not to ignore selectors. It should distinguish:

1. Executable dependencies inside formatting expressions.
2. Selector identities that still correspond to live visual projections.
3. Selector identities that are confidently persisted historical state.
4. Selectors whose relevance cannot be proven either way.

Only high-confidence persisted selectors should eventually be excluded from model-integrity errors or direct semantic usage. Ambiguous cases should remain conservative until more fixtures establish reliable rules.

## Controlled sample findings

| Sample | What PBIR contains | Current PBI Assure result |
|---|---|---|
| Live line formatting | `Category` in `queryState.Series`, supporting `filterConfig`, and `lineStyles.selector.scopeId` | `Category` is Directly used from three references |
| Stale line formatting | Series projection and Category filter removed; identical `lineStyles.scopeId` retained | `Category` remains Directly used solely from the selector |
| Active icon formatting | `Status` absent from projections and filters but present in `properties.icon.value.expr.Conditional` | `Status` is correctly Directly used from three executable expression references |

All three currently have zero unresolved semantic references. Their single finding is an unrelated missing-alt-text warning.

### Live and stale line-chart comparison

In `samples-local/StaleMetadata_Line_01_Formatted`, the visual contains:

- `TestData[Category]` under `queryState.Series`;
- a matching Category entry in `filterConfig`;
- `lineStyles.selector.data.scopeId.Comparison` identifying `Category = "B"`.

After removing the legend field, `samples-local/StaleMetadata_00` removes the first two but retains the complete selector and dashed-line property unchanged.

The structured project files otherwise differ only in this visual. Local Power BI cache and settings files also differ but are irrelevant to this classification.

### Active icon-formatting control

The table in `samples-local/StaleMetadata_table_IconFormat` projects only `Category` and `Value`. Its icon expression reads `Status` in three conditional cases.

The accompanying selector:

- uses `dataViewWildcard`;
- identifies the formatted projection through `metadata = "Sum(TestData.Value)"`;
- does not contain the dependency on `Status`.

This is an important distinction:

- The selector identifies **what is being formatted**.
- The property expression identifies **what data drives the formatting**.

The latter is unquestionably active.

## Current PBI Assure parsing flow

1. `PbirReportParser` passes the complete report, page, or visual JSON to `PbirFieldReferenceExtractor`.
2. `PbirFieldReferenceExtractor` recursively finds every structural `Column`, `Measure`, or `HierarchyLevel`.
3. Each becomes a `VisualFieldReference` containing:
   - table and object identity;
   - broad usage context;
   - role;
   - exact JSON evidence path.
4. Context is inferred from ancestor names:
   - `queryState` -> Projection;
   - filter-like ancestor -> Filter;
   - sort-like ancestor -> Sort;
   - `objects` or `visualContainerObjects` -> Formatting.
5. The extractor does not retain structured information about:
   - the originating formatting object, such as `lineStyles` or `columnWidth`;
   - whether the reference was inside `properties` or `selector`;
   - selector type;
   - projection `queryRef`;
   - selector `metadata`;
   - whether a selector maps to a live projection.
6. `SemanticUsageReconciler` treats every resolved reference as direct report evidence.
7. `SemanticDependencyAnalyzer` uses every directly referenced object as a dependency root.
8. Every unresolved reference reaches `UnresolvedReportReferenceRule`, which produces an Error-level `PBI-MODEL-001` finding.

The unresolved rule groups duplicate paths for the same object on a visual, but discards contextual differences when choosing severity and wording.

## Root cause of stale-reference noise

The recursive extraction is deliberately comprehensive, but downstream processing treats all extracted references as equivalent evidence of active use.

Consequently, all of the following become `UsageContext = Formatting`:

- an executable conditional-formatting dependency;
- a current series identity selector;
- historical selector state left behind by Power BI.

Resolution then answers only "does this object exist?", not "does the visual currently depend on it?"

This causes both problems:

- unresolved stale selectors become misleading Errors;
- resolved stale selectors incorrectly keep objects in Directly used status.

## Proposed reference-context taxonomy

| Reference context | Meaning | Recommended usage treatment | Unresolved treatment |
|---|---|---|---|
| Live query projection | Displayed or grouped visual data | Direct use | Error |
| Sort reference | Current visual sort | Direct use | Error |
| Active filter, drillthrough, or tooltip field | Current behavioural dependency | Direct use | Error |
| Active formatting expression | Field or measure read inside `properties...expr` | Direct use | Error |
| Current selector identity | Selects a live series, category, or value projection | Retain as contextual evidence; normally already direct through its projection | Avoid a duplicate error; the active binding should drive the integrity result |
| Confirmed persisted selector | Passive formatting selector with no current visual identity | Preserve evidence, but do not count as direct use | Exclude from `PBI-MODEL-001`; optionally report grouped Information |
| Ambiguous selector | Current relevance cannot be proven | Conservative treatment initially | Information with Review required, preferably grouped |
| Unknown visual metadata | Unsupported or unclassified context | Preserve separately | Review required, not automatically Error |

"Formatting expression" must include any semantic expression inside formatting properties, not just nodes literally named `Conditional`.

For example, the Columns Usage fixture contains an active colour expression using `Aggregation` directly under `dataPoint.properties.fill`.

## Reliable and unreliable selector signals

### Strong evidence of active relevance

- A structural semantic reference inside a formatting property expression.
- `selector.metadata` exactly matches a current projection `queryRef`.
- A `scopeId` field matches a current grouping, series, or category projection.
- `dataViewWildcard` targets a current projection and the associated property contains an executable semantic expression.
- The same identity is present in current `queryState`, sort, or genuine filter behaviour.

### Strong evidence of persisted state

Use a combination of signals rather than any one condition:

- The semantic reference exists only inside a selector.
- No matching field identity exists in current `queryState`.
- No matching grouping, series, category, sort, or relevant filter identity exists.
- Selector metadata cannot map to any current projection `queryRef`.
- The formatting property is passive and literal, such as line style, width, or alignment.
- A `scopeId` comparison refers to a former grouping field.
- No active property expression consumes the field.

The stale line-chart control satisfies this combination.

### Unreliable signals on their own

None of the following is sufficient independently:

- being under `selector`;
- being outside `queryState`;
- being absent from `filterConfig`;
- resolving or not resolving in the model;
- the formatting object being named `lineStyles`, `columnWidth`, `dataPoint`, or similar;
- using `dataViewWildcard`;
- having a literal formatting value;
- the visual type.

## Other observed PBIR contexts

The local sample survey found several relevant patterns:

- `visualContainerObjects.title[].properties.text.expr` can contain active dynamic title dependencies.
- `objects.dataPoint[].properties.fill...expr.Conditional` contains active conditional formatting.
- `objects.dataPoint[].properties.fill...expr.Aggregation` can be an active dynamic formatting dependency without a `Conditional` ancestor.
- `objects.values[].properties.icon.value.expr.Conditional` contains active icon rules.
- `lineStyles.selector.scopeId.Comparison` is a selector identity that can be current or stale.
- `columnWidth.selector.metadata` and `columnFormatting.selector.metadata` identify formatted projections through query-reference strings.
- `dataViewWildcard` is generic and is not evidence of either activity or staleness by itself.
- `selector.id` identifies formatting instances but is not itself a semantic reference.
- `categoryAxis`, `valueAxis`, labels, data points, and similar objects must be classified by where any semantic expression occurs, not by object name alone.

An additional controlled table comparison shows that a live `columnWidth.selector.metadata = "Sum(TestData.OldValue)"` matched the projection's `queryRef`. In that small example Power BI removed both the projection and column-width selector when `OldValue` was removed. This is useful positive evidence for projection association, but it does not prove Power BI always removes these selectors in long-lived reports.

## Semantic-usage recommendation

For an initial safe implementation:

- Active expressions and live bindings continue to establish Directly used.
- High-confidence stale selectors stop establishing direct usage.
- Selector identity evidence remains retained for diagnostics.
- Ambiguous selectors remain conservative initially, preferably continuing to block an unused conclusion until further fixtures improve confidence.
- Do not create a new public semantic usage state in the first implementation.

Longer term, an internal "ambiguous report evidence" flag may be useful. If later exposed, its wording must preserve the existing principle that Apparently unused never means safe to delete.

Classification must run whether the object resolves or not. A stale reference to an existing column is still stale.

## Integrity finding and severity recommendation

- Active query, filter, sort, drillthrough, or formatting expression targeting a missing object: **Error**.
- Confirmed stale selector: remove it from `PBI-MODEL-001`; optionally produce one **Information** finding per visual.
- Ambiguous selector: **Information plus Review required**, not Error.
- Mixed active and stale evidence for the same missing object: active evidence wins; retain stale paths only as technical details.
- Do not create one stale-metadata finding per field.

Suggested grouped wording:

> This visual contains 23 persisted formatting references that are not linked to its current fields.

Expanded details could list the formatting object, selector type, referenced identity, and evidence path.

## Grouping recommendation

Group stale or ambiguous selector findings by:

- report;
- page;
- visual;
- relevance classification;
- optionally formatting object.

Active missing references should remain prominent and should not be hidden inside a stale-reference group. When one object has both active and stale evidence, the active classification should determine the finding severity.

## Risks and false-negative risks

- Field parameters, calculation groups, hierarchy/date variations, custom visuals, and implicit projections can make association non-trivial.
- `selector.metadata` uses query-reference syntax, including aggregations, rather than a simple table/column identity.
- Some active formatting expressions do not contain a `Conditional` node.
- Some selectors may remain operational even when their identity is represented indirectly.
- `filterConfig` can contain supporting representations of visual fields and should not be treated as an independent proof of user filtering.
- Power BI can evolve PBIR structures between schema versions.
- Removing a stale selector from direct semantic roots can also change downstream DAX dependency classifications.
- Aggressive stale classification could produce false Apparently unused results, which is more harmful than retaining some conservative noise.

## Proposed minimal architecture change

Add structured context to `VisualFieldReference` rather than deriving policy repeatedly from evidence-path strings:

- reference origin: binding, property expression, selector identity, or unknown;
- formatting object and property;
- selector kind: metadata, scopeId, wildcard, total, ID, or unknown;
- relevance: active, persisted, or ambiguous;
- matched projection identity or `queryRef`, where available.

Retain the existing evidence path.

Use a two-stage visual pipeline:

1. Extract raw references and build a live visual-binding index containing field identities, roles, `queryRef`, `nativeQueryRef`, sorts, and filters.
2. Correlate formatting selectors and expressions against that index.

A visual-specific classifier is safer than adding more substring checks to `DetermineUsageContext`. Any serialized inventory change should also trigger a schema-version review.

## Recommended implementation sequence

1. Convert the three controls into deterministic committed test fixtures or equivalent inline tests. Do not make automated tests depend on `samples-local`.
2. Add origin and selector metadata without changing findings or usage.
3. Implement classification in shadow form and assert the three controls.
4. Change unresolved-reference handling only for high-confidence stale selectors.
5. Validate against the work-only report.
6. Address semantic usage in a separate change after integrity behaviour is proven.
7. Add grouped HTML presentation only after the core classification stabilises.

## Recommended regression-test matrix

At minimum, cover:

1. Live line series plus `scopeId` selector -> current selector; Category remains direct.
2. Removed line series plus retained selector -> persisted selector; Category is not direct solely because of it.
3. The same stale selector with Category removed from the model -> no active-binding Error.
4. Icon conditional formatting with non-projected Status -> active dependency; Status remains direct.
5. Missing Status in that expression -> Error.
6. Live `columnWidth.selector.metadata` matching a projection `queryRef` -> current selector.
7. Orphaned column-width metadata -> stale or ambiguous, never automatically Error.
8. Direct `Aggregation` or Measure formatting expressions without a `Conditional` ancestor -> active.
9. Wildcard selector without a semantic property expression -> not assumed active or stale.
10. Query, filter, sort, drillthrough, field-parameter, hierarchy, and Q&A tests remain unchanged.
11. Many stale selectors on one visual -> one eventual grouped finding.
12. A resolved stale selector remains classified as stale despite successful resolution.

Existing tests most likely to regress include:

- `ScanParsesPbirPagesAndVisualsInReportOrder`
- `ScanParsesTmdlAndReconcilesDirectReportUsage`
- `ScanKeepsQnaGeneratedReferencesStrictWithoutTreatingUnresolvedLanguageAsBrokenBindings`
- `ScanReconcilesReportPageAndDrillthroughDependencies`
- HTML semantic-location tests
- CSV semantic-usage tests

## Files likely to require changes later

Core changes would probably involve:

- `src/PbiAssure.Core/Scanning/PbirFieldReferenceExtractor.cs`
- `src/PbiAssure.Core/Scanning/PbirReportParser.cs`
- `src/PbiAssure.Core/Inventory/VisualFieldReference.cs`
- `src/PbiAssure.Core/Inventory/UsageContexts.cs`
- a new visual binding/reference classifier
- `src/PbiAssure.Core/Scanning/SemanticUsageReconciler.cs`
- `src/PbiAssure.Core/Inventory/SemanticUsageEvidence.cs`
- `src/PbiAssure.Core/Inventory/UnresolvedSemanticReference.cs`
- `src/PbiAssure.Core/Assurance/UnresolvedReportReferenceRule.cs`
- `src/PbiAssure.Core/Scanning/ProjectScanner.cs` if the inventory schema changes
- focused tests in `tests/PbiAssure.Core.Tests/ProjectScannerTests.cs`

HTML and CSV renderers should change only later if contextual evidence or grouping becomes user-facing.

## Explicitly out of scope

A subsequent implementation should not:

- ignore all `visual.objects` or all selectors;
- infer staleness solely from absence in `queryState`;
- treat resolution as proof of activity;
- treat non-resolution as proof of staleness;
- suppress active conditional or dynamic formatting dependencies;
- weaken query, filter, sort, or drillthrough integrity checks;
- change semantic usage and finding behaviour in the same first step;
- modify the controlled samples;
- modify navigation or tooltip fixes;
- touch `docs/pbix-ingestion-feasibility.md`.

## Repository state during the spike

- No source, sample, report, test, or existing documentation files were changed.
- Temporary scan outputs were created only under the gitignored `artifacts` directory.
- `docs/pbix-ingestion-feasibility.md` remained untouched.
- No commit or push was performed.
