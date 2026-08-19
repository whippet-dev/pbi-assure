# Object-type coverage review

**Date:** 2026-08-19 · **Against commit:** `42f2c42` · **Status: read-only analysis. No code, tests, fixtures or existing documentation were modified.**

Third companion to `architecture-review.md` and `../design/proposed-rules.md`. This one asks a narrower question: **which Power BI object types does the analysis not currently consider, and does that absence matter?**

Findings are ranked by the product's own cardinal risk — *could this absence make an object look unused when it is actually required?* An object type that is merely descriptive matters far less than one that creates a hidden dependency.

---

## 1. What is currently modelled

`SemanticObjectTypes` recognises **seven** object types:

`Table` · `Relationship` · `Column` · `Measure` · `ReportMeasure` · `HierarchyLevel` · `CalculationItem`

The TMDL parser reads these declarations and properties: `table`, `column`, `measure`, `hierarchy`, `level`, `partition`, `relationship`, `calculationGroup`, `calculationItem`, `expression`, `sortByColumn`, `dataType`, `isHidden`, `isPrivate`, `formatString`, `formatStringDefinition`, `precedence`, `selectionExpression`, `multipleOrEmptySelectionExpression`, `crossFilteringBehavior`, `fromCardinality`/`toCardinality`, `fromColumn`/`toColumn`, `isActive`, `mode`, `source`, `sourceColumn`, `kind`, `ordinal`, `isNameInferred`, and exactly two annotations (`__PBI_LocalDateTable`, `__PBI_TemplateDateTable`).

This is a solid core. The gaps below are additions, not corrections.

---

## 2. Method

Two passes, both mechanical:

1. Every TMDL keyword appearing in the committed Desktop-authored fixtures, counted and compared against what the parser reads.
2. A direct search of `src/PbiAssure.Core` for each known TOM/TMDL object type.

Search result (`NOT parsed` = zero references anywhere in Core):

```
tablePermission   NOT parsed      perspective       NOT parsed
kpi               NOT parsed      detailRows        NOT parsed
refreshPolicy     NOT parsed      alternateOf       NOT parsed
variation         NOT parsed      objectTranslation NOT parsed
dataCategory      NOT parsed      isKey             NOT parsed
lineageTag        NOT parsed      summarizeBy       NOT parsed
queryGroup        NOT parsed
```

*(`role` matches in Core are PBIR **visual field roles**, an unrelated concept. RLS roles are confirmed absent by `tablePermission`.)*

Even the small committed fixtures contain constructs the parser never reads: **`lineageTag` (33 occurrences)**, **`summarizeBy` (28)**, **`annotation` (53, of which two kinds are read)**, and a whole `definition/cultures/en-US.tmdl` file.

---

## 3. Tier 1 — absences that can create a false "apparently unused"

These are the ones that matter. Each is a construct whose DAX or metadata **references model objects**, so an object used *only* there is currently invisible to the dependency graph and will classify as `ApparentlyUnused`.

### 3.1 Row-level security — `role` / `tablePermission` — **highest risk**

RLS role definitions carry DAX filter expressions such as `[Region] = USERNAME()`. A column referenced *only* by an RLS filter has no report reference, no measure reference and no relationship endpoint.

**Consequence:** it classifies as `ApparentlyUnused`, and the user is invited to review it for removal. Deleting it silently breaks security filtering.

This is the most severe gap in the product, because the failure mode is not a cosmetic mislabel — it is a security control that looks like dead weight. The existing `DaxReferenceExtractor` could almost certainly process these expressions unchanged; the work is in parsing the `role` / `tablePermission` blocks.

### 3.2 Incremental refresh policy — `refreshPolicy`

An incremental refresh policy references the `RangeStart` and `RangeEnd` parameters (named M expressions). Those parameters typically have no other consumer.

**Consequence:** `PBI-QUERY-002` ("named Power Query expression is not statically reachable from any loaded table query") would fire on `RangeStart`/`RangeEnd` in any model using incremental refresh. That is a **false positive in an existing shipped rule**, not merely a future gap.

*Inferred* — I have no incremental-refresh fixture to confirm the rule fires. It is the first thing I would test.

### 3.3 KPI definitions — `kpi`

A measure's KPI carries target, status and trend expressions referencing other measures. Those references are dependencies.

**Consequence:** a measure used only as a KPI target classifies as `ApparentlyUnused`.

### 3.4 Detail rows — `detailRows`

A `detailRows` definition is a DAX expression on a measure or table controlling drill-to-detail. It references columns.

**Consequence:** columns surfaced only through detail rows appear unused.

### 3.5 Aggregation mappings — `alternateOf`

Aggregation tables map their columns to detail-table columns via `alternateOf`. The mapping is a genuine structural dependency in both directions.

**Consequence:** either side can appear unused, and the relationship between them is invisible. Rarer than the above — enterprise models mostly — but high impact where present.

### 3.6 Column variations — `variation`

`variation` is how a date column binds to a date hierarchy, including the auto date/time hierarchies the product already handles carefully elsewhere.

**Note:** `PbirFieldReferenceExtractor` already normalises `PropertyVariationSource` **on the report side** (lines 105–109, 382–388), which is good. But the **model side** — the TMDL `variation` block declaring the binding — is not parsed. The two halves of the same concept are handled asymmetrically.

**Consequence:** a variation's target hierarchy or column may not be reachable through the model graph even though the report reference resolves.

---

