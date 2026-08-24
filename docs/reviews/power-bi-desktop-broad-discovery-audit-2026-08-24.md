# Broad Power BI Desktop semantic discovery audit — 2026-08-24

## Executive summary

This documentation-only audit broadened the focused Desktop semantic audit to the installed
Power BI Desktop **2.157.879.0 (26.08)** resources and cross-checked every useful signal against
the current PBI Assure parser and dependency graph. No Desktop binary or resource has been copied
into this repository.

The clearest new candidate is **table-owned Detail Rows**: Desktop's model metadata exposes a
table default Detail Rows definition as a first-class object. PBI Assure intentionally supports
only the separately proven measure-owned form today. That is not enough evidence to implement
table support: the exact Desktop-authored TMDL shape, save/reopen stability and referenced-object
behaviour still need a small fixture.

Desktop also exposes first-class dynamic format-string definitions and visual calculations. Both
could contain semantic expressions that PBI Assure does not currently analyse in every possible
location, but this audit did not establish a persisted, Desktop-authored source-to-target dependency
that would justify a product rule. No current production assumption is overturned. The strongest
corrective result is that it would be unsafe to generalise the existing measure-only Detail Rows
support into an assumption that Detail Rows is measure-only.

**Outcome:** no implementation is justified by this audit alone. The next evidence task should be a
minimal Desktop round-trip fixture for table-owned Detail Rows; dynamic measure format strings are
the next best companion fixture if they can be authored independently.

## Scope, method and evidence grades

The baseline was the targeted Desktop semantic audit recorded on the diagnostic branch as
`power-bi-desktop-semantic-audit-2026-08-24.md`. It established the method used here, including
the bounded Auto Date/Time and effective-visibility conclusions now implemented on `master`.
That baseline document is not currently present in this branch's working tree.

Two directions were used:

1. Start from PBI Assure assumptions and source boundaries: PBIR page/visual parsing, bookmarks,
   relationship provenance, TMDL declarations and semantic dependency roots.
2. Search the installed Desktop WebView/Minerva schemas and JavaScript resources for those areas,
   then trace concise schema descriptions, validation paths and metadata-view registrations far
   enough to identify their practical boundary.

Only names, short property identifiers and behavioural summaries are recorded below. Proprietary
source, resources and binaries are neither reproduced nor vendored. A missing text hit is treated as
**UNKNOWN**, not evidence that native Desktop or Analysis Services does not implement a behaviour.

| Grade | Meaning in this review |
|---|---|
| **PROVEN-RUNTIME** | An installed Desktop runtime/schema path directly establishes the behaviour. |
| **PROVEN-EXPERIMENT** | A controlled Desktop action followed by save/close/reopen establishes it. |
| **STRONGLY-SUPPORTED** | Independent resource/schema and product-code evidence establish existence, but not every effective-runtime consequence. |
| **INFERENCE** | A plausible impact follows, but needs a fixture or runtime experiment. |
| **UNKNOWN** | The available evidence does not safely establish the claim. |

## What was inspected

| Area | Evidence inspected | Result |
|---|---|---|
| Desktop build and resource surface | Installed executable version and the Minerva WebView schema/JavaScript resource set | Desktop 2.157.879.0 (26.08); resources expose report-schema validation, report interaction, bookmark, model metadata and visual-calculation surfaces. **PROVEN-RUNTIME** |
| PBIR page/navigation | Page schema definitions for `visibility`, `pageBinding`, visual interactions, bookmark and mobile-related terms | The schema confirms these are persisted report concepts; it does not make every one a semantic dependency source. **STRONGLY-SUPPORTED** |
| TMDL/model metadata | Model metadata-view registrations for measures, tables, relationships, Detail Rows and format-string definitions | Detail Rows, format-string definitions and relationship date behaviour are first-class model concepts. **PROVEN-RUNTIME** |
| Current PBI Assure behaviour | `TmdlSemanticModelParser`, `SemanticDependencyAnalyzer`, PBIR parser, interaction rule and current evidence documentation | Existing measure KPI/Detail Rows, calculation-item format strings, page visibility and visual interactions are already bounded as described below. **PROVEN-RUNTIME** |
| Opaque/native areas | Targeted searches for composite-model connection/binding behaviour and state replay | No sufficient exposed decision path was found. This does not demonstrate absence of native behaviour. **UNKNOWN** |

