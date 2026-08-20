# Report-level measure UDF dependency investigation

**Date:** 2026-08-20

**Starting commit:** `707b67ee65477fc04df8fff40a316ef5718fa188`

**Scope:** code-path investigation and Power BI Desktop fixture design only. No parser, dependency,
classification, report or fixture behaviour changed.

Evidence labels: **[verified]** means checked directly in repository code or the preserved
Desktop-authored project bytes; **[reported manual observation]** means recorded from the controlled
Desktop session but not independently recoverable from the saved files; **[verified by Microsoft primary
documentation]** means stated in current Microsoft Learn documentation; **[inferred]** identifies a
conclusion reasoned from those sources rather than directly present in one file.

## Desktop experiment completed

The planned experiment was performed in Power BI Desktop **2.157.879.0 64-bit (August 2026)** against a
published copy of the synthetic `desktop-udf-references` model. The report remained live-connected and
was not converted to a composite/local model. [reported manual observation; the saved `byConnection`
shape is verified in the Desktop-authored bytes]

The result differs materially from the Microsoft documentation that motivated the experiment:

- the published Service model still showed its UDFs [reported manual observation];
- the live-connected Desktop report exposed no Functions node [reported manual observation];
- `INFO.FUNCTIONS()` filtered to UDF origin returned no UDFs [reported manual observation; the saved
  query text confirms what was run, not its result];
- Desktop rejected `Report UDF Result = Doubled()` because it did not recognise `Doubled` as a function
  [reported manual observation];
- Desktop accepted `Report Measure Control = [Total Amount]` and allowed it on a Card [reported manual
  observation; the saved expression, reference and Card binding are verified];
- both report-measure records nevertheless persisted in `reportExtensions.json`, including the rejected
  expression text.

The project and its preserved before-reopen copy contain the same 14 files and 106,274 bytes. SHA-256
comparison found **zero changed, added or removed files** after close, reopen and save. [verified]

## Result

PBI Assure already captures a report-level measure's complete DAX `Expression` from
`<report>.Report/definition/reportExtensions.json`. The missing link is entirely downstream:
`SemanticDependencyAnalyzer.AnalyzeReportMeasures` follows only the structured `References` collection
and never reads `measure.Expression`. [verified]

The existing DAX machinery could be reused if a report were truthfully bound to the model that owns the
referenced objects. `DaxReferenceExtractor.Extract` already accepts known table and function names,
identifies calls to declared UDFs, and distinguishes those calls from built-ins.
`ModelLookup.TryResolveDax` supplies the owning-table context required for an unqualified `[Name]`, and
`AddDaxDependencies` turns those results into `Dax` and `FunctionCall` edges. The observed
`byConnection` report supplies none of the model/function inventory those components require. [verified]

The Desktop bytes establish what the repository previously lacked. The rejected UDF measure is written
as `expression: "Doubled()"` with **no `references` object and no `unrecognizedReferences` marker**. The
valid control is written as `expression: "[Total Amount]"` with one
`references.measures` entry naming `Sales[Total Amount]`. `Doubled` appears nowhere else in the report.
[verified by Power BI Desktop-authored evidence]

No production implementation is now justified. The real report is `byConnection`, carries no source
model definition, and cannot be bound by PBI Assure's offline scanner. More importantly, the only UDF
expression in the evidence was rejected by Desktop; parsing its persisted text into a dependency would
promote an invalid authored expression into a confident graph edge.

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

## Previously plausible implementation path — now parked

The code could technically analyse an expression for a report already bound to a local model:

1. create the same `ModelLookup` used by model DAX analysis;
2. use the report measure's `Entity` as the DAX owning-table context;
3. pass `measure.Expression`, the report-measure graph node, and `measure.RelativePath` through the
   existing DAX/UDF extraction and resolution path;
4. retain the structured `references.measures` edges as the authoritative report-measure metadata;
5. avoid emitting a second expression-derived edge for a target already represented structurally.

That path is not a truthful product capability for the evidence observed. Desktop created report measures
only in the live-connected report; the scanner has no local model there. The valid direct model-measure
reference is already represented structurally, while the expression-only UDF call is an invalid measure
Desktop preserved after rejecting it. Synthetic local traversal would prove only that code can join two
hand-arranged inputs, not that PBI Assure supports a real PBIP state.

No new DAX parser, UDF resolver, graph traversal, usage state or confidence state is indicated.

## Architectural boundary discovered

