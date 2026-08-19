# Unsupported-construct handling — slice 1 implementation handover

**Date:** 2026-08-19 · **Commit:** `06eaec2` on `master` · **Parent:** `b84d487`
**Implements:** slice 1 of `../design/unsupported-construct-design.md` §10.4 · **CI:** green
**Repository:** `whippet-dev/pbi-assure`

Standalone handover — no prior document or chat context is required to review or continue this work.

---

## 1. The problem this slice fixes

PBI Assure analyses a Power BI Project (PBIP) and classifies every semantic-model object as directly
used, indirectly used, structurally required, used only by an unused branch, or apparently unused.

**Before this change**, scanning a model whose `definition/roles.tmdl` contained
`tablePermission Sales = Sales[Region] = USERNAME()` produced [verified by CLI execution]:

```
Sales[Region] -> ApparentlyUnused
UnresolvedSemanticDependencies: []
AssuranceFindings:              (none)
```

A column whose only purpose is row-level security filtering was presented as a review candidate for
removal, and **nothing anywhere in the output recorded that `roles.tmdl` existed**.

The root cause is a disagreement between two enumerations that nothing compared [verified]:

- `ProjectScanner.CountDefinitionFiles` enumerates every `.tmdl`, `.bim` and `.pbism` file recursively
  inside each `.SemanticModel` directory, and counts them.
- `TmdlSemanticModelParser.Parse` opens only `definition/tables/*.tmdl`,
  `definition/relationships.tmdl` and `definition/expressions.tmdl`.

So the scanner demonstrably *knew* `roles.tmdl` was there — it contributed to `DefinitionFileCount=3` —
and then discarded its identity. This was not missing information; it was computed and thrown away.

**After this change**, the same scan produces [verified by CLI execution at `06eaec2`]:

```json
{
  "LimitationId": "PBI-LIMIT-MODEL-ROLE",
  "Cause": "ConstructNotSupported",
  "SupportState": "NotYetAnalyzed",
  "ConstructType": "role",
  "Scope": "SemanticModel",
  "SemanticModel": "Probe",
  "Table": null,
  "ObjectName": null,
  "ArtifactPath": "Probe.SemanticModel/definition/roles.tmdl",
  "EvidencePath": "(entire file)",
  "DependencyImpact": "MayCreateDependencies",
  "Concerns": ["Dependency", "Security"],
  "Reason": "Row-level security role definitions are not analysed by this version. Security filters can reference model objects that no report or measure uses."
}
```

Usage states are **unchanged** — `Sales[Region]` is still `ApparentlyUnused` [verified]. Slice 1 is
detection only; uncertainty propagation is a later slice (§8).

---

## 2. What changed

Six files, 736 insertions, 2 deletions.

### New files

| File | Purpose |
|---|---|
| `src/PbiAssure.Core/Inventory/AnalysisLimitation.cs` | The record and its vocabulary constants |
| `src/PbiAssure.Core/Scanning/SemanticDefinitionFileRegistry.cs` | The construct registry — single source of truth for classification |
| `src/PbiAssure.Core/Scanning/AnalysisLimitationDetector.cs` | Compares enumerated artifacts against the registry and emits limitations |
| `tests/PbiAssure.Core.Tests/AnalysisLimitationTests.cs` | 14 tests |

### Modified files

**`src/PbiAssure.Core/Inventory/ProjectInventory.cs`** — one additive property:

```csharp
public IReadOnlyList<AnalysisLimitation> AnalysisLimitations { get; init; } = [];
```

Added as an `init` property rather than a positional record parameter, matching the existing convention
for additive fields in this codebase (`ReportInventory.Theme`, `ReportInventory.ThemeReview`). This
avoids churning every construction site and keeps the change to a single line.

**`src/PbiAssure.Core/Scanning/ProjectScanner.cs`** — two changes:

1. Wire detection into `Scan`.
2. `CountDefinitionFiles` now takes its semantic-model extension set from
   `SemanticDefinitionFileRegistry.DefinitionExtensions` instead of a local literal, **so the set that is
   counted and the set that is classified cannot drift apart.** This is the mechanism the backbone
   invariant (§4) depends on; without it, adding an extension in one place would silently reintroduce the
   original defect.

---

## 3. Registry entries added

Patterns are relative to the `.SemanticModel` directory. Classification decides whether a limitation is
emitted; `SupportState`, `DependencyImpact` and `Concerns` travel onto the emitted record.

