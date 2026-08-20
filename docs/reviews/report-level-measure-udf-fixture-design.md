# Report-level measure UDF dependency investigation

**Date:** 2026-08-20

**Starting commit:** `590b94a84a981b027db2b5b3a0d2e28cb507da57`

**Scope:** code-path investigation and Power BI Desktop fixture design only. No parser, dependency,
classification, report or fixture behaviour changed.

Evidence labels: **[verified]** means checked in this repository; **[verified by Microsoft primary
documentation]** means stated in current Microsoft Learn documentation; **[inferred]** means the exact
Desktop-emitted bytes still need the experiment below.

## Result

PBI Assure already captures a report-level measure's complete DAX `Expression` from
`<report>.Report/definition/reportExtensions.json`. The missing link is entirely downstream:
`SemanticDependencyAnalyzer.AnalyzeReportMeasures` follows only the structured `References` collection
and never reads `measure.Expression`. [verified]

The existing DAX machinery is suitable for the missing work. `DaxReferenceExtractor.Extract` already
accepts known table and function names, identifies calls to declared UDFs, and distinguishes those calls
from built-ins. `ModelLookup.TryResolveDax` already supplies the owning-table context required for an
unqualified `[Name]`. `AddDaxDependencies` already turns those results into `Dax` and `FunctionCall`
edges. [verified]

Implementation should nevertheless wait for one Desktop-authored fixture. The repository has no actual
`reportExtensions.json`; every current report-measure test constructs the JSON synthetically. The fixture
must establish what Desktop writes for a UDF call, particularly whether `references.measures` is empty,
whether another reference collection appears, and whether `unrecognizedReferences` changes.

## Current code path

1. `PbirReportParser.ParseReportExtensions` opens
   `<report>.Report/definition/reportExtensions.json`.
2. It reads the root extension `name`, then each `entities[]` table identity and `measures[]` item.
3. `ReportMeasureInventory` preserves `Entity`, `Name`, `DataType`, the full `Expression`, formatting and
   presentation properties, `HasUnrecognizedReferences`, `References`, and the evidence path.
4. `ReadMeasureReferences` reads only `references.measures[]`. A nonblank `schema` identifies a reference
   to another report measure; no schema identifies a semantic-model measure.
5. `SemanticUsageReconciler` recognises a visual reference matching the report measure's entity/name and
   keeps it separate from model-measure usage.
6. `SemanticDependencyAnalyzer.AnalyzeReportMeasures` registers every report measure as an internal graph
   node, makes one a root when active report metadata uses it, and follows `measure.References` to model or
   report measures.
7. **It never reads `measure.Expression`.** A direct UDF call, table/column reference, or any other
   expression-only dependency therefore creates no edge.

## Smallest likely implementation path — not started

For a report that is already bound to a local model:

1. create the same `ModelLookup` used by model DAX analysis;
2. use the report measure's `Entity` as the DAX owning-table context;
3. pass `measure.Expression`, the report-measure graph node, and `measure.RelativePath` through the
   existing DAX/UDF extraction and resolution path;
4. retain the structured `references.measures` edges as the authoritative report-measure metadata;
5. avoid emitting a second expression-derived edge for a target already represented structurally.

The fifth point must be decided against the Desktop fixture rather than guessed. Parsing the full
expression may rediscover a model measure already present in `references.measures`, but UDF calls and
explicit column/table references may exist only in the expression. The desired graph is one truthful edge
per meaningful dependency, not parallel `ReportMeasure` and `Dax` edges describing the same reference.

No new DAX parser, UDF resolver, graph traversal, usage state or confidence state is indicated.

## Architectural boundary discovered

