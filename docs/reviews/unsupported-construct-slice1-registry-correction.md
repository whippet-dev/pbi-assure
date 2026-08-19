# Slice 1 registry correction — aligning with documented TMDL/PBIP structure

**Date:** 2026-08-19 · **Commit:** `dcbde4a` on `master` · **Parent:** `06eaec2` · **CI:** green
**Repository:** `whippet-dev/pbi-assure`

Standalone handover — no prior document or chat context is required.

**Scope:** correcting and hardening the definition-file registry introduced in slice 1 against the
current documented TMDL and Power BI Project file structure. **No new feature was started.** The
registry / detector / `AnalysisLimitation` architecture from slice 1 is unchanged and was not rejected.

---

## 1. Background in one paragraph

PBI Assure scans a Power BI Project (PBIP) and classifies semantic-model objects as used or unused. Its
TMDL parser reads only some of the files in a semantic model, and before slice 1 the rest were skipped
silently — a column referenced only by a row-level security filter was reported as apparently unused with
no indication that security metadata had been skipped. Slice 1 (`06eaec2`) added a **construct registry**
that classifies every semantic-model definition artifact as `Analyzed`, `SemanticNotYetAnalyzed`,
`Packaging` or `Unrecognized`, and emits an `AnalysisLimitation` record for the latter two. It changes no
usage state; it only records what was not read.

Slice 1's registry was written against a **hand-authored probe**, not against Microsoft's documented file
layout. This change corrects that.

---

## 2. What the official documentation established

Two primary Microsoft Learn documents were fetched and read directly.

### 2.1 TMDL folder structure