| Pattern | Match | Classification | Limitation? | DependencyImpact | Evidence |
|---|---|---|---|---|---|
| `definition/tables` | directory | `Analyzed` | No | `NoKnownDependencyEffect` | **[verified]** parsed |
| `definition/relationships.tmdl` | exact | `Analyzed` | No | `NoKnownDependencyEffect` | **[verified]** parsed |
| `definition/expressions.tmdl` | exact | `Analyzed` | No | `NoKnownDependencyEffect` | **[verified]** parsed |
| `definition/roles.tmdl` | exact | `SemanticNotYetAnalyzed` | **Yes** | `MayCreateDependencies` | **[verified]** not parsed; **[inferred]** that RLS filters reference columns |
| `definition/model.tmdl` | exact | `SemanticNotYetAnalyzed` | **Yes** | `DependencyEffectUnknown` | **[verified]** not parsed; **[inferred]** classification |
| `definition/perspectives.tmdl` | exact | `SemanticNotYetAnalyzed` | **Yes** | `DependencyEffectUnknown` | **[verified]** not parsed; **[inferred]** classification |
| `definition/cultures` | directory | `SemanticNotYetAnalyzed` | **Yes** | `DependencyEffectUnknown` | **[verified]** not parsed; **[inferred]** classification |
| `definition/database.tmdl` | exact | `Packaging` | No | `NoKnownDependencyEffect` | **[verified]** not parsed; **[inferred]** that it is packaging |
| `definition.pbism` | exact | `Packaging` | No | `NoKnownDependencyEffect` | **[verified]** manifest |
| *(fallback)* | — | `Unrecognized` | **Yes** | `MayCreateDependencies` | conservative default |

"**[verified]** not parsed" means a reference count across all of `src/PbiAssure.Core` returns zero for
that filename, and `TmdlSemanticModelParser.Parse` is confirmed by reading to open only the three paths
listed in §1.

### Entries that remain [inferred] — read this before extending

**`model.tmdl`, `database.tmdl`, `cultures/`, `perspectives.tmdl`.**

These four classifications rest on general knowledge of TMDL, **not** on Power BI Desktop-authored
fixtures. They are marked `[inferred]` in a comment block at the top of
`SemanticDefinitionFileRegistry.cs` and individually with `// [inferred]` comments, so a future reader
cannot mistake them for established facts. Specifically **not** established:

- whether `model.tmdl` or `database.tmdl` carry dependency-bearing content;
- whether `cultures/` and `perspectives.tmdl` are purely presentational.

`database.tmdl` is the one classified `Packaging` on inference, so it is the entry most likely to be
wrong in the harmful direction — if it turns out to carry dependency-bearing content, it is currently
silent. It should be the first of the four verified.

Changing any of these is a one-line edit to the registry. That is the point of centralising it.

The conservative default is deliberate: an unrecognised artifact is assumed capable of creating
dependencies, because an unnecessary caveat is recoverable whereas a confident deletion recommendation
for an object something uses is not.

---

## 4. Tests added

14 tests, all in `AnalysisLimitationTests.cs`. **No test encodes a known-wrong usage outcome.** In
particular there is no test asserting that an RLS-only column is `ApparentlyUnused`.

### Registry — totality and unambiguity

| Test | Asserts |
|---|---|
| `ClassificationIsTotalForAnyDefinitionPath` | Every path — including empty, deeply nested and invented ones — receives exactly one known classification |
| `NoTwoRegistryRulesShareAPattern` | No path can match two rules |
| `EveryRegistryRuleMatchesItsOwnRepresentativePath` | Each rule's pattern actually matches what it claims |
| `EveryRegistryRuleUsesKnownVocabulary` | No rule carries an unrecognised classification, support state or impact value, and every rule has an id, construct type and reason |

### Registry-to-behaviour consistency — the durable pattern

These iterate **the registry**, not a hardcoded construct list, so behaviour follows the registry
automatically when a construct moves between classifications:

| Test | Asserts |
|---|---|
| `RulesClassifiedAnalyzedProduceNoLimitation` | every `Analyzed` rule → no limitation |
| `RulesClassifiedPackagingProduceNoLimitation` | every `Packaging` rule → no limitation |
| `RulesClassifiedSemanticNotYetAnalyzedProduceALimitation` | every such rule → exactly one limitation, with matching id and construct type |
| `UnrecognizedDefinitionArtifactsProduceALimitation` | an invented filename → limitation with `Unrecognized` and `MayCreateDependencies` |

**Why this shape matters.** When row-level security support is implemented, the `roles.tmdl` rule moves
from `SemanticNotYetAnalyzed` to `Analyzed`, and the same two tests then assert the opposite behaviour —
**correctly, with no rename and no judgement call about whether the failure is progress or regression.**
An earlier draft of the design proposed `RoleMetadataDoesNotContributeDependencyEdges`; that was
withdrawn because it becomes false the day RLS lands.

### Backbone invariant