## New findings, ranked by likely user impact

### 1. Table-owned Detail Rows is a real model concept, but its persisted dependency shape is unverified

Desktop's model metadata registration exposes both a Detail Rows definition object and a table field
for a default Detail Rows definition. The same metadata surface associates measures with their own
Detail Rows definition. This establishes that a table-level form exists in the engine model, rather
than Detail Rows being intrinsically measure-only. **PROVEN-RUNTIME**

PBI Assure currently parses a measure's `detailRowsDefinition` and sends it through normal DAX
dependency reachability. It deliberately does not parse a table-owned form; the current state record
explicitly leaves table-owned Detail Rows evidence-gated. **PROVEN-RUNTIME**

The resource evidence does **not** establish the exact TMDL spelling/location, whether Desktop
currently authors it, whether it survives close/reopen, or which DAX references should become graph
edges. Those remain **UNKNOWN**.

| Consequence if a real table expression has unique references | Evidence needed to settle it | Next action |
|---|---|---|
| A column or measure referenced only by table Detail Rows could be shown as `ApparentlyUnused` rather than `IndirectlyUsed` (false negative in semantic usage). **INFERENCE** | Small Desktop project: table Detail Rows expression referencing one sacrificial object; save, close/reopen, inspect TMDL and scan alongside an unused control. **PROVEN-EXPERIMENT required** | **Create fixture first** |

### 2. Dynamic format-string definitions are first-class expressions; measure-owned dependency coverage is not established

Desktop exposes format-string definitions as model objects with an expression and owning object/type
metadata. It also exposes a dedicated metadata view for them. **PROVEN-RUNTIME**

PBI Assure already reads `formatStringDefinition` for calculation items and analyses that expression.
Its measure construction reads the ordinary measure expression, static `formatString`, KPI and
measure Detail Rows fields, but no separate measure-owned dynamic format-string expression.
**PROVEN-RUNTIME**

This does not prove that the current Desktop TMDL serializer emits a measure-owned
`formatStringDefinition`, nor that such an expression can reference a model object not already
referenced by the measure expression. It would be premature to add parser support from the resource
name alone. **UNKNOWN**

| Consequence if independently referenced objects are permitted | Evidence needed to settle it | Next action |
|---|---|---|
| An object used only by a dynamic measure format expression could be misclassified as `ApparentlyUnused`. **INFERENCE** | One Desktop-authored dynamic-format measure with one sacrificial referenced object, ordinary report use for its owner, an unused control, and save/close/reopen TMDL evidence. **PROVEN-EXPERIMENT required** | **Create fixture first** |

### 3. Visual calculations are Analysis Services-evaluated and sensitive to the visual field configuration

The Desktop resources contain visual-calculation validation and user-facing diagnostics that attribute
syntax/semantic errors to Analysis Services and warn that changing visual fields can break a visual
calculation. This strongly supports that visual calculations are runtime computations coupled to a
visual's configured data rather than ordinary model measures. **STRONGLY-SUPPORTED**

PBI Assure currently records the regular PBIR field references that make a visual direct usage, but
does not parse visual-calculation semantics as a separate model dependency source. **PROVEN-RUNTIME**
The resource evidence does not show whether a visual calculation can introduce a unique, qualified
model-object reference absent from its fields. **UNKNOWN**

| Potential consequence | Evidence needed to settle it | Next action |
|---|---|---|
| A unique hidden dependency would be a semantic-usage false negative; treating all visual-calculation text as DAX would instead risk false positives. **INFERENCE** | A controlled visual-calculation project with deliberately different field-well and calculation references, inspected after save/reopen. **PROVEN-EXPERIMENT required** | **Investigate further** |