Microsoft documents a **report-based measure in a live-connect report** calling a UDF declared in the
source model, without IntelliSense, and says a model-based measure in a composite model cannot call a UDF
declared in the source model. See [DAX user-defined functions](https://learn.microsoft.com/en-us/dax/best-practices/dax-user-defined-functions#considerations-and-limitations)
and [Connect to semantic models in Power BI](https://learn.microsoft.com/en-us/power-bi/connect-data/desktop-report-lifecycle-datasets).
[verified by Microsoft primary documentation] Desktop 2.157.879.0 did not reproduce that capability in
this controlled experiment. Do not silently promote documentation into observed Desktop behaviour.

That means a genuine Desktop fixture is expected to have `datasetReference.byConnection`, not a local
`byPath` model connection. PBI Assure deliberately treats `byConnection` as remote:
`ReportModelBinder.FindLocalModel` returns `null`, and report-measure dependency analysis stops because it
has no local model inventory against which to resolve names. [verified]

The external Desktop evidence proves serialization, not an analysable model dependency. Do not rewrite
its connection to `byPath`, bind it to an unrelated local model, or add a synthetic traversal merely
because the existing DAX extractor could process the string.

## Desktop experiment procedure and observed outcome

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
7. Desktop rejected the UDF measure, so bind the Card to the valid `Report Measure Control`; do not
   misrepresent the rejected measure as executable.
8. Save as a Power BI Project in a temporary folder outside the
   repository. Do not convert the live connection to a composite/local model.

Desktop refused `Doubled()` and the experiment correctly stopped treating that expression as a valid
consumer. No project file was hand-edited.

### C. Prove save/reopen stability

1. Close Desktop completely and snapshot the generated project outside its folder.
2. Reopen the `.pbip`.
3. Confirm the control Card still renders and both report-measure records remain present.
4. Save without intentional edits, close Desktop, and compare the before/after project files.
5. Record every changed file. Exclude `.pbi/cache.abf` and machine-specific local settings from the
   committed fixture, following the existing fixture policy.

## Files and evidence inspected

The investigation inspected these Desktop-emitted files and related source-model definitions where
available:

| File | Evidence required |
|---|---|
| `<report>.Report/definition/reportExtensions.json` | Root schema/name; entity identity; both measure names, data types and exact expressions; complete `references` objects; `unrecognizedReferences`; any previously unseen fields or collections |
| `<report>.Report/definition.pbir` | The actual `datasetReference.byConnection` shape and whether any stable source-model identity is exposed |
| `<report>.Report/definition/pages/<page>/visuals/<visual>/visual.json` | How the Card identifies the valid control `Sales[Report Measure Control]`, including its extension/schema marker and query reference |
| `<report>.Report/definition/report.json` and `version.json` | Report/PBIR schema versions needed to interpret the fixture |
| Source `definition/functions.tmdl` | The exact declared `Doubled` identity and expression used by the live-connected report |
| Source `definition/tables/Sales.tmdl` | The model measure and column chain reached by `Doubled()` |
| Root `.pbip` and `.platform` files | Normal Desktop project identity/provenance; no hand-authored additions |

The completed search found `Doubled` only once, in the rejected report measure's expression. The control
measure appears in `reportExtensions.json` and in the Card's projection, sort and visual-filter metadata;
`Total Amount` appears in the control expression and its structured `references.measures` entry. The Card
uses `SourceRef.Schema = "extension"`, `Entity = "Sales"`, and `Property = "Report Measure Control"`.

## What the evidence establishes

- the real Desktop location and JSON shape of report-level measure expressions;
- the report measure's entity/name identity and the visual's reference to the valid control;
- the UDF call is not represented anywhere beyond the rejected expression;
- the rejected UDF expression has neither `references.measures` nor an `unrecognizedReferences` marker,
  while the valid ordinary measure call has a structured `references.measures` entry;
- that rejected expression text can survive close/reopen unchanged and therefore is not proof of a valid
  dependency;
- that the valid control and Card survive close/reopen unchanged;
- the boundary between report inventory and unavailable remote-model dependency analysis.

## Fixture decision

The raw project contains no credential, token, email address or user filesystem path. Its
`definition.pbir` connection string does contain the service endpoint, one tenant identifier, and the
semantic-model identifier (the `initial catalog` and `semanticmodelid` values are the same). These are not
authentication secrets, but they are persistent organization/service metadata. `.pbi/localSettings.json`
also contains the usual machine-specific encrypted `securityBindingsSignature`.

The raw project is therefore retained as external evidence and is **not committed**. Removing only the
standard `.pbi` file would still expose the tenant/model identifiers; replacing them would make the
project a sanitised derivative rather than untouched Desktop evidence. A partial collection of the safe
JSON files would not be a valid PBIP fixture and adds no executable test value while the feature is
parked.

## Recommendation

Do not implement report-level measure → UDF traversal for the current offline scanner. Reconsider it only
if PBI Assure gains trustworthy access to the source model metadata for `byConnection` reports, or a
future Desktop-authored local `byPath` fixture proves a valid report-measure state.

General report-measure expression parsing is also not currently worth implementing. The valid dependency
observed here is already represented by `references.measures`; expression parsing adds no truthful
classification without a bound model. Continue inventorying the expression and structured references.
Visual-calculation parsing remains a separate, unread gap and was not investigated here.
