# PBIX ingestion feasibility

Research date: 11 August 2026

Scope: controlled comparison of PBIX, PBIX with PBIR, legacy PBIP, and modern PBIP with PBIR and TMDL. This is a research spike only. It does not propose or implement a PBIX parser.

## Executive verdict

**PBIX support requires substantial/fragile engineering.**

PBI Assure can technically open a PBIX as a ZIP-compatible package and, when PBIR is enabled, read a well-structured report definition directly. That makes a **PBIR-enabled PBIX report-only mode** plausible in a client-side browser.

It does not make full PBIX assurance plausible under the current architecture. In both controlled PBIX files, the semantic model and applied Power Query definitions remain inside the `DataModel` stream. That stream identifies itself as an XPress9-compressed Analysis Services backup and is not a plain TMSL, TMDL, JSON, or M document. Enabling PBIR changes the report representation only.

An untouched PBIX also contains a readable legacy report definition in `Report/Layout`, but it is the undocumented PBIR-Legacy representation: one UTF-16 JSON document containing many nested JSON strings. The information is demonstrably present, but supporting it would mean maintaining a second, reverse-engineered report parser.

The practical conclusions are:

- Keep full PBI Assure analysis PBIP-first.
- Do not describe PBIR-enabled PBIX as enabling full assurance; it only makes the report layer substantially easier.
- Do not pursue arbitrary full PBIX support while PBI Assure must remain browser-only, backend-free, and independent of Power BI Desktop or Analysis Services.
- If user demand justifies one further experiment, test PBIR-enabled PBIX **report-only** ingestion in memory and label the reduced scope explicitly.
- Legacy PBIP is a separate and more tractable opportunity: its `model.bim` is documented TMSL JSON and contains the same model metadata as TMDL, although its `report.json` remains PBIR-Legacy.

## Controlled sample setup

The four samples are fresh derivatives of the same Microsoft Sales & Returns PBIX. Preview settings were changed before the relevant file was first opened and saved, so the comparison isolates the chosen storage options better than unrelated files would.

| Sample | Intended control | Files | Total bytes |
|---|---|---:|---:|
| `SampleControlPBIX` | Untouched/default PBIX | 1 | 6,530,381 |
| `SamplePBIXPBIR` | PBIX saved with enhanced report format | 1 | 6,691,504 |
| `SamplePBIPLegacy` | PBIP with PBIR-Legacy and TMSL | 36 | 12,418,157 |
| `SamplePBIPPBIRTMDL` | PBIP with PBIR and TMDL; PBI Assure reference | 316 | 13,603,076 |

This setup supports strong structural comparisons, but not universal format claims. Separate saves can change cache bytes, security bindings, serialization whitespace, and generated metadata. The sample does not exercise every Power BI feature, historical PBIX version, sensitivity-protected file, thin report, composite model, calculation group, field parameter, or unapplied Power Query change. Those gaps are called out rather than inferred away.

## Structure comparison

### PBIX control

The PBIX is an OPC/ZIP-style package with 36 entries. Its root-level streams and subtrees are:

```text
[Content_Types].xml
_rels/.rels
Connections
DataModel
DiagramLayout
docProps/custom.xml
Metadata
Report/
  CustomVisuals/                    8 files
  Layout                            one legacy report definition
  LinguisticSchema
  StaticResources/
    RegisteredResources/           15 files
    SharedResources/BaseThemes/    1 file
SecurityBindings
Settings
Version
```

Significant entry sizes:

| Entry | Bytes | Representation |
|---|---:|---|
| `Report/Layout` | 1,931,634 | UTF-16 JSON using PBIR-Legacy structures and nested JSON strings |
| `DataModel` | 1,042,011 | Binary XPress9-compressed Analysis Services backup; ZIP entry itself is stored, not deflated |
| `DiagramLayout` | 4,170 | UTF-16 JSON |
| `Connections` | 136 | UTF-8 JSON |
| `Metadata` | 234 | UTF-16 JSON |
| `Settings` | 280 | UTF-16 JSON |
| `Report/LinguisticSchema` | 4,800 | UTF-16 XML |
| `SecurityBindings` | 358 | Binary protected data |

There is no separate `DataMashup` entry in this controlled PBIX, despite the corresponding PBIP model containing 15 M partitions. `Settings` holds query editor settings, not M. `Connections` holds remote artifact identifiers in this file, not source-system connection definitions.

The legacy layout is not empty or merely rendered state. Read-only parsing found:

- 18 pages;
- 166 visuals;
- 28 bookmarks;
- one report filter;
- six resource packages;
- stable page and visual IDs matching the legacy PBIP derivative;
- visual container coordinates, sizes, configuration, filters, queries, formatting objects, tab order, actions, tooltip references, and sort references.

### PBIX + PBIR

The PBIR-enabled PBIX has 294 entries. It retains the same broad package shell, semantic model storage, resources, diagram, settings, and connection representation, but changes the report layer:

