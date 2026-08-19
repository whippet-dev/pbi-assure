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
| Last verified product state | `ec6a3d0` — *Show analysis limitations and classification confidence in the HTML report* |
| Working tree | Expected clean of tracked modifications. Untracked local review documents may be present |

`master` may have moved past that commit for documentation-only changes. Re-verify and update this
section whenever a commit changes build, test or behaviour — not for every commit.

## Verified at that commit

- `dotnet build PbiAssure.slnx` — **succeeded, 0 warnings, 0 errors** [verified]. `TreatWarningsAsErrors`
  is on, so warnings fail the build.
- `dotnet test PbiAssure.slnx` — **381 core + 2 privacy end-to-end tests passed**, 0 failed [verified].
- CI (`.github/workflows/ci.yml`) — **green** [verified]. Runs restore, build, a Playwright Chromium
  install, then the whole solution test suite on `windows-latest`.
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
- **Row-level security table permission dependencies.** `definition/roles/<role>.tmdl` is parsed;
  each `tablePermission` filter's references resolve against the table the permission names — Desktop
  serialises them unqualified, `[Region]` not `Sales[Region]` — and become model-structure roots. An
  object required by a role filter is therefore `StructurallyRequired`, via ordinary traversal rather
  than any RLS-specific rule. Column permissions are **not** parsed, so roles stay `PartiallyAnalyzed`.
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
  **Qualified** link beside its status, pointing at that model's coverage block. Terminology:
  `QualifiedByLimitation` is shown as **Qualified**, support states as *Partially analysed* / *Not yet
  analysed*, and `DependencyImpact` as its consequence — *May affect usage classification* rather than
  *May create dependencies*. Counts only; there is no score, percentage or severity.

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
| **Where a UDF is called from outside the model definition** | **Open, and the reason functions still qualify.** Microsoft documents that visual calculations and report-level measures can call a user-defined function. PBI Assure parses neither — visual calculations not at all, and a report measure's expression is never DAX-extracted. A function that looks uncalled may be called from metadata nobody reads, and missing a consumer under-reports usage |
| Whether a UDF name can be namespaced with dots | Not observed. `DaxReferenceExtractor` does not treat `.` as an identifier character, so a dotted name would not tokenise as one identifier |
| Multi-parameter UDFs, other parameter type hints, `VAR`/`RETURN` or multi-line bodies | Not observed. Every function in `desktop-udf-references` is one line, and only one takes a parameter at all |
| Perspective `includeAll`, `perspectiveHierarchy` and perspective sets in real Desktop output | Implemented from Microsoft-documented syntax for the first two; no fixture emits any of them |
| Whether current Desktop can still produce TMSL `model.bim` | Unknown |
| RLS forms beyond the two the fixture proves — cross-table filters, OLS column permissions, DirectQuery/Direct Lake roles | **Open.** Parser tests cover more shapes synthetically; only the two static/dynamic same-table forms are Desktop-verified |
| `PBI-ACCESS-001` real-world finding volume | **[inferred], never measured.** Do not change the rule on this inference alone |

### Settled, so it does not need re-investigating

- **Emitted paths for roles, perspectives, cultures and functions** — confirmed against real Desktop
  output by `DesktopSemanticConstructsFixtureTests`, and again by `DesktopUdfReferencesFixtureTests`. No
  registry path needed correcting.
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

**Not yet chosen — see [HANDOVER.md](HANDOVER.md) for the ranked options.**

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
  disclosure is a native `<details>`. Muted marker text measures 6.46:1 against the card background, and
  at a 375px viewport the section fits with no horizontal overflow.
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