`EveryDefinitionArtifactIsClassifiedByTheConstructRegistry` — scans a model containing all nine known
artifact types plus an unrecognised one, then asserts in both directions:

- every definition artifact is limited **iff** its registry classification says it should be; and
- nothing is reported that is not part of the classified universe.

This is the test that makes silent disappearance a build failure rather than a discovery months later.

### Integration through `ProjectScanner`

| Test | Asserts |
|---|---|
| `RoleMetadataIsReportedAsALimitationAgainstItsOwnFile` | full record shape, including `Table`/`ObjectName` null and correct artifact path |
| `PackagingArtifactsDoNotProduceLimitations` | the `definition.pbism` / `database.tmdl` false-positive guard |
| `AProjectWithOnlySupportedArtifactsHasNoLimitations` | no noise on a clean project |
| `LimitationsAreScopedToTheSemanticModelThatContainsThem` | two-model project: only the model with `roles.tmdl` reports it |
| `AProjectWithNoSemanticModelHasNoLimitations` | report-only project is unaffected |

### Test-first evidence

The tests were written before the detection logic. With a stub detector returning an empty array, **6 of
the 14 failed** — the four behavioural/consistency tests, the backbone invariant, and totality. The
eight purely declarative registry tests passed immediately, as expected for data-only assertions. All 14
pass after implementation.

---

## 5. Verification

| Check | Result |
|---|---|
| `dotnet build PbiAssure.slnx` | **succeeded, 0 warnings, 0 errors** (`TreatWarningsAsErrors=true`) |
| `dotnet test PbiAssure.slnx` — core | **221 passed**, 0 failed (was 207; +14) |
| `dotnet test PbiAssure.slnx` — privacy E2E | **2 passed**, 0 failed |
| CI run `32231519323` | **green**, including the Playwright privacy step |
| End-to-end CLI against the RLS probe | limitation emitted, usage states unchanged |

### Formatting — pre-existing failure, not mixed into this commit

`dotnet format PbiAssure.slnx --verify-no-changes` **still fails with 24 whitespace errors**, all
pre-existing and none in any file touched by this change [verified — filtering the output for the six
changed files returns zero matches].

**Correction to an earlier report:** those 24 errors are split across **two** files, not one:

- `src/PbiAssure.Reporting/HtmlReportRenderer.ThemeReview.cs` — 21
- `src/PbiAssure.Core/Scanning/ThemeReviewAnalyzer.cs` — 3

An earlier note attributed all 24 to `HtmlReportRenderer.ThemeReview.cs`. This matters because
`CONTRIBUTING.md` instructs contributors to run `dotnet format --verify-no-changes`, so the command fails
on a clean checkout. It is a separate one-line-per-site cleanup and was deliberately kept out of this
commit.

---

## 6. Unexpected findings

**1. `CA1720` blocked a designed constant.** The design specified `AnalysisLimitationScopes` with
`SemanticModel`, `Table` and `Object`. `Object` triggers analyser rule CA1720 ("identifier contains type
name"), which is an error under `TreatWarningsAsErrors`. Since `Table` and `Object` are only used by
block-level detection — explicitly deferred — both were removed rather than suppressing the rule for
unused constants. Block-level detection should reintroduce them under non-conflicting names.

**2. `Classify` was not total.** The design asserts classification is total, but the first implementation
delegated to `ProjectFilePaths.Normalize`, which throws `ArgumentException` on an empty string. The
totality test caught this immediately. A guard returning the fallback for null/whitespace was added, so
the function is total as designed rather than total-in-documentation-only.

**3. `.bim` is not covered by any design rule.** `CountDefinitionFiles` includes `.bim` (the legacy JSON
model format) in the semantic-model extension set, but `../design/unsupported-construct-design.md` §7.5 does not
enumerate it. It therefore falls to the `Unrecognized` fallback and emits a limitation.

That behaviour is defensible — a `.bim` model is entirely unparsed, so a limitation is correct — but the
*classification* is arguably wrong: `.bim` is a **known** format that is not supported, which is closer
to `SemanticNotYetAnalyzed` than to "not recognised by this version". The user-visible outcome is
identical either way (both emit a limitation with `MayCreateDependencies`), so this was **not** treated as
a blocking semantic decision. **Flagged for the design to settle**; adding an explicit `.bim` rule is a
one-line registry change.

**4. Spelling divergence between code and documents.** The design documents use British spelling
(`Analysed`, `Unrecognised`). The codebase uses American spelling in identifiers throughout — `Analyze`,
`Analyzer`, `Normalize`, with zero `-ise`/`-yse` occurrences [verified]. Because these constants become
values in the JSON output contract, code convention won: `Analyzed`, `SemanticNotYetAnalyzed`,
`Unrecognized`. Prose in reasons and comments remains British, matching the rest of the repository's
user-facing text. This is a naming choice with no semantic content, recorded here so the divergence from
the design document is not mistaken for an error.

---

## 7. Deviations from `../design/unsupported-construct-design.md`

The implementation matches the approved design with three recorded deviations, none semantic:

| Deviation | Reason |
|---|---|
| Registry named `SemanticDefinitionFileRegistry`, with no `MatchKind` discriminator for `TableBlock` / `Property` | Design §7.3 specifies one registry covering all three match kinds. Slice 1 has only definition-file entries, so a discriminator with one value would be dead weight. Block- and property-level entries should extend or merge into this type. Structural only. |
| `AnalysisLimitationScopes.Table` and `.Object` omitted | CA1720, plus both are unused until block-level detection (§6.1) |
| Constant values use American spelling | Codebase convention (§6.4) |

Everything else follows the design as approved: the record shape from §7.2 including `Cause` separate
from `SupportState`; the four `DependencyImpact` values including the deliberately-unused
`MayInvalidateExistingEvidence`; the four-way classification from §7.5; the conservative fallback; and
the durable test pattern from §9.2–9.3.

Fields present but never populated in slice 1, retained because the design specifies the record and
changing a public record's shape later would churn the JSON contract: `Table`, `ObjectName` (block-level
detection), `AnalysisLimitationCauses.ParseFailed` (malformed recovery),
`ConstructSupportStates.PartiallyAnalyzed`, `ConstructDependencyImpacts.MayInvalidateExistingEvidence`
(explicitly required by design §7.4 to exist unused).