### 4. Bookmark `suppressData` participates in saved-state capture, but does not make bookmark state a safe graph root

Desktop's exploration-state capture path explicitly checks the bookmark `suppressData` option before
including data state. This gives the option a real capture-role meaning rather than treating it as
decorative metadata. **PROVEN-RUNTIME**

PBI Assure already inventories bookmark options and has fixture evidence that stale or inert saved
state can persist. The new resource evidence strengthens the existing conservative decision not to
turn every saved-state reference into a live semantic dependency. It does not reveal whether an
individual bookmark is reachable, selected by an action, or effective under its target-visual scope.
**STRONGLY-SUPPORTED** for the capture boundary; **UNKNOWN** for effective dependency reachability.

| Competing error | Evidence needed to settle it | Next action |
|---|---|---|
| Treating all bookmark state as live risks false-positive usage; ignoring a proven active data-state bookmark could miss a real dependency. **INFERENCE** | A fixture with an action-reachable bookmark, `suppressData` on/off controls and a field referenced only by the captured state. **PROVEN-EXPERIMENT required** | **Park** unless a user case requires it |

### 5. Relationship date behaviour and variation sources are exposed metadata, not safe Auto Date/Time classifiers

Desktop's model metadata includes a relationship date-join behaviour field, and its report-model
handling includes property-variation source processing. This confirms that date/variation semantics
have model/runtime representation beyond a table name. **STRONGLY-SUPPORTED**

It does not prove that either property identifies generated Auto Date/Time structure, that it has a
stable TMDL form in the installed build, or that it should change dependency roots. PBI Assure's
existing provenance rule therefore remains correctly narrow: retain the relationship and use an exact
`__PBI_LocalDateTable = true` target annotation for the system-generated path. **PROVEN-RUNTIME** for
the existing parser boundary; **UNKNOWN** for a broader classifier.

| Consequence | Evidence needed to settle it | Next action |
|---|---|---|
| Expanding provenance from these properties now could mislabel genuine user relationships or generated structures (false positives and false negatives). **INFERENCE** | Paired Desktop models covering generated and user date relationships, with Auto Date/Time enabled/disabled and variation controls, then save/close/reopen comparison. **PROVEN-EXPERIMENT required** | **Investigate further** |

## Existing assumptions strengthened or corrected

| Assumption / current boundary | Audit result | Grade | Product consequence |
|---|---|---|---|
| A PBIR page `visibility` value is meaningful and should be retained | The installed page schema describes the default visible state and `HiddenInViewMode`; PBI Assure already parses and renders this property. | **STRONGLY-SUPPORTED** | Strengthens existing coverage; no new gap or rule. |
| Visual interactions are configured report relationships, not implicit semantic-model roots | The schema describes selection flowing from a source visual to a target as filtering/highlighting. PBI Assure already inventories interactions and checks endpoints. | **STRONGLY-SUPPORTED** | Strengthens the present interpretation; no dependency edge should be invented. |
| Bookmark data state needs effective-state evidence before contributing usage | `suppressData` is part of capture, while persisted state can still be stale/inert. | **STRONGLY-SUPPORTED** | Retain the conservative no-root boundary. |
| Auto Date/Time provenance can be broadened from incidental date metadata | The extra metadata exists, but no source establishes it as a generated-object discriminator. | **UNKNOWN** | Preserve the exact annotation-based rule; do not classify by name, visibility, variation or join behaviour. |
| Detail Rows is necessarily measure-only | Desktop exposes a table default Detail Rows concept. | **PROVEN-RUNTIME** | This corrects any future extrapolation of the current bounded measure-only implementation; it does not contradict the current documented evidence gate. |

## Other candidate behaviours discovered, but not promoted to product rules