```text
Report/definition/
  version.json
  report.json
  bookmarks/
    bookmarks.json
    28 *.bookmark.json files
  pages/
    pages.json
    18 page folders
    166 visual.json files
    44 mobile.json files
```

There are 260 `Report/definition` JSON files in total. Their relative path set exactly matches the modern PBIP report's 260 definition files. Representative files normalize to the same JSON content; a few visual/bookmark files contain small save-specific value differences, so byte identity should not be expected across independently saved copies.

The important package delta is:

| Change | Observation |
|---|---|
| Added | 260 plain JSON files under `Report/definition/` |
| Removed | `Report/Layout` |
| Removed | `_rels/.rels` in this save |
| Retained | `DataModel`, `Connections`, `DiagramLayout`, `Metadata`, `Settings`, `SecurityBindings`, `Version`, custom visuals, static resources, linguistic schema |

PBIR is therefore a replacement for the legacy report definition, not an additional copy alongside it in this controlled PBIX. Microsoft likewise documents that a PBIR `definition` folder replaces PBIR-Legacy `report.json`, and explicitly states that PBIR can be embedded in PBIX files ([Power BI project report folder](https://learn.microsoft.com/en-us/power-bi/developer/projects/projects-report)).

The `DataModel` bytes differ after resave (1,016,595 bytes rather than 1,042,011), as do metadata/security/version bytes, but its signature and storage architecture do not change. `Connections`, `DiagramLayout`, `Settings`, and `Report/LinguisticSchema` are byte-identical between the two PBIX controls. There is still no separate `DataMashup`.

### Legacy PBIP

The legacy project separates the package into folders but uses the older text formats:

```text
Sales & Returns Sample v201912.pbip
Sales & Returns Sample v201912.Report/
  definition.pbir                  264 bytes; byPath model reference
  report.json                      816,420 bytes; PBIR-Legacy
  CustomVisuals/                   8 files
  StaticResources/                 16 files
  .pbi/localSettings.json
  .platform
Sales & Returns Sample v201912.SemanticModel/
  definition.pbism                 170 bytes
  model.bim                        113,920 bytes; TMSL JSON
  diagramLayout.json
  .pbi/cache.abf                   1,016,616 bytes; local cache
  .pbi/editorSettings.json
  .pbi/localSettings.json
  .platform
```

`report.json` is a readable extraction of the same PBIR-Legacy model used by `Report/Layout`. It contains the same 18 page IDs, 166 visual IDs, 28 bookmarks, one report filter, resources, theme reference, and nested visual/query/filter state as the untouched PBIX. Microsoft documents the file's purpose but says PBIR-Legacy does not support external editing; unlike PBIR, it has no public per-object schema.

`model.bim` is a readable TMSL `Database` object at compatibility level 1606. It contains:

| Object | Count |
|---|---:|
| Tables | 18 |
| Columns | 86 |
| Measures | 58 |
| Relationships | 9 |
| Hierarchies | 2 |
| Partitions | 18 |
| M partitions | 15 |
| Calculated partitions | 3 |

The counts exactly match the modern TMDL sample. Microsoft documents `model.bim` as the TMSL semantic model definition and `definition/` as its TMDL replacement ([Power BI project semantic model folder](https://learn.microsoft.com/en-us/power-bi/developer/projects/projects-dataset)). The information is not intrinsically absent from a legacy PBIP; PBI Assure simply has no `model.bim` or PBIR-Legacy parser today.

The `.pbi/cache.abf` file is a local Analysis Services cache and is normally ignored by source control. It is not needed to read the model definition from `model.bim`.

### Modern PBIP + PBIR + TMDL

The reference project uses the structures PBI Assure currently parses:

```text
Sales & Returns Sample v201912.pbip
Sales & Returns Sample v201912.Report/
  definition.pbir
  definition/                       260 JSON files
    report.json
    version.json
    bookmarks/
    pages/
  CustomVisuals/
  StaticResources/
Sales & Returns Sample v201912.SemanticModel/
  definition.pbism
  definition/                       22 TMDL files
    database.tmdl
    model.tmdl
    relationships.tmdl
    cultures/en-US.tmdl
    tables/*.tmdl                   18 table files
  diagramLayout.json
  .pbi/cache.abf
```

PBIR makes pages, visuals, bookmarks, and report metadata independently addressable and publicly schema-backed. TMDL splits the tabular database into human-readable model/object files. Microsoft describes TMDL as the human-friendly alternative to the single TMSL JSON document ([TMDL overview](https://learn.microsoft.com/en-us/analysis-services/tmdl/tmdl-overview)).

## Key format differences

| Layer | Untouched PBIX | PBIX + PBIR | Legacy PBIP | Modern PBIP |
|---|---|---|---|---|
| Container | ZIP/OPC package | ZIP/OPC package | Filesystem project | Filesystem project |
| Report | `Report/Layout`, UTF-16 PBIR-Legacy | `Report/definition/**/*.json`, PBIR | `Report/report.json`, PBIR-Legacy | `Report/definition/**/*.json`, PBIR |
| Report documentation | Undocumented internals | Public JSON schemas | Purpose documented; internals not supported for editing | Public JSON schemas |
| Semantic model | `DataModel`, XPress9/AS backup | Same `DataModel` architecture | `model.bim`, TMSL JSON | `definition/**/*.tmdl` |
| Power Query | Applied M is within opaque model storage in this sample | Unchanged | M expressions in TMSL partitions | M expressions in TMDL partitions/named expressions |
| Resources/themes | Direct package entries plus report references | Same | Direct files plus report references | Same |
| Local data cache | Inside `DataModel` | Inside `DataModel` | `.pbi/cache.abf` | `.pbi/cache.abf` |
| Browser friendliness | Report technically readable but fragile; model not practical | Report good; model not practical | Text definitions are browser-readable | Text definitions are browser-readable and current PBI Assure path |

Three distinctions matter:

1. **Existence is not accessibility.** Most semantic metadata must exist for Power BI Desktop to run the model, but inside PBIX it is packaged as an Analysis Services backup rather than a supported text contract.
2. **Accessibility is not maintainability.** The legacy layout can be decoded as JSON, but its nested schema is not the supported PBIR contract.
3. **PBIR does not imply TMDL.** PBIR-enabled PBIX improves only the report layer. It leaves `DataModel` and Power Query storage unchanged.

## Current PBI Assure reference output

PBI Assure was run without code changes against `samples-local/SamplePBIPPBIRTMDL`, writing an ignored JSON inventory outside the sample. The reference scan produced:

| Inventory | Count |
|---|---:|
| Reports | 1 |
| Pages | 18 |
| Visuals | 166 |
| Bookmarks | 28 |
| Semantic models | 1 |
| Tables | 18 |
| Columns | 86 |
| Measures | 58 |
| Relationships | 9 |
| Hierarchies | 2 |
| Partitions | 18 |
| Power Query queries/usages | 15 |
| Power Query dependencies | 8 |
| Power Query column-lineage observations | 22 |
| Data sources | 8 |
| Semantic object usage records | 152 |

Semantic usage states were 66 directly used, 17 indirectly used, 7 structurally required, 20 used only by an unused branch, and 42 apparently unused. The old Microsoft sample also generated 217 findings (2 errors, 178 warnings, and 37 informational items). Those finding counts are a property of this sample and the current rules, not an expected PBIX outcome.

The current scanner source confirms the format dependency:

- report discovery calls `PbirReportParser`, which expects `definition/report.json`, `definition/pages`, per-visual JSON, and PBIR bookmark files;
- semantic discovery calls `TmdlSemanticModelParser`, which reads `.tmdl` files, `relationships.tmdl`, and optional `expressions.tmdl`;
- dependency, semantic usage, Power Query, and cross-layer analyses operate only after those two normalized inventories exist;
- `.bim` is accepted by file enumeration but is not parsed into a semantic inventory;
- neither `Report/Layout` nor PBIR-Legacy `report.json` is parsed.

## Capability matrix

Availability codes use the brief's classifications:

- **R** — Readily available
- **D** — Available but represented differently
- **O** — Present but encoded/opaque
- **U** — Requires undocumented/reverse-engineered parsing
- **P** — Requires Power BI Desktop/runtime for the practical supported-style extraction route
- **N** — Not present
- **NA** — Not applicable
- **?** — Unknown

Compound codes distinguish existence from the route needed to use it, for example **O/U/P** means that the information is present in opaque storage, needs reverse engineering for direct access, or can be obtained by loading the model into an Analysis Services runtime. “Difficulty” is the expected new PBI Assure ingestion work for the best PBIX route. “Browser” assumes no upload, server, Desktop, Analysis Services, COM, or native Windows dependency.

### Report layer

| Capability | Current modern source | Legacy PBIP | Control PBIX | PBIR PBIX | Confidence | Difficulty | Browser feasibility |
|---|---|---|---|---|---|---|---|
| Reports | `.Report` + `definition.pbir` | R | D: package/report identity | R | High | Low | Yes |
| Pages | `pages.json`, `page.json` | D: `report.json.sections` | D/U: `Report/Layout.sections` | R | High | Low for PBIR; high for legacy | Yes; legacy fragile |
| Visuals | per-visual `visual.json` | D: `visualContainers` | D/U: `visualContainers` | R | High | Low for PBIR; high for legacy | Yes; legacy fragile |
| Visual types | `visual.visualType` | D: nested `config` | D/U: nested `config` | R | High | Low/High | Yes |
| Positions and sizes | `position` | D: container `x/y/z/width/height` | D/U: same | R | High | Low/High | Yes |
| Titles | visual `objects`/text | D: nested `config.objects` | D/U: same | R | High | Low/High | Yes |
| Visual field references | visual query/projections/objects | D: nested `query`, `config`, `filters` | D/U: same | R | High | Low/High | Yes |
| Measure/column usage | extracted PBIR expressions | D: nested expressions | D/U: nested expressions | R | High | Low/High | Yes |
| Visual filters | visual `filterConfig` | D: container `filters`/config | D/U: same | R | High | Low/High | Yes |
| Page filters | `page.json.filterConfig` | D: section filters | D/U: same | R | High | Low/High | Yes |
| Report filters | `definition/report.json` | D: top-level filters | D/U: same | R | High | Low/High | Yes |
| Bookmarks | `bookmarks/*.json` | D: nested report config | D/U: same | R | High | Low/High | Yes |
| Bookmark visual state | bookmark `explorationState` | D: nested bookmark state | D/U: same | R | High | Low/High | Yes |
| Buttons/actions | visual objects/action | D: nested visual config | D/U: same | R | High | Low/High | Yes |
| Page navigation | action destination + pages | D | D/U | R | High | Low/High | Yes |
| Bookmark navigation | action bookmark + bookmark state | D | D/U | R | High | Low/High | Yes |
| Drillthrough | page binding/filters | D | D/U | R | Medium | Low/High | Yes |
| Tooltip pages | page type + visual tooltip config | D | D/U | R | High | Low/High | Yes |
| Tooltip-only usage | visual tooltip/filter/query expressions | D | D/U | R | High | Low/High | Yes |
| Tab order | visual position/order metadata | D: nested config | D/U: `tabOrder` present | R | High | Low/High | Yes |
| Alt text | visual accessibility/objects | D when authored | D/U when authored; absent in sample | R | Medium | Low/High | Yes |
| Formatting properties | visual/page/report objects | D: nested objects | D/U: nested objects | R | High | Low/High | Yes |
| Themes | report theme collection + resources | D: theme string/resources | R/D: layout reference + resource entry | R | High | Low | Yes |
| Conditional-format references | visual objects/expressions | D: nested expressions | D/U: nested expressions | R | Medium-high | Low/High | Yes |
| Sort references | visual query/order expressions | D: nested query/config | D/U: nested query/config | R | High | Low/High | Yes |

### Semantic model

| Capability | Current modern source | Legacy PBIP | Control PBIX | PBIR PBIX | Confidence | Difficulty | Browser feasibility |
|---|---|---|---|---|---|---|---|
| Tables | `definition/tables/*.tmdl` | R: `model.bim.model.tables` | O/U/P: `DataModel` | O/U/P: `DataModel` | High | Very high | Not practical without reverse engineering |
| Columns | table TMDL | R: TMSL columns | O/U/P | O/U/P | High | Very high | Same |
| Measures | table TMDL | R: TMSL measures | O/U/P | O/U/P | High | Very high | Same |
| DAX expressions | measures/calculated objects | R: TMSL expressions | O/U/P | O/U/P | High | Very high | Same |
| Descriptions | object properties | R | O/U/P | O/U/P | High | Very high | Same |
| Hidden state | `isHidden` | R | O/U/P | O/U/P | High | Very high | Same |
| Data types | column properties | R | O/U/P | O/U/P | High | Very high | Same |
| Format strings | measure/column properties | R | O/U/P | O/U/P | High | Very high | Same |
| Sort-by-column | column `sortByColumn` | R | O/U/P | O/U/P | High | Very high | Same |
| Relationships | `relationships.tmdl` | R: TMSL relationships | O/U/P | O/U/P | High | Very high | Same |
| Active/inactive state | relationship `isActive` | R | O/U/P | O/U/P | High | Very high | Same |
| Cardinality | relationship endpoints | R | O/U/P | O/U/P | High | Very high | Same |
| Cross-filter direction | relationship behavior | R | O/U/P | O/U/P | High | Very high | Same |
| Hierarchies | table hierarchy/levels | R | O/U/P | O/U/P | High | Very high | Same |
| Field parameters | calculated table expression + metadata | R when present | O/U/P | O/U/P | Medium | Very high | Same |
| Calculation groups/items | table calculation group/items | R when present | O/U/P | O/U/P | Medium-high | Very high | Same |
| Partitions | table partitions | R | O/U/P | O/U/P | High | Very high | Same |
| Generated/system objects | annotations/naming/structure | R | O/U/P | O/U/P | High | Very high | Same |

The legacy PBIP column is “readily available” in storage terms, not current PBI Assure implementation terms. A TMSL JSON parser/adapter would still be required, but it would consume a documented text model rather than decode PBIX internals.

### Power Query

| Capability | Current modern source | Legacy PBIP | Control PBIX | PBIR PBIX | Confidence | Difficulty | Browser feasibility |
|---|---|---|---|---|---|---|---|
| Queries | M partitions/named expressions | R: TMSL partitions/expressions | O/U/P: `DataModel` | O/U/P: `DataModel` | High for applied queries | Very high | Not practical without reverse engineering |
| Load state | partition vs named expression context | D: same concepts in TMSL | O/U/P | O/U/P | Medium | Very high | Same |
| Full M expressions | partition/expression text | R: `source.expression` | O/U/P | O/U/P | High | Very high | Same |
| Query dependencies | statically derived from M | D: derive from TMSL M | O/U/P | O/U/P | High | Very high | Same |
| Sources/connectors | statically derived from M | D | O/U/P | O/U/P | High | Very high | Same |
| Joins/merges | statically derived from M | D | O/U/P | O/U/P | High | Very high | Same |
| Expands | statically derived from M | D | O/U/P | O/U/P | High | Very high | Same |
| Renamed/removed columns | statically derived from M | D | O/U/P | O/U/P | High | Very high | Same |
| Model/query association | partition table ownership | R/D | O/U/P | O/U/P | High | Very high | Same |
| Source-lineage evidence | derived from M and partitions | D | O/U/P | O/U/P | High | Very high | Same |

No `DataMashup` entry was found in either controlled PBIX. That matters because a design based only on older PBIX descriptions that expect a separate mashup stream would fail on this sample. The applied M demonstrably survives in the PBIP derivatives, but directly locating it inside `DataModel` would require decoding/restoring the Analysis Services backup.

### Cross-layer analysis

| Capability | Current modern source | Legacy PBIP | Control PBIX | PBIR PBIX | Confidence | Difficulty | Browser feasibility |
|---|---|---|---|---|---|---|---|
| Visual to semantic mapping | PBIR refs + TMDL inventory | D: legacy refs + TMSL | O/U/P overall | O/U/P model side | High | Very high for PBIX | Not practical for full mapping |
| Measure dependencies | DAX refs + model inventory | D: derive from TMSL | O/U/P | O/U/P | High | Very high | Same |
| Structural dependencies | relationships/sort/hierarchy | D: TMSL | O/U/P | O/U/P | High | Very high | Same |
| Report locations | PBIR pages/visuals/bookmarks | D/U: legacy report | D/U: legacy layout | R | High | Low PBIR; high legacy | Yes |
| Semantic usage classification | normalized report + model graphs | D: two alternate text parsers | O/U/P | O/U/P model side | High | Very high | Not practical in full |
| Apparently-unused detection | complete model + dependency roots | D | O/U/P | O/U/P | High | Very high | Same |
| Unused-branch detection | DAX graph + direct roots | D | O/U/P | O/U/P | High | Very high | Same |
| Power Query to model lineage | M partitions + table inventory | D: TMSL | O/U/P | O/U/P | High | Very high | Same |

PBIR-enabled PBIX can provide a friendly list of report references and locations without the model. It cannot reliably say whether an object exists, resolve every reference, build DAX dependencies, or classify unused model objects.

## Semantic model findings

The PBIX package does not expose `model.bim`, TMDL, or another plain semantic schema. Its `DataModel` stream begins with the UTF-16 text “This backup was created using XPress9 compression”. The stream is approximately 1 MB in this sample because the outer ZIP does not compress it again; that size should not be confused with plain metadata size or the eventual in-memory database size.

The corresponding project forms demonstrate what is logically inside the model:

- legacy PBIP serializes it as a 113,920-byte TMSL `model.bim`;
- modern PBIP serializes it as 22 TMDL files totalling 66,903 bytes;
- both expose the same 18 tables, 86 columns, 58 measures, 9 relationships, 2 hierarchies, and 18 partitions;
- DAX, descriptions, hidden flags, data types, formats, sort-by relationships, relationship properties, and M partition expressions are plain text in the PBIP forms.

There are two realistic PBIX extraction classes:

1. **Load the backup through Microsoft runtime components.** Start or connect to a local Analysis Services/Power BI Desktop model, restore/load the stream, and serialize metadata through TOM. This is practical for Windows desktop tooling but violates PBI Assure Web's constraints.
2. **Independently decode the stream and Analysis Services backup internals.** This avoids Desktop but relies on reverse-engineered XPress9 framing, ABF internals, and model metadata storage. It is a substantial second ingestion platform with version-compatibility and security risk.

The package's `DiagramLayout`, `Metadata`, and `Connections` streams are not substitutes. They offer diagram state, file metadata, and connection/report identifiers, not a complete tabular object model.

For these reasons, PBIX semantic capabilities are classified as present but encoded/opaque. “Not present” would be wrong; “readily available” would be equally wrong.

## Power Query findings

The legacy and modern PBIP controls expose 15 applied M partitions and three calculated partitions. PBI Assure uses the M expressions to derive query dependencies, connectors, merge/expand/rename/remove operations, model association, and column-lineage evidence.

Neither controlled PBIX contains a standalone `DataMashup` stream. `Settings` contains only query settings and `Connections` does not contain the source M. The applied M therefore travels with the tabular model backup in these files. This is consistent with the project conversion: the same expressions reappear as TMSL/TMDL partition sources when Desktop saves the PBIP.

Direct browser extraction would consequently require the same `DataModel` work as semantic model extraction. Even after decompression, PBI Assure would need a stable way to locate and interpret partition metadata. A dedicated mashup parser alone would not solve this controlled case.

Unapplied Power Query changes are a separate edge case. Modern PBIP can store them in `.pbi/unappliedChanges.json`; Microsoft warns that applied and unapplied definitions have different overwrite behavior. This sample has no such file. A general ingestion design would need an explicit policy about whether it reports applied model M, pending M, or both.

## Report-layer findings

A useful report-only PBIX mode is technically possible, but its quality depends on format:

### PBIR-enabled PBIX

This is the strongest candidate. The package contains the same publicly schema-backed hierarchy PBI Assure already understands: report metadata, pages, visuals, bookmarks, mobile layouts, resources, filters, actions, formatting, queries, and accessibility properties. A ZIP-entry-backed `IProjectFileSource` could theoretically present those entries to the existing PBIR parser without writing them to disk.

Likely report-only capabilities include:

- page and visual inventory;
- visual types, positions, sizes, titles, and field references;
- report/page/visual filters;
- bookmarks, bookmark state, buttons, and navigation;
- drillthrough and tooltip configuration;
- tab order, alt text, and visual-title findings;
- themes/resources and report-level formatting references;
- report-only assurance findings that do not need semantic resolution.

Semantic usage must be reduced or omitted. Field references can be listed, but unresolved references are not proof of a broken model when the model was deliberately not decoded.

### Untouched/legacy PBIX

`Report/Layout` is readable and contains extensive equivalent information. In the controlled comparison it exactly preserves the page and visual identities found in legacy PBIP. A browser could decode the ZIP entry and UTF-16 JSON without Desktop.

The problem is maintainability, not basic byte access:

- the representation is PBIR-Legacy rather than the public PBIR schema;
- visual `config`, `query`, and `filters` are themselves serialized JSON strings;
- feature-specific shapes vary and are not covered by public PBIR schemas;
- current PBI Assure PBIR parsing logic cannot simply be pointed at the document;
- historic PBIX versions expand the compatibility surface.

Supporting this properly would be a second report parser plus a large equivalence test suite. A superficially successful 18-page sample parse would not establish robust product support.

## Browser/WebAssembly feasibility

| Route | Client-side | No Desktop/AS | Managed .NET WASM | Memory profile | Verdict |
|---|---|---|---|---|---|
| Open PBIX package and list entries | Yes | Yes | Likely via `System.IO.Compression`/browser file stream | Whole-file/random-access handling must be bounded | Feasible |
| Read embedded PBIR JSON | Yes | Yes | Yes | Can skip `DataModel`; sample is small | Feasible |
| Read legacy `Report/Layout` | Yes | Yes | Yes | UTF-16 expansion and nested JSON add cost | Technically feasible, contract fragile |
| Read static resources/themes | Yes | Yes | Yes | Large images/custom visuals should be skipped unless needed | Feasible |
| Restore `DataModel` with TOM | No | No | No | Requires local server/runtime | Incompatible |
| Connect to running Desktop model | No for ordinary browser | No | No | Depends on localhost server, ports, auth/process discovery | Incompatible |
| Decode XPress9 + ABF + model metadata independently | Theoretically | Yes | Only with new/reused WASM/native-compatible codecs and parsers | Decompression and model copies can greatly exceed file size | Possible research, not a sensible product dependency today |
| Upload to a backend extractor | No longer local-only | Backend could host runtime/parser | N/A | Server controls limits | Violates current privacy/architecture goal |

The outer ZIP is not the hard part. A report-only implementation can deliberately avoid reading the `DataModel` payload. Full analysis cannot.

PBI Assure Web currently accepts at most 100 MiB of selected metadata and 25 MiB per file. Normal PBIX files can be much larger because they contain model data. A PBIX feature would need a different selection and streaming policy even for report-only analysis: inspect the central directory, apply total/package-entry limits, reject encrypted or malformed archives, and read only allowed report entries. Loading a large PBIX and its decompressed `DataModel` into WebAssembly memory would create multiple copies and unpredictable failure behavior.

Any route requiring Power BI Desktop, a TOM connection, local Analysis Services, Windows registry/process discovery, COM, native Windows DLLs, or localhost privileged services is incompatible with the current browser product.

## Third-party extraction approaches

Third-party extraction proves that information can be recovered; it does not establish a supported or browser-compatible route.

### pbi-tools

The current pbi-tools CLI documents offline PBIX extraction modes, optional extraction from a running Power BI Desktop port, TMSL/TMDL serialization, and mashup serialization ([CLI usage](https://pbi.tools/cli/usage.html)). Its repository also:

- describes Power BI Desktop x64 as a development prerequisite;
- references `Microsoft.AnalysisServices` and `Microsoft.AnalysisServices.AdomdClient` packages ([dependency manifest](https://github.com/pbi-tools/pbi-tools/blob/main/paket.dependencies));
- includes a `DataModelConverter` path that starts a local `msmdsrv`, calls `LoadPbixModel`, connects with TOM, and serializes the database ([DataModelConverter.cs](https://github.com/pbi-tools/pbi-tools/blob/main/src/PBI-Tools/PowerBI/DataModelConverter.cs));
- includes custom PBIX package and mashup converters;
- is AGPL-licensed.

That is a capable desktop/CLI architecture, but it relies on exactly the kind of runtime and platform integration PBI Assure Web excludes. Its newer extraction paths should be evaluated on their own terms if ever considered, but the existence of a CLI command is not evidence that the same operation can run safely in Blazor WebAssembly.

### TOM/DAX Studio/Tabular Editor-style access

Tools that connect to Power BI Desktop's local Analysis Services instance can query rich model metadata through supported object models. They require the PBIX to be open, a local port/server, and Windows desktop process access. They are useful comparators for a native desktop mode, not for a self-contained browser.

### Independent binary/WASM implementations

Independent projects now claim XPress9/ABF/VertiPaq parsing, including browser/WASM demonstrations. This changes “impossible” to “theoretically implementable,” but not to “documented or low risk.” Such implementations necessarily track reverse-engineered binary details, often use native/WASM codecs, and create a continuing compatibility and security burden for untrusted files. PBI Assure should not adopt that architecture merely to remove the PBIP save step.

No third-party dependency was added or copied during this spike.

## Thin-report limitation

A thin/live-connected PBIX has a report definition and a connection to an external semantic model but does not contain the external model's complete metadata or Power Query definitions.

Report-only analysis can still inspect, depending on report format:

- pages, visuals, positions, titles, filters, and field references;
- bookmarks, buttons, navigation, drillthrough, and tooltips;
- tab order, alt text, and other report accessibility properties;
- report-level resources, themes, and formatting;
- report-level measures if represented in PBIR `reportExtensions.json` or equivalent report metadata.

It inherently cannot derive offline:

- the complete table/column/measure inventory of the remote model;
- DAX measure dependencies held in that model;
- relationships, hierarchies, calculation groups, partitions, or generated objects;
- Power Query M, sources, dependencies, or lineage from the remote model;
- semantic usage classifications that require knowing all model objects;
- apparently-unused or unused-branch detection across the external model.

Visual field references can be reported as references, not verified against the remote model. Connecting to the service or Desktop to fill that gap would change the product's authentication, privacy, deployment, and browser architecture.

## Product questions

### A. Could PBI Assure realistically accept an untouched PBIX directly?

It could accept and inspect the package, and it could technically parse the legacy report layer. It could not provide current full assurance without substantial binary model extraction. Calling that general PBIX support would overstate the result.

### B. What level of assurance could it provide?

- **Full assurance:** No practical, robust, browser-only route today.
- **Report-only assurance:** Yes for PBIR-enabled PBIX with moderate ingestion work; technically yes but fragile for untouched PBIX/PBIR-Legacy.
- **Partial semantic assurance:** Only coarse package/connection/diagram observations without decoding `DataModel`; too incomplete to justify a semantic-assurance label.

### C. Would PBIR-enabled PBIX materially reduce the work?

Yes for report analysis. It exposes exactly the kind of per-object JSON PBI Assure already consumes. It does not reduce semantic model or Power Query extraction work.

### D. Would supporting PBIX introduce a second substantial ingestion architecture?

Yes for arbitrary/full PBIX. It would add archive security, legacy report parsing, XPress9/ABF handling or runtime orchestration, model serialization, format-version compatibility, and much larger memory limits. PBIR report-only support would be a smaller archive adapter, not a full second analysis engine.

### E. Could that architecture remain fully client-side/browser-only?

PBIR report-only could. Full PBIX would require reverse-engineered binary/WASM components and aggressive resource controls. A Desktop/TOM or server extraction route would not remain browser-only.

### F. Does “select your normal PBIX” justify the complexity?

The UX benefit is real, especially for non-project users, but it does not currently justify promising full assurance. A report-only mode also risks user confusion: the simplest input would produce the least complete output. The benefit may justify a narrowly labelled PBIR report-only experiment after demand is proven, not a general parser now.

### G. What status should PBIX support have?

**Ignore full arbitrary PBIX support for now.** Preserve this research and, if a future product decision needs evidence, research only PBIR-enabled PBIX report-only support. Do not consider PBIR-enabled PBIX a route to full support.

## Product options

| Option | Benefit | Engineering cost | Fragility | Browser viability | Maintenance burden |
|---|---|---|---|---|---|
| 1. PBIP only | Full current assurance; one transparent source format; clear privacy story | Low/current | Low relative to alternatives; PBIP/PBIR/TMDL are still evolving previews | Strong and already proven | Low-medium |
| 2. PBIP full + arbitrary PBIX report-only | Lets most users select an existing PBIX; useful accessibility/navigation inventory | Medium-high: package adapter plus PBIR and PBIR-Legacy routing/parity | High for legacy layout; reduced analysis can confuse users | Technically viable if `DataModel` is skipped | Medium-high |
| 3. PBIP full + PBIR-enabled PBIX report-only | Convenient input with public report schemas and high parser reuse | Moderate: secure ZIP source, format detection, reduced-mode UX, tests | Medium; PBIR still preview/evolving but documented | Good with size/entry limits | Medium |
| 4. Full arbitrary PBIX | Maximum input convenience and feature parity aspiration | Very high: report compatibility plus model/PQ binary extraction | Very high; undocumented binary internals and historical variants | Poor without major WASM/reverse-engineering investment | Very high and continuous |

Legacy PBIP support is not one of the PBIX options, but the evidence suggests it is more tractable than full PBIX: add adapters for documented TMSL `model.bim` and unsupported-but-readable PBIR-Legacy `report.json`, with no archive or XPress9 work. It should be evaluated separately if real users retain legacy projects.

## Recommendation

PBI Assure should remain **PBIP full assurance only** today, with modern PBIR/TMDL as the preferred input.

Do not implement or advertise general PBIX support. In particular, do not infer that PBIR inside PBIX unlocks the semantic model; the controlled comparison disproves that assumption.

If reducing the save-as-PBIP hurdle becomes a measured adoption priority, consider a future **PBIR-enabled PBIX — report assurance only** mode with all of the following boundaries:

- detect embedded `Report/definition` and reject or separately classify legacy-only PBIX;
- never load/decompress `DataModel`;
- explicitly state that semantic inventory, DAX dependencies, unused-object detection, and Power Query lineage were not assessed;
- suppress false unresolved-model findings;
- apply strict archive path, entry count, compression ratio, and size limits;
- preserve local-only processing and no-network behavior;
- compare report results against the equivalent PBIP on controlled fixtures.

This is a possible future convenience feature, not the next core architecture. Full PBIX support should be reconsidered only if Microsoft publishes a stable browser-compatible extraction contract or product requirements explicitly permit a trusted backend/native desktop runtime.

## Smallest useful follow-up experiment

Build one disposable, non-production parity harness for **PBIR-enabled PBIX report-only ingestion**:

1. Open `SamplePBIXPBIR` with managed ZIP APIs.
2. Enumerate only `Report/definition/**`, `Report/StaticResources/**`, and the minimal report metadata required by the existing virtual file source.
3. Map those entries in memory to a synthetic `.Report` directory and deliberately ignore `DataModel`.
4. Run the existing PBIR report parser, not the semantic/PQ pipeline.
5. Compare against the modern PBIP reference: 18 pages, 166 visuals, 28 bookmarks, filters, actions, navigation, tooltips, accessibility properties, and field-reference locations.
6. Run the harness once in Blazor WebAssembly and record peak memory and elapsed time for the 6.7 MB sample.

Success would establish that a narrowly labelled PBIR report-only mode is mechanically viable and largely reusable. It would not authorize production PBIX support, legacy layout parsing, or `DataModel` reverse engineering.

## Sources and evidence boundary

Primary external references used:

- [Microsoft: Power BI Desktop project report folder and PBIR](https://learn.microsoft.com/en-us/power-bi/developer/projects/projects-report)
- [Microsoft: Power BI Desktop project semantic model folder](https://learn.microsoft.com/en-us/power-bi/developer/projects/projects-dataset)
- [Microsoft: Tabular Model Definition Language](https://learn.microsoft.com/en-us/analysis-services/tmdl/tmdl-overview)
- [pbi-tools: CLI extraction modes](https://pbi.tools/cli/usage.html)
- [pbi-tools: repository and prerequisites](https://github.com/pbi-tools/pbi-tools)
- [pbi-tools: DataModel converter implementation](https://github.com/pbi-tools/pbi-tools/blob/main/src/PBI-Tools/PowerBI/DataModelConverter.cs)
- [pbi-tools: dependency manifest](https://github.com/pbi-tools/pbi-tools/blob/main/paket.dependencies)

Local evidence comes from read-only ZIP enumeration of both PBIX files, JSON parsing and structure comparison across all four controlled samples, current scanner source inspection, and a normal PBI Assure scan of the modern PBIP reference. No PBIX or sample file was extracted to disk, modified, rewritten, or saved by this spike.