---

## 8. Explicitly deferred — not in this commit

Everything below was excluded by the slice definition and none of it is partially present:

- **Row-level security parsing** — `tablePermission` expressions, role DAX, dynamic RLS
- **`ClassificationConfidence`** and all uncertainty propagation — usage states are untouched
- **HTML and CSV surfaces** — limitations reach the JSON inventory only; no renderer was modified
- **Block-level detection** — `kpi`, `detailRows`, `alternateOf`, model-side `variation` inside parsed
  table files are still skipped silently
- **Property-level detection** — deliberately blocked on verifying whether `lineageTag`, `summarizeBy`,
  `dataCategory` and `isKey` are reference-free. `lineageTag` appears 33 times and `summarizeBy` 28 times
  in the small committed fixtures alone; shipping property-level detection before that verification would
  qualify essentially every model and destroy the signal this design exists to create
- **Malformed-TMDL recovery** — a single unparseable table file still crashes the scan with an unhandled
  `InvalidDataException` and a raw stack trace. Separate verified defect; `Program.cs` catches
  `ArgumentException`, `DirectoryNotFoundException`, `IOException` and `UnauthorizedAccessException` but
  not `InvalidDataException`
- **`AnalysisScopeBoundary`** — the static catalog for permanent input-format limits (Service role
  membership, workspace permissions, app audiences, sharing)
- **Report-side PBIR detection** — only `.SemanticModel` artifacts are classified
- **New assurance rules** — no rule was added; `AssuranceRuleCatalog` is unchanged
- **Accessibility work**, including `PBI-ACCESS-001`

---

## 9. Recommended next steps

1. **Verify the four `[inferred]` registry entries** with Power BI Desktop-authored fixtures, starting
   with `database.tmdl` (the only one classified `Packaging` on inference, so the only one currently
   silent if wrong).
2. **Answer the property-level precondition** — whether `lineageTag`, `summarizeBy`, `dataCategory` and
   `isKey` are reference-free. This gates both property-level detection and the propagation slice.
3. **Settle the `.bim` classification** (§6.3).
4. **Fix the malformed-TMDL crash** as an independent defect.
5. **Then** the propagation slice (`ClassificationConfidence` and the user-facing surface), and only
   after that, RLS support.

---

## 10. Is the implementation faithful to the design?

**Yes**, with the three structural deviations in §7 recorded and none of them semantic.

Slice 1 does what the design said it should: it is registry-driven, additive, changes no classification
and no output surface, is provable by an invariant that failed before the change and passes after, and
depends on none of the design's six open questions. The motivating defect — silence about skipped
metadata — is closed for the file-level case that RLS falls into.

The design's own judgement that slices 2+ should wait for Desktop verification of the descriptive
properties still stands and is unaffected by this work.

---

## Scope statement

One commit, `06eaec2`, pushed to `master`. Six files changed. No RLS implementation, no rules, no HTML or
CSV changes, no accessibility changes, no refactors beyond sharing the definition-extension set between
the counting and classifying code — which is the mechanism the backbone invariant depends on. The
pre-existing `dotnet format` failure was left untouched and is reported separately in §5.
