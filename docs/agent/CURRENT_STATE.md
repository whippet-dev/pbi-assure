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
| Last verified product state | `PRECCOMMIT` — *Decide role dependency impact from actual role content* |
| Working tree | Expected clean of tracked modifications. Untracked local review documents may be present |

`master` may have moved past that commit for documentation-only changes. Re-verify and update this
section whenever a commit changes build, test or behaviour — not for every commit.

## Verified at that commit

- `dotnet build PbiAssure.slnx` — **succeeded, 0 warnings, 0 errors** [verified]. `TreatWarningsAsErrors`
  is on, so warnings fail the build.
- `dotnet test PbiAssure.slnx` — **306 core + 2 privacy end-to-end tests passed**, 0 failed [verified].
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
- **Artifact-sensitive limitation impact.** The registry gives the conservative construct-type default;
  where the scanner proves a *particular* role file contains nothing unanalysed that could reference a
  model object, the emitted limitation is narrowed to `NoKnownDependencyEffect`. The limitation is still
  emitted and the support state is unchanged. Coverage is affirmative: only constructs known to carry no
  object reference count as accounted for, so anything unrecognised keeps the conservative impact.

Limitation **detection** is file level only. Confidence is an orthogonal additive field, and no
user-facing surface consumes limitations or confidence yet.

Usage states change only through real dependency evidence entering the graph — as RLS parsing now does.
Nothing derives a usage state from the presence of a limitation.

### Not implemented — do not assume otherwise

- Block-level detection (`kpi`, `detailRows`, `alternateOf`, model-side `variation`)
- Property-level detection
- Malformed-TMDL recovery
- `AnalysisScopeBoundary` catalog
- Any HTML or CSV surface for limitations — they reach the JSON inventory only
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
| How a DAX user-defined function that references a model object serialises | **Open.** The fixture's function uses only its parameter |
| Whether current Desktop can still produce TMSL `model.bim` | Unknown |
| RLS forms beyond the two the fixture proves — cross-table filters, OLS column permissions, DirectQuery/Direct Lake roles | **Open.** Parser tests cover more shapes synthetically; only the two static/dynamic same-table forms are Desktop-verified |
| `PBI-ACCESS-001` real-world finding volume | **[inferred], never measured.** Do not change the rule on this inference alone |

### Settled, so it does not need re-investigating

- **Emitted paths for roles, perspectives, cultures and functions** — confirmed against real Desktop
  output by `DesktopSemanticConstructsFixtureTests`. No registry path needed correcting.
- **Re-saving does not normalise semantic-model files** — every definition file is byte-identical across
  a close/reopen/save round trip at Desktop 2.156.951.0 [verified by fixture].
- **Property-level precondition is cleared for four properties.** `summarizeBy` (enum), `isKey`
  (boolean) and `dataCategory` (fixed-vocabulary string) cannot reference model objects [verified by
  Microsoft API reference]. `lineageTag` is the object's own stable identity, consumed by *other* models
  for composite binding, so it is not an ordinary intra-model dependency [verified by documentation]. The
  remaining nuance is two semantic models bound compositely inside one project. **This clearance does not
  generalise to properties not on that list.**

## Immediate task

**Design how limitations and qualified confidence should appear to a user.**

The analysis side is complete for the constructs supported so far: limitations are detected, RLS
dependencies are analysed, and absence-state classifications are qualified. Nothing surfaces in HTML, CSV
or the browser app, so a user still cannot see that a conclusion was qualified. That presentation
deserves its own design pass rather than an ad-hoc badge — see
[../design/unsupported-construct-design.md](../design/unsupported-construct-design.md) §5 for the shape
already proposed, which has not been reviewed against the implemented behaviour.

The alternative candidate is perspective and function dependency parsing, which would shrink the
remaining caveat on the Desktop fixture the way RLS parsing just did.

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