## 4. Tier 2 — absences that affect interpretation, not correctness

None of these can cause a false unused classification. They limit what the tool can *say*, not whether what it says is right.

| Object type | What is lost |
|---|---|
| `perspective` | Perspectives express curated intent — an object in a perspective is deliberately surfaced. Useful signal for review prioritisation; not usage evidence. |
| `culture` / `objectTranslation` | Translated captions and descriptions. A `definition/cultures/*.tmdl` file exists in the fixtures and is never read. Relevant if the tool ever reports on multilingual completeness. |
| `dataCategory` | Geography, WebUrl, ImageUrl, Barcode. Would enable genuinely useful accessibility and formatting checks (e.g. an ImageUrl column with no alt-text pathway). |
| `summarizeBy` | Default aggregation. Present 28 times in fixtures. Enables "numeric column with `summarizeBy: none` is a category, not a measure" style insight. |
| `isKey` | Structural intent on a column. |
| `displayFolder` | Organisation. Would support model-tidiness review. |
| `queryGroup` | Power Query folder organisation. |
| `lineageTag` | **The most strategically interesting.** Present 33 times in fixtures. It is the stable identity that survives renames — the enabling primitive for *comparing two scans of the same model over time*, which the product cannot currently do. Not a usage concern; a roadmap one. |
| `annotation` (general) | 53 occurrences; two kinds read. Third-party tools (Tabular Editor, Bravo) store metadata here. |
| `extendedProperty`, `changedProperty` | Rarely load-bearing. |

---

## 5. Tier 3 — report-side objects not read

The scanner reads `report.json`, `pages.json`, `page.json`, `visual.json` and `reportExtensions.json`. Present in real fixtures but not read:

| File / object | Assessment |
|---|---|
| `definition/version.json` | **Ties directly to a P1 finding in the architecture review** — the PBIR format version is on disk, unread, while `ProjectInventory.SchemaVersion` is the hardcoded literal `"0.21"`. The evidence for a version-compatibility rule is already sitting in the project folder. |
| `.platform` | Artifact logical id, display name and type. Would give artifacts stable identity independent of folder naming. |
| **Mobile layouts** | PBIR supports a separate mobile layout per page with its own visual set and positions. Not present in the current fixtures, so unconfirmed — but if a visual appears only in a mobile layout, or has different tab order there, the accessibility rules would be analysing the desktop layout alone. **Needs a Desktop fixture to establish.** |
| **Slicer sync groups** | Slicers synced across pages are a report-level relationship between visuals. Whether PBIR stores this in `report.json` needs confirming; if so, a synced slicer's field is used on pages where the slicer does not appear. |
| **Linguistic schema / Q&A synonyms** | Relevant to `PBI-COMPAT-001`'s Q&A retirement theme. |
| `.pbi/localSettings.json`, `.pbi/cache.abf` | Correctly ignored — local state, not project definition. |

---

## 6. Ranked recommendation

| # | Gap | Why first | Effort |
|---|---|---|---|
| 1 | **RLS `tablePermission` DAX** | Only gap where a false `ApparentlyUnused` has a security consequence. Existing DAX extractor likely reusable. | Medium |
| 2 | **`refreshPolicy` parameters** | Plausibly a live false positive in shipped `PBI-QUERY-002`. Test before building. | Low |
| 3 | **`kpi` expressions** | Straightforward hidden dependency; contained change. | Low |
| 4 | **`version.json`** | Unlocks the version rule `architecture.md` already promises. | Low |
| 5 | **`detailRows`** | Same pattern as KPI. | Low |
| 6 | **Mobile layouts** | Unknown scope until a fixture exists. Establish before estimating. | Investigate |
| 7 | `lineageTag` | No current consumer, but the prerequisite for scan-over-scan comparison. Capture now, use later. | Low |
| 8 | `alternateOf`, `variation` (model side) | Real but rarer. | Medium |

**Suggested first step is not code.** Take one real enterprise model with RLS and incremental refresh, scan it, and check whether `RangeStart`/`RangeEnd` appear under `PBI-QUERY-002` and whether RLS-only columns appear as `ApparentlyUnused`. That measurement converts two *inferred* risks into *verified* ones and sizes the work honestly.

---

## 7. What is correctly ignored

Worth stating so nobody "fixes" these:

- `.pbi/localSettings.json`, `.pbi/cache.abf`, `.pbi/editorSettings.json` — machine-local state, not project definition.
- `outputs/` — the tool's own prior output.
- Most `annotation` values — reading two specific ones deliberately, and refusing to infer from the rest, is the correct discipline described in `usage-classification.md`.

---

## 8. Assumptions needing Power BI Desktop verification

1. Whether a PBIR page can carry a **mobile layout with a distinct visual set or tab order** — determines whether accessibility rules currently analyse a partial picture.
2. Whether **slicer sync groups** are represented in `report.json`, and in what shape.
3. Whether `RangeStart`/`RangeEnd` in an incremental-refresh model **currently trigger `PBI-QUERY-002`** — the one live false positive suspected here.
4. Whether TMDL `variation` blocks appear in models where the report-side `PropertyVariationSource` handling already works — i.e. whether the asymmetry is observable in practice.

---

## Scope statement

Read-only analysis. No rules implemented, no parsers changed, no tests or fixtures added, no existing documentation edited. All "NOT parsed" claims were established by search across `src/PbiAssure.Core`; all fixture counts by direct inspection of committed TMDL.