Microsoft documents the supported scenario specifically as a **report-based measure in a live-connect
report** calling a UDF declared in the source model. It also states that the UDF has no IntelliSense in
that formula bar. A model-based measure in a composite model cannot call a UDF declared in the source
model. See [DAX user-defined functions](https://learn.microsoft.com/en-us/dax/best-practices/dax-user-defined-functions#considerations-and-limitations)
and [Connect to semantic models in Power BI](https://learn.microsoft.com/en-us/power-bi/connect-data/desktop-report-lifecycle-datasets).
[verified by Microsoft primary documentation]

That means a genuine Desktop fixture is expected to have `datasetReference.byConnection`, not a local
`byPath` model connection. PBI Assure deliberately treats `byConnection` as remote:
`ReportModelBinder.FindLocalModel` returns `null`, and report-measure dependency analysis stops because it
has no local model inventory against which to resolve names. [verified]

The fixture can therefore prove Desktop serialization and the report parser's input, but it cannot by
itself prove end-to-end dependency classification in the current offline scanner. The later implementation
should use:

- the Desktop fixture to pin emitted `reportExtensions.json`, `definition.pbir`, and visual-reference
  shapes; and
- a clearly labelled synthetic local-model test to pin report-measure → UDF → model-object traversal.

Do not rewrite the Desktop fixture's connection to `byPath`, and do not broaden remote-model binding as a
side effect of this feature.

## Exact Desktop experiment

Use a disposable copy of `tests/fixtures/desktop-udf-references` as the source model. It already contains
the synthetic `Sales` table, model measure `[Total Amount]`, and UDF
`Doubled = () => [Total Amount] * 2`. Its three rows total 1,250, so `Doubled()` should return 2,500.

### A. Publish the synthetic source model

1. Copy the fixture outside the repository and open its `.pbip` in the current Power BI Desktop release.
2. Confirm `Sales[Total Amount]` exists and the `Doubled` function remains in
   `definition/functions.tmdl`/Model explorer.
3. Publish the project to a disposable test workspace. The author must have Build permission on the
   resulting semantic model.
4. Record the exact Desktop version, date, workspace type, compatibility level and synthetic-data
   provenance. Do not record credentials or tokens.

### B. Create the live-connected report

1. Start a new Power BI Desktop report.
2. Choose **Home → Get data → Power BI semantic models** (the current OneLake Catalog), select the
   published synthetic model, and choose **Connect**.
3. Confirm Desktop indicates a live connection and that `Sales` and `[Total Amount]` appear in the Data
   pane.
4. Right-click the `Sales` table and choose **New measure** (or select `Sales`, then
   **Modeling → New measure**). In a live-connected report this creates a report measure rather than
   changing the source model.
5. Enter exactly `Report UDF Result = Doubled()`. Type the function name manually: Microsoft documents
   that live-connect report measures can call source-model UDFs but currently receive no IntelliSense.
6. Create a second diagnostic control measure on `Sales`:
   `Report Measure Control = [Total Amount]`. This is intentionally separate from the UDF measure so the
   UDF-only expression stays unambiguous while the fixture also shows how Desktop writes an ordinary
   structured measure reference.
7. Add a Card visual and bind it only to `Report UDF Result`. Confirm the value is 2,500.
8. Save as a Power BI Project named `desktop-report-measure-udf` in a temporary folder outside the
   repository. Do not convert the live connection to a composite/local model.

If Desktop refuses `Doubled()` or changes the connection mode, stop and record the exact message and
emitted state. Do not substitute a model measure, hand-edit the project, or describe altered metadata as
Desktop-authored.

### C. Prove save/reopen stability

1. Close Desktop completely and snapshot the generated project outside its folder.
2. Reopen the `.pbip`.
3. Confirm the Card still evaluates to 2,500 and the report measure formula still reads `Doubled()`.
4. Save without intentional edits, close Desktop, and compare the before/after project files.
5. Record every changed file. Exclude `.pbi/cache.abf` and machine-specific local settings from the
   committed fixture, following the existing fixture policy.

## Files and evidence to inspect

Inspect these Desktop-emitted files before any fixture is committed:

| File | Evidence required |
|---|---|
| `<report>.Report/definition/reportExtensions.json` | Root schema/name; entity identity; both measure names, data types and exact expressions; complete `references` objects; `unrecognizedReferences`; any previously unseen fields or collections |
| `<report>.Report/definition.pbir` | The actual `datasetReference.byConnection` shape and whether any stable source-model identity is exposed |
| `<report>.Report/definition/pages/<page>/visuals/<visual>/visual.json` | How the Card identifies `Sales[Report UDF Result]`, including any extension/schema marker and query reference |
| `<report>.Report/definition/report.json` and `version.json` | Report/PBIR schema versions needed to interpret the fixture |
| Source `definition/functions.tmdl` | The exact declared `Doubled` identity and expression used by the live-connected report |
| Source `definition/tables/Sales.tmdl` | The model measure and column chain reached by `Doubled()` |
| Root `.pbip` and `.platform` files | Normal Desktop project identity/provenance; no hand-authored additions |

Search all emitted report files for `Doubled`, `Report UDF Result`, `Report Measure Control`, and
`Total Amount`. The important evidence is whether `Doubled` occurs only in the report measure expression,
while the control measure appears in both its expression and structured `references.measures` metadata.

## What the fixture would establish

- the real Desktop location and JSON shape of report-level measure expressions;
- the report measure's entity/name identity and the visual's reference to it;
- whether a UDF call is represented anywhere beyond the expression;
- how `references.measures` and `unrecognizedReferences` behave beside UDF and ordinary measure calls;
- that the primary report measure survives close/reopen and still evaluates through the source UDF;
- the precise boundary between Desktop-backed parser evidence and synthetic local graph testing.

## Recommendation

Implementation is blocked on this small Desktop experiment. The code path is likely a narrow reuse of
existing DAX/UDF analysis, but the fixture must first establish which expression references are absent
from structured metadata and how duplicates should be avoided. Visual-calculation parsing remains out of
scope.