| Candidate | What the resources establish | Evidence grade | Safe interpretation / action |
|---|---|---|---|
| Synchronized slicers | Desktop has persisted synchronization and cross-page visibility UI/runtime support. | **STRONGLY-SUPPORTED** | A slicer's regular field use is already direct usage. Whether sync state needs a separate accessibility or navigation interpretation needs a fixture; **park**. |
| `pageBinding` (tooltip, drillthrough and related page roles) | The page schema describes it as metadata for tooltip, drillthrough and similar bindings. | **STRONGLY-SUPPORTED** | PBI Assure already retains page bindings and reconciles report tooltips. Do not infer missing drillthrough field roots without a persisted example; **investigate further**. |
| Object translations and linguistic metadata | The model object registry includes translation and linguistic metadata concepts. | **PROVEN-RUNTIME** for existence; **UNKNOWN** for dependency effect | A culture/Q&A semantic dependency claim needs a controlled Desktop fixture; **park**. |
| Composite/remote model binding | The targeted resource search did not expose a sufficient connection-binding decision path. | **UNKNOWN** | Native/model-service behaviour may be opaque. Retain current cautious coverage wording; **investigate further** only when a customer artifact requires it. |
| Mobile layout | The resource surface contains mobile-specific state and layout handling. | **STRONGLY-SUPPORTED** | Existing PBI Assure mobile formatting-reference extraction remains the relevant bounded coverage. Position/visibility semantics were not established here; **park**. |

## Ranked next actions

This audit does **not** rerank the existing evidence-led roadmap. The list below is a discovery queue:
each implementation candidate remains behind the required Desktop fixture. The established KPI/measure
Detail Rows, formatting-reference, Auto Date/Time and effective-visibility work is unchanged.

| Rank | Action | Why now / not now |
|---:|---|---|
| 1 | **Create fixture first — table-owned Detail Rows** | Highest confidence new model capability and a direct extension of an explicit current boundary. Prove exact TMDL persistence and indirect usage before coding. |
| 2 | **Create fixture first — dynamic measure format string** | Potentially broad semantic-usage impact, but the authoring/persistence shape and independent-reference capability are unproved. |
| 3 | **Investigate further — visual calculations** | Strong runtime evidence of a visual feature, but no proof of a unique semantic graph dependency. |
| 4 | **Investigate further — generated-date variation/join metadata** | Useful explanatory metadata may exist, but the current exact marker is safer until a paired experiment distinguishes cases. |
| 5 | **Park — bookmark data-state graph edges** | The capture option is real, but effective replay/reachability makes an unconditional edge unsound. Revisit only for a concrete user case. |
| 6 | **Park / demand-led — sync slicer, translations, composite binding** | The discovery establishes concept existence or leaves the native path opaque, not a PBI Assure product rule. |

### Implementation threshold

**Implement now: none.** The conditional highest-value implementation candidate is parsing and analysing
a table-owned Detail Rows expression, but only after the rank-1 fixture demonstrates the persisted
Desktop form and the expected owner-to-reference reachability. This keeps the product's established
evidence discipline intact.

## Areas inspected but still unknown

- The exact current TMDL serialization and close/reopen behaviour for table-owned Detail Rows.
- Whether a dynamic measure format-string expression can uniquely reference a model object in Desktop.
- Whether visual calculations introduce model references absent from the visual field configuration.
- Effective field dependency semantics for bookmark replay, drillthrough and synchronized slicers.
- Native composite/DirectQuery/remote-model binding and query-state resolution where no sufficient
  WebView resource decision path is exposed.
- Whether variation and relationship date-join metadata safely distinguish generated from user-authored
  date structure.

## Audit boundaries

- No production code, tests, fixtures, schemas or existing decision records were changed.
- No resource absence was used as a negative product claim.
- No inferred behaviour was converted into a semantic dependency, root or user-facing finding.
- The recommended fixtures must use harmless sacrificial objects and explicit unused controls so that
  `IndirectlyUsed` versus `ApparentlyUnused` can be established unambiguously.
