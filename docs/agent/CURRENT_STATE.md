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
| Last verified product state | `dcbde4a` — *Align the definition-file registry with documented TMDL structure* |
| Working tree | Expected clean of tracked modifications. Untracked local review documents may be present |

`master` may have moved past that commit for documentation-only changes. Re-verify and update this
section whenever a commit changes build, test or behaviour — not for every commit.

## Verified at that commit

- `dotnet build PbiAssure.slnx` — **succeeded, 0 warnings, 0 errors** [verified]. `TreatWarningsAsErrors`
  is on, so warnings fail the build.
- `dotnet test PbiAssure.slnx` — **227 core + 2 privacy end-to-end tests passed**, 0 failed [verified].
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

Detection is **file level only** and changes **no** usage classification.

### Not implemented — do not assume otherwise

- `ClassificationConfidence` and uncertainty propagation
- Row-level security / `tablePermission` parsing
- Block-level detection (`kpi`, `detailRows`, `alternateOf`, model-side `variation`)
- Property-level detection
- Malformed-TMDL recovery
- `AnalysisScopeBoundary` catalog
- Any HTML or CSV surface for limitations — they reach the JSON inventory only
- Report-side PBIR limitation detection

## Blocking issue — propagation must not proceed yet

**[verified]** Scanning the Desktop-authored fixture `tests/fixtures/tab-order-states` produces three
limitations: `model.tmdl`, `database.tmdl` and `cultures/en-US.tmdl`. Power BI Desktop emits all three
for **every** semantic model, confirmed across all three Desktop-authored fixtures in this repository.

All three currently carry `DependencyEffectUnknown`. The proposed propagation rule caveats absence-state
objects whenever a model holds a limitation with `MayCreateDependencies` or `DependencyEffectUnknown` —
so propagating today would caveat **every object in every model**, destroying the signal.

Resolve first, by giving those three `NoKnownDependencyEffect` while keeping them classified
`SemanticNotYetAnalyzed`: still recorded and visible, but no longer qualifying anything. Evidence and
reasoning are in [../design/desktop-semantic-fixture-plan.md](../design/desktop-semantic-fixture-plan.md) §6.

## Current evidence gaps

| Gap | Status |
|---|---|
| Desktop-emitted paths and content for roles, perspectives, DAX user-defined functions | Awaiting the fixture below |
| Whether `dataSources.tmdl` is ever emitted by current Desktop | Not observed in any fixture [verified]; cause unknown [inferred] |
| Whether re-saving normalises semantic-model files | Unknown |
| Whether a *translated* culture file names model objects | Unknown; the default empty culture file does not [verified] |
| Whether current Desktop can still produce TMSL `model.bim` | Unknown |
| `PBI-ACCESS-001` real-world finding volume | **[inferred], never measured.** Do not change the rule on this inference alone |

### Settled, so it does not need re-investigating

`summarizeBy` (enum), `isKey` (boolean) and `dataCategory` (fixed vocabulary string) cannot reference
model objects [verified by Microsoft API reference]. `lineageTag` is the object's own identity, consumed
by *other* models for composite binding, so it is reference-free within a model [verified by
documentation]. The open sub-case is two semantic models in one project bound compositely.

## Immediate task — Desktop-authored fixture

A person must author a fixture in Power BI Desktop following [../design/desktop-semantic-fixture-plan.md](../design/desktop-semantic-fixture-plan.md): a
minimal model with two RLS roles, a perspective, and a DAX user-defined function, saved as a PBIP.

An agent must **not** hand-write these files. A hand-written PBIP proves nothing about what Desktop
emits, which is the entire purpose. See [DECISIONS.md](DECISIONS.md).

Expected destination: `tests/fixtures/desktop-semantic-constructs/`, with a README following
`tests/fixtures/tab-order-states/README.md`. Never commit `.pbi/cache.abf` — it can contain data and is
gitignored.

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