Source: [Tabular Model Definition Language (TMDL)](https://learn.microsoft.com/en-us/analysis-services/tmdl/tmdl-overview)
(page last updated 2026-06-11).

The documented default folder structure has **one level of sub-folders**, each holding one file per
object:

```
TMDL/
├── cultures/          one file for each culture linguistic schema
├── perspectives/      one file for each perspective
├── roles/             one file for each role
├── tables/            one file for each table
├── relationships.tmdl one file for all relationships
├── functions.tmdl     one file for all functions (DAX user-defined functions)
├── expressions.tmdl   one file for all expressions
├── dataSources.tmdl   one file for all datasources
├── model.tmdl         one file for model definition
└── database.tmdl      one file for database definition
```

Three further points from the same document bear on classification:

- **`TablePermission.FilterExpression` is DAX.** Listed explicitly in the expression-language table. This
  raises "RLS filters can reference model objects" from inference to documented fact.
- **Perspectives hold object references.** The document lists "Table/column/measure reference in
  perspectives" under *Named object references*.
- **`model.tmdl` holds `ref` declarations** (`ref table`, `ref culture`, `ref role`) that preserve
  collection ordering — so it does reference other objects, though as ordering rather than as usage.

### 2.2 PBIP semantic model folder

Source: [Power BI Desktop project semantic model folder](https://learn.microsoft.com/en-us/power-bi/developer/projects/projects-dataset)
(page last updated 2026-05-30).

- **`definition.pbism`** — required. "Contains the overall definition of a semantic model and core
  settings", and specifies the supported definition format via its `version` property. **Settings, not
  model content** — so `Packaging` is correct.
- **`model.bim`** — required when saving in **TMSL** format. Contains the TMSL Database object
  definition of the whole model.
- **`definition/` folder** — required when saving in **TMDL** format. *Replaces* `model.bim`.
- **`TMDLScripts/` folder** — "Contains a file for each **TMDL view** script tab saved as a
  `[Tab name].tmdl` TMDL file." **Editor scratch content, not model definition.**

TMDL and TMSL are alternatives; a project uses one or the other, never both.

---

## 3. Which slice-1 assumptions were wrong

| Slice-1 rule | Status | Consequence |
|---|---|---|
| `definition/roles.tmdl` (exact) | **Wrong** | Documented shape is `roles/<role>.tmdl`. A real Desktop role file reached the `Unrecognized` fallback and **lost its Security concern classification** |
| `definition/perspectives.tmdl` (exact) | **Wrong** | Documented shape is `perspectives/<perspective>.tmdl`. Same fallback problem |
| `definition/cultures/*` (directory) | **Correct** | matched documentation already |
| `definition/tables/*` (directory) | **Correct** | |
| `definition/relationships.tmdl`, `definition/expressions.tmdl` | **Correct** | |
| `definition/dataSources.tmdl` | **Missing** | Known TMDL construct, reported as `Unrecognized` |
| `definition/functions.tmdl` | **Missing** | Known TMDL construct, reported as `Unrecognized` |
| `definition/database.tmdl` → `Packaging` | **Insufficiently evidenced** | Documented as part of the TMDL *database definition*, not PBIP packaging. Classifying it `Packaging` meant not reading it was **silent** |
| `definition.pbism` → `Packaging` | **Correct** | Documentation supports the settings/manifest distinction |
| `model.bim` | **Missing** | Fell to `Unrecognized`. A TMSL project's entire model is unread, which deserves a specific statement |
| `TMDLScripts/*.tmdl` | **Missing — false positive** | See §4 |

### 3.1 The unanticipated finding: `TMDLScripts/` produced false limitations

`ProjectScanner.CountDefinitionFiles` enumerates `.tmdl`, `.bim` and `.pbism` **recursively** inside each
`.SemanticModel` directory. `TMDLScripts/` lives inside that directory and its files carry the `.tmdl`
extension, so every TMDL view script tab was enumerated as a definition artifact and — matching no rule —
reported as an unanalysed semantic construct.

**Any project whose author had used the TMDL view would have produced spurious limitations**, one per
script tab. This was not predicted by the design and was found only by reading the PBIP documentation.

It is exactly the class of false positive the design warned about when it required packaging files to be
classified explicitly rather than ignored, and it validates that design decision: the fix is one registry
rule, not a special case in the detector.

---

## 4. Exact registry changes

| Construct | Pattern | Match | Classification | SupportState | DependencyImpact | Concerns |
|---|---|---|---|---|---|---|
| `table` | `definition/tables` | dir | `Analyzed` | `Analyzed` | `NoKnownDependencyEffect` | — |
| `relationship` | `definition/relationships.tmdl` | exact | `Analyzed` | `Analyzed` | `NoKnownDependencyEffect` | — |
| `expression` | `definition/expressions.tmdl` | exact | `Analyzed` | `Analyzed` | `NoKnownDependencyEffect` | — |
| **`role`** | **`definition/roles`** | **dir** *(was exact file)* | `SemanticNotYetAnalyzed` | `NotYetAnalyzed` | `MayCreateDependencies` | Dependency, **Security** |
| **`perspective`** | **`definition/perspectives`** | **dir** *(was exact file)* | `SemanticNotYetAnalyzed` | `NotYetAnalyzed` | **`MayCreateDependencies`** *(was Unknown)* | Dependency, Presentation |
| `culture` | `definition/cultures` | dir | `SemanticNotYetAnalyzed` | `NotYetAnalyzed` | `DependencyEffectUnknown` | Presentation |
| **`function`** *(new)* | `definition/functions.tmdl` | exact | `SemanticNotYetAnalyzed` | `NotYetAnalyzed` | `MayCreateDependencies` | Dependency |
| **`dataSource`** *(new)* | `definition/dataSources.tmdl` | exact | `SemanticNotYetAnalyzed` | `NotYetAnalyzed` | `DependencyEffectUnknown` | Dependency |
| `modelDefinition` | `definition/model.tmdl` | exact | `SemanticNotYetAnalyzed` | `NotYetAnalyzed` | `DependencyEffectUnknown` | Dependency |
| **`database`** | `definition/database.tmdl` | exact | **`SemanticNotYetAnalyzed`** *(was Packaging)* | `NotYetAnalyzed` | `DependencyEffectUnknown` | Dependency |
| **`tmslModelDefinition`** *(new)* | `model.bim` | exact | `SemanticNotYetAnalyzed` | `NotYetAnalyzed` | `MayCreateDependencies` | Dependency |
| **`tmdlEditorScript`** *(new)* | `TMDLScripts` | dir | `Packaging` | `NotYetAnalyzed` | `NoKnownDependencyEffect` | — |
| `semanticModelSettings` | `definition.pbism` | exact | `Packaging` | `NotYetAnalyzed` | `NoKnownDependencyEffect` | — |
| *(fallback)* | — | — | `Unrecognized` | `Unrecognized` | `MayCreateDependencies` | Dependency |

### Impact values justified by documentation, not guesswork

- **`role` → `MayCreateDependencies`** — `TablePermission.FilterExpression` is documented as DAX.
- **`perspective` → `MayCreateDependencies`** *(raised from `DependencyEffectUnknown`)* — documentation
  states perspectives hold table/column/measure references. The impact value answers only "can this
  construct reference model objects", which is now settled. **Whether a perspective reference should
  count as *usage* is a separate question and is deliberately not decided here** — that belongs to the
  propagation slice.
- **`function` → `MayCreateDependencies`** — `Function.Expression` is documented as DAX.
- **`dataSource`, `culture`, `model`, `database` → `DependencyEffectUnknown`** — no documentation
  reviewed establishes whether these reference model objects. Left honestly undetermined.
- **`model.bim` → `MayCreateDependencies`** — it is the entire model definition.

### The removed `definition/roles.tmdl` rule

Removed rather than retained. No evidence was found that Power BI emits a single root `roles.tmdl`;
documentation consistently describes one file per role under `roles/`. The original slice-1 probe used
that shape because it was **hand-written by us**, not observed from Desktop.

**Behaviour is still safe.** A hand-authored `definition/roles.tmdl` now reaches the fallback and is
reported as `Unrecognized` with `MayCreateDependencies` — verified by rescanning the original probe. It
loses only the specific `Security` concern tag; it does not become silent.

---

## 5. Match unambiguity — the test that did not prove what was claimed

The slice-1 handover stated `NoTwoRegistryRulesShareAPattern` proved no path can match two rules. **It did
not.** It grouped rules by pattern *string* and asserted no duplicates, which cannot detect a directory
rule overlapping an exact rule — for example `definition/tables` (directory) and
`definition/tables/Sales.tmdl` (exact) would both match the same path while having different pattern
strings. With `Classify` taking the first match, classification would then silently depend on declaration
order.

Replaced with two tests that exercise the **real matcher**:

- `NoPathMatchesMoreThanOneRegistryRule` — for each rule's own representative path, exactly one rule
  matches, and it is the expected one.
- `NoBoundaryPathMatchesMoreThanOneRegistryRule` — sixteen paths chosen to sit on the boundaries between
  rules (nested table paths, the legacy `roles.tmdl`, `model.bim`, `TMDLScripts/…`, an unknown file), each
  matching at most one rule.

This required exposing `SemanticDefinitionFileRegistry.MatchingRules(path)`, which returns every matching
rule; `Classify` now calls it and takes the first. No pattern engine was built.

---

## 6. Tests changed and added

Total in `AnalysisLimitationTests.cs`: **14 → 20**.

| Test | Change |
|---|---|
| `NoTwoRegistryRulesShareAPattern` | **Removed** — did not prove match unambiguity (§5) |
| `NoPathMatchesMoreThanOneRegistryRule` | **Added** |
| `NoBoundaryPathMatchesMoreThanOneRegistryRule` | **Added** |
| `RoleMetadataIsReportedAsALimitationAgainstItsOwnFile` | **Updated** to the documented `definition/roles/<role>.tmdl` shape |
| `EachRoleFileIsReportedSeparately` | **Added** — role-per-file means one limitation per role |
| `ATmslModelDefinitionIsReportedAsALimitation` | **Added** — `model.bim` |
| `EditorScriptsDoNotProduceLimitations` | **Added** — the `TMDLScripts` false-positive guard |
| `TheDatabaseDefinitionIsRecordedRatherThanTreatedAsPackaging` | **Added** |
| `DocumentedRootDefinitionFilesAreRecognizedRatherThanUnknown` | **Added** — `dataSources.tmdl`, `functions.tmdl` are not `Unrecognized` |
| `PackagingArtifactsDoNotProduceLimitations` | **Updated** — now covers `definition.pbism` only, since `database.tmdl` moved |
| `EveryDefinitionArtifactIsClassifiedByTheConstructRegistry` | **Updated** — fixture now uses the full documented TMDL shape plus an editor script and an unknown file |

The registry-to-behaviour consistency tests (`RulesClassifiedAnalyzedProduceNoLimitation`,
`…PackagingProduceNoLimitation`, `…SemanticNotYetAnalyzedProduceALimitation`) were **not** changed — they
iterate the registry, so they picked up every new and corrected rule automatically. That is the property
the durable-test design was chosen for, and this change exercised it for real.

### Teeth verified by mutation

Removing the `TMDLScripts` rule and rerunning produced exactly one failure —
`EditorScriptsDoNotProduceLimitations` — confirming the guard detects the regression it exists for. The
rule was restored and the file confirmed identical before committing.

---

## 7. Verification

| Check | Result |
|---|---|
| `dotnet build PbiAssure.slnx` | succeeded, **0 warnings, 0 errors** (`TreatWarningsAsErrors=true`) |
| Core tests | **227 passed**, 0 failed (was 221; +6) |
| Privacy E2E tests | **2 passed**, 0 failed |
| CI run `32233006456` | **green** |
| Diff scope | **2 files** — the registry and its tests. No detector, scanner, inventory, renderer or rule change |

### End-to-end checks

A probe using the documented shape (`definition/roles/RegionalManager.tmdl`, `definition/model.tmdl`, a
`TMDLScripts/Untitled 1.tmdl`) produced:

```
PBI-LIMIT-MODEL-SETTINGS  NotYetAnalyzed  DependencyEffectUnknown   .../definition/model.tmdl
PBI-LIMIT-MODEL-ROLE      NotYetAnalyzed  MayCreateDependencies     .../definition/roles/RegionalManager.tmdl
--- usage states ---
Sales[Region] -> ApparentlyUnused        (unchanged, as slice 1 requires)
```

The role file is correctly identified, the editor script produced **no** limitation, and usage
classification is untouched.

### Pre-existing formatting failure — unchanged

`dotnet format PbiAssure.slnx --verify-no-changes` still fails with **24 whitespace errors**, unchanged in
count and location, and **zero of them in any file touched by this change**:

- `src/PbiAssure.Reporting/HtmlReportRenderer.ThemeReview.cs` — 21
- `src/PbiAssure.Core/Scanning/ThemeReviewAnalyzer.cs` — 3

`CONTRIBUTING.md` instructs contributors to run this command, so it fails on a clean checkout. Separate
cleanup, deliberately not mixed into this commit.

---

## 8. What remains inferred, and what needs a Desktop fixture

The registry now distinguishes **three** evidence levels in its own source comments, because primary
documentation and observed Desktop output are not the same kind of evidence:

**[verified in this repository]** — which files `TmdlSemanticModelParser.Parse` actually opens: only
`definition/tables/*.tmdl`, `definition/relationships.tmdl`, `definition/expressions.tmdl`. No other
definition path is referenced anywhere in `PbiAssure.Core`.

**[verified by Microsoft primary documentation]** — the TMDL folder shape; that `definition.pbism` holds
settings; that `model.bim` is the TMSL alternative; that `TMDLScripts/` holds editor scripts; that
`TablePermission.FilterExpression` and `Function.Expression` are DAX; that perspectives hold object
references.

**[not verified by Desktop serialization]** — **everything about actual emitted output.** No
Desktop-authored fixture containing roles, perspectives, cultures, dataSources or functions exists in this
repository. Every path rule is matched against the *documented* shape.

### Remaining fixture requirements

A Power BI Desktop-authored PBIP saved in TMDL format containing at least: **row-level security roles**,
**a perspective**, **a non-default culture**, **DAX user-defined functions**, and ideally a project saved
in **TMSL** format for `model.bim`. Needed to confirm:

1. that emitted paths match the documented shape;
2. whether `cultures/`, `dataSources.tmdl`, `model.tmdl` and `database.tmdl` carry dependency-bearing
   content — all four are currently `DependencyEffectUnknown`;
3. whether `roles/` is the only location Desktop emits role definitions.

`database.tmdl` is no longer the highest-risk inference: moving it from `Packaging` to
`SemanticNotYetAnalyzed` means it now fails *loud* rather than silent if the inference is wrong.

### One naming observation

`TMDLScripts` is classified `Packaging`, which is the least-wrong of the four available classifications —
the classification's operative meaning is "not parsing this is not a limitation" — but "packaging" is a
slightly strained description of editor scratch content. If more such categories appear, the enum may
want a distinct value such as `NotModelDefinition`. Not changed now; behaviour is correct.

---

## 9. Is slice 1 now aligned with documented TMDL/PBIP structure?

**Yes, to the level documentation can establish.**

Every file and folder named in the two primary documents now has an explicit registry rule; no documented
construct reaches the `Unrecognized` fallback; the one false-positive source discovered
(`TMDLScripts/`) is closed; and the match-unambiguity claim is now actually proven rather than asserted.

**With one honest qualification:** alignment is with *documentation*, not with *observed Desktop output*.
The distinction is now explicit in the registry source so a future reader cannot mistake one for the
other. Until the fixtures in §8 exist, a discrepancy between documented and emitted paths would show up
as a construct reaching the `Unrecognized` fallback — which is reported and conservative, not silent.

The slice-1 architecture needed no change. Every correction here was a registry data change plus tests,
which is the outcome the registry was centralised to produce.

---

## 10. Still deferred — unchanged from slice 1

RLS / `tablePermission` parsing · `ClassificationConfidence` · uncertainty propagation · block-level
detection · property-level detection · malformed-TMDL recovery · HTML/CSV surfaces · Security tab ·
`AnalysisScopeBoundary` catalog · report-side PBIR detection · accessibility and `PBI-ACCESS-001`.

Usage states remain unchanged by any limitation, by design.

---

## Scope statement

One commit, `dcbde4a`, pushed to `master`; two files changed (registry and its tests); 256 insertions,
55 deletions. No production behaviour outside the registry was altered, no new feature was started, and
the pre-existing `dotnet format` failure was left untouched and reported separately in §7.
